// 에디터 전용 — SpaceScene을 직접 실행할 때 더미 캐릭터/함대 데이터를 주입
using System.Collections.Generic;
using UnityEngine;

// 빌드 시 클래스가 사라지지 않도록 #if UNITY_EDITOR를 클래스 밖에 두지 않음.
// 대신 Awake에서 자기 파괴하여 missing script 경고를 방지.
public class SpaceSceneDebugBootstrap : MonoBehaviour
{
    private void Awake()
    {
#if !UNITY_EDITOR
        Destroy(gameObject);
        return;
#endif

#if UNITY_EDITOR
        if (DataManager.Instance.m_currentCommander != null)
            return;

        InjectDebugCommander();
        InjectDebugFleet();

        Debug.LogWarning("[DebugBootstrap] 에디터 직접 실행 감지 — 더미 캐릭터/함대 데이터 주입 완료");
#endif
    }

#if UNITY_EDITOR
    private void InjectDebugCommander()
    {
        var commanderInfo = new CommanderInfo
        {
            commanderId   = 1,
            commanderName = "debug_player",
            mineral       = 999999,
            exp           = 999999,
            modulePoint   = 999999,
            pvpPoint      = 0,
            clearedZones  = new List<string>(),
            nameChangeCount = 2,
        };
        DataManager.Instance.SetCommanderInfo(commanderInfo);
    }

    private void InjectDebugFleet()
    {
        if (DataManager.Instance.m_currentFleetInfo != null)
            return;

        var body = new ModuleBodyInfo
        {
            moduleType    = EModuleType.body,
            moduleSubType = EModuleSubType.body_t1_m1,
            moduleLevel   = 1,
            bodyIndex     = 0,
            beams         = new List<ModuleInfo>(),
            missiles      = new List<ModuleInfo>(),
            hangers       = new List<ModuleInfo>(),
        };

        var ship = new ShipInfo
        {
            id            = 1,
            fleetId       = 1,
            shipName      = "Debug Ship",
            positionIndex = 0,
            description   = "",
            bodies        = new List<ModuleBodyInfo> { body },
        };

        var fleet = new FleetInfo
        {
            id        = 1,
            fleetName = "Debug Fleet",
            formation = EFormationType.linear_horizontal,
            ships     = new List<ShipInfo> { ship },
        };

        DataManager.Instance.SetFleetData(fleet);
    }
#endif
}

