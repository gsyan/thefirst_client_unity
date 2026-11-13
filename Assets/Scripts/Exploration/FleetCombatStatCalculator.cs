using System.Collections.Generic;
using UnityEngine;

// 함선 1척의 최종 전투 능력치(표시용) — 강화 레벨과 패시브가 모두 반영된 값. 강화 레벨/패시브 단계 자체는 담지 않음(상대에게 공개하지 않는 정보)
public struct ShipCombatStats
{
    public string hullSubType;
    public string hullArchetype;
    public float health;
    public float repair;             // 초당 체력 회복(수리력)
    public float shieldGauge;
    public float shieldRegenRate;    // 초당 실드 회복량
    public float beamAttack;
    public float missileAttack;
    public float hangarAttackToShip; // 함재기 대함 공격력 합
    public int airCount;             // 함재기 수 합
    public int interceptorCount;     // 요격체 수
    public int interceptorCapacity;  // 요격 가능한 미사일 수(요격체 수 × 1기당 요격 횟수)
}

// 함선을 스폰하지 않고 함대 정보 + 지휘관 빌드 데이터로 최종 전투 능력치를 계산 — 전투 스폰과 같은 규칙
// (아키타입 강화 레벨 행 → ShipStatCalculator, 패시브 배율 → HullArchetypePassiveEffects). 함대 범위 패시브는 함대 정보에 있는 함선 전부가 살아있다고 보고 계산
// 전투 중에만 바뀌는 값(함선 수 배율, 진형, 전술 토글, 보상카드)은 포함하지 않음
public static class FleetCombatStatCalculator
{
    public static List<ShipCombatStats> BuildShipStats(FleetInfo fleetInfo, CommanderBuildData buildData)
    {
        List<ShipCombatStats> result = new List<ShipCombatStats>();
        if (fleetInfo == null || fleetInfo.ships == null) return result;

        DataTableModule moduleTable = DataManager.Instance.m_dataTableModule;
        ShipStatFormulaSettings formula = DataManager.Instance.m_dataTableConfig.gameSettings.shipStatFormula;
        List<string> fleetArchetypes = CollectFleetArchetypes(fleetInfo, moduleTable);

        for (int i = 0; i < fleetInfo.ships.Count; i++)
        {
            ShipInfo shipInfo = fleetInfo.ships[i];
            if (shipInfo == null) continue;

            ModuleData hullData = moduleTable.GetModuleDataFromTable(shipInfo.hullSubType);
            if (hullData == null) continue;

            result.Add(BuildOneShip(shipInfo, hullData, buildData, fleetArchetypes, moduleTable, formula));
        }
        return result;
    }

    // 함대 합계 — hullSubType/hullArchetype은 비움
    public static ShipCombatStats SumFleet(List<ShipCombatStats> ships)
    {
        ShipCombatStats total = new ShipCombatStats { hullSubType = "", hullArchetype = "" };
        for (int i = 0; i < ships.Count; i++)
        {
            ShipCombatStats ship = ships[i];
            total.health += ship.health;
            total.repair += ship.repair;
            total.shieldGauge += ship.shieldGauge;
            total.shieldRegenRate += ship.shieldRegenRate;
            total.beamAttack += ship.beamAttack;
            total.missileAttack += ship.missileAttack;
            total.hangarAttackToShip += ship.hangarAttackToShip;
            total.airCount += ship.airCount;
            total.interceptorCount += ship.interceptorCount;
            total.interceptorCapacity += ship.interceptorCapacity;
        }
        return total;
    }

    private static ShipCombatStats BuildOneShip(ShipInfo shipInfo, ModuleData hullData, CommanderBuildData buildData, List<string> fleetArchetypes,
        DataTableModule moduleTable, ShipStatFormulaSettings formula)
    {
        string archetype = hullData.hullArchetype;
        ModuleHullInfo modules = shipInfo.hulls != null && shipInfo.hulls.Count > 0 ? shipInfo.hulls[0] : null;
        ShipStatAllocation allocation = ShipStatAllocation.BuildFromModuleHullInfo(formula.maxModuleSlots, modules);

        // 빌드 데이터가 없으면(강화/패시브 정보 없음) 강화 없이 원본 수치 — 내 지휘관 강화가 섞여 들어가지 않게 아키타입을 넘기지 않음
        string upgradeArchetype = buildData != null ? archetype : null;
        ShipFinalStats stats = ShipStatCalculator.Calculate(allocation, formula, hullData, moduleTable, upgradeArchetype, buildData);

        float healthMultiplier = HullArchetypePassiveEffects.GetPlannedMultiplier(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_shipHealthPercent);
        float beamMultiplier = HullArchetypePassiveEffects.GetPlannedMultiplier(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_beamAttackPercent);
        float missileMultiplier = HullArchetypePassiveEffects.GetPlannedMultiplier(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_missileAttackPercent);
        float hangarMultiplier = HullArchetypePassiveEffects.GetPlannedMultiplier(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_hangarAttackPercent);
        float shieldGaugeMultiplier = HullArchetypePassiveEffects.GetPlannedMultiplier(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_shieldGaugePercent);
        float shieldRegenMultiplier = HullArchetypePassiveEffects.GetPlannedMultiplier(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_shieldRegenPercent);
        float repairMultiplier = HullArchetypePassiveEffects.GetPlannedMultiplier(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_healthRegenPercent);
        float extraHitsPerUnit = HullArchetypePassiveEffects.SumEffectForPlannedFleet(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_interceptorHitsPerUnit);
        int hitsPerUnit = 1 + Mathf.RoundToInt(extraHitsPerUnit);

        float beamAttackSum = Sum(stats.beamAttacks);
        float missileAttackSum = Sum(stats.missileAttacks);
        float hangarAttackSum = Sum(stats.hangarShipAttacks);
        float shieldGaugeBase = stats.shieldInstalled == true ? stats.shieldGauge : 0f;
        float shieldRegenBase = stats.shieldInstalled == true ? stats.shieldRegenRate : 0f;
        int interceptorCount = Mathf.RoundToInt(Sum(stats.interceptorCounts));

        return new ShipCombatStats
        {
            hullSubType = shipInfo.hullSubType,
            hullArchetype = archetype,
            health = stats.health * shipInfo.healthMultiplier * healthMultiplier,
            repair = stats.repair * shipInfo.healthMultiplier * repairMultiplier,
            shieldGauge = shieldGaugeBase * shieldGaugeMultiplier,
            shieldRegenRate = shieldRegenBase * shieldRegenMultiplier,
            beamAttack = beamAttackSum * beamMultiplier,
            missileAttack = missileAttackSum * missileMultiplier,
            hangarAttackToShip = hangarAttackSum * hangarMultiplier,
            airCount = CountAircraft(allocation, archetype, buildData, moduleTable),
            interceptorCount = interceptorCount,
            interceptorCapacity = interceptorCount * hitsPerUnit,
        };
    }

    // 함재기 수 — 격납고 슬롯별 강화 레벨 행(hangar_{level}_1)의 airCount 합(행이 없으면 장착 서브타입 원본)
    private static int CountAircraft(ShipStatAllocation allocation, string archetype, CommanderBuildData buildData, DataTableModule moduleTable)
    {
        int total = 0;
        for (int i = 0; i < allocation.hangarModuleSubType.Length; i++)
        {
            string subType = allocation.hangarModuleSubType[i];
            if (string.IsNullOrEmpty(subType) == true) continue;

            ModuleData subTypeData = moduleTable.GetModuleDataFromTable(subType);
            int level = ArchetypeUpgradeLevelLookup.GetLevel(buildData, archetype, EModuleType.hangar, i);
            ModuleData levelData = buildData != null ? moduleTable.GetModuleDataFromTable($"hangar_{level}_1") : null;
            ModuleData sourceData = levelData != null ? levelData : subTypeData;
            if (sourceData != null)
                total += sourceData.airCount;
        }
        return total;
    }

    private static List<string> CollectFleetArchetypes(FleetInfo fleetInfo, DataTableModule moduleTable)
    {
        List<string> archetypes = new List<string>();
        for (int i = 0; i < fleetInfo.ships.Count; i++)
        {
            ShipInfo shipInfo = fleetInfo.ships[i];
            if (shipInfo == null) continue;

            ModuleData hullData = moduleTable.GetModuleDataFromTable(shipInfo.hullSubType);
            if (hullData == null || string.IsNullOrEmpty(hullData.hullArchetype) == true) continue;
            if (archetypes.Contains(hullData.hullArchetype) == false)
                archetypes.Add(hullData.hullArchetype);
        }
        return archetypes;
    }

    private static float Sum(float[] values)
    {
        if (values == null) return 0f;

        float total = 0f;
        for (int i = 0; i < values.Length; i++)
            total += values[i];
        return total;
    }
}
