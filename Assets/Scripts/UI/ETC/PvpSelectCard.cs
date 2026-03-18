// PvP 상대 선택 카드 - 이름/랭크/점수/함대 스탯/공격 버튼 표시
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PvpSelectCard : MonoBehaviour
{
    [SerializeField] private Button m_attackButton;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private TMP_Text m_scoreRankText;
    [SerializeField] private TMP_Text m_statText;

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

        if (m_nameText != null)      m_nameText.text      = Character.GetDisplayName(opponentInfo.characterName, opponentInfo.characterId);
        if (m_scoreRankText != null) m_scoreRankText.text = LocalizationManager.Instance.Get("pvp_score_rank", opponentInfo.pvpScore, opponentInfo.rank);
        if (m_statText != null)      m_statText.text      = BuildStatText(stats, shipCount);
    }

    public void SetEmpty()
    {
        m_opponentInfo = null;
        m_attackButton.onClick.RemoveAllListeners();
        if (m_nameText != null)      m_nameText.text      = "-";
        if (m_scoreRankText != null) m_scoreRankText.text = "-";
        if (m_statText != null)      m_statText.text      = "-";
    }

    private string BuildStatText(CapabilityProfile stats, int shipCount)
    {
        // 1줄: Ships / HP / ATK
        string line1 = $"<sprite name=\"IconShips\"> {shipCount}  <sprite name=\"IconHp\"> {CommonUtility.FormatBigNumber(stats.health_power)}  <sprite name=\"IconAttack\"> {CommonUtility.FormatBigNumber(stats.attack_power)}";

        if (stats.aircraft_count <= 0)
            return line1;

        // 함재기 보유 시 2줄째에 추가
        string line2 = $"<sprite name=\"IconAircraft\"> {stats.aircraft_count}";
        return $"{line1}\n{line2}";
    }
}
