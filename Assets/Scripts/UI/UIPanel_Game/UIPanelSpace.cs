// 우주 공간 UI 패널 — 3D 모듈 선택 자동 전환 등을 담당. UIManager 패널 스택의 base(메인 패널)로, 절대 pop되지 않고
// OnShowUIPanel은 게임 시작 시 딱 한 번만 호출됨(그 뒤로는 항상 상주) — 진입 버튼(COMMANDER/FLEET/SETTINGS/RANK/EXPLORATION)은
// 스택 깊이가 1(base만 남음)일 때만 노출. COMMANDER/SETTINGS/EXPLORATION/FLEET 등은 이제 전부 독립 UIPanelBase 프리팹이라
// 이 스크립트가 그 참조를 들고 있지 않음 — 각자가 UIManager에 스스로 등록되고, 서로 UIManager를 통해서만 상호작용함
// (구 TabSystem 컴포넌트는 실사용 항목이 전부 죽은 참조이거나 다른 전용 스크립트로 이미 대체돼 있어 제거함)
using UnityEngine;

public class UIPanelSpace : UIPanelBase
{
    [Header("진입 버튼 그룹 (기본 상태에서만 노출)")]
    [SerializeField] private GameObject m_tapButtons; // COMMANDER/FLEET/SETTINGS/RANK/EXPLORATION 진입 버튼 컨테이너 — 스택 위에 뭐라도 쌓이면 숨김. 캘린더/VIP(Top)도 이 컨테이너의 자식으로 옮겨져 함께 숨겨짐

    public override void OnShowUIPanel()
    {
        EventManager.Subscribe_SpaceShipSelected(OnShipSelectedAutoTabSwitch);
        EventManager.Subscribe_VipStatusChanged(OnVipStatusChangedForDailyReward);
        EventManager.Subscribe_TutorialGeneralUIBlockedChanged(OnTutorialGeneralUIBlockedChanged);
        EventManager.Subscribe_CurrentPanelChanged(OnPanelStackChanged);
        // CheckAndClaimPvpSeasonReward(); // PvP 주석처리로 임시 비활성화

        RefreshTapButtonsVisibility();
    }

    // base는 절대 pop되지 않아 OnHideUIPanel이 다시 호출될 일이 없음 — 구독 해제는 실제 오브젝트 소멸(씬 전환) 시점에 처리
    private void OnDestroy()
    {
        EventManager.Unsubscribe_SpaceShipSelected(OnShipSelectedAutoTabSwitch);
        EventManager.Unsubscribe_VipStatusChanged(OnVipStatusChangedForDailyReward);
        EventManager.Unsubscribe_TutorialGeneralUIBlockedChanged(OnTutorialGeneralUIBlockedChanged);
        EventManager.Unsubscribe_CurrentPanelChanged(OnPanelStackChanged);
    }

    // 스택 top이 바뀔 때마다 호출 — 진입 버튼 노출 여부만 갱신. 카메라 타겟 리셋은 그게 필요한 패널이 자기 책임으로
    // 처리(예: UIPanelFleet.OnShowUIPanel) — 이 메인 패널이 스택 깊이만 보고 일괄 처리하지 않음(탐사그리드 등 자체
    // 카메라 시스템을 가진 패널까지 무차별로 카메라를 되돌려버리는 문제가 있었음)
    private void OnPanelStackChanged(string topPanelName)
    {
        RefreshTapButtonsVisibility();
    }

    private void RefreshTapButtonsVisibility()
    {
        if (m_tapButtons != null)
            m_tapButtons.SetActive(UIManager.Instance.GetPanelStackDepth() == 1);
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

    // ── VIP 일일 미네랄 팝업 ──────────────────────────────────────────────────
    // 최초 진입 시점 체크는 ObjectManager.StartNormalPlay()에서 담당 — 튜토리얼 진행 중에는 호출되지 않도록 보장

    private void OnVipStatusChangedForDailyReward()
    {
        if (DailyBonusManager.Instance == null) return;
        DailyBonusManager.Instance.CheckAndShowDailyRewardPopup();
    }

    // 내 함선 클릭(모듈 명중 여부 무관) 시 함대편성 패널로 자동 전환하며 그 함선을 선택 상태로 표시
    // 튜토리얼 진행 중에는 자동 전환 안 함 — 튜토리얼 스텝이 preActionPanelName 등으로 화면 전환을 직접 제어함
    private void OnShipSelectedAutoTabSwitch(SpaceShip ship)
    {
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsPlaying) return;

        // 전투 중(실전투)에는 함선을 터치해도 함대관리 UI가 자동으로 열리지 않음 — 어색한 화면 전환 방지.
        // 3D 클릭 자체(카메라 포커스 전환 등)는 CameraController.HandleModuleSelection에서 그대로 처리되므로 이 흐름만 막으면 됨
        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet != null && myFleet.m_fleetState.IsBattleState() == true) return;

        const string panelName = "UIPanelFleet";
        if (UIManager.Instance.GetCurrentActivePanelName() != panelName)
            UIManager.Instance.ShowPanel(panelName);

        UIPanelFleet panel = UIManager.Instance.GetPanel<UIPanelFleet>(panelName);
        if (panel != null)
            panel.SelectPlacedShipByPositionIndex(ship.m_shipInfo.positionIndex);
    }

    // 튜토리얼 dim 없는 스텝에서 상단 진입 버튼(Commander/Fleet/Settings/Exploration 등) 클릭 차단 — 3D 카메라 조작은 별개라 영향 없음
    private void OnTutorialGeneralUIBlockedChanged(bool isBlocked)
    {
        if (m_tapButtons == null) return;
        var entryButtons = m_tapButtons.GetComponentsInChildren<UIPanelEntryButton>(true);
        for (int i = 0; i < entryButtons.Length; i++)
            entryButtons[i].SetInteractable(!isBlocked);
    }

}
