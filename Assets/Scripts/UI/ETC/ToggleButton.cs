using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Button + Checkmark + Text 로 구성된 토글 버튼
// Awake 에서 자식 컴포넌트를 자동으로 찾음
public class ToggleButton : MonoBehaviour
{
    public Button button;

    private Color    m_colorSelected;
    private Color    m_colorUnselected;
    private Image    m_outline;
    private Image    m_checkmark;
    private TMP_Text m_text;
    private TMP_Text m_textDescription;

    private void Awake()
    {
        m_colorSelected   = CommonUtility.PaletteColor("GeneralBright1");
        m_colorUnselected = CommonUtility.PaletteColor("GeneralDark1");

        if (button == null)
            button = GetComponentInChildren<Button>();
        m_outline = button.GetComponentInChildren<Image>();

        var texts = button.GetComponentsInChildren<TMP_Text>(true);
        m_text            = texts.Length >= 1 ? texts[0] : null;
        m_textDescription = texts.Length >= 2 ? texts[1] : null;

        Transform checkmarkTr = button.transform.Find("CheckmarkBorder/Checkmark");
        if (checkmarkTr != null)
            m_checkmark = checkmarkTr.GetComponent<Image>();
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
        if (m_outline != null)         m_outline.color         = c;
        if (m_text != null)            m_text.color            = c;
        if (m_textDescription != null) m_textDescription.color = c;
        if (m_checkmark != null)       m_checkmark.gameObject.SetActive(selected);
    }
}
