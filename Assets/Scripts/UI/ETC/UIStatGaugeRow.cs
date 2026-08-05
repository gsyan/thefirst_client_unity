// 함선 스탯 팝업 1행 — 라벨 + 게이지(fillAmount) + 값 텍스트. 감소형 스탯(쿨다운/딜레이 등)은 강화율 기준 반전 게이지로 표시
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStatGaugeRow : MonoBehaviour
{
    [SerializeField] private TMP_Text m_labelText;
    [SerializeField] private Image m_gaugeFillImage; // Image Type: Filled, Fill Method: Horizontal 권장
    [SerializeField] private TMP_Text m_valueText;

    // gaugeMax 기준 채움 비율 표시 — 라벨은 로컬라이즈 미정이라 raw 텍스트 그대로 사용
    // diffText: 다른 프리셋과 비교한 증감(리치텍스트 색상 포함, 예: "<color=red>(+1.0)</color>") — 비교 대상 없으면 null
    public void SetGauge(string label, float value, float gaugeMax, string diffText = null)
    {
        gameObject.SetActive(true);
        if (m_labelText != null) m_labelText.text = label;
        if (m_valueText != null) m_valueText.text = diffText == null ? $"{value:F1}" : $"{value:F1} {diffText}";
        if (m_gaugeFillImage != null)
        {
            m_gaugeFillImage.gameObject.SetActive(true);
            m_gaugeFillImage.fillAmount = gaugeMax > 0f ? Mathf.Clamp01(value / gaugeMax) : 0f;
        }

        RebuildSelf();
    }

    // 감소형 스탯(쿨다운/딜레이 등) 반전 게이지 — fillAmount는 강화율(0=미강화, 1=하한 도달)로 이미 계산되어 전달됨
    public void SetReverseGauge(string label, string valueText, float fillAmount, string diffText = null)
    {
        gameObject.SetActive(true);
        if (m_labelText != null) m_labelText.text = label;
        if (m_valueText != null) m_valueText.text = diffText == null ? valueText : $"{valueText} {diffText}";
        if (m_gaugeFillImage != null)
        {
            m_gaugeFillImage.gameObject.SetActive(true);
            m_gaugeFillImage.fillAmount = Mathf.Clamp01(fillAmount);
        }

        RebuildSelf();
    }

    // 게이지 자체가 의미 없는 스탯(침묵 시간 등) — 값만 텍스트로 표시
    public void SetValueOnly(string label, string valueText, string diffText = null)
    {
        gameObject.SetActive(true);
        if (m_labelText != null) m_labelText.text = label;
        if (m_valueText != null) m_valueText.text = diffText == null ? valueText : $"{valueText} {diffText}";
        if (m_gaugeFillImage != null) m_gaugeFillImage.gameObject.SetActive(false);

        RebuildSelf();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // 게이지 이미지 활성/비활성 전환 등으로 이 행 자신의 내부 레이아웃(자식 배치)이 바뀔 수 있어,
    // 상위 컨테이너 리빌드(UITabFleetComposition에서 별도로 수행)와 별개로 자기 자신도 즉시 갱신
    private void RebuildSelf()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }
}
