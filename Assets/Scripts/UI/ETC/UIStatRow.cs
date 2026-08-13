// 함선 스탯 팝업 1행 — 라벨 + 값 텍스트(게이지 없이 텍스트로만 표시)
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIStatRow : MonoBehaviour
{
    [SerializeField] private TMP_Text m_labelText;
    [SerializeField] private TMP_Text m_valueText;
    [SerializeField] private TMP_Text m_diffText; // 값과 별도 컬럼(고정 폭 레이아웃) — 숫자 자릿수와 무관하게 모든 행이 항상 같은 x에서 시작하도록 정렬

    // 값이 숫자인 스탯(공격력/체력 등) — F1 포맷 + 증감 표시
    // diffText: 다른 프리셋과 비교한 증감(리치텍스트 색상 포함, 예: "<color=red>(+1.0)</color>") — 비교 대상 없으면 null
    // buffDiffText: 보상카드 지속버프로 늘어난 만큼(리치텍스트 포함) — 버프 없으면 null. diffText와 동시에 있으면 이어붙임
    public void SetStatRow(string label, float value, string diffText = null, string buffDiffText = null)
    {
        gameObject.SetActive(true);
        if (m_labelText != null) m_labelText.text = label;
        if (m_valueText != null) m_valueText.text = $"{value:F1}";
        SetDiffText(diffText, buffDiffText);

        RebuildSelf();
    }

    // 감소형 스탯(쿨다운/딜레이 등)/게이지 의미 없는 스탯(침묵 시간 등) 공통 — 이미 포맷된 값 텍스트를 그대로 표시
    public void SetValueOnly(string label, string valueText, string diffText = null)
    {
        gameObject.SetActive(true);
        if (m_labelText != null) m_labelText.text = label;
        if (m_valueText != null) m_valueText.text = valueText;
        SetDiffText(diffText, null);

        RebuildSelf();
    }

    private void SetDiffText(string diffText, string buffDiffText)
    {
        if (m_diffText == null) return;

        string combined = diffText;
        if (buffDiffText != null)
            combined = combined == null ? buffDiffText : $"{combined} {buffDiffText}";

        m_diffText.text = combined;
        m_diffText.gameObject.SetActive(combined != null);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // 활성/비활성 전환 등으로 이 행 자신의 내부 레이아웃(자식 배치)이 바뀔 수 있어,
    // 상위 컨테이너 리빌드(UITabFleetComposition에서 별도로 수행)와 별개로 자기 자신도 즉시 갱신
    private void RebuildSelf()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }
}
