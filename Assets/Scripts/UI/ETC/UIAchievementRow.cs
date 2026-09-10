// 업적 패널(UIPanelAchievement) 리스트의 행 1개 — InfiniteScrollView가 재활용하는 단일 프리팹이 헤더/업적 두 표시 상태를 토글
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIAchievementRow : MonoBehaviour
{
    [Header("카테고리 헤더 상태")]
    [SerializeField] private GameObject m_headerRoot;
    [SerializeField] private TMP_Text m_headerLabelText;
    [SerializeField] private GameObject m_headerRedDot; // 이 카테고리에 완료+미수령 업적이 있을 때만 표시

    [Header("업적 항목 상태")]
    [SerializeField] private GameObject m_itemRoot;
    [SerializeField] private GameObject m_itemRedDot; // 이 항목이 완료+미수령 상태일 때만 표시
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private TMP_Text m_descText;
    [SerializeField] private RowLabelValue m_progressRow;
    [SerializeField] private RowLabelValue m_rewardRow;
    [SerializeField] private RectTransform m_progressRewardLayoutRoot; // Horizon — HorizontalLayoutGroup+ContentSizeFitter로 Progress/Reward를 나란히 배치, 텍스트 변경 후 강제 리빌드 필요
    [SerializeField] private Button m_claimButton;
    [SerializeField] private GameObject m_claimedRoot; // 수령 완료 시 m_claimButton 대신 표시(체크 아이콘 등)

    private string m_achievementId;
    private System.Action<string> m_onClaimClick;

    private void Awake()
    {
        if (m_claimButton != null)
            m_claimButton.onClick.AddListener(OnClaimButtonClicked);
    }

    public void SetupHeader(string headerLabel, bool hasUnclaimed)
    {
        gameObject.SetActive(true);
        if (m_headerRoot != null) m_headerRoot.SetActive(true);
        if (m_itemRoot != null) m_itemRoot.SetActive(false);

        if (m_headerLabelText != null)
            m_headerLabelText.text = headerLabel;
        if (m_headerRedDot != null)
            m_headerRedDot.SetActive(hasUnclaimed);
    }

    public void SetupItem(AchievementData data, int currentValue, bool isClaimed, System.Action<string> onClaimClick)
    {
        gameObject.SetActive(true);
        if (m_headerRoot != null) m_headerRoot.SetActive(false);
        if (m_itemRoot != null) m_itemRoot.SetActive(true);

        m_achievementId = data.achievementId;
        m_onClaimClick = onClaimClick;

        bool isCompleted = currentValue >= data.threshold;

        if (m_nameText != null)
            CommonUtility.SetUILocText(m_nameText, data.nameKey);
        if (m_descText != null)
            m_descText.text = string.Format(LocalizationManager.Instance.Get(data.descKey), data.threshold, data.achievementPointReward);
        if (m_progressRow != null)
            m_progressRow.SetRow("UIAchievement_Progress", $"{Mathf.Min(currentValue, data.threshold)}/{data.threshold}", rawValue: true);
        if (m_rewardRow != null)
            m_rewardRow.SetRow("UIAchievement_Reward", $"+{data.achievementPointReward}", rawValue: true);
        if (m_progressRewardLayoutRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_progressRewardLayoutRoot);

        if (m_claimedRoot != null)
            m_claimedRoot.SetActive(isClaimed);
        if (m_claimButton != null)
        {
            m_claimButton.gameObject.SetActive(isClaimed == false);
            m_claimButton.interactable = isCompleted;
        }
        if (m_itemRedDot != null)
            m_itemRedDot.SetActive(isCompleted && isClaimed == false);
    }

    private void OnClaimButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onClaimClick != null) m_onClaimClick(m_achievementId);
    }
}
