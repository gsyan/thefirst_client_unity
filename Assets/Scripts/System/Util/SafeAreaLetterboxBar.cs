//------------------------------------------------------------------------------
// 좌/우 화면 끝(카메라 컷아웃 등)을 검게 가리는 바 — 고정 마진과 실기기 Screen.safeArea가
// 실제로 침범하는 폭 중 더 넓은 쪽으로 항상 가려서, 마진이 침범폭보다 좁아 게임 화면이
// 그대로 노출되는 일이 없게 함
//------------------------------------------------------------------------------
using UnityEngine;

public class SafeAreaLetterboxBar : MonoBehaviour
{
    public enum ESide { Left, Right }

    [SerializeField] private ESide m_side;
    [SerializeField] private float m_fixedMargin = 0.02f;

    private RectTransform m_rectTransform;
    private Rect m_lastSafeArea;
    private int m_lastScreenWidth;
    private int m_lastScreenHeight;

    private void Awake()
    {
        m_rectTransform = GetComponent<RectTransform>();
        ApplyMargin();
    }

    private void Update()
    {
        if (m_lastSafeArea != Screen.safeArea || m_lastScreenWidth != Screen.width || m_lastScreenHeight != Screen.height)
            ApplyMargin();
    }

    private void ApplyMargin()
    {
        Rect safeArea = Screen.safeArea;
        m_lastSafeArea = safeArea;
        m_lastScreenWidth = Screen.width;
        m_lastScreenHeight = Screen.height;

        if (m_side == ESide.Left)
        {
            float unsafeFraction = safeArea.x / Screen.width;
            float margin = Mathf.Max(m_fixedMargin, unsafeFraction);
            m_rectTransform.anchorMin = new Vector2(0f, 0f);
            m_rectTransform.anchorMax = new Vector2(margin, 1f);
        }
        else
        {
            float unsafeFraction = (Screen.width - (safeArea.x + safeArea.width)) / Screen.width;
            float margin = Mathf.Max(m_fixedMargin, unsafeFraction);
            m_rectTransform.anchorMin = new Vector2(1f - margin, 0f);
            m_rectTransform.anchorMax = new Vector2(1f, 1f);
        }
    }
}
