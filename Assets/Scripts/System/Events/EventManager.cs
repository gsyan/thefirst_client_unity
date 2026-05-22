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

    # region Character Tech, Mineral ----------------------------------------------------------------------
    // TechLevel
    public static event Action<int> OnTechLevelChanged;
    public static void TriggerTechLevelChange(int techLevel)
    {
        OnTechLevelChanged?.Invoke(techLevel);
    }
    public static void Subscribe_TechLevelChanged(Action<int> callback)
    {
        OnTechLevelChanged += callback;
    }
    public static void Unsubscribe_TechLevelChanged(Action<int> callback)
    {
        OnTechLevelChanged -= callback;
    }

    // mineral
    public static event Action<long> OnMineralChanged;
    public static void TriggerMineralChange(long money)
    {
        OnMineralChanged?.Invoke(money);
    }
    public static void Subscribe_MineralChanged(Action<long> callback)
    {
        OnMineralChanged += callback;
    }
    public static void Unsubscribe_MineralChanged(Action<long> callback)
    {
        OnMineralChanged -= callback;
    }

    // techPoint
    public static event Action<int> OnTechPointChanged;
    public static void TriggerTechPointChanged(int techPoint) { OnTechPointChanged?.Invoke(techPoint); }
    public static void Subscribe_TechPointChanged(Action<int> callback)   { OnTechPointChanged += callback; }
    public static void Unsubscribe_TechPointChanged(Action<int> callback) { OnTechPointChanged -= callback; }

    // modulePoint
    public static event Action<int> OnModulePointChanged;
    public static void TriggerModulePointChanged(int modulePoint) { OnModulePointChanged?.Invoke(modulePoint); }
    public static void Subscribe_ModulePointChanged(Action<int> callback)   { OnModulePointChanged += callback; }
    public static void Unsubscribe_ModulePointChanged(Action<int> callback) { OnModulePointChanged -= callback; }

    // pvpPoint
    public static event Action<int> OnPvpPointChanged;
    public static void TriggerPvpPointChanged(int pvpPoint) { OnPvpPointChanged?.Invoke(pvpPoint); }
    public static void Subscribe_PvpPointChanged(Action<int> callback)   { OnPvpPointChanged += callback; }
    public static void Unsubscribe_PvpPointChanged(Action<int> callback) { OnPvpPointChanged -= callback; }

    #endregion Character Tech, Mineral ----------------------------------------------------------------------
    
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

    public static event Action OnRetreatRequested;
    public static void TriggerRetreatRequested() { OnRetreatRequested?.Invoke(); }
    public static void Subscribe_RetreatRequested(Action callback)   { OnRetreatRequested += callback; }
    public static void Unsubscribe_RetreatRequested(Action callback) { OnRetreatRequested -= callback; }

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

    // Zone 적 함대 격멸 (kill 보상용)
    public static event Action OnEnemyFleetKilled;
    public static void Trigger_EnemyFleetKilled()
    {
        OnEnemyFleetKilled?.Invoke();
    }
    public static void Subscribe_EnemyFleetKilled(Action callback)
    {
        OnEnemyFleetKilled += callback;
    }
    public static void Unsubscribe_EnemyFleetKilled(Action callback)
    {
        OnEnemyFleetKilled -= callback;
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

}