// 장착 로드아웃(슬롯 배열) → 최종 전투 수치 변환기 — 기본값은 DataTableModule 원본 데이터 그대로
// upgradeArchetype을 넘기면 아키타입 단위 영구 모듈강화(M6) 레벨 행({type}_{level}_1, 함체는 hull_{tier}_{archetype}_{level})의 수치를 사용 —
// 내 함대/내 함선 표시에만 넘기고, 적 함대는 null(강화 없음)
// 이 클래스는 DataTableConfig를 직접 조회하지 않음 — 호출부가 moduleTable을 꺼내 넘겨줌
public static class ShipStatCalculator
{
    // bodyModuleData: 함체(hullSubType, body subType)에 대응하는 DataTableModule 원본 데이터 — 체력/수리력/선회력의 기본 수치 출처
    // null이면(조회 실패 등) 기본 수치 0으로 처리
    // moduleTable: 슬롯에 장착된 무기/실드/요격체 서브타입별 기본 수치(공격력/쿨다운/발사체속도 등) 조회용
    // upgradeArchetype: 아키타입 강화 레벨을 적용할 함체 아키타입 — null/빈 문자열이면 강화 없이 서브타입 원본 수치
    // buildData: 강화 레벨을 읽을 지휘관 빌드 데이터 — null이면 내 지휘관 기준(상대 지휘관 함대를 계산할 때만 넘김)
    public static ShipFinalStats Calculate(ShipStatAllocation allocation, ShipStatFormulaSettings formula, ModuleData bodyModuleData = null, DataTableModule moduleTable = null, string upgradeArchetype = null, CommanderBuildData buildData = null)
    {
        ShipFinalStats stats = new ShipFinalStats();

        float baseHealth = 0f;
        float baseRepair = 0f;
        float baseTurnRate = 0f;
        if (bodyModuleData != null)
        {
            ModuleData bodyLevelData = GetHullLevelData(moduleTable, bodyModuleData, upgradeArchetype, buildData);
            baseHealth = bodyLevelData.health;
            baseRepair = bodyLevelData.repair;
            baseTurnRate = bodyModuleData.turnRate;
        }

        stats.health = baseHealth;
        stats.turnRate = baseTurnRate;
        stats.repair = baseRepair;

        CalculateWeaponSlots(moduleTable, EModuleType.beam, upgradeArchetype, buildData, allocation.beamModuleSubType, out stats.beamAttacks, out stats.beamAttackCools, out stats.beamProjectileSpeeds, out stats.beamModuleSubType);
        CalculateWeaponSlots(moduleTable, EModuleType.missile, upgradeArchetype, buildData, allocation.missileModuleSubType, out stats.missileAttacks, out stats.missileAttackCools, out stats.missileProjectileSpeeds, out stats.missileModuleSubType);
        stats.missileSilenceTimes = CalculateMissileSilenceSlots(moduleTable, upgradeArchetype, buildData, allocation.missileModuleSubType);

        CalculateHangarSlots(moduleTable, upgradeArchetype, buildData, allocation, out stats.hangarShipAttacks, out stats.hangarFighterAttacks, out stats.hangarAmmos, out stats.hangarHealths, out stats.hangarAirDisrupts, out stats.hangarModuleSubType);

        stats.shieldInstalled = string.IsNullOrEmpty(allocation.shieldModuleSubType) == false;
        if (stats.shieldInstalled)
        {
            ModuleData shieldSubTypeData = GetModuleData(moduleTable, allocation.shieldModuleSubType);
            ModuleData shieldModuleData = shieldSubTypeData != null ? GetSlotLevelData(moduleTable, upgradeArchetype, buildData, EModuleType.shield, 0, shieldSubTypeData) : null;
            stats.shieldGauge = shieldModuleData != null ? shieldModuleData.shieldGauge : 0f;
            stats.shieldRegenRate = shieldModuleData != null ? shieldModuleData.shieldRegenRate : 0f;
        }

        CalculateInterceptorSlots(moduleTable, upgradeArchetype, buildData, allocation, out stats.interceptorRegenTimes, out stats.interceptorCounts, out stats.interceptorModuleSubType);

        return stats;
    }

    // subTypeName(예: beam_1_1)에 대응하는 DataTableModule 원본 데이터 조회 — moduleTable 또는 이름이 비어있으면 null
    private static ModuleData GetModuleData(DataTableModule moduleTable, string subTypeName)
    {
        if (moduleTable == null || string.IsNullOrEmpty(subTypeName)) return null;
        return moduleTable.GetModuleDataFromTable(subTypeName);
    }

    // 강화 레벨 출처 — 빌드 데이터가 있으면 그 지휘관, 없으면 내 지휘관
    private static int GetUpgradeLevel(CommanderBuildData buildData, string upgradeArchetype, EModuleType moduleType, int slotIndex)
    {
        if (buildData != null) return ArchetypeUpgradeLevelLookup.GetLevel(buildData, upgradeArchetype, moduleType, slotIndex);
        return ArchetypeUpgradeLevelLookup.GetLevel(upgradeArchetype, moduleType, slotIndex);
    }

    // 함체 체력/수리력의 출처 행 — 강화 아키타입이 있으면 현재 강화 레벨 행(hull_{tier}_{archetype}_{level}), 없거나 행이 없으면 함체 원본
    private static ModuleData GetHullLevelData(DataTableModule moduleTable, ModuleData bodyModuleData, string upgradeArchetype, CommanderBuildData buildData)
    {
        if (string.IsNullOrEmpty(upgradeArchetype) == true) return bodyModuleData;

        int level = GetUpgradeLevel(buildData, upgradeArchetype, EModuleType.hull, 0);
        string levelKey = CommonUtility.BuildHullArchetypeLevelKey(bodyModuleData.moduleSubType, level);
        ModuleData levelData = GetModuleData(moduleTable, levelKey);
        return levelData != null ? levelData : bodyModuleData;
    }

    // 슬롯 모듈의 강화 수치 출처 행 — 강화 아키타입이 있으면 {type}_{level}_1(슬롯별 레벨), 없거나 행이 없으면 장착 서브타입 원본
    private static ModuleData GetSlotLevelData(DataTableModule moduleTable, string upgradeArchetype, CommanderBuildData buildData, EModuleType moduleType, int slotIndex, ModuleData subTypeData)
    {
        if (string.IsNullOrEmpty(upgradeArchetype) == true) return subTypeData;

        int level = GetUpgradeLevel(buildData, upgradeArchetype, moduleType, slotIndex);
        ModuleData levelData = GetModuleData(moduleTable, $"{moduleType}_{level}_1");
        return levelData != null ? levelData : subTypeData;
    }

    // 빔/미사일 공용 — 슬롯별 공격력/연사력(쿨다운)/발사체속도. 출력 배열은 슬롯 인덱스와 1:1(빈 슬롯은 0, 서브타입 "")
    private static void CalculateWeaponSlots(DataTableModule moduleTable, EModuleType moduleType, string upgradeArchetype, CommanderBuildData buildData, string[] moduleSubType,
        out float[] attacks, out float[] attackCools, out float[] projectileSpeeds, out string[] slotSubType)
    {
        int slotCount = moduleSubType.Length;
        attacks = new float[slotCount];
        attackCools = new float[slotCount];
        projectileSpeeds = new float[slotCount];
        slotSubType = new string[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            slotSubType[i] = "";
            if (string.IsNullOrEmpty(moduleSubType[i])) continue;

            ModuleData moduleData = GetModuleData(moduleTable, moduleSubType[i]);
            ModuleData levelData = moduleData != null ? GetSlotLevelData(moduleTable, upgradeArchetype, buildData, moduleType, i, moduleData) : null;
            attacks[i] = levelData != null ? levelData.attack : 0f;
            attackCools[i] = levelData != null ? levelData.attackCool : 0f;
            projectileSpeeds[i] = moduleData != null ? moduleData.speed : 0f;
            slotSubType[i] = moduleSubType[i];
        }
    }

    // 미사일 전용 — 적중 시 대상 무장 침묵 시간. 기본 수치는 장착 서브타입의 DataTableModule.silenceTime에서 조회
    private static float[] CalculateMissileSilenceSlots(DataTableModule moduleTable, string upgradeArchetype, CommanderBuildData buildData, string[] moduleSubType)
    {
        float[] result = new float[moduleSubType.Length];
        for (int i = 0; i < moduleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(moduleSubType[i])) continue;
            ModuleData moduleData = GetModuleData(moduleTable, moduleSubType[i]);
            ModuleData levelData = moduleData != null ? GetSlotLevelData(moduleTable, upgradeArchetype, buildData, EModuleType.missile, i, moduleData) : null;
            result[i] = levelData != null ? levelData.silenceTime : 0f;
        }
        return result;
    }

    private static void CalculateHangarSlots(DataTableModule moduleTable, string upgradeArchetype, CommanderBuildData buildData, ShipStatAllocation allocation,
        out float[] shipAttacks, out float[] fighterAttacks, out float[] ammos, out float[] healths, out float[] airDisrupts, out string[] slotSubType)
    {
        int slotCount = allocation.hangarModuleSubType.Length;
        shipAttacks = new float[slotCount];
        fighterAttacks = new float[slotCount];
        ammos = new float[slotCount];
        healths = new float[slotCount];
        airDisrupts = new float[slotCount];
        slotSubType = new string[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            slotSubType[i] = "";
            if (string.IsNullOrEmpty(allocation.hangarModuleSubType[i])) continue;

            ModuleData hangarSubTypeData = GetModuleData(moduleTable, allocation.hangarModuleSubType[i]);
            ModuleData hangarModuleData = hangarSubTypeData != null ? GetSlotLevelData(moduleTable, upgradeArchetype, buildData, EModuleType.hangar, i, hangarSubTypeData) : null;
            shipAttacks[i] = hangarModuleData != null ? hangarModuleData.airAttackToShip : 0f;
            fighterAttacks[i] = hangarModuleData != null ? hangarModuleData.airAttackToFighter : 0f;
            ammos[i] = hangarModuleData != null ? hangarModuleData.airAmmo : 0f;
            healths[i] = hangarModuleData != null ? hangarModuleData.airHealth : 0f;
            airDisrupts[i] = hangarModuleData != null ? hangarModuleData.airDisrupt : 0f;
            slotSubType[i] = allocation.hangarModuleSubType[i];
        }
    }

    // 개수/회복시간은 강화 레벨 행(interceptor_{level}_1, 슬롯 개념 없어 slotIndex 0), 없으면 장착 서브타입 원본. 슬롯 인덱스와 1:1(빈 슬롯 0, 서브타입 "")
    private static void CalculateInterceptorSlots(DataTableModule moduleTable, string upgradeArchetype, CommanderBuildData buildData, ShipStatAllocation allocation,
        out float[] regenTimes, out float[] counts, out string[] slotSubType)
    {
        int slotCount = allocation.interceptorModuleSubType.Length;
        regenTimes = new float[slotCount];
        counts = new float[slotCount];
        slotSubType = new string[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            slotSubType[i] = "";
            if (string.IsNullOrEmpty(allocation.interceptorModuleSubType[i])) continue;

            ModuleData subTypeData = GetModuleData(moduleTable, allocation.interceptorModuleSubType[i]);
            ModuleData moduleData = subTypeData != null ? GetSlotLevelData(moduleTable, upgradeArchetype, buildData, EModuleType.interceptor, 0, subTypeData) : null;
            counts[i] = moduleData != null ? moduleData.interceptorCount : 0f;
            regenTimes[i] = moduleData != null ? moduleData.interceptorRegenTime : 0f;
            slotSubType[i] = allocation.interceptorModuleSubType[i];
        }
    }
}
