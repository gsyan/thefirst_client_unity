using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupDailyBonusDayCell : MonoBehaviour
{
    public enum EDailyBonusCellState { Future, MissedNoReward, Claimed, ClaimedToday }

    [SerializeField] private TMP_Text m_dayText;
    [SerializeField] private GameObject m_checkIcon;
    [SerializeField] private Image m_background;
    [SerializeField] private Image m_borderHighlight; // ClaimedToday 전용 테두리

    [Header("Colors")]
    [SerializeField] private Color m_colorClaimed      = new Color(0.2f, 0.8f, 0.4f, 1f);
    [SerializeField] private Color m_colorClaimedToday = new Color(0.2f, 0.8f, 0.4f, 1f);
    [SerializeField] private Color m_colorMissed       = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color m_colorFuture       = new Color(0.15f, 0.15f, 0.15f, 1f);

    public void Setup(int day, EDailyBonusCellState state)
    {
        if (m_dayText != null)
            m_dayText.text = day.ToString();

        bool isClaimed = state == EDailyBonusCellState.Claimed || state == EDailyBonusCellState.ClaimedToday;
        if (m_checkIcon != null)
            m_checkIcon.SetActive(isClaimed);

        if (m_borderHighlight != null)
            m_borderHighlight.gameObject.SetActive(state == EDailyBonusCellState.ClaimedToday);

        if (m_background != null)
        {
            if (state == EDailyBonusCellState.Claimed)          m_background.color = m_colorClaimed;
            else if (state == EDailyBonusCellState.ClaimedToday) m_background.color = m_colorClaimedToday;
            else if (state == EDailyBonusCellState.MissedNoReward) m_background.color = m_colorMissed;
            else                                                 m_background.color = m_colorFuture;
        }
    }
}
