// 보상카드 3택1 팝업의 카드 버튼 1개 — 이름/설명 텍스트와 클릭 콜백을 스스로 관리
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RewardCardButton : MonoBehaviour
{
    [SerializeField] private Button m_button;
    [SerializeField] private Image m_iconImage;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private TMP_Text m_descText;
    [SerializeField] private GameObject m_selectedIndicator; // 선택됨을 표시하는 테두리/체크 등 — 없으면(null) 시각 표시 없이 클릭만 동작

    private System.Action m_onClicked;

    private void Awake()
    {
        if (m_button == null)
            m_button = GetComponent<Button>();
        m_button.onClick.AddListener(OnClicked);
    }

    public void SetCard(RewardCardData card, System.Action onClicked)
    {
        gameObject.SetActive(true);
        m_onClicked = onClicked;

        if (card == null)
        {
            m_nameText.text = "";
            m_descText.text = "";
            return;
        }

        CommonUtility.SetUILocText(m_nameText, card.nameKey);
        m_descText.text = LocalizationManager.Instance.Get(card.descKey, card.value1, card.value2);
        if (m_iconImage != null)
            m_iconImage.sprite = UISpriteCache.Get(card.iconName);
        SetSelected(false);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        m_onClicked = null;
    }

    public void SetSelected(bool isSelected)
    {
        if (m_selectedIndicator != null)
            m_selectedIndicator.SetActive(isSelected);
    }

    private void OnClicked()
    {
        m_onClicked?.Invoke();
    }
}
