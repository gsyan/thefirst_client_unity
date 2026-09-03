// 함체(ModuleData, body) → 실제 3D SpaceShip 스폰 브릿지
// ModuleHullInfo/ModuleInfo는 서버 저장용 데이터가 아니라, 기존 슬롯 배치·런처 생성 배관(ModuleHull.CreateMissingModules 등)을
// 재사용하기 위한 메모리상 임시 어댑터로만 사용한다 — 여기서 만든 값은 저장/전송되지 않음
using System.Collections.Generic;
using UnityEngine;

public static class ExplorationShipSpawnBridge
{
    // positionIndex는 함대편성 UI 슬롯 인덱스와 반드시 일치해야 함 — 중간 슬롯이 비어도(null) 재번호 없이 그 인덱스 그대로 사용
    public static SpaceShip SpawnShip(SpaceFleet fleet, ModuleData hull, ShipFinalStats finalStats, int positionIndex, bool isFront, float healthMultiplier = 1f, float attackMultiplier = 1f)
    {
        if (fleet == null || hull == null) return null;

        string hullSubType = hull.moduleSubType;
        ShipInfo shipInfo = BuildShipInfo(hullSubType, finalStats, positionIndex, isFront, healthMultiplier);

        GameObject shipGo = new GameObject(hullSubType);
        SpaceShip spaceShip = shipGo.AddComponent<SpaceShip>();
        spaceShip.m_healthMultiplier = healthMultiplier;
        spaceShip.m_attackMultiplier = attackMultiplier;
        spaceShip.InitializeSpaceShip(fleet, shipInfo, finalStats);
        // bWarp=false — 워프 이펙트 없이 최종 대형 위치로 즉시 배치 (UpdateShipFormation이 그 자리를 잡아줌)
        fleet.AddShip(spaceShip, bWarp: false);
        return spaceShip;
    }

    private static ShipInfo BuildShipInfo(string hullSubType, ShipFinalStats finalStats, int positionIndex, bool isFront, float healthMultiplier)
    {
        ModuleHullInfo hullInfo = new ModuleHullInfo
        {
            moduleType    = EModuleType.hull,
            moduleSubType = hullSubType,
            moduleLevel   = 1,
            hullIndex     = 0,
            beams         = new List<ModuleInfo>(),
            missiles      = new List<ModuleInfo>(),
            hangars       = new List<ModuleInfo>(),
        };

        int beamCount = finalStats.beamModuleSubType != null ? finalStats.beamModuleSubType.Length : 0;
        for (int i = 0; i < beamCount; i++)
            hullInfo.beams.Add(BuildModuleInfo(EModuleType.beam, finalStats.beamModuleSubType[i], i));

        int missileCount = finalStats.missileModuleSubType != null ? finalStats.missileModuleSubType.Length : 0;
        for (int i = 0; i < missileCount; i++)
            hullInfo.missiles.Add(BuildModuleInfo(EModuleType.missile, finalStats.missileModuleSubType[i], i));

        // 함재기 세부 스탯(함선/함재기 대상 공격력 분리) 및 요격체 반영은 후속 작업 — 지금은 구조만 채워 슬롯이 비어보이지 않게 함
        int hangarCount = finalStats.hangarModuleSubType != null ? finalStats.hangarModuleSubType.Length : 0;
        for (int i = 0; i < hangarCount; i++)
            hullInfo.hangars.Add(BuildModuleInfo(EModuleType.hangar, finalStats.hangarModuleSubType[i], i));

        // on/off만 지원하므로 서브타입은 항상 shield_1_1 고정 — 무기 티어는 함체와 독립적인 별도 축. 서버 FleetService.getDefaultSubTypeForCategory와 동일 규칙
        hullInfo.shieldModuleSubType = finalStats.shieldInstalled ? "shield_1_1" : "";

        return new ShipInfo
        {
            shipName         = hullSubType,
            positionIndex    = positionIndex,
            hulls            = new List<ModuleHullInfo> { hullInfo },
            hullSubType      = hullSubType,
            isFront          = isFront,
            healthMultiplier = healthMultiplier,
        };
    }

    private static ModuleInfo BuildModuleInfo(EModuleType moduleType, string subTypeName, int slotIndex)
    {
        return new ModuleInfo
        {
            moduleType    = moduleType,
            moduleSubType = subTypeName,
            moduleLevel   = 1,
            slotIndex     = slotIndex,
            hullIndex     = 0,
        };
    }
}
