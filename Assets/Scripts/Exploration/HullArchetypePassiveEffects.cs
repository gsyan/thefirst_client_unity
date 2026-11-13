using System.Collections.Generic;

// 아키타입 패시브의 수치 효과(effect_type/effect_value) 합산 — 완료된 단계만 반영, 단계별 효과는 합산(누적), 적용 범위는 effect_scope
// self: 효과를 가진 아키타입과 같은 아키타입의 함선에만, fleet: 그 아키타입 함선이 함대에 살아 있으면 함대 전체 함선에 적용
public static class HullArchetypePassiveEffects
{
    // effect_type 이름 — datatable_hull_archetype_passive.csv와 같은 문자열. 수치 단위는 이름 접미사로 구분(_percent: 퍼센트, _sec: 초, _slot/_hits: 개수)
    public const string k_beamAttackPercent = "beam_attack_percent";
    public const string k_missileAttackPercent = "missile_attack_percent";
    public const string k_hangarAttackPercent = "hangar_attack_percent";
    public const string k_shipHealthPercent = "ship_health_percent";
    public const string k_healthRegenPercent = "health_regen_percent";
    public const string k_missileBlastRadiusPercent = "missile_blast_radius_percent";
    public const string k_missileSpeedPercent = "missile_speed_percent";
    public const string k_missileSilenceTimePercent = "missile_silence_time_percent";
    public const string k_shieldGaugePercent = "shield_gauge_percent";
    public const string k_shieldRegenPercent = "shield_regen_percent";
    public const string k_shieldDamageCostReductionPercent = "shield_damage_cost_reduction_percent";
    public const string k_interceptorHitsPerUnit = "interceptor_hits_per_unit";
    public const string k_interceptorRegenTimeReductionSec = "interceptor_regen_time_reduction_sec";
    // 치명타 확률 가산(%p) — 무기 데이터의 기본 치명타 확률(crit_chance)에 더함
    public const string k_beamCritChance = "beam_crit_chance";
    public const string k_missileCritChance = "missile_crit_chance";
    public const string k_hangarCritChance = "hangar_crit_chance";
    // 빔 공격력 누적 — 빔 모듈이 발사할 때마다 다음 발사부터 stack%씩 증가, 최대 stack_max%. 모듈별로 따로 쌓이고 전투 진입 시 초기화
    public const string k_beamAttackStackPercent = "beam_attack_stack_percent";
    public const string k_beamAttackStackMaxPercent = "beam_attack_stack_max_percent";
    // 전술 자원 사용량 감소(%) — 모든 전술 비용에 적용
    public const string k_resourceCostReductionPercent = "resource_cost_reduction_percent";
    // 버서커 — 값이 0보다 크면 자원 비율 구간별 공격력 배율 적용(SpaceFleet.GetBerserkerAttackMultiplier)
    public const string k_berserkerMode = "berserker_mode";

    private const string k_scopeSelf = "self";
    private const string k_scopeFleet = "fleet";

    // 전투 중 함선에 적용되는 효과 합 — 함선이 속한 함대의 지휘관 빌드 데이터 기준, 함대 범위 효과는 그 아키타입 함선이 지금 함대에 살아 있을 때만 포함
    public static float SumEffect(SpaceShip ship, string effectType)
    {
        if (ship == null || ship.m_ownerFleet == null) return 0f;

        CommanderBuildData buildData = ship.m_ownerFleet.GetBuildData();
        string shipArchetype = GetShipArchetype(ship);
        float sum = SumEffectCore(buildData, shipArchetype, ship.m_ownerFleet, null, effectType);
        PassiveAudit.RecordUse(ship, effectType, sum);
        return sum;
    }

    // 전투 중 함대 단위 효과 합 — 함대 범위(fleet) 효과만 포함(self 효과는 특정 함선 기준이라 제외)
    public static float SumFleetScopeEffect(SpaceFleet fleet, string effectType)
    {
        if (fleet == null) return 0f;

        CommanderBuildData buildData = fleet.GetBuildData();
        float sum = SumEffectCore(buildData, "", fleet, null, effectType);
        PassiveAudit.RecordFleetUse(fleet, effectType, sum);
        return sum;
    }

    // 함대 편성(전투 전 계획) 기준 합, 내 지휘관 — 능력치 미리보기용. fleetArchetypes는 편성에 배치된 아키타입 목록
    public static float SumEffectForPlannedFleet(string shipArchetype, ICollection<string> fleetArchetypes, string effectType)
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        CommanderBuildData buildData = commander != null ? commander.GetBuildData() : null;
        return SumEffectCore(buildData, shipArchetype, null, fleetArchetypes, effectType);
    }

    // 함대 정보(스폰 없이) 기준 합, 임의 지휘관 — 상대 상세 정보 등에서 그 지휘관의 빌드 데이터로 최종 능력치를 계산할 때 사용
    public static float SumEffectForPlannedFleet(CommanderBuildData buildData, string shipArchetype, ICollection<string> fleetArchetypes, string effectType)
    {
        return SumEffectCore(buildData, shipArchetype, null, fleetArchetypes, effectType);
    }

    // 함대 편성에 배치된 아키타입 목록(+ 추가로 포함할 아키타입 1개) — 능력치 미리보기에서 함대 범위 효과를 계획 기준으로 판정할 때 사용
    public static List<string> GetPlannedFleetArchetypes(string additionalArchetype)
    {
        List<string> archetypes = new List<string>();
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition != null)
        {
            List<FleetSlotEntry> placedShips = composition.GetPlacedShips();
            for (int i = 0; i < placedShips.Count; i++)
            {
                if (string.IsNullOrEmpty(placedShips[i].hullSubType) == true) continue;

                ModuleData hullData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(placedShips[i].hullSubType);
                if (hullData == null || string.IsNullOrEmpty(hullData.hullArchetype) == true) continue;
                if (archetypes.Contains(hullData.hullArchetype) == false)
                    archetypes.Add(hullData.hullArchetype);
            }
        }

        if (string.IsNullOrEmpty(additionalArchetype) == false && archetypes.Contains(additionalArchetype) == false)
            archetypes.Add(additionalArchetype);
        return archetypes;
    }

    // 표시용 배율(1 + 합/100), 내 지휘관 — 패시브 효과 합이 0 이하면 1(감소는 표시 대상 아님)
    public static float GetPlannedMultiplier(string shipArchetype, ICollection<string> fleetArchetypes, string effectType)
    {
        float sum = SumEffectForPlannedFleet(shipArchetype, fleetArchetypes, effectType);
        return sum > 0f ? 1f + sum / 100f : 1f;
    }

    // 배율(1 + 합/100), 임의 지휘관 빌드 데이터 기준
    public static float GetPlannedMultiplier(CommanderBuildData buildData, string shipArchetype, ICollection<string> fleetArchetypes, string effectType)
    {
        float sum = SumEffectForPlannedFleet(buildData, shipArchetype, fleetArchetypes, effectType);
        return sum > 0f ? 1f + sum / 100f : 1f;
    }

    public static string GetShipArchetype(SpaceShip ship)
    {
        if (ship.m_moduleHulls.Count == 0) return "";
        return ship.m_moduleHulls[0].GetHullArchetype();
    }

    // buildData: 단계 완료 판정에 쓸 지휘관 빌드 데이터(null이면 패시브 없음)
    // liveFleet: 전투 중 함대(함대 범위 효과는 살아있는 함선 기준), null이면 plannedFleetArchetypes(편성/함대 정보 기준) 사용
    private static float SumEffectCore(CommanderBuildData buildData, string shipArchetype, SpaceFleet liveFleet, ICollection<string> plannedFleetArchetypes, string effectType)
    {
        if (buildData == null) return 0f;

        List<HullArchetypePassiveData> passiveList = DataManager.Instance.m_dataTableHullArchetypePassive.GetPassiveDataList();

        float sum = 0f;
        for (int i = 0; i < passiveList.Count; i++)
        {
            HullArchetypePassiveData passive = passiveList[i];
            List<HullArchetypePassiveEffect> effects = passive.GetEffects();
            if (effects.Count == 0) continue;

            if (buildData.IsPassiveStageCompleted(passive.hullArchetype, passive.stage, passive.threshold) == false) continue;

            for (int j = 0; j < effects.Count; j++)
            {
                HullArchetypePassiveEffect effect = effects[j];
                if (effect.effectType != effectType) continue;

                bool applies = false;
                if (effect.effectScope == k_scopeSelf)
                    applies = passive.hullArchetype == shipArchetype;
                else if (effect.effectScope == k_scopeFleet)
                    applies = IsArchetypeInFleet(passive.hullArchetype, liveFleet, plannedFleetArchetypes);

                if (applies == true)
                    sum += effect.effectValue;
            }
        }
        return sum;
    }

    private static bool IsArchetypeInFleet(string hullArchetype, SpaceFleet liveFleet, ICollection<string> plannedFleetArchetypes)
    {
        if (liveFleet == null)
            return plannedFleetArchetypes != null && plannedFleetArchetypes.Contains(hullArchetype);

        for (int i = 0; i < liveFleet.m_ships.Count; i++)
        {
            SpaceShip fleetShip = liveFleet.m_ships[i];
            if (fleetShip == null || fleetShip.IsAlive() == false) continue;
            if (GetShipArchetype(fleetShip) == hullArchetype) return true;
        }
        return false;
    }
}
