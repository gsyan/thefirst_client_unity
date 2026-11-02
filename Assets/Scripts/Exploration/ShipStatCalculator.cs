// 장착 로드아웃(슬롯 배열) → 최종 전투 수치 변환기 — 기본값은 DataTableModule 원본 데이터 그대로(강화 없음)
// 이 클래스는 DataTableConfig를 직접 조회하지 않음 — 호출부가 moduleTable을 꺼내 넘겨줌 (순수 함수 유지)
public static class ShipStatCalculator
{
    // bodyModuleData: 함체(hullSubType, body subType)에 대응하는 DataTableModule 원본 데이터 — 체력/수리력/선회력의 기본 수치 출처
    // null이면(조회 실패 등) 기본 수치 0으로 처리
    // moduleTable: 슬롯에 장착된 무기/실드/요격체 서브타입별 기본 수치(공격력/쿨다운/발사체속도 등) 조회용
    public static ShipFinalStats Calculate(ShipStatAllocation allocation, ShipStatFormulaSettings formula, ModuleData bodyModuleData = null, DataTableModule moduleTable = null)
    {
        ShipFinalStats stats = new ShipFinalStats();

        float baseHealth = 0f;
        float baseRepair = 0f;
        float baseTurnRate = 0f;
        if (bodyModuleData != null)
        {
            baseHealth = bodyModuleData.health;
            baseRepair = bodyModuleData.repair;
            baseTurnRate = bodyModuleData.turnRate;
        }

        stats.health = baseHealth;
        stats.turnRate = baseTurnRate;
        stats.repair = baseRepair;

        CalculateWeaponSlots(moduleTable, allocation.beamModuleSubType, out stats.beamAttacks, out stats.beamAttackCools, out stats.beamProjectileSpeeds, out stats.beamModuleSubType);
        CalculateWeaponSlots(moduleTable, allocation.missileModuleSubType, out stats.missileAttacks, out stats.missileAttackCools, out stats.missileProjectileSpeeds, out stats.missileModuleSubType);
        stats.missileSilenceTimes = CalculateMissileSilenceSlots(moduleTable, allocation.missileModuleSubType);

        CalculateHangarSlots(moduleTable, allocation, out stats.hangarShipAttacks, out stats.hangarFighterAttacks, out stats.hangarAmmos, out stats.hangarHealths, out stats.hangarAirDisrupts, out stats.hangarModuleSubType);

        stats.shieldInstalled = string.IsNullOrEmpty(allocation.shieldModuleSubType) == false;
        if (stats.shieldInstalled)
        {
            ModuleData shieldModuleData = GetModuleData(moduleTable, allocation.shieldModuleSubType);
            stats.shieldGauge = shieldModuleData != null ? shieldModuleData.shieldGauge : 0f;
            stats.shieldRegenRate = shieldModuleData != null ? shieldModuleData.shieldRegenRate : 0f;
        }

        CalculateInterceptorSlots(moduleTable, allocation, out stats.interceptorRegenTimes, out stats.interceptorCounts, out stats.interceptorModuleSubType);

        return stats;
    }

    // subTypeName(예: beam_1_1)에 대응하는 DataTableModule 원본 데이터 조회 — moduleTable 또는 이름이 비어있으면 null
    private static ModuleData GetModuleData(DataTableModule moduleTable, string subTypeName)
    {
        if (moduleTable == null || string.IsNullOrEmpty(subTypeName)) return null;
        return moduleTable.GetModuleDataFromTable(subTypeName);
    }

    // 빔/미사일 공용 — 슬롯별 공격력/연사력(쿨다운)/발사체속도. 출력 배열은 슬롯 인덱스와 1:1(빈 슬롯은 0, 서브타입 "")
    private static void CalculateWeaponSlots(DataTableModule moduleTable, string[] moduleSubType,
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
            attacks[i] = moduleData != null ? moduleData.attack : 0f;
            attackCools[i] = moduleData != null ? moduleData.attackCool : 0f;
            projectileSpeeds[i] = moduleData != null ? moduleData.speed : 0f;
            slotSubType[i] = moduleSubType[i];
        }
    }

    // 미사일 전용 — 적중 시 대상 무장 침묵 시간. 기본 수치는 장착 서브타입의 DataTableModule.silenceTime에서 조회
    private static float[] CalculateMissileSilenceSlots(DataTableModule moduleTable, string[] moduleSubType)
    {
        float[] result = new float[moduleSubType.Length];
        for (int i = 0; i < moduleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(moduleSubType[i])) continue;
            ModuleData moduleData = GetModuleData(moduleTable, moduleSubType[i]);
            result[i] = moduleData != null ? moduleData.silenceTime : 0f;
        }
        return result;
    }

    private static void CalculateHangarSlots(DataTableModule moduleTable, ShipStatAllocation allocation,
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

            ModuleData hangarModuleData = GetModuleData(moduleTable, allocation.hangarModuleSubType[i]);
            shipAttacks[i] = hangarModuleData != null ? hangarModuleData.airAttackToShip : 0f;
            fighterAttacks[i] = hangarModuleData != null ? hangarModuleData.airAttackToFighter : 0f;
            ammos[i] = hangarModuleData != null ? hangarModuleData.airAmmo : 0f;
            healths[i] = hangarModuleData != null ? hangarModuleData.airHealth : 0f;
            airDisrupts[i] = hangarModuleData != null ? hangarModuleData.airDisrupt : 0f;
            slotSubType[i] = allocation.hangarModuleSubType[i];
        }
    }

    // 기본 수치(회복시간)는 장착 서브타입의 DataTableModule.interceptorRegenTime에서 조회. 슬롯 인덱스와 1:1(빈 슬롯 0, 서브타입 "")
    private static void CalculateInterceptorSlots(DataTableModule moduleTable, ShipStatAllocation allocation,
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

            ModuleData moduleData = GetModuleData(moduleTable, allocation.interceptorModuleSubType[i]);
            counts[i] = moduleData != null ? moduleData.interceptorCount : 0f;
            regenTimes[i] = moduleData != null ? moduleData.interceptorRegenTime : 0f;
            slotSubType[i] = allocation.interceptorModuleSubType[i];
        }
    }
}
