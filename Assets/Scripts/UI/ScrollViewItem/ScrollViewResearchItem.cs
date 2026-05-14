using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 연구 노드 기본 상태 (선택 여부와 별개 - 선택은 isSelected 파라미터로 전달)
public enum EResearchNodeState
{
    Locked,         // 안배움
    Researched,     // 배움
    Current,        // 현재 장착 중    
}

public class ScrollViewResearchItem : MonoBehaviour
{
    [SerializeField] private Button m_selectButton;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private Image m_borderImage;
    [SerializeField] private Image m_bgImage;

    [Header("상태별 색상")]
    [SerializeField] private Color m_colorLocked    = CommonUtility.HexColor("#253549");
    [SerializeField] private Color m_colorResearched = CommonUtility.HexColor("#2DE8A0");
    [SerializeField] private Color m_colorSelected  = CommonUtility.HexColor("#F0B429");

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

        if (m_borderImage == null)
            m_borderImage = m_selectButton.GetComponent<Image>();
    }

    public void SetNodeState(EResearchNodeState baseState, bool isSelected)
    {
        if (m_borderImage == null) return;

        // borderImage: selected → colorSelected 우선, 아니면 baseState 기준
        Color borderColor;
        if (isSelected == true)
            borderColor = m_colorSelected;
        else if (baseState == EResearchNodeState.Current)
            borderColor = m_colorResearched;
        else if (baseState == EResearchNodeState.Researched)
            borderColor = m_colorResearched;
        else
            borderColor = m_colorLocked;

        m_borderImage.color = borderColor;

        // bgImage: Current(비선택)만 solid fill, 나머지 검정
        if (m_bgImage != null)
            m_bgImage.color = (baseState == EResearchNodeState.Current && isSelected == false) ? m_colorResearched : Color.black;

        // nameText: Current(비선택) → 검정, 나머지 → borderColor
        if (m_nameText != null)
            m_nameText.color = (baseState == EResearchNodeState.Current && isSelected == false) ? Color.black : borderColor;
    }
}
