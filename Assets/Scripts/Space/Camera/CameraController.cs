using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[System.Serializable]
public enum ECameraControllerMode
{
    Normal                  // 일반 게임 모드
    , Select_Ship
    , Upgrade_Fleet         // 함대 보기 모드
    , Manage_Ship          // 함선 보기 모드
}


public class CameraController : MonoSingleton<CameraController>
{
    [Header("Camera Settings")]
    public Camera m_targetCamera;
    private Camera m_backgroundCamera; // 하단 영역을 Clear하기 위한 배경 카메라
    private float m_rotationSpeed = 0.1f;
    private float m_zoomSpeed = 50f;
    private float m_panSpeed = 0.001f;
    private float m_minZoom = 100f;
    private float m_maxZoom = 1000f;

    // Camera state management
    private Vector3 m_offset;

    // Current camera state
    private Transform m_currentTarget; // (Optional) 움직이는 타겟을 따라가기 위한 Transform
    private Vector3 m_targetPosition; // 카메라가 바라보는 목표 위치
    private Vector3 m_interpolatedTargetPosition; // 부드럽게 보간된 타겟 위치
    private float m_currentZoom;
    private float m_currentRotationY = 0f;
    private float m_currentRotationX = 0f;
    public ECameraControllerMode m_currentMode = ECameraControllerMode.Normal;

    // LayerMask
    private LayerMask m_layerDefault = default;
    private const int m_layerShip = 30;
    private LayerMask m_layerMaskShip = 1 << m_layerShip;
    private const int m_layerShipModule = 31;
    private LayerMask m_layerMaskShipModule = 1 << m_layerShipModule;

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

    public void SwitchCameraMode(ECameraControllerMode mode, Transform viewTarget = null)
    {
        if (m_targetCamera == null) return;

        m_currentMode = mode;

        switch (mode)
        {
            case ECameraControllerMode.Normal:
                break;
            case ECameraControllerMode.Select_Ship:
            case ECameraControllerMode.Upgrade_Fleet:
            case ECameraControllerMode.Manage_Ship:
                break;
        }

        // 카메라 모드 변경 이벤트 발생
        EventManager.TriggerCameraModeChanged(mode);
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

        // 3. 보간된 타겟 위치 + 회전 오프셋 + 세로 오프셋 = 카메라 위치
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
    //private bool m_isPanning = false;
    private Vector3 m_startTouchPosition;
    private float m_startRotationY;
    private float m_startRotationX;
    private float m_lastPinchDistance = 0f;
    private Vector2 m_lastTwoTouchCenter = Vector2.zero;

    // 이전 프레임 터치 위치 저장 (방향 벡터 계산용)
    private Vector2 m_prevTouch0Position;
    private Vector2 m_prevTouch1Position;

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
            //m_isPanning = true;
            m_startTouchPosition = Input.mousePosition;
            HandleModuleSelection(m_startTouchPosition); // 카메라 모드에 따라 처리 필요
        }
        else if (Input.GetMouseButtonUp(0) == true)
        {
            //m_isPanning = false;
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
                m_prevTouch0Position = touch0.position;
                m_prevTouch1Position = touch1.position;
                //m_isPanning = false;
            }
            else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
            {
                // 각 터치의 이동 방향 벡터 계산
                Vector2 moveVector0 = touch0.position - m_prevTouch0Position;
                Vector2 moveVector1 = touch1.position - m_prevTouch1Position;

                // 최소 이동량 체크 (노이즈 방지)
                if (moveVector0.magnitude > 1f && moveVector1.magnitude > 1f)
                {
                    // 방향 벡터 정규화 후 내적 계산
                    float dotProduct = Vector2.Dot(moveVector0.normalized, moveVector1.normalized);

                    // dot < -0.5: 반대 방향 → 핀치 줌
                    if (dotProduct < -0.5f)
                    {
                        //m_isPanning = false;
                        float deltaPinch = currentPinchDistance - m_lastPinchDistance;

                        if (m_currentTarget != null)
                            ZoomCamera(-deltaPinch * 0.01f);
                        else
                            CameraMove_FrontBack(deltaPinch * 0.01f);
                    }
                    // dot > 0.8: 같은 방향 → 팬 이동
                    else if (dotProduct > 0.8f)
                    {
                        //m_isPanning = true;
                        Vector2 touchCenterDelta = currentTouchCenter - m_lastTwoTouchCenter;
                        CameraMove_LeftRightUpDown(touchCenterDelta);
                    }
                    // 그 외: 애매한 경우 → 이전 상태 유지 (아무것도 안 함)
                }

                m_lastPinchDistance = currentPinchDistance;
                m_lastTwoTouchCenter = currentTouchCenter;
                m_prevTouch0Position = touch0.position;
                m_prevTouch1Position = touch1.position;
            }
            else if (touch0.phase == TouchPhase.Ended || touch1.phase == TouchPhase.Ended)
            {
                //m_isPanning = false;
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
    

    private void HandleModuleSelection(Vector3? screenPosition = null)
    {
        if(m_currentMode != ECameraControllerMode.Manage_Ship)
            return;

        if (GetCameraRaycast(out RaycastHit hit, m_layerDefault, 1000f, screenPosition))
        {
            SpaceShip ship = hit.collider.GetComponentInParent<SpaceShip>();
            ModuleBase module = hit.collider.GetComponentInParent<ModuleBase>();
            if (ship != null && ship.gameObject == m_currentTarget.gameObject && module != null)
                EventManager.TriggerSpaceShipModuleSelected_TabUpgrade(ship, module);
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

        // Normal 모드가 아니면 자유 이동 불가
        if( UIManager.Instance.CanCameraMove() == false)
            return;

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
    public void SetTargetOfCameraController(Transform target)
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

    // 카메라 뷰포트를 화면 위쪽 절반으로 설정
    public void SetCameraViewportToUpperHalf()
    {
        if (m_targetCamera != null)
        {
            m_targetCamera.rect = new Rect(0, 0.5f, 1, 0.5f);

            // 하단 영역을 Clear하기 위한 배경 카메라 생성
            if (m_backgroundCamera == null)
            {
                GameObject bgCamObj = new GameObject("BackgroundCamera");
                bgCamObj.transform.SetParent(m_targetCamera.transform.parent);
                m_backgroundCamera = bgCamObj.AddComponent<Camera>();
                m_backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
                m_backgroundCamera.backgroundColor = Color.black;
                m_backgroundCamera.cullingMask = 0; // 아무것도 렌더링하지 않음
                m_backgroundCamera.depth = m_targetCamera.depth - 1; // 메인 카메라보다 먼저 렌더링
                m_backgroundCamera.rect = new Rect(0, 0, 1, 0.5f); // 하단 절반만
            }
            m_backgroundCamera.enabled = true;
        }
    }

    // 카메라 뷰포트를 전체 화면으로 복구
    public void ResetCameraViewport()
    {
        if (m_targetCamera != null)
        {
            m_targetCamera.rect = new Rect(0, 0, 1, 1);

            // 배경 카메라 비활성화
            if (m_backgroundCamera != null)
                m_backgroundCamera.enabled = false;
        }
    }

}