// PvP 패널 — 전투 진입/퇴각/결과 정산 + 랭킹 리스트 탭(UIPanelRank_Leaderboard)까지 구현
// 존/좌표 계산은 레거시의 ZoneStageConfig 기반이 아니라, 현재 탐사 그리드가 쓰는 ZoneConfig/ExplorationGridGenerator 기준으로 새로 짬
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelRank : UIPanelBase
{
    [SerializeField] private TMP_Text m_myScoreText;
    [SerializeField] private TMP_Text m_myRankText;
    [SerializeField] private TMP_Text m_seasonText;
    [SerializeField] private Button m_refreshButton;
    [SerializeField] private TMP_Text m_refreshButtonText;
    [SerializeField] private Transform m_opponentCardContainer; // PvpCards — 하위 PvpSelectCard를 그대로 파싱해서 씀

    private PvpSelectCard[] m_opponentCards;

    private int m_myScore;
    private int m_myRank;
    private int m_refreshRemain;
    private int m_seasonNumber;
    private string m_seasonEndTime;

    private string m_currentBattleToken;
    private bool m_isBattleInProgress;

    public override void InitializeUIPanel()
    {
        if (m_refreshButton != null)
            m_refreshButton.onClick.AddListener(OnRefreshClicked);
        if (m_opponentCardContainer != null)
            m_opponentCards = m_opponentCardContainer.GetComponentsInChildren<PvpSelectCard>(true);

        EventManager.Subscribe_RetreatPvp(OnRetreatPvp);
        EventManager.Subscribe_PvpBattleEnd(OnPvpBattleEnd);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_RetreatPvp(OnRetreatPvp);
        EventManager.Unsubscribe_PvpBattleEnd(OnPvpBattleEnd);
    }

    public override void OnShowUIPanel()
    {
        RequestPvpMyRank();
        RequestPvpList();
    }

    private void RequestPvpMyRank()
    {
        NetworkManager.Instance.PvpMyRank(new PvpMyRankRequest(), OnPvpMyRankResponse);
    }

    private void OnPvpMyRankResponse(ApiResponse<PvpMyRankResponse> response)
    {
        if (response == null || response.errorCode != 0 || response.data == null || response.data.myRankInfo == null)
        {
            ShowErrorMessage("내 정보를 불러올 수 없습니다.");
            return;
        }

        PvpRankInfo rankInfo = response.data.myRankInfo;
        m_myScore = rankInfo.pvpScore;
        m_myRank = rankInfo.pvpRank;
        m_refreshRemain = rankInfo.pvpListRefreshRemain;
        m_seasonNumber = rankInfo.seasonNumber;
        m_seasonEndTime = rankInfo.seasonEndTime;
        UpdateMyInfo();
    }

    private void RequestPvpList()
    {
        NetworkManager.Instance.PvpList(new PvpListRequest(), OnPvpListResponse);
    }

    private void OnPvpListResponse(ApiResponse<PvpListResponse> response)
    {
        if (response == null || response.errorCode != 0 || response.data == null)
        {
            ShowErrorMessage("상대 목록을 불러올 수 없습니다.");
            return;
        }
        SetOpponentList(response.data.opponents);
    }

    private void SetOpponentList(List<PvpOpponentInfo> opponents)
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

    private void UpdateMyInfo()
    {
        var loc = LocalizationManager.Instance;

        if (m_myScoreText != null) m_myScoreText.text = $"{m_myScore}";
        if (m_myRankText != null) m_myRankText.text = loc.Get("UITabRank_Rank") + $" {m_myRank}";

        if (m_seasonText != null)
        {
            bool hasSeason = m_seasonNumber > 0;
            m_seasonText.gameObject.SetActive(hasSeason);
            if (hasSeason == true)
            {
                string dStr = "";
                if (string.IsNullOrEmpty(m_seasonEndTime) == false &&
                    System.DateTime.TryParse(m_seasonEndTime, null, System.Globalization.DateTimeStyles.RoundtripKind, out System.DateTime endTime))
                {
                    int daysRemain = (int)(endTime - System.DateTime.UtcNow).TotalDays;
                    dStr = daysRemain > 0 ? $" (D-{daysRemain})" : " (D-0)";
                }
                m_seasonText.text = loc.Get("UITabRank_SeasonNumber", m_seasonNumber) + dStr;
            }
        }

        if (m_refreshButtonText != null)
        {
            int refreshMaxCount = DataManager.Instance.m_dataTableConfig.gameSettings.pvpListRefreshCount;
            m_refreshButtonText.text = loc.Get("UITabRank_refresh", m_refreshRemain, refreshMaxCount);
        }
    }

    private void OnRefreshClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_refreshRemain <= 0)
        {
            ShowErrorMessage("오늘 갱신 횟수를 모두 사용했습니다.");
            return;
        }

        var loc = LocalizationManager.Instance;
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message   = loc.Get("pvp_opponent_list") + "\n" + loc.Get("pvp_refresh_confirm", m_refreshRemain),
            onConfirm = ExecuteRefresh,
            onCancel  = () => { }
        });
    }

    private void ExecuteRefresh()
    {
        NetworkManager.Instance.PvpRefresh(new PvpRefreshRequest(), OnPvpRefreshResponse);
    }

    private void OnPvpRefreshResponse(ApiResponse<PvpRefreshResponse> response)
    {
        if (response == null || response.errorCode != 0 || response.data == null)
        {
            ShowErrorMessage("갱신에 실패했습니다.");
            return;
        }

        m_refreshRemain = response.data.refreshRemain;
        UpdateMyInfo();
        SetOpponentList(response.data.opponents);
    }

    private void OnAttackClicked(PvpOpponentInfo opponent)
    {
        if (m_isBattleInProgress == true) return;

        var loc = LocalizationManager.Instance;
        // TODO: 테스트 위해 레벨 제한 임시 주석처리 — 원복 필요
        /*
        int minCommanderLevel = DataManager.Instance.m_dataTableConfig.gameSettings.pvpMinCommanderLevel;
        int myCommanderLevel = DataManager.Instance.m_currentCommander.m_commanderInfo.commanderLevel;
        if (myCommanderLevel < minCommanderLevel)
        {
            // 레벨 부족 안내는 유저가 직접 확인해야 하는 차단 메시지 — ShowErrorMessage(자동 닫힘)와 달리 autoCloseSec 없이 표시
            UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
            {
                message   = loc.Get("UIPopupMessage_PvpCommanderLevelRequiredMessage", minCommanderLevel),
                onConfirm = () => { },
            });
            return;
        }
        */

        CapabilityProfile stats = CommonUtility.GetFleetCapabilityProfile(opponent.fleetInfo);
        int shipCount = (opponent.fleetInfo != null && opponent.fleetInfo.ships != null) ? opponent.fleetInfo.ships.Count : 0;

        var rows = new List<(string label, string value)>
        {
            ("UIFleet_PlacedShipCount", shipCount.ToString()),
            ("UIFleet_Stats_Health",    CommonUtility.FormatBigNumber(stats.health)),
            ("Simple_BeamAttack",       CommonUtility.FormatBigNumber(stats.beamAttack)),
            ("Simple_MissileAttack",    CommonUtility.FormatBigNumber(stats.missileAttack)),
        };
        if (stats.airCount > 0)
        {
            rows.Add(("Simple_AirAttack", CommonUtility.FormatBigNumber(stats.airAttack)));
            rows.Add(("Simple_AirCount", stats.airCount.ToString()));
        }

        string opponentName = Commander.GetDisplayName(opponent.commanderName, opponent.commanderId);
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message         = $"{opponentName}\n{loc.Get("pvp_opponent_info", opponent.pvpScore, opponent.rank)}",
            pvpOpponentRows = rows,
            onConfirm       = () => ExecuteAttack(opponent),
            onCancel        = () => { }
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
        if (response == null || response.errorCode != 0 || response.data == null)
        {
            m_isBattleInProgress = false;
            ShowErrorMessage("전투를 시작할 수 없습니다.");
            return;
        }

        EventManager.TriggerPvpBattleStart();
        UIManager.Instance.HideCurrentPanel();

        m_currentBattleToken = response.data.battleToken;
        FleetInfo opponentFleetInfo = response.data.opponentFleetInfo;

        ObjectManager.Instance.ChangeZone(1);
        ObjectManager.Instance.SetMyFleetPosition(GetPvpArenaPosition(), 0f);
        CameraController.Instance.SnapToTarget();

        ObjectManager.Instance.GetMyFleet().StartFleetWarpIn(onArrived: () =>
        {
            ObjectManager.Instance.StartPvpBattle(opponentFleetInfo);
        });
    }

    // 항상 존1의 그리드 시작 셀 — 레거시가 고정 워프하던 "zone 1, stage 1-1"의 현재 그리드 시스템 대응 좌표
    private Vector3 GetPvpArenaPosition()
    {
        ZoneConfig zoneConfig = DataManager.Instance.m_dataTableZone.GetZoneByZoneIndex(1);
        if (zoneConfig == null) return Vector3.zero;

        ExplorationGridData gridData = ExplorationGridGenerator.Generate(zoneConfig);
        return gridData.GetCell(gridData.startRow, gridData.startCol).worldPos;
    }

    private void OnRetreatPvp()
    {
        if (m_isBattleInProgress == false) return;

        var loc = LocalizationManager.Instance;
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message   = loc.Get("UITabPvp_RetreatConfirm"),
            onConfirm = () => ObjectManager.Instance.ForceEndBattle(false),
            onCancel  = () => { }
        });
    }

    private void OnPvpBattleEnd(bool isVictory)
    {
        ReportBattleResult(isVictory);
    }

    private void ReportBattleResult(bool isVictory)
    {
        var request = new PvpBattleResultRequest
        {
            battleToken = m_currentBattleToken,
            isVictory   = isVictory,
        };
        NetworkManager.Instance.PvpBattleResult(request, OnBattleResultResponse);
    }

    private void OnBattleResultResponse(ApiResponse<PvpBattleResultResponse> response)
    {
        if (response == null || response.errorCode != 0 || response.data == null)
        {
            ShowErrorMessage("전투 결과 처리 실패");
            ReturnToPrevious();
            return;
        }

        var loc = LocalizationManager.Instance;
        int scoreChange = response.data.scoreChange;
        int oldScore = m_myScore;
        int oldRank = m_myRank;
        bool isVictory = scoreChange >= 0;

        m_myScore = response.data.newScore;
        m_myRank = response.data.newRank;
        UpdateMyInfo();
        EventManager.TriggerPvpBattleResult(isVictory, scoreChange, response.data.newScore, response.data.newRank);

        string resultKey = isVictory ? "pvp_battle_result_win" : "pvp_battle_result_lose";
        string scoreStr = isVictory ? $"+{scoreChange}" : $"{scoreChange}";
        string scoreLine = loc.Get("pvp_battle_result_score", oldScore, response.data.newScore, scoreStr);
        string rankLine  = loc.Get("pvp_battle_result_rank", oldRank, response.data.newRank);

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message      = $"{loc.Get(resultKey)}\n{scoreLine}\n{rankLine}",
            onConfirm    = ReturnToPrevious,
            autoCloseSec = 5f,
        });
    }

    private void ReturnToPrevious()
    {
        m_isBattleInProgress = false;
        m_currentBattleToken = null;

        CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_my_fleet);

        int returnZoneNumber = ObjectManager.Instance.GetInitialZoneIndex();
        ObjectManager.Instance.ChangeZone(returnZoneNumber);
        ObjectManager.Instance.SetMyFleetPosition(ObjectManager.Instance.GetInitialGridStartCellPosition(), 0f);
        CameraController.Instance.SnapToTarget();

        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        myFleet.StartFleetWarpIn(onArrived: () =>
        {
            if (myFleet.IsFleetAlive() == false) myFleet.RebuildFleet(0.1f);
            else myFleet.RestoreDestroyedShips(0.1f);

            RequestPvpList();
            RequestPvpMyRank();
        });
    }

    private void ShowErrorMessage(string message)
    {
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig { message = message, autoCloseSec = 5f });
    }
}
