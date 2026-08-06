// 레이블(로컬라이제이션 키) + 값(로컬라이제이션 키 또는 raw 텍스트) 1행 UI 컴포넌트
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RowLabelValue : MonoBehaviour
{
    [SerializeField] private TMP_Text m_label;
    [SerializeField] private TMP_Text m_value1;
    [SerializeField] private LayoutElement m_layoutElement;

    private void Awake()
    {
        if (m_label == null)
            m_label = GetComponent<RectTransform>().GetChild(0).GetComponent<TMP_Text>();
        if (m_value1 == null)
            m_value1 = GetComponent<RectTransform>().GetChild(1).GetComponent<TMP_Text>();
        if (m_layoutElement == null)
            m_layoutElement = GetComponent<LayoutElement>();
    }

    // 호출부가 필요할 때만 명시적으로 부르는 폭 조절 — width<0이거나 LayoutElement가 없으면 무시
    public void SetPreferredWidth(float width)
    {
        if (m_layoutElement == null || width < 0f) return;
        m_layoutElement.preferredWidth = width;
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

    // 라벨/값 텍스트 색을 한 번에 동일하게 적용 — 선택 상태 등 행 전체 색 토글용
    public void SetTextColor(Color color)
    {
        if (m_label != null) m_label.color = color;
        if (m_value1 != null) m_value1.color = color;
    }

    // 값이 즉시 바뀌지 않고 from -> to로 카운팅되는 롤링 연출(재화 텍스트와 동일한 느낌) — 라벨은 건드리지 않음
    // GameObject가 비활성 상태면 코루틴을 시작할 수 없으므로(Unity가 에러 로그를 남김) 아예 시도하지 않음 —
    // 값 자체는 호출부가 이미 갱신했을 것이고, 화면 반영은 패널이 다시 활성화될 때 호출부가 재호출해서 따라잡음
    private Coroutine m_valueAnimCoroutine;
    public void SetValueAnimated(long from, long to)
    {
        if (m_value1 == null) return;
        if (gameObject.activeInHierarchy == false) return;

        if (m_valueAnimCoroutine != null) StopCoroutine(m_valueAnimCoroutine);
        m_valueAnimCoroutine = StartCoroutine(CommonUtility.AnimateCounterText(m_value1, from, to));
    }
}
