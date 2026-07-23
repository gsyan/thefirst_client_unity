// 함대설정(정찰) UI — 대치 중인 내 함대/적 함대를 좌우 분할뷰로 표시
// "내 함대"는 항상 실제 메인카메라(CameraController.m_targetCamera)로 보여줌 — 로비 풀스크린이든 이 대치 화면이든 동일 카메라가
// viewport rect만 바뀌며 계속 담당(로비↔함대편집 진입 애니메이션과 일관된 로직 공유 목적). "적 함대"만 이 화면 전용의 별도 카메라로 비춤
// Camera.rect로 화면을 반씩 나눔 — 터치 좌표를 바로 해당 카메라로 레이캐스트할 수 있어 3D 피킹(후속 단계)이 단순해짐
// 두 함대는 이미 대치 거리(k_enemyEncounterDistance)만큼 떨어져 있어 culling mask 분리 없이 카메라 위치+LookAt만으로 서로의 함대가 화면에 안 걸리게 함
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class UIFleetStandoffView : MonoBehaviour
{
    private const float k_myFleetViewportX = 0.5f; // 내 함대(메인카메라) 쪽 — 우측 절반, 고정 50/50(이 대치 화면 한정. 유동 폭이 필요한 로비 함대편성 UI와는 별개)

    [SerializeField] private Camera m_enemyFleetCamera; // Rect: 좌측 절반 (0, 0, 0.5, 1) — 이 화면 전용 카메라
    [SerializeField] private Button m_backButton;        // 함대설정 버튼이 ButtonContainer 안에 있어 뷰가 열리면 같이 숨겨지므로, 복귀 전용 버튼을 이 뷰 안에 별도로 둠

    [SerializeField] private float m_viewDistance = 20f;  // 적함대 카메라 — 함대 중심에서 카메라까지 거리
    [SerializeField] private float m_viewHeight = 6f;     // 적함대 카메라 — 살짝 위에서 내려다보는 각도용 높이
    [SerializeField] private float m_rotationSpeed = 0.3f; // 적함대 카메라 드래그 회전 속도(도/픽셀) — 내 함대 쪽은 CameraController 자체 회전 로직을 그대로 씀

    // 씬에 배치해둔 EnemyFleetCamera의 초기 각도(월드 eulerAngles pitch=15.13, yaw=21.94)를 그대로 시작 각도로 사용 —
    // 적 함대가 어느 방향을 보고 있든(회전과 무관하게 위치만 기준) 항상 이 상대 각도로 시작해야 하므로 값으로 고정
    [SerializeField] private float m_initialYaw = 21.9377365f;
    [SerializeField] private float m_initialPitch = 15.1318998f;

    private System.Action m_onClose;

    private const float k_pitchClampDeg = 80f; // 카메라가 함대를 정수리/바닥에서 내려다보며 뒤집히지 않게 제한

    private Transform m_enemyFleetTransform;
    private float m_enemyYaw;
    private float m_enemyPitch;
    private bool m_isDraggingEnemy;
    private Vector2 m_lastPointerScreenPos;

    private void Awake()
    {
        if (m_backButton != null)
            m_backButton.onClick.AddListener(OnClickBack);
    }

    // onClose: 이 뷰를 닫을 때 호출부(UIPanelPrepareBattle)가 3버튼 UI를 다시 보여주기 위한 콜백
    public void Open(SpaceFleet myFleet, SpaceFleet enemyFleet, System.Action onClose)
    {
        if (myFleet == null || enemyFleet == null) return;

        m_onClose = onClose;
        m_enemyFleetTransform = enemyFleet.transform;
        m_enemyYaw = m_initialYaw;
        m_enemyPitch = m_initialPitch;
        m_isDraggingEnemy = false;

        UpdateEnemyCameraOrbit();

        // 내 함대 쪽 — 메인카메라를 우측 절반으로 축소하고, 입력도 그 구간에서만 받게 제한. 회전/줌은 CameraController 자체 로직이 그대로 처리
        if (CameraController.Instance != null)
        {
            CameraController.Instance.SetTargetOfCameraController(myFleet.transform);
            CameraController.Instance.SetViewportRect(k_myFleetViewportX, 1f - k_myFleetViewportX);
            CameraController.Instance.SetInputScreenXRange(k_myFleetViewportX, 1f);
        }

        gameObject.SetActive(true);
    }

    public void Close()
    {
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
    }

    // 적 함대 카메라 전용 — 좌측 절반에서 시작한 드래그만 반응, 함대를 중심으로 좌우(yaw)/상하(pitch) 회전
    // 내 함대 쪽(CameraController)과 동일한 입력 컨벤션: 마우스는 우클릭 드래그(좌클릭은 추후 함선 피킹용으로 비워둠), 터치는 버튼 구분 없는 한 손가락 드래그
    private void HandleEnemyCameraDrag()
    {
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
        UpdateEnemyCameraOrbit();
    }

    private void OnClickBack()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        Close();

        if (m_onClose != null) m_onClose();
    }

    private void UpdateEnemyCameraOrbit()
    {
        if (m_enemyFleetTransform == null) return;

        Vector3 baseOffset = Vector3.back * m_viewDistance + Vector3.up * m_viewHeight;
        Vector3 rotatedOffset = Quaternion.Euler(m_enemyPitch, m_enemyYaw, 0f) * baseOffset;
        m_enemyFleetCamera.transform.position = m_enemyFleetTransform.position + rotatedOffset;
        m_enemyFleetCamera.transform.LookAt(m_enemyFleetTransform.position);
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
