// 아이콘 이미지 + 텍스트 1행 UI 컴포넌트 (HorizontalLayoutGroup 자식으로 사용)
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RowImageText : MonoBehaviour
{
    [SerializeField] private Image m_image;
    [SerializeField] private TMP_Text m_text;

    private void Awake()
    {
        if (m_image == null)
            m_image = GetComponent<RectTransform>().GetChild(0).GetComponent<Image>();
        if (m_text == null)
            m_text = GetComponent<RectTransform>().GetChild(1).GetComponent<TMP_Text>();
    }

    public void SetRow(string spriteName, string text)
    {
        gameObject.SetActive(true);
        SetImage(spriteName);
        SetText(text);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetImage(string spriteName)
    {
        if (m_image == null) return;
        Sprite sprite = UISpriteCache.Get(spriteName);
        if (sprite != null)
            m_image.sprite = sprite;
    }

    public void SetText(string text)
    {
        if( m_text != null)
            CommonUtility.SetUILocText(m_text, text);
    }

    public void SetTextColor(Color color)
    {
        if (m_text != null)
            m_text.color = color;
    }
}
