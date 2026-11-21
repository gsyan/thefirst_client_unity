using System.Collections.Generic;
using UnityEngine;

// 이번 셀 전투의 일일퀘스트 판정값 — ClearExplorationCellRequest에 그대로 실림
public struct BattleQuestStats
{
    public bool isMissileOnlyClear;  // 빔·함재기 없이 미사일만 씀
    public bool isAircraftOnlyClear; // 빔·미사일 없이 함재기만 씀
    public bool isNoShieldClear;     // 실드가 피해를 한 번도 흡수하지 않음
    public int shieldAbsorbAmount;
    public int interceptCount;
    public int fleetRepairAmount;
}

// 탐사 셀 전투 중 일일퀘스트/아키타입 패시브용 전투 통계를 모으는 정적 집계기 — 전투 시작~종료 구간에서만 누적
// 셀 클리어 요청(ClearExplorationCellRequest)에 실어 서버로 보고
public static class BattleStatTracker
{
    // 아키타입 패시브 카운터 종류 — datatable_hull_archetype_passive.csv counter_type, 서버 ExplorationService 허용 목록과 같은 문자열
    public const string k_counterDamageTaken = "damage_taken_cumulative";
    public const string k_counterShieldAbsorb = "shield_absorb_amount";
    public const string k_counterIntercept = "intercept_count";
    public const string k_counterScoutSuccess = "scout_success";
    public const string k_counterAllyRepair = "ally_repair_amount";
    public const string k_counterAllyShieldCharge = "ally_shield_charge_amount";
    // 함대 범위 카운터 — 이벤트가 일어난 순간 살아있던 내 함선의 아키타입에만 적립(파괴된 함선은 이후 적립 없음)
    public const string k_counterDestroyEnemyShip = "destroy_enemy_ship";
    public const string k_counterScavengeMineral = "scavenge_mineral";
    public const string k_counterFleetResourceSaved = "fleet_resource_saved";

    private class ArchetypeCounterEntry
    {
        public string hullArchetype;
        public string counterType;
        public double amount;
    }

    private static bool s_isCollecting;
    private static int s_enemyShipKills;
    private static int s_aircraftLaunches;
    private static int s_scavengedMineral;
    private static int s_scoutedMineral;
    private static readonly List<ArchetypeCounterEntry> s_archetypeCounters = new List<ArchetypeCounterEntry>();

    // 일일퀘스트용 — 이번 셀 전투에서 내 함대가 쓴 무기 종류와 방어/수리 합계
    private static bool s_isBeamFired;
    private static bool s_isMissileFired;
    private static double s_shieldAbsorbTotal;
    private static int s_interceptTotal;
    private static double s_fleetRepairTotal;

    public static bool IsCollecting()
    {
        return s_isCollecting;
    }

    public static void BeginCollecting()
    {
        s_isCollecting = true;
        s_enemyShipKills = 0;
        s_aircraftLaunches = 0;
        s_scavengedMineral = 0;
        s_scoutedMineral = 0;
        s_archetypeCounters.Clear();
        ResetQuestStats();
        EventManager.Trigger_BattleStatCollectingChanged(true);
    }

    // 일일퀘스트 조건 중 전투 도중 값이 오르는 종류의 이번 전투 누적값 — 셀 클리어로만 정해지는 조건이면 false
    // MineralEarned는 셀 보상분 없이 약탈+탐사 광물만(셀 보상은 클리어 확정 시 서버 지급)
    public static bool TryGetLiveDailyCounter(EAchievementConditionType conditionType, out long value)
    {
        value = 0;
        if (conditionType == EAchievementConditionType.EnemyShipKill) value = s_enemyShipKills;
        else if (conditionType == EAchievementConditionType.AircraftLaunch) value = s_aircraftLaunches;
        else if (conditionType == EAchievementConditionType.ScavengedMineral) value = s_scavengedMineral;
        else if (conditionType == EAchievementConditionType.MineralEarned) value = s_scavengedMineral + s_scoutedMineral;
        else if (conditionType == EAchievementConditionType.ShieldAbsorb) value = (long)System.Math.Round(s_shieldAbsorbTotal);
        else if (conditionType == EAchievementConditionType.InterceptCount) value = s_interceptTotal;
        else if (conditionType == EAchievementConditionType.FleetRepairAmount) value = (long)System.Math.Round(s_fleetRepairTotal);
        else return false;
        return true;
    }

    // 이번 전투에서 (아키타입, 카운터 종류)에 쌓인 값 — 없으면 0
    public static long GetArchetypeCounterValue(string hullArchetype, string counterType)
    {
        for (int i = 0; i < s_archetypeCounters.Count; i++)
        {
            ArchetypeCounterEntry entry = s_archetypeCounters[i];
            if (entry.hullArchetype != hullArchetype || entry.counterType != counterType) continue;

            return (long)System.Math.Round(entry.amount);
        }
        return 0;
    }

    private static void ResetQuestStats()
    {
        s_isBeamFired = false;
        s_isMissileFired = false;
        s_shieldAbsorbTotal = 0;
        s_interceptTotal = 0;
        s_fleetRepairTotal = 0;
    }

    // 집계를 끝내고 값을 꺼냄 — 전투 중이 아니었으면 0, 0, 0, 0, 빈 목록, 일일퀘스트 값 모두 0/false
    public static void EndCollecting(out int enemyShipKills, out int aircraftLaunches, out int scavengedMineral, out int scoutedMineral, out List<ArchetypeCounterReport> archetypeCounters, out BattleQuestStats questStats)
    {
        enemyShipKills = s_enemyShipKills;
        aircraftLaunches = s_aircraftLaunches;
        scavengedMineral = s_scavengedMineral;
        scoutedMineral = s_scoutedMineral;

        questStats = new BattleQuestStats();
        if (s_isCollecting == true)
        {
            bool isAircraftLaunched = s_aircraftLaunches > 0;
            questStats.isMissileOnlyClear = s_isMissileFired == true && s_isBeamFired == false && isAircraftLaunched == false;
            questStats.isAircraftOnlyClear = isAircraftLaunched == true && s_isBeamFired == false && s_isMissileFired == false;
            questStats.isNoShieldClear = s_shieldAbsorbTotal <= 0;
            questStats.shieldAbsorbAmount = (int)System.Math.Round(s_shieldAbsorbTotal);
            questStats.interceptCount = s_interceptTotal;
            questStats.fleetRepairAmount = (int)System.Math.Round(s_fleetRepairTotal);
        }

        archetypeCounters = new List<ArchetypeCounterReport>();
        for (int i = 0; i < s_archetypeCounters.Count; i++)
        {
            ArchetypeCounterEntry entry = s_archetypeCounters[i];
            long value = (long)System.Math.Round(entry.amount);
            if (value <= 0) continue;

            archetypeCounters.Add(new ArchetypeCounterReport { hullArchetype = entry.hullArchetype, counterType = entry.counterType, value = value });
        }

        bool wasCollecting = s_isCollecting;
        s_isCollecting = false;
        s_enemyShipKills = 0;
        s_aircraftLaunches = 0;
        s_scavengedMineral = 0;
        s_scoutedMineral = 0;
        s_archetypeCounters.Clear();
        ResetQuestStats();
        if (wasCollecting == true)
            EventManager.Trigger_BattleStatCollectingChanged(false);
    }

    // 내 함대 빔 발사 — 일일퀘스트 "미사일만/함재기만" 판정용
    public static void MarkBeamFired(SpaceShip ship)
    {
        if (s_isCollecting == false) return;
        if (IsPlayerShip(ship) == false) return;
        s_isBeamFired = true;
    }

    // 내 함대 미사일 발사 — 일일퀘스트 "미사일만/함재기만" 판정용
    public static void MarkMissileFired(SpaceShip ship)
    {
        if (s_isCollecting == false) return;
        if (IsPlayerShip(ship) == false) return;
        s_isMissileFired = true;
    }

    // 수리 전술로 내 함대가 회복한 체력 — 아군 수리(피닉스)는 AddShipArchetypeCounter(ally_repair_amount)에서 합산
    public static void AddFleetRepair(SpaceFleet fleet, float healedAmount)
    {
        if (s_isCollecting == false) return;
        if (healedAmount <= 0f) return;
        if (fleet == null || fleet.m_fleetSource != EFleetSource.fleet_source_player) return;
        s_fleetRepairTotal += healedAmount;
    }

    private static bool IsPlayerShip(SpaceShip ship)
    {
        if (ship == null || ship.m_ownerFleet == null) return false;
        return ship.m_ownerFleet.m_fleetSource == EFleetSource.fleet_source_player;
    }

    public static void AddEnemyShipKill()
    {
        if (s_isCollecting == false) return;
        s_enemyShipKills++;
        AddAliveFleetArchetypeCounter(k_counterDestroyEnemyShip, 1f);
    }

    public static void AddAircraftLaunch()
    {
        if (s_isCollecting == false) return;
        s_aircraftLaunches++;
    }

    // 함재기 약탈로 얻은 광물 누적 — 셀 클리어 때 서버가 지급
    public static void AddScavengedMineral(int amount)
    {
        if (s_isCollecting == false) return;
        s_scavengedMineral += amount;
        AddAliveFleetArchetypeCounter(k_counterScavengeMineral, amount);
    }

    // 자원 사용량 감소 패시브로 아낀 전술 자원 누적
    public static void AddFleetResourceSaved(int amount)
    {
        if (s_isCollecting == false) return;
        AddAliveFleetArchetypeCounter(k_counterFleetResourceSaved, amount);
    }

    // 탐사(argus) 성공으로 얻은 광물 누적 — 셀 클리어 때 서버가 지급(함재기 약탈 광물과 별도 집계)
    public static void AddScoutedMineral(int amount)
    {
        if (s_isCollecting == false) return;
        s_scoutedMineral += amount;
    }

    // "그 함선 자신"이 겪은 값을 그 함선의 아키타입 카운터로 누적 — 플레이어 함대 함선이고 탐사 셀 전투 중일 때만
    public static void AddShipArchetypeCounter(SpaceShip ship, string counterType, float amount)
    {
        if (s_isCollecting == false) return;
        if (amount <= 0f) return;
        if (ship == null || ship.m_ownerFleet == null) return;
        if (ship.m_ownerFleet.m_fleetSource != EFleetSource.fleet_source_player) return;
        if (ship.m_moduleHulls.Count == 0) return;

        string hullArchetype = ship.m_moduleHulls[0].GetHullArchetype();
        AddArchetypeEntry(hullArchetype, counterType, amount);
        AddFleetQuestTotal(counterType, amount);
    }

    // 아키타입 카운터 중 일일퀘스트에도 쓰는 종류는 함대 전체 합계로도 누적
    private static void AddFleetQuestTotal(string counterType, float amount)
    {
        if (counterType == k_counterShieldAbsorb)
            s_shieldAbsorbTotal += amount;
        else if (counterType == k_counterIntercept)
            s_interceptTotal += Mathf.RoundToInt(amount);
        else if (counterType == k_counterAllyRepair)
            s_fleetRepairTotal += amount;
    }

    // 함대 범위 카운터 — 지금 살아있는 내 함선의 아키타입마다 같은 값 적립(같은 아키타입은 함대에 1척뿐)
    private static void AddAliveFleetArchetypeCounter(string counterType, float amount)
    {
        if (amount <= 0f) return;

        SpaceFleet myFleet = ObjectManager.Instance != null ? ObjectManager.Instance.GetMyFleet() : null;
        if (myFleet == null) return;

        for (int i = 0; i < myFleet.m_ships.Count; i++)
        {
            SpaceShip ship = myFleet.m_ships[i];
            if (ship == null || ship.IsAlive() == false || ship.m_moduleHulls.Count == 0) continue;

            string hullArchetype = ship.m_moduleHulls[0].GetHullArchetype();
            AddArchetypeEntry(hullArchetype, counterType, amount);
        }
    }

    private static void AddArchetypeEntry(string hullArchetype, string counterType, float amount)
    {
        if (string.IsNullOrEmpty(hullArchetype) == true) return;

        for (int i = 0; i < s_archetypeCounters.Count; i++)
        {
            ArchetypeCounterEntry entry = s_archetypeCounters[i];
            if (entry.hullArchetype != hullArchetype || entry.counterType != counterType) continue;

            entry.amount += amount;
            return;
        }

        s_archetypeCounters.Add(new ArchetypeCounterEntry { hullArchetype = hullArchetype, counterType = counterType, amount = amount });
    }
}
