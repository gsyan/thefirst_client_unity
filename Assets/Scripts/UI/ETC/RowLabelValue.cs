using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization;

public class RowLabelValue : MonoBehaviour
{
    [SerializeField] private TMP_Text m_label;
    [SerializeField] private TMP_Text m_value;

    public void SetRow(string label, string value)
    {
        CommonUtility.SetUILocText(m_label, label);
        SetValue(value);
    }

    public void SetLabel(string label)
    {
        CommonUtility.SetUILocText(m_label, label);
    }

    public void SetValue(string value)
    {
        CommonUtility.SetUILocText(m_value, value);
    }
}
