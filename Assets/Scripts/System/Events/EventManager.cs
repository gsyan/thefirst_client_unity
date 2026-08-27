// 게임 전역 이벤트 허브 — 씬/컴포넌트 간 의존성 없이 발행/구독
// 로그아웃 시 UnsubscribeAll()로 리플렉션 기반 일괄 해제
using System;
using System.Reflection;
using UnityEngine;

public static class EventManager
{
    // 로그아웃 등 씬 전환 시 모든 이벤트 구독 해제 (리플렉션으로 자동 처리)
    public static void UnsubscribeAll()
    {
        var fields = typeof(EventManager).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        for (int i = 0; i < fields.Length; i++)
        {
            if (typeof(Delegate).IsAssignableFrom(fields[i].FieldType) == true)
                fields[i].SetValue(null, null);
        }
    }

    #region ModuleBody -----------------------------------------------------------------------
    public static event Action<ModuleBody> OnModuleBodyDestroyed;
    public static void Trigger_ModuleBodyDestroyed(ModuleBody body) { OnModuleBodyDestroyed?.Invoke(body); }
    public static void Subscribe_ModuleBodyDestroyed(Action<ModuleBody> cb)   { OnModuleBodyDestroyed += cb; }
    public static void Unsubscribe_ModuleBodyDestroyed(Action<ModuleBody> cb) { OnModuleBodyDestroyed -= cb; }
    #endregion

    # region Commander Level, Mineral ----------------------------------------------------------------------
    // CommanderLevel
    public static event Action<int> OnCommanderLevelChanged;
    public static void TriggerCommanderLevelChange(int commanderLevel)
    {
        OnCommanderLevelChanged?.Invoke(commanderLevel);
    }
    public static void Subscribe_CommanderLevelChanged(Action<int> callback)
    {
        OnCommanderLevelChanged += callback;
    }
    public static void Unsubscribe_CommanderLevelChanged(Action<int> callback)
    {
        OnCommanderLevelChanged -= callback;
    }

    // exp — 값을 직접 대입하지 말고 반드시 Commander.UpdateExp()를 거칠 것(이 이벤트가 자동 발행됨)
    public static event Action<int> OnCommanderExpChanged;
    public static void TriggerCommanderExpChanged(int exp) { OnCommanderExpChanged?.Invoke(exp); }
    public static void Subscribe_CommanderExpChanged(Action<int> callback)   { OnCommanderExpChanged += callback; }
    public static void Unsubscribe_CommanderExpChanged(Action<int> callback) { OnCommanderExpChanged -= callback; }

    // pvpPoint
    public static event Action<int> OnPvpPointChanged;
    public static void TriggerPvpPointChanged(int pvpPoint) { OnPvpPointChanged?.Invoke(pvpPoint); }
    public static void Subscribe_PvpPointChanged(Action<int> callback)   { OnPvpPointChanged += callback; }
    public static void Unsubscribe_PvpPointChanged(Action<int> callback) { OnPvpPointChanged -= callback; }

    // explorationPoint — 값을 직접 대입하지 말고 반드시 Commander.UpdateExplorationPoint()를 거칠 것(이 이벤트가 자동 발행됨)
    public static event Action<int> OnExplorationPointChanged;
    public static void TriggerExplorationPointChanged(int explorationPoint) { OnExplorationPointChanged?.Invoke(explorationPoint); }
    public static void Subscribe_ExplorationPointChanged(Action<int> callback)   { OnExplorationPointChanged += callback; }
    public static void Unsubscribe_ExplorationPointChanged(Action<int> callback) { OnExplorationPointChanged -= callback; }

    #endregion Commander Tech, Mineral ----------------------------------------------------------------------
    
    # region Fleet ShipCount / HP----------------------------------------------------------------------
    public static event Action OnFleetShipCountChanged;
    public static void Trigger_FleetShipCountChanged()
    {
        OnFleetShipCountChanged?.Invoke();
    }
    public static void Subscribe_FleetShipCountChanged(Action callback)
    {
        OnFleetShipCountChanged += callback;
    }
    public static void Unsubscribe_FleetShipCountChanged(Action callback)
    {
        OnFleetShipCountChanged -= callback;
    }

    public static event Action OnFleetUpdateHP;
    public static void Trigger_FleetUpdateHP()
    {
        OnFleetUpdateHP?.Invoke();
    }
    public static void Subscribe_FleetUpdateHP(Action callback)
    {
        OnFleetUpdateHP += callback;
    }
    public static void Unsubscribe_FleetUpdateHP(Action callback)
    {
        OnFleetUpdateHP -= callback;
    }

    // 함대 스폰/교체 시점 (튜토리얼→실제 함대 전환 포함) — 스폰 시점에 UI가 아직 없어도 이 이벤트로 뒤늦게 바인딩 가능
    public static event Action OnMyFleetSet;
    public static void Trigger_MyFleetSet()
    {
        OnMyFleetSet?.Invoke();
    }
    public static void Subscribe_MyFleetSet(Action callback)
    {
        OnMyFleetSet += callback;
    }
    public static void Unsubscribe_MyFleetSet(Action callback)
    {
        OnMyFleetSet -= callback;
    }

    public static event Action<EFormationType> OnFormationChanged;
    public static void Trigger_FormationChanged(EFormationType formation)
    {
        OnFormationChanged?.Invoke(formation);
    }
    public static void Subscribe_FormationChanged(Action<EFormationType> callback)
    {
        OnFormationChanged += callback;
    }
    public static void Unsubscribe_FormationChanged(Action<EFormationType> callback)
    {
        OnFormationChanged -= callback;
    }

    # endregion Fleet --------------------------------------------------------------------

    # region Ship ----------------------------------------------------------------------
    public static event Action<SpaceShip> OnSpaceShipSelected;
    public static void Trigger_SpaceShipSelected(SpaceShip ship)
    {
        OnSpaceShipSelected?.Invoke(ship);
    }
    public static void Subscribe_SpaceShipSelected(Action<SpaceShip> callback)
    {
        OnSpaceShipSelected += callback;
    }
    public static void Unsubscribe_SpaceShipSelected(Action<SpaceShip> callback)
    {
        OnSpaceShipSelected -= callback;
    }

    public static event Action OnShipUpdateHP;
    public static void Trigger_ShipUpdateHP()
    {
        OnShipUpdateHP?.Invoke();
    }
    public static void Subscribe_ShipUpdateHP(Action callback)
    {
        OnShipUpdateHP += callback;
    }
    public static void Unsubscribe_ShipUpdateHP(Action callback)
    {
        OnShipUpdateHP -= callback;
    }
    
    public static event Action<SpaceShip> OnShipStatsChanged;
    public static void Trigger_ShipStatsChanged(SpaceShip ship)
    {
        OnShipStatsChanged?.Invoke(ship);
    }
    public static void Subscribe_ShipStatsChanged(Action<SpaceShip> callback)
    {
        OnShipStatsChanged += callback;
    }
    public static void Unsubscribe_ShipStatsChanged(Action<SpaceShip> callback)
    {
        OnShipStatsChanged -= callback;
    }

    // Body 프리팹 교체로 물리적 크기가 변했을 때
    public static event Action<SpaceShip> OnShipBodyChanged;
    public static void Trigger_ShipBodyChanged(SpaceShip ship)
    {
        OnShipBodyChanged?.Invoke(ship);
    }
    public static void Subscribe_ShipBodyChanged(Action<SpaceShip> callback)
    {
        OnShipBodyChanged += callback;
    }
    public static void Unsubscribe_ShipBodyChanged(Action<SpaceShip> callback)
    {
        OnShipBodyChanged -= callback;
    }

    # endregion Ship --------------------------------------------------------------------
    

    # region Module ----------------------------------------------------------------------

    public static event Action<SpaceShip, ModuleBase> OnSpaceShipModuleSelected;
    public static void TriggerSpaceShipModuleSelected(SpaceShip ship, ModuleBase module)
    {
        OnSpaceShipModuleSelected?.Invoke(ship, module);
    }
    public static void Subscribe_SpaceShipModuleSelected(Action<SpaceShip, ModuleBase> callback)
    {
        OnSpaceShipModuleSelected += callback;
    }
    public static void Unsubscribe_SpaceShipModuleSelected(Action<SpaceShip, ModuleBase> callback)
    {
        OnSpaceShipModuleSelected -= callback;
    }

    # endregion Module --------------------------------------------------------------------


    // Camera Focus Change
    public static event Action<ECameraFocusTarget> OnCameraFocusTargetChanged;
    public static void TriggerCameraFocusTargetChanged(ECameraFocusTarget target)
    {
        OnCameraFocusTargetChanged?.Invoke(target);
    }
    public static void Subscribe_CameraFocusTargetChanged(Action<ECameraFocusTarget> callback)
    {
        OnCameraFocusTargetChanged += callback;
    }
    public static void Unsubscribe_CameraFocusTargetChanged(Action<ECameraFocusTarget> callback)
    {
        OnCameraFocusTargetChanged -= callback;
    }

    // 갤럭시뷰 → 함대뷰 카메라 복귀 완료
    public static event Action OnFleetViewRestored;
    public static void TriggerFleetViewRestored() { OnFleetViewRestored?.Invoke(); }
    public static void Subscribe_FleetViewRestored(Action callback)   { OnFleetViewRestored += callback; }
    public static void Unsubscribe_FleetViewRestored(Action callback) { OnFleetViewRestored -= callback; }

    // Camera Viewport Open Ratio (0 = 전체화면, 1 = UI 열림)
    public static event Action<float> OnCameraViewportChanged;
    public static void TriggerCameraViewportChanged(float ratio)
    {
        OnCameraViewportChanged?.Invoke(ratio);
    }
    public static void Subscribe_CameraViewportChanged(Action<float> callback)
    {
        OnCameraViewportChanged += callback;
    }
    public static void Unsubscribe_CameraViewportChanged(Action<float> callback)
    {
        OnCameraViewportChanged -= callback;
    }

    // 플레이어 함대 전멸
    public static event Action OnMyFleetDestroyed;
    public static void Trigger_MyFleetDestroyed()
    {
        OnMyFleetDestroyed?.Invoke();
    }
    public static void Subscribe_MyFleetDestroyed(Action callback)
    {
        OnMyFleetDestroyed += callback;
    }
    public static void Unsubscribe_MyFleetDestroyed(Action callback)
    {
        OnMyFleetDestroyed -= callback;
    }

    // 플레이어 함대 상태 변경 (SpaceFleet.SetFleetState에서 발행)
    public static event Action<EUnitState> OnMyFleetStateChanged;
    public static void TriggerMyFleetStateChanged(EUnitState state) { OnMyFleetStateChanged?.Invoke(state); }
    public static void Subscribe_MyFleetStateChanged(Action<EUnitState> callback)   { OnMyFleetStateChanged += callback; }
    public static void Unsubscribe_MyFleetStateChanged(Action<EUnitState> callback) { OnMyFleetStateChanged -= callback; }

    // 탐사 탭 열림/닫힘
    public static event Action OnExplorationTabOpened;
    public static void TriggerExplorationTabOpened() { OnExplorationTabOpened?.Invoke(); }
    public static void Subscribe_ExplorationTabOpened(Action callback)   { OnExplorationTabOpened += callback; }
    public static void Unsubscribe_ExplorationTabOpened(Action callback) { OnExplorationTabOpened -= callback; }

    public static event Action OnExplorationTabClosed;
    public static void TriggerExplorationTabClosed() { OnExplorationTabClosed?.Invoke(); }
    public static void Subscribe_ExplorationTabClosed(Action callback)   { OnExplorationTabClosed += callback; }
    public static void Unsubscribe_ExplorationTabClosed(Action callback) { OnExplorationTabClosed -= callback; }

    // 오버레이 패널(메인 패널이 아닌 UIPanelBase) 오픈 개수가 0↔1로 전이될 때 발행 — 탭 진입 버튼 가시성 판단에 사용
    public static event Action<bool> OnOverlayPanelActiveChanged;
    public static void TriggerOverlayPanelActiveChanged(bool isActive) { OnOverlayPanelActiveChanged?.Invoke(isActive); }
    public static void Subscribe_OverlayPanelActiveChanged(Action<bool> callback)   { OnOverlayPanelActiveChanged += callback; }
    public static void Unsubscribe_OverlayPanelActiveChanged(Action<bool> callback) { OnOverlayPanelActiveChanged -= callback; }

    // UIManager.currentActivePanel(bHideCurWhenActive 패널들의 배타적 전환 슬롯)이 바뀔 때 발행 — 진입 버튼 하이라이트 갱신용, 없으면 빈 문자열
    public static event Action<string> OnCurrentPanelChanged;
    public static void TriggerCurrentPanelChanged(string panelName) { OnCurrentPanelChanged?.Invoke(panelName); }
    public static void Subscribe_CurrentPanelChanged(Action<string> callback)   { OnCurrentPanelChanged += callback; }
    public static void Unsubscribe_CurrentPanelChanged(Action<string> callback) { OnCurrentPanelChanged -= callback; }

    // 갤럭시 뷰 애니메이션 완료
    public static event Action OnGalaxyViewSettled;
    public static void TriggerGalaxyViewSettled() { OnGalaxyViewSettled?.Invoke(); }
    public static void Subscribe_GalaxyViewSettled(Action callback)   { OnGalaxyViewSettled += callback; }
    public static void Unsubscribe_GalaxyViewSettled(Action callback) { OnGalaxyViewSettled -= callback; }

    public static event Action OnRetreatExploration;
    public static void TriggerRetreatExploration() { OnRetreatExploration?.Invoke(); }
    public static void Subscribe_RetreatExploration(Action callback)   { OnRetreatExploration += callback; }
    public static void Unsubscribe_RetreatExploration(Action callback) { OnRetreatExploration -= callback; }

    public static event Action OnRetreatPvp;
    public static void TriggerRetreatPvp() { OnRetreatPvp?.Invoke(); }
    public static void Subscribe_RetreatPvp(Action callback)   { OnRetreatPvp += callback; }
    public static void Unsubscribe_RetreatPvp(Action callback) { OnRetreatPvp -= callback; }

    public static event Action<bool> OnZoneStageBattleEnd;
    public static void TriggerZoneStageBattleEnd(bool isVictory) { OnZoneStageBattleEnd?.Invoke(isVictory); }
    public static void Subscribe_ZoneStageBattleEnd(Action<bool> callback)   { OnZoneStageBattleEnd += callback; }
    public static void Unsubscribe_ZoneStageBattleEnd(Action<bool> callback) { OnZoneStageBattleEnd -= callback; }

    public static event Action OnPvpBattleStart;
    public static void TriggerPvpBattleStart() { OnPvpBattleStart?.Invoke(); }
    public static void Subscribe_PvpBattleStart(Action callback)   { OnPvpBattleStart += callback; }
    public static void Unsubscribe_PvpBattleStart(Action callback) { OnPvpBattleStart -= callback; }

    public static event Action<bool> OnPvpBattleEnd;
    public static void TriggerPvpBattleEnd(bool isVictory) { OnPvpBattleEnd?.Invoke(isVictory); }
    public static void Subscribe_PvpBattleEnd(Action<bool> callback)   { OnPvpBattleEnd += callback; }
    public static void Unsubscribe_PvpBattleEnd(Action<bool> callback) { OnPvpBattleEnd -= callback; }

    // 존 진입 (zoneName, isFirstClear)
    public static event Action<string, bool> OnZoneEntered;
    public static void TriggerZoneEntered(string zoneName, bool isFirstClear)
    {
        OnZoneEntered?.Invoke(zoneName, isFirstClear);
    }
    public static void Subscribe_ZoneEntered(Action<string, bool> callback)
    {
        OnZoneEntered += callback;
    }
    public static void Unsubscribe_ZoneEntered(Action<string, bool> callback)
    {
        OnZoneEntered -= callback;
    }

    // Zone 모든 적 함대 격멸 (kill 보상용)
    public static event Action OnAllEnemyFleetKilled;
    public static void Trigger_AllEnemyFleetKilled()
    {
        OnAllEnemyFleetKilled?.Invoke();
    }
    public static void Subscribe_AllEnemyFleetKilled(Action callback)
    {
        OnAllEnemyFleetKilled += callback;
    }
    public static void Unsubscribe_AllEnemyFleetKilled(Action callback)
    {
        OnAllEnemyFleetKilled -= callback;
    }

    // PvP 전투 결과 (isVictory, scoreChange, newScore, newRank)
    public static event Action<bool, int, int, int> OnPvpBattleResult;
    public static void TriggerPvpBattleResult(bool isVictory, int scoreChange, int newScore, int newRank)
    {
        OnPvpBattleResult?.Invoke(isVictory, scoreChange, newScore, newRank);
    }
    public static void Subscribe_PvpBattleResult(Action<bool, int, int, int> callback)
    {
        OnPvpBattleResult += callback;
    }
    public static void Unsubscribe_PvpBattleResult(Action<bool, int, int, int> callback)
    {
        OnPvpBattleResult -= callback;
    }

    // 게임 속도 변경 (speed = timeScale 값, pitch = 오디오 피치)
    public static event Action<float, float> OnGameSpeedChanged;
    public static void Trigger_GameSpeedChanged(float speed, float pitch)
    {
        OnGameSpeedChanged?.Invoke(speed, pitch);
    }
    public static void Subscribe_GameSpeedChanged(Action<float, float> callback)
    {
        OnGameSpeedChanged += callback;
    }
    public static void Unsubscribe_GameSpeedChanged(Action<float, float> callback)
    {
        OnGameSpeedChanged -= callback;
    }

    // 전술 옵션 변경 (tacticOptions 비트마스크: 0=수리, 1=미사일, 2=함재기)
    public static event Action<int> OnTacticOptionsChanged;
    public static void Trigger_TacticOptionsChanged(int tacticOptions) { OnTacticOptionsChanged?.Invoke(tacticOptions); }
    public static void Subscribe_TacticOptionsChanged(Action<int> callback)   { OnTacticOptionsChanged += callback; }
    public static void Unsubscribe_TacticOptionsChanged(Action<int> callback) { OnTacticOptionsChanged -= callback; }

    // 전술 토글 요청 — UIPanelCameraView 등 외부에서 idx 토글을 UITabFleetTactics에 위임
    public static event Action<int> OnTacticToggleRequested;
    public static void Trigger_TacticToggleRequested(int idx) { OnTacticToggleRequested?.Invoke(idx); }
    public static void Subscribe_TacticToggleRequested(Action<int> callback)   { OnTacticToggleRequested += callback; }
    public static void Unsubscribe_TacticToggleRequested(Action<int> callback) { OnTacticToggleRequested -= callback; }

    // Module Replaced (oldModule, newModule)
    public static event Action<ModuleBase, ModuleBase> OnModuleReplaced;
    public static void TriggerModuleReplaced(ModuleBase oldModule, ModuleBase newModule)
    {
        OnModuleReplaced?.Invoke(oldModule, newModule);
    }
    public static void Subscribe_ModuleReplaced(Action<ModuleBase, ModuleBase> callback)
    {
        OnModuleReplaced += callback;
    }
    public static void Unsubscribe_ModuleReplaced(Action<ModuleBase, ModuleBase> callback)
    {
        OnModuleReplaced -= callback;
    }

    // Empty Space Tapped — 3D 빈공간 탭 (UI/함선/모듈 아닌 곳)
    public static event Action OnEmptySpaceTapped;
    public static void Trigger_EmptySpaceTapped() { OnEmptySpaceTapped?.Invoke(); }
    public static void Subscribe_EmptySpaceTapped(Action callback) { OnEmptySpaceTapped += callback; }
    public static void Unsubscribe_EmptySpaceTapped(Action callback) { OnEmptySpaceTapped -= callback; }

    // 탐사 그리드 셀 3D 클릭 (갤럭시뷰 전용) — CameraController.HandleGalaxyGridSelection에서 발행
    public static event Action<GridCell3D> OnExplorationGridCellClicked;
    public static void Trigger_ExplorationGridCellClicked(GridCell3D cell) { OnExplorationGridCellClicked?.Invoke(cell); }
    public static void Subscribe_ExplorationGridCellClicked(Action<GridCell3D> callback) { OnExplorationGridCellClicked += callback; }
    public static void Unsubscribe_ExplorationGridCellClicked(Action<GridCell3D> callback) { OnExplorationGridCellClicked -= callback; }

    // 튜토리얼 AnyClick(화면 아무 곳이나 클릭) 대기 상태 변경 — HandleInputMouse/HandleInputTouch가 로컬 캐시 갱신용으로 구독
    public static event Action<bool> OnTutorialWaitingForAnyClickChanged;
    public static void Trigger_TutorialWaitingForAnyClickChanged(bool isWaiting) { OnTutorialWaitingForAnyClickChanged?.Invoke(isWaiting); }
    public static void Subscribe_TutorialWaitingForAnyClickChanged(Action<bool> callback) { OnTutorialWaitingForAnyClickChanged += callback; }
    public static void Unsubscribe_TutorialWaitingForAnyClickChanged(Action<bool> callback) { OnTutorialWaitingForAnyClickChanged -= callback; }

    // 화면 클릭으로 튜토리얼 AnyClick 소비 요청 — HandleInputMouse/HandleInputTouch가 release 시점에 발행, TutorialManager가 구독해서 실제 진행 처리
    public static event Action OnConsumeAnyClick;
    public static void Trigger_ConsumeAnyClick() { OnConsumeAnyClick?.Invoke(); }
    public static void Subscribe_ConsumeAnyClick(Action callback) { OnConsumeAnyClick += callback; }
    public static void Unsubscribe_ConsumeAnyClick(Action callback) { OnConsumeAnyClick -= callback; }

    // 튜토리얼 dim 없는(hasHole 없는) 스텝에서 일반 UI(상단 탭 버튼 등) 차단 여부 변경 — 3D 카메라 조작과는 무관
    public static event Action<bool> OnTutorialGeneralUIBlockedChanged;
    public static void Trigger_TutorialGeneralUIBlockedChanged(bool isBlocked) { OnTutorialGeneralUIBlockedChanged?.Invoke(isBlocked); }
    public static void Subscribe_TutorialGeneralUIBlockedChanged(Action<bool> callback) { OnTutorialGeneralUIBlockedChanged += callback; }
    public static void Unsubscribe_TutorialGeneralUIBlockedChanged(Action<bool> callback) { OnTutorialGeneralUIBlockedChanged -= callback; }

    // Tab Selection Changed — 탭 선택 변경 (systemName: TabSystem 고유 이름, tabIndex: -1이면 전체 닫힘)
    public static event Action<string, int> OnTabSelectionChanged;
    public static void Trigger_TabSelectionChanged(string systemName, int tabIndex) { OnTabSelectionChanged?.Invoke(systemName, tabIndex); }
    public static void Subscribe_TabSelectionChanged(Action<string, int> callback) { OnTabSelectionChanged += callback; }
    public static void Unsubscribe_TabSelectionChanged(Action<string, int> callback) { OnTabSelectionChanged -= callback; }

    // VIP 상태 변경 (구매 완료 / FetchVipStatus 완료 시 발행)
    public static event Action OnVipStatusChanged;
    public static void TriggerVipStatusChanged() { OnVipStatusChanged?.Invoke(); }
    public static void Subscribe_VipStatusChanged(Action callback)   { OnVipStatusChanged += callback; }
    public static void Unsubscribe_VipStatusChanged(Action callback) { OnVipStatusChanged -= callback; }
}