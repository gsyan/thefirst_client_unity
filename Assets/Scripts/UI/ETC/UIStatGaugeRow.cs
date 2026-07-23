// 함선 스탯 팝업 1행 — 라벨 + 게이지(fillAmount) + 값 텍스트. 감소형 스탯(쿨다운/딜레이 등)은 게이지 없이 값만 표시
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStatGaugeRow : MonoBehaviour
{
    [SerializeField] private TMP_Text m_labelText;
    [SerializeField] private Image m_gaugeFillImage; // Image Type: Filled, Fill Method: Horizontal 권장
    [SerializeField] private TMP_Text m_valueText;

    // gaugeMax 기준 채움 비율 표시 — 라벨은 로컬라이즈 미정이라 raw 텍스트 그대로 사용
    public void SetGauge(string label, float value, float gaugeMax)
    {
        gameObject.SetActive(true);
        if (m_labelText != null) m_labelText.text = label;
        if (m_valueText != null) m_valueText.text = $"{value:F1}";
        if (m_gaugeFillImage != null)
        {
            m_gaugeFillImage.gameObject.SetActive(true);
            m_gaugeFillImage.fillAmount = gaugeMax > 0f ? Mathf.Clamp01(value / gaugeMax) : 0f;
        }

        RebuildSelf();
    }

    // 게이지로 표현하기 애매한 감소형 스탯(쿨다운/딜레이 등) — 값만 텍스트로 표시
    public void SetValueOnly(string label, string valueText)
    {
        gameObject.SetActive(true);
        if (m_labelText != null) m_labelText.text = label;
        if (m_valueText != null) m_valueText.text = valueText;
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
