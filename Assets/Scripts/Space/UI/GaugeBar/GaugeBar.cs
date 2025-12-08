using UnityEngine;
using UnityEngine.UI;

public class GaugeBar : MonoBehaviour
{
    [SerializeField] private Image m_backgroundImage;
    [SerializeField] private Image m_fillImage;    
    [SerializeField] private Text m_valueText;

    private Color m_color = Color.green;
    private float m_currentValue;
    private float m_maxValue;
    private float m_targetValue;
    private float m_smoothSpeed = 5f;

    public void SetColor(Color color)
    {
        m_color = color;
        if (m_fillImage != null)
            m_fillImage.color = color;
    }

    public void SetSmoothSpeed(float speed)
    {
        m_smoothSpeed = speed;
    }

    public void UpdateValue(float currentValue, float maxValue)
    {
        m_targetValue = Mathf.Clamp(currentValue, 0, maxValue);
        m_maxValue = maxValue;
    }

    public void InitializeFromPrefab()
    {
        if (m_backgroundImage == null)
            m_backgroundImage = transform.Find("Background")?.GetComponent<Image>();
        if (m_fillImage == null)
            m_fillImage = transform.Find("Fill")?.GetComponent<Image>();        
        if (m_valueText == null)
            m_valueText = transform.Find("ValueText")?.GetComponent<Text>();

        if (m_fillImage != null)
            m_color = m_fillImage.color;

        m_currentValue = 100f;
        m_maxValue = 100f;
        m_targetValue = 100f;
    }

    void Update()
    {
        UpdateSmooth();
    }

    private void UpdateSmooth()
    {
        if (m_fillImage == null) return;

        m_currentValue = Mathf.Lerp(m_currentValue, m_targetValue, Time.deltaTime * m_smoothSpeed);

        float fillAmount = m_maxValue > 0 ? m_currentValue / m_maxValue : 0;
        RectTransform fillRect = m_fillImage.rectTransform;
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(fillAmount), 1);

        if (m_valueText != null)
            m_valueText.text = $"{m_currentValue:F0} / {m_maxValue:F0}";
    }

    public float GetCurrentValue() => m_currentValue;
    public float GetMaxValue() => m_maxValue;
    public Color GetColor() => m_color;
}
