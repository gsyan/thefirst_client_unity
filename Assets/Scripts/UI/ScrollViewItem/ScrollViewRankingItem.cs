// 랭킹 보드 단일 아이템 - 순위/이름/점수/능력치 표시, 내 순위 강조
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScrollViewRankingItem : MonoBehaviour
{
    [SerializeField] private TMP_Text m_rankText;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private TMP_Text m_scoreText;
    [SerializeField] private Image m_highlightImage;

    private static readonly Color COLOR_OTHER = new(0x00 / 255f, 0x96 / 255f, 0x82 / 255f, 1f); // #009682
    private static readonly Color COLOR_MINE  = new(0x00 / 255f, 0xFF / 255f, 0x82 / 255f, 1f); // #00FF82
    [SerializeField] private Transform m_statContainer;

    private RowImageText[] m_statRows;

    private void Awake()
    {
        if (m_statContainer != null)
            m_statRows = m_statContainer.GetComponentsInChildren<RowImageText>(true);
    }

    public void SetData(RankingEntry entry, bool isMyRank)
    {
        if (m_rankText != null) m_rankText.text = entry.rank > 0 ? $"#{entry.rank}" : "-";
        if (m_nameText != null) m_nameText.text = Character.GetDisplayName(entry.characterName, entry.characterId);
        if (m_scoreText != null) m_scoreText.text = entry.score ?? "";
        if (m_highlightImage != null) m_highlightImage.color = isMyRank ? COLOR_MINE : COLOR_OTHER;
        PopulateStats(entry);
    }

    public void SetLoading()
    {
        if (m_rankText != null) m_rankText.text = "...";
        if (m_nameText != null) m_nameText.text = "...";
        if (m_scoreText != null) m_scoreText.text = "...";
        if (m_highlightImage != null) m_highlightImage.color = COLOR_OTHER;
        HideAllStats();
    }

    private void PopulateStats(RankingEntry entry)
    {
        HideAllStats();
        if (m_statRows == null || m_statRows.Length == 0) return;

        int idx = 0;
        if (idx < m_statRows.Length && entry.shipCount > 0)
            m_statRows[idx++].SetRow("icon_ship", entry.shipCount.ToString());
        if (idx < m_statRows.Length && entry.statAttack > 0f)
            m_statRows[idx++].SetRow("bubbling-beam", CommonUtility.FormatBigNumber(entry.statAttack));
        if (idx < m_statRows.Length && entry.statHealth > 0f)
            m_statRows[idx++].SetRow("techno-heart", CommonUtility.FormatBigNumber(entry.statHealth));
        
        if (idx < m_statRows.Length && entry.statAirCount > 0)
            m_statRows[idx++].SetRow("jet-fighter", entry.statAirCount.ToString());
        if (idx < m_statRows.Length && entry.statAirAttack > 0)
            m_statRows[idx++].SetRow("strafe", entry.statAirAttack.ToString());

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_statContainer as RectTransform);
    }

    private void HideAllStats()
    {
        if (m_statRows == null) return;
        for (int i = 0; i < m_statRows.Length; i++)
            m_statRows[i].Hide();
    }
}
