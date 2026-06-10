// 아이콘 이미지 + 텍스트 1행 UI 컴포넌트 (HorizontalLayoutGroup 자식으로 사용)
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RowImageText : MonoBehaviour
{
    [SerializeField] private Image    m_image;
    [SerializeField] private TMP_Text m_text;
    [SerializeField] private Image    m_image2;
    [SerializeField] private string   m_imageColorKey  = "GeneralBright1";
    [SerializeField] private string   m_textColorKey   = "GeneralBright1";
    [SerializeField] private string   m_image2ColorKey = "GeneralBright1";

    private void Awake()
    {
        if (m_image == null)
            m_image = GetComponent<RectTransform>().GetChild(0).GetComponent<Image>();
        if (m_text == null)
            m_text = GetComponent<RectTransform>().GetChild(1).GetComponent<TMP_Text>();
        if (m_image2 != null)
            m_image2.gameObject.SetActive(false);
    }

    public void SetRow(string spriteName, string text)
    {
        SetImage(spriteName);
        SetImageColor(CommonUtility.PaletteColor(m_imageColorKey));
        SetTextColor(CommonUtility.PaletteColor(m_textColorKey));
        SetTextWithString(text);
        if (m_image2 != null)
            m_image2.gameObject.SetActive(false);
    }

    // 아이템 이미지 - 텍스트 - tier 이미지 3단 구성
    public void SetRow(string spriteName, string text, string image2SpriteName)
    {
        SetRow(spriteName, text);
        SetImage2(image2SpriteName);
    }

    public void SetImage2(string spriteName)
    {
        if (m_image2 == null) return;
        Sprite sprite = UISpriteCache.Get(spriteName);
        if (sprite != null)
            m_image2.sprite = sprite;
        m_image2.color = CommonUtility.PaletteColor(m_image2ColorKey);
        m_image2.gameObject.SetActive(true);
    }

    public void SetImage2Color(Color color)
    {
        if (m_image2 == null) return;
        m_image2.color = color;
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

    public void SetImageColor(Color color)
    {
        if (m_image == null) return;
        m_image.color = color;
    }

    public void SetTextColor(Color color)
    {
        if (m_text == null) return;
        m_text.color = color;
    }

    public void SetTextWithString(string text)
    {
        gameObject.SetActive(true);
        m_text.text = text;
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    public void SetTextWithInt(int value)
    {
        gameObject.SetActive(true);
        m_text.SetText("{0}", (float)value);
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }
}
