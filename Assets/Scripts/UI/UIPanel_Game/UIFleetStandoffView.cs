// 함대설정(정찰) UI — 대치 중인 내 함대/적 함대를 좌우 분할뷰로 표시
// "내 함대"는 항상 실제 메인카메라(CameraController.m_targetCamera)로 보여줌 — 로비 풀스크린이든 이 대치 화면이든 동일 카메라가
// viewport rect만 바뀌며 계속 담당(로비↔함대편집 진입 애니메이션과 일관된 로직 공유 목적). "적 함대"만 이 화면 전용의 별도 카메라로 비춤
// Camera.rect로 화면을 반씩 나눔 — 터치 좌표를 바로 해당 카메라로 레이캐스트할 수 있어 3D 피킹이 단순해짐
// 두 함대는 이미 대치 거리(k_enemyEncounterDistance)만큼 떨어져 있어 culling mask 분리 없이 카메라 위치+LookAt만으로 서로의 함대가 화면에 안 걸리게 함
// 내 함선 클릭 → UIPanelSpace.OnShipSelectedAutoTabSwitch가 UIPanelFleet을 열고, 그 열림/닫힘에 맞춰 이 화면은 좌측 EnemyFleetCamera를 숨김/복원함(OnFleetCompositionPanelChanged)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class UIFleetStandoffView : MonoBehaviour
{
    private const float k_myFleetViewportX = 0.5f; // 내 함대(메인카메라) 쪽 — 우측 절반, 고정 50/50(이 대치 화면 한정. 유동 폭이 필요한 로비 함대편성 UI와는 별개)

    [SerializeField] private Camera m_enemyFleetCamera; // Rect: 좌측 절반 (0, 0, 0.5, 1) — 이 화면 전용 카메라
    [SerializeField] private RectTransform m_dividerLine; // 좌우 분할 경계선 — 정적 50% 고정이 아니라 매 프레임 카메라 rect의 왼쪽 경계(GetViewportX)를 따라감

    [SerializeField] private float m_viewDistance = 20f;  // 적함대 카메라 — 함대 중심에서 카메라까지 거리
    [SerializeField] private float m_viewHeight = 6f;     // 적함대 카메라 — 살짝 위에서 내려다보는 각도용 높이
    [SerializeField] private float m_rotationSpeed = 0.3f; // 적함대 카메라 드래그 회전 속도(도/픽셀) — 내 함대 쪽은 CameraController 자체 회전 로직을 그대로 씀

    // 씬에 배치해둔 EnemyFleetCamera의 초기 각도(월드 eulerAngles pitch=15.13, yaw=21.94)를 그대로 시작 각도로 사용 —
    // 적 함대가 어느 방향을 보고 있든(회전과 무관하게 위치만 기준) 항상 이 상대 각도로 시작해야 하므로 값으로 고정
    [SerializeField] private float m_initialYaw = 21.9377365f;
    [SerializeField] private float m_initialPitch = 15.1318998f;

    private const float k_pitchClampDeg = 80f; // 카메라가 함대를 정수리/바닥에서 내려다보며 뒤집히지 않게 제한
    private const float k_positionLerpSpeed = 10f; // CameraController.k_positionLerpSpeed와 동일 값 — 워프인 등 타겟 이동을 부드럽게 추종

    private Transform m_enemyFleetTransform;
    private SpaceFleet m_enemyFleet;
    private Vector3 m_interpolatedEnemyPosition; // 카메라가 실제로 바라보는(보간된) 함대 위치 — 타겟 원본 위치를 그대로 스냅하지 않음
    private float m_enemyYaw;
    private float m_enemyPitch;
    private bool m_isDraggingEnemy;
    private Vector2 m_lastPointerScreenPos;

    private const float k_tapMoveThreshold = 12f; // 이 이하 이동이면 드래그가 아니라 탭/클릭으로 판정(픽셀)
    private Vector2 m_leftPressStartPos;
    private bool m_isLeftPressActive;

    // ProcessEnemyAreaTap이 세팅하고 OnFleetPanelChanged가 소비 — CurrentPanelChanged 이벤트가 UIPanelFleet.OnShowUIPanel보다
    // 먼저 발행되어 그 시점엔 UIPanelFleet.m_isReadOnlyMode를 아직 믿을 수 없으므로, 호출부가 직접 기억해뒀다가 알려줌
    private bool m_pendingEnemyReadOnlyOpen;

    private void OnEnable()
    {
        EventManager.Subscribe_CurrentPanelChanged(OnFleetPanelChanged);
    }

    private void OnDisable()
    {
        EventManager.Unsubscribe_CurrentPanelChanged(OnFleetPanelChanged);
    }

    // 내 함선 클릭 시 UIPanelSpace가 함대편성 패널을 여는데(OnShipSelectedAutoTabSwitch), 그 패널이 열리면 좌측 적함대 카메라를 숨겨
    // 우측 뷰가 화면 전체로 확장되고 비워진 우측엔 함대편성 UI가 채워지도록 함. 패널이 닫히면 다시 좌우 절반으로 복귀
    private void OnFleetPanelChanged(string panelName)
    {
        if (gameObject.activeSelf == false) return;

        bool isFleetCompositionOpen = panelName == "UIPanelFleet";

        // 적 함선 클릭으로 읽기전용 오픈된 경우엔 좌측 적 카메라/입력범위를 그대로 둠 —
        // UIPanelFleet도 이 경우 카메라를 안 건드리므로 여기서도 손대면 안 됨(좌측 3D가 계속 적 함대를 비춰야 함)
        bool isEnemyReadOnlyOpen = isFleetCompositionOpen == true && m_pendingEnemyReadOnlyOpen == true;
        if (isFleetCompositionOpen == false)
            m_pendingEnemyReadOnlyOpen = false; // 패널이 닫히면 다음 오픈에 영향 없도록 리셋

        if (isEnemyReadOnlyOpen == true)
            return;

        if (m_enemyFleetCamera != null)
            m_enemyFleetCamera.gameObject.SetActive(isFleetCompositionOpen == false);
        if (m_dividerLine != null)
            m_dividerLine.gameObject.SetActive(isFleetCompositionOpen == false);

        // 탭이 열리면 내 함대 카메라가 화면 전체(좌측 포함)로 확장되므로 입력 허용 구간도 전체로 넓혀야
        // 빈 공간(좌측 포함) 탭으로 EmptySpaceTapped가 발생해 탭을 닫을 수 있음. 닫히면 다시 우측 절반으로 복귀
        if (CameraController.Instance != null)
        {
            if (isFleetCompositionOpen == true)
                CameraController.Instance.SetInputScreenXRange(0f, 1f);
            else
                CameraController.Instance.SetInputScreenXRange(k_myFleetViewportX, 1f);
        }
    }

    public void Open(SpaceFleet myFleet, SpaceFleet enemyFleet)
    {
        if (myFleet == null || enemyFleet == null) return;

        m_enemyFleetTransform = enemyFleet.transform;
        m_enemyFleet = enemyFleet;
        m_interpolatedEnemyPosition = enemyFleet.transform.position; // 보간 시작점을 실제 위치로 스냅 — 원점(0,0,0)에서부터 날아오는 것처럼 보이는 것 방지
        m_enemyYaw = m_initialYaw;
        m_enemyPitch = m_initialPitch;
        m_isDraggingEnemy = false;
        m_isLeftPressActive = false;

        UpdateEnemyCameraOrbit();

        // 내 함대 쪽 — 메인카메라를 우측 절반으로 축소하고, 입력도 그 구간에서만 받게 제한. 회전/줌은 CameraController 자체 로직이 그대로 처리
        if (CameraController.Instance != null)
        {
            CameraController.Instance.SetTargetOfCameraController(myFleet.transform);
            CameraController.Instance.SetViewportRect(k_myFleetViewportX, 1f - k_myFleetViewportX);
            CameraController.Instance.SetInputScreenXRange(k_myFleetViewportX, 1f);
        }

        if (m_enemyFleetCamera != null)
            m_enemyFleetCamera.gameObject.SetActive(true);
        if (m_dividerLine != null)
            m_dividerLine.gameObject.SetActive(true);

        // 함대편성 패널이 닫힐 때 풀스크린이 아니라 이 화면의 우측 절반으로 복귀하도록 재정의
        UIPanelFleet panelFleet = UIManager.Instance.GetPanel<UIPanelFleet>("UIPanelFleet");
        if (panelFleet != null)
            panelFleet.SetClosedCameraRectProvider(() => new Rect(k_myFleetViewportX, 0f, 1f - k_myFleetViewportX, 1f));

        gameObject.SetActive(true);
    }

    public void Close()
    {
        UIPanelFleet panelFleet = UIManager.Instance.GetPanel<UIPanelFleet>("UIPanelFleet");
        if (panelFleet != null)
        {
            panelFleet.CloseImmediateIfOpen();
            panelFleet.SetClosedCameraRectProvider(null);
        }

        gameObject.SetActive(false);

        if (CameraController.Instance != null)
        {
            CameraController.Instance.SetViewportRect(0f, 1f);
            CameraController.Instance.SetInputScreenXRange(0f, 1f);
        }
    }

    private void Update()
    {
        HandleEnemyCameraDrag();
        HandleEnemyCameraClick();
        UpdateDividerLinePosition();
    }

    // 타겟(적 함대)의 이동은 각자의 Update()에서 일어나므로, 그 이후인 LateUpdate에서 위치를 읽어야
    // 이번 프레임 이동이 반영된 최신 위치를 보간할 수 있음(CameraController.LateUpdate와 동일 이유) — Update에서 읽으면 떨림 발생
    private void LateUpdate()
    {
        if (m_enemyFleetCamera != null && m_enemyFleetCamera.gameObject.activeInHierarchy == true)
            UpdateEnemyCameraOrbit();
    }

    // 좌측(적함대) 영역 좌클릭/탭 — 우클릭 드래그(회전)와 별개 입력. 함선 명중 시 UIPanelFleet을 읽기전용 모드로 열어 그 함선을 선택,
    // 빈 공간 명중이면 패널을 닫음. 이동량이 k_tapMoveThreshold를 넘으면 드래그로 보고 탭 처리하지 않음
    private void HandleEnemyCameraClick()
    {
        // 함대편성 패널이 열려 적함대 카메라가 비활성화된 동안에는 좌측 영역 탭을 처리하지 않음 —
        // Camera.ScreenPointToRay는 GameObject 비활성 상태에서도 동작해, 방치 시 내 함선을 눌러도 적함선 정보 패널이 뜨는 오류가 났었음
        if (m_enemyFleetCamera == null || m_enemyFleetCamera.gameObject.activeInHierarchy == false) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            m_leftPressStartPos = mouse.position.ReadValue();
            m_isLeftPressActive = IsPointerOverUIObject(m_leftPressStartPos) == false;
            return;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (m_isLeftPressActive == false) return;
            m_isLeftPressActive = false;

            Vector2 releasePos = mouse.position.ReadValue();
            if (Vector2.Distance(releasePos, m_leftPressStartPos) > k_tapMoveThreshold) return;

            ProcessEnemyAreaTap(releasePos);
        }
#elif UNITY_ANDROID || UNITY_IOS
        var touches = Touch.activeTouches;
        if (touches.Count != 1) return;

        Touch touch = touches[0];

        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            m_leftPressStartPos = touch.screenPosition;
            m_isLeftPressActive = IsPointerOverUIObject(m_leftPressStartPos) == false;
            return;
        }

        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended)
        {
            if (m_isLeftPressActive == false) return;
            m_isLeftPressActive = false;

            Vector2 releasePos = touch.screenPosition;
            if (Vector2.Distance(releasePos, m_leftPressStartPos) > k_tapMoveThreshold) return;

            ProcessEnemyAreaTap(releasePos);
        }
        else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
        {
            m_isLeftPressActive = false;
        }
#endif
    }

    private void ProcessEnemyAreaTap(Vector2 screenPos)
    {
        if (screenPos.x >= Screen.width * k_myFleetViewportX) return; // 우측(내 함대) 영역은 CameraController가 자체 처리
        if (m_enemyFleetCamera == null || m_enemyFleet == null) return;

        Ray ray = m_enemyFleetCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 3000f) == true)
        {
            SpaceShip ship = hit.collider.GetComponentInParent<SpaceShip>();
            if (ship != null && ship.m_ownerFleet == m_enemyFleet)
            {
                UIPanelFleet panelFleet = UIManager.Instance.GetPanel<UIPanelFleet>("UIPanelFleet");
                if (panelFleet != null)
                {
                    m_pendingEnemyReadOnlyOpen = true;
                    panelFleet.OpenForFleet(m_enemyFleet, isReadOnly: true, ship.m_shipInfo.positionIndex);
                }
                return;
            }
        }

        UIPanelFleet openPanelFleet = UIManager.Instance.GetPanel<UIPanelFleet>("UIPanelFleet");
        if (openPanelFleet != null)
            openPanelFleet.CloseImmediateIfOpen();
    }

    // DividerLine을 카메라 rect의 왼쪽 경계(GetViewportX)에 매 프레임 맞춤 — UIPanelSpace의 뷰포트 애니메이션 중에도
    // 그 값을 직접 폴링하므로 누가 rect를 바꾸고 있는지와 무관하게 항상 실제 경계를 따라감
    private void UpdateDividerLinePosition()
    {
        if (m_dividerLine == null || m_dividerLine.gameObject.activeSelf == false) return;
        if (CameraController.Instance == null) return;

        float viewportX = CameraController.Instance.GetViewportX();
        Vector2 anchorMin = m_dividerLine.anchorMin;
        Vector2 anchorMax = m_dividerLine.anchorMax;
        anchorMin.x = viewportX;
        anchorMax.x = viewportX;
        m_dividerLine.anchorMin = anchorMin;
        m_dividerLine.anchorMax = anchorMax;
    }

    // 적 함대 카메라 전용 — 좌측 절반에서 시작한 드래그만 반응, 함대를 중심으로 좌우(yaw)/상하(pitch) 회전
    // 내 함대 쪽(CameraController)과 동일한 입력 컨벤션: 마우스는 우클릭 드래그(좌클릭은 추후 함선 피킹용으로 비워둠), 터치는 버튼 구분 없는 한 손가락 드래그
    private void HandleEnemyCameraDrag()
    {
        if (m_enemyFleetCamera == null || m_enemyFleetCamera.gameObject.activeInHierarchy == false) return;

#if UNITY_EDITOR || UNITY_STANDALONE
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 screenPos = mouse.position.ReadValue();

        if (mouse.rightButton.wasPressedThisFrame)
        {
            if (IsPointerOverUIObject(screenPos) == true)
                return; // 뒤로가기 버튼 등 UI 위 터치는 회전 시작으로 취급하지 않음

            m_isDraggingEnemy = screenPos.x < Screen.width * k_myFleetViewportX;
            m_lastPointerScreenPos = screenPos;
            return;
        }

        if (mouse.rightButton.wasReleasedThisFrame)
        {
            m_isDraggingEnemy = false;
            return;
        }

        if (mouse.rightButton.isPressed == false || m_isDraggingEnemy == false) return;

        Vector2 dragDelta = screenPos - m_lastPointerScreenPos;
        m_lastPointerScreenPos = screenPos;
#elif UNITY_ANDROID || UNITY_IOS
        var touches = Touch.activeTouches;
        if (touches.Count != 1)
        {
            // 두 손가락 이상 겹치는 중엔 드래그 중단 — 다시 한 손가락으로 돌아왔을 때 오래된 좌표와의 차이로 카메라가 튀는 것 방지
            m_isDraggingEnemy = false;
            return;
        }

        Touch touch = touches[0];
        Vector2 screenPos = touch.screenPosition;

        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            if (IsPointerOverUIObject(screenPos) == true)
                return; // 뒤로가기 버튼 등 UI 위 터치는 회전 시작으로 취급하지 않음

            m_isDraggingEnemy = screenPos.x < Screen.width * k_myFleetViewportX;
            m_lastPointerScreenPos = screenPos;
            return;
        }

        if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
        {
            m_isDraggingEnemy = false;
            return;
        }

        if (m_isDraggingEnemy == false) return;

        Vector2 dragDelta = screenPos - m_lastPointerScreenPos;
        m_lastPointerScreenPos = screenPos;
#else
        return;
#endif

        m_enemyYaw += dragDelta.x * m_rotationSpeed;
        m_enemyPitch = Mathf.Clamp(m_enemyPitch - dragDelta.y * m_rotationSpeed, -k_pitchClampDeg, k_pitchClampDeg);
        // 위치/회전 반영은 LateUpdate에서 일괄 처리(CameraController와 동일 패턴) — 여기선 각도값만 갱신
    }

    // CameraController.LateUpdate와 동일한 이유로 LateUpdate에서 위치를 읽음(타겟 이동은 각자의 Update에서 일어나므로,
    // 그 이후에 읽어야 이번 프레임 이동분까지 반영된 최신 위치를 따라감 — Update에서 읽으면 스크립트 실행 순서에 따라 한 프레임씩 어긋나며 떨림)
    private void UpdateEnemyCameraOrbit()
    {
        if (m_enemyFleetTransform == null) return;

        // 워프인 등으로 함대가 계속 움직여도 카메라가 순간이동하듯 튀지 않도록, 목표 위치를 그대로 스냅하지 않고 부드럽게 보간
        m_interpolatedEnemyPosition = Vector3.Lerp(m_interpolatedEnemyPosition, m_enemyFleetTransform.position, k_positionLerpSpeed * Time.unscaledDeltaTime);

        Vector3 baseOffset = Vector3.back * m_viewDistance + Vector3.up * m_viewHeight;
        Vector3 rotatedOffset = Quaternion.Euler(m_enemyPitch, m_enemyYaw, 0f) * baseOffset;
        m_enemyFleetCamera.transform.position = m_interpolatedEnemyPosition + rotatedOffset;
        m_enemyFleetCamera.transform.LookAt(m_interpolatedEnemyPosition);
    }

    // 특정 스크린 좌표가 UI 위인지 직접 레이캐스트로 판정 — EventSystem.IsPointerOverGameObject()의 파라미터 없는 버전은
    // 터치 환경에서 손가락 ID를 못 잡는 경우가 있어(CameraController.HandleInputTouch와 동일한 이유로) 이 방식을 씀
    private bool IsPointerOverUIObject(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        PointerEventData eventData = new PointerEventData(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }
}
