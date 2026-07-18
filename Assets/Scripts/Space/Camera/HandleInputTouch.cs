using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;


public class HandleInputTouch
{
    private static readonly LayerMask s_pickMask = ~(1 << 13);
    private const float k_dragThreshold = 10f; // 탭/드래그 구분 임계값 (픽셀)

    private CameraController m_camera;
    private bool m_inputBlockedByUI;
    private Collider m_tapHitCollider;
    private bool m_isDragging;
    private bool m_tutorialWaitingForAnyClick;
    private bool m_wasWaitingForAnyClickAtPress;
    public Vector3 m_lastInputScreenPosition;

    private float m_lastPinchDistance = 0f;
    private Vector2 m_lastTwoTouchCenter = Vector2.zero;
    private Vector2 m_prevTouch0Position;
    private Vector2 m_prevTouch1Position;
    private int m_prevTouchCount = 0;

    public HandleInputTouch(CameraController camera)
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

    // cameraInputEnabled: 갤럭시뷰 등에서 CameraController가 3D 카메라 조작(드래그/핀치줌/모듈 레이캐스트)을 막고 싶을 때 false로 전달됨.
    // 단, 튜토리얼 AnyClick 감지/소비는 카메라 조작과 무관한 별개 기능이라 이 값과 상관없이 항상 동작해야 함
    public void Process(bool cameraInputEnabled)
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
                m_isDragging = false;
                m_lastPinchDistance = currentPinchDistance;
                m_lastTwoTouchCenter = currentTouchCenter;
                m_prevTouch0Position = touch0.screenPosition;
                m_prevTouch1Position = touch1.screenPosition;
            }
            else if (touch0.phase == UnityEngine.InputSystem.TouchPhase.Moved || touch1.phase == UnityEngine.InputSystem.TouchPhase.Moved)
            {
                Vector2 moveVector0 = touch0.screenPosition - m_prevTouch0Position;
                Vector2 moveVector1 = touch1.screenPosition - m_prevTouch1Position;

                if (cameraInputEnabled == true && moveVector0.magnitude > 1f && moveVector1.magnitude > 1f)
                {
                    float dotProduct = Vector2.Dot(moveVector0.normalized, moveVector1.normalized);
                    if (dotProduct < -0.5f)
                    {
                        float deltaPinch = currentPinchDistance - m_lastPinchDistance;
                        m_camera.ZoomCamera(-deltaPinch * 0.01f);
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
            if (cameraInputEnabled == true && m_prevTouchCount >= 2)
            {
                m_camera.ResetDragOrigin(pos);
                m_isDragging = true;
            }

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                m_isDragging = false;
                m_lastInputScreenPosition = pos;
                m_inputBlockedByUI = IsPointerOverUIObject(pos);
                // press 시점 스냅샷 — 이 터치 자체가(TargetClick 등으로) 스텝을 AnyClick으로 바꿔도,
                // 같은 터치가 새 스텝의 AnyClick까지 이어서 소비하지 않도록 제스처 시작 시점 값을 고정
                m_wasWaitingForAnyClickAtPress = m_tutorialWaitingForAnyClick;
                if (cameraInputEnabled == true && m_inputBlockedByUI == false)
                {
                    m_camera.OnDragStart(pos);
                    m_tapHitCollider = m_camera.GetCameraRaycast(out RaycastHit downHit, s_pickMask, 3000f, pos)
                        ? downHit.collider : null;
                }
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended)
            {
                // 튜토리얼이 AnyClick(화면 아무 곳이나 클릭) 대기 중이었으면(press 시점 기준) 이 터치는 튜토리얼이 우선 소비 —
                // 3D 탭 처리(HandleModuleSelection/EmptySpaceTapped)와 별도 폴링 루프가 같은 터치를 다투는 경쟁 상태 자체를 없앰.
                // TutorialManager를 직접 참조하지 않고 EventManager로 상태 구독/소비 요청만 주고받음 — cameraInputEnabled와 무관하게 항상 동작.
                // 단, 카메라 드래그(회전) 중이었다면 클릭으로 보면 안 되므로 AnyClick 소비 대상에서 제외
                if (m_wasWaitingForAnyClickAtPress == true && m_isDragging == false)
                {
                    EventManager.Trigger_ConsumeAnyClick();
                    m_isDragging = false;
                    m_inputBlockedByUI = false;
                    m_tapHitCollider = null;
                    m_prevTouchCount = 1;
                    return;
                }

                // press~release 사이에 새 UI(튜토리얼 등)가 열렸을 수 있어 release 시점도 다시 확인 —
                // 단 press 때 이미 UI 위였다면(버튼 누르고 3D로 끌고 나가는 경우) 여전히 차단
                if (cameraInputEnabled == true && m_inputBlockedByUI == false && IsPointerOverUIObject(pos) == false && m_isDragging == false)
                {
                    if (m_tapHitCollider != null)
                    {
                        if (m_camera.GetCameraRaycast(out RaycastHit upHit, s_pickMask, 3000f, pos) && upHit.collider == m_tapHitCollider)
                            m_camera.HandleModuleSelection(pos);
                    }
                    else
                    {
                        m_camera.HandleModuleSelection(pos);
                    }
                }
                m_isDragging = false;
                m_inputBlockedByUI = false;
                m_tapHitCollider = null;
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                m_isDragging = false;
                m_inputBlockedByUI = false;
                m_tapHitCollider = null;
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved || touch.phase == UnityEngine.InputSystem.TouchPhase.Stationary)
            {
                if (cameraInputEnabled == true && m_inputBlockedByUI == false)
                {
                    if (m_isDragging == false && Vector2.Distance(pos, m_lastInputScreenPosition) >= k_dragThreshold)
                        m_isDragging = true;
                    m_camera.OnDragMove(pos);
                }
            }

            m_prevTouchCount = 1;
        }
        else
        {
            m_prevTouchCount = 0;
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
