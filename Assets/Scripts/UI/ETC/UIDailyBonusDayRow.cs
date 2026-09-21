using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 출석 달력의 요일 하나 — UIAchievementRow와 동일하게 InfiniteScrollView가 재활용하는 좌우로 긴 행
// 일반 보상(Normal)과 VIP 전용 보상(VIP)을 완전히 별개로 수령 — VIP 블록은 비VIP에게도 잠긴 채로 노출(가입 유도)
public class UIDailyBonusDayRow : MonoBehaviour
{
    [SerializeField] private TMP_Text m_dayText;

    [Header("일반 보상")]
    [SerializeField] private TMP_Text m_rewardText;
    [SerializeField] private Button m_claimButton;     // 항상 표시하되 클레임 가능(출석일수 이하) + 미수령 상태일 때만 interactable
    [SerializeField] private GameObject m_claimedRoot;  // 오늘 행 수령 완료 표시(체크 아이콘 등)

    [Header("VIP 전용 보상 — 비VIP에게도 잠긴 채로 노출(가입 유도)")]
    [SerializeField] private TMP_Text m_vipRewardText;
    [SerializeField] private Button m_vipClaimButton;
    [SerializeField] private GameObject m_vipClaimedRoot;

    private Action<bool> m_onClaimClick; // (claimVip)

    private void Awake()
    {
        if (m_claimButton != null)
            m_claimButton.onClick.AddListener(OnClaimButtonClicked);
        if (m_vipClaimButton != null)
            m_vipClaimButton.onClick.AddListener(OnVipClaimButtonClicked);
    }

    public void SetupDailyBonusDayCell(int day, bool claimed, bool vipClaimed, bool bClaimable, bool isVipActive,
        DailyBonusRewardEntry[] normalRewards, DailyBonusRewardEntry[] vipRewards, Action<bool> onClaimClick)
    {
        if (m_dayText != null)
            m_dayText.text = LocalizationManager.Instance.Get("DailyBonus_DayLabel", day);

        m_onClaimClick = onClaimClick;

        if (m_claimButton != null)
        {
            m_claimButton.gameObject.SetActive(claimed == false);
            m_claimButton.interactable = bClaimable && claimed == false;
        }
        if (m_claimedRoot != null)
            m_claimedRoot.SetActive(claimed);
        RefreshRewardText(m_rewardText, normalRewards);

        // VIP 버튼은 비VIP여도 항상 노출은 하되(가입 유도), 비VIP면 비활성화해 클릭 자체가 안 되게 함
        if (m_vipClaimButton != null)
        {
            m_vipClaimButton.gameObject.SetActive(vipClaimed == false);
            m_vipClaimButton.interactable = isVipActive == true && bClaimable == true && vipClaimed == false;
        }
        if (m_vipClaimedRoot != null)
            m_vipClaimedRoot.SetActive(vipClaimed);
        RefreshRewardText(m_vipRewardText, vipRewards);
    }

    private void OnClaimButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onClaimClick != null) m_onClaimClick(false);
    }

    private void OnVipClaimButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onClaimClick != null) m_onClaimClick(true);
    }

    private void RefreshRewardText(TMP_Text rewardTextComponent, DailyBonusRewardEntry[] rewards)
    {
        if (rewardTextComponent == null) return;

        string rewardText = BuildRewardListText(rewards);
        if (string.IsNullOrEmpty(rewardText) == true)
        {
            rewardTextComponent.gameObject.SetActive(false);
            return;
        }

        rewardTextComponent.gameObject.SetActive(true);
        rewardTextComponent.text = rewardText;
    }

    // "탐험 포인트 100, 업적포인트 10" 형태로 이어붙임 — amount<=0인 항목은 건너뜀
    private static string BuildRewardListText(DailyBonusRewardEntry[] rewards)
    {
        if (rewards == null || rewards.Length == 0) return string.Empty;

        var loc = LocalizationManager.Instance;
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < rewards.Length; i++)
        {
            if (rewards[i].amount <= 0) continue;

            if (sb.Length > 0)
                sb.Append(", ");

            sb.Append(loc.Get(GetRewardTypeLocKey(rewards[i].rewardType)));
            sb.Append(' ');
            sb.Append(rewards[i].amount);
        }

        return sb.ToString();
    }

    private static string GetRewardTypeLocKey(EDailyBonusRewardType type)
    {
        if (type == EDailyBonusRewardType.AchievementPoint)
            return "UIPanelFleet_AchievementPoint";
        return "ExplorationPoint";
    }
}
