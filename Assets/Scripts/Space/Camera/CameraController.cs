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
    private float m_minZoom = 10f;
    private float m_maxZoom = 50f;

    // Camera state management
    private Transform m_originalTarget;
    private Vector3 m_offset;
    
    // Current camera state
    private Transform m_currentTarget;    
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
                FocusOnTarget(viewTarget, 1.0f);
                break;
            case CameraControllerMode.Upgrade_Fleet:
            case CameraControllerMode.Upgrade_Ship:
                if (viewTarget != null)
                    m_currentTarget = viewTarget;
                UpdateFleetViewPosition();
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
        if (m_targetCamera == null || m_currentTarget == null) return;
        
        // 1. 회전 각도를 라디안으로 변환
        float radiansY = m_currentRotationY * Mathf.Deg2Rad;
        float radiansX = m_currentRotationX * Mathf.Deg2Rad;
        // 2. 구면 좌표계(Spherical Coordinates)로 카메라 위치 계산
        float horizontalDistance = m_currentZoom * Mathf.Cos(radiansX);
        Vector3 rotatedOffset = new Vector3(
            Mathf.Sin(radiansY) * horizontalDistance,
            m_currentZoom * Mathf.Sin(radiansX),
            Mathf.Cos(radiansY) * horizontalDistance
        );

         // 3. 타겟 위치 + 오프셋 = 카메라 위치
        Vector3 targetPosition = m_currentTarget.position;
        m_targetCamera.transform.position = targetPosition + rotatedOffset;
        m_targetCamera.transform.LookAt(targetPosition);
    }

    private bool m_inputEnabled = true;

    public void SetInputEnabled(bool enabled)
    {
        m_inputEnabled = enabled;
    }

    // Input handling
    private bool m_isDragging = false;
    private Vector3 m_startTouchPosition;
    private float m_startRotationY;
    private float m_startRotationX;
    private float m_lastPinchDistance = 0f;

    private void Update()
    {
        HandleInput();

        if (m_currentTarget != null)
            UpdateFleetViewPosition();
    }

    private void HandleInput()
    {
        if (m_inputEnabled == false) return;

        bool inputDown = false;
        bool inputUp = false;
        bool inputHeld = false;
        Vector3 inputPosition = Vector3.zero;

        if (Input.touchCount > 0 && IsPointerOverUIObject())
            return;

        if (Input.touchCount >= 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            float currentPinchDistance = Vector2.Distance(touch0.position, touch1.position);

            if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
            {
                m_lastPinchDistance = currentPinchDistance;
            }
            else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
            {
                float deltaPinch = currentPinchDistance - m_lastPinchDistance;
                ZoomCamera(-deltaPinch * 0.01f);
                m_lastPinchDistance = currentPinchDistance;
            }
        }
        else if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                inputDown = true;
                inputPosition = touch.position;
                HandleModuleSelection(touch.position);
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
        else
        {
            if (Input.GetMouseButtonDown(1))
            {
                inputDown = true;
                inputPosition = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(1))
            {
                inputUp = true;
            }
            else if (Input.GetMouseButton(1))
            {
                inputHeld = true;
                inputPosition = Input.mousePosition;
            }

            if (Input.GetMouseButtonDown(0))
            {
                HandleModuleSelection();
            }
        }

        if (inputDown)
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

        float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollDelta) > 0.01f)
        {
            ZoomCamera(-scrollDelta * 5f);
        }

        if (m_isDragging == false)
            HandleModuleHover();
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
        UpdateFleetViewPosition();
    }

    public void ZoomCamera(float deltaZoom)
    {
        m_currentZoom = Mathf.Clamp(m_currentZoom + deltaZoom * m_zoomSpeed, m_minZoom, m_maxZoom);
        UpdateFleetViewPosition();
    }

    public void SetZoom(float normalizedZoom)
    {
        m_currentZoom = Mathf.Lerp(m_minZoom, m_maxZoom, normalizedZoom);
        UpdateFleetViewPosition();
    }

    private bool IsPointerOverUIObject()
    {
        if (EventSystem.current == null)
            return false;

        if (Input.touchCount > 0)
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Input.GetTouch(0).position;
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            return results.Count > 0;
        }

        return false;
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


    public void FocusOnTarget(Transform target, float transitionTime = 0.5f)
    {
        if (target == null) return;

        // 부드러운 전환 시작
        StartCoroutine(SmoothTransitionToTarget(target, transitionTime));
    }

    private System.Collections.IEnumerator SmoothTransitionToTarget(Transform target, float duration)
    {
        if (m_targetCamera == null || target == null) yield break;

        // 시작 타겟 위치 (현재 m_currentTarget이 있다면 그 위치, 없으면 현재 카메라가 보는 위치)
        Vector3 startTargetPosition = m_currentTarget != null ? m_currentTarget.position : target.position;
        Vector3 endTargetPosition = target.position;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // SmoothStep을 사용한 부드러운 보간
            float smoothT = t * t * (3f - 2f * t);

            // 타겟 위치를 부드럽게 보간
            Vector3 interpolatedTargetPosition = Vector3.Lerp(startTargetPosition, endTargetPosition, smoothT);

            // 현재 회전/줌 설정을 유지하면서 타겟만 이동
            // UpdateFleetViewPosition과 동일한 방식으로 카메라 위치 계산
            float radiansY = m_currentRotationY * Mathf.Deg2Rad;
            float radiansX = m_currentRotationX * Mathf.Deg2Rad;
            float horizontalDistance = m_currentZoom * Mathf.Cos(radiansX);
            Vector3 rotatedOffset = new(
                Mathf.Sin(radiansY) * horizontalDistance,
                m_currentZoom * Mathf.Sin(radiansX),
                Mathf.Cos(radiansY) * horizontalDistance
            );

            m_targetCamera.transform.position = interpolatedTargetPosition + rotatedOffset;
            m_targetCamera.transform.LookAt(interpolatedTargetPosition);

            yield return null;
        }

        // 최종 타겟 설정
        m_currentTarget = target;
        UpdateFleetViewPosition();
    }

}