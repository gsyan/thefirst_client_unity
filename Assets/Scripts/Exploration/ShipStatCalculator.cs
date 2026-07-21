// 성능포인트 배분(슬롯 배열, 총량은 프리셋마다 가변) → 최종 전투 수치 변환기
// 계수/기본값은 DataTableConfig.gameSettings.shipStatFormula(ShipStatFormulaSettings)에서 주입받음 — 하드코딩 없음
// 이 클래스는 DataTableConfig를 직접 조회하지 않음 — 호출부가 formula를 꺼내 넘겨줌 (순수 함수 유지)
using System.Collections.Generic;

public static class ShipStatCalculator
{
    public static ShipFinalStats Calculate(ShipStatAllocation allocation, ShipStatFormulaSettings formula)
    {
        ShipFinalStats stats = new ShipFinalStats();

        stats.health = allocation.healthPoints * formula.flatStats.perPoint;
        stats.turnRate = allocation.turnRatePoints * formula.flatStats.perPoint;
        stats.repair = allocation.repairPoints * formula.flatStats.perPoint;

        stats.beamAttacks = CalculateSingleStatSlots(allocation.beamModuleSubType, allocation.beamReinforcePoints, formula.beam.baseAttack, formula.beam.reinforcePerPoint, out stats.beamModuleSubType);
        stats.missileAttacks = CalculateSingleStatSlots(allocation.missileModuleSubType, allocation.missileReinforcePoints, formula.missile.baseAttack, formula.missile.reinforcePerPoint, out stats.missileModuleSubType);

        CalculateHangarSlots(allocation, formula, out stats.hangarShipAttacks, out stats.hangarFighterAttacks, out stats.hangarAmmos, out stats.hangarHealths, out stats.hangarModuleSubType);

        stats.shieldInstalled = allocation.shieldInstalled;
        if (allocation.shieldInstalled)
        {
            stats.shieldGauge = formula.shield.baseGauge + allocation.shieldGaugePoints * formula.shield.gaugePerPoint;
            float shieldDelayRaw = formula.shield.baseDelay - allocation.shieldDelayPoints * formula.shield.delayReductionPerPoint;
            stats.shieldDelay = System.Math.Max(formula.shield.delayFloor, shieldDelayRaw);
            stats.shieldRegenRate = formula.shield.baseRegenRate + allocation.shieldRegenRatePoints * formula.shield.regenRatePerPoint;
        }

        CalculateInterceptorSlots(allocation, formula, out stats.interceptorDelays, out stats.interceptorRegenRates);

        return stats;
    }

    // 빔/미사일처럼 슬롯당 스탯이 하나뿐인 카테고리 공용 계산 — moduleSubType이 빈 문자열이면 미장착
    private static float[] CalculateSingleStatSlots(string[] moduleSubType, int[] reinforcePoints, float baseValue, float reinforcePerPoint, out string[] compactSubType)
    {
        List<float> result = new List<float>();
        List<string> subTypeResult = new List<string>();
        for (int i = 0; i < moduleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(moduleSubType[i])) continue;
            float value = baseValue + reinforcePoints[i] * reinforcePerPoint;
            result.Add(value);
            subTypeResult.Add(moduleSubType[i]);
        }
        compactSubType = subTypeResult.ToArray();
        return result.ToArray();
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

        for (int i = 0; i < allocation.interceptorSlotInstalled.Length; i++)
        {
            if (allocation.interceptorSlotInstalled[i] == false) continue;

            float delayRaw = formula.interceptor.baseDelay - allocation.interceptorDelayPoints[i] * formula.interceptor.delayReductionPerPoint;
            delayList.Add(System.Math.Max(formula.interceptor.delayFloor, delayRaw));
            regenRateList.Add(formula.interceptor.baseRegenRate + allocation.interceptorRegenRatePoints[i] * formula.interceptor.regenRatePerPoint);
        }

        delays = delayList.ToArray();
        regenRates = regenRateList.ToArray();
    }
}

