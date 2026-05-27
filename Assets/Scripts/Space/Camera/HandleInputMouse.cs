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
    public Vector3 m_lastInputScreenPosition;

    public HandleInputMouse(CameraController camera)
    {
        m_camera = camera;
    }

    public void Process()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector3 mousePos = mouse.position.ReadValue();

        if (mouse.rightButton.wasPressedThisFrame == true)
        {
            m_camera.OnDragStart(mousePos);
        }
        else if (mouse.rightButton.isPressed == true)
        {
            m_camera.OnDragMove(mousePos);
        }

        if (mouse.leftButton.wasPressedThisFrame == true)
        {
            m_lastInputScreenPosition = mousePos;
            m_inputBlockedByUI = IsPointerOverUIObject(mousePos);
            if (m_inputBlockedByUI == false)
            {
                m_tapHitCollider = m_camera.GetCameraRaycast(out RaycastHit downHit, s_pickMask, 3000f, mousePos)
                    ? downHit.collider : null;
            }
        }
        else if (mouse.leftButton.wasReleasedThisFrame == true)
        {
            if (m_inputBlockedByUI == false)
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
            m_inputBlockedByUI = false;
            m_tapHitCollider = null;
        }

        float scrollDelta = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scrollDelta) > 0.01f)
            m_camera.ZoomCamera(-scrollDelta * 0.5f);
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
