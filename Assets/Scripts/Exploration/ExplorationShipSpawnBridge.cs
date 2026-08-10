// 함선 프리셋(ShipPresetData) → 실제 3D SpaceShip 스폰 브릿지
// ModuleBodyInfo/ModuleInfo는 서버 저장용 데이터가 아니라, 기존 슬롯 배치·런처 생성 배관(ModuleBody.CreateMissingModules 등)을
// 재사용하기 위한 메모리상 임시 어댑터로만 사용한다 — 여기서 만든 값은 저장/전송되지 않음
using System.Collections.Generic;
using UnityEngine;

public static class ExplorationShipSpawnBridge
{
    // positionIndex는 함대편성 UI 슬롯 인덱스와 반드시 일치해야 함 — 중간 슬롯이 비어도(null) 재번호 없이 그 인덱스 그대로 사용
    public static SpaceShip SpawnShip(SpaceFleet fleet, ShipPresetData preset, ShipFinalStats finalStats, int positionIndex, bool isFront, float healthMultiplier = 1f, float attackMultiplier = 1f)
    {
        if (fleet == null || preset == null) return null;

        ShipInfo shipInfo = BuildShipInfo(preset, finalStats, positionIndex, isFront);

        GameObject shipGo = new GameObject(preset.presetId);
        SpaceShip spaceShip = shipGo.AddComponent<SpaceShip>();
        spaceShip.m_healthMultiplier = healthMultiplier;
        spaceShip.m_attackMultiplier = attackMultiplier;
        spaceShip.InitializeSpaceShip(fleet, shipInfo, finalStats);
        // bWarp=false — 워프 이펙트 없이 최종 대형 위치로 즉시 배치 (UpdateShipFormation이 그 자리를 잡아줌)
        fleet.AddShip(spaceShip, bWarp: false);
        return spaceShip;
    }

    private static ShipInfo BuildShipInfo(ShipPresetData preset, ShipFinalStats finalStats, int positionIndex, bool isFront)
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
            positionIndex = positionIndex,
            bodies        = new List<ModuleBodyInfo> { bodyInfo },
            shipPresetId  = preset.presetId,
            isFront       = isFront,
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
