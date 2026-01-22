using TMPro;
using UnityEngine;

/// <summary>
/// 라벨(좌측)과 값(우측)을 표시하는 UI 행 컴포넌트
/// HorizontalLayoutGroup이 있는 오브젝트에 부착
/// </summary>
public class RowLabelValue : MonoBehaviour
{
    [SerializeField] private TMP_Text m_label;
    [SerializeField] private TMP_Text m_value;

    public void SetRow(string label, string value)
    {
        if (m_label != null) m_label.text = label;
        if (m_value != null) m_value.text = value;
    }

    public void SetLabel(string label)
    {
        if (m_label != null) m_label.text = label;
    }

    public void SetValue(string value)
    {
        if (m_value != null) m_value.text = value;
    }
}
