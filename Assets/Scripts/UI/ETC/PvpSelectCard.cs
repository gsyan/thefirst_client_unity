// PvP 상대 선택 카드 - 이름/랭크/점수/함대 스탯/공격 버튼 표시
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PvpSelectCard : MonoBehaviour
{
    [SerializeField] private Button m_attackButton;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private TMP_Text m_scoreRankText;
    [SerializeField] private TMP_Text m_hpText;
    [SerializeField] private TMP_Text m_atkText;
    [SerializeField] private TMP_Text m_shipCountText;
    [SerializeField] private TMP_Text m_aircraftCountText;

    private PvpOpponentInfo m_opponentInfo;
    public PvpOpponentInfo OpponentInfo => m_opponentInfo;

    public void InitializePvpSelectCard(PvpOpponentInfo opponentInfo, UnityEngine.Events.UnityAction onAttack)
    {
        m_opponentInfo = opponentInfo;

        m_attackButton.onClick.RemoveAllListeners();
        m_attackButton.onClick.AddListener(onAttack);

        CapabilityProfile stats = CommonUtility.GetFleetCapabilityProfile(opponentInfo.fleetInfo);
        int shipCount = (opponentInfo.fleetInfo != null && opponentInfo.fleetInfo.ships != null)
            ? opponentInfo.fleetInfo.ships.Count : 0;

        if (m_nameText != null)        m_nameText.text        = Character.GetDisplayName(opponentInfo.characterName, opponentInfo.characterId);
        if (m_scoreRankText != null)   m_scoreRankText.text   = LocalizationManager.Instance.Get("pvp_score_rank", opponentInfo.pvpScore, opponentInfo.rank);
        if (m_hpText != null)          m_hpText.text          = CommonUtility.FormatBigNumber(stats.health_power);
        if (m_atkText != null)         m_atkText.text         = CommonUtility.FormatBigNumber(stats.attack_power);
        if (m_shipCountText != null)   m_shipCountText.text   = $"{shipCount}";
        if (m_aircraftCountText != null) m_aircraftCountText.text = $"{stats.aircraft_count}";
    }

    public void SetEmpty()
    {
        m_opponentInfo = null;
        m_attackButton.onClick.RemoveAllListeners();
        if (m_nameText != null)          m_nameText.text          = "-";
        if (m_scoreRankText != null)     m_scoreRankText.text     = "-";
        if (m_hpText != null)            m_hpText.text            = "-";
        if (m_atkText != null)           m_atkText.text           = "-";
        if (m_shipCountText != null)     m_shipCountText.text     = "-";
        if (m_aircraftCountText != null) m_aircraftCountText.text = "-";
    }
}
