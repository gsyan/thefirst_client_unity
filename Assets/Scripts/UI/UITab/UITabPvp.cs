// PvP 탭 - 상대 목록, 전투 시작/결과, 랭킹 보드(InfiniteScrollView) 관리
using UnityEngine;

public class UITabPvp : UITabBase
{
    [Header("PvP Sub Tabs")]
    [SerializeField] private TabSystem m_innerTabSystem;
    [SerializeField] private UITabPvp_MyInfo m_tabMyInfo;
    [SerializeField] private UITabPvp_Rank m_tabRank;

    [Header("PvP Warp")]
    [SerializeField] private DataTableZone m_datatableZone;

    private SpaceFleet m_myFleet;
    private Character m_myCharacter;
    private string m_currentBattleToken;

    public override void InitializeUITab()
    {
        m_myCharacter = DataManager.Instance.m_currentCharacter;
        if (m_myCharacter == null || m_myCharacter.GetOwnedFleet() == null) return;
        m_myFleet = m_myCharacter.GetOwnedFleet();

        m_innerTabSystem.InitializeTabBases();
        m_tabMyInfo.onAttackClicked = OnAttackClicked;

        EventManager.Subscribe_MyFleetDestroyed(OnMyFleetWiped);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_MyFleetDestroyed(OnMyFleetWiped);
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        SetOtherTabsVisible(false, includeSelf: true);

        m_innerTabSystem.SwitchToTab(0);
        RequestPvpMyRank();
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
        SetOtherTabsVisible(true, includeSelf: true);
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
        CapabilityProfile stats = CommonUtility.GetFleetCapabilityProfile(opponent.fleetInfo);
        int shipCount = (opponent.fleetInfo != null && opponent.fleetInfo.ships != null) ? opponent.fleetInfo.ships.Count : 0;

        string title = opponent.characterName;
        string message = LocalizationManager.Instance.Get("pvp_opponent_info", new object[] { opponent.pvpScore, opponent.rank });

        var sb = new System.Text.StringBuilder();
        sb.Append($"{CommonUtility.Sprite("spaceship")} {shipCount}");
        sb.Append("\n");
        sb.Append($"{CommonUtility.Sprite("techno-heart")} {CommonUtility.FormatBigNumber(stats.health)}");
        sb.Append("\n");
        sb.Append($"{CommonUtility.Sprite("bubbling-beam")} {CommonUtility.FormatBigNumber(stats.attack)}");
        if (stats.airCount > 0)
        {
            sb.Append("\n");
            sb.Append($"\n{CommonUtility.Sprite("jet-fighter")} {stats.airCount}");
        }

        UIManager.Instance.ShowConfirmPopup(title, message, sb.ToString(), null, 0,
            () => ExecuteAttack(opponent));
    }

    private void ExecuteAttack(PvpOpponentInfo opponent)
    {
        var request = new PvpBattleStartRequest { opponentCharacterId = opponent.characterId };
        NetworkManager.Instance.PvpBattleStart(request, OnBattleStartResponse);
    }

    private void OnBattleStartResponse(ApiResponse<PvpBattleStartResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            ShowErrorMessage("전투를 시작할 수 없습니다.");
            return;
        }

        m_currentBattleToken = response.data.battleToken;
        FleetInfo opponentFleetInfo = response.data.opponentFleetInfo;

        ZoneStageConfig pvpZoneStage = m_datatableZone.GetZoneStage(0);
        if (pvpZoneStage != null)
            ObjectManager.Instance.SetMyFleetPosition(m_datatableZone.ResolveFleetWorldPosition(pvpZoneStage), pvpZoneStage.fleetRotationY);

        m_myFleet.StartFleetWarpIn(onArrived: () =>
        {
            UIManager.Instance.ShowPanel("UIPanelCameraView");

            ObjectManager.Instance.StartPvpBattle(opponentFleetInfo, (isVictory) =>
            {
                ReportBattleResult(isVictory);
            });
        });
    }

    private void OnMyFleetWiped()
    {
        // ObjectManager.ForceEndBattle(false)가 콜백을 호출함
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
            ReturnFromBattle();
            return;
        }

        int scoreChange = response.data.scoreChange;
        int oldRank = m_tabMyInfo.GetCurrentRank();
        bool isVictory = scoreChange >= 0;

        m_tabMyInfo.UpdateScore(response.data.newScore, response.data.newRank);
        EventManager.TriggerPvpBattleResult(isVictory, scoreChange, response.data.newScore, response.data.newRank);

        string titleKey = isVictory ? "pvp_battle_result_win" : "pvp_battle_result_lose";
        string scoreStr = isVictory ? $"+{scoreChange}" : $"{scoreChange}";
        string title = LocalizationManager.Instance.Get(titleKey);
        string scoreLine = LocalizationManager.Instance.Get("pvp_battle_result_score", scoreStr, response.data.newScore);
        string rankLine = LocalizationManager.Instance.Get("pvp_battle_result_rank", oldRank, response.data.newRank);
        UIManager.Instance.ShowPopupAlert(title, $"{scoreLine}\n{rankLine}", ReturnFromBattle);
    }

    private void ReturnFromBattle()
    {
        m_currentBattleToken = null;

        UIManager.Instance.HidePanel("UIPanelCameraView");
        CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_my_fleet);

        ZoneStageConfig returnZoneStage = m_datatableZone.GetZoneStage(0);
        if (returnZoneStage == null) return;
        ObjectManager.Instance.SetMyFleetPosition(m_datatableZone.ResolveFleetWorldPosition(returnZoneStage), returnZoneStage.fleetRotationY);
        m_myFleet.StartFleetWarpIn(onArrived: () =>
        {
            if (m_myFleet.IsFleetAlive() == false)
                m_myFleet.RebuildFleet(0.1f);
            else
                m_myFleet.RestoreDestroyedShips(0.1f);

            m_tabMyInfo.RequestPvpList();
            RequestPvpMyRank();
        });
    }
}
