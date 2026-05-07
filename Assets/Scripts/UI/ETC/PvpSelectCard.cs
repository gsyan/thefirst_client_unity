// PvP 상대 선택 카드 - 이름/랭크/점수/함대 스탯/공격 버튼 표시
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PvpSelectCard : MonoBehaviour
{
    [SerializeField] private Button m_attackButton;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private TMP_Text m_scoreRankText;
    [SerializeField] private Transform m_statsContainer1;
    [SerializeField] private Transform m_statsContainer2;

    private RowImageText[] m_rows1;
    private RowImageText[] m_rows2;

    private PvpOpponentInfo m_opponentInfo;
    public PvpOpponentInfo OpponentInfo => m_opponentInfo;

    private void Awake()
    {
        if (m_statsContainer1 != null)
            m_rows1 = m_statsContainer1.GetComponentsInChildren<RowImageText>(true);
        if (m_statsContainer2 != null)
            m_rows2 = m_statsContainer2.GetComponentsInChildren<RowImageText>(true);
    }

    public void InitializePvpSelectCard(PvpOpponentInfo opponentInfo, UnityEngine.Events.UnityAction onAttack)
    {
        m_opponentInfo = opponentInfo;

        m_attackButton.onClick.RemoveAllListeners();
        m_attackButton.onClick.AddListener(onAttack);

        CapabilityProfile stats = CommonUtility.GetFleetCapabilityProfile(opponentInfo.fleetInfo);
        int shipCount = (opponentInfo.fleetInfo != null && opponentInfo.fleetInfo.ships != null)
            ? opponentInfo.fleetInfo.ships.Count : 0;

        if (m_nameText != null)      m_nameText.text      = Character.GetDisplayName(opponentInfo.characterName, opponentInfo.characterId);
        if (m_scoreRankText != null) m_scoreRankText.text = LocalizationManager.Instance.Get("UITabRank_ScoreRank", opponentInfo.pvpScore, opponentInfo.rank);

        PopulateStats(stats, shipCount);
    }

    public void SetEmpty()
    {
        m_opponentInfo = null;
        m_attackButton.onClick.RemoveAllListeners();
        if (m_nameText != null)      m_nameText.text      = "-";
        if (m_scoreRankText != null) m_scoreRankText.text = "-";
        HideAllStats();
    }

    private void PopulateStats(CapabilityProfile stats, int shipCount)
    {
        HideAllStats();
        if (m_rows1 == null || m_rows1.Length < 3) return;

        m_rows1[0].SetRow("icon_ship",     shipCount.ToString());
        m_rows1[1].SetRow("techno-heart",  CommonUtility.FormatBigNumber(stats.health));
        m_rows1[2].SetRow("bubbling-beam", CommonUtility.FormatBigNumber(stats.attack));
        
        if (stats.airCount > 0 && m_rows2 != null && m_rows2.Length > 0)
            m_rows2[0].SetRow("jet-fighter", stats.airCount.ToString());
        
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_statsContainer1 as RectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_statsContainer2 as RectTransform);
    }

    private void HideAllStats()
    {
        if (m_rows1 != null)
        {
            for (int i = 0; i < m_rows1.Length; i++)
                m_rows1[i].Hide();
        }
        if (m_rows2 != null)
        {
            for (int i = 0; i < m_rows2.Length; i++)
                m_rows2[i].Hide();
        }
    }
}
