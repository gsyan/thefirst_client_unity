// 레이블(로컬라이제이션 키) + 값(로컬라이제이션 키 또는 raw 텍스트) 1행 UI 컴포넌트
using TMPro;
using UnityEngine;

public class RowLabelValue : MonoBehaviour
{
    [SerializeField] private TMP_Text m_label;
    [SerializeField] private TMP_Text m_value1;

    private void Awake()
    {
        if (m_label == null)
            m_label = GetComponent<RectTransform>().GetChild(0).GetComponent<TMP_Text>();
        if (m_value1 == null)
            m_value1 = GetComponent<RectTransform>().GetChild(1).GetComponent<TMP_Text>();
    }

    // rawValue=true 이면 value를, rawLabel=true 이면 label을 로컬라이제이션 없이 직접 표시 (숫자, 아직 로컬라이즈 안 된 코드성 문자열 등)
    public void SetRow(string label, string value1, bool rawValue = false, bool rawLabel = false)
    {
        gameObject.SetActive(true);
        SetLabel(label, rawLabel);
        SetValues(value1, rawValue);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetLabel(string label, bool rawLabel = false)
    {
        if (m_label == null) return;
        if (rawLabel) m_label.text = label;
        else CommonUtility.SetUILocText(m_label, label);
    }

    public void SetValues(string value1, bool rawValue = false)
    {
        if (m_value1 != null)
        {
            if (rawValue) m_value1.text = value1;
            else CommonUtility.SetUILocText(m_value1, value1);
        }
    }

    public void SetValueColor(Color color)
    {
        if (m_value1 != null) m_value1.color = color;
    }
}
