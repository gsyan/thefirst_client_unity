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
    private bool m_instantFollow = false; // true면 위치를 lerp 없이 매 프레임 그대로 스냅 (초고속 이동 타겟이 화면 밖으로 벗어나는 것 방지용)
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

    // 갤럭시 뷰 (탐사 탭)
    private bool m_isGalaxyView = false;
    public bool IsGalaxyView => m_isGalaxyView;
    private bool  m_isGalaxyViewAnimating = false;
    private bool  m_isEnteringGalaxy = false; // true=함대→갤럭시, false=갤럭시→함대
    private bool  m_isZoneRefocus = false; // true=갤럭시뷰 안에서 존만 전환(FocusOnZoneAnchor) — 진입/복귀 전용 프리페이즈 없이 단순 보간
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
        m_explorationGridCellLayerMask = LayerMask.GetMask("ExplorationGridCell");

        EventManager.Subscribe_SpaceShipSelected(OnSpaceShipSelectedForZoom);
        EventManager.Subscribe_ShipBodyChanged(OnShipBodyChangedForZoom);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        EventManager.Unsubscribe_SpaceShipSelected(OnSpaceShipSelectedForZoom);
        EventManager.Unsubscribe_ShipBodyChanged(OnShipBodyChangedForZoom);
        m_handleInputMouse?.Unsubscribe();
        m_handleInputTouch?.Unsubscribe();
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

            if (m_isZoneRefocus == true)
            {
                // 갤럭시뷰 안에서 존만 전환 — 목표값을 향한 지수 보간(연속 추적). 존 탭 스크롤 드래그 중 목표가 계속 바뀌어도
                // 리셋 없이 항상 현재 값에서부터 이어서 따라가므로 줌/회전이 멈춰 보이지 않음
                m_currentRotationX = Mathf.LerpAngle(m_currentRotationX, m_targetRotationX, k_rotateLerpSpeed * Time.unscaledDeltaTime);
                m_currentRotationY = Mathf.LerpAngle(m_currentRotationY, m_targetRotationY, k_rotateLerpSpeed * Time.unscaledDeltaTime);
                m_currentZoom      = Mathf.Lerp(m_currentZoom, m_targetZoom, k_zoomLerpSpeed * Time.unscaledDeltaTime);
                m_interpolatedTargetPosition = Vector3.Lerp(m_interpolatedTargetPosition, m_galaxyTargetPos, k_positionLerpSpeed * Time.unscaledDeltaTime);

                bool rotXArrived = Mathf.Abs(Mathf.DeltaAngle(m_currentRotationX, m_targetRotationX)) < k_rotateArriveThreshold;
                bool rotYArrived = Mathf.Abs(Mathf.DeltaAngle(m_currentRotationY, m_targetRotationY)) < k_rotateArriveThreshold;
                bool zoomArrived = Mathf.Abs(m_currentZoom - m_targetZoom) < k_zoomArriveThreshold;
                if (rotXArrived == true && rotYArrived == true && zoomArrived == true)
                {
                    m_isGalaxyViewAnimating = false;
                    m_isZoneRefocus = false;
                }
            }
            else if (m_isEnteringGalaxy == true)
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

                // 진입 전용: preRotDuration + mainDuration
                if (m_galaxyViewAnimTimer >= m_galaxyPreRotDuration + m_galaxyViewAnimDuration)
                {
                    m_isGalaxyViewAnimating = false;
                    galaxyViewJustSettled = true;
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

                // 복귀 전용: preRotDuration + mainDuration
                if (m_galaxyViewAnimTimer >= m_galaxyPreRotDuration + m_galaxyViewAnimDuration)
                {
                    m_isGalaxyViewAnimating = false;
                    m_inputEnabled = true;
                }
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
            if (m_instantFollow == true)
                m_interpolatedTargetPosition = m_targetPosition;
            else
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

    // 화면이 좌/우로 분할되어 메인카메라가 일부만 차지할 때(함대편성 UI 등), 그 영역 밖 터치는 카메라 조작으로 받지 않기 위한 허용 구간(스크린 비율 0~1) — 분할 비율은 호출하는 쪽 레이아웃에 따라 유동적
    private float m_inputScreenXMin = 0f;
    private float m_inputScreenXMax = 1f;

    public void SetInputScreenXRange(float xMin, float xMax)
    {
        m_inputScreenXMin = xMin;
        m_inputScreenXMax = xMax;
    }

    public bool IsScreenPositionInInputRange(Vector2 screenPos)
    {
        float normalizedX = screenPos.x / Screen.width;
        return normalizedX >= m_inputScreenXMin && normalizedX <= m_inputScreenXMax;
    }

    // Input handling
    private HandleInputMouse m_handleInputMouse;
    private HandleInputTouch m_handleInputTouch;

    private Vector3 m_startTouchPosition;
    private float m_startRotationY;
    private float m_startRotationX;

    private void Update()
    {
        HandleInput();
    }

    // 타겟(함선 등)의 이동은 각자의 Update()에서 일어나므로, 그 이후인 LateUpdate에서 위치를 읽어야
    // 이번 프레임 이동이 반영된 최신 위치를 스냅/보간할 수 있음 (한 프레임 지연 방지)
    private void LateUpdate()
    {
        UpdateCameraTransform();
    }

    private void HandleInput()
    {
        // m_inputEnabled==false(갤럭시뷰 등)여도 튜토리얼 AnyClick 감지는 계속 동작해야 하므로 여기서 조기 return하지 않고,
        // 카메라 조작(드래그/줌/레이캐스트) 여부만 Process()에 넘겨서 그 안에서 게이트함
#if UNITY_EDITOR || UNITY_STANDALONE
        m_handleInputMouse.Process(m_inputEnabled);
#elif UNITY_ANDROID || UNITY_IOS
        m_handleInputTouch.Process(m_inputEnabled);
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
    // 여기서는 "지금 어떤 UI 패널이 열려있는지" 등을 따지지 않고 항상 이벤트를 발행하기만 함 — 그 이벤트를 실제로
    // 처리할지 말지는 각 리스너(UIPanelSpace/UIPanelFleetComposition 등)가 자기 활성 상태에 맞춰 스스로 판단(구독/해제)
    public void HandleModuleSelection(Vector3? screenPosition = null)
    {
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

    // 갤럭시뷰(탐사 그리드) 중 3D 클릭 — 갤럭시뷰에서는 m_inputEnabled가 계속 false로 유지되어 HandleModuleSelection 경로를 타지 않으므로
    // HandleInputMouse가 별도로 이 메서드를 호출함. 셀이 아니면 로컬뷰와 동일하게 EmptySpaceTapped를 재사용(UIManager가 오버레이 패널을 닫음)
    // s_pickMask(HandleInputMouse.cs)와 동일하게 이름 기반 하드코딩 — 인스펙터 배선을 깜빡할 위험 없음, 레이어 순서 바뀌어도 안전
    // LayerMask.GetMask는 정적 필드 초기화 시점(MonoBehaviour 생성자/cctor)에 호출 불가 — OnInitialize()에서 계산
    private const float k_galaxyGridPickMaxDistance = 20000f; // 존별 galaxyCameraZoom이 최대 12000까지 쓰여서 여유 있게 설정
    private int m_explorationGridCellLayerMask;
    public void HandleGalaxyGridSelection(Vector3? screenPosition = null)
    {
        if (GetCameraRaycast(out RaycastHit hit, m_explorationGridCellLayerMask, k_galaxyGridPickMaxDistance, screenPosition) == false)
        {
            EventManager.Trigger_EmptySpaceTapped();
            return;
        }

        GridCell3D cell = hit.collider.GetComponentInParent<GridCell3D>();
        if (cell != null)
            EventManager.Trigger_ExplorationGridCellClicked(cell);
        else
            EventManager.Trigger_EmptySpaceTapped();
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

    // 카메라 viewport width를 즉시 설정 (UIPanelSpace에서 레이아웃 애니메이션 구동용) — x는 항상 0(좌측 고정), 오른쪽만 줄어드는 용도
    public void SetViewportWidth(float width)
    {
        SetViewportRect(0f, width);
    }

    public float GetViewportWidth()
    {
        return m_targetCamera != null ? m_targetCamera.rect.width : 1f;
    }

    // 카메라 viewport의 x(시작 위치)와 width를 함께 설정 — 화면 좌/우 어느 쪽으로도 이동 가능(함대편성 UI의 우측↔좌측 전환 등)
    public void SetViewportRect(float x, float width)
    {
        if (m_targetCamera == null) return;
        Rect r = m_targetCamera.rect;
        r.x = Mathf.Clamp01(x);
        r.width = Mathf.Clamp01(width);
        m_targetCamera.rect = r;
        RefreshCenterModeZoom();
    }

    public float GetViewportX()
    {
        return m_targetCamera != null ? m_targetCamera.rect.x : 0f;
    }

    // 카메라 viewport rect(x, width) 애니메이션 — UIPanelFleetComposition처럼 화면 일부만 카메라에 내주는 패널이 열고 닫힐 때 사용.
    // CameraController는 항상 살아있는 싱글톤이라, 호출한 패널의 GameObject가 애니메이션 도중 비활성화돼도 코루틴이 끊기지 않음
    private Coroutine m_viewportAnimCoroutine;

    public void AnimateViewportRect(float targetX, float targetWidth, float duration, System.Action<float, float> onProgress = null, System.Action onComplete = null)
    {
        StopViewportAnimation();
        m_viewportAnimCoroutine = StartCoroutine(Co_AnimateViewportRect(targetX, targetWidth, duration, onProgress, onComplete));
    }

    public void StopViewportAnimation()
    {
        if (m_viewportAnimCoroutine != null)
        {
            StopCoroutine(m_viewportAnimCoroutine);
            m_viewportAnimCoroutine = null;
        }
    }

    private System.Collections.IEnumerator Co_AnimateViewportRect(float targetX, float targetWidth, float duration, System.Action<float, float> onProgress, System.Action onComplete)
    {
        float startX = GetViewportX();
        float startWidth = GetViewportWidth();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            float x = Mathf.Lerp(startX, targetX, t);
            float width = Mathf.Lerp(startWidth, targetWidth, t);
            SetViewportRect(x, width);
            EventManager.TriggerCameraViewportChanged(Mathf.InverseLerp(startWidth, targetWidth, width));
            onProgress?.Invoke(x, width);

            yield return null;
        }

        SetViewportRect(targetX, targetWidth);
        onProgress?.Invoke(targetX, targetWidth);
        m_viewportAnimCoroutine = null;
        onComplete?.Invoke();
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

    // true면 위치 추적을 lerp 없이 매 프레임 그대로 스냅 — 타겟이 순간적으로 초고속 이동(워프 등)할 때 화면 밖으로 벗어나는 것 방지
    public void SetInstantFollow(bool enabled)
    {
        m_instantFollow = enabled;
    }

    // 목표 회전각을 직접 설정 (Lerp로 부드럽게 회전) — rotY/rotX는 절대각(월드 기준)
    public void SetTargetRotation(float rotY, float rotX)
    {
        m_hasTargetRotationY = true;
        m_targetRotationY = rotY;
        m_hasTargetRotationX = true;
        m_targetRotationX = Mathf.Clamp(rotX, -80f, 80f);
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
    // snapImmediately: true면 m_interpolatedTargetPosition도 즉시 스냅 — 최초 함대 스폰 등 Lerp로 이동하는 모습이 보이면 안 되는 경우 호출부에서 지정
    public void SetTargetOfCameraController(Transform target, bool snapImmediately = false)
    {
        if (target == null) return;

        m_currentTarget = target;
        m_targetPosition = target.position;
        if (snapImmediately == true)
            m_interpolatedTargetPosition = target.position;

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

    // Zone 그룹 탭 선택 시 해당 앵커로 포커스 (갤럭시 뷰 중에만 동작) — 목표값만 갱신, 타이머/시작스냅샷은 리셋하지 않음(m_isZoneRefocus).
    // 존 탭 스크롤 드래그 중엔 이 함수가 존이 바뀔 때마다 연속으로 호출되는데, 타이머 기반 이징 애니메이션을 쓰면 매번 리셋되어
    // 줌이 거의 진행되지 못하고 멈춰 보이는 문제가 있었음(느린 시작 구간에서 계속 재시작) — 지수 보간(연속 추적)으로 전환해 해결
    public void FocusOnZoneAnchor(Vector3 zoneWorldPos, float zoom, float rotX, float rotY)
    {
        if (m_isGalaxyView == false) return;

        m_isZoneRefocus     = true;
        m_isGalaxyViewAnimating = true;
        m_targetPosition    = zoneWorldPos;
        m_galaxyTargetPos   = zoneWorldPos;
        m_targetZoom        = zoom;
        m_targetRotationX   = Mathf.Clamp(rotX, -80f, 80f);
        m_targetRotationY   = rotY;
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

        // 존 탭 스크롤 중 FocusOnZoneAnchor가 켜둔 m_isZoneRefocus가 도착 임계값에 못 미친 채 남아있으면,
        // 아래에서 시작하는 복귀 애니메이션을 UpdateCameraTransform의 존 추적 분기가 가로채 엉뚱한 존 앵커를 계속 쫓아감 — 여기서 명시적으로 끔
        m_isZoneRefocus = false;

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