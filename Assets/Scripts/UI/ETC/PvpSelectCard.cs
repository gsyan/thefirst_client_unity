// PvP 상대 선택 카드 - 이름/랭크/점수/함대 스탯/공격 버튼 표시
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PvpSelectCard : MonoBehaviour
{
    [SerializeField] private Button m_attackButton;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private TMP_Text m_scoreRankText;
    [SerializeField] private Transform m_statsContainer;
    [SerializeField] private UIStatRow m_statRowPrefab; // 컨테이너에 미리 배치된 것보다 더 필요할 때 추가 생성용

    private readonly List<UIStatRow> m_statRows = new();

    private PvpOpponentInfo m_opponentInfo;
    public PvpOpponentInfo OpponentInfo => m_opponentInfo;

    private void Awake()
    {
        if (m_statsContainer != null)
            m_statRows.AddRange(m_statsContainer.GetComponentsInChildren<UIStatRow>(true));
    }

    public void InitializePvpSelectCard(PvpOpponentInfo opponentInfo, UnityEngine.Events.UnityAction onAttack)
    {
        m_opponentInfo = opponentInfo;

        m_attackButton.onClick.RemoveAllListeners();
        m_attackButton.onClick.AddListener(() => SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true));
        m_attackButton.onClick.AddListener(onAttack);

        CapabilityProfile stats = CommonUtility.GetFleetCapabilityProfile(opponentInfo.fleetInfo);
        int shipCount = (opponentInfo.fleetInfo != null && opponentInfo.fleetInfo.ships != null)
            ? opponentInfo.fleetInfo.ships.Count : 0;

        if (m_nameText != null)      m_nameText.text      = Commander.GetDisplayName(opponentInfo.commanderName, opponentInfo.commanderId);
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
        var loc = LocalizationManager.Instance;
        var rows = new List<(string label, string valueText)>
        {
            (loc.Get("fleet_ship_count"), shipCount.ToString()),
            (loc.Get("UIFleet_Stats_Health"), CommonUtility.FormatBigNumber(stats.health)),
            (loc.Get("Simple_Attack"), CommonUtility.FormatBigNumber(stats.attack)),
        };
        if (stats.airCount > 0)
            rows.Add((loc.Get("Simple_AirCount"), stats.airCount.ToString()));

        EnsureStatRowCount(rows.Count);
        for (int i = 0; i < m_statRows.Count; i++)
        {
            if (i < rows.Count) m_statRows[i].SetValueOnly(rows[i].label, rows[i].valueText);
            else m_statRows[i].Hide();
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_statsContainer as RectTransform);
    }

    private void EnsureStatRowCount(int neededCount)
    {
        if (m_statsContainer == null || m_statRowPrefab == null) return;
        while (m_statRows.Count < neededCount)
            m_statRows.Add(Instantiate(m_statRowPrefab, m_statsContainer));
    }

    private void HideAllStats()
    {
        for (int i = 0; i < m_statRows.Count; i++)
            m_statRows[i].Hide();
    }
}
