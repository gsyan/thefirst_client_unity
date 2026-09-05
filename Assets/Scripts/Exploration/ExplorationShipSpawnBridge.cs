// 함체(ModuleData, body) → 실제 3D SpaceShip 스폰 브릿지
// ModuleHullInfo/ModuleInfo는 서버 저장용 데이터가 아니라, 기존 슬롯 배치·런처 생성 배관(ModuleHull.CreateMissingModules 등)을
// 재사용하기 위한 메모리상 임시 어댑터로만 사용한다 — 여기서 만든 값은 저장/전송되지 않음
using System.Collections.Generic;
using UnityEngine;

public static class ExplorationShipSpawnBridge
{
    // positionIndex는 함대편성 UI 슬롯 인덱스와 반드시 일치해야 함 — 중간 슬롯이 비어도(null) 재번호 없이 그 인덱스 그대로 사용.
    // id는 서버가 실제 함대 편성과 대조(SpaceFleet.RestoreDestroyedShips의 생존 판정, 체력 스냅샷 매칭 등)하는 데 쓰는 고유값 —
    // 호출부가 서버 응답의 ShipInfo.id를 그대로 넘겨야 함(생략하면 0 — 새로 배치돼 아직 서버에 확정되지 않은 슬롯에만 해당)
    // actualModules: 이 함선이 실제로 장착한 모듈 구성(슬롯 인덱스 포함) — null이면 무장 없는 빈 로드아웃
    public static SpaceShip SpawnShip(SpaceFleet fleet, ModuleData hull, ShipFinalStats finalStats, ModuleHullInfo actualModules, int positionIndex, bool isFront, float healthMultiplier = 1f, float attackMultiplier = 1f, long id = 0)
    {
        if (fleet == null || hull == null) return null;

        string hullSubType = hull.moduleSubType;
        ShipInfo shipInfo = BuildShipInfo(hullSubType, actualModules, positionIndex, isFront, healthMultiplier, id);

        GameObject shipGo = new GameObject(hullSubType);
        SpaceShip spaceShip = shipGo.AddComponent<SpaceShip>();
        spaceShip.m_healthMultiplier = healthMultiplier;
        spaceShip.m_attackMultiplier = attackMultiplier;
        spaceShip.InitializeSpaceShip(fleet, shipInfo, finalStats);
        // bWarp=false — 워프 이펙트 없이 최종 대형 위치로 즉시 배치 (UpdateShipFormation이 그 자리를 잡아줌)
        fleet.AddShip(spaceShip, bWarp: false);
        return spaceShip;
    }

    // actualModules를 그대로 복사해 슬롯 인덱스/서브타입/강화 포인트를 원본 그대로 유지 — 이전엔 ShipFinalStats의 압축된(빈 슬롯 제거) 배열에서
    // 재구성하다 보니 슬롯 인덱스 정보가 손실되어(예: 3번 슬롯만 장착해도 배열 위치 0으로 압축) 엉뚱한 슬롯에 스폰되는 버그가 있었음
    private static ShipInfo BuildShipInfo(string hullSubType, ModuleHullInfo actualModules, int positionIndex, bool isFront, float healthMultiplier, long id)
    {
        ModuleHullInfo hullInfo = new ModuleHullInfo
        {
            moduleType    = EModuleType.hull,
            moduleSubType = hullSubType,
            moduleLevel   = 1,
            hullIndex     = 0,
            beams         = CopyModuleInfoList(actualModules != null ? actualModules.beams : null),
            missiles      = CopyModuleInfoList(actualModules != null ? actualModules.missiles : null),
            hangars       = CopyModuleInfoList(actualModules != null ? actualModules.hangars : null),
        };

        hullInfo.shieldModuleSubType = actualModules != null && string.IsNullOrEmpty(actualModules.shieldModuleSubType) == false
            ? actualModules.shieldModuleSubType
            : "";

        return new ShipInfo
        {
            id               = id,
            shipName         = hullSubType,
            positionIndex    = positionIndex,
            hulls            = new List<ModuleHullInfo> { hullInfo },
            hullSubType      = hullSubType,
            isFront          = isFront,
            healthMultiplier = healthMultiplier,
        };
    }

    private static List<ModuleInfo> CopyModuleInfoList(List<ModuleInfo> source)
    {
        List<ModuleInfo> result = new List<ModuleInfo>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            ModuleInfo original = source[i];
            result.Add(new ModuleInfo
            {
                moduleType             = original.moduleType,
                moduleSubType          = original.moduleSubType,
                moduleLevel            = original.moduleLevel,
                hullIndex              = original.hullIndex,
                slotIndex              = original.slotIndex,
                attackPoints           = original.attackPoints,
                attackToFighterPoints  = original.attackToFighterPoints,
            });
        }
        return result;
    }
}
