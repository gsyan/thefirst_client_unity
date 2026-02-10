using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization;

public class RowLabelValue : MonoBehaviour
{
    [SerializeField] private TMP_Text m_label;
    [SerializeField] private TMP_Text m_value;

    public void SetRow(string text, string value)
    {
        CommonUtility.SetUILabelText(m_label, text);
        SetValue(value);
    }

    public void SetValue(string value)
    {
        if (m_value != null) m_value.text = value;
    }
}
