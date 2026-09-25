// 업적 패널(UIPanelAchievement) 리스트의 행 1개 — InfiniteScrollView가 재활용하는 단일 프리팹이 헤더/업적 두 표시 상태를 토글
// 업적 항목은 일반 보상(Normal)과 VIP 전용 보상(VIP)을 완전히 별개로 수령 — VIP 블록은 비VIP 유저에게도 잠긴 채로 노출해 가입을 유도함
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

    [Header("일반 보상")]
    [SerializeField] private RowLabelValue m_rewardRow;
    [SerializeField] private Button m_claimButton;
    [SerializeField] private GameObject m_claimedRoot; // 수령 완료 시 m_claimButton 대신 표시(체크 아이콘 등)

    [Header("VIP 전용 보상 — 비VIP에게도 잠긴 채로 노출(가입 유도)")]
    [SerializeField] private RowLabelValue m_vipRewardRow;
    [SerializeField] private Button m_vipClaimButton;
    [SerializeField] private GameObject m_vipClaimedRoot;

    private string m_achievementId;
    private System.Action<string, bool> m_onClaimClick; // (achievementId, claimVip)

    private void Awake()
    {
        if (m_claimButton != null)
            m_claimButton.onClick.AddListener(OnClaimButtonClicked);
        if (m_vipClaimButton != null)
            m_vipClaimButton.onClick.AddListener(OnVipClaimButtonClicked);
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

    public void SetupItem(AchievementData data, int currentValue, bool isClaimed, bool isVipClaimed, bool isVipActive,
        System.Action<string, bool> onClaimClick)
    {
        gameObject.SetActive(true);
        if (m_headerRoot != null) m_headerRoot.SetActive(false);
        if (m_itemRoot != null) m_itemRoot.SetActive(true);

        m_achievementId = data.achievementId;
        m_onClaimClick = onClaimClick;

        bool isCompleted = currentValue >= data.threshold;

        string achievementName = string.Format(LocalizationManager.Instance.Get(data.nameKey), data.conditionParam, data.threshold, data.achievementPointReward);
        string progressLabel = LocalizationManager.Instance.Get("UIAchievement_Progress");
        string progressValue = $"{Mathf.Min(currentValue, data.threshold)}/{data.threshold}";
        if (m_nameText != null)
            m_nameText.text = $"{achievementName} ({progressLabel} {progressValue})";
        if (m_descText != null)
            m_descText.text = string.Format(LocalizationManager.Instance.Get(data.descKey), data.conditionParam, data.threshold, data.achievementPointReward);
        if (m_rewardRow != null)
        {
            string rewardText = CommonUtility.FormatNumber(data.achievementPointReward);
            m_rewardRow.SetRow("UIAchievement_Reward", $"+{rewardText}", rawValue: true);
        }
        if (m_vipRewardRow != null)
        {
            string vipRewardText = CommonUtility.FormatNumber(data.achievementPointRewardVip);
            m_vipRewardRow.SetRow("UIAchievement_RewardVip", $"+{vipRewardText}", rawValue: true);
        }

        if (m_claimedRoot != null)
            m_claimedRoot.SetActive(isClaimed);
        if (m_claimButton != null)
        {
            m_claimButton.gameObject.SetActive(isClaimed == false);
            m_claimButton.interactable = isCompleted;
        }

        // VIP 버튼은 비VIP여도 항상 노출은 하되(가입 유도), 비VIP면 비활성화해 클릭 자체가 안 되게 함
        if (m_vipClaimedRoot != null)
            m_vipClaimedRoot.SetActive(isVipClaimed);
        if (m_vipClaimButton != null)
        {
            m_vipClaimButton.gameObject.SetActive(isVipClaimed == false);
            m_vipClaimButton.interactable = isVipActive == true && isCompleted == true;
        }

        bool hasUnclaimedNormal = isCompleted == true && isClaimed == false;
        bool hasUnclaimedVip = isVipActive == true && isCompleted == true && isVipClaimed == false;
        if (m_itemRedDot != null)
            m_itemRedDot.SetActive(hasUnclaimedNormal || hasUnclaimedVip);
    }

    private void OnClaimButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onClaimClick != null) m_onClaimClick(m_achievementId, false);
    }

    private void OnVipClaimButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onClaimClick != null) m_onClaimClick(m_achievementId, true);
    }
}
