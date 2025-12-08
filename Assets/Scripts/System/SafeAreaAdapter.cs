//------------------------------------------------------------------------------
// 핸드폰에 있는 노치 또는 아일랜드 캠 같은 것을 고려
//------------------------------------------------------------------------------
using UnityEngine;

public class SafeAreaAdapter : MonoBehaviour
{
    private RectTransform m_rectTransform;
    private Rect m_lastSafeArea;

    private void Awake()
    {
        m_rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void Update()
    {
        if (m_lastSafeArea != Screen.safeArea)
            ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        m_lastSafeArea = safeArea;

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        m_rectTransform.anchorMin = anchorMin;
        m_rectTransform.anchorMax = anchorMax;
    }
}
