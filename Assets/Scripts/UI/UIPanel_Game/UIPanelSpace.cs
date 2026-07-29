// 우주 공간 UI 패널 — 잔존 TabSystem(tab_ship/tab_pvp/tab_calandar 등 미전환 레거시) 초기화, 3D 모듈 선택 자동 전환.
// 진입 버튼(COMMANDER/FLEET/SETTINGS/RANK/EXPLORATION)은 UIManager의 오버레이 패널 카운트가 0일 때(기본 상태)만
// 노출됨(OnOverlayPanelActiveChanged). COMMANDER/SETTINGS/EXPLORATION/FLEET 등은 이제 전부 독립 UIPanelBase 프리팹이라
// 이 스크립트가 그 참조를 들고 있지 않음 — 각자가 UIManager에 스스로 등록되고, 서로 UIManager를 통해서만 상호작용함
using UnityEngine;

public class UIPanelSpace : UIPanelBase
{
    [Header("Tab System")]
    public TabSystem m_tabSystem;

    [Header("진입 버튼 그룹 (기본 상태에서만 노출)")]
    [SerializeField] private GameObject m_tapButtons; // COMMANDER/FLEET/SETTINGS/RANK/EXPLORATION 진입 버튼 컨테이너 — 오버레이 패널이 하나라도 열리면 숨김

    public override void InitializeUIPanel()
    {
        m_tabSystem.InitializeTabBases();
    }

    public override void OnShowUIPanel()
    {
        EventManager.Subscribe_SpaceShipSelected(OnShipSelectedAutoTabSwitch);
        EventManager.Subscribe_EmptySpaceTapped(OnEmptySpaceTapped);
        EventManager.Subscribe_VipStatusChanged(OnVipStatusChangedForDailyReward);
        EventManager.Subscribe_TutorialGeneralUIBlockedChanged(OnTutorialGeneralUIBlockedChanged);
        EventManager.Subscribe_OverlayPanelActiveChanged(OnOverlayPanelActiveChanged);
        // CheckAndClaimPendingStageRewards(); // 서버 claimPendingStageRewards 주석처리(구 ZoneStageConfig 제거)로 임시 비활성화
        // CheckAndClaimPvpSeasonReward(); // PvP 주석처리로 임시 비활성화
        m_tabSystem.ForceActivateTab();
    }

    public override void OnHideUIPanel()
    {
        EventManager.Unsubscribe_SpaceShipSelected(OnShipSelectedAutoTabSwitch);
        EventManager.Unsubscribe_EmptySpaceTapped(OnEmptySpaceTapped);
        EventManager.Unsubscribe_VipStatusChanged(OnVipStatusChangedForDailyReward);
        EventManager.Unsubscribe_TutorialGeneralUIBlockedChanged(OnTutorialGeneralUIBlockedChanged);
        EventManager.Unsubscribe_OverlayPanelActiveChanged(OnOverlayPanelActiveChanged);
        m_tabSystem.ForceDeactivateTab();

        CameraController.Instance.SetTargetOfCameraController(ObjectManager.Instance.GetMyFleet().transform);
    }

    // ── 미수령 존 보상 복구 ───────────────────────────────────────────────────

    // PvP 주석처리로 임시 비활성화(삭제 아님)
    /*
    private void CheckAndClaimPvpSeasonReward()
    {
        NetworkManager.Instance.PvpClaimSeasonReward(response =>
        {
            if (response == null || response.errorCode != 0) return;
            if (response.data.pvpPointGained <= 0) return;

            var loc = LocalizationManager.Instance;
            UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
            {
                title   = loc.Get("UIPopupMessage_PvpSeasonRewardTitle"),
                message = loc.Get("UIPopupMessage_PvpSeasonRewardMessage"),
                rewardAmounts = new System.Collections.Generic.List<int> { 0, 0, 0, response.data.pvpPointGained },
                onConfirm = () => { }
            });
        });
    }
    */

    // 서버 claimPendingStageRewards 주석처리(구 ZoneStageConfig 제거)로 임시 비활성화
    /*
    private void CheckAndClaimPendingStageRewards()
    {
        NetworkManager.Instance.ClaimPendingStageRewards(response =>
        {
            if (response == null || response.errorCode != 0) return;
            if (response.data.mineralGained == 0) return;

            var commander = DataManager.Instance.m_currentCommander;
            int newLevel  = 0;
            int prevLevel = 0;
            if (commander != null)
            {
                prevLevel = commander.GetCommanderLevel();
                commander.UpdateMineral(response.data.mineralRemain);
                commander.UpdateExp(response.data.totalExp);
                commander.UpdateModulePointMaxGot(response.data.modulePointMaxGot);
                commander.UpdateModulePoint(response.data.modulePointRemain);
                newLevel = response.data.commanderLevel;
                commander.UpdateCommanderLevel(newLevel);
            }

            // 보상 획득 후 기술 레벨업 순서로 표시
            UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
            {
                title   = LocalizationManager.Instance.Get("pending_reward_title"),
                message = LocalizationManager.Instance.Get("pending_reward_message"),
                rewardAmounts = new System.Collections.Generic.List<int>
                {
                    response.data.mineralGained,
                    response.data.expGained,
                    response.data.modulePointGained,
                    0
                },
                onConfirm = () => { }
            });

            if (newLevel > prevLevel)
                UIManager.Instance.ShowCommanderLevelupNotify(newLevel);

            if (response.data.mineralSettingReset == true && response.data.updatedFleetInfo != null)
            {
                var loc = LocalizationManager.Instance;
                var fleet = ObjectManager.Instance.GetMyFleet();
                FleetInfo fleetInfoToApply = response.data.updatedFleetInfo;
                UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
                {
                    title        = loc.Get("UIPopupMessage_MineralResetTitle"),
                    message      = loc.Get("UIPopupMessage_MineralResetMessage"),
                    confirmText1 = loc.Get("Simple_Confirm"),
                    autoCloseSec = 5f,
                    onConfirm    = () =>
                    {
                        if (fleet != null)
                            fleet.ApplyMineralReset(fleetInfoToApply);
                    },
                });
            }
        });
    }
    */

    // ── VIP 일일 미네랄 팝업 ──────────────────────────────────────────────────
    // 최초 진입 시점 체크는 ObjectManager.StartNormalPlay()에서 담당 — 튜토리얼 진행 중에는 호출되지 않도록 보장

    private void OnVipStatusChangedForDailyReward()
    {
        if (DailyBonusManager.Instance == null) return;
        DailyBonusManager.Instance.CheckAndShowDailyRewardPopup();
    }

    // 오버레이 패널을 닫는 로직은 UIManager 자신이 전역으로 처리(UIManager.OnEmptySpaceTapped) —
    // UIPanelSpace는 여기 남아있는 레거시 TabSystem 탭(tab_ship/tab_pvp/tab_calandar)만 정리
    private void OnEmptySpaceTapped()
    {
        if (m_tabSystem.GetCurrentActiveTab() >= 0)
            m_tabSystem.SwitchToTab(-1);
    }

    // 오버레이 패널(메인 패널이 아닌 UIPanelBase, VIP 팝오버 제외)이 하나라도 열리면 진입 버튼들을 숨기고, 전부 닫히면 다시 보임 —
    // "탭 버튼은 기본 상태(아무 화면도 안 열린 상태)에서만 보인다"는 규칙을 UIManager의 오버레이 카운트 하나로 통합 처리
    private void OnOverlayPanelActiveChanged(bool isActive)
    {
        if (m_tapButtons != null)
            m_tapButtons.SetActive(isActive == false);
    }

    // 내 함선 클릭(모듈 명중 여부 무관) 시 함대편성 패널로 자동 전환하며 그 함선을 선택 상태로 표시
    // 튜토리얼 진행 중에는 자동 전환 안 함 — 튜토리얼 스텝이 preActionPanelName 등으로 화면 전환을 직접 제어함
    private void OnShipSelectedAutoTabSwitch(SpaceShip ship)
    {
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsPlaying) return;

        const string panelName = "UIPanelFleetComposition";
        if (UIManager.Instance.GetCurrentActivePanelName() != panelName)
            UIManager.Instance.ShowPanel(panelName);

        UIPanelFleetComposition panel = UIManager.Instance.GetPanel<UIPanelFleetComposition>(panelName);
        if (panel != null)
            panel.SelectPlacedShipByPositionIndex(ship.m_shipInfo.positionIndex);
    }

    // 튜토리얼 dim 없는 스텝에서 상단 진입 버튼(Commander/Fleet/Settings/Exploration 등) 클릭 차단 — 3D 카메라 조작은 별개라 영향 없음
    private void OnTutorialGeneralUIBlockedChanged(bool isBlocked)
    {
        m_tabSystem.SetTabButtonsInteractable(!isBlocked);

        if (m_tapButtons == null) return;
        var entryButtons = m_tapButtons.GetComponentsInChildren<UIPanelEntryButton>(true);
        for (int i = 0; i < entryButtons.Length; i++)
            entryButtons[i].SetInteractable(!isBlocked);
    }

}
