using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 연구 노드의 3가지 상태
public enum EResearchNodeState
{
    Researchable,   // 배울 수 있음 (선행 조건 충족)
    Current,        // 현재 선택됨
    Researched      // 이미 배움
}

public class ScrollViewResearchItem : MonoBehaviour
{
    [SerializeField] private Button m_selectButton;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private Image m_backgroundImage;

    [Header("상태별 색상")]
    [SerializeField] private Color m_colorResearchable = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color m_colorCurrent = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color m_colorResearched = new Color(0.2f, 0.8f, 0.4f, 1f);

    // string locKey 기반 (범용)
    public void InitializeScrollViewResearchItem(string locKey, UnityEngine.Events.UnityAction onSelect)
    {
        m_selectButton.onClick.RemoveAllListeners();
        m_selectButton.onClick.AddListener(onSelect);
        if (m_nameText != null) CommonUtility.SetUILocText(m_nameText, locKey);

        if (m_backgroundImage == null)
            m_backgroundImage = m_selectButton.GetComponent<Image>();
    }

    // 상태에 따라 배경색 적용
    public void SetNodeState(EResearchNodeState state)
    {
        if (m_backgroundImage == null) return;

        switch (state)
        {
            case EResearchNodeState.Researched:
                m_backgroundImage.color = m_colorResearched;
                break;
            case EResearchNodeState.Current:
                m_backgroundImage.color = m_colorCurrent;
                break;
            case EResearchNodeState.Researchable:
                m_backgroundImage.color = m_colorResearchable;
                break;
        }
    }
}
