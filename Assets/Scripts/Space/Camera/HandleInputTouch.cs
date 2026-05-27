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
    public Vector3 m_lastInputScreenPosition;

    private float m_lastPinchDistance = 0f;
    private Vector2 m_lastTwoTouchCenter = Vector2.zero;
    private Vector2 m_prevTouch0Position;
    private Vector2 m_prevTouch1Position;
    private int m_prevTouchCount = 0;

    public HandleInputTouch(CameraController camera)
    {
        m_camera = camera;
    }

    public void Process()
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

                if (moveVector0.magnitude > 1f && moveVector1.magnitude > 1f)
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
            if (m_prevTouchCount >= 2)
            {
                m_camera.ResetDragOrigin(pos);
                m_isDragging = true;
            }

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                m_isDragging = false;
                m_lastInputScreenPosition = pos;
                m_inputBlockedByUI = IsPointerOverUIObject(pos);
                if (m_inputBlockedByUI == false)
                {
                    m_camera.OnDragStart(pos);
                    m_tapHitCollider = m_camera.GetCameraRaycast(out RaycastHit downHit, s_pickMask, 3000f, pos)
                        ? downHit.collider : null;
                }
            }
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended)
            {
                if (m_inputBlockedByUI == false && m_isDragging == false)
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
                if (m_inputBlockedByUI == false)
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
