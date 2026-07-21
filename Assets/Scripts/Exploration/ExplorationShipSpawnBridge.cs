// 함선 프리셋(ShipPresetData) → 실제 3D SpaceShip 스폰 브릿지
// ModuleBodyInfo/ModuleInfo는 서버 저장용 데이터가 아니라, 기존 슬롯 배치·런처 생성 배관(ModuleBody.CreateMissingModules 등)을
// 재사용하기 위한 메모리상 임시 어댑터로만 사용한다 — 여기서 만든 값은 저장/전송되지 않음
using System.Collections.Generic;
using UnityEngine;

public static class ExplorationShipSpawnBridge
{
    public static SpaceShip SpawnShip(SpaceFleet fleet, ShipPresetData preset, ShipFinalStats finalStats)
    {
        if (fleet == null || preset == null) return null;

        ShipInfo shipInfo = BuildShipInfo(preset, finalStats);

        GameObject shipGo = new GameObject(preset.presetId);
        SpaceShip spaceShip = shipGo.AddComponent<SpaceShip>();
        spaceShip.m_bodyMultiplier = 1f;
        spaceShip.m_beamMultiplier = 1f;
        spaceShip.m_missileMultiplier = 1f;
        spaceShip.m_hangerMultiplier = 1f;
        spaceShip.InitializeSpaceShip(fleet, shipInfo, finalStats);
        // bWarp=false면 AddShip이 대형 위치보다 40유닛 뒤(-Z)에 스폰만 하고 MoveToFormation을 호출하지 않아 그 자리에 멈춰있게 됨
        fleet.AddShip(spaceShip, bWarp: true);
        return spaceShip;
    }

    private static ShipInfo BuildShipInfo(ShipPresetData preset, ShipFinalStats finalStats)
    {
        ModuleBodyInfo bodyInfo = new ModuleBodyInfo
        {
            moduleType    = EModuleType.body,
            moduleSubType = ParseSubType(preset.prefabName),
            moduleLevel   = 1,
            bodyIndex     = 0,
            beams         = new List<ModuleInfo>(),
            missiles      = new List<ModuleInfo>(),
            hangers       = new List<ModuleInfo>(),
        };

        int beamCount = finalStats.beamModuleSubType != null ? finalStats.beamModuleSubType.Length : 0;
        for (int i = 0; i < beamCount; i++)
            bodyInfo.beams.Add(BuildModuleInfo(EModuleType.beam, finalStats.beamModuleSubType[i], i));

        int missileCount = finalStats.missileModuleSubType != null ? finalStats.missileModuleSubType.Length : 0;
        for (int i = 0; i < missileCount; i++)
            bodyInfo.missiles.Add(BuildModuleInfo(EModuleType.missile, finalStats.missileModuleSubType[i], i));

        // 함재기 세부 스탯(함선/함재기 대상 공격력 분리, 실드, 요격체) 반영은 후속 작업 — 지금은 구조만 채워 슬롯이 비어보이지 않게 함
        int hangerCount = finalStats.hangarModuleSubType != null ? finalStats.hangarModuleSubType.Length : 0;
        for (int i = 0; i < hangerCount; i++)
            bodyInfo.hangers.Add(BuildModuleInfo(EModuleType.hanger, finalStats.hangarModuleSubType[i], i));

        return new ShipInfo
        {
            shipName      = preset.presetId,
            positionIndex = 0,
            bodies        = new List<ModuleBodyInfo> { bodyInfo },
        };
    }

    private static ModuleInfo BuildModuleInfo(EModuleType moduleType, string subTypeName, int slotIndex)
    {
        return new ModuleInfo
        {
            moduleType    = moduleType,
            moduleSubType = ParseSubType(subTypeName),
            moduleLevel   = 1,
            slotIndex     = slotIndex,
            bodyIndex     = 0,
        };
    }

    private static EModuleSubType ParseSubType(string name)
    {
        return System.Enum.TryParse(name, out EModuleSubType result) ? result : EModuleSubType.none;
    }
}
