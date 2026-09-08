using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 출석 달력의 요일 하나 — UIAchievementRow와 동일하게 InfiniteScrollView가 재활용하는 좌우로 긴 행
// 보상 항목은 개수가 가변(4~6일차는 탐험+업적 2종)이라 항목별 아이콘 슬롯 대신 한 줄로 이어붙여 표시 —
// 타입이 더 늘어도 프리팹 변경 없이 그대로 확장됨
public class UIDailyBonusDayRow : MonoBehaviour
{
    [SerializeField] private TMP_Text m_dayText;
    [SerializeField] private RowLabelValue m_rewardRow;
    [SerializeField] private Button m_claimButton;      // 항상 표시하되 오늘 행 + 미수령 상태일 때만 interactable
    [SerializeField] private GameObject m_claimedRoot;  // 오늘 행 수령 완료 표시(체크 아이콘 등)

    private Action m_onClaimClick;

    private void Awake()
    {
        if (m_claimButton != null)
            m_claimButton.onClick.AddListener(OnClaimButtonClicked);
    }

    public void SetupDailyBonusDayCell(int day, bool claimed, bool bToday, DailyBonusRewardEntry[] rewards, Action onClaimClick)
    {
        if (m_dayText != null)
            m_dayText.text = LocalizationManager.Instance.Get("DailyBonus_DayLabel", day);

        m_onClaimClick = onClaimClick;
        if (m_claimButton != null)
            m_claimButton.interactable = bToday && claimed == false;
        if (m_claimedRoot != null)
            m_claimedRoot.SetActive(bToday && claimed);

        RefreshRewards(rewards);
    }

    private void OnClaimButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onClaimClick != null)
            m_onClaimClick();
    }

    private void RefreshRewards(DailyBonusRewardEntry[] rewards)
    {
        if (m_rewardRow == null) return;

        string rewardText = BuildRewardListText(rewards);
        if (string.IsNullOrEmpty(rewardText) == true)
        {
            m_rewardRow.Hide();
            return;
        }

        m_rewardRow.SetRow("DailyBonus_RewardLabel", rewardText, rawValue: true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_rewardRow.transform.parent as RectTransform);
    }

    // "탐사 포인트 +100, 업적포인트 +10" 형태로 이어붙임 — amount<=0(아직 값 안 채운 VIP 등)은 건너뜀
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

            if (rewards[i].tier == EDailyBonusTier.VIP)
            {
                sb.Append(loc.Get("DailyBonus_VipPrefix"));
                sb.Append(' ');
            }

            sb.Append(loc.Get(GetRewardTypeLocKey(rewards[i].rewardType)));
            sb.Append(" +");
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
