// 성능포인트 배분(슬롯 배열, 총량은 프리셋마다 가변) → 최종 전투 수치 변환기
// 계수/기본값은 DataTableConfig.gameSettings.shipStatFormula(ShipStatFormulaSettings)에서 주입받음 — 하드코딩 없음
// 이 클래스는 DataTableConfig를 직접 조회하지 않음 — 호출부가 formula를 꺼내 넘겨줌 (순수 함수 유지)
using System.Collections.Generic;

public static class ShipStatCalculator
{
    // bodyModuleData: 프리셋의 prefabName(body subType)에 대응하는 DataTableModule 원본 데이터 — 체력/수리력/선회력의 기본 수치 출처
    // null이면(조회 실패 등) 기본 수치 0으로 처리 — 강화 포인트만 반영
    public static ShipFinalStats Calculate(ShipStatAllocation allocation, ShipStatFormulaSettings formula, ModuleData bodyModuleData = null)
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

        stats.health = baseHealth + allocation.healthPoints * formula.flatStats.perPoint;
        stats.turnRate = baseTurnRate + allocation.turnRatePoints * formula.flatStats.perPoint;
        stats.repair = baseRepair + allocation.repairPoints * formula.flatStats.perPoint;

        CalculateWeaponSlots(allocation.beamModuleSubType, allocation.beamAttackPoints, allocation.beamFireRatePoints, allocation.beamProjectileSpeedPoints,
            formula.beam.baseAttack, formula.beam.attackPerPoint, formula.beam.baseAttackCool, formula.beam.attackCoolReductionPerPoint, formula.beam.attackCoolFloor, formula.beam.baseProjectileSpeed, formula.beam.projectileSpeedPerPoint,
            out stats.beamAttacks, out stats.beamAttackCools, out stats.beamProjectileSpeeds, out stats.beamModuleSubType);
        CalculateWeaponSlots(allocation.missileModuleSubType, allocation.missileAttackPoints, allocation.missileFireRatePoints, allocation.missileProjectileSpeedPoints,
            formula.missile.baseAttack, formula.missile.attackPerPoint, formula.missile.baseAttackCool, formula.missile.attackCoolReductionPerPoint, formula.missile.attackCoolFloor, formula.missile.baseProjectileSpeed, formula.missile.projectileSpeedPerPoint,
            out stats.missileAttacks, out stats.missileAttackCools, out stats.missileProjectileSpeeds, out stats.missileModuleSubType);
        stats.missileSilenceTimes = CalculateMissileSilenceSlots(allocation.missileModuleSubType, allocation.missileSilencePoints, formula.missile.baseSilenceTime, formula.missile.silenceTimePerPoint);

        CalculateHangarSlots(allocation, formula, out stats.hangarShipAttacks, out stats.hangarFighterAttacks, out stats.hangarAmmos, out stats.hangarHealths, out stats.hangarModuleSubType);

        stats.shieldInstalled = string.IsNullOrEmpty(allocation.shieldModuleSubType) == false;
        if (stats.shieldInstalled)
        {
            stats.shieldGauge = formula.shield.baseGauge + allocation.shieldGaugePoints * formula.shield.gaugePerPoint;
            float shieldDelayRaw = formula.shield.baseDelay - allocation.shieldDelayPoints * formula.shield.delayReductionPerPoint;
            stats.shieldDelay = System.Math.Max(formula.shield.delayFloor, shieldDelayRaw);
            stats.shieldRegenRate = formula.shield.baseRegenRate + allocation.shieldRegenRatePoints * formula.shield.regenRatePerPoint;
        }

        CalculateInterceptorSlots(allocation, formula, out stats.interceptorDelays, out stats.interceptorRegenRates);

        return stats;
    }

    // 빔/미사일 공용 계산 — 슬롯당 공격력/연사력(쿨다운)/발사체속도 3속성 강화. moduleSubType이 빈 문자열이면 미장착
    private static void CalculateWeaponSlots(string[] moduleSubType, int[] attackPoints, int[] fireRatePoints, int[] projectileSpeedPoints,
        float baseAttack, float attackPerPoint, float baseAttackCool, float attackCoolReductionPerPoint, float attackCoolFloor, float baseProjectileSpeed, float projectileSpeedPerPoint,
        out float[] attacks, out float[] attackCools, out float[] projectileSpeeds, out string[] compactSubType)
    {
        List<float> attackList = new List<float>();
        List<float> attackCoolList = new List<float>();
        List<float> projectileSpeedList = new List<float>();
        List<string> subTypeResult = new List<string>();

        for (int i = 0; i < moduleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(moduleSubType[i])) continue;

            attackList.Add(baseAttack + GetAt(attackPoints, i) * attackPerPoint);

            float attackCoolRaw = baseAttackCool - GetAt(fireRatePoints, i) * attackCoolReductionPerPoint;
            attackCoolList.Add(System.Math.Max(attackCoolFloor, attackCoolRaw));

            projectileSpeedList.Add(baseProjectileSpeed + GetAt(projectileSpeedPoints, i) * projectileSpeedPerPoint);

            subTypeResult.Add(moduleSubType[i]);
        }

        attacks = attackList.ToArray();
        attackCools = attackCoolList.ToArray();
        projectileSpeeds = projectileSpeedList.ToArray();
        compactSubType = subTypeResult.ToArray();
    }

    // 미사일 전용 — 적중 시 대상 무장 침묵 시간 강화
    private static float[] CalculateMissileSilenceSlots(string[] moduleSubType, int[] silencePoints, float baseSilenceTime, float silenceTimePerPoint)
    {
        List<float> result = new List<float>();
        for (int i = 0; i < moduleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(moduleSubType[i])) continue;
            result.Add(baseSilenceTime + GetAt(silencePoints, i) * silenceTimePerPoint);
        }
        return result.ToArray();
    }

    // 신규 추가 필드는 기존 프리셋 데이터에서 배열 크기가 subType 배열과 다를 수 있어 범위를 벗어나면 0으로 취급
    private static int GetAt(int[] array, int index)
    {
        return index < array.Length ? array[index] : 0;
    }

    private static void CalculateHangarSlots(ShipStatAllocation allocation, ShipStatFormulaSettings formula,
        out float[] shipAttacks, out float[] fighterAttacks, out float[] ammos, out float[] healths, out string[] compactSubType)
    {
        List<float> shipAttackList = new List<float>();
        List<float> fighterAttackList = new List<float>();
        List<float> ammoList = new List<float>();
        List<float> healthList = new List<float>();
        List<string> subTypeList = new List<string>();

        for (int i = 0; i < allocation.hangarModuleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(allocation.hangarModuleSubType[i])) continue;

            shipAttackList.Add(formula.hangar.baseShipAttack + allocation.hangarShipAttackPoints[i] * formula.hangar.reinforcePerPoint);
            fighterAttackList.Add(formula.hangar.baseFighterAttack + allocation.hangarFighterAttackPoints[i] * formula.hangar.reinforcePerPoint);
            ammoList.Add(formula.hangar.baseAmmo + allocation.hangarAmmoPoints[i] * formula.hangar.reinforcePerPoint);
            healthList.Add(formula.hangar.baseHealth + allocation.hangarHealthPoints[i] * formula.hangar.reinforcePerPoint);
            subTypeList.Add(allocation.hangarModuleSubType[i]);
        }

        shipAttacks = shipAttackList.ToArray();
        fighterAttacks = fighterAttackList.ToArray();
        ammos = ammoList.ToArray();
        healths = healthList.ToArray();
        compactSubType = subTypeList.ToArray();
    }

    private static void CalculateInterceptorSlots(ShipStatAllocation allocation, ShipStatFormulaSettings formula,
        out float[] delays, out float[] regenRates)
    {
        List<float> delayList = new List<float>();
        List<float> regenRateList = new List<float>();

        for (int i = 0; i < allocation.interceptorModuleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(allocation.interceptorModuleSubType[i])) continue;

            float delayRaw = formula.interceptor.baseDelay - allocation.interceptorDelayPoints[i] * formula.interceptor.delayReductionPerPoint;
            delayList.Add(System.Math.Max(formula.interceptor.delayFloor, delayRaw));
            regenRateList.Add(formula.interceptor.baseRegenRate + allocation.interceptorRegenRatePoints[i] * formula.interceptor.regenRatePerPoint);
        }

        delays = delayList.ToArray();
        regenRates = regenRateList.ToArray();
    }
}

