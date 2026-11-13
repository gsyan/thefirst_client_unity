// 상대 지휘관 상세 정보 팝업 — PVP 선택 카드 클릭(공격 확인 겸용)/랭킹 항목 클릭으로 열림. 서버 상세 조회(PvpOpponentDetail)의 실시간 값으로 표시
// 함대 스탯은 전투 스폰과 같은 규칙(강화 레벨 + 패시브 배율)으로 계산한 최종값만 보여주고, 강화 레벨/패시브 단계/진형/전술은 표시하지 않음
// 좌: 지휘관 정보 + 함대 요약(라벨/값 행), 우: 함선별 최종 스탯(고정 칸) — 둘 다 InfiniteScrollView(보이는 행만 생성/재활용)
// onConfirm을 넘기면 취소/확인(공격) 버튼, 없으면 닫기 버튼 하나
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupOpponentDetail : UIPopupBase
{
    [SerializeField] private TMP_Text m_titleText; // 지휘관 이름

    [Header("좌 — 지휘관 정보(레벨/점수/순위/전적) + 함대 요약")]
    [SerializeField] private InfiniteScrollView m_infoScrollView;
    [SerializeField] private UIStatRow m_infoRowPrefab;

    [Header("우 — 함선별 최종 스탯")]
    [SerializeField] private InfiniteScrollView m_shipScrollView;
    [SerializeField] private UIOpponentShipRow m_shipRowPrefab;

    [SerializeField] private Button m_closeButton;        // 닫기(확인 모드에서는 취소)
    [SerializeField] private TMP_Text m_closeButtonText;
    [SerializeField] private Button m_confirmButton;      // 공격 확인 — onConfirm이 있을 때만 표시
    [SerializeField] private TMP_Text m_confirmButtonText;

    private readonly List<(string label, string valueText)> m_infoEntries = new List<(string label, string valueText)>();
    private readonly List<(string shipName, List<(string label, string valueText)> cells)> m_shipEntries = new List<(string shipName, List<(string label, string valueText)> cells)>();
    private System.Action m_onClose;
    private System.Action m_onConfirm;

    protected override void Awake()
    {
        base.Awake();
        if (m_closeButton != null) m_closeButton.onClick.AddListener(OnCloseClicked);
        if (m_confirmButton != null) m_confirmButton.onClick.AddListener(OnConfirmClicked);
        if (m_infoScrollView != null) m_infoScrollView.onItemBind = OnInfoItemBind;
        if (m_shipScrollView != null) m_shipScrollView.onItemBind = OnShipItemBind;
        if (m_confirmButtonText != null) CommonUtility.SetUILocText(m_confirmButtonText, "UI_Confirm");
    }

    // onConfirm: 공격 확인용(확인 클릭 시 호출) — null이면 정보 열람 전용(닫기 버튼만)
    public void ShowPopupOpponentDetail(PvpOpponentDetailResponse detail, System.Action onClose, System.Action onConfirm = null)
    {
        m_onClose = onClose;
        m_onConfirm = onConfirm;

        bool isConfirmMode = onConfirm != null;
        if (m_confirmButton != null) m_confirmButton.gameObject.SetActive(isConfirmMode);
        if (m_closeButtonText != null) CommonUtility.SetUILocText(m_closeButtonText, isConfirmMode == true ? "UI_Cancel" : "UI_Close");

        if (m_titleText != null)
            m_titleText.text = Commander.GetDisplayName(detail.commanderName, detail.commanderId);

        List<ShipCombatStats> shipStats = PvpOpponentStatRows.BuildShipStats(detail.fleetInfo, detail.passiveProgress, detail.archetypeUpgrades);
        ShipCombatStats total = FleetCombatStatCalculator.SumFleet(shipStats);
        BuildInfoEntries(detail, total, shipStats.Count);
        BuildShipEntries(shipStats);

        // 스크롤뷰는 viewport 크기로 풀 개수를 정하므로 팝업이 활성화된 뒤 초기화
        ShowPopup();

        if (m_infoScrollView != null && m_infoRowPrefab != null)
            m_infoScrollView.Initialize(m_infoEntries.Count, m_infoRowPrefab.gameObject);
        if (m_shipScrollView != null && m_shipRowPrefab != null)
            m_shipScrollView.Initialize(m_shipEntries.Count, m_shipRowPrefab.gameObject);
    }

    // 지휘관 정보 4행 + 함대 요약 행을 한 목록으로 — 라벨은 여기서 로컬라이즈해 둠(행 바인딩 시 재조회 없음)
    private void BuildInfoEntries(PvpOpponentDetailResponse detail, ShipCombatStats total, int shipCount)
    {
        LocalizationManager loc = LocalizationManager.Instance;
        m_infoEntries.Clear();

        string rankText = detail.rank > 0 ? $"#{detail.rank}" : "-";
        m_infoEntries.Add((loc.Get("UIOpponentDetail_Level"), detail.commanderLevel.ToString()));
        m_infoEntries.Add((loc.Get("UIOpponentDetail_Score"), BuildScoreText(detail.pvpScore)));
        m_infoEntries.Add((loc.Get("UIOpponentDetail_Rank"), rankText));
        m_infoEntries.Add((loc.Get("UIOpponentDetail_Record"), BuildRecordText(detail.wins, detail.losses)));

        List<(string labelKey, string valueText)> summaryRows = PvpOpponentStatRows.BuildSummaryRows(total, shipCount);
        for (int i = 0; i < summaryRows.Count; i++)
            m_infoEntries.Add((loc.Get(summaryRows[i].labelKey), summaryRows[i].valueText));
    }

    private void BuildShipEntries(List<ShipCombatStats> shipStats)
    {
        m_shipEntries.Clear();
        for (int i = 0; i < shipStats.Count; i++)
        {
            ShipCombatStats ship = shipStats[i];
            string shipName = CommonUtility.BuildHullDisplayName(ship.hullSubType);
            List<(string label, string valueText)> cells = PvpOpponentStatRows.BuildShipCells(ship);
            m_shipEntries.Add((shipName, cells));
        }
    }

    private void OnInfoItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_infoEntries.Count) return;

        UIStatRow row = rowObject.GetComponent<UIStatRow>();
        if (row == null) return;

        row.SetValueOnly(m_infoEntries[dataIndex].label, m_infoEntries[dataIndex].valueText);
    }

    private void OnShipItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_shipEntries.Count) return;

        UIOpponentShipRow row = rowObject.GetComponent<UIOpponentShipRow>();
        if (row == null) return;

        row.Setup(m_shipEntries[dataIndex].shipName, m_shipEntries[dataIndex].cells);
    }

    // "1,250 (Gold)" — 티어 데이터가 없으면 점수만, PVP 기록이 없으면(점수 0) "-"
    private static string BuildScoreText(int pvpScore)
    {
        if (pvpScore <= 0) return "-";

        DataTablePvpSeason pvpSeason = DataManager.Instance.m_dataTablePvpSeason;
        PvpSeasonTierEntry tier = pvpSeason != null ? pvpSeason.GetTierByScore(pvpScore) : null;
        string scoreText = CommonUtility.FormatNumber(pvpScore);
        if (tier == null) return scoreText;
        return $"{scoreText} ({tier.tierName})";
    }

    // "12승 8패 (60%)" — 전적이 없으면 0%
    private static string BuildRecordText(int wins, int losses)
    {
        int totalGames = wins + losses;
        int winRatePercent = totalGames > 0 ? Mathf.RoundToInt(wins * 100f / totalGames) : 0;
        return LocalizationManager.Instance.Get("UIOpponentDetail_RecordValue", wins, losses, winRatePercent);
    }

    private void OnCloseClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_onClose != null) m_onClose();
    }

    // 팝업을 먼저 닫고 공격 진행 — 콜백 안에서 다른 팝업/화면 전환이 일어나도 이 팝업이 남지 않게
    private void OnConfirmClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        System.Action onConfirm = m_onConfirm;
        if (m_onClose != null) m_onClose();
        if (onConfirm != null) onConfirm();
    }
}
