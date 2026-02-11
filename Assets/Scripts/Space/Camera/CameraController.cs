using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

// 카메라 중심점 타겟
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
    private float m_zoomSpeed = 50f;
    private float m_panSpeed = 0.001f;
    private float m_minZoom = 100f;
    private float m_maxZoom = 1500f; // 카메라 줌

    // Current camera state
    private Transform m_currentTarget; // (Optional) 움직이는 타겟을 따라가기 위한 Transform
    private Transform m_currentTargetBackup; // (Optional) 움직이는 타겟을 따라가기 위한 Transform
    private Vector3 m_targetPosition; // 카메라가 바라보는 목표 위치
    private Vector3 m_interpolatedTargetPosition; // 부드럽게 보간된 타겟 위치
    private float m_currentZoom;
    public float CurrentZoom => m_currentZoom;
    private float m_currentRotationY = 200f;
    private float m_currentRotationX = 30f;
    
    // UIPanelSpace 활성화 시 true, 비활성화 시 false
    private bool m_shipSelectionEnabled = false;
    public void SetShipSelectionEnabled(bool enabled) { m_shipSelectionEnabled = enabled; }

    // 카메라 중심점 타겟
    private ECameraFocusTarget m_focusTarget = ECameraFocusTarget.camera_focus_my_fleet;
    public ECameraFocusTarget FocusTarget => m_focusTarget;

    // LayerMask
    private const int m_layerShip = 30;
    private LayerMask m_layerMaskShip = 1 << m_layerShip;
    private const int m_layerShipModule = 31;
    private LayerMask m_layerMaskShipModule = 1 << m_layerShipModule;
    private const int m_layerShield = 13;
    private LayerMask m_layerMaskShield = 1 << m_layerShield;

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

    public void UpdateCameraTransform()
    {
        if (m_targetCamera == null) return;
	
        // Transform이 설정되어 있으면 해당 위치를 따라감
        if (m_currentTarget != null && m_focusTarget == ECameraFocusTarget.camera_focus_my_fleet)
            m_targetPosition = m_currentTarget.position;

        // Center 모드: 매 프레임 두 함대의 중간점을 갱신
        if (m_focusTarget == ECameraFocusTarget.camera_focus_center && m_currentTarget == null)
        {
            var objMgr = ObjectManager.Instance;
            if (objMgr != null && objMgr.m_myFleet != null
                && objMgr.m_enemyFleets.Count > 0 && objMgr.m_enemyFleets[0] != null)
            {
                m_targetPosition = (objMgr.m_myFleet.transform.position
                    + objMgr.m_enemyFleets[0].transform.position) * 0.5f;
            }
        }
        

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

        // 3. 보간된 타겟 위치 기준으로 카메라 배치
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
        UpdateCameraTransform();
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
                CameraPanning(mouseDelta);
                m_startTouchPosition = Input.mousePosition;
            }
        }

        // 마우스 휠 줌 또는 전진/후퇴
        float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollDelta) > 0.01f)
        {
            if (Mathf.Abs(scrollDelta) > 0.01f)
                ZoomCamera(-scrollDelta * 5f);
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
                        CameraPanning(touchCenterDelta);
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
    

    // 3D 클릭으로 내 함선/모듈 선택 (UIPanelSpace 활성 시에만 동작)
    private void HandleModuleSelection(Vector3? screenPosition = null)
    {
        if (!m_shipSelectionEnabled) return;

        LayerMask pickMask = ~m_layerMaskShield;
        if (!GetCameraRaycast(out RaycastHit hit, pickMask, 3000f, screenPosition))
            return;

        SpaceShip ship = hit.collider.GetComponentInParent<SpaceShip>();
        if (ship == null) return;
        if (ship.m_myFleet == null || ship.m_myFleet.m_isEnemyFleet) return;

        // 내함대 보기
        SetCameraFocusTarget(ECameraFocusTarget.camera_focus_my_fleet);

        // 함선 선택 이벤트 (UITabShip에서 동일 함선 중복 체크)
        EventManager.TriggerSpaceShipSelected(ship);

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

    public void ZoomCamera(float deltaZoom)
    {
        m_currentZoom = Mathf.Clamp(m_currentZoom + deltaZoom * m_zoomSpeed, m_minZoom, m_maxZoom);
    }

    public void SetZoom(float normalizedZoom)
    {
        m_currentZoom = Mathf.Lerp(m_minZoom, m_maxZoom, normalizedZoom);
    }

    public void CameraPanning(Vector2 screenDelta)
    {
        if (m_targetCamera == null) return;

        // Normal 모드가 아니면 자유 이동 불가
        if( UIManager.Instance.CanCameraMove() == false)
            return;

        // Transform 추적 중이면 해제 (팬 이동 시 고정 위치로 전환)
        if (m_currentTarget != null)
        {
            m_currentTarget = null;
            m_targetPosition = m_targetCamera.transform.position;
            m_interpolatedTargetPosition = m_targetPosition;
        }

            
        
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

    // 카메라 중심점 전환 (적함대, 중간, 우리함대)
    public void SetCameraFocusTarget(ECameraFocusTarget focusTarget)
    {
        if( m_focusTarget == focusTarget) return;
        ApplyFocusTarget(focusTarget);
    }

    // 카메라 중심점을 순환 전환 (MyFleet → Center → EnemyFleet → MyFleet)
    public void CycleCameraFocusTarget()
    {
        // m_focusTarget = m_focusTarget switch
        // {
        //     ECameraFocusTarget.camera_focus_enemy_fleet => ECameraFocusTarget.camera_focus_center,
        //     ECameraFocusTarget.camera_focus_center => ECameraFocusTarget.camera_focus_my_fleet,
        //     ECameraFocusTarget.camera_focus_my_fleet => ECameraFocusTarget.camera_focus_enemy_fleet,
        //     _ => ECameraFocusTarget.camera_focus_my_fleet
        // };
        // ApplyFocusTarget();
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
                if (objMgr.m_enemyFleets.Count < 1 || objMgr.m_enemyFleets[0] == null) return; // 적 함대 없으면 리턴
                if( m_focusTarget == ECameraFocusTarget.camera_focus_my_fleet)
                {
                    m_currentTargetBackup = m_currentTarget;
                    m_currentTarget = null;
                }                
                m_targetPosition = objMgr.m_enemyFleets[0].transform.position;
                break;
            case ECameraFocusTarget.camera_focus_center:
                if (objMgr.m_enemyFleets.Count < 1 || objMgr.m_enemyFleets[0] == null) return; // 적 함대 없으면 리턴
                if( m_focusTarget == ECameraFocusTarget.camera_focus_my_fleet)
                {
                    m_currentTargetBackup = m_currentTarget;
                    m_currentTarget = null;    
                }                
                m_targetPosition = (myFleet.transform.position + objMgr.m_enemyFleets[0].transform.position) * 0.5f;                    
                break;
            case ECameraFocusTarget.camera_focus_my_fleet:
                if (m_currentTargetBackup == null)
                {
                    m_currentTarget = myFleet.transform;
                }
                else
                {
                    SetTargetOfCameraController(m_currentTargetBackup);
                    m_currentTargetBackup = null;
                }
                break;
        }

        m_focusTarget = focusTarget;
        EventManager.TriggerCameraFocusTargetChanged(focusTarget);
    }

}