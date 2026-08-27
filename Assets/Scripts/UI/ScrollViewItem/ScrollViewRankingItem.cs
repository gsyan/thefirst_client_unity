// 랭킹 보드 단일 아이템 - 순위/이름/점수 표시, 내 순위 강조, 터치 시 상세 스탯 팝업
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScrollViewRankingItem : MonoBehaviour
{
    [SerializeField] private TMP_Text m_rankText;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private TMP_Text m_scoreText;
    [SerializeField] private GameObject m_myInfoImage;
    [SerializeField] private Button m_button;

    // tierName → 팔레트 키 (rank 4 이상)
    private static readonly (string tierName, string paletteKey)[] TIER_PALETTE_KEYS =
    {
        ("Bronze",   "PvpBronze"),
        ("Silver",   "PvpSilver"),
        ("Gold",     "PvpGold"),
        ("Platinum", "PvpPlatinum"),
        ("Diamond",  "PvpDiamond"),
    };

    private RankingEntry m_entry;

    private void Awake()
    {
        if (m_button != null)
            m_button.onClick.AddListener(OnClicked);
    }

    public void SetData(RankingEntry entry, bool isMyRank)
    {
        m_entry = entry;

        if (m_rankText != null)
        {
            m_rankText.text = entry.rank > 0 ? $"#{entry.rank}" : "-";
            m_rankText.color = GetRankTextColor(entry.rank, entry.score);
        }
        if (m_nameText != null) m_nameText.text = Commander.GetDisplayName(entry.commanderName, entry.commanderId);
        if (m_scoreText != null) m_scoreText.text = entry.score ?? "";
        if (m_myInfoImage != null) m_myInfoImage.SetActive(isMyRank);
    }

    private static Color GetRankTextColor(int rank, string scoreStr)
    {
        if (rank == 1) return CommonUtility.PaletteColor("PvpRank1");
        if (rank == 2) return CommonUtility.PaletteColor("PvpRank2");
        if (rank == 3) return CommonUtility.PaletteColor("PvpRank3");

        var pvpSeason = DataManager.Instance.m_dataTablePvpSeason;
        if (pvpSeason == null == true || int.TryParse(scoreStr, out int score) == false)
            return Color.gray;

        var tier = pvpSeason.GetTierByScore(score);
        if (tier == null) return Color.gray;

        for (int i = 0; i < TIER_PALETTE_KEYS.Length; i++)
        {
            if (TIER_PALETTE_KEYS[i].tierName == tier.tierName)
                return CommonUtility.PaletteColor(TIER_PALETTE_KEYS[i].paletteKey);
        }
        return Color.gray;
    }

    public void SetLoading()
    {
        m_entry = null;
        if (m_rankText != null) m_rankText.text = "...";
        if (m_nameText != null) m_nameText.text = "...";
        if (m_scoreText != null) m_scoreText.text = "...";
        if (m_myInfoImage != null) m_myInfoImage.SetActive(false);
    }

    private void OnClicked()
    {
        if (m_entry == null) return;
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);

        var rows = new List<(string label, string value)>
        {
            ("fleet_ship_count", m_entry.shipCount.ToString()),
            ("UIFleet_Stats_Health", CommonUtility.FormatBigNumber(m_entry.statHealth)),
            ("Simple_Attack", CommonUtility.FormatBigNumber(m_entry.statAttack)),
        };
        if (m_entry.statAirCount > 0)
        {
            rows.Add(("Simple_AirAttack", m_entry.statAirAttack.ToString()));
            rows.Add(("Simple_AirCount", m_entry.statAirCount.ToString()));
        }

        string name = Commander.GetDisplayName(m_entry.commanderName, m_entry.commanderId);
        string rankStr = m_entry.rank > 0 ? $"#{m_entry.rank}" : "-";
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message         = $"{name}\n{rankStr}  {m_entry.score}",
            pvpOpponentRows = rows,
            onConfirm       = () => { },
        });
    }
}
