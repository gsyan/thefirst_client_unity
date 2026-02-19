using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 연구 노드 기본 상태 (선택 여부와 별개 - 선택은 isSelected 파라미터로 전달)
public enum EResearchNodeState
{
    Researchable,   // 배울 수 있음 (선행 조건 충족)
    Researched      // 이미 배움
}

public class ScrollViewResearchItem : MonoBehaviour
{
    [SerializeField] private Button m_selectButton;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private Image m_backgroundImage;

    [Header("상태별 색상")]
    [SerializeField] private Color m_colorResearchable = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color m_colorResearched = new Color(0.2f, 0.8f, 0.4f, 1f);
    [SerializeField] private Color m_colorSelected = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private float m_outlineWidth = 4f;

    private UnityEngine.UI.Outline m_outline;

    // string locKey 기반 (범용)
    public void InitializeScrollViewResearchItem(string locKey, UnityEngine.Events.UnityAction onSelect)
    {
        m_selectButton.onClick.RemoveAllListeners();
        m_selectButton.onClick.AddListener(onSelect);
        if (m_nameText != null) CommonUtility.SetUILocText(m_nameText, locKey);

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

        m_backgroundImage.color = baseState == EResearchNodeState.Researched ? m_colorResearched : m_colorResearchable;

        if (m_outline != null)
            m_outline.enabled = isSelected;
    }
}
