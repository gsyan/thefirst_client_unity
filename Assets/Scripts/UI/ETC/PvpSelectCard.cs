// PvP 상대 선택 카드 - 이름/랭크/점수/함대 스탯(최종 전투 스탯 요약)/공격 버튼 표시. 공격 버튼은 상세 팝업(공격 확인 겸용)을 엶
// 함대 스탯 행은 InfiniteScrollView(보이는 행만 생성/재활용) — 항목 수가 카드 높이를 넘으면 스크롤
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PvpSelectCard : MonoBehaviour
{
    [SerializeField] private Button m_attackButton;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private TMP_Text m_scoreRankText;
    [SerializeField] private InfiniteScrollView m_statsScrollView;
    [SerializeField] private UIStatRow m_statRowPrefab;

    private readonly List<(string label, string valueText)> m_statEntries = new List<(string label, string valueText)>();

    private PvpOpponentInfo m_opponentInfo;
    public PvpOpponentInfo OpponentInfo => m_opponentInfo;

    private void Awake()
    {
        if (m_statsScrollView != null)
            m_statsScrollView.onItemBind = OnStatItemBind;
    }

    public void InitializePvpSelectCard(PvpOpponentInfo opponentInfo, UnityEngine.Events.UnityAction onAttack)
    {
        m_opponentInfo = opponentInfo;

        m_attackButton.onClick.RemoveAllListeners();
        m_attackButton.onClick.AddListener(() => SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true));
        m_attackButton.onClick.AddListener(onAttack);

        // 전투 스폰과 같은 규칙의 최종 전투 스탯(강화 레벨 + 패시브 반영) — 상대 빌드 데이터는 계산에만 쓰고 표시하지 않음
        List<ShipCombatStats> shipStats = PvpOpponentStatRows.BuildShipStats(opponentInfo.fleetInfo, opponentInfo.passiveProgress, opponentInfo.archetypeUpgrades);
        ShipCombatStats total = FleetCombatStatCalculator.SumFleet(shipStats);

        if (m_nameText != null)      m_nameText.text      = Commander.GetDisplayName(opponentInfo.commanderName, opponentInfo.commanderId);
        if (m_scoreRankText != null) m_scoreRankText.text = LocalizationManager.Instance.Get("UITabRank_ScoreRank", opponentInfo.pvpScore, opponentInfo.rank);

        BuildStatEntries(PvpOpponentStatRows.BuildSummaryRows(total, shipStats.Count));
        RefreshStatsScrollView();
    }

    public void SetEmpty()
    {
        m_opponentInfo = null;
        m_attackButton.onClick.RemoveAllListeners();
        if (m_nameText != null)      m_nameText.text      = "-";
        if (m_scoreRankText != null) m_scoreRankText.text = "-";
        m_statEntries.Clear();
        RefreshStatsScrollView();
    }

    // 라벨은 여기서 로컬라이즈해 둠(행 바인딩 시 재조회 없음)
    private void BuildStatEntries(List<(string labelKey, string valueText)> rows)
    {
        LocalizationManager loc = LocalizationManager.Instance;
        m_statEntries.Clear();
        for (int i = 0; i < rows.Count; i++)
            m_statEntries.Add((loc.Get(rows[i].labelKey), rows[i].valueText));
    }

    private void RefreshStatsScrollView()
    {
        if (m_statsScrollView == null || m_statRowPrefab == null) return;
        // 카드가 비활성 상태에서 먼저 채워지면 Awake 전일 수 있어 바인딩을 여기서도 보장
        m_statsScrollView.onItemBind = OnStatItemBind;
        m_statsScrollView.Initialize(m_statEntries.Count, m_statRowPrefab.gameObject);
    }

    private void OnStatItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_statEntries.Count) return;

        UIStatRow row = rowObject.GetComponent<UIStatRow>();
        if (row == null) return;

        row.SetValueOnly(m_statEntries[dataIndex].label, m_statEntries[dataIndex].valueText);
    }
}
