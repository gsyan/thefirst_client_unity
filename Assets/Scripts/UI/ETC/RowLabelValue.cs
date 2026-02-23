using TMPro;
using UnityEngine;

public class RowLabelValue : MonoBehaviour
{
    [SerializeField] private TMP_Text m_label;
    [SerializeField] private TMP_Text m_value1;
    [SerializeField] private TMP_Text m_value2;

    private void Awake()
    {
        if (m_label == null)
            m_label = GetComponent<RectTransform>().GetChild(0).GetComponent<TMP_Text>();
        if (m_value1 == null)
            m_value1 = GetComponent<RectTransform>().GetChild(1).GetComponent<TMP_Text>();
    }

    public void SetRow(string label, string value1, string value2 = "")
    {
        SetLabel(label);
        SetValues(value1, value2);
    }

    public void SetLabel(string label)
    {
        if( m_label != null)
            CommonUtility.SetUILocText(m_label, label);
    }

    public void SetValues(string value1, string value2 = "")
    {
        if( m_value1 != null)
            CommonUtility.SetUILocText(m_value1, value1);
        if( m_value2 != null)
            CommonUtility.SetUILocText(m_value2, value2);
    }

    public void SetValueColor(Color color)
    {
        if (m_value1 != null) m_value1.color = color;
    }
}
