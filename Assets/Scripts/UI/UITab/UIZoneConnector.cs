// 존 스테이지 노드 간 연결선 — Screen Space 두 지점 사이를 잇는 얇은 Image
using UnityEngine;
using UnityEngine.UI;

public class UIZoneConnector : MonoBehaviour
{
    [SerializeField] private RectTransform m_rectTransform;
    [SerializeField] private Image         m_lineImage;
    [SerializeField] private RectTransform m_capStart;
    [SerializeField] private RectTransform m_capEnd;
    [SerializeField] private Image         m_capStartImage;
    [SerializeField] private Image         m_capEndImage;
    [SerializeField] private float         m_thickness = 3f;
    [SerializeField] private float         m_capSize   = 10f;

    private void Awake()
    {
        Sprite capSprite = UISpriteCache.Get("plain-circle");
        if (capSprite != null)
        {
            if (m_capStartImage != null) m_capStartImage.sprite = capSprite;
            if (m_capEndImage != null)   m_capEndImage.sprite   = capSprite;
        }
    }

    // 두 노드의 screen-space position(RectTransform.position 기준) 사이를 잇는다
    public void SetPoints(Vector3 screenPosA, Vector3 screenPosB, bool isCleared)
    {
        if (m_rectTransform == null) return;

        Vector3 diff     = screenPosB - screenPosA;
        float   distance = diff.magnitude;
        float   angle    = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

        float lossyScaleX   = m_rectTransform.lossyScale.x;
        float localDistance = lossyScaleX != 0f ? distance / lossyScaleX : distance;

        m_rectTransform.position        = screenPosA;
        m_rectTransform.sizeDelta       = new Vector2(localDistance, m_thickness);
        m_rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);

        Color color = isCleared == true ? CommonUtility.PaletteColor("Unlocked") : CommonUtility.PaletteColor("Zone.Locked");
        if (m_lineImage != null)
            m_lineImage.color = color;

        if (m_capStart != null)
        {
            m_capStart.position  = screenPosA;
            m_capStart.sizeDelta = new Vector2(m_capSize, m_capSize);
        }
        if (m_capEnd != null)
        {
            m_capEnd.position  = screenPosB;
            m_capEnd.sizeDelta = new Vector2(m_capSize, m_capSize);
        }
        if (m_capStartImage != null) m_capStartImage.color = color;
        if (m_capEndImage != null)   m_capEndImage.color   = color;
    }
}
