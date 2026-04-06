using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 연구 노드 기본 상태 (선택 여부와 별개 - 선택은 isSelected 파라미터로 전달)
public enum EResearchNodeState
{
    Researchable,   // 배울 수 있음 (선행 조건 충족)
    Researched,     // 이미 배움
    Current,        // 현재 장착 중 (모듈 업그레이드 팝업 전용)
    Locked          // 기술레벨 부족 — 선택 불가
}

public class ScrollViewResearchItem : MonoBehaviour
{
    [SerializeField] private Button m_selectButton;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private Image m_backgroundImage;

    [Header("상태별 색상")]
    [SerializeField] private Color m_colorResearchable = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color m_colorResearched = new Color(0.2f, 0.8f, 0.4f, 1f);
    [SerializeField] private Color m_colorCurrent = new Color(0.2f, 0.6f, 1f, 1f);    // 현재 장착 중 - 파란색
    [SerializeField] private Color m_colorLocked = new Color(0.15f, 0.15f, 0.15f, 1.0f); // 기술레벨 부족 - 어둡게
    [SerializeField] private Color m_colorSelected = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private float m_outlineWidth = 4f;

    private UnityEngine.UI.Outline m_outline;

    // displayName: 이미 로컬라이즈된 표시명 (동적 생성 지원)
    public void InitializeScrollViewResearchItem(string displayName, UnityEngine.Events.UnityAction onSelect, bool isLocKey = true)
    {
        m_selectButton.onClick.RemoveAllListeners();
        m_selectButton.onClick.AddListener(onSelect);
        if (m_nameText != null)
        {
            if (isLocKey == true)
                CommonUtility.SetUILocText(m_nameText, displayName);
            else
                m_nameText.text = displayName;
        }

        if (m_backgroundImage == null)
            m_backgroundImage = m_selectButton.GetComponent<Image>();

        // 동적으로 Outline 부착 (UnityEngine.UI.Outline, QuickOutline 아님)
        m_outline = m_selectButton.GetComponent<UnityEngine.UI.Outline>();
        if (m_outline == null)
            m_outline = m_selectButton.gameObject.AddComponent<UnityEngine.UI.Outline>();
        m_outline.effectColor = m_colorSelected;
        m_outline.effectDistance = new Vector2(m_outlineWidth, -m_outlineWidth);
        m_outline.enabled = false;
    }

    // 배경색은 연구 상태, 외곽선(색상 포함)은 선택 여부로 독립 처리
    public void SetNodeState(EResearchNodeState baseState, bool isSelected)
    {
        if (m_backgroundImage == null) return;

        if (baseState == EResearchNodeState.Locked)
            m_backgroundImage.color = m_colorLocked;
        else if (baseState == EResearchNodeState.Current)
            m_backgroundImage.color = m_colorCurrent;
        else if (baseState == EResearchNodeState.Researched)
            m_backgroundImage.color = m_colorResearched;
        else
            m_backgroundImage.color = m_colorResearchable;

        // 풀 재사용 시 m_outline이 소실될 수 있으므로 방어적으로 재획득
        if (m_outline == null)
        {
            m_outline = m_selectButton.GetComponent<UnityEngine.UI.Outline>();
            if (m_outline == null)
                m_outline = m_selectButton.gameObject.AddComponent<UnityEngine.UI.Outline>();
            m_outline.effectColor    = m_colorSelected;
            m_outline.effectDistance = new Vector2(m_outlineWidth, -m_outlineWidth);
        }

        m_outline.enabled = isSelected;
        if (isSelected == true) m_backgroundImage.SetAllDirty();
    }
}
