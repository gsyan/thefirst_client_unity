using UnityEngine;
using UnityEngine.EventSystems;

[System.Serializable]
public enum CameraControllerMode
{
    Normal                  // 일반 게임 모드
    , Select_Ship
    , Upgrade_Fleet         // 함대 보기 모드
    , Upgrade_Ship          // 함대 보기 모드
}

public class CameraController : MonoSingleton<CameraController>
{
    [Header("Camera Settings")]
    public Camera m_targetCamera;
    private float m_rotationSpeed = 0.1f;
    private float m_zoomSpeed = 2f;
    private float m_panSpeed = 0.001f;
    private float m_minZoom = 10f;
    private float m_maxZoom = 50f;

    // Camera state management
    private Transform m_originalTarget;
    private Vector3 m_offset;
    
    // Current camera state
    private Transform m_currentTarget; // (Optional) 움직이는 타겟을 따라가기 위한 Transform
    private Vector3 m_targetPosition; // 카메라가 바라보는 목표 위치
    private Vector3 m_interpolatedTargetPosition; // 부드럽게 보간된 타겟 위치
    private float m_currentZoom;
    private float m_currentRotationY = 0f;
    private float m_currentRotationX = 0f;
    public CameraControllerMode m_currentMode = CameraControllerMode.Normal;

    // LayerMask
    private LayerMask m_layerDefault = default;
    private const int m_layerShip = 30;
    private LayerMask m_layerMaskShip = 1 << m_layerShip;
    private const int m_layerShipModule = 31;
    private LayerMask m_layerMaskShipModule = 1 << m_layerShipModule;


    // Hover tracking
    private ModuleBase m_hoveredModule = null;
    //private DisplayShip m_currentDisplayShip = null;

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
    }

    public void SaveOriginalState()
    {
        if (m_targetCamera == null) return;

        // 기존 타겟 설정 (전투용 함대나 다른 타겟)
        GameObject myFleet = GameObject.Find("MyFleet");
        if (myFleet == null) return;
        m_originalTarget = myFleet.transform;

        if (m_originalTarget == null) return;
        m_offset = m_targetCamera.transform.position - m_originalTarget.position;

        m_currentTarget = m_originalTarget;
    }

    public void SwitchCameraMode(CameraControllerMode mode, Transform viewTarget = null)
    {
        if (m_targetCamera == null) return;

        m_currentMode = mode;

        switch (mode)
        {
            case CameraControllerMode.Normal:
                RestoreOriginalState();
                SetSpaceSceneGaugesVisible(true);
                break;
            case CameraControllerMode.Select_Ship:
                if (viewTarget != null)
                    SetTargetTransform(viewTarget);
                break;
            case CameraControllerMode.Upgrade_Fleet:
            case CameraControllerMode.Upgrade_Ship:
                if (viewTarget != null)
                    SetTargetTransform(viewTarget);
                SetSpaceSceneGaugesVisible(false);
                break;
        }
    }

    public void RestoreOriginalState()
    {
        if (m_targetCamera == null) return;
        m_currentTarget = m_originalTarget;
        
        if (m_targetCamera == null || m_currentTarget == null) return;
        m_targetCamera.transform.position = m_currentTarget.position + m_offset;
    }

    private void SetSpaceSceneGaugesVisible(bool visible)
    {
        ModuleGaugeDisplay[] allGauges = FindObjectsByType<ModuleGaugeDisplay>(FindObjectsSortMode.None);
        foreach (ModuleGaugeDisplay gauge in allGauges)
        {
            if (gauge != null )
                gauge.SetGaugeVisible(visible);
        }
    }

    public void UpdateFleetViewPosition()
    {
        if (m_targetCamera == null) return;

        // Transform이 설정되어 있으면 해당 위치를 따라감 (움직이는 타겟)
        if (m_currentTarget != null)
            m_targetPosition = m_currentTarget.position;

        // 타겟 위치를 부드럽게 보간 (Lerp 속도 조절 가능)
        float lerpSpeed = 5f * Time.deltaTime; // 속도 조절 파라미터
        m_interpolatedTargetPosition = Vector3.Lerp(m_interpolatedTargetPosition, m_targetPosition, lerpSpeed);

        // 1. 회전 각도를 라디안으로 변환
        float radiansY = m_currentRotationY * Mathf.Deg2Rad;
        float radiansX = m_currentRotationX * Mathf.Deg2Rad;
        // 2. 구면 좌표계(Spherical Coordinates)로 카메라 위치 계산
        float horizontalDistance = m_currentZoom * Mathf.Cos(radiansX);
        Vector3 rotatedOffset = new(
            Mathf.Sin(radiansY) * horizontalDistance,
            m_currentZoom * Mathf.Sin(radiansX),
            Mathf.Cos(radiansY) * horizontalDistance
        );

         // 3. 보간된 타겟 위치 + 오프셋 = 카메라 위치
        m_targetCamera.transform.position = m_interpolatedTargetPosition + rotatedOffset;
        m_targetCamera.transform.LookAt(m_interpolatedTargetPosition);
    }

    private bool m_inputEnabled = true;

    public void SetInputEnabled(bool enabled)
    {
        m_inputEnabled = enabled;
    }

    // Input handling
    private bool m_isDragging = false;
    private bool m_isPanning = false;
    private Vector3 m_startTouchPosition;
    private float m_startRotationY;
    private float m_startRotationX;
    private float m_lastPinchDistance = 0f;
    private Vector2 m_lastTwoTouchCenter = Vector2.zero;

    private void Update()
    {
        HandleInput();
        UpdateFleetViewPosition();
    }

    private void HandleInput()
    {
        if (m_inputEnabled == false) return;
        if (IsPointerOverUIObject() == true) return;

        bool inputDown = false;
        bool inputUp = false;
        bool inputHeld = false;
        Vector3 inputPosition = Vector3.zero;


#if UNITY_EDITOR || UNITY_STANDALONE
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

        if (m_isDragging == false)
            HandleModuleHover();
    }
    
    private void HandleInput_Mouse(ref bool inputDown, ref bool inputUp, ref bool inputHeld, ref Vector3 inputPosition)
    {
        // 우클릭: 회전
        if (Input.GetMouseButtonDown(1) == true)
        {
            inputDown = true;
            inputPosition = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(1) == true)
        {
            inputUp = true;
        }
        else if (Input.GetMouseButton(1) == true)
        {
            inputHeld = true;
            inputPosition = Input.mousePosition;
        }

        // 좌클릭: 팬 이동
        if (Input.GetMouseButtonDown(0) == true)
        {
            m_isPanning = true;
            m_startTouchPosition = Input.mousePosition;
            HandleModuleSelection(m_startTouchPosition); // 카메라 모드에 따라 처리 필요
        }
        else if (Input.GetMouseButtonUp(0) == true)
        {
            m_isPanning = false;
        }
        else if (Input.GetMouseButton(0) == true)
        {
            Vector2 mouseDelta = (Vector2)Input.mousePosition - (Vector2)m_startTouchPosition;
            if (mouseDelta.magnitude > 1f)
            {
                CameraMove_LeftRightUpDown(mouseDelta);
                m_startTouchPosition = Input.mousePosition;
            }
        }

        // 마우스 휠 줌 또는 전진/후퇴
        float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollDelta) > 0.01f)
        {
            if (Mathf.Abs(scrollDelta) > 0.01f)
            {
                if (m_currentTarget != null)
                    ZoomCamera(-scrollDelta * 5f);
                else
                    CameraMove_FrontBack(scrollDelta);
            }
        }
            
    }

    private void HandleInput_Touch(ref bool inputDown, ref bool inputUp, ref bool inputHeld, ref Vector3 inputPosition)
    {
        if (Input.touchCount >= 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            Vector2 currentTouchCenter = (touch0.position + touch1.position) * 0.5f;
            float currentPinchDistance = Vector2.Distance(touch0.position, touch1.position);

            if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
            {
                m_lastPinchDistance = currentPinchDistance;
                m_lastTwoTouchCenter = currentTouchCenter;
                m_isPanning = false;
            }
            else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
            {
                float deltaPinch = currentPinchDistance - m_lastPinchDistance;
                float pinchChangeRatio = Mathf.Abs(deltaPinch) / m_lastPinchDistance;

                // 핀치 변화가 작으면 팬으로 간주 (5% 미만)
                if (pinchChangeRatio < 0.05f)
                {
                    // 두 터치의 중심점 이동량으로 팬 처리
                    Vector2 touchCenterDelta = currentTouchCenter - m_lastTwoTouchCenter;
                    if (touchCenterDelta.magnitude > 1f) // 최소 이동량 체크
                    {
                        m_isPanning = true;
                        CameraMove_LeftRightUpDown(touchCenterDelta);
                    }
                }
                else
                {
                    // 핀치 줌
                    m_isPanning = false;
                    if (m_currentTarget != null)
                        ZoomCamera(-deltaPinch * 0.01f);
                    else
                        CameraMove_FrontBack(deltaPinch * 0.01f);
                }

                m_lastPinchDistance = currentPinchDistance;
                m_lastTwoTouchCenter = currentTouchCenter;
            }
            else if (touch0.phase == TouchPhase.Ended || touch1.phase == TouchPhase.Ended)
            {
                m_isPanning = false;
            }
        }
        else if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                inputDown = true;
                inputPosition = touch.position;
                HandleModuleSelection(touch.position); // 카메라 모드에 따라 처리 필요
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                inputUp = true;
            }
            else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                inputHeld = true;
                inputPosition = touch.position;
            }
        }
    }
    


    private void HandleModuleHover()
    {
        if (GetCameraRaycast(out RaycastHit hit))
        {
            ModuleBase module = hit.collider.GetComponentInParent<ModuleBase>();
            //DisplayShip displayShip = hit.collider.GetComponentInParent<DisplayShip>();

            if (module != m_hoveredModule)
            {
                // if (m_hoveredModule != null && m_currentDisplayShip != null)
                //     m_currentDisplayShip.OnModuleHover(m_hoveredModule, false);

                // m_hoveredModule = module;
                // m_currentDisplayShip = displayShip;

                // if (module != null && displayShip != null)
                //     displayShip.OnModuleHover(module, true);
            }
        }
        else
        {
            // if (m_hoveredModule != null && m_currentDisplayShip != null)
            // {
            //     m_currentDisplayShip.OnModuleHover(m_hoveredModule, false);
            //     m_hoveredModule = null;
            //     m_currentDisplayShip = null;
            // }
        }
    }

    private void HandleModuleSelection(Vector3? screenPosition = null)
    {
        if (GetCameraRaycast(out RaycastHit hit, m_layerDefault, 1000f, screenPosition))
        {
            SpaceShip ship = hit.collider.GetComponentInParent<SpaceShip>();
            if (ship != null)
                ship.OnClicked(hit.point, hit.collider);
        }
    }

    public void RotateCamera(float deltaRotationY, float deltaRotationX)
    {
        m_currentRotationY += deltaRotationY;
        m_currentRotationX = Mathf.Clamp(m_currentRotationX + deltaRotationX, -80f, 80f);
    }

    public void ZoomCamera(float deltaZoom)
    {
        m_currentZoom = Mathf.Clamp(m_currentZoom + deltaZoom * m_zoomSpeed, m_minZoom, m_maxZoom);
    }

    public void SetZoom(float normalizedZoom)
    {
        m_currentZoom = Mathf.Lerp(m_minZoom, m_maxZoom, normalizedZoom);
    }

    public void CameraMove_LeftRightUpDown(Vector2 screenDelta)
    {
        if (m_targetCamera == null) return;

        // Transform 추적 중이면 해제 (팬 이동 시 고정 위치로 전환)
        m_currentTarget = null;

        // 카메라의 오른쪽(Right)과 위쪽(Up) 방향 벡터 구하기
        Vector3 cameraRight = m_targetCamera.transform.right;
        Vector3 cameraUp = m_targetCamera.transform.up;

        // 화면 이동량을 월드 공간으로 변환 (줌 레벨에 비례)
        float panScale = m_currentZoom * m_panSpeed;
        Vector3 worldDelta = (-cameraRight * screenDelta.x - cameraUp * screenDelta.y) * panScale;

        // 타겟 위치 이동
        m_targetPosition += worldDelta;
    }

    private void CameraMove_FrontBack(float deltaZoom)
    {
        float moveSpeed = m_currentZoom * 0.5f; // 줌 레벨에 비례한 이동 속도
        Vector3 moveDirection = m_targetCamera.transform.forward * deltaZoom * moveSpeed;
        m_targetPosition += moveDirection;
    }

    


    private bool IsPointerOverUIObject()
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current);

        // 터치 입력 체크
        if (Input.touchCount > 0)
            eventData.position = Input.GetTouch(0).position;
        // 마우스 입력 체크
        else
            eventData.position = Input.mousePosition;

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

        Vector3 inputPos = screenPosition ?? Input.mousePosition;
        Ray ray = m_targetCamera.ScreenPointToRay(inputPos);
        if (layerMask == default)
            return Physics.Raycast(ray, out hit, maxDistance);
        else
            return Physics.Raycast(ray, out hit, maxDistance, layerMask);
    }

    // Transform을 타겟으로 설정 (움직이는 오브젝트 추적용)
    public void SetTargetTransform(Transform target)
    {
        if (target == null) return;

        // 처음 설정할 때는 즉시 위치 동기화
        if (m_currentTarget == null && m_targetPosition == Vector3.zero)
        {
            m_targetPosition = target.position;
            m_interpolatedTargetPosition = target.position;
        }

        m_currentTarget = target;
        m_targetPosition = target.position;
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

}