using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

// 카메라 뷰포트, 중심점 타겟, 함선 크기 기반 줌 범위 자동 적용
public enum ECameraFocusTarget
{
    camera_focus_my_fleet,      // 우리 함대
    camera_focus_center,        // 중간 (우리 함대와 적 함대의 중간점)
    camera_focus_enemy_fleet    // 적 함대
}


public class CameraController : MonoSingleton<CameraController>
{
    [Header("Camera Settings")]
    public Camera m_targetCamera;
    private float m_rotationSpeed = 0.1f;
    private float m_zoomSpeed = 3f;
    [SerializeField] private float m_minZoom = 4f;
    [SerializeField] private float m_maxZoom = 40f; // 카메라 줌

    // Current camera state
    private Transform m_currentTarget; // (Optional) 움직이는 타겟을 따라가기 위한 Transform
    private Transform m_currentTargetBackup; // (Optional) 움직이는 타겟을 따라가기 위한 Transform
    private Vector3 m_targetPosition; // 카메라가 바라보는 목표 위치
    private Vector3 m_interpolatedTargetPosition; // 부드럽게 보간된 타겟 위치
    [SerializeField] private float m_currentZoom;
    public float CurrentZoom => m_currentZoom;
    private float m_currentRotationY = 20f;
    private float m_currentRotationX = 30f;

    // 모듈 포커싱용 목표 회전각/줌
    private bool m_hasTargetRotationY = false;
    private float m_targetRotationY = 0f;
    private bool m_hasTargetRotationX = false;
    private float m_targetRotationX = 0f;
    private bool m_hasTargetZoom = false;
    private float m_targetZoom = 0f;
    // center 모드 기준 줌: 두 함대가 보이는 최솟값
    private bool m_isCenterMode = false;
    private float m_centerModeBaseZoom = 0f;
    private const float k_centerModeExtraZoom = 20f;
    private const float k_rotateLerpSpeed = 4f;
    private const float k_rotateArriveThreshold = 0.5f;
    private const float k_zoomLerpSpeed = 8f;
    private const float k_zoomArriveThreshold = 0.01f;
    private const float k_positionLerpSpeed = 10f; // 줌(8f)보다 빠르게 — 위치가 먼저 도착해야 커브 방지

    // UIPanelSpace 활성화 시 true, 비활성화 시 false
    private bool m_shipSelectionEnabled = false;
    public void SetShipSelectionEnabled(bool enabled) { m_shipSelectionEnabled = enabled; }

    // 갤럭시 뷰 (탐사 탭)
    private bool m_isGalaxyView = false;
    public bool IsGalaxyView => m_isGalaxyView;
    private bool  m_isGalaxyViewAnimating = false;
    private bool  m_isEnteringGalaxy = false; // true=함대→갤럭시, false=갤럭시→함대
    private float m_galaxyViewAnimTimer   = 0f;
    [Header("Galaxy View Animation")]
    private float m_galaxyPreRotDuration  = 0.2f;  // 진입 시 회전 전용 구간 (위치 고정, m_galaxyViewAnimDuration 외)
    private float m_galaxyPreRotZoomMultiplyer  = 8f;  // 진입 시 회전 전용 구간 줌 곱하기
    private float m_galaxyViewAnimDuration  = 0.5f;
    private float m_galaxySlowPhaseRatio    = 0.8f;  // 느린 구간 비율: 이 t까지 천천히 이동 (예: 0.8 = 전체의 80%)
    private float m_galaxySlowPhaseProgress = 0.01f; // 느린 구간 끝에서 달성할 이동 진행도 (예: 0.01 = 전체 거리의 1%만 이동)

    // 애니메이션 시작 스냅샷
    private float   m_animStartRotX, m_animStartRotY, m_animStartZoom;
    private Vector3 m_animStartPos;
    private Vector3 m_galaxyTargetPos; // 갤럭시 뷰 목표 위치 (애니메이션 중 덮어써지지 않도록 별도 보관)
    // 갤럭시→함대 복귀 시 회전/줌 복귀 시작점 (갤럭시 현재 상태)
    private float   m_animExitRotX, m_animExitRotY, m_animExitZoom;

    private Coroutine m_fleetViewRestoreCoroutine;

    private Transform m_savedTarget = null;
    private float m_savedRotationX = 0f;
    private float m_savedRotationY = 0f;
    private float m_savedZoom = 0f;

    // 카메라 중심점 타겟
    private ECameraFocusTarget m_focusTarget = ECameraFocusTarget.camera_focus_my_fleet;
    public ECameraFocusTarget FocusTarget => m_focusTarget;
    private int m_enemyFleetFocusIndex = 0;

    // LayerMask
    private const int m_layerShield = 13;
    private LayerMask m_layerMaskShield = 1 << m_layerShield;

    // 현재 줌 범위를 적용한 대상 함선
    private SpaceShip m_zoomRangeSourceShip = null;

    protected override bool ShouldDontDestroyOnLoad => false;

    protected override void OnInitialize()
    {
        base.OnInitialize();
        if (m_targetCamera == null)
        {
            m_targetCamera = Camera.main;
            if (m_targetCamera == null)
                m_targetCamera = FindFirstObjectByType<Camera>();
        }

        m_currentZoom = (m_minZoom + m_maxZoom) / 2f;
        EnhancedTouchSupport.Enable();

        m_handleInputMouse = new HandleInputMouse(this);
        m_handleInputTouch = new HandleInputTouch(this);

        EventManager.Subscribe_SpaceShipSelected(OnSpaceShipSelectedForZoom);
        EventManager.Subscribe_ShipBodyChanged(OnShipBodyChangedForZoom);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        EventManager.Unsubscribe_SpaceShipSelected(OnSpaceShipSelectedForZoom);
        EventManager.Unsubscribe_ShipBodyChanged(OnShipBodyChangedForZoom);
    }

    // 함선 선택 시 해당 함선 기준 줌 범위 적용 (내 함대만)
    private void OnSpaceShipSelectedForZoom(SpaceShip ship)
    {
        if (ship == null || ship.m_ownerFleet == null || ObjectManager.Instance.IsEnemyOfMyTeam(ship.m_ownerFleet) == true) return;
        ApplyZoomRangeFromShip(ship);
    }

    // Body 교체 시 현재 줌 범위 기준 함선이면 갱신
    private void OnShipBodyChangedForZoom(SpaceShip ship)
    {
        if (ship != m_zoomRangeSourceShip) return;
        ApplyZoomRangeFromShip(ship);
    }

    // 함선의 첫 번째 ModuleBody에서 줌 범위를 읽어 적용
    public void ApplyZoomRangeFromShip(SpaceShip ship)
    {
        if (ship == null || ship.m_moduleBodys == null || ship.m_moduleBodys.Count == 0) return;
        ModuleBody body = ship.m_moduleBodys[0];
        if (body == null) return;

        m_zoomRangeSourceShip = ship;
        m_minZoom = body.m_cameraMinZoom;
        m_maxZoom = body.m_cameraMaxZoom;

        // 현재 줌이 새 범위를 벗어나면 clamp
        float clampedZoom = Mathf.Clamp(m_currentZoom, m_minZoom, m_maxZoom);
        if (Mathf.Abs(clampedZoom - m_currentZoom) > 0.01f)
            SetTargetZoom(clampedZoom);
    }

    public void UpdateCameraTransform()
    {
        if (m_targetCamera == null) return;
	
        // m_currentTarget 이 계속 움직이기 때문, 이벤트로는 안됨
        if (m_currentTarget != null && m_focusTarget == ECameraFocusTarget.camera_focus_my_fleet)
            m_targetPosition = m_currentTarget.position;

        // Center 모드: 매 프레임 두 함대의 중간점을 갱신 (기함이 타겟 중인 적 함대 기준)
        if (m_focusTarget == ECameraFocusTarget.camera_focus_center && m_currentTarget == null)
        {
            var objMgr = ObjectManager.Instance;
            SpaceFleet myFleet = objMgr != null ? objMgr.GetMyFleet() : null;
            if (myFleet != null)
                m_targetPosition = (myFleet.transform.position + GetCenterModeEnemyPosition(objMgr, myFleet)) * 0.5f;
        }

        bool galaxyViewJustSettled = false;
        if (m_isGalaxyViewAnimating == true)
        {
            m_galaxyViewAnimTimer += Time.unscaledDeltaTime;

            if (m_isEnteringGalaxy == true)
            {
                if (m_galaxyViewAnimTimer < m_galaxyPreRotDuration)
                {
                    // Pre-Phase: 위치 고정 + 각도 전환 + 줌을 함대뷰 최대치로 확장 (시야 확보)
                    float rotT = Mathf.Clamp01(m_galaxyViewAnimTimer / m_galaxyPreRotDuration);
                    m_currentRotationX = Mathf.LerpAngle(m_animStartRotX, m_targetRotationX, rotT);
                    m_currentRotationY = Mathf.LerpAngle(m_animStartRotY, m_targetRotationY, rotT);
                    m_currentZoom      = Mathf.Lerp(m_animStartZoom, m_maxZoom * m_galaxyPreRotZoomMultiplyer, rotT);
                    // m_interpolatedTargetPosition 변경 없음 (함대 위치 고정)
                }
                else
                {
                    // Main Phase: 각도 유지 + 줌 전환 + 위치 이동
                    m_currentRotationX = m_targetRotationX;
                    m_currentRotationY = m_targetRotationY;

                    float mainT = Mathf.Clamp01((m_galaxyViewAnimTimer - m_galaxyPreRotDuration) / m_galaxyViewAnimDuration);
                    float ct    = GalaxyEasedT(mainT);
                    m_currentZoom = Mathf.Lerp(m_maxZoom * m_galaxyPreRotZoomMultiplyer, m_targetZoom, ct);
                    float newX  = Mathf.Lerp(m_animStartPos.x, m_galaxyTargetPos.x, ct);
                    float newY  = Mathf.Lerp(m_animStartPos.y, m_galaxyTargetPos.y, ct);
                    float newZ  = Mathf.Lerp(m_animStartPos.z, m_galaxyTargetPos.z, ct);
                    m_interpolatedTargetPosition = new Vector3(newX, newY, newZ);
                }
            }
            else
            {
                Vector3 fleetPos = m_savedTarget != null ? m_savedTarget.position : m_targetPosition;

                if (m_galaxyViewAnimTimer < m_galaxyViewAnimDuration)
                {
                    // Main Phase: 갤럭시 각도 유지 + 줌 m_maxZoom으로 축소 + 위치 이동
                    float mainT = Mathf.Clamp01(m_galaxyViewAnimTimer / m_galaxyViewAnimDuration);
                    float ct    = 1f - GalaxyEasedT(1f - mainT); // 초반 빠르고 마지막 느림

                    m_currentRotationX = m_animExitRotX;
                    m_currentRotationY = m_animExitRotY;
                    m_currentZoom      = Mathf.Lerp(m_animExitZoom, m_maxZoom * m_galaxyPreRotZoomMultiplyer, ct);

                    float newX = Mathf.Lerp(m_animStartPos.x, fleetPos.x, ct);
                    float newY = Mathf.Lerp(m_animStartPos.y, fleetPos.y, ct);
                    float newZ = Mathf.Lerp(m_animStartPos.z, fleetPos.z, ct);
                    m_interpolatedTargetPosition = new Vector3(newX, newY, newZ);
                }
                else
                {
                    // Post-Phase: 함대 위치 고정 + 각도/줌 원래 함대뷰로 복귀
                    float postT = Mathf.Clamp01((m_galaxyViewAnimTimer - m_galaxyViewAnimDuration) / m_galaxyPreRotDuration);
                    m_currentRotationX = Mathf.LerpAngle(m_animExitRotX, m_targetRotationX, postT);
                    m_currentRotationY = Mathf.LerpAngle(m_animExitRotY, m_targetRotationY, postT);
                    m_currentZoom      = Mathf.Lerp(m_maxZoom * m_galaxyPreRotZoomMultiplyer, m_targetZoom, postT);
                    // m_interpolatedTargetPosition 변경 없음 (함대 위치 고정)
                }
            }

            // 진입/복귀 모두 preRotDuration + mainDuration
            float totalDuration = m_galaxyPreRotDuration + m_galaxyViewAnimDuration;
            if (m_galaxyViewAnimTimer >= totalDuration)
            {
                m_isGalaxyViewAnimating = false;
                if (m_isEnteringGalaxy == true)
                    galaxyViewJustSettled = true;
                else
                    m_inputEnabled = true;
            }
        }
        else
        {
            // 일반 뷰 — 기존 지수 보간 (모듈 포커싱 등)
            if (m_hasTargetRotationY == true)
            {
                m_currentRotationY = Mathf.LerpAngle(m_currentRotationY, m_targetRotationY, k_rotateLerpSpeed * Time.unscaledDeltaTime);
                if (Mathf.Abs(Mathf.DeltaAngle(m_currentRotationY, m_targetRotationY)) < k_rotateArriveThreshold)
                {
                    m_currentRotationY = m_targetRotationY;
                    m_hasTargetRotationY = false;
                }
            }
            if (m_hasTargetRotationX == true)
            {
                m_currentRotationX = Mathf.LerpAngle(m_currentRotationX, m_targetRotationX, k_rotateLerpSpeed * Time.unscaledDeltaTime);
                if (Mathf.Abs(Mathf.DeltaAngle(m_currentRotationX, m_targetRotationX)) < k_rotateArriveThreshold)
                {
                    m_currentRotationX = m_targetRotationX;
                    m_hasTargetRotationX = false;
                }
            }
            if (m_hasTargetZoom == true)
            {
                m_currentZoom = Mathf.Lerp(m_currentZoom, m_targetZoom, k_zoomLerpSpeed * Time.unscaledDeltaTime);
                if (Mathf.Abs(m_currentZoom - m_targetZoom) < k_zoomArriveThreshold)
                {
                    m_currentZoom = m_targetZoom;
                    m_hasTargetZoom = false;
                }
            }
            m_interpolatedTargetPosition = Vector3.Lerp(m_interpolatedTargetPosition, m_targetPosition, k_positionLerpSpeed * Time.unscaledDeltaTime);

            // 카메라 이동 완료 시 입력 자동 활성화 (갤럭시 뷰 중에는 입력 유지 차단)
            if (m_inputEnabled == false && m_isGalaxyView == false
                && m_hasTargetRotationX == false && m_hasTargetRotationY == false && m_hasTargetZoom == false)
                m_inputEnabled = true;
        }

        // 1. 회전 각도를 라디안으로 변환 (RotY=0 → +Z 방향 기준, +180° 오프셋)
        float radiansY = (m_currentRotationY + 180f) * Mathf.Deg2Rad;
        float radiansX = m_currentRotationX * Mathf.Deg2Rad;
        // 2. 구면 좌표계(Spherical Coordinates)로 카메라 위치 계산
        float horizontalDistance = m_currentZoom * Mathf.Cos(radiansX);
        Vector3 rotatedOffset = new(
            Mathf.Sin(radiansY) * horizontalDistance,
            m_currentZoom * Mathf.Sin(radiansX),
            Mathf.Cos(radiansY) * horizontalDistance
        );

        // 3. 보간된 타겟 위치 기준으로 카메라 배치
        m_targetCamera.transform.position = m_interpolatedTargetPosition + rotatedOffset;
        m_targetCamera.transform.LookAt(m_interpolatedTargetPosition);

        if (galaxyViewJustSettled == true)
            EventManager.TriggerGalaxyViewSettled();
    }

    private bool m_inputEnabled = true;

    // Input handling
    private HandleInputMouse m_handleInputMouse;
    private HandleInputTouch m_handleInputTouch;

    private Vector3 m_startTouchPosition;
    private float m_startRotationY;
    private float m_startRotationX;

    private void Update()
    {
        HandleInput();
        UpdateCameraTransform();
    }

    private void HandleInput()
    {
        if (m_inputEnabled == false) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        m_handleInputMouse.Process();
#elif UNITY_ANDROID || UNITY_IOS
        m_handleInputTouch.Process();
#endif
    }

    public void OnDragStart(Vector3 position)
    {
        m_startTouchPosition = position;
        m_startRotationY = m_currentRotationY;
        m_startRotationX = m_currentRotationX;
        m_hasTargetRotationY = false;
        m_hasTargetRotationX = false;
        m_hasTargetZoom = false;
    }

    public void OnDragMove(Vector3 position)
    {
        Vector3 delta = (position - m_startTouchPosition) * m_rotationSpeed;
        m_currentRotationY = m_startRotationY + delta.x;
        m_currentRotationX = Mathf.Clamp(m_startRotationX - delta.y, -80f, 80f);
    }

    // 2터치→1터치 전환 시: 플래그 초기화 없이 기준점만 갱신
    public void ResetDragOrigin(Vector3 position)
    {
        m_startTouchPosition = position;
        m_startRotationY = m_currentRotationY;
        m_startRotationX = m_currentRotationX;
    }
    

    // 3D 클릭으로 내 함선/모듈 선택. 빈공간이면 EmptySpaceTapped 발행
    public void HandleModuleSelection(Vector3? screenPosition = null)
    {
        if (!m_shipSelectionEnabled) return;

        LayerMask pickMask = ~m_layerMaskShield;
        if (!GetCameraRaycast(out RaycastHit hit, pickMask, 3000f, screenPosition))
        {
            EventManager.Trigger_EmptySpaceTapped();
            return;
        }

        SpaceShip ship = hit.collider.GetComponentInParent<SpaceShip>();
        if (ship == null || ship.m_ownerFleet == null || ObjectManager.Instance.IsEnemyOfMyTeam(ship.m_ownerFleet))
        {
            EventManager.Trigger_EmptySpaceTapped();
            return;
        }

        // 내함대 보기
        SetCameraFocusTarget(ECameraFocusTarget.camera_focus_my_fleet);

        // 함선 선택 이벤트 (UITabShip 에서 동일 함선 중복 체크)
        EventManager.Trigger_SpaceShipSelected(ship);

        // 모듈도 감지되었으면 모듈 선택
        ModuleBase module = hit.collider.GetComponentInParent<ModuleBase>();
        if (module != null)
            EventManager.TriggerSpaceShipModuleSelected(ship, module);
    }

    public void RotateCamera(float deltaRotationY, float deltaRotationX)
    {
        m_currentRotationY += deltaRotationY;
        m_currentRotationX = Mathf.Clamp(m_currentRotationX + deltaRotationX, -80f, 80f);
    }

    // UI 모듈 버튼 선택 시 슬롯에 미리 설정된 카메라 회전/줌으로 이동
    // m_cameraRotationY/X는 함선 기준 상대각 → 함선의 현재 world Y 회전을 더해 절대각으로 변환
    public void FocusOnModuleIfHidden(ModuleSlot moduleSlot)
    {
        if (moduleSlot == null) return;

        float shipYaw = 0f;
        SpaceShip ship = moduleSlot.GetComponentInParent<SpaceShip>();
        if (ship != null)
            shipYaw = ship.transform.eulerAngles.y;

        m_hasTargetRotationY = true;
        // +180f: 슬롯 rotY=0 → 함선 정면에서 바라보는 각도 (카메라 수식의 +180° 오프셋 보정)
        m_targetRotationY = moduleSlot.m_cameraRotationY + shipYaw + 180f;
        m_hasTargetRotationX = true;
        m_targetRotationX = moduleSlot.m_cameraRotationX;
        m_hasTargetZoom = true;
        m_targetZoom = Mathf.Clamp(moduleSlot.m_cameraZoom, m_minZoom, m_maxZoom);
    }

    // 카메라 viewport width를 즉시 설정 (UIPanelSpace에서 레이아웃 애니메이션 구동용)
    public void SetViewportWidth(float width)
    {
        if (m_targetCamera == null) return;
        Rect r = m_targetCamera.rect;
        r.width = Mathf.Clamp01(width);
        m_targetCamera.rect = r;
        RefreshCenterModeZoom();
    }

    public float GetViewportWidth()
    {
        return m_targetCamera != null ? m_targetCamera.rect.width : 1f;
    }

    public void ZoomCamera(float deltaZoom)
    {
        if (m_isGalaxyView == true) return;

        m_hasTargetZoom = false;

        if (m_isCenterMode == true)
        {
            float cMin = m_centerModeBaseZoom;
            float cMax = m_centerModeBaseZoom + k_centerModeExtraZoom;
            m_currentZoom = Mathf.Clamp(m_currentZoom + deltaZoom * m_zoomSpeed, cMin, cMax);
            return;
        }

        if (m_targetCamera != null && m_targetCamera.orthographic)
        {
            m_targetCamera.orthographicSize = Mathf.Clamp(
                m_targetCamera.orthographicSize + deltaZoom * m_zoomSpeed,
                m_minZoom, m_maxZoom);
        }
        else
        {
            m_currentZoom = Mathf.Clamp(m_currentZoom + deltaZoom * m_zoomSpeed, m_minZoom, m_maxZoom);
        }
    }

    public void SetZoom(float normalizedZoom)
    {
        m_currentZoom = Mathf.Lerp(m_minZoom, m_maxZoom, normalizedZoom);
    }

    // 줌 목표값을 직접 설정 (Lerp로 부드럽게 이동)
    public void SetTargetZoom(float zoom)
    {
        m_hasTargetZoom = true;
        if (m_isCenterMode == true)
            m_targetZoom = zoom;
        else
            m_targetZoom = Mathf.Clamp(zoom, m_minZoom, m_maxZoom);
    }

    // center 모드 진입: 기준 줌 계산 후 m_isCenterMode 활성화
    public void EnterCenterMode()
    {
        float baseZoom = CalcCenterZoom();
        m_isCenterMode = true;
        m_centerModeBaseZoom = baseZoom;
        m_hasTargetZoom = true;
        m_targetZoom = baseZoom;
    }

    // center 모드 뷰포트 변경 시 기준 줌 갱신
    public void RefreshCenterModeZoom()
    {
        if (m_isCenterMode == false) return;
        float baseZoom = CalcCenterZoom();
        m_centerModeBaseZoom = baseZoom;
        if (m_currentZoom < baseZoom)
        {
            m_hasTargetZoom = true;
            m_targetZoom = baseZoom;
        }
    }

    public void ExitCenterMode()
    {
        m_isCenterMode = false;
        m_centerModeBaseZoom = 0f;
    }

    // 현재 두 함대 거리·FoV·뷰포트 폭 기반으로 센터 모드 적정 줌 계산
    public float CalcCenterZoom()
    {
        var objMgr = ObjectManager.Instance;
        if (objMgr == null || objMgr.GetMyFleet() == null) return m_currentZoom;

        float dist = Vector3.Distance(objMgr.GetMyFleet().transform.position, objMgr.GetEnemySpawnPosition());
        // camera.aspect는 rect.width를 이미 반영 (pixelWidth/pixelHeight 기준)
        float aspect = m_targetCamera != null ? m_targetCamera.aspect : (16f / 9f);
        float vFovRad = (m_targetCamera != null ? m_targetCamera.fieldOfView : 60f) * Mathf.Deg2Rad;

        float hFovRad = 2f * Mathf.Atan(Mathf.Tan(vFovRad * 0.5f) * aspect);

        // 수직 앙각(rotationX)만큼 수평 거리가 줄어드는 보정
        float cosX = Mathf.Max(Mathf.Cos(m_currentRotationX * Mathf.Deg2Rad), 0.1f);
        float zoomNeeded = (dist * 0.5f) / (Mathf.Tan(hFovRad * 0.5f) * cosX);

        // center 모드는 함선 zoom 범위 제한(m_maxZoom)과 무관하게 두 함대가 보이는 값을 우선
        float rawZoom = zoomNeeded * 1.3f;
        return Mathf.Max(rawZoom, m_minZoom);
    }

    // Raycast for object selection
    public bool GetCameraRaycast(out RaycastHit hit, LayerMask layerMask = default, float maxDistance = 1000f, Vector3? screenPosition = null)
    {
        if (m_targetCamera == null)
        {
            hit = new RaycastHit();
            return false;
        }

        Vector3 inputPos = screenPosition ?? Vector3.zero;
        Ray ray = m_targetCamera.ScreenPointToRay(inputPos);
        if (layerMask == default)
            return Physics.Raycast(ray, out hit, maxDistance);
        else
            return Physics.Raycast(ray, out hit, maxDistance, layerMask);
    }

    // Transform을 타겟으로 설정 (움직이는 오브젝트 추적용)
    public void SetTargetOfCameraController(Transform target)
    {
        if (target == null) return;

        // 처음 설정할 때는 즉시 위치 동기화
        if (m_currentTarget == null && m_targetPosition == Vector3.zero)
            m_targetPosition = target.position;

        m_currentTarget = target;
        m_targetPosition = target.position;

        EventManager.TriggerCameraFocusTargetChanged(ECameraFocusTarget.camera_focus_my_fleet);
    }

    // 기함이 타겟 중인 적 함선이 속한 함대 위치 반환 (타겟 없으면 레거시 스폰 위치로 폴백)
    private Vector3 GetCenterModeEnemyPosition(ObjectManager objMgr, SpaceFleet myFleet)
    {
        SpaceShip flagship = myFleet.GetFlagship();
        if (flagship != null && flagship.m_targetShip != null && flagship.m_targetShip.m_ownerFleet != null)
            return flagship.m_targetShip.m_ownerFleet.transform.position;
        return objMgr.GetEnemySpawnPosition();
    }

    // 현재 FocusTarget 기준 월드 위치 반환 (상태 변경 없음)
    public Vector3 GetFocusTargetPosition()
    {
        var objMgr = ObjectManager.Instance;
        var myFleet = objMgr != null ? objMgr.GetMyFleet() : null;
        if (m_focusTarget == ECameraFocusTarget.camera_focus_enemy_fleet && objMgr != null)
            return objMgr.GetEnemySpawnPosition();
        if (m_focusTarget == ECameraFocusTarget.camera_focus_center && objMgr != null && myFleet != null)
            return (myFleet.transform.position + GetCenterModeEnemyPosition(objMgr, myFleet)) * 0.5f;
        // camera_focus_my_fleet
        if (m_currentTarget != null) return m_currentTarget.position;
        return myFleet != null ? myFleet.transform.position : m_targetPosition;
    }

    // 카메라를 현재 타겟 위치로 즉시 스냅 (스테이지 입장 등 순간이동 시 사용)
    // my_fleet: m_currentTarget이 없으면 함대 transform으로 재연결 후 스냅
    // center/enemy: EnemyFleetFocusPosition 기준으로 스냅 (적 없어도 스폰 위치 사용)
    public void SnapToTarget()
    {
        var objMgr = ObjectManager.Instance;
        var myFleet = objMgr != null ? objMgr.GetMyFleet() : null;
        switch (m_focusTarget)
        {
            case ECameraFocusTarget.camera_focus_my_fleet:
                if (m_currentTarget == null && myFleet != null)
                    m_currentTarget = myFleet.transform;
                if (m_currentTarget != null)
                    m_targetPosition = m_currentTarget.position;
                break;
            case ECameraFocusTarget.camera_focus_enemy_fleet:
                if (objMgr != null)
                    m_targetPosition = objMgr.GetEnemySpawnPosition();
                break;
            case ECameraFocusTarget.camera_focus_center:
                if (objMgr != null && myFleet != null)
                    m_targetPosition = (myFleet.transform.position + objMgr.GetEnemySpawnPosition()) * 0.5f;
                break;
        }
        m_interpolatedTargetPosition = m_targetPosition;
    }

    // Vector3 위치를 타겟으로 설정 (고정된 위치용)
    public void SetTargetPosition(Vector3 position)
    {
        // 처음 설정할 때는 즉시 위치 동기화
        if (m_currentTarget == null && m_targetPosition == Vector3.zero)
            m_interpolatedTargetPosition = position;

        m_currentTarget = null; // Transform 추적 해제
        m_targetPosition = position;
    }

    // 현재 타겟 위치 가져오기
    public Vector3 GetTargetPosition()
    {
        return m_targetPosition;
    }

    // 갤럭시 뷰 진입 — 현재 상태에서 목표까지 고정 시간 선형 이동
    public void EnterGalaxyView(Vector3 targetPos, float zoom, float rotX, float rotY)
    {
        if (m_isGalaxyView == true) return;
        m_isGalaxyView = true;
        m_isEnteringGalaxy = true;
        m_inputEnabled = false;

        m_savedTarget = m_currentTarget;
        m_savedRotationX = m_currentRotationX;
        m_savedRotationY = m_currentRotationY;
        m_savedZoom = m_currentZoom;

        m_currentTarget  = null;
        m_targetPosition = targetPos;
        m_galaxyTargetPos = targetPos;
        m_targetRotationX   = Mathf.Clamp(rotX, -80f, 80f);
        m_targetRotationY   = rotY;
        m_targetZoom        = zoom;

        StartGalaxyViewAnimation();
    }

    // Zone 그룹 탭 선택 시 해당 앵커로 포커스 (갤럭시 뷰 중에만 동작) — 고정 시간 선형 이동
    public void FocusOnZoneAnchor(Vector3 zoneWorldPos, float zoom, float rotX, float rotY)
    {
        if (m_isGalaxyView == false) return;

        m_targetPosition    = zoneWorldPos;
        m_targetZoom        = zoom;
        m_targetRotationX   = Mathf.Clamp(rotX, -80f, 80f);
        m_targetRotationY   = rotY;

        StartGalaxyViewAnimation();
    }

    private void StartGalaxyViewAnimation()
    {
        m_animStartRotX = m_currentRotationX;
        m_animStartRotY = m_currentRotationY;
        m_animStartZoom = m_currentZoom;
        m_animStartPos  = m_interpolatedTargetPosition;
        m_galaxyViewAnimTimer   = 0f;
        m_isGalaxyViewAnimating = true;
    }

    // 느린 구간(0~m_galaxySlowPhaseRatio)에서 m_galaxySlowPhaseProgress까지만 이동,
    // 이후 구간에서 나머지를 완료
    private float GalaxyEasedT(float t)
    {
        if (t <= m_galaxySlowPhaseRatio)
        {
            float localT = t / m_galaxySlowPhaseRatio;
            return localT * m_galaxySlowPhaseProgress;
        }
        else
        {
            float localT = (t - m_galaxySlowPhaseRatio) / (1f - m_galaxySlowPhaseRatio);
            return m_galaxySlowPhaseProgress + localT * (1f - m_galaxySlowPhaseProgress);
        }
    }

// 탐사뷰 종료하면서 저장된 함대 위치+각도+줌으로 복귀
    public void ExitGalaxyView(Vector3 position, bool ignoreFleetTarget = false)
    {
        if (m_isGalaxyView == false) return;
        m_isGalaxyView = false;
        m_isEnteringGalaxy = false;
        m_inputEnabled = false;

        // Phase3 회전/줌 복귀 시작점으로 현재 갤럭시 상태 저장
        m_animExitRotX = m_currentRotationX;
        m_animExitRotY = m_currentRotationY;
        m_animExitZoom = m_currentZoom;

        // m_currentTarget은 Phase3 실시간 추적용으로 null 유지, m_savedTarget 별도 보존
        // ignoreFleetTarget=true 시 Main Phase가 m_targetPosition(새 존 위치)을 사용하도록 null 처리
        if (ignoreFleetTarget == true)
            m_savedTarget = null;
        m_currentTarget  = null;
        m_targetPosition = position;

        m_targetRotationX = m_savedRotationX;
        m_targetRotationY = m_savedRotationY;
        m_targetZoom      = m_savedZoom;

        StartGalaxyViewAnimation();
        StartFleetViewRestoreCoroutine();
    }

    private void StartFleetViewRestoreCoroutine()
    {
        if (m_fleetViewRestoreCoroutine != null) StopCoroutine(m_fleetViewRestoreCoroutine);
        m_fleetViewRestoreCoroutine = StartCoroutine(WaitForFleetViewRestored());
    }

    private System.Collections.IEnumerator WaitForFleetViewRestored()
    {
        yield return new UnityEngine.WaitUntil(() => m_inputEnabled == true);
        m_fleetViewRestoreCoroutine = null;
        EventManager.TriggerFleetViewRestored();
    }

    // 카메라 중심점 전환 (적함대, 중간, 우리함대)
    public void SetCameraFocusTarget(ECameraFocusTarget focusTarget)
    {
        if (focusTarget == ECameraFocusTarget.camera_focus_enemy_fleet)
        {
            var objMgr = ObjectManager.Instance;
            if (objMgr == null) return;

            List<SpaceFleet> aliveEnemies = new List<SpaceFleet>();
            foreach (SpaceFleet fleet in objMgr.GetEnemyFleets())
            {
                if (fleet != null && fleet.IsFleetAlive() == true)
                    aliveEnemies.Add(fleet);
            }

            if (aliveEnemies.Count == 0) return;

            bool isAlreadyEnemyFocus = m_focusTarget == ECameraFocusTarget.camera_focus_enemy_fleet;
            if (isAlreadyEnemyFocus == true)
            {
                m_enemyFleetFocusIndex = (m_enemyFleetFocusIndex + 1) % aliveEnemies.Count;
            }
            else
            {
                m_enemyFleetFocusIndex = 0;
            }

            ApplyEnemyFleetFocus(aliveEnemies[m_enemyFleetFocusIndex]);
            return;
        }

        if (m_focusTarget == focusTarget) return;
        ApplyFocusTarget(focusTarget);
    }

    private void ApplyEnemyFleetFocus(SpaceFleet targetFleet)
    {
        if (m_focusTarget == ECameraFocusTarget.camera_focus_my_fleet)
        {
            m_currentTargetBackup = m_currentTarget;
        }

        ExitCenterMode();
        m_currentTarget  = targetFleet.transform;
        m_targetPosition = targetFleet.transform.position;

        ApplyZoomRangeFromShip(targetFleet.GetFlagship());

        m_focusTarget = ECameraFocusTarget.camera_focus_enemy_fleet;
        EventManager.TriggerCameraFocusTargetChanged(ECameraFocusTarget.camera_focus_enemy_fleet);
    }

    // 현재 focusTarget에 따라 카메라 타겟을 적용
    private void ApplyFocusTarget(ECameraFocusTarget focusTarget)
    {
        var objMgr = ObjectManager.Instance;
        if (objMgr == null) return;

        SpaceFleet myFleet = objMgr.GetMyFleet();
        if (myFleet == null) return;

        switch (focusTarget)
        {
            case ECameraFocusTarget.camera_focus_enemy_fleet:
                // SetCameraFocusTarget에서 처리하므로 여기에 오지 않음
                break;
            case ECameraFocusTarget.camera_focus_center:
                if (m_focusTarget == ECameraFocusTarget.camera_focus_my_fleet)
                {
                    m_currentTargetBackup = m_currentTarget;
                    m_currentTarget = null;
                }
                m_targetPosition = (myFleet.transform.position + GetCenterModeEnemyPosition(objMgr, myFleet)) * 0.5f;
                EnterCenterMode();
                break;
            case ECameraFocusTarget.camera_focus_my_fleet:
                ExitCenterMode();
                if (m_currentTargetBackup == null)
                {
                    m_currentTarget = myFleet.transform;
                }
                else
                {
                    m_currentTarget = m_currentTargetBackup;
                    m_currentTargetBackup = null;
                }
                // 현재 선택된 함선(또는 기함) 기준 줌 범위 복원
                ApplyZoomRangeFromShip(m_zoomRangeSourceShip != null ? m_zoomRangeSourceShip : myFleet.GetFlagship());
                break;
        }

        m_focusTarget = focusTarget;
        EventManager.TriggerCameraFocusTargetChanged(focusTarget);
    }

}