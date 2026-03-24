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

    // mineral rare
    public static event Action<long> OnMineralRareChanged;
    public static void TriggerMineralRareChange(long mineral)
    {
        OnMineralRareChanged?.Invoke(mineral);
    }
    public static void Subscribe_MineralRareChanged(Action<long> callback)
    {
        OnMineralRareChanged += callback;
    }
    public static void Unsubscribe_MineralRareChanged(Action<long> callback)
    {
        OnMineralRareChanged -= callback;
    }
    
    // mineral Exotic
    public static event Action<long> OnMineralExoticChanged;
    public static void TriggerMineralExoticChange(long mineral)
    {
        OnMineralExoticChanged?.Invoke(mineral);
    }
    public static void Subscribe_MineralExoticChanged(Action<long> callback)
    {
        OnMineralExoticChanged += callback;
    }
    public static void Unsubscribe_MineralExoticChanged(Action<long> callback)
    {
        OnMineralExoticChanged -= callback;
    }

    // mineral Dark
    public static event Action<long> OnMineralDarkChanged;
    public static void TriggerMineralDarkChange(long mineral)
    {
        OnMineralDarkChanged?.Invoke(mineral);
    }
    public static void Subscribe_MineralDarkChanged(Action<long> callback)
    {
        OnMineralDarkChanged += callback;
    }
    public static void Unsubscribe_MineralDarkChanged(Action<long> callback)
    {
        OnMineralDarkChanged -= callback;
    }
    #endregion Character Tech, Mineral ----------------------------------------------------------------------
    
    # region Fleet AddShip / HP----------------------------------------------------------------------
    public static event Action OnFleetAddShip;
    public static void Trigger_AddShip()
    {
        OnFleetAddShip?.Invoke();
    }
    public static void Subscribe_AddShip(Action callback)
    {
        OnFleetAddShip += callback;
    }
    public static void Unsubscribe_AddShip(Action callback)
    {
        OnFleetAddShip -= callback;
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

    // Wave Started (1-based currentWave, zoneClearCount)
    public static event Action<int, int> OnWaveStarted;
    public static void TriggerWaveStarted(int currentWave, int zoneClearCount)
    {
        OnWaveStarted?.Invoke(currentWave, zoneClearCount);
    }
    public static void Subscribe_WaveStarted(Action<int, int> callback)
    {
        OnWaveStarted += callback;
    }
    public static void Unsubscribe_WaveStarted(Action<int, int> callback)
    {
        OnWaveStarted -= callback;
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

    // 존 진입 (zoneName, isFirstClear, totalWaves)
    public static event Action<string, bool, int> OnZoneEntered;
    // 존 웨이브 처치 완료 (clearedCount, totalWaves)
    public static event Action<int, int> OnZoneWaveCleared;
    public static void TriggerZoneWaveCleared(int clearedCount, int totalWaves)
    {
        OnZoneWaveCleared?.Invoke(clearedCount, totalWaves);
    }
    public static void Subscribe_ZoneWaveCleared(Action<int, int> callback)
    {
        OnZoneWaveCleared += callback;
    }
    public static void Unsubscribe_ZoneWaveCleared(Action<int, int> callback)
    {
        OnZoneWaveCleared -= callback;
    }
    public static void TriggerZoneEntered(string zoneName, bool isFirstClear, int totalWaves)
    {
        OnZoneEntered?.Invoke(zoneName, isFirstClear, totalWaves);
    }
    public static void Subscribe_ZoneEntered(Action<string, bool, int> callback)
    {
        OnZoneEntered += callback;
    }
    public static void Unsubscribe_ZoneEntered(Action<string, bool, int> callback)
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