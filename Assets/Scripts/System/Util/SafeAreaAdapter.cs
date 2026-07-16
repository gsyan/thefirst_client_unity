//------------------------------------------------------------------------------
// 핸드폰에 있는 노치 또는 아일랜드 캠 같은 것을 고려
// 곡면(엣지) 디스플레이는 Screen.safeArea로 감지되지 않는 기종이 있어
// 고정 % 마진을 추가로 깎아 사각형 영역만 사용하도록 함
//------------------------------------------------------------------------------
using UnityEngine;

public class SafeAreaAdapter : MonoBehaviour
{
    [Header("곡면 화면 대비 추가 마진 (0~0.5, 화면 크기 대비 비율)")]
    [SerializeField] private float m_extraMarginLeft;
    [SerializeField] private float m_extraMarginRight;
    [SerializeField] private float m_extraMarginTop;
    [SerializeField] private float m_extraMarginBottom;

    private RectTransform m_rectTransform;
    private Rect m_lastSafeArea;
    private int m_lastScreenWidth;
    private int m_lastScreenHeight;

    private void Awake()
    {
        m_rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void Update()
    {
        if (m_lastSafeArea != Screen.safeArea || m_lastScreenWidth != Screen.width || m_lastScreenHeight != Screen.height)
            ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        m_lastSafeArea = safeArea;
        m_lastScreenWidth = Screen.width;
        m_lastScreenHeight = Screen.height;

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        anchorMin.x = Mathf.Max(anchorMin.x, m_extraMarginLeft);
        anchorMin.y = Mathf.Max(anchorMin.y, m_extraMarginBottom);
        anchorMax.x = Mathf.Min(anchorMax.x, 1f - m_extraMarginRight);
        anchorMax.y = Mathf.Min(anchorMax.y, 1f - m_extraMarginTop);

        m_rectTransform.anchorMin = anchorMin;
        m_rectTransform.anchorMax = anchorMax;
    }
}
