// PvP 탭 - 상대 목록, 전투 시작/결과, 랭킹 보드(InfiniteScrollView) 관리
using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UITabPvp : UITabBase
{
    [Header("PvP UI Components")]
    [SerializeField] private TMP_Text m_myInfoScoreText;
    [SerializeField] private TMP_Text m_myInfoRankText;
    [SerializeField] private TMP_Text m_myInfoSeasonText;
    [SerializeField] private Button m_refreshButton;
    [SerializeField] private TMP_Text m_refreshButtonText;
    [SerializeField] private PvpSelectCard[] m_opponentCards; // 고정 3슬롯

    [Header("PvP Ranking Board")]
    [SerializeField] private Button m_pvpRankListButton;

    [Header("PvP Warp")]
    [SerializeField] private DataTableZone m_datatableZone;

    private SpaceFleet m_myFleet;
    private Character m_myCharacter;

    private int m_myScore;
    private int m_myRank;
    private int m_refreshRemain;
    private string m_currentBattleToken;
    private string m_seasonName;
    private string m_seasonEndTime;

    public override void InitializeUITab()
    {
        m_myCharacter = DataManager.Instance.m_currentCharacter;
        if (m_myCharacter == null || m_myCharacter.GetOwnedFleet() == null) return;
        m_myFleet = m_myCharacter.GetOwnedFleet();

        m_refreshButton.onClick.AddListener(OnRefreshClicked);
        if (m_pvpRankListButton != null)
            m_pvpRankListButton.onClick.AddListener(OnRankListButtonClicked);

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
        
        RequestPvpList();
        RequestPvpMyRank();
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
        SetOtherTabsVisible(true, includeSelf: true);
    }

    private void OnRankListButtonClicked()
    {
        UIManager.Instance.ShowRankingPopup();
    }

    // 상대 목록 요청
    private void RequestPvpList()
    {
        var request = new PvpListRequest();
        NetworkManager.Instance.PvpList(request, OnPvpListResponse);
    }

    // 내 랭크 정보 요청
    private void RequestPvpMyRank()
    {
        var request = new PvpMyRankRequest();
        NetworkManager.Instance.PvpMyRank(request, OnPvpMyRankResponse);
    }

    private void OnPvpListResponse(ApiResponse<PvpListResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            ShowErrorMessage("상대 목록을 불러올 수 없습니다.");
            return;
        }

        PopulateOpponentList(response.data.opponents);
    }

    private void OnPvpMyRankResponse(ApiResponse<PvpMyRankResponse> response)
    {
        if (response == null || response.errorCode != 0 || response.data?.myRankInfo == null) return;

        m_myScore = response.data.myRankInfo.pvpScore;
        m_myRank = response.data.myRankInfo.pvpRank;
        m_refreshRemain = response.data.myRankInfo.pvpListRefreshRemain;
        m_seasonName = response.data.myRankInfo.seasonName;
        m_seasonEndTime = response.data.myRankInfo.seasonEndTime;
        UpdateMyInfo();
    }

    private void UpdateMyInfo()
    {
        string rankStr = m_myRank > 0 ? $"#{m_myRank}" : "#-";
        m_myInfoScoreText.text = $"{m_myScore}";
        m_myInfoRankText.text = rankStr;

        if (m_myInfoSeasonText != null)
        {
            bool hasSeason = string.IsNullOrEmpty(m_seasonName) == false;
            m_myInfoSeasonText.gameObject.SetActive(hasSeason);
            if (hasSeason == true)
            {
                string dStr = "";
                if (string.IsNullOrEmpty(m_seasonEndTime) == false &&
                    System.DateTime.TryParse(m_seasonEndTime, null, System.Globalization.DateTimeStyles.RoundtripKind, out System.DateTime endTime))
                {
                    int daysRemain = (int)(endTime - System.DateTime.UtcNow).TotalDays;
                    dStr = daysRemain > 0 ? $" (D-{daysRemain})" : " (D-0)";
                }
                m_myInfoSeasonText.text = m_seasonName + dStr;
            }
        }

        if (m_refreshButtonText != null)
            m_refreshButtonText.text = LocalizationManager.Instance.Get("UITabPvp_refresh", new object[] { m_refreshRemain, 5 });
    }

    private void PopulateOpponentList(List<PvpOpponentInfo> opponents)
    {
        if (m_opponentCards == null) return;

        for (int i = 0; i < m_opponentCards.Length; i++)
        {
            if (m_opponentCards[i] == null) continue;

            if (opponents != null && i < opponents.Count)
            {
                PvpOpponentInfo opponent = opponents[i];
                m_opponentCards[i].InitializePvpSelectCard(opponent, () => OnAttackClicked(opponent));
            }
            else
            {
                m_opponentCards[i].SetEmpty();
            }
        }
    }

    // 갱신 버튼
    private void OnRefreshClicked()
    {
        if (m_refreshRemain <= 0)
        {
            ShowErrorMessage("오늘 갱신 횟수를 모두 사용했습니다.");
            return;
        }

        string title = LocalizationManager.Instance.Get("pvp_opponent_list");
        string message = LocalizationManager.Instance.Get("pvp_refresh_confirm", m_refreshRemain);
        UIManager.Instance.ShowConfirmPopup(title, message, null, null, 0,
            onConfirm: ExecuteRefresh);
    }

    private void ExecuteRefresh()
    {
        var request = new PvpRefreshRequest();
        NetworkManager.Instance.PvpRefresh(request, OnPvpRefreshResponse);
    }

    private void OnPvpRefreshResponse(ApiResponse<PvpRefreshResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            ShowErrorMessage("갱신에 실패했습니다.");
            return;
        }

        m_refreshRemain = response.data.refreshRemain;
        UpdateMyInfo();
        PopulateOpponentList(response.data.opponents);
    }

    // 공격 버튼 - 상대 함대 정보 팝업 표시
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

    // 서버에 전투 시작 요청
    private void ExecuteAttack(PvpOpponentInfo opponent)
    {
        var request = new PvpBattleStartRequest { opponentCharacterId = opponent.characterId };
        NetworkManager.Instance.PvpBattleStart(request, OnBattleStartResponse);
    }

    // 서버 응답 후 워프 연출 → 전투 시작
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

    // 플레이어 함대 전멸
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
        int oldRank = m_myRank;
        m_myScore = response.data.newScore;
        m_myRank = response.data.newRank;
        bool isVictory = scoreChange >= 0;

        UpdateMyInfo();
        EventManager.TriggerPvpBattleResult(isVictory, scoreChange, m_myScore, m_myRank);

        string titleKey = isVictory ? "pvp_battle_result_win" : "pvp_battle_result_lose";
        string scoreStr = isVictory ? $"+{scoreChange}" : $"{scoreChange}";
        string title = LocalizationManager.Instance.Get(titleKey);
        string scoreLine = LocalizationManager.Instance.Get("pvp_battle_result_score", scoreStr, m_myScore);
        string rankLine = LocalizationManager.Instance.Get("pvp_battle_result_rank", oldRank, m_myRank);
        UIManager.Instance.ShowPopupAlert(title, $"{scoreLine}\n{rankLine}", ReturnFromBattle);
    }

    // 전투 종료 후 워프 복귀
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

            RequestPvpList();
            RequestPvpMyRank();
        });
    }
}
