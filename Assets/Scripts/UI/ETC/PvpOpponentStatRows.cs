// 상대 함대 최종 전투 스탯 → 표시 행 변환 — PVP 선택 카드/상대 상세 팝업(공격 확인 겸용)이 같은 항목·같은 수치를 쓰도록 한곳에서 만듦
// 수치는 FleetCombatStatCalculator(전투 스폰과 같은 규칙: 강화 레벨 + 패시브 배율)가 계산한 최종값, 강화 레벨/패시브 단계 자체는 표시하지 않음
using System.Collections.Generic;

public static class PvpOpponentStatRows
{
    // 함대 요약 행(라벨 로컬라이즈 키, 값 텍스트) — 0인 선택 항목(실드/함재기/요격체)은 생략
    public static List<(string labelKey, string valueText)> BuildSummaryRows(ShipCombatStats total, int shipCount)
    {
        List<(string labelKey, string valueText)> rows = new List<(string labelKey, string valueText)>
        {
            ("fleet_ship_count", shipCount.ToString()),
            ("UIFleet_Stats_Health", CommonUtility.FormatBigNumber(total.health)),
        };

        if (total.shieldGauge > 0f)
            rows.Add(("UIFleet_Stats_ShieldGauge", CommonUtility.FormatBigNumber(total.shieldGauge)));

        rows.Add(("Simple_BeamAttack", CommonUtility.FormatBigNumber(total.beamAttack)));
        rows.Add(("Simple_MissileAttack", CommonUtility.FormatBigNumber(total.missileAttack)));

        if (total.airCount > 0)
        {
            rows.Add(("Simple_AirAttack", CommonUtility.FormatBigNumber(total.hangarAttackToShip)));
            rows.Add(("Simple_AirCount", total.airCount.ToString()));
        }

        if (total.interceptorCount > 0)
            rows.Add(("UIFleet_Stats_InterceptorCount", BuildInterceptorText(total)));

        return rows;
    }

    // 함선 1척의 고정 위치 칸 8개(라벨, 값) — 1줄: 체력/빔/미사일/함재기(공격), 2줄: 실드 게이지/요격체 개수/수리력(체력)/회복력(실드)
    // 라벨은 항상 표시하고 0인 항목은 값만 "-". 함재기 칸 값은 "공격력 (N기)"
    public static List<(string label, string valueText)> BuildShipCells(ShipCombatStats ship)
    {
        LocalizationManager loc = LocalizationManager.Instance;
        List<(string label, string valueText)> cells = new List<(string label, string valueText)>(8);

        cells.Add((loc.Get("UIFleet_Stats_Health"), CommonUtility.FormatBigNumber(ship.health)));
        cells.Add((loc.Get("Simple_BeamAttack"), BuildValueOrDash(ship.beamAttack > 0f, CommonUtility.FormatBigNumber(ship.beamAttack))));
        cells.Add((loc.Get("Simple_MissileAttack"), BuildValueOrDash(ship.missileAttack > 0f, CommonUtility.FormatBigNumber(ship.missileAttack))));
        string airValueText = $"{CommonUtility.FormatBigNumber(ship.hangarAttackToShip)} ({ship.airCount})";
        cells.Add((loc.Get("Simple_AirAttack"), BuildValueOrDash(ship.airCount > 0, airValueText)));

        cells.Add((loc.Get("UIFleet_Stats_ShieldGauge"), BuildValueOrDash(ship.shieldGauge > 0f, CommonUtility.FormatBigNumber(ship.shieldGauge))));
        cells.Add((loc.Get("UIFleet_Stats_InterceptorCount"), BuildValueOrDash(ship.interceptorCount > 0, BuildInterceptorText(ship))));
        cells.Add((loc.Get("UIFleet_Stats_Repair"), BuildValueOrDash(ship.repair > 0f, CommonUtility.FormatBigNumber(ship.repair))));
        cells.Add((loc.Get("UIOpponentDetail_ShieldRegen"), BuildValueOrDash(ship.shieldRegenRate > 0f, CommonUtility.FormatBigNumber(ship.shieldRegenRate))));
        return cells;
    }

    private static string BuildValueOrDash(bool hasValue, string valueText)
    {
        if (hasValue == true) return valueText;
        return "-";
    }

    // 상대 빌드 데이터로 함대 최종 스탯 계산 — 진행도/강화가 응답에 없으면 빈 목록으로 보고 기본 수치(1단계 패시브만)로 계산
    public static List<ShipCombatStats> BuildShipStats(FleetInfo fleetInfo, List<HullArchetypeProgressInfo> passiveProgress, List<ArchetypeUpgradeInfo> archetypeUpgrades)
    {
        List<HullArchetypeProgressInfo> progress = passiveProgress != null ? passiveProgress : new List<HullArchetypeProgressInfo>();
        List<ArchetypeUpgradeInfo> upgrades = archetypeUpgrades != null ? archetypeUpgrades : new List<ArchetypeUpgradeInfo>();
        CommanderBuildData buildData = new CommanderBuildData(progress, upgrades);
        return FleetCombatStatCalculator.BuildShipStats(fleetInfo, buildData);
    }

    // 요격체 수 — 1기가 여러 발을 막으면 "개수 (요격 N회)"로 실제 요격 가능 수를 함께 표시
    private static string BuildInterceptorText(ShipCombatStats stats)
    {
        if (stats.interceptorCapacity > stats.interceptorCount)
            return LocalizationManager.Instance.Get("UIOpponentDetail_InterceptorWithCapacity", stats.interceptorCount, stats.interceptorCapacity);
        return stats.interceptorCount.ToString();
    }
}
