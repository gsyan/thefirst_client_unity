// 성능포인트 배분(슬롯 배열, 총량은 함체마다 가변) → 최종 전투 수치 변환기
// 계수/기본값은 DataTableConfig.gameSettings.shipStatFormula(ShipStatFormulaSettings)에서 주입받음 — 하드코딩 없음
// 이 클래스는 DataTableConfig를 직접 조회하지 않음 — 호출부가 formula를 꺼내 넘겨줌 (순수 함수 유지)
using System.Collections.Generic;

public static class ShipStatCalculator
{
    // bodyModuleData: 함체(hullSubType, body subType)에 대응하는 DataTableModule 원본 데이터 — 체력/수리력/선회력의 기본 수치 출처
    // null이면(조회 실패 등) 기본 수치 0으로 처리 — 강화 포인트만 반영
    // moduleTable: 슬롯에 장착된 무기/실드/요격체 서브타입별 기본 수치(공격력/쿨다운/발사체속도 등) 조회용 — formula는 포인트당 증감 계수만 제공
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

        CalculateWeaponSlots(moduleTable, allocation.beamModuleSubType, allocation.beamAttackPoints, allocation.beamFireRatePoints,
            formula, formula.beam.maxAttackBonusRatio, formula.beam.maxCoolReductionRatio, formula.beam.attackCoolFloor,
            out stats.beamAttacks, out stats.beamAttackCools, out stats.beamProjectileSpeeds, out stats.beamModuleSubType);
        CalculateWeaponSlots(moduleTable, allocation.missileModuleSubType, allocation.missileAttackPoints, allocation.missileFireRatePoints,
            formula, formula.missile.maxAttackBonusRatio, formula.missile.maxCoolReductionRatio, formula.missile.attackCoolFloor,
            out stats.missileAttacks, out stats.missileAttackCools, out stats.missileProjectileSpeeds, out stats.missileModuleSubType);
        stats.missileSilenceTimes = CalculateMissileSilenceSlots(moduleTable, allocation.missileModuleSubType, allocation.missileSilencePoints, formula);

        CalculateHangarSlots(moduleTable, allocation, formula, out stats.hangarShipAttacks, out stats.hangarFighterAttacks, out stats.hangarAmmos, out stats.hangarHealths, out stats.hangarAirDisrupts, out stats.hangarModuleSubType);

        stats.shieldInstalled = string.IsNullOrEmpty(allocation.shieldModuleSubType) == false;
        if (stats.shieldInstalled)
        {
            ModuleData shieldModuleData = GetModuleData(moduleTable, allocation.shieldModuleSubType);
            float baseShieldGauge = shieldModuleData != null ? shieldModuleData.shieldGauge : 0f;
            float baseShieldRegenRate = shieldModuleData != null ? shieldModuleData.shieldRegenRate : 0f;

            stats.shieldGauge = ComputeShieldGauge(baseShieldGauge, allocation.shieldGaugePoints, formula);
            stats.shieldRegenRate = ComputeShieldRegenRate(baseShieldRegenRate, allocation.shieldRegenRatePoints, formula);
        }

        CalculateInterceptorSlots(moduleTable, allocation, formula, out stats.interceptorRegenTimes, out stats.interceptorCounts, out stats.interceptorModuleSubType);

        return stats;
    }

    // 강화 공식 공용 — 최종값 = 기본값 × (1 + 상한 도달 시 증가비율 × 투자포인트 / 슬롯 상한). 성능 표시/강화 팝업/전투 모듈이 모두 이 함수를 사용
    public static float ComputeBoostedValue(float baseValue, int points, float maxBonusRatio, ShipStatFormulaSettings formula)
    {
        int maxPoints = formula.maxAttackReinforcePointsPerSlot;
        if (maxPoints <= 0) return baseValue;

        float reinforceProgress = (float)points / maxPoints;
        return baseValue * (1f + maxBonusRatio * reinforceProgress);
    }

    // 연사력 공용 — 쿨다운 = max(하한, 기본값 × (1 - 상한 도달 시 감소비율 × 투자포인트 / 슬롯 상한))
    public static float ComputeReducedCooldown(float baseCool, int points, float maxCoolReductionRatio, float coolFloor, ShipStatFormulaSettings formula)
    {
        int maxPoints = formula.maxAttackReinforcePointsPerSlot;
        if (maxPoints <= 0) return baseCool;

        float reinforceProgress = (float)points / maxPoints;
        float reducedCool = baseCool * (1f - maxCoolReductionRatio * reinforceProgress);
        return System.Math.Max(coolFloor, reducedCool);
    }

    // 탄약은 정수 수량 — 최종값 = 기본값 + 투자포인트 × 포인트당 증가 수량
    public static float ComputeReinforcedAmmo(float baseAmmo, int points, ShipStatFormulaSettings formula)
    {
        return baseAmmo + points * formula.hangar.ammoPerPoint;
    }

    public static float ComputeShieldGauge(float baseGauge, int points, ShipStatFormulaSettings formula)
    {
        return ComputeBoostedValue(baseGauge, points, formula.shield.maxGaugeBonusRatio, formula);
    }

    public static float ComputeShieldRegenRate(float baseRegenRate, int points, ShipStatFormulaSettings formula)
    {
        return ComputeBoostedValue(baseRegenRate, points, formula.shield.maxRegenBonusRatio, formula);
    }

    // 요격체 회복시간(1기 생성에 걸리는 초) — 강화할수록 짧아짐. 하한 0(감소 비율이 1 미만이면 0에 도달하지 않음)
    public static float ComputeInterceptorRegenTime(float baseRegenTime, int points, ShipStatFormulaSettings formula)
    {
        return ComputeReducedCooldown(baseRegenTime, points, formula.interceptor.maxRegenTimeReductionRatio, 0f, formula);
    }

    // subTypeName(예: beam_1_1)에 대응하는 DataTableModule 원본 데이터 조회 — moduleTable 또는 이름이 비어있으면 null
    private static ModuleData GetModuleData(DataTableModule moduleTable, string subTypeName)
    {
        if (moduleTable == null || string.IsNullOrEmpty(subTypeName)) return null;
        return moduleTable.GetModuleDataFromTable(subTypeName);
    }

    // 빔/미사일 공용 계산 — 슬롯당 공격력/연사력(쿨다운) 강화. 출력 배열은 슬롯 인덱스와 1:1(빈 슬롯은 0, 서브타입 "")
    // 기본 수치(공격력/쿨다운/발사체속도)는 장착된 서브타입의 DataTableModule 원본값에서 조회 — 발사체속도는 강화 대상 아님
    private static void CalculateWeaponSlots(DataTableModule moduleTable, string[] moduleSubType, int[] attackPoints, int[] fireRatePoints,
        ShipStatFormulaSettings formula, float maxAttackBonusRatio, float maxCoolReductionRatio, float attackCoolFloor,
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
            float baseAttack = moduleData != null ? moduleData.attack : 0f;
            float baseAttackCool = moduleData != null ? moduleData.attackCool : 0f;
            float baseProjectileSpeed = moduleData != null ? moduleData.speed : 0f;

            attacks[i] = ComputeBoostedValue(baseAttack, GetAt(attackPoints, i), maxAttackBonusRatio, formula);
            attackCools[i] = ComputeReducedCooldown(baseAttackCool, GetAt(fireRatePoints, i), maxCoolReductionRatio, attackCoolFloor, formula);
            projectileSpeeds[i] = baseProjectileSpeed;
            slotSubType[i] = moduleSubType[i];
        }
    }

    // 미사일 전용 — 적중 시 대상 무장 침묵 시간 강화. 기본 수치는 장착 서브타입의 DataTableModule.silenceTime에서 조회. 슬롯 인덱스와 1:1(빈 슬롯 0)
    private static float[] CalculateMissileSilenceSlots(DataTableModule moduleTable, string[] moduleSubType, int[] silencePoints, ShipStatFormulaSettings formula)
    {
        float[] result = new float[moduleSubType.Length];
        for (int i = 0; i < moduleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(moduleSubType[i])) continue;
            ModuleData moduleData = GetModuleData(moduleTable, moduleSubType[i]);
            float baseSilenceTime = moduleData != null ? moduleData.silenceTime : 0f;
            result[i] = ComputeBoostedValue(baseSilenceTime, GetAt(silencePoints, i), formula.missile.maxSilenceBonusRatio, formula);
        }
        return result;
    }

    // 신규 추가 필드는 기존 로드아웃 데이터에서 배열 크기가 subType 배열과 다를 수 있어 범위를 벗어나면 0으로 취급
    private static int GetAt(int[] array, int index)
    {
        return index < array.Length ? array[index] : 0;
    }

    private static void CalculateHangarSlots(DataTableModule moduleTable, ShipStatAllocation allocation, ShipStatFormulaSettings formula,
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

            float baseShipAttack = hangarModuleData != null ? hangarModuleData.airAttackToShip : 0f;
            float baseFighterAttack = hangarModuleData != null ? hangarModuleData.airAttackToFighter : 0f;
            float baseAmmo = hangarModuleData != null ? hangarModuleData.airAmmo : 0f;
            float baseHealth = hangarModuleData != null ? hangarModuleData.airHealth : 0f;

            shipAttacks[i] = ComputeBoostedValue(baseShipAttack, allocation.hangarAttackToShip[i], formula.hangar.maxAttackBonusRatio, formula);
            fighterAttacks[i] = ComputeBoostedValue(baseFighterAttack, allocation.hangarAttackToFighter[i], formula.hangar.maxAttackBonusRatio, formula);
            ammos[i] = ComputeReinforcedAmmo(baseAmmo, allocation.hangarAmmoPoints[i], formula);
            healths[i] = ComputeBoostedValue(baseHealth, allocation.hangarHealthPoints[i], formula.hangar.maxHealthBonusRatio, formula);
            float baseDisrupt = hangarModuleData != null ? hangarModuleData.airDisrupt : 0f;
            airDisrupts[i] = ComputeBoostedValue(baseDisrupt, allocation.hangarDisruptPoints[i], formula.hangar.maxDisruptBonusRatio, formula);
            slotSubType[i] = allocation.hangarModuleSubType[i];
        }
    }

    // 기본 수치(회복시간)는 장착 서브타입의 DataTableModule.interceptorRegenTime에서 조회. 슬롯 인덱스와 1:1(빈 슬롯 0, 서브타입 "")
    private static void CalculateInterceptorSlots(DataTableModule moduleTable, ShipStatAllocation allocation, ShipStatFormulaSettings formula,
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
            float baseRegenTime = moduleData != null ? moduleData.interceptorRegenTime : 0f;

            counts[i] = moduleData != null ? moduleData.interceptorCount : 0f;
            regenTimes[i] = ComputeInterceptorRegenTime(baseRegenTime, allocation.interceptorRegenRatePoints[i], formula);
            slotSubType[i] = allocation.interceptorModuleSubType[i];
        }
    }
}

