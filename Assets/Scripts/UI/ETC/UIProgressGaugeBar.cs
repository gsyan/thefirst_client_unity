// 함체 선택 팝업(UIHullPickerView) 상단 — 현재 선택된 함체의 다음 패시브 단계 진척도 게이지바 + 텍스트
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIProgressGaugeBar : MonoBehaviour
{
    [SerializeField] private Image m_fillImage; // Image Type=Filled
    [SerializeField] private TMP_Text m_valueText;
    [SerializeField] private TMP_Text m_descriptionText; // 같은 슬롯(Progress1/2) 아래 형제 오브젝트 — 어떤 패시브 효과를 향한 진척도인지 표시

    // 자기 자신(바)이 아니라 슬롯 전체(부모, Progress1/2)를 켜고 꺼야 설명 텍스트까지 함께 토글됨
    public void SetGauge(float ratio, string valueText, string description)
    {
        transform.parent.gameObject.SetActive(true);
        if (m_fillImage != null) m_fillImage.fillAmount = Mathf.Clamp01(ratio);
        if (m_valueText != null) m_valueText.text = valueText;
        if (m_descriptionText != null) m_descriptionText.text = description;
    }

    public void Hide()
    {
        transform.parent.gameObject.SetActive(false);
    }
}
