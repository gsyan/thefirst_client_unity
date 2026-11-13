// [PassiveAudit] 아키타입 패시브 검증 로그(에디터 전용) — 전투 시작 시 ①서버 빌드 데이터 ②스폰된 함선 능력치, 전투 중 ③효과 사용 기록, 종료 시 요약
// 기대값은 게임 코드(HullArchetypePassiveEffects)를 쓰지 않고 패시브 테이블 + 서버 진행도로 따로 계산 — 같은 버그를 공유해 서로 맞다고 나오는 것을 피함
// 호출은 [Conditional("UNITY_EDITOR")]라 빌드에서는 호출부(인자 계산 포함)가 통째로 제거됨. k_enabled=false면 에디터에서도 끔
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class PassiveAudit
{
    public const bool k_enabled = false; // false로 바꾸면 에디터에서도 로그 안 나옴

    private const string k_tag = "[PassiveAudit]";
    private const int k_maxDetailLogsPerKey = 3; // 함선·항목마다 상세 로그는 처음 몇 번만
    private const float k_tolerance = 0.01f;

    // 패시브 CSV effect_type 전체 — 여기 없는 이름이 CSV에 있으면 오타(효과가 조용히 0이 됨)로 보고
    private static readonly string[] k_knownEffectTypes =
    {
        HullArchetypePassiveEffects.k_beamAttackPercent, HullArchetypePassiveEffects.k_missileAttackPercent, HullArchetypePassiveEffects.k_hangarAttackPercent,
        HullArchetypePassiveEffects.k_shipHealthPercent, HullArchetypePassiveEffects.k_healthRegenPercent,
        HullArchetypePassiveEffects.k_missileBlastRadiusPercent, HullArchetypePassiveEffects.k_missileSpeedPercent, HullArchetypePassiveEffects.k_missileSilenceTimePercent,
        HullArchetypePassiveEffects.k_shieldGaugePercent, HullArchetypePassiveEffects.k_shieldRegenPercent, HullArchetypePassiveEffects.k_shieldDamageCostReductionPercent,
        HullArchetypePassiveEffects.k_interceptorHitsPerUnit, HullArchetypePassiveEffects.k_interceptorRegenTimeReductionSec,
        HullArchetypePassiveEffects.k_beamCritChance, HullArchetypePassiveEffects.k_missileCritChance, HullArchetypePassiveEffects.k_hangarCritChance,
        HullArchetypePassiveEffects.k_beamAttackStackPercent, HullArchetypePassiveEffects.k_beamAttackStackMaxPercent,
        HullArchetypePassiveEffects.k_resourceCostReductionPercent, HullArchetypePassiveEffects.k_berserkerMode,
        "scout_chance", "scout_slot", "scavenge_chance", "ally_repair_slot", "ally_shield_charge_slot",
        "unlock_shield", "unlock_interceptor", "unlock_shield_interceptor",
    };

    private static readonly string[] k_knownCounterTypes =
    {
        BattleStatTracker.k_counterDamageTaken, BattleStatTracker.k_counterShieldAbsorb, BattleStatTracker.k_counterIntercept,
        BattleStatTracker.k_counterScoutSuccess, BattleStatTracker.k_counterAllyRepair, BattleStatTracker.k_counterAllyShieldCharge,
        BattleStatTracker.k_counterDestroyEnemyShip, BattleStatTracker.k_counterScavengeMineral, BattleStatTracker.k_counterFleetResourceSaved,
        "zone_run_clear",
    };

    // 해금 효과는 전투 수치가 아니라 장착 여부로 검증(별도 처리)
    private static readonly HashSet<string> k_unlockEffectTypes = new HashSet<string> { "unlock_shield", "unlock_interceptor", "unlock_shield_interceptor" };

    // 스폰 시 능력치에 미리 반영되는 효과(전투 중 조회 없음) — [Spawn] 비교로 검증하므로 종료 요약의 사용 횟수 경고에서 제외
    private static readonly HashSet<string> k_spawnBakedEffectTypes = new HashSet<string>
    {
        HullArchetypePassiveEffects.k_beamAttackPercent, HullArchetypePassiveEffects.k_missileAttackPercent,
        HullArchetypePassiveEffects.k_shipHealthPercent, HullArchetypePassiveEffects.k_healthRegenPercent,
        HullArchetypePassiveEffects.k_shieldGaugePercent, HullArchetypePassiveEffects.k_shieldRegenPercent,
    };

    // 함대 단위로 조회·기록되는 효과(SumFleetScopeEffect) — 함선별이 아니라 함대 줄에서 사용 여부 확인
    private static readonly HashSet<string> k_fleetLevelEffectTypes = new HashSet<string>
    {
        HullArchetypePassiveEffects.k_resourceCostReductionPercent, HullArchetypePassiveEffects.k_berserkerMode,
    };

    // 효과 → 그 효과가 쓰이는 모듈 — 함선에 해당 모듈이 없으면 사용 0회가 정상이라 종료 요약에서 경고하지 않음
    private static readonly Dictionary<string, EModuleType> k_effectRequiredModule = new Dictionary<string, EModuleType>
    {
        { HullArchetypePassiveEffects.k_beamCritChance, EModuleType.beam },
        { HullArchetypePassiveEffects.k_beamAttackStackPercent, EModuleType.beam },
        { HullArchetypePassiveEffects.k_beamAttackStackMaxPercent, EModuleType.beam },
        { HullArchetypePassiveEffects.k_missileBlastRadiusPercent, EModuleType.missile },
        { HullArchetypePassiveEffects.k_missileSpeedPercent, EModuleType.missile },
        { HullArchetypePassiveEffects.k_missileSilenceTimePercent, EModuleType.missile },
        { HullArchetypePassiveEffects.k_missileCritChance, EModuleType.missile },
        { HullArchetypePassiveEffects.k_hangarAttackPercent, EModuleType.hangar },
        { HullArchetypePassiveEffects.k_hangarCritChance, EModuleType.hangar },
        { "scavenge_chance", EModuleType.hangar },
        { HullArchetypePassiveEffects.k_shieldDamageCostReductionPercent, EModuleType.shield },
        { HullArchetypePassiveEffects.k_interceptorHitsPerUnit, EModuleType.interceptor },
        { HullArchetypePassiveEffects.k_interceptorRegenTimeReductionSec, EModuleType.interceptor },
    };

    private class UseRecord
    {
        public int count;
        public float lastValue;
    }

    private static bool s_isTracking;
    private static readonly Dictionary<string, UseRecord> s_uses = new Dictionary<string, UseRecord>();      // "함선키|효과" → 사용 기록
    private static readonly Dictionary<string, int> s_detailCounts = new Dictionary<string, int>();
    private static readonly List<SpaceFleet> s_trackedFleets = new List<SpaceFleet>();
    private static bool s_isTableChecked;

    // 자동 스폰 테스트(PassiveAuditSpawnTest)용 — 조용한 모드면 문제 있는 줄만 출력, 문제 수는 누계
    public static bool s_quiet;
    public static int s_problemTotal;

    // ── 진입점 ─────────────────────────────────────────────

    // ObjectManager.TryStartCombat — 양 함대가 전투에 들어가는 순간(탐사/PVP 공통)
    [Conditional("UNITY_EDITOR")]
    public static void OnCombatStart(SpaceFleet myFleet, SpaceFleet enemyFleet)
    {
        if (k_enabled == false) return;

        if (s_isTableChecked == false)
        {
            AuditPassiveTable();
            s_isTableChecked = true;
        }

        if (s_isTracking == false)
        {
            s_uses.Clear();
            s_detailCounts.Clear();
            s_trackedFleets.Clear();
        }

        // 검증 중의 패시브 조회는 사용 기록에 넣지 않음
        s_isTracking = false;
        AuditFleet(myFleet);
        AuditFleet(enemyFleet);
        s_isTracking = true;

        if (myFleet != null && s_trackedFleets.Contains(myFleet) == false) s_trackedFleets.Add(myFleet);
        if (enemyFleet != null && s_trackedFleets.Contains(enemyFleet) == false) s_trackedFleets.Add(enemyFleet);
    }

    // ObjectManager.DelayedEndBattle — 전투 종료 시 효과별 사용 요약 + 보유했는데 안 쓰인 효과
    [Conditional("UNITY_EDITOR")]
    public static void OnCombatEnd(bool isVictory)
    {
        if (k_enabled == false || s_isTracking == false) return;
        s_isTracking = false;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"{k_tag}[Combat] 전투 종료 요약 (승리={isVictory})");
        for (int f = 0; f < s_trackedFleets.Count; f++)
        {
            SpaceFleet fleet = s_trackedFleets[f];
            if (fleet == null) continue;
            CommanderBuildData buildData = fleet.GetBuildData();
            if (buildData == null) continue;

            List<string> fleetArchetypes = CollectFleetArchetypes(fleet, false);
            for (int s = 0; s < fleet.m_ships.Count; s++)
            {
                SpaceShip ship = fleet.m_ships[s];
                if (ship == null) continue;
                string shipKey = BuildShipKey(ship);
                string archetype = GetArchetype(ship);
                sb.AppendLine($"  {shipKey}");

                for (int e = 0; e < k_knownEffectTypes.Length; e++)
                {
                    string effectType = k_knownEffectTypes[e];
                    if (k_unlockEffectTypes.Contains(effectType) == true) continue;
                    if (k_fleetLevelEffectTypes.Contains(effectType) == true) continue;

                    float expected = ComputeExpectedSum(buildData, archetype, fleetArchetypes, effectType);
                    UseRecord record;
                    bool hasRecord = s_uses.TryGetValue($"{shipKey}|{effectType}", out record);
                    if (expected == 0f && hasRecord == false) continue;

                    if (k_spawnBakedEffectTypes.Contains(effectType) == true)
                        sb.AppendLine($"    {effectType} 보유={expected} (스폰 능력치에 반영 — [Spawn] 비교로 검증)");
                    else if (hasRecord == true)
                        sb.AppendLine($"    {effectType} 보유={expected} 사용={record.count}회 마지막값={record.lastValue}");
                    else if (HasModuleForEffect(ship, effectType) == false)
                        sb.AppendLine($"    {effectType} 보유={expected} (해당 모듈 없음 — 적용 대상 아님)");
                    else
                        sb.AppendLine($"    {effectType} 보유={expected} 사용=0회 (확인 필요 — 전투 조건상 안 쓰였을 수 있음)");
                }
            }

            // 함대 단위 효과(비용 감소/버서커) — 함대 범위 효과라 어느 함선 기준으로 계산해도 보유값이 같음
            SpaceShip referenceShip = fleet.GetFlagship();
            string referenceArchetype = referenceShip != null ? GetArchetype(referenceShip) : null;
            string fleetKey = BuildFleetKey(fleet);
            foreach (string effectType in k_fleetLevelEffectTypes)
            {
                float expected = referenceShip != null ? ComputeExpectedSum(buildData, referenceArchetype, fleetArchetypes, effectType) : 0f;
                UseRecord record;
                bool hasRecord = s_uses.TryGetValue($"{fleetKey}|{effectType}", out record);
                if (expected == 0f && hasRecord == false) continue;

                if (hasRecord == true)
                    sb.AppendLine($"  {fleetKey} {effectType} 보유={expected} 사용={record.count}회 마지막값={record.lastValue}");
                else
                    sb.AppendLine($"  {fleetKey} {effectType} 보유={expected} 사용=0회 (확인 필요 — 자원 소모가 없었을 수 있음)");
            }
        }
        Debug.Log(sb.ToString());
        s_trackedFleets.Clear();
    }

    // 함선 단위 패시브 조회(SpaceShip.GetPassiveEffect 캐시 적중, HullArchetypePassiveEffects.SumEffect)
    [Conditional("UNITY_EDITOR")]
    public static void RecordUse(SpaceShip ship, string effectType, float value)
    {
        if (k_enabled == false || s_isTracking == false || ship == null) return;
        AddUse($"{BuildShipKey(ship)}|{effectType}", value);
    }

    // 함대 단위 패시브 조회(HullArchetypePassiveEffects.SumFleetScopeEffect)
    [Conditional("UNITY_EDITOR")]
    public static void RecordFleetUse(SpaceFleet fleet, string effectType, float value)
    {
        if (k_enabled == false || s_isTracking == false || fleet == null) return;
        AddUse($"{BuildFleetKey(fleet)}|{effectType}", value);
    }

    // 효과가 실제 계산에 들어간 순간의 상세(발사 배율 분해, 비용 절감 등) — 함선·항목마다 처음 몇 번만 출력
    [Conditional("UNITY_EDITOR")]
    public static void RecordDetail(SpaceShip ship, string key, string detail)
    {
        if (k_enabled == false || s_isTracking == false) return;
        string ownerKey = ship != null ? BuildShipKey(ship) : "?";
        LogDetailLimited($"{ownerKey}|{key}", $"{k_tag}[Combat] {ownerKey} {key}: {detail}");
    }

    [Conditional("UNITY_EDITOR")]
    public static void RecordFleetDetail(SpaceFleet fleet, string key, string detail)
    {
        if (k_enabled == false || s_isTracking == false) return;
        string ownerKey = fleet != null ? BuildFleetKey(fleet) : "?";
        LogDetailLimited($"{ownerKey}|{key}", $"{k_tag}[Combat] {ownerKey} {key}: {detail}");
    }

    // 셀 클리어 때 서버로 보내는 아키타입 카운터 — 해금 진행도에 들어갈 값 확인
    [Conditional("UNITY_EDITOR")]
    public static void LogReportedCounters(List<ArchetypeCounterReport> archetypeCounters)
    {
        if (k_enabled == false) return;

        StringBuilder sb = new StringBuilder();
        sb.Append($"{k_tag}[Counter] 서버 보고 카운터 {(archetypeCounters != null ? archetypeCounters.Count : 0)}건");
        if (archetypeCounters != null)
        {
            for (int i = 0; i < archetypeCounters.Count; i++)
            {
                ArchetypeCounterReport report = archetypeCounters[i];
                bool isKnown = System.Array.IndexOf(k_knownCounterTypes, report.counterType) >= 0;
                sb.Append($" | {report.hullArchetype}.{report.counterType}={report.value}{(isKnown == true ? "" : " UNKNOWN")}");
            }
        }
        Debug.Log(sb.ToString());
    }

    // 자동 스폰 테스트 진입점 — 전투 없이 스폰 직후 함대 하나를 검증(사용 기록에는 넣지 않음)
    [Conditional("UNITY_EDITOR")]
    public static void AuditFleetForTest(SpaceFleet fleet)
    {
        bool wasTracking = s_isTracking;
        s_isTracking = false;
        AuditFleet(fleet);
        s_isTracking = wasTracking;
    }

    [Conditional("UNITY_EDITOR")]
    public static void AuditTableForTest()
    {
        AuditPassiveTable();
        s_isTableChecked = true;
    }

    private static void LogLine(string message, int problemCount)
    {
        if (s_quiet == true && problemCount == 0) return;
        if (problemCount > 0) Debug.LogWarning(message);
        else Debug.Log(message);
    }

    // ── ① 테이블/서버 데이터 ───────────────────────────────

    // 패시브 테이블 자체 — 효과/카운터 이름 오타, 범위, 수치 형식, 아키타입별 1~4단계 존재
    private static void AuditPassiveTable()
    {
        List<HullArchetypePassiveData> passiveList = DataManager.Instance.m_dataTableHullArchetypePassive.GetPassiveDataList();
        int problemCount = 0;
        Dictionary<string, int> stageMaskByArchetype = new Dictionary<string, int>();

        for (int i = 0; i < passiveList.Count; i++)
        {
            HullArchetypePassiveData passive = passiveList[i];
            string rowName = $"{passive.hullArchetype}.{passive.stage}";

            int stageMask;
            stageMaskByArchetype.TryGetValue(passive.hullArchetype, out stageMask);
            stageMaskByArchetype[passive.hullArchetype] = stageMask | (1 << passive.stage);

            if (System.Array.IndexOf(k_knownCounterTypes, passive.counterType) < 0)
            {
                Debug.LogWarning($"{k_tag}[Table] {rowName} 알 수 없는 counter_type '{passive.counterType}'");
                problemCount++;
            }

            List<HullArchetypePassiveEffect> effects = passive.GetEffects();
            int rawTypeCount = string.IsNullOrEmpty(passive.effectType) == true ? 0 : passive.effectType.Split(';').Length;
            if (rawTypeCount != effects.Count)
            {
                Debug.LogWarning($"{k_tag}[Table] {rowName} effect_type {rawTypeCount}개 중 {effects.Count}개만 해석됨(effect_value 개수/형식 확인) type='{passive.effectType}' value='{passive.effectValue}'");
                problemCount++;
            }

            for (int j = 0; j < effects.Count; j++)
            {
                HullArchetypePassiveEffect effect = effects[j];
                if (System.Array.IndexOf(k_knownEffectTypes, effect.effectType) < 0)
                {
                    Debug.LogWarning($"{k_tag}[Table] {rowName} 알 수 없는 effect_type '{effect.effectType}' — 효과가 적용되지 않음");
                    problemCount++;
                }
                if (effect.effectScope != "self" && effect.effectScope != "fleet")
                {
                    Debug.LogWarning($"{k_tag}[Table] {rowName} 알 수 없는 effect_scope '{effect.effectScope}'");
                    problemCount++;
                }
            }

            if (string.IsNullOrEmpty(passive.descriptionKey) == false && LocalizationManager.Instance.Get(passive.descriptionKey) == passive.descriptionKey)
            {
                Debug.LogWarning($"{k_tag}[Table] {rowName} 설명 키 '{passive.descriptionKey}'가 UI 테이블에 없음");
                problemCount++;
            }
        }

        foreach (KeyValuePair<string, int> pair in stageMaskByArchetype)
        {
            int fullMask = (1 << 1) | (1 << 2) | (1 << 3) | (1 << 4);
            if ((pair.Value & fullMask) != fullMask)
            {
                Debug.LogWarning($"{k_tag}[Table] {pair.Key} 1~4단계 중 빠진 단계 있음(mask={pair.Value})");
                problemCount++;
            }
        }

        s_problemTotal += problemCount;
        Debug.Log($"{k_tag}[Table] 패시브 테이블 {passiveList.Count}행 검사, 문제 {problemCount}건");
    }

    private static void AuditFleet(SpaceFleet fleet)
    {
        if (fleet == null) return;

        CommanderBuildData buildData = fleet.GetBuildData();
        string fleetKey = BuildFleetKey(fleet);
        if (buildData == null)
        {
            // 존 적/시네마틱 — 패시브가 새지 않았는지만 확인
            AuditNoPassiveLeak(fleet);
            return;
        }

        AuditBuildData(fleetKey, buildData);
        AuditSpawnedShips(fleet, buildData);
        AuditCalculator(fleet, buildData);
    }

    // ④ 화면 표시용 계산기(FleetCombatStatCalculator — 상세 팝업/미리보기)와 실제 스폰값 — 강화 레벨 반영(티어 행 선택)과 표시-전투 일치 검증
    // 계산기는 보상카드 버프를 모르므로 내 함대에 버프가 하나라도 있으면 건너뜀. 체력은 현재 체력이 아니라 최대치로 비교(탐사 중 피해 반영 제외)
    private static void AuditCalculator(SpaceFleet fleet, CommanderBuildData buildData)
    {
        string fleetKey = BuildFleetKey(fleet);
        if (fleet.m_fleetSource == EFleetSource.fleet_source_player && HasAnyRewardCardBuff() == true)
        {
            Debug.Log($"{k_tag}[Calc] {fleetKey} 보상카드 버프 적용 중이라 계산기 비교 생략");
            return;
        }

        List<ShipCombatStats> expectedStats = FleetCombatStatCalculator.BuildShipStats(fleet.m_fleetInfo, buildData);
        int mismatchCount = 0;
        for (int i = 0; i < expectedStats.Count; i++)
        {
            ShipCombatStats expected = expectedStats[i];
            SpaceShip ship = fleet.m_ships.Find(s => s != null && s.m_shipInfo != null && s.m_shipInfo.hullSubType == expected.hullSubType);
            int shipMismatchStart = mismatchCount;
            StringBuilder line = new StringBuilder();
            line.Append($"{k_tag}[Calc] {fleetKey} {expected.hullArchetype}({expected.hullSubType})");
            if (ship == null)
            {
                line.Append(" 스폰된 함선 없음 MISMATCH");
                Debug.LogWarning(line.ToString());
                mismatchCount++;
                continue;
            }

            CapabilityProfile spawnedStats = ship.GetShipCapabilityProfile(false);
            float healthMax = 0f;
            float shieldGauge = 0f;
            float shieldRegen = 0f;
            int interceptorCount = 0;
            for (int b = 0; b < ship.m_moduleHulls.Count; b++)
            {
                ModuleHull body = ship.m_moduleHulls[b];
                if (body == null) continue;
                healthMax += body.m_healthMax;
                if (body.m_shield != null && body.m_shield.IsEquipped() == true)
                {
                    shieldGauge += body.m_shield.GetGaugeMax();
                    shieldRegen += GetPrivateFloat(body.m_shield, "m_regenRate");
                }
                if (body.m_interceptor != null && body.m_interceptor.IsEquipped() == true)
                    interceptorCount += GetPrivateInt(body.m_interceptor, "m_maxCount");
            }

            mismatchCount += AppendStoredStat(line, "체력최대", expected.health, healthMax);
            mismatchCount += AppendStoredStat(line, "빔", expected.beamAttack, spawnedStats.beamAttack);
            mismatchCount += AppendStoredStat(line, "미사일", expected.missileAttack, spawnedStats.missileAttack);
            mismatchCount += AppendStoredStat(line, "함재기수", expected.airCount, spawnedStats.airCount);
            mismatchCount += AppendStoredStat(line, "수리력", expected.repair, spawnedStats.repair);
            mismatchCount += AppendStoredStat(line, "실드게이지", expected.shieldGauge, shieldGauge);
            mismatchCount += AppendStoredStat(line, "실드회복", expected.shieldRegenRate, shieldRegen);
            mismatchCount += AppendStoredStat(line, "요격체수", expected.interceptorCount, interceptorCount);
            // 함재기 공격력은 정의가 달라(계산기=1기당 합, 스폰=1기당×기수) 참고값으로만
            line.Append($" | (참고) 함재기공격 계산={expected.hangarAttackToShip} 스폰={spawnedStats.airAttack}");
            LogLine(line.ToString(), mismatchCount - shipMismatchStart);
        }

        s_problemTotal += mismatchCount;
        LogLine($"{k_tag}[Calc] {fleetKey} 계산기 {expectedStats.Count}척 비교, MISMATCH {mismatchCount}건", mismatchCount);
    }

    private static bool HasAnyRewardCardBuff()
    {
        RewardCardSessionState sessionState = ObjectManager.Instance != null ? ObjectManager.Instance.m_rewardCardSessionState : null;
        if (sessionState == null) return false;

        System.Array effectTypes = System.Enum.GetValues(typeof(ECardEffectType));
        for (int i = 0; i < effectTypes.Length; i++)
        {
            float multiplier = sessionState.GetMultiplier((ECardEffectType)effectTypes.GetValue(i));
            if (Mathf.Abs(multiplier - 1f) > 0.0001f) return true;
        }
        return false;
    }

    // ② 서버에서 받은 진행도/강화 레벨 — 테이블 규칙과 어긋나는 값
    private static void AuditBuildData(string fleetKey, CommanderBuildData buildData)
    {
        List<HullArchetypePassiveData> passiveList = DataManager.Instance.m_dataTableHullArchetypePassive.GetPassiveDataList();
        int problemCount = 0;
        int progressCount = buildData.passiveProgress != null ? buildData.passiveProgress.Count : 0;
        int upgradeCount = buildData.archetypeUpgrades != null ? buildData.archetypeUpgrades.Count : 0;

        for (int i = 0; i < progressCount; i++)
        {
            HullArchetypeProgressInfo entry = buildData.passiveProgress[i];
            string rowName = $"{entry.hullArchetype}.{entry.stage}";
            HullArchetypePassiveData passive = FindPassive(passiveList, entry.hullArchetype, entry.stage);
            if (passive == null)
            {
                Debug.LogWarning($"{k_tag}[Data] {fleetKey} 진행도 {rowName} — 테이블에 없는 단계");
                problemCount++;
                continue;
            }
            if (entry.counterType != passive.counterType)
            {
                Debug.LogWarning($"{k_tag}[Data] {fleetKey} 진행도 {rowName} counterType 서버='{entry.counterType}' 테이블='{passive.counterType}'");
                problemCount++;
            }
            if (entry.completed == true && entry.counterValue < passive.threshold)
            {
                Debug.LogWarning($"{k_tag}[Data] {fleetKey} 진행도 {rowName} 완료인데 counter={entry.counterValue} < 문턱 {passive.threshold}");
                problemCount++;
            }
            if (entry.completed == false && passive.threshold > 0 && entry.counterValue >= passive.threshold)
            {
                Debug.LogWarning($"{k_tag}[Data] {fleetKey} 진행도 {rowName} 미완료인데 counter={entry.counterValue} >= 문턱 {passive.threshold}");
                problemCount++;
            }
            if (entry.completed == true && entry.stage > 1 && IsStageCompletedRaw(buildData, passiveList, entry.hullArchetype, entry.stage - 1) == false)
            {
                Debug.LogWarning($"{k_tag}[Data] {fleetKey} 진행도 {rowName} 완료인데 앞 단계 미완료");
                problemCount++;
            }
        }

        int maxLevel = DataManager.Instance.m_dataTableConfig.gameSettings.archetypeUpgrade.maxLevel;
        for (int i = 0; i < upgradeCount; i++)
        {
            ArchetypeUpgradeInfo entry = buildData.archetypeUpgrades[i];
            string rowName = $"{entry.hullArchetype}.{entry.moduleType}[{entry.slotIndex}]";
            int effectLevel = entry.level + 1;
            if (entry.level < 0 || effectLevel > maxLevel)
            {
                Debug.LogWarning($"{k_tag}[Data] {fleetKey} 강화 {rowName} 레벨 {effectLevel}이 범위(1~{maxLevel}) 밖");
                problemCount++;
            }
            bool isSlotless = entry.moduleType == EModuleType.hull || entry.moduleType == EModuleType.shield || entry.moduleType == EModuleType.interceptor;
            if (isSlotless == true && entry.slotIndex != 0)
            {
                Debug.LogWarning($"{k_tag}[Data] {fleetKey} 강화 {rowName} 슬롯 없는 모듈인데 slotIndex≠0");
                problemCount++;
            }
            if (FindAnyPassiveOfArchetype(passiveList, entry.hullArchetype) == false)
            {
                Debug.LogWarning($"{k_tag}[Data] {fleetKey} 강화 {rowName} 테이블에 없는 아키타입");
                problemCount++;
            }
        }

        s_problemTotal += problemCount;
        LogLine($"{k_tag}[Data] {fleetKey} 진행도 {progressCount}건, 강화 {upgradeCount}건 검사, 문제 {problemCount}건", problemCount);
    }

    // ③ 스폰된 함선 — 효과별 기대 합 vs 함선이 실제로 쓰는 값, 저장형 능력치(체력/공격/실드 등) vs 원본×배율
    private static void AuditSpawnedShips(SpaceFleet fleet, CommanderBuildData buildData)
    {
        string fleetKey = BuildFleetKey(fleet);
        List<string> fleetArchetypes = CollectFleetArchetypes(fleet, true);
        int mismatchCount = 0;

        for (int s = 0; s < fleet.m_ships.Count; s++)
        {
            SpaceShip ship = fleet.m_ships[s];
            if (ship == null) continue;

            string shipKey = BuildShipKey(ship);
            string archetype = GetArchetype(ship);
            int shipMismatchStart = mismatchCount;
            StringBuilder line = new StringBuilder();
            line.Append($"{k_tag}[Spawn] {shipKey}");

            // 효과 합 — 함선 캐시(GetPassiveEffect)를 지우고 다시 받아 비교
            ship.InvalidatePassiveEffectCache();
            for (int e = 0; e < k_knownEffectTypes.Length; e++)
            {
                string effectType = k_knownEffectTypes[e];
                if (k_unlockEffectTypes.Contains(effectType) == true) continue;

                float expected = ComputeExpectedSum(buildData, archetype, fleetArchetypes, effectType);
                float actual = ship.GetPassiveEffect(effectType);
                if (expected == 0f && actual == 0f) continue;

                bool isMismatch = Mathf.Abs(expected - actual) > k_tolerance;
                if (isMismatch == true) mismatchCount++;
                line.Append($" | {effectType} 기대={expected} 실제={actual}{(isMismatch == true ? " MISMATCH" : "")}");
            }

            for (int b = 0; b < ship.m_moduleHulls.Count; b++)
            {
                ModuleHull body = ship.m_moduleHulls[b];
                if (body == null) continue;
                mismatchCount += AuditHullStats(body, buildData, archetype, fleetArchetypes, line);
            }

            LogLine(line.ToString(), mismatchCount - shipMismatchStart);
        }

        s_problemTotal += mismatchCount;
        LogLine($"{k_tag}[Spawn] {fleetKey} 함선 {fleet.m_ships.Count}척 검사, MISMATCH {mismatchCount}건", mismatchCount);
    }

    private static int AuditHullStats(ModuleHull body, CommanderBuildData buildData, string archetype, List<string> fleetArchetypes, StringBuilder line)
    {
        int mismatchCount = 0;

        float healthPassive = ComputeExpectedSum(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_shipHealthPercent);
        float healthReward = body.GetRewardCardBuffMultiplier(ECardEffectType.Buff_ShipHealth);
        float baseHealthMax = GetPrivateFloat(body, "m_baseHealthMax");
        mismatchCount += AppendStoredStat(line, "체력최대", baseHealthMax * healthReward * (1f + healthPassive / 100f), body.m_healthMax);

        float regenPassive = ComputeExpectedSum(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_healthRegenPercent);
        float baseRepair = GetPrivateFloat(body, "m_baseRepair");
        mismatchCount += AppendStoredStat(line, "수리력", baseRepair * (1f + regenPassive / 100f), body.GetRepair());

        float beamPassive = ComputeExpectedSum(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_beamAttackPercent);
        for (int i = 0; i < body.m_beams.Count; i++)
        {
            ModuleBeam beam = body.m_beams[i];
            if (beam == null) continue;
            float reward = beam.GetRewardCardBuffMultiplier(ECardEffectType.Buff_BeamAttack);
            float expected = GetPrivateFloat(beam, "m_baseAttack") * reward * (1f + beamPassive / 100f);
            mismatchCount += AppendStoredStat(line, $"빔{i}공격", expected, beam.m_attack);
        }

        float missilePassive = ComputeExpectedSum(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_missileAttackPercent);
        float radiusPassive = ComputeExpectedSum(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_missileBlastRadiusPercent);
        float speedPassive = ComputeExpectedSum(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_missileSpeedPercent);
        for (int i = 0; i < body.m_missiles.Count; i++)
        {
            ModuleMissile missile = body.m_missiles[i];
            if (missile == null) continue;
            float reward = missile.GetRewardCardBuffMultiplier(ECardEffectType.Buff_MissileAttack);
            float expected = GetPrivateFloat(missile, "m_baseAttack") * reward * (1f + missilePassive / 100f);
            mismatchCount += AppendStoredStat(line, $"미사일{i}공격", expected, missile.m_attack);
            mismatchCount += AppendStoredStat(line, $"미사일{i}범위배율", 1f + radiusPassive / 100f, missile.GetBlastRadiusMultiplier());
            mismatchCount += AppendStoredStat(line, $"미사일{i}속도배율", 1f + speedPassive / 100f, missile.GetSpeedMultiplier());
        }

        ModuleShield shield = body.m_shield;
        bool isShieldEquipped = shield != null && shield.IsEquipped() == true;
        if (isShieldEquipped == true)
        {
            float gaugePassive = ComputeExpectedSum(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_shieldGaugePercent);
            float shieldRegenPassive = ComputeExpectedSum(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_shieldRegenPercent);
            mismatchCount += AppendStoredStat(line, "실드게이지", GetPrivateFloat(shield, "m_baseGaugeMax") * (1f + gaugePassive / 100f), shield.GetGaugeMax());
            mismatchCount += AppendStoredStat(line, "실드회복", GetPrivateFloat(shield, "m_baseRegenRate") * (1f + shieldRegenPassive / 100f), GetPrivateFloat(shield, "m_regenRate"));
        }

        ModuleInterceptor interceptor = body.m_interceptor;
        bool isInterceptorEquipped = interceptor != null && interceptor.IsEquipped() == true;
        if (isInterceptorEquipped == true)
        {
            float hitsPassive = ComputeExpectedSum(buildData, archetype, fleetArchetypes, HullArchetypePassiveEffects.k_interceptorHitsPerUnit);
            mismatchCount += AppendStoredStat(line, "요격횟수", 1f + Mathf.Round(hitsPassive), interceptor.GetHitsPerUnit());
        }

        // 해금 — 실드/요격체 장착 여부가 진행도의 해금 단계와 맞는지(슬롯 없는 함체는 해금돼도 미장착이 정상이라 반대 방향만 MISMATCH)
        bool isShieldUnlocked = IsUnlockedRaw(buildData, archetype, EModuleType.shield);
        bool isInterceptorUnlocked = IsUnlockedRaw(buildData, archetype, EModuleType.interceptor);
        if (isShieldEquipped == true && isShieldUnlocked == false) { line.Append(" | 실드 미해금인데 장착 MISMATCH"); mismatchCount++; }
        if (isInterceptorEquipped == true && isInterceptorUnlocked == false) { line.Append(" | 요격체 미해금인데 장착 MISMATCH"); mismatchCount++; }
        line.Append($" | 해금 실드={isShieldUnlocked}/장착={isShieldEquipped} 요격체={isInterceptorUnlocked}/장착={isInterceptorEquipped}");

        return mismatchCount;
    }

    // 빌드 데이터가 없는 함대(존 적/시네마틱)에 패시브가 0이 아닌 값으로 새는지
    private static void AuditNoPassiveLeak(SpaceFleet fleet)
    {
        int leakCount = 0;
        for (int s = 0; s < fleet.m_ships.Count; s++)
        {
            SpaceShip ship = fleet.m_ships[s];
            if (ship == null) continue;
            for (int e = 0; e < k_knownEffectTypes.Length; e++)
            {
                float actual = ship.GetPassiveEffect(k_knownEffectTypes[e]);
                if (actual == 0f) continue;
                Debug.LogWarning($"{k_tag}[Spawn] {BuildShipKey(ship)} 패시브 없는 함대인데 {k_knownEffectTypes[e]}={actual} MISMATCH");
                leakCount++;
            }
        }
        s_problemTotal += leakCount;
        LogLine($"{k_tag}[Spawn] {BuildFleetKey(fleet)} 패시브 없는 함대 {fleet.m_ships.Count}척, 새는 효과 {leakCount}건", leakCount);
    }

    // ── 독립 계산(게임 코드와 별도) ─────────────────────────

    // 효과 합 — 완료 단계만, self는 그 함선 아키타입, fleet는 함대에 살아 있는 아키타입
    private static float ComputeExpectedSum(CommanderBuildData buildData, string shipArchetype, List<string> fleetArchetypes, string effectType)
    {
        List<HullArchetypePassiveData> passiveList = DataManager.Instance.m_dataTableHullArchetypePassive.GetPassiveDataList();
        float sum = 0f;
        for (int i = 0; i < passiveList.Count; i++)
        {
            HullArchetypePassiveData passive = passiveList[i];
            if (IsStageCompletedRaw(buildData, passiveList, passive.hullArchetype, passive.stage) == false) continue;

            List<HullArchetypePassiveEffect> effects = passive.GetEffects();
            for (int j = 0; j < effects.Count; j++)
            {
                HullArchetypePassiveEffect effect = effects[j];
                if (effect.effectType != effectType) continue;

                bool isSelfMatch = effect.effectScope == "self" && passive.hullArchetype == shipArchetype;
                bool isFleetMatch = effect.effectScope == "fleet" && fleetArchetypes.Contains(passive.hullArchetype) == true;
                if (isSelfMatch == true || isFleetMatch == true)
                    sum += effect.effectValue;
            }
        }
        return sum;
    }

    // 서버 진행도 원본으로 단계 완료 판정 — 기록이 없으면 문턱 0 단계만 완료
    private static bool IsStageCompletedRaw(CommanderBuildData buildData, List<HullArchetypePassiveData> passiveList, string hullArchetype, int stage)
    {
        if (buildData.passiveProgress != null)
        {
            for (int i = 0; i < buildData.passiveProgress.Count; i++)
            {
                HullArchetypeProgressInfo entry = buildData.passiveProgress[i];
                if (entry.hullArchetype == hullArchetype && entry.stage == stage) return entry.completed;
            }
        }
        HullArchetypePassiveData passive = FindPassive(passiveList, hullArchetype, stage);
        return passive != null && passive.threshold <= 0;
    }

    private static bool IsUnlockedRaw(CommanderBuildData buildData, string hullArchetype, EModuleType moduleType)
    {
        List<HullArchetypePassiveData> passiveList = DataManager.Instance.m_dataTableHullArchetypePassive.GetPassiveDataList();
        string specificType = moduleType == EModuleType.shield ? "unlock_shield" : "unlock_interceptor";
        for (int i = 0; i < passiveList.Count; i++)
        {
            HullArchetypePassiveData passive = passiveList[i];
            if (passive.hullArchetype != hullArchetype) continue;
            if (IsStageCompletedRaw(buildData, passiveList, passive.hullArchetype, passive.stage) == false) continue;

            List<HullArchetypePassiveEffect> effects = passive.GetEffects();
            for (int j = 0; j < effects.Count; j++)
            {
                if (effects[j].effectType == specificType || effects[j].effectType == "unlock_shield_interceptor") return true;
            }
        }
        return false;
    }

    // ── 유틸 ──────────────────────────────────────────────

    private static void AddUse(string key, float value)
    {
        UseRecord record;
        if (s_uses.TryGetValue(key, out record) == false)
        {
            record = new UseRecord();
            s_uses[key] = record;
        }
        record.count++;
        record.lastValue = value;
    }

    private static void LogDetailLimited(string countKey, string message)
    {
        int count;
        s_detailCounts.TryGetValue(countKey, out count);
        if (count >= k_maxDetailLogsPerKey) return;
        s_detailCounts[countKey] = count + 1;
        Debug.Log(message);
    }

    private static int AppendStoredStat(StringBuilder line, string label, float expected, float actual)
    {
        float diff = Mathf.Abs(expected - actual);
        float allowed = Mathf.Max(k_tolerance, Mathf.Abs(expected) * 0.001f);
        bool isMismatch = diff > allowed;
        line.Append($" | {label} 기대={expected:0.##} 실제={actual:0.##}{(isMismatch == true ? " MISMATCH" : "")}");
        return isMismatch == true ? 1 : 0;
    }

    private static float GetPrivateFloat(object target, string fieldName)
    {
        System.Type type = target.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) return (float)field.GetValue(target);
            type = type.BaseType;
        }
        Debug.LogWarning($"{k_tag} 필드 {target.GetType().Name}.{fieldName} 없음 — 검증 코드 갱신 필요");
        return float.NaN;
    }

    private static int GetPrivateInt(object target, string fieldName)
    {
        System.Type type = target.GetType();
        while (type != null)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) return (int)field.GetValue(target);
            type = type.BaseType;
        }
        Debug.LogWarning($"{k_tag} 필드 {target.GetType().Name}.{fieldName} 없음 — 검증 코드 갱신 필요");
        return -1;
    }

    private static List<string> CollectFleetArchetypes(SpaceFleet fleet, bool aliveOnly)
    {
        List<string> archetypes = new List<string>();
        for (int i = 0; i < fleet.m_ships.Count; i++)
        {
            SpaceShip ship = fleet.m_ships[i];
            if (ship == null) continue;
            if (aliveOnly == true && ship.IsAlive() == false) continue;
            string archetype = GetArchetype(ship);
            if (string.IsNullOrEmpty(archetype) == false && archetypes.Contains(archetype) == false)
                archetypes.Add(archetype);
        }
        return archetypes;
    }

    private static HullArchetypePassiveData FindPassive(List<HullArchetypePassiveData> passiveList, string hullArchetype, int stage)
    {
        for (int i = 0; i < passiveList.Count; i++)
        {
            if (passiveList[i].hullArchetype == hullArchetype && passiveList[i].stage == stage) return passiveList[i];
        }
        return null;
    }

    private static bool FindAnyPassiveOfArchetype(List<HullArchetypePassiveData> passiveList, string hullArchetype)
    {
        for (int i = 0; i < passiveList.Count; i++)
        {
            if (passiveList[i].hullArchetype == hullArchetype) return true;
        }
        return false;
    }

    // 효과가 쓰이는 모듈을 함선이 갖고 있는지 — 대응 모듈이 정해지지 않은 효과는 항상 true(경고 유지)
    private static bool HasModuleForEffect(SpaceShip ship, string effectType)
    {
        EModuleType requiredModule;
        if (k_effectRequiredModule.TryGetValue(effectType, out requiredModule) == false) return true;

        for (int b = 0; b < ship.m_moduleHulls.Count; b++)
        {
            ModuleHull body = ship.m_moduleHulls[b];
            if (body == null) continue;

            bool hasModule = false;
            if (requiredModule == EModuleType.beam) hasModule = body.m_beams.Count > 0;
            else if (requiredModule == EModuleType.missile) hasModule = body.m_missiles.Count > 0;
            else if (requiredModule == EModuleType.hangar) hasModule = body.m_hangars.Count > 0;
            else if (requiredModule == EModuleType.shield) hasModule = body.m_shield != null && body.m_shield.IsEquipped() == true;
            else if (requiredModule == EModuleType.interceptor) hasModule = body.m_interceptor != null && body.m_interceptor.IsEquipped() == true;

            if (hasModule == true) return true;
        }
        return false;
    }

    private static string GetArchetype(SpaceShip ship)
    {
        if (ship.m_moduleHulls.Count == 0 || ship.m_moduleHulls[0] == null) return "";
        return ship.m_moduleHulls[0].GetHullArchetype();
    }

    private static string BuildShipKey(SpaceShip ship)
    {
        string source = ship.m_ownerFleet != null ? ship.m_ownerFleet.m_fleetSource.ToString() : "?";
        string subType = ship.m_shipInfo != null ? ship.m_shipInfo.hullSubType : "?";
        return $"{source}:{GetArchetype(ship)}({subType})";
    }

    private static string BuildFleetKey(SpaceFleet fleet)
    {
        return $"{fleet.m_fleetSource}:fleet";
    }
}
