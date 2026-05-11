using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UISelectableButton : MonoBehaviour
{
    [SerializeField] private Button m_button;
    [SerializeField] private Image m_image;
    [SerializeField] private TMP_Text m_label;

    [SerializeField] private Color m_colorActive   = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color m_colorInactive = Color.white;
    [SerializeField] private float m_imageAlpha    = 0.02f;

    public void Setup(string labelText, UnityAction onClick)
    {
        if (m_label != null) m_label.text = labelText;
        m_button.onClick.RemoveAllListeners();
        m_button.onClick.AddListener(onClick);
    }

    public void SetSelected(bool selected)
    {
        Color c = selected ? m_colorActive : m_colorInactive;
        var colors = m_button.colors;
        colors.normalColor   = c;
        colors.selectedColor = c;
        m_button.colors = colors;
        if (m_image != null)
        {
            Color imgColor = c;
            imgColor.a = m_imageAlpha;
            m_image.color = imgColor;
        }
        if (m_label != null) m_label.color = c;
    }
}
