using NUnit.Framework.Constraints;
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
        SetLabel(label);
        SetValue(value);
    }

    public void SetLabel(string label)
    {
        // Label (Localized)
        var labelLocalize = m_label.GetComponent<LocalizeStringEvent>();
        if (labelLocalize != null)
        {
            const string TABLE = "UI";
            labelLocalize.StringReference = new LocalizedString(TABLE, label);
            labelLocalize.RefreshString();
        }
        else
        {
            // LocalizeStringEvent가 없으면 그냥 raw 텍스트로라도 표시
            m_label.text = label;
        }
    }

    public void SetValue(string value)
    {
        if (m_value != null) m_value.text = value;
    }
}
