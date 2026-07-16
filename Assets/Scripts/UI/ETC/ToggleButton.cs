using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Button + Checkmark + Text 로 구성된 토글 버튼
// Awake 에서 자식 컴포넌트를 자동으로 찾음
public class ToggleButton : MonoBehaviour
{
    public Button button;

    private Color     m_colorSelected;
    private Color     m_colorUnselected;
    private Color     m_colorLocked;
    private TMP_Text  m_text;
    private TMP_Text  m_textDescription;
    private Image     m_checkmark;
    private Graphic[] m_graphics; // 색상 일괄 적용 대상 (checkmark 제외)

    private void Awake()
    {
        m_colorSelected   = CommonUtility.PaletteColor("Rose500");
        m_colorUnselected = CommonUtility.PaletteColor("RoseDark");
        m_colorLocked     = CommonUtility.PaletteColor("State.Disabled");

        if (button == null)
            button = GetComponentInChildren<Button>();

        Transform checkmarkTr = button.transform.Find("CheckmarkBorder/Checkmark");
        if (checkmarkTr != null)
            m_checkmark = checkmarkTr.GetComponent<Image>();

        var texts = button.GetComponentsInChildren<TMP_Text>(true);
        m_text            = texts.Length >= 1 ? texts[0] : null;
        m_textDescription = texts.Length >= 2 ? texts[1] : null;

        // 텍스트는 선택/잠김 상태와 무관하게 항상 Text.Dark1 고정 (색상 전환 대상에서 제외)
        Color textColor = CommonUtility.PaletteColor("Text.Dark1");
        if (m_text != null)            m_text.color            = textColor;
        if (m_textDescription != null) m_textDescription.color = textColor;

        var all = button.GetComponentsInChildren<Graphic>(true);
        var list = new System.Collections.Generic.List<Graphic>(all.Length);
        foreach (var g in all)
            if (g != m_checkmark && g != (Graphic)m_text && g != (Graphic)m_textDescription) list.Add(g);
        m_graphics = list.ToArray();
    }

    public void SetTexts(string nameKey, string descKey)
    {
        if (m_text != null)            CommonUtility.SetUILocText(m_text,            nameKey);
        if (m_textDescription != null) CommonUtility.SetUILocText(m_textDescription, descKey);
    }

    // descKey 에 {0} 포맷 플레이스홀더가 있을 때 값 주입
    public void SetTexts(string nameKey, string descKey, object descArg)
    {
        if (m_text != null) CommonUtility.SetUILocText(m_text, nameKey);
        if (m_textDescription != null)
            m_textDescription.text = string.Format(LocalizationManager.Instance.Get(descKey), descArg);
    }

    public void SetSelected(bool selected)
    {
        Color c = selected ? m_colorSelected : m_colorUnselected;
        foreach (var g in m_graphics) g.color = c;
        if (m_checkmark != null) m_checkmark.gameObject.SetActive(selected);
    }

    // 잠김 상태 표시 — 색은 Locked 팔레트로 고정, 체크마크는 잠김 상태에서의 기본값 표시용으로 별도 제어
    public void SetLockedVisual(bool checkmarkOn)
    {
        foreach (var g in m_graphics) g.color = m_colorLocked;
        if (m_checkmark != null) m_checkmark.gameObject.SetActive(checkmarkOn);
    }
}
