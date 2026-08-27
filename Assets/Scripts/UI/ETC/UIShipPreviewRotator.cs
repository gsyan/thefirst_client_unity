// 함선 프리뷰 RawImage 위에서 드래그로 X/Y축 회전 — 자동 회전 없음, 드래그 중에만 돌고 손 떼면 그 자리에 정지
// 매 프레임 절대 각도(yaw/pitch)로 회전을 재구성해서 적용 — Transform.Rotate를 누적 호출하면 축이 서서히 틀어지며 Z축 롤이 생기는 문제 방지
using UnityEngine;
using UnityEngine.EventSystems;

public class UIShipPreviewRotator : MonoBehaviour, IDragHandler
{
    [SerializeField] private float m_rotationSpeed = 0.3f; // 드래그 픽셀당 회전각(도)
    [SerializeField] private float m_maxPitch = 30f; // X축(위아래) 회전 제한 각도

    private float m_yaw;
    private float m_pitch;

    public void OnDrag(PointerEventData eventData)
    {
        Transform previewRoot = ShipPreviewManager.Instance.GetPreviewRoot();
        if (previewRoot == null) return;

        m_yaw -= eventData.delta.x * m_rotationSpeed;
        m_pitch = Mathf.Clamp(m_pitch - eventData.delta.y * m_rotationSpeed, -m_maxPitch, m_maxPitch);

        previewRoot.localRotation = Quaternion.Euler(m_pitch, m_yaw, 0f);
    }
}
