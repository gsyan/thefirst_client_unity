using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupDailyBonusDayCell : MonoBehaviour
{
    [SerializeField] private TMP_Text m_dayText;
    [SerializeField] private Image m_background;
    [SerializeField] private Image m_borderHighlight;
    // 하루 보상은 항상 일반(0)+VIP(1) 2줄 고정 — 프리팹에 미리 배치
    [SerializeField] private UIPopupDailyBonusRewardRow[] m_rewardRows;

    public void SetupDailyBonusDayCell(int day, bool claimed, bool vipClaimed, bool bToday, bool passed, DailyBonusRewardEntry[] rewards)
    {
        if (m_dayText != null)
            m_dayText.text = day.ToString();

        if (m_borderHighlight != null)
        {
            m_borderHighlight.gameObject.SetActive(bToday);
            if (bToday == true)
                m_borderHighlight.color = CommonUtility.PaletteColor("Selected");
        }

        if (m_background != null)
            m_background.color = CommonUtility.PaletteColor(passed == true ? "Olive1000" : "Black2");

        RefreshRewards(rewards, claimed, vipClaimed);
    }

    private void RefreshRewards(DailyBonusRewardEntry[] rewards, bool isClaimed, bool vipClaimed)
    {
        if (m_rewardRows == null) return;

        int rewardCount = (rewards == null) ? 0 : rewards.Length;

        for (int i = 0; i < m_rewardRows.Length; i++)
        {
            if (m_rewardRows[i] == null) continue;

            if (i >= rewardCount)
            {
                m_rewardRows[i].gameObject.SetActive(false);
                continue;
            }

            m_rewardRows[i].gameObject.SetActive(true);
            RowImageText row = m_rewardRows[i].GetRow();

            string spriteName = GetSpriteName(rewards[i].rewardType);
            string text = $"{rewards[i].amount}";
            bool isVipTier = rewards[i].tier == EDailyBonusTier.VIP;
            bool rowClaimed = isVipTier ? vipClaimed : isClaimed;

            row.SetRow(spriteName, text);
            // 획득 여부에 따라 숫자 색 이원화 — 획득함=Text.Dark1, 못함=Text.Dark2(같은 계열, 더 어둡게)
            row.SetTextColor(CommonUtility.PaletteColor(rowClaimed ? "Text.Dark1" : "Text.Dark2"));

            // VIP 줄은 앰버 계열로 포인트 — 일반 줄과 색으로 구분되도록
            string accentBright = isVipTier ? "Vip"     : "General";
            string accentDark   = isVipTier ? "VipDark" : "General.Dark2";

            row.SetImageColor(CommonUtility.PaletteColor(rowClaimed ? "Mineral" : "MineralDark"));

            Image vipIcon = m_rewardRows[i].GetVipIcon();
            if (isVipTier == true && vipIcon != null)
                vipIcon.color = CommonUtility.PaletteColor(rowClaimed ? "Vip" : "VipDark");

            // 체크마크는 줄(row) 단위로 독립 — 일반/VIP 중 하나만 받아도 그 줄만 체크됨
            // 정렬 유지를 위해 SetActive 대신 alpha만 조절 (비활성화 시 레이아웃에서 공간이 사라짐)
            Color checkColor = CommonUtility.PaletteColor(rowClaimed ? accentBright : accentDark);
            checkColor.a = rowClaimed ? 1f : 0f;
            m_rewardRows[i].GetCheckmark().color = checkColor;
        }
    }

    private static string GetSpriteName(EDailyBonusRewardType type)
    {
        return "exploration_point";
    }
}
