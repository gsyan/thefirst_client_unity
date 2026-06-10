using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupDailyBonusDayCell : MonoBehaviour
{
    public enum EDailyBonusCellState { Future, MissedNoReward, Claimed, ClaimedToday }

    [SerializeField] private TMP_Text m_dayText;
    [SerializeField] private GameObject m_checkIcon;
    [SerializeField] private Image m_background;
    [SerializeField] private Image m_borderHighlight;
    [SerializeField] private RectTransform m_rewardContainer;

    [Header("Colors")]
    [SerializeField] private Color m_colorClaimed = new Color(0.2f, 0.8f, 0.4f, 1f);
    [SerializeField] private Color m_colorPassed  = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color m_colorFuture  = new Color(0.15f, 0.15f, 0.15f, 1f);

    private readonly List<RowImageText> m_rows = new List<RowImageText>();
    private static RowImageText s_rowPrefab;

    private static RowImageText GetRowPrefab()
    {
        if (s_rowPrefab == null)
            s_rowPrefab = Resources.Load<RowImageText>("Prefabs/UI/ETC/RowImageText");
        return s_rowPrefab;
    }

    public void SetupDailyBonusDayCell(int day, EDailyBonusCellState state, bool vipClaimed, DailyBonusRewardEntry[] rewards)
    {
        if (m_dayText != null)
            m_dayText.text = day.ToString();

        bool isClaimed = state == EDailyBonusCellState.Claimed || state == EDailyBonusCellState.ClaimedToday;
        if (m_checkIcon != null)
            m_checkIcon.SetActive(isClaimed);

        if (m_borderHighlight != null)
        {
            bool isToday = state == EDailyBonusCellState.ClaimedToday;
            m_borderHighlight.gameObject.SetActive(isToday);
            if (isToday == true)
                m_borderHighlight.color = CommonUtility.PaletteColor("Selected");
        }

        if (m_background != null)
        {
            if (state == EDailyBonusCellState.Claimed || state == EDailyBonusCellState.ClaimedToday)
                m_background.color = m_colorPassed;
            else if (state == EDailyBonusCellState.MissedNoReward)
                m_background.color = m_colorPassed;
            else
                m_background.color = m_colorFuture;
        }

        RefreshRewards(rewards, isClaimed, vipClaimed);
    }

    private void RefreshRewards(DailyBonusRewardEntry[] rewards, bool isClaimed, bool vipClaimed)
    {
        if (m_rewardContainer == null) return;

        int rewardCount = (rewards == null) ? 0 : rewards.Length;

        // 부족한 row는 생성
        if (rewardCount > m_rows.Count)
        {
            RowImageText prefab = GetRowPrefab();
            if (prefab == null)
            {
                Debug.LogError("[UIPopupDailyBonusDayCell] RowImageText 프리팹 로드 실패");
                return;
            }
            while (m_rows.Count < rewardCount)
                m_rows.Add(Instantiate(prefab, m_rewardContainer));
        }

        Color textColor = CommonUtility.PaletteColor(isClaimed ? "GeneralBright1" : "GeneralDark2");

        // 사용하는 row는 내용 갱신
        for (int i = 0; i < rewardCount; i++)
        {
            string spriteName = GetSpriteName(rewards[i].rewardType);
            string text = $"+{rewards[i].amount}";

            if (rewards[i].tier == EDailyBonusTier.VIP && vipClaimed == true)
                m_rows[i].SetRow(spriteName, text, "rank-3");
            else
                m_rows[i].SetRow(spriteName, text);

            m_rows[i].SetImageColor(CommonUtility.PaletteColor("Mineral"));
            m_rows[i].SetTextColor(textColor);
        }

        // 남는 row는 숨김
        for (int i = rewardCount; i < m_rows.Count; i++)
            m_rows[i].Hide();
    }

    private static string GetSpriteName(EDailyBonusRewardType type)
    {
        if (type == EDailyBonusRewardType.Mineral) return "mineral_basic";
        return "mineral_basic";
    }
}
