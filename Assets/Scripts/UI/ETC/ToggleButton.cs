using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Button + Checkmark + Text 로 구성된 토글 버튼
// Awake 에서 자식 컴포넌트를 자동으로 찾음
public class ToggleButton : MonoBehaviour
{
    private static readonly Color COLOR_SELECTED   = new(0x00 / 255f, 0xFF / 255f, 0x82 / 255f, 1f);
    private static readonly Color COLOR_UNSELECTED = new(0x00 / 255f, 0x96 / 255f, 0x82 / 255f, 1f);

    public Button button;

    private Image    m_outline;
    private Image    m_checkmark;
    private TMP_Text m_text;

    private void Awake()
    {
        if (button == null)
            button = GetComponentInChildren<Button>();
        m_outline = button.GetComponentInChildren<Image>();
        m_text    = button.GetComponentInChildren<TMP_Text>(true);

        Transform checkmarkTr = button.transform.Find("Checkmark");
        if (checkmarkTr != null)
            m_checkmark = checkmarkTr.GetComponent<Image>();
    }

    public void SetSelected(bool selected)
    {
        Color c = selected ? COLOR_SELECTED : COLOR_UNSELECTED;
        if (m_outline != null)   m_outline.color = c;
        if (m_text != null)      m_text.color    = c;
        if (m_checkmark != null) m_checkmark.gameObject.SetActive(selected);
    }
}
