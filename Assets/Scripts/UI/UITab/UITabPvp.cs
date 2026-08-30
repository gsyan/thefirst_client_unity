// PvP 탭 - 상대 목록, 전투 시작/결과, 랭킹 보드(InfiniteScrollView) 관리
// 함선 시스템 대격변으로 전체 주석처리 — 구식 ShipInfo/FleetInfo 기반 PvP 로직, 마이그레이션 전까지 비활성화(삭제 아님)
#if false
using UnityEngine;

public class UITabPvp : UITabBase
{
    [Header("PvP Sub Tabs")]
    [SerializeField] private TabSystem m_innerTabSystem;
    [SerializeField] private UITabPvp_MyInfo m_tabMyInfo;
    [SerializeField] private UITabPvp_Rank m_tabRank;

    [Header("PvP Warp")]
    [SerializeField] private DataTableZone m_datatableZone;

    private string m_currentBattleToken;
    private bool m_isBattleInProgress;

    public override void InitializeUITab()
    {
        if (DataManager.Instance.m_currentCommander == null) return;

        m_innerTabSystem.InitializeTabBases();
        m_tabMyInfo.onAttackClicked = OnAttackClicked;

        EventManager.Subscribe_MyFleetDestroyed(OnMyFleetWiped);
        EventManager.Subscribe_RetreatPvp(OnRetreatPvp);
        EventManager.Subscribe_PvpBattleEnd(OnPvpBattleEnd);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_MyFleetDestroyed(OnMyFleetWiped);
        EventManager.Unsubscribe_RetreatPvp(OnRetreatPvp);
        EventManager.Unsubscribe_PvpBattleEnd(OnPvpBattleEnd);
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        HideTabButtons();

        m_innerTabSystem.SwitchToTab(0);
        RequestPvpMyRank();
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
        if (m_isBattleInProgress == false)
            RefreshTabButtons();
    }

    private void RequestPvpMyRank()
    {
        var request = new PvpMyRankRequest();
        NetworkManager.Instance.PvpMyRank(request, OnPvpMyRankResponse);
    }

    private void OnPvpMyRankResponse(ApiResponse<PvpMyRankResponse> response)
    {
        if (response == null || response.errorCode != 0) return;
        if (response.data == null || response.data.myRankInfo == null) return;
        m_tabMyInfo.SetMyRankInfo(response.data.myRankInfo);
    }

    private void OnAttackClicked(PvpOpponentInfo opponent)
    {
        if (m_isBattleInProgress == true) return;

        int minCommanderLevel = DataManager.Instance.m_dataTableConfig.gameSettings.pvpMinCommanderLevel;
        int myCommanderLevel  = DataManager.Instance.m_currentCommander.m_commanderInfo.commanderLevel;
        if (myCommanderLevel < minCommanderLevel)
        {
            var loc = LocalizationManager.Instance;
            UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
            {
                title     = loc.Get("UIPopupMessage_PvpTechCommanderRequiredTitle"),
                message   = loc.Get("UIPopupMessage_PvpCommanderLevelRequiredMessage", minCommanderLevel),
                onConfirm = () => { },
            });
            return;
        }
        CapabilityProfile stats = CommonUtility.GetFleetCapabilityProfile(opponent.fleetInfo);
        int shipCount = (opponent.fleetInfo != null && opponent.fleetInfo.ships != null) ? opponent.fleetInfo.ships.Count : 0;

        string title = Commander.GetDisplayName(opponent.commanderName, opponent.commanderId);
        string message = LocalizationManager.Instance.Get("pvp_opponent_info", new object[] { opponent.pvpScore, opponent.rank });

        var rows = new System.Collections.Generic.List<(string label, string value)>
        {
            ("UIFleet_PlacedShipCount", shipCount.ToString()),
            ("UIFleet_Stats_Health",    CommonUtility.FormatBigNumber(stats.health)),
            ("UIFleet_Stats_BeamDps",   CommonUtility.FormatBigNumber(stats.attack)),
        };
        if (stats.airCount > 0)
            rows.Add(("UIPopupConfirm_AircraftCount", stats.airCount.ToString()));

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title      = title,
            message    = message,
            pvpOpponentRows = rows,
            onConfirm  = () => ExecuteAttack(opponent),
            onCancel   = () => { }
        });
    }

    private void ExecuteAttack(PvpOpponentInfo opponent)
    {
        m_isBattleInProgress = true;
        var request = new PvpBattleStartRequest { opponentCommanderId = opponent.commanderId };
        NetworkManager.Instance.PvpBattleStart(request, OnBattleStartResponse);
    }

    private void OnBattleStartResponse(ApiResponse<PvpBattleStartResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            m_isBattleInProgress = false;
            ShowErrorMessage("전투를 시작할 수 없습니다.");
            return;
        }

        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet != null && myFleet.m_fleetState == EUnitState.BattleExploration)
        {
            ObjectManager.Instance.StopEnemySpawning();
            ObjectManager.Instance.OrderAllAircraftReturn();
            ObjectManager.Instance.CleanupAllProjectiles();
            ObjectManager.Instance.RemoveAllEnemyFleets();
        }
        EventManager.TriggerPvpBattleStart();

        m_tabSystemParent.SwitchToTab(-1);
        ObjectManager.Instance.ChangeZone(1);

        m_currentBattleToken = response.data.battleToken;
        FleetInfo opponentFleetInfo = response.data.opponentFleetInfo;

        ZoneStageConfig pvpZoneStage = m_datatableZone.GetZoneStageByName("1-1");
        if (pvpZoneStage != null)
            ObjectManager.Instance.SetMyFleetPosition(m_datatableZone.ResolveFleetWorldPosition(pvpZoneStage), pvpZoneStage.fleetRotationY);
        CameraController.Instance.SnapToTarget();

        ObjectManager.Instance.GetMyFleet().StartFleetWarpIn(onArrived: () =>
        {
            ObjectManager.Instance.StartPvpBattle(opponentFleetInfo);
        });
    }

    private void OnRetreatPvp()
    {
        if (m_isBattleInProgress == false) return;
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title     = LocalizationManager.Instance.Get("UITabPvp_RetreatTitle"),
            message   = LocalizationManager.Instance.Get("UITabPvp_RetreatConfirm"),
            onConfirm = () => ObjectManager.Instance.ForceEndBattle(false),
            onCancel  = () => { }
        });
    }

    private void OnPvpBattleEnd(bool isVictory)
    {
        ReportBattleResult(isVictory);
    }

    private void OnMyFleetWiped()
    {
        // ObjectManager.ForceEndBattle(false) → EventManager.TriggerPvpBattleEnd 로 처리됨
    }

    private void ReportBattleResult(bool isVictory)
    {
        var request = new PvpBattleResultRequest
        {
            battleToken = m_currentBattleToken,
            isVictory = isVictory
        };
        NetworkManager.Instance.PvpBattleResult(request, OnBattleResultResponse);
    }

    private void OnBattleResultResponse(ApiResponse<PvpBattleResultResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            ShowErrorMessage("전투 결과 처리 실패");
            ReturnToPrevious();
            return;
        }

        int scoreChange = response.data.scoreChange;
        int oldScore = m_tabMyInfo.GetCurrentScore();
        int oldRank = m_tabMyInfo.GetCurrentRank();
        bool isVictory = scoreChange >= 0;

        m_tabMyInfo.UpdateScore(response.data.newScore, response.data.newRank);
        EventManager.TriggerPvpBattleResult(isVictory, scoreChange, response.data.newScore, response.data.newRank);

        string titleKey = isVictory ? "pvp_battle_result_win" : "pvp_battle_result_lose";
        string scoreStr = isVictory ? $"+{scoreChange}" : $"{scoreChange}";
        string title = LocalizationManager.Instance.Get(titleKey);
        string scoreLine = LocalizationManager.Instance.Get("pvp_battle_result_score", oldScore, response.data.newScore, scoreStr);
        string rankLine = LocalizationManager.Instance.Get("pvp_battle_result_rank", oldRank, response.data.newRank);
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title     = title,
            message   = $"{scoreLine}\n{rankLine}",
            onConfirm = ReturnToPrevious,
            autoCloseSec = 5.0f
        });
    }

    private void ReturnToPrevious()
    {
        m_isBattleInProgress = false;
        m_currentBattleToken = null;
        RefreshTabButtons();

        UIManager.Instance.HidePanel("UIPanelBattle");
        CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_my_fleet);

        ZoneStageConfig returnZoneStage = null;
        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander != null && commander.m_commanderInfo != null && commander.m_commanderInfo.clearedZones != null && commander.m_commanderInfo.clearedZones.Count > 0)
            returnZoneStage = m_datatableZone.GetZoneStageByName(commander.m_commanderInfo.clearedZones[^1]);
        if (returnZoneStage == null)
            returnZoneStage = m_datatableZone.GetZoneFirstStage(1);
        if (returnZoneStage == null)
        {
            Debug.LogError("[UITabPvp] zone1 fallback 스테이지도 없음, ReturnFromBattle 중단");
            return;
        }

        int zoneGroup = 1;
        int dashIdx = returnZoneStage.zoneName.IndexOf('-');
        if (dashIdx > 0)
            int.TryParse(returnZoneStage.zoneName[..dashIdx], out zoneGroup);
        if (zoneGroup <= 0)
            zoneGroup = 1;

        ObjectManager.Instance.ChangeZone(zoneGroup);
        ObjectManager.Instance.SetMyFleetPosition(m_datatableZone.ResolveFleetWorldPosition(returnZoneStage), returnZoneStage.fleetRotationY);
        CameraController.Instance.SnapToTarget();
        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        myFleet.StartFleetWarpIn(onArrived: () =>
        {
            if (myFleet.IsFleetAlive() == false)
                myFleet.RebuildFleet(0.1f);
            else
                myFleet.RestoreDestroyedShips(0.1f);

            m_tabMyInfo.RequestPvpList();
            RequestPvpMyRank();
        });
    }
}
#endif

