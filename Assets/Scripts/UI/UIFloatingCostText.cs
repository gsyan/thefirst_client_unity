using System.Collections;
using TMPro;
using UnityEngine;

// 미네랄 소비 플로팅 텍스트 — 위로 이동하며 페이드아웃
public class UIFloatingCostText : MonoBehaviour
{
    private TextMeshProUGUI m_text;
    private RectTransform   m_rect;
    private Coroutine       m_animCoroutine;
    private System.Action<UIFloatingCostText> m_onFinished;

    private const float k_duration     = 1.5f;
    private const float k_moveDistance = 60f;

    private Color m_baseColor;

    private void Awake()
    {
        m_rect = GetComponent<RectTransform>();
        m_text = GetComponent<TextMeshProUGUI>();
        if (m_text == null)
            m_text = gameObject.AddComponent<TextMeshProUGUI>();
        m_text.fontSize = 18f;
        m_text.alignment = TextAlignmentOptions.Center;
        m_text.fontStyle = FontStyles.Bold;
        m_text.raycastTarget = false;
    }

    public void Play(string content, Vector2 anchoredPos, System.Action<UIFloatingCostText> onFinished)
    {
        m_baseColor = CommonUtility.PaletteColor("Mineral");

        m_onFinished = onFinished;
        m_rect.anchoredPosition = anchoredPos;
        m_text.text = content;
        m_text.color = m_baseColor;

        gameObject.SetActive(true);

        if (m_animCoroutine != null)
            StopCoroutine(m_animCoroutine);
        m_animCoroutine = StartCoroutine(AnimateCoroutine(anchoredPos));
    }

    private IEnumerator AnimateCoroutine(Vector2 startPos)
    {
        float elapsed = 0f;
        while (elapsed < k_duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / k_duration;

            m_rect.anchoredPosition = startPos + new Vector2(0f, k_moveDistance * t);

            Color c = m_baseColor;
            c.a = 1f - t;
            m_text.color = c;

            yield return null;
        }

        m_animCoroutine = null;
        gameObject.SetActive(false);
        m_onFinished?.Invoke(this);
    }
}
