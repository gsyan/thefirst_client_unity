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

        CalculateWeaponSlots(moduleTable, allocation.beamModuleSubType, allocation.beamAttackPoints, allocation.beamFireRatePoints, allocation.beamProjectileSpeedPoints,
            formula.beam.attackPerPoint, formula.beam.attackCoolReductionPerPoint, formula.beam.attackCoolFloor, formula.beam.projectileSpeedPerPoint,
            out stats.beamAttacks, out stats.beamAttackCools, out stats.beamProjectileSpeeds, out stats.beamModuleSubType);
        CalculateWeaponSlots(moduleTable, allocation.missileModuleSubType, allocation.missileAttackPoints, allocation.missileFireRatePoints, allocation.missileProjectileSpeedPoints,
            formula.missile.attackPerPoint, formula.missile.attackCoolReductionPerPoint, formula.missile.attackCoolFloor, formula.missile.projectileSpeedPerPoint,
            out stats.missileAttacks, out stats.missileAttackCools, out stats.missileProjectileSpeeds, out stats.missileModuleSubType);
        stats.missileSilenceTimes = CalculateMissileSilenceSlots(moduleTable, allocation.missileModuleSubType, allocation.missileSilencePoints, formula.missile.silenceTimePerPoint);

        CalculateHangarSlots(moduleTable, allocation, formula, out stats.hangarShipAttacks, out stats.hangarFighterAttacks, out stats.hangarAmmos, out stats.hangarHealths, out stats.hangarAirDisrupts, out stats.hangarModuleSubType);

        stats.shieldInstalled = string.IsNullOrEmpty(allocation.shieldModuleSubType) == false;
        if (stats.shieldInstalled)
        {
            ModuleData shieldModuleData = GetModuleData(moduleTable, allocation.shieldModuleSubType);
            float baseShieldGauge = shieldModuleData != null ? shieldModuleData.shieldGauge : 0f;
            float baseShieldRegenRate = shieldModuleData != null ? shieldModuleData.shieldRegenRate : 0f;

            stats.shieldGauge = baseShieldGauge + allocation.shieldGaugePoints * formula.shield.gaugePerPoint;
            stats.shieldRegenRate = baseShieldRegenRate + allocation.shieldRegenRatePoints * formula.shield.regenRatePerPoint;
        }

        CalculateInterceptorSlots(moduleTable, allocation, formula, out stats.interceptorDelays, out stats.interceptorRegenRates, out stats.interceptorModuleSubType);

        return stats;
    }

    // subTypeName(예: beam_t1_m1)에 대응하는 DataTableModule 원본 데이터 조회 — moduleTable 또는 이름이 비어있으면 null
    private static ModuleData GetModuleData(DataTableModule moduleTable, string subTypeName)
    {
        if (moduleTable == null || string.IsNullOrEmpty(subTypeName)) return null;
        if (System.Enum.TryParse(subTypeName, out EModuleSubType subType) == false) return null;
        return moduleTable.GetModuleDataFromTable(subType);
    }

    // 빔/미사일 공용 계산 — 슬롯당 공격력/연사력(쿨다운)/발사체속도 3속성 강화. moduleSubType이 빈 문자열이면 미장착
    // 기본 수치(공격력/쿨다운/발사체속도)는 장착된 서브타입의 DataTableModule 원본값에서 조회 — formula는 포인트당 증감 계수만 제공
    private static void CalculateWeaponSlots(DataTableModule moduleTable, string[] moduleSubType, int[] attackPoints, int[] fireRatePoints, int[] projectileSpeedPoints,
        float attackPerPoint, float attackCoolReductionPerPoint, float attackCoolFloor, float projectileSpeedPerPoint,
        out float[] attacks, out float[] attackCools, out float[] projectileSpeeds, out string[] compactSubType)
    {
        List<float> attackList = new List<float>();
        List<float> attackCoolList = new List<float>();
        List<float> projectileSpeedList = new List<float>();
        List<string> subTypeResult = new List<string>();

        for (int i = 0; i < moduleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(moduleSubType[i])) continue;

            ModuleData moduleData = GetModuleData(moduleTable, moduleSubType[i]);
            float baseAttack = moduleData != null ? moduleData.attack : 0f;
            float baseAttackCool = moduleData != null ? moduleData.attackCool : 0f;
            float baseProjectileSpeed = moduleData != null ? moduleData.speed : 0f;

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

    // 미사일 전용 — 적중 시 대상 무장 침묵 시간 강화. 기본 수치는 장착 서브타입의 DataTableModule.silenceTime에서 조회
    private static float[] CalculateMissileSilenceSlots(DataTableModule moduleTable, string[] moduleSubType, int[] silencePoints, float silenceTimePerPoint)
    {
        List<float> result = new List<float>();
        for (int i = 0; i < moduleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(moduleSubType[i])) continue;
            ModuleData moduleData = GetModuleData(moduleTable, moduleSubType[i]);
            float baseSilenceTime = moduleData != null ? moduleData.silenceTime : 0f;
            result.Add(baseSilenceTime + GetAt(silencePoints, i) * silenceTimePerPoint);
        }
        return result.ToArray();
    }

    // 신규 추가 필드는 기존 로드아웃 데이터에서 배열 크기가 subType 배열과 다를 수 있어 범위를 벗어나면 0으로 취급
    private static int GetAt(int[] array, int index)
    {
        return index < array.Length ? array[index] : 0;
    }

    private static void CalculateHangarSlots(DataTableModule moduleTable, ShipStatAllocation allocation, ShipStatFormulaSettings formula,
        out float[] shipAttacks, out float[] fighterAttacks, out float[] ammos, out float[] healths, out float[] airDisrupts, out string[] compactSubType)
    {
        List<float> shipAttackList = new List<float>();
        List<float> fighterAttackList = new List<float>();
        List<float> ammoList = new List<float>();
        List<float> healthList = new List<float>();
        List<float> airDisruptList = new List<float>();
        List<string> subTypeList = new List<string>();

        for (int i = 0; i < allocation.hangarModuleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(allocation.hangarModuleSubType[i])) continue;

            ModuleData hangarModuleData = GetModuleData(moduleTable, allocation.hangarModuleSubType[i]);

            shipAttackList.Add(formula.hangar.baseShipAttack + allocation.hangarAttackToShip[i] * formula.hangar.reinforcePerPoint);
            fighterAttackList.Add(formula.hangar.baseFighterAttack + allocation.hangarAttackToFighter[i] * formula.hangar.reinforcePerPoint);
            ammoList.Add(formula.hangar.baseAmmo + allocation.hangarAmmoPoints[i] * formula.hangar.reinforcePerPoint);
            healthList.Add(formula.hangar.baseHealth + allocation.hangarHealthPoints[i] * formula.hangar.reinforcePerPoint);
            airDisruptList.Add(hangarModuleData != null ? hangarModuleData.airDisrupt : 0f);
            subTypeList.Add(allocation.hangarModuleSubType[i]);
        }

        shipAttacks = shipAttackList.ToArray();
        fighterAttacks = fighterAttackList.ToArray();
        ammos = ammoList.ToArray();
        healths = healthList.ToArray();
        airDisrupts = airDisruptList.ToArray();
        compactSubType = subTypeList.ToArray();
    }

    // 기본 수치(보충 딜레이/회복속도)는 장착 서브타입의 DataTableModule.interceptorDelay/interceptorRegenRate에서 조회
    private static void CalculateInterceptorSlots(DataTableModule moduleTable, ShipStatAllocation allocation, ShipStatFormulaSettings formula,
        out float[] delays, out float[] regenRates, out string[] compactSubType)
    {
        List<float> delayList = new List<float>();
        List<float> regenRateList = new List<float>();
        List<string> subTypeResult = new List<string>();

        for (int i = 0; i < allocation.interceptorModuleSubType.Length; i++)
        {
            if (string.IsNullOrEmpty(allocation.interceptorModuleSubType[i])) continue;

            ModuleData moduleData = GetModuleData(moduleTable, allocation.interceptorModuleSubType[i]);
            float baseDelay = moduleData != null ? moduleData.interceptorDelay : 0f;
            float baseRegenRate = moduleData != null ? moduleData.interceptorRegenRate : 0f;

            float delayRaw = baseDelay - allocation.interceptorDelayPoints[i] * formula.interceptor.delayReductionPerPoint;
            delayList.Add(System.Math.Max(formula.interceptor.delayFloor, delayRaw));
            regenRateList.Add(baseRegenRate + allocation.interceptorRegenRatePoints[i] * formula.interceptor.regenRatePerPoint);
            subTypeResult.Add(allocation.interceptorModuleSubType[i]);
        }

        delays = delayList.ToArray();
        regenRates = regenRateList.ToArray();
        compactSubType = subTypeResult.ToArray();
    }
}

