// 랭킹 보드 단일 아이템 - 순위/이름/점수 표시, 내 순위 강조
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScrollViewRankingItem : MonoBehaviour
{
    [SerializeField] private TMP_Text m_rankText;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private TMP_Text m_scoreText;
    [SerializeField] private Image m_highlightImage; // 내 순위 강조 (없으면 무시)

    public void SetData(RankingEntry entry, bool isMyRank)
    {
        if (m_rankText != null) m_rankText.text = entry.rank > 0 ? entry.rank.ToString() : "-";
        if (m_nameText != null) m_nameText.text = entry.characterName;
        if (m_scoreText != null) m_scoreText.text = entry.score ?? "";
        if (m_highlightImage != null) m_highlightImage.enabled = isMyRank;
    }

    public void SetLoading()
    {
        if (m_rankText != null) m_rankText.text = "...";
        if (m_nameText != null) m_nameText.text = "...";
        if (m_scoreText != null) m_scoreText.text = "...";
        if (m_highlightImage != null) m_highlightImage.enabled = false;
    }
}
