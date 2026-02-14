using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization;

public class RowLabelValue2 : MonoBehaviour
{
    [SerializeField] private TMP_Text m_label;
    [SerializeField] private TMP_Text m_value1;
    [SerializeField] private TMP_Text m_value2;

    public void SetRow(string label, string value1, string value2)
    {
        CommonUtility.SetUILocText(m_label, label);
        SetValue1(value1);
        SetValue2(value2);
    }

    public void SetLabel(string label)
    {
        CommonUtility.SetUILocText(m_label, label);
    }

    public void SetValue1(string value)
    {
        CommonUtility.SetUILocText(m_value1, value);
    }

    public void SetValue2(string value)
    {
        CommonUtility.SetUILocText(m_value2, value);
    }
}
