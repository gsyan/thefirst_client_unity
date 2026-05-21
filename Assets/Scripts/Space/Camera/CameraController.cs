using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
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
    private const float k_zoomArriveThreshold = 0.1f;
    private const float k_positionLerpSpeed = 10f; // 줌(8f)보다 빠르게 — 위치가 먼저 도착해야 커브 방지

    // UIPanelSpace 활성화 시 true, 비활성화 시 false
    private bool m_shipSelectionEnabled = false;
    public void SetShipSelectionEnabled(bool enabled) { m_shipSelectionEnabled = enabled; }

    // 갤럭시 뷰 (탐사 탭)
    private bool m_isGalaxyView = false;
    public event System.Action OnGalaxyViewSettled;
    private bool  m_isGalaxyViewAnimating = false;
    private float m_galaxyViewAnimTimer   = 0f;
    private const float k_galaxyViewAnimDuration = 0.5f;
    private float   m_animStartRotX, m_animStartRotY, m_animStartZoom;
    private Vector3 m_animStartPos;

    private Transform m_savedTarget = null;
    private Vector3 m_savedTargetPosition = Vector3.zero;
    private float m_savedRotationX = 0f;
    private float m_savedRotationY = 0f;
    private float m_savedZoom = 0f;

    // 카메라 중심점 타겟
    private ECameraFocusTarget m_focusTarget = ECameraFocusTarget.camera_focus_my_fleet;
    public ECameraFocusTarget FocusTarget => m_focusTarget;

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
        if (ship == null || ship.m_myFleet == null || ship.m_myFleet.IsEnemy == true) return;
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
	
        // Transform이 설정되어 있으면 해당 위치를 따라감
        if (m_currentTarget != null && m_focusTarget == ECameraFocusTarget.camera_focus_my_fleet)
            m_targetPosition = m_currentTarget.position;

        // Center 모드: 매 프레임 두 함대의 중간점을 갱신 (적 없으면 마지막 스폰 위치 사용)
        if (m_focusTarget == ECameraFocusTarget.camera_focus_center && m_currentTarget == null)
        {
            var objMgr = ObjectManager.Instance;
            if (objMgr != null && objMgr.m_myFleet != null)
                m_targetPosition = (objMgr.m_myFleet.transform.position + objMgr.GetEnemySpawnPosition()) * 0.5f;
        }
        

        bool galaxyViewJustSettled = false;
        if (m_isGalaxyView == true && m_isGalaxyViewAnimating == true)
        {
            // 갤럭시뷰 전용 — 고정 시간 선형 보간
            m_galaxyViewAnimTimer += Time.deltaTime;
            float t = Mathf.Clamp01(m_galaxyViewAnimTimer / k_galaxyViewAnimDuration);
            m_currentRotationX = Mathf.LerpAngle(m_animStartRotX, m_targetRotationX, t);
            m_currentRotationY = Mathf.LerpAngle(m_animStartRotY, m_targetRotationY, t);
            m_currentZoom = Mathf.Lerp(m_animStartZoom, m_targetZoom, t);
            m_interpolatedTargetPosition = Vector3.Lerp(m_animStartPos, m_targetPosition, t);
            if (t >= 1f)
            {
                m_isGalaxyViewAnimating = false;
                galaxyViewJustSettled = true;
            }
        }
        else
        {
            // 일반 뷰 — 기존 지수 보간 (모듈 포커싱 등)
            if (m_hasTargetRotationY == true)
            {
                m_currentRotationY = Mathf.LerpAngle(m_currentRotationY, m_targetRotationY, k_rotateLerpSpeed * Time.deltaTime);
                if (Mathf.Abs(Mathf.DeltaAngle(m_currentRotationY, m_targetRotationY)) < k_rotateArriveThreshold)
                {
                    m_currentRotationY = m_targetRotationY;
                    m_hasTargetRotationY = false;
                }
            }
            if (m_hasTargetRotationX == true)
            {
                m_currentRotationX = Mathf.LerpAngle(m_currentRotationX, m_targetRotationX, k_rotateLerpSpeed * Time.deltaTime);
                if (Mathf.Abs(Mathf.DeltaAngle(m_currentRotationX, m_targetRotationX)) < k_rotateArriveThreshold)
                {
                    m_currentRotationX = m_targetRotationX;
                    m_hasTargetRotationX = false;
                }
            }
            if (m_hasTargetZoom == true)
            {
                m_currentZoom = Mathf.Lerp(m_currentZoom, m_targetZoom, k_zoomLerpSpeed * Time.deltaTime);
                if (Mathf.Abs(m_currentZoom - m_targetZoom) < k_zoomArriveThreshold)
                {
                    m_currentZoom = m_targetZoom;
                    m_hasTargetZoom = false;
                }
            }
            m_interpolatedTargetPosition = Vector3.Lerp(m_interpolatedTargetPosition, m_targetPosition, k_positionLerpSpeed * Time.deltaTime);

            // 카메라 이동 완료 시 입력 자동 활성화
            if (m_inputEnabled == false
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
            OnGalaxyViewSettled?.Invoke();
    }

    private bool m_inputEnabled = true;

    // Input handling
    private bool m_isDragging = false;
    private bool m_touchBlockedByUI = false; // 터치 시작이 UI 위였으면 해당 터치 전체 차단
    //private bool m_isPanning = false;
    private Vector3 m_startTouchPosition;
    private float m_startRotationY;
    private float m_startRotationX;
    private float m_lastPinchDistance = 0f;
    private Vector2 m_lastTwoTouchCenter = Vector2.zero;

    // 이전 프레임 터치 위치 저장 (방향 벡터 계산용)
    private Vector2 m_prevTouch0Position;
    private Vector2 m_prevTouch1Position;
    private int m_prevTouchCount = 0; // 2터치→1터치 전환 감지용

    // 탭 판정 — 누를 때와 뗄 때 같은 콜라이더를 픽하면 선택
    private Collider m_tapHitCollider;

    private void Update()
    {
        HandleInput();
        UpdateCameraTransform();
    }

    private void HandleInput()
    {
        if (m_inputEnabled == false) return;

        bool inputDown = false;
        bool inputUp = false;
        bool inputHeld = false;
        Vector3 inputPosition = Vector3.zero;

#if UNITY_EDITOR || UNITY_STANDALONE
        // PC: 우클릭은 UI와 충돌 없으므로 UI 체크 없이 바로 처리
        HandleInput_Mouse(ref inputDown, ref inputUp, ref inputHeld, ref inputPosition);
#elif UNITY_ANDROID || UNITY_IOS
        HandleInput_Touch(ref inputDown, ref inputUp, ref inputHeld, ref inputPosition);
#endif

        // 우클릭 회전 처리 (공통)
        if (inputDown == true)
        {
            m_isDragging = true;
            m_startTouchPosition = inputPosition;
            m_startRotationY = m_currentRotationY;
            m_startRotationX = m_currentRotationX;
            m_hasTargetRotationY = false;
            m_hasTargetRotationX = false;
            m_hasTargetZoom = false;
        }
        else if (inputUp)
        {
            m_isDragging = false;
        }

        if (m_isDragging && inputHeld)
        {
            Vector3 touchDelta = (inputPosition - m_startTouchPosition) * m_rotationSpeed;
            m_currentRotationY = m_startRotationY + touchDelta.x;
            m_currentRotationX = Mathf.Clamp(m_startRotationX - touchDelta.y, -80f, 80f);
        }
    }
    
    private void HandleInput_Mouse(ref bool inputDown, ref bool inputUp, ref bool inputHeld, ref Vector3 inputPosition)
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector3 mousePos = mouse.position.ReadValue();

        // 우클릭: 회전
        if (mouse.rightButton.wasPressedThisFrame == true)
        {
            inputDown = true;
            inputPosition = mousePos;
        }
        else if (mouse.rightButton.wasReleasedThisFrame == true)
        {
            inputUp = true;
        }
        else if (mouse.rightButton.isPressed == true)
        {
            inputHeld = true;
            inputPosition = mousePos;
        }

        // 좌클릭: 누를 때 픽 저장, 뗄 때 같은 콜라이더면 선택
        if (mouse.leftButton.wasPressedThisFrame == true)
        {
            m_startTouchPosition = mousePos;
            LayerMask pickMask = ~m_layerMaskShield;
            m_tapHitCollider = IsPointerOverUIObject() == false && GetCameraRaycast(out RaycastHit downHit, pickMask, 3000f, mousePos)
                ? downHit.collider : null;
        }
        else if (mouse.leftButton.wasReleasedThisFrame == true)
        {
            if (m_tapHitCollider != null)
            {
                LayerMask pickMask = ~m_layerMaskShield;
                if (GetCameraRaycast(out RaycastHit upHit, pickMask, 3000f, mousePos) && upHit.collider == m_tapHitCollider)
                    HandleModuleSelection(mousePos);
                m_tapHitCollider = null;
            }
        }

        // 마우스 휠 줌 (new Input System scroll.y: 1노치 ≈ 1.0)
        float scrollDelta = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scrollDelta) > 0.01f)
            ZoomCamera(-scrollDelta * 0.5f);
    }

    private void HandleInput_Touch(ref bool inputDown, ref bool inputUp, ref bool inputHeld, ref Vector3 inputPosition)
    {
        var touches = Touch.activeTouches;

        if (touches.Count >= 2)
        {
            Touch touch0 = touches[0];
            Touch touch1 = touches[1];

            Vector2 currentTouchCenter = (touch0.screenPosition + touch1.screenPosition) * 0.5f;
            float currentPinchDistance = Vector2.Distance(touch0.screenPosition, touch1.screenPosition);

            if (touch0.phase == UnityEngine.InputSystem.TouchPhase.Began || touch1.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                m_isDragging = false; // 핀치 진입 시 단일 터치 드래그 중단
                m_lastPinchDistance = currentPinchDistance;
                m_lastTwoTouchCenter = currentTouchCenter;
                m_prevTouch0Position = touch0.screenPosition;
                m_prevTouch1Position = touch1.screenPosition;
            }
            else if (touch0.phase == UnityEngine.InputSystem.TouchPhase.Moved || touch1.phase == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                Vector2 moveVector0 = touch0.screenPosition - m_prevTouch0Position;
                Vector2 moveVector1 = touch1.screenPosition - m_prevTouch1Position;

                if (moveVector0.magnitude > 1f && moveVector1.magnitude > 1f)
                {
                    float dotProduct = Vector2.Dot(moveVector0.normalized, moveVector1.normalized);

                    // dot < -0.5: 반대 방향 → 핀치 줌
                    if (dotProduct < -0.5f)
                    {
                        float deltaPinch = currentPinchDistance - m_lastPinchDistance;
                        ZoomCamera(-deltaPinch * 0.01f);
                    }
                }

                m_lastPinchDistance = currentPinchDistance;
                m_lastTwoTouchCenter = currentTouchCenter;
                m_prevTouch0Position = touch0.screenPosition;
                m_prevTouch1Position = touch1.screenPosition;
            }

            m_prevTouchCount = 2;
        }
        else if (touches.Count == 1)
        {
            Touch touch = touches[0];
            Vector2 pos = touch.screenPosition;

            // 2터치 → 1터치 전환: 현재 손가락 위치를 새 기준점으로 즉시 초기화
            if (m_prevTouchCount >= 2)
            {
                m_startTouchPosition = pos;
                m_startRotationY = m_currentRotationY;
                m_startRotationX = m_currentRotationX;
                m_isDragging = true;
            }
            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                // 터치 시작이 UI 위면 해당 터치 전체를 UI에게 양보
                m_touchBlockedByUI = IsPointerOverUIObject();
                if (m_touchBlockedByUI == false)
                {
                    inputDown = true;
                    inputPosition = pos;
                    LayerMask pickMask = ~m_layerMaskShield;
                    m_tapHitCollider = GetCameraRaycast(out RaycastHit downHit, pickMask, 3000f, pos) ? downHit.collider : null;
                }
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended)
            {
                m_touchBlockedByUI = false;
                inputUp = true;
                if (m_tapHitCollider != null)
                {
                    LayerMask pickMask = ~m_layerMaskShield;
                    if (GetCameraRaycast(out RaycastHit upHit, pickMask, 3000f, pos) && upHit.collider == m_tapHitCollider)
                        HandleModuleSelection(pos);
                    m_tapHitCollider = null;
                }
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                m_touchBlockedByUI = false;
                inputUp = true;
                m_tapHitCollider = null;
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved || touch.phase == UnityEngine.InputSystem.TouchPhase.Stationary)
            {
                if (m_touchBlockedByUI == false)
                {
                    inputHeld = true;
                    inputPosition = pos;
                }
            }

            m_prevTouchCount = 1;
        }
        else
        {
            m_prevTouchCount = 0;
        }
    }
    

    // 3D 클릭으로 내 함선/모듈 선택 (UIPanelSpace 활성 시에만 동작)
    private void HandleModuleSelection(Vector3? screenPosition = null)
    {
        if (!m_shipSelectionEnabled) return;

        LayerMask pickMask = ~m_layerMaskShield;
        if (!GetCameraRaycast(out RaycastHit hit, pickMask, 3000f, screenPosition))
            return;

        SpaceShip ship = hit.collider.GetComponentInParent<SpaceShip>();
        if (ship == null) return;
        if (ship.m_myFleet == null || ship.m_myFleet.IsEnemy) return;

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
        if (objMgr == null || objMgr.m_myFleet == null) return m_currentZoom;

        float dist = Vector3.Distance(objMgr.m_myFleet.transform.position, objMgr.GetEnemySpawnPosition());
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

    private bool IsPointerOverUIObject()
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current);

        // 터치 입력 체크
        if (Touch.activeTouches.Count > 0)
            eventData.position = Touch.activeTouches[0].screenPosition;
        else
            eventData.position = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    // Raycast for object selection
    public bool GetCameraRaycast(out RaycastHit hit, LayerMask layerMask = default, float maxDistance = 1000f, Vector3? screenPosition = null)
    {
        if (m_targetCamera == null)
        {
            hit = new RaycastHit();
            return false;
        }

        Vector3 inputPos = screenPosition ?? (Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero);
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
        {
            m_targetPosition = target.position;
            //m_interpolatedTargetPosition = target.position;
        }

        m_currentTarget = target;
        m_targetPosition = target.position;

        EventManager.TriggerCameraFocusTargetChanged(ECameraFocusTarget.camera_focus_my_fleet);
    }

    // 카메라를 현재 타겟 위치로 즉시 스냅 (스테이지 입장 등 순간이동 시 사용)
    // my_fleet: m_currentTarget이 없으면 함대 transform으로 재연결 후 스냅
    // center/enemy: EnemyFleetFocusPosition 기준으로 스냅 (적 없어도 스폰 위치 사용)
    public void SnapToTarget()
    {
        var objMgr = ObjectManager.Instance;
        var myFleet = objMgr != null ? objMgr.m_myFleet : null;
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
        m_inputEnabled = false;

        m_savedTarget = m_currentTarget;
        m_savedTargetPosition = m_targetPosition;
        m_savedRotationX = m_currentRotationX;
        m_savedRotationY = m_currentRotationY;
        m_savedZoom = m_currentZoom;

        m_currentTarget = null;
        m_targetPosition    = targetPos;
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
        m_galaxyViewAnimTimer  = 0f;
        m_isGalaxyViewAnimating = true;
    }

    // 갤럭시 뷰 종료 — 함선 뷰로 복귀, 진입 전 회전·줌 복원
    public void ExitGalaxyView()
    {
        if (m_isGalaxyView == false) return;
        m_isGalaxyView = false;
        m_isGalaxyViewAnimating = false;
        m_inputEnabled = false;

        m_currentTarget = m_savedTarget;
        m_targetPosition = m_savedTargetPosition;

        RestoreFleetView();
    }

    // 갤럭시 뷰 종료하면서 카메라를 이전 함선 위치 대신 지정 위치로 이동, 회전·줌 복원
    public void ExitGalaxyViewMoveTo(Vector3 position)
    {
        if (m_isGalaxyView == false) return;
        m_isGalaxyView = false;
        m_isGalaxyViewAnimating = false;
        m_inputEnabled = false;
        m_currentTarget = null;
        m_targetPosition = position;

        RestoreFleetView();
    }

    private void RestoreFleetView()
    {
        m_hasTargetRotationX = true;
        m_targetRotationX = m_savedRotationX;
        m_hasTargetRotationY = true;
        m_targetRotationY = m_savedRotationY;
        SetTargetZoom(Mathf.Clamp(m_savedZoom, m_minZoom, m_maxZoom));
    }

    // 카메라 중심점 전환 (적함대, 중간, 우리함대)
    public void SetCameraFocusTarget(ECameraFocusTarget focusTarget)
    {
        if( m_focusTarget == focusTarget) return;
        ApplyFocusTarget(focusTarget);
    }

    // 카메라 중심점을 순환 전환 (MyFleet → EnemyFleet → Center → MyFleet)
    public void CycleCameraFocusTarget()
    {
        ECameraFocusTarget next;
        switch (m_focusTarget)
        {
            case ECameraFocusTarget.camera_focus_my_fleet:
                next = ECameraFocusTarget.camera_focus_enemy_fleet;
                break;
            case ECameraFocusTarget.camera_focus_enemy_fleet:
                next = ECameraFocusTarget.camera_focus_center;
                break;
            case ECameraFocusTarget.camera_focus_center:
                next = ECameraFocusTarget.camera_focus_my_fleet;
                break;
            default:
                next = ECameraFocusTarget.camera_focus_my_fleet;
                break;
        }
        ApplyFocusTarget(next);
    }

    // 현재 focusTarget에 따라 카메라 타겟을 적용
    private void ApplyFocusTarget(ECameraFocusTarget focusTarget)
    {
        var objMgr = ObjectManager.Instance;
        if (objMgr == null) return;

        SpaceFleet myFleet = objMgr.m_myFleet;
        if (myFleet == null) return;

        switch (focusTarget)
        {
            case ECameraFocusTarget.camera_focus_enemy_fleet:
                if (m_focusTarget == ECameraFocusTarget.camera_focus_my_fleet)
                {
                    m_currentTargetBackup = m_currentTarget;
                    m_currentTarget = null;
                }
                ExitCenterMode();
                m_targetPosition = objMgr.GetEnemySpawnPosition();
                // 적 기함 크기 기준 줌 범위 적용
                if (objMgr.m_enemyFleets.Count > 0)
                    ApplyZoomRangeFromShip(objMgr.m_enemyFleets[0].GetFlagship());
                break;
            case ECameraFocusTarget.camera_focus_center:
                if (m_focusTarget == ECameraFocusTarget.camera_focus_my_fleet)
                {
                    m_currentTargetBackup = m_currentTarget;
                    m_currentTarget = null;
                }
                m_targetPosition = (myFleet.transform.position + objMgr.GetEnemySpawnPosition()) * 0.5f;
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
                    SetTargetOfCameraController(m_currentTargetBackup);
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