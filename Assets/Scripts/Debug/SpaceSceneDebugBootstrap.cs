#if UNITY_EDITOR
// 에디터 전용 — SpaceScene을 직접 실행할 때 더미 캐릭터/함대 데이터를 주입
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SpaceScene에 배치. DataManager 미초기화 시 더미 데이터를 Awake에서 주입한다.
/// 모든 MonoBehaviour의 Awake는 Start보다 먼저 실행되므로 ObjectManager.Start → SpawnFleet 전에 처리됨.
/// </summary>
public class SpaceSceneDebugBootstrap : MonoBehaviour
{
    private void Awake()
    {
        if (DataManager.Instance.m_currentCharacter != null)
            return;

        InjectDebugCharacter();
        InjectDebugFleet();

        Debug.LogWarning("[DebugBootstrap] 에디터 직접 실행 감지 — 더미 캐릭터/함대 데이터 주입 완료");
    }

    private void InjectDebugCharacter()
    {
        var characterInfo = new CharacterInfo
        {
            characterId   = 1,
            characterName = "debug_player",
            mineral       = 999999,
            pvpMineral    = 0,
            tempMineral   = 0,
            clearedZones  = new List<string>(),
            nameChangeCount = 2,
        };
        DataManager.Instance.SetCharacterInfo(characterInfo);
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
            unlockedSubTypes = new List<EModuleSubType>(),
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
            formation = EFormationType.formation_type_linear_horizontal,
            ships     = new List<ShipInfo> { ship },
        };

        DataManager.Instance.SetFleetData(fleet);
    }
}
#endif
