// 패시브 자동 스폰 테스트(플레이 모드 전용, 서버 불필요) — 아키타입 전체 × 패시브 완료 단계 × 강화 레벨 조합으로 가짜 PVP 상대 함대를 스폰하고
// PassiveAudit의 [Data]/[Spawn]/[Calc] 검증을 돌린 뒤 바로 파괴. 조용한 모드라 문제 있는 줄만 콘솔에 남고 마지막에 요약 1줄
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PassiveAuditSpawnTest
{
    private const int k_maxShipsPerFleet = 7;
    private const int k_stageMixed = -1;        // 아키타입마다 완료 단계를 다르게(1~4 순환)
    private const int k_upgradeNone = 0;
    private const int k_upgradeMax = 1;
    private const int k_upgradeVaried = 2;      // 아키타입/모듈/슬롯마다 다른 레벨

    [MenuItem("Tools/PassiveAudit/Spawn Test (Play Mode)")]
    public static void RunFromMenu()
    {
        Debug.Log(RunAll());
    }

    public static string RunAll()
    {
        if (Application.isPlaying == false) return "[PassiveAuditTest] 플레이 모드에서 실행해야 함";
        if (ObjectManager.Instance == null || DataManager.Instance == null || DataManager.Instance.m_dataTableHullArchetypePassive == null)
            return "[PassiveAuditTest] 매니저/데이터 미준비";

        List<HullArchetypePassiveData> passiveList = DataManager.Instance.m_dataTableHullArchetypePassive.GetPassiveDataList();
        List<string> archetypes = CollectArchetypes(passiveList);
        Dictionary<string, string> hullByArchetype = CollectHullSubTypes(archetypes);
        List<string> missingHull = new List<string>();
        for (int i = 0; i < archetypes.Count; i++)
        {
            if (hullByArchetype.ContainsKey(archetypes[i]) == false) missingHull.Add(archetypes[i]);
        }

        PassiveAudit.s_quiet = true;
        PassiveAudit.s_problemTotal = 0;
        PassiveAudit.AuditTableForTest();

        int fleetCount = 0;
        int shipCount = 0;
        int[] stageScenarios = { 1, 2, 3, 4, k_stageMixed };
        int[] upgradeScenarios = { k_upgradeNone, k_upgradeMax, k_upgradeVaried };

        List<List<string>> groups = SplitGroups(archetypes, hullByArchetype);
        for (int s = 0; s < stageScenarios.Length; s++)
        {
            for (int u = 0; u < upgradeScenarios.Length; u++)
            {
                CommanderBuildData buildData = BuildFakeBuildData(passiveList, archetypes, hullByArchetype, stageScenarios[s], upgradeScenarios[u]);
                for (int g = 0; g < groups.Count; g++)
                {
                    Debug.Log($"[PassiveAuditTest] 시나리오 단계={StageName(stageScenarios[s])} 강화={UpgradeName(upgradeScenarios[u])} 그룹={string.Join(",", groups[g])}");
                    shipCount += SpawnAuditDestroy(groups[g], hullByArchetype, buildData, archetypes, passiveList);
                    fleetCount++;
                }
            }
        }

        // 단독 함대 — 함대 범위 효과가 그 아키타입이 없을 때 새지 않는지(자기 효과만 남는지)
        CommanderBuildData soloData = BuildFakeBuildData(passiveList, archetypes, hullByArchetype, 4, k_upgradeVaried);
        for (int i = 0; i < archetypes.Count; i++)
        {
            if (hullByArchetype.ContainsKey(archetypes[i]) == false) continue;
            shipCount += SpawnAuditDestroy(new List<string> { archetypes[i] }, hullByArchetype, soloData, archetypes, passiveList);
            fleetCount++;
        }

        PassiveAudit.s_quiet = false;
        string missingText = missingHull.Count > 0 ? $" | 함체 없는 아키타입: {string.Join(",", missingHull)}" : "";
        return $"[PassiveAuditTest] 완료 — 함대 {fleetCount}개, 함선 {shipCount}척, 문제 {PassiveAudit.s_problemTotal}건{missingText}";
    }

    // 검증기 자가 점검 — 정상 함대는 문제 0, 일부러 망가뜨린 값(빔 공격력/최대 체력/진행도)은 반드시 잡아야 통과
    public static string RunSelfCheck()
    {
        if (Application.isPlaying == false) return "[PassiveAuditTest] 플레이 모드에서 실행해야 함";

        List<HullArchetypePassiveData> passiveList = DataManager.Instance.m_dataTableHullArchetypePassive.GetPassiveDataList();
        List<string> archetypes = CollectArchetypes(passiveList);
        Dictionary<string, string> hullByArchetype = CollectHullSubTypes(archetypes);
        List<List<string>> groups = SplitGroups(archetypes, hullByArchetype);
        CommanderBuildData buildData = BuildFakeBuildData(passiveList, archetypes, hullByArchetype, 4, k_upgradeVaried);

        FleetInfo fleetInfo = new FleetInfo { fleetName = "PassiveAuditSelfCheck", tacticOptions = 63, ships = new List<ShipInfo>() };
        for (int i = 0; i < groups[0].Count; i++)
        {
            string hullSubType = hullByArchetype[groups[0][i]];
            fleetInfo.ships.Add(new ShipInfo { id = i + 1, positionIndex = i, shipName = hullSubType, hullSubType = hullSubType, isFront = true,
                hulls = new List<ModuleHullInfo> { BuildModules(hullSubType, groups[0][i], buildData, passiveList) } });
        }

        GameObject fleetObj = new GameObject("PassiveAuditSelfCheckFleet");
        fleetObj.transform.position = new Vector3(0f, -5000f, 0f);
        SpaceFleet fleet = fleetObj.AddComponent<SpaceFleet>();
        fleet.m_remoteBuildData = buildData;
        fleet.InitializeSpaceFleet(fleetInfo, ObjectManager.Instance.GetOpposingTeam(ObjectManager.Instance.m_myTeam), EFleetSource.fleet_source_player_remote, EUnitState.Idle);

        // 1) 정상 — 전체 로그 출력(검증 내용 눈으로 확인용)
        PassiveAudit.s_quiet = false;
        PassiveAudit.s_problemTotal = 0;
        PassiveAudit.AuditFleetForTest(fleet);
        int cleanProblems = PassiveAudit.s_problemTotal;

        // 2) 저장 능력치 변조 — 빔 공격력/최대 체력
        int tamperedValues = 0;
        for (int s = 0; s < fleet.m_ships.Count && tamperedValues < 2; s++)
        {
            ModuleHull body = fleet.m_ships[s].m_moduleHulls[0];
            if (body.m_beams.Count > 0 && tamperedValues == 0) { body.m_beams[0].m_attack *= 2f; tamperedValues++; }
            else if (tamperedValues == 1) { body.m_healthMax *= 1.5f; tamperedValues++; }
        }
        PassiveAudit.s_quiet = true;
        PassiveAudit.s_problemTotal = 0;
        PassiveAudit.AuditFleetForTest(fleet);
        int statProblems = PassiveAudit.s_problemTotal;

        // 3) 진행도 변조 — 완료인데 counter가 문턱 미만인 단계
        HullArchetypeProgressInfo tamperedEntry = buildData.passiveProgress.Find(p => p.stage == 2);
        long originalCounter = tamperedEntry.counterValue;
        tamperedEntry.counterValue = 0;
        PassiveAudit.s_problemTotal = 0;
        PassiveAudit.AuditFleetForTest(fleet);
        int dataProblems = PassiveAudit.s_problemTotal;
        tamperedEntry.counterValue = originalCounter;

        PassiveAudit.s_quiet = false;
        fleet.ClearAllInterceptorUnits();
        Object.Destroy(fleetObj);

        bool isPass = cleanProblems == 0 && statProblems >= tamperedValues && tamperedValues == 2 && dataProblems > statProblems;
        return $"[PassiveAuditTest] 자가 점검 {(isPass == true ? "통과" : "실패")} — 정상 문제={cleanProblems}(0이어야), 능력치 변조 {tamperedValues}건 → 검출={statProblems}, 진행도 변조 추가 → 검출={dataProblems}";
    }

    private static int SpawnAuditDestroy(List<string> groupArchetypes, Dictionary<string, string> hullByArchetype, CommanderBuildData buildData, List<string> allArchetypes, List<HullArchetypePassiveData> passiveList)
    {
        FleetInfo fleetInfo = new FleetInfo { fleetName = "PassiveAuditTest", tacticOptions = 63, ships = new List<ShipInfo>() };
        for (int i = 0; i < groupArchetypes.Count; i++)
        {
            string archetype = groupArchetypes[i];
            string hullSubType = hullByArchetype[archetype];
            fleetInfo.ships.Add(new ShipInfo
            {
                id = i + 1,
                positionIndex = i,
                shipName = hullSubType,
                hullSubType = hullSubType,
                isFront = i % 2 == 0,
                hulls = new List<ModuleHullInfo> { BuildModules(hullSubType, archetype, buildData, passiveList) },
            });
        }

        GameObject fleetObj = new GameObject("PassiveAuditTestFleet");
        fleetObj.transform.position = new Vector3(0f, -5000f, 0f); // 화면/전투 대상 밖
        SpaceFleet fleet = fleetObj.AddComponent<SpaceFleet>();
        fleet.m_remoteBuildData = buildData;
        ETeam enemyTeam = ObjectManager.Instance.GetOpposingTeam(ObjectManager.Instance.m_myTeam);
        fleet.InitializeSpaceFleet(fleetInfo, enemyTeam, EFleetSource.fleet_source_player_remote, EUnitState.Idle);

        PassiveAudit.AuditFleetForTest(fleet);

        int spawned = fleet.m_ships.Count;
        fleet.ClearAllInterceptorUnits();
        Object.Destroy(fleetObj);
        return spawned;
    }

    // 모든 구조 슬롯에 1레벨 기본 모듈, 실드/요격체는 슬롯이 있고 가짜 진행도로 해금됐을 때만(서버 buildModuleHullInfoDto 규칙)
    private static ModuleHullInfo BuildModules(string hullSubType, string archetype, CommanderBuildData buildData, List<HullArchetypePassiveData> passiveList)
    {
        int[] maxSlots = FleetComposition.ParseMaxSlotsFromHullSubType(hullSubType);
        ModuleHullInfo hull = new ModuleHullInfo
        {
            moduleType = EModuleType.hull,
            moduleSubType = hullSubType,
            moduleLevel = 1,
            beams = BuildSlotModules(EModuleType.beam, maxSlots[0]),
            missiles = BuildSlotModules(EModuleType.missile, maxSlots[1]),
            hangars = BuildSlotModules(EModuleType.hangar, maxSlots[2]),
            shieldModuleSubType = "",
            interceptorModuleSubType = "",
        };

        if (maxSlots[3] > 0 && IsUnlocked(buildData, passiveList, archetype, "unlock_shield") == true) hull.shieldModuleSubType = "shield_1_1";
        if (maxSlots[4] > 0 && IsUnlocked(buildData, passiveList, archetype, "unlock_interceptor") == true) hull.interceptorModuleSubType = "interceptor_1_1";
        return hull;
    }

    private static List<ModuleInfo> BuildSlotModules(EModuleType moduleType, int count)
    {
        List<ModuleInfo> modules = new List<ModuleInfo>();
        for (int i = 0; i < count; i++)
            modules.Add(new ModuleInfo { moduleType = moduleType, moduleSubType = $"{moduleType}_1_1", moduleLevel = 1, slotIndex = i });
        return modules;
    }

    private static bool IsUnlocked(CommanderBuildData buildData, List<HullArchetypePassiveData> passiveList, string archetype, string specificType)
    {
        for (int i = 0; i < passiveList.Count; i++)
        {
            HullArchetypePassiveData passive = passiveList[i];
            if (passive.hullArchetype != archetype) continue;
            if (buildData.IsPassiveStageCompleted(passive.hullArchetype, passive.stage, passive.threshold) == false) continue;

            List<HullArchetypePassiveEffect> effects = passive.GetEffects();
            for (int j = 0; j < effects.Count; j++)
            {
                if (effects[j].effectType == specificType || effects[j].effectType == "unlock_shield_interceptor") return true;
            }
        }
        return false;
    }

    // 가짜 지휘관 빌드 데이터 — stageScenario 단계까지 완료(counter=문턱), 강화는 시나리오별 레벨
    private static CommanderBuildData BuildFakeBuildData(List<HullArchetypePassiveData> passiveList, List<string> archetypes, Dictionary<string, string> hullByArchetype, int stageScenario, int upgradeScenario)
    {
        List<HullArchetypeProgressInfo> progress = new List<HullArchetypeProgressInfo>();
        for (int i = 0; i < passiveList.Count; i++)
        {
            HullArchetypePassiveData passive = passiveList[i];
            int archetypeIndex = archetypes.IndexOf(passive.hullArchetype);
            int completedUpTo = stageScenario == k_stageMixed ? (archetypeIndex % 4) + 1 : stageScenario;
            bool isCompleted = passive.stage <= completedUpTo;
            progress.Add(new HullArchetypeProgressInfo
            {
                hullArchetype = passive.hullArchetype,
                stage = passive.stage,
                counterType = passive.counterType,
                counterValue = isCompleted == true ? passive.threshold : 0,
                completed = isCompleted,
            });
        }

        List<ArchetypeUpgradeInfo> upgrades = new List<ArchetypeUpgradeInfo>();
        if (upgradeScenario != k_upgradeNone)
        {
            int maxLevel = DataManager.Instance.m_dataTableConfig.gameSettings.archetypeUpgrade.maxLevel;
            for (int a = 0; a < archetypes.Count; a++)
            {
                string hullSubType;
                if (hullByArchetype.TryGetValue(archetypes[a], out hullSubType) == false) continue;

                int[] maxSlots = FleetComposition.ParseMaxSlotsFromHullSubType(hullSubType);
                AddUpgrade(upgrades, archetypes[a], EModuleType.hull, 0, a, 0, upgradeScenario, maxLevel);
                AddUpgrade(upgrades, archetypes[a], EModuleType.shield, 0, a, 4, upgradeScenario, maxLevel);
                AddUpgrade(upgrades, archetypes[a], EModuleType.interceptor, 0, a, 5, upgradeScenario, maxLevel);
                for (int slot = 0; slot < maxSlots[0]; slot++) AddUpgrade(upgrades, archetypes[a], EModuleType.beam, slot, a, 1, upgradeScenario, maxLevel);
                for (int slot = 0; slot < maxSlots[1]; slot++) AddUpgrade(upgrades, archetypes[a], EModuleType.missile, slot, a, 2, upgradeScenario, maxLevel);
                for (int slot = 0; slot < maxSlots[2]; slot++) AddUpgrade(upgrades, archetypes[a], EModuleType.hangar, slot, a, 3, upgradeScenario, maxLevel);
            }
        }

        return new CommanderBuildData(progress, upgrades);
    }

    // 저장값 level = 효과 레벨 - 1(구매 횟수)
    private static void AddUpgrade(List<ArchetypeUpgradeInfo> upgrades, string archetype, EModuleType moduleType, int slotIndex, int archetypeIndex, int typeIndex, int upgradeScenario, int maxLevel)
    {
        int effectLevel = upgradeScenario == k_upgradeMax ? maxLevel : 1 + (archetypeIndex + typeIndex + slotIndex) % maxLevel;
        upgrades.Add(new ArchetypeUpgradeInfo { hullArchetype = archetype, moduleType = moduleType, slotIndex = slotIndex, level = effectLevel - 1 });
    }

    private static List<string> CollectArchetypes(List<HullArchetypePassiveData> passiveList)
    {
        List<string> archetypes = new List<string>();
        for (int i = 0; i < passiveList.Count; i++)
        {
            if (archetypes.Contains(passiveList[i].hullArchetype) == false) archetypes.Add(passiveList[i].hullArchetype);
        }
        return archetypes;
    }

    // 아키타입마다 플레이어용 함체 하나(테이블 첫 행)
    private static Dictionary<string, string> CollectHullSubTypes(List<string> archetypes)
    {
        Dictionary<string, string> result = new Dictionary<string, string>();
        List<ModuleData> hulls = DataManager.Instance.m_dataTableModule.HullModules.modules;
        for (int i = 0; i < hulls.Count; i++)
        {
            ModuleData hull = hulls[i];
            if (hull.isEnemyOnly == true || string.IsNullOrEmpty(hull.hullArchetype) == true) continue;
            if (archetypes.Contains(hull.hullArchetype) == false || result.ContainsKey(hull.hullArchetype) == true) continue;
            result[hull.hullArchetype] = hull.moduleSubType;
        }
        return result;
    }

    private static List<List<string>> SplitGroups(List<string> archetypes, Dictionary<string, string> hullByArchetype)
    {
        List<List<string>> groups = new List<List<string>>();
        List<string> current = new List<string>();
        for (int i = 0; i < archetypes.Count; i++)
        {
            if (hullByArchetype.ContainsKey(archetypes[i]) == false) continue;
            current.Add(archetypes[i]);
            if (current.Count >= k_maxShipsPerFleet)
            {
                groups.Add(current);
                current = new List<string>();
            }
        }
        if (current.Count > 0) groups.Add(current);
        return groups;
    }

    private static string StageName(int stageScenario)
    {
        return stageScenario == k_stageMixed ? "혼합" : $"{stageScenario}단계까지";
    }

    private static string UpgradeName(int upgradeScenario)
    {
        if (upgradeScenario == k_upgradeMax) return "최대";
        if (upgradeScenario == k_upgradeVaried) return "다양";
        return "없음";
    }
}
