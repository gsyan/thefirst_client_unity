using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class HandleInputMouse
{
    private static readonly LayerMask s_pickMask = ~(1 << 13); // 13 = Shield layer

    private CameraController m_camera;
    private bool m_inputBlockedByUI;
    private Collider m_tapHitCollider;
    private bool m_tutorialWaitingForAnyClick;
    private bool m_wasWaitingForAnyClickAtPress;
    public Vector3 m_lastInputScreenPosition;

    public HandleInputMouse(CameraController camera)
    {
        m_camera = camera;
        EventManager.Subscribe_TutorialWaitingForAnyClickChanged(OnTutorialWaitingForAnyClickChanged);
    }

    // CameraController.OnDestroy에서 호출 — EventManager 정적 이벤트에 걸린 구독을 해제
    public void Unsubscribe()
    {
        EventManager.Unsubscribe_TutorialWaitingForAnyClickChanged(OnTutorialWaitingForAnyClickChanged);
    }

    private void OnTutorialWaitingForAnyClickChanged(bool isWaiting)
    {
        m_tutorialWaitingForAnyClick = isWaiting;
    }

    // cameraInputEnabled: 갤럭시뷰 등에서 CameraController가 3D 카메라 조작(드래그 회전/줌/모듈 레이캐스트)을 막고 싶을 때 false로 전달됨.
    // 단, 튜토리얼 AnyClick 감지/소비는 카메라 조작과 무관한 별개 기능이라 이 값과 상관없이 항상 동작해야 함
    public void Process(bool cameraInputEnabled)
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector3 mousePos = mouse.position.ReadValue();
        bool cameraInputAllowed = cameraInputEnabled == true && m_camera.IsScreenPositionInInputRange(mousePos);

        if (cameraInputAllowed == true)
        {
            if (mouse.rightButton.wasPressedThisFrame == true)
            {
                m_camera.OnDragStart(mousePos);
            }
            else if (mouse.rightButton.isPressed == true)
            {
                m_camera.OnDragMove(mousePos);
            }
        }

        if (mouse.leftButton.wasPressedThisFrame == true)
        {
            m_lastInputScreenPosition = mousePos;
            m_inputBlockedByUI = IsPointerOverUIObject(mousePos);
            // press 시점 스냅샷 — 이 클릭 자체가(TargetClick 등으로) 스텝을 AnyClick으로 바꿔도,
            // 같은 클릭이 새 스텝의 AnyClick까지 이어서 소비하지 않도록 제스처 시작 시점 값을 고정
            m_wasWaitingForAnyClickAtPress = m_tutorialWaitingForAnyClick;
            if (cameraInputAllowed == true && m_inputBlockedByUI == false)
            {
                m_tapHitCollider = m_camera.GetCameraRaycast(out RaycastHit downHit, s_pickMask, 3000f, mousePos)
                    ? downHit.collider : null;
            }
        }
        else if (mouse.leftButton.wasReleasedThisFrame == true)
        {
            // 튜토리얼이 AnyClick(화면 아무 곳이나 클릭) 대기 중이었으면(press 시점 기준) 이 클릭은 튜토리얼이 우선 소비 —
            // 3D 탭 처리(HandleModuleSelection/EmptySpaceTapped)와 별도 폴링 루프가 같은 클릭을 다투는 경쟁 상태 자체를 없앰.
            // TutorialManager를 직접 참조하지 않고 EventManager로 상태 구독/소비 요청만 주고받음 — cameraInputEnabled와 무관하게 항상 동작
            if (m_wasWaitingForAnyClickAtPress == true)
            {
                EventManager.Trigger_ConsumeAnyClick();
                m_inputBlockedByUI = false;
                m_tapHitCollider = null;
                return;
            }

            // press~release 사이에 새 UI(튜토리얼 등)가 열렸을 수 있어 release 시점도 다시 확인 —
            // 단 press 때 이미 UI 위였다면(버튼 누르고 3D로 끌고 나가는 경우) 여전히 차단
            if (m_inputBlockedByUI == false && IsPointerOverUIObject(mousePos) == false)
            {
                // 갤럭시뷰(탐사 그리드) 중엔 cameraInputEnabled가 항상 false로 유지되어 위 일반 경로를 타지 않으므로 별도 처리
                if (m_camera.IsGalaxyView == true)
                {
                    m_camera.HandleGalaxyGridSelection(mousePos);
                }
                else if (cameraInputAllowed == true)
                {
                    if (m_tapHitCollider != null)
                    {
                        if (m_camera.GetCameraRaycast(out RaycastHit upHit, s_pickMask, 3000f, mousePos) && upHit.collider == m_tapHitCollider)
                            m_camera.HandleModuleSelection(mousePos);
                    }
                    else
                    {
                        m_camera.HandleModuleSelection(mousePos);
                    }
                }
            }
            m_inputBlockedByUI = false;
            m_tapHitCollider = null;
        }

        if (cameraInputAllowed == true)
        {
            float scrollDelta = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollDelta) > 0.01f)
                m_camera.ZoomCamera(-scrollDelta * 0.5f);
        }
    }

    private bool IsPointerOverUIObject(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        PointerEventData eventData = new PointerEventData(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }
}
