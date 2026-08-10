// ShipPresetData → 게이지 표시용 스탯 항목 리스트 변환 — 배치가능 프리셋 클릭 시 뜨는 UIPopupConfirm(stat gauge 섹션)과 함대편성 성능 컬럼(UIPanelFleet)이 공유
// 증가형 스탯(공격력/체력/실드게이지 등)은 게이지로, 감소형(쿨다운/딜레이 등)은 강화율 기준 반전 게이지로 표시
// 빔/미사일/격납고처럼 슬롯이 여러 개인 카테고리는 슬롯 수와 무관하게 항목 수를 고정하기 위해 종합 표시한다
// - DPS로 흡수 가능한 스탯(공격력+쿨다운)은 슬롯 전체 합산 DPS 1줄로 압축
// - DPS로 흡수 불가능한 스탯(탄약/체력/침묵시간/교란 등)은 슬롯 간 최소~최대 범위로 압축
using System.Collections.Generic;

public struct ShipStatGaugeEntry
{
    public string label;
    public float value;
    public float gaugeMax;              // Normal 모드 전용 — fillAmount = value/gaugeMax
    public string rawValueText;
    public EGaugeMode mode;
    public float reverseFillAmount;     // Reverse 모드 전용 — 강화율(0~1) 사전 계산값
    public float compareValue;          // 표시 모드와 무관하게 항상 채워지는 순수 수치 — 프리셋 비교(diff) 계산 전용
    public bool hasCompareValue;        // 슬롯 여러 개를 min~max 범위로 압축한 항목(None 모드)은 단일 수치가 아니라 비교 불가 — false
}

// None: 게이지 없이 숫자만, Normal: value/gaugeMax 채움, Reverse: 강화율(0=미강화, 1=floor 도달) 채움
public enum EGaugeMode
{
    None,
    Normal,
    Reverse,
}

public static class ShipStatGaugeBuilder
{
    // 증가형 스탯 게이지의 만렙 기준 — "기본 능력치의 20배" (정확한 밸런스 수치 아님, 임시)
    private const float k_gaugeMaxMultiplier = 20f;

    // actualModules를 생략하면(null) preset.statAllocation(프리셋 기본 장착 구성)을 그대로 씀 — 프리셋 후보 목록/비교 미리보기용
    // actualModules를 넘기면 그 함선이 실제로 장착한 모듈 구성(로드아웃)을 반영 — 배치된 함선의 현재 성능 표시용
    // healthMultiplier/attackMultiplier는 Zone 적 함대 열람 시에만 1이 아님 — ModuleBeam/Body/Hanger.cs의 Zone 배율 적용 규칙과 반드시 동일하게 유지할 것
    // (체력/수리력=healthMultiplier, 공격력 계열=attackMultiplier, 선회력은 배율 미적용)
    public static List<ShipStatGaugeEntry> Build(ShipPresetData preset, ModuleBodyInfo actualModules = null, float healthMultiplier = 1f, float attackMultiplier = 1f)
    {
        DataTableModule moduleTable = DataManager.Instance.m_dataTableModule;

        ModuleData bodyModuleData = null;
        if (System.Enum.TryParse(preset.prefabName, out EModuleSubType bodySubType))
            bodyModuleData = moduleTable.GetModuleDataFromTable(bodySubType);

        ShipStatFormulaSettings formula = DataManager.Instance.m_dataTableConfig.gameSettings.shipStatFormula;
        ShipStatAllocation allocation = ShipStatAllocation.BuildFromModuleBodyInfo(preset.statAllocation, actualModules);
        ShipFinalStats stats = ShipStatCalculator.Calculate(allocation, formula, bodyModuleData, moduleTable);

        List<ShipStatGaugeEntry> entries = new();

        float baseHealth = bodyModuleData != null ? bodyModuleData.health : 0f;
        float baseTurnRate = bodyModuleData != null ? bodyModuleData.turnRate : 0f;
        float baseRepair = bodyModuleData != null ? bodyModuleData.repair : 0f;

        LocalizationManager loc = LocalizationManager.Instance;

        entries.Add(MakeGauge(loc.Get("UIFleet_Stats_Health"), stats.health * healthMultiplier, baseHealth));
        entries.Add(MakeGauge(loc.Get("UIFleet_Stats_TurnRate"), stats.turnRate, baseTurnRate));
        entries.Add(MakeGauge(loc.Get("UIFleet_Stats_Repair"), stats.repair * healthMultiplier, baseRepair));

        if (stats.beamModuleSubType.Length > 0)
        {
            float beamDps = SumDps(stats.beamAttacks, stats.beamAttackCools) * attackMultiplier;
            float baseBeamDps = SumBaseDps(moduleTable, stats.beamModuleSubType);
            entries.Add(MakeGauge(loc.Get("UIFleet_Stats_BeamDps"), beamDps, baseBeamDps));
        }

        if (stats.missileModuleSubType.Length > 0)
        {
            float missileDps = SumDps(stats.missileAttacks, stats.missileAttackCools) * attackMultiplier;
            float baseMissileDps = SumBaseDps(moduleTable, stats.missileModuleSubType);
            entries.Add(MakeGauge(loc.Get("UIFleet_Stats_MissileDps"), missileDps, baseMissileDps));
        }

        if (stats.missileSilenceTimes.Length > 0)
            entries.Add(MakeMinMaxValueOnly(loc.Get("UIFleet_Stats_MissileSilenceTime"), stats.missileSilenceTimes, "F1", "s"));

        if (stats.hangarModuleSubType.Length > 0)
        {
            float baseHangarShipDps = formula.hangar.baseShipAttack * stats.hangarModuleSubType.Length;
            float baseHangarFighterDps = formula.hangar.baseFighterAttack * stats.hangarModuleSubType.Length;
            entries.Add(MakeGauge(loc.Get("UIFleet_Stats_FighterAttackPowerToShip"), Sum(stats.hangarShipAttacks) * attackMultiplier, baseHangarShipDps));
            entries.Add(MakeGauge(loc.Get("UIFleet_Stats_FighterAttackPowerToFighter"), Sum(stats.hangarFighterAttacks) * attackMultiplier, baseHangarFighterDps));
            entries.Add(MakeMinMaxValueOnly(loc.Get("UIFleet_Stats_FighterAmmo"), stats.hangarAmmos, "F0", ""));
            entries.Add(MakeMinMaxValueOnly(loc.Get("UIFleet_Stats_FighterHealth"), ScaleArray(stats.hangarHealths, healthMultiplier), "F0", ""));
            entries.Add(MakeMinMaxValueOnly(loc.Get("UIFleet_Stats_FighterDisrupt"), stats.hangarAirDisrupts, "F2", "s"));
        }

        if (stats.shieldInstalled == true)
        {
            ModuleData shieldModuleData = GetModuleData(moduleTable, preset.statAllocation.shieldModuleSubType);
            float baseShieldGauge = shieldModuleData != null ? shieldModuleData.shieldGauge : 0f;
            float baseShieldDelay = shieldModuleData != null ? shieldModuleData.shieldDelay : 0f;
            float baseShieldRegenRate = shieldModuleData != null ? shieldModuleData.shieldRegenRate : 0f;
            entries.Add(MakeGauge(loc.Get("UIFleet_Stats_ShieldGauge"), stats.shieldGauge, baseShieldGauge));
            entries.Add(MakeReverseGauge(loc.Get("UIFleet_Stats_ShieldRestartDelay"), stats.shieldDelay, baseShieldDelay, formula.shield.delayFloor, $"{stats.shieldDelay:F2}s"));
            entries.Add(MakeGauge(loc.Get("UIFleet_Stats_ShieldRegenRate"), stats.shieldRegenRate, baseShieldRegenRate));
        }

        // 요격체는 모듈 1개만 장착 가능(추가 배치 없음) — 슬롯 종합 없이 그대로 표시
        for (int i = 0; i < stats.interceptorDelays.Length; i++)
        {
            ModuleData interceptorModuleData = GetModuleData(moduleTable, stats.interceptorModuleSubType[i]);
            float baseInterceptorDelay = interceptorModuleData != null ? interceptorModuleData.interceptorDelay : 0f;
            float baseInterceptorRegenRate = interceptorModuleData != null ? interceptorModuleData.interceptorRegenRate : 0f;
            entries.Add(MakeReverseGauge(loc.Get("UIFleet_Stats_InterceptorReloadDelay"), stats.interceptorDelays[i], baseInterceptorDelay, formula.interceptor.delayFloor, $"{stats.interceptorDelays[i]:F2}s"));
            entries.Add(MakeGauge(loc.Get("UIFleet_Stats_InterceptorRegenRate"), stats.interceptorRegenRates[i], baseInterceptorRegenRate));
        }

        return entries;
    }

    // multiplier가 1이면 원본 배열을 그대로 재사용해 불필요한 GC Alloc 방지
    private static float[] ScaleArray(float[] values, float multiplier)
    {
        if (multiplier == 1f) return values;
        float[] result = new float[values.Length];
        for (int i = 0; i < values.Length; i++)
            result[i] = values[i] * multiplier;
        return result;
    }

    private static float Sum(float[] values)
    {
        float total = 0f;
        for (int i = 0; i < values.Length; i++)
            total += values[i];
        return total;
    }

    // 슬롯별 공격력/쿨다운을 DPS(초당 피해량)로 환산해 합산 — attackCool이 0이면(비정상 데이터) 해당 슬롯 제외
    private static float SumDps(float[] attacks, float[] attackCools)
    {
        float total = 0f;
        for (int i = 0; i < attacks.Length; i++)
        {
            if (attackCools[i] > 0f)
                total += attacks[i] / attackCools[i];
        }
        return total;
    }

    // 게이지 만렙 기준 산정용 — 강화 포인트 반영 전 ModuleData 원본 공격력/쿨다운으로 계산한 DPS 합산
    private static float SumBaseDps(DataTableModule moduleTable, string[] subTypes)
    {
        float total = 0f;
        for (int i = 0; i < subTypes.Length; i++)
        {
            ModuleData moduleData = GetModuleData(moduleTable, subTypes[i]);
            if (moduleData != null && moduleData.attackCool > 0f)
                total += moduleData.attack / moduleData.attackCool;
        }
        return total;
    }

    // subTypeName(예: beam_t1_m1)에 대응하는 DataTableModule 원본 데이터 조회 — 게이지 만렙 기준 산정용
    private static ModuleData GetModuleData(DataTableModule moduleTable, string subTypeName)
    {
        if (moduleTable == null || string.IsNullOrEmpty(subTypeName)) return null;
        if (System.Enum.TryParse(subTypeName, out EModuleSubType subType) == false) return null;
        return moduleTable.GetModuleDataFromTable(subType);
    }

    private static ShipStatGaugeEntry MakeGauge(string label, float value, float baseValue)
    {
        return new ShipStatGaugeEntry { label = label, value = value, gaugeMax = baseValue * k_gaugeMaxMultiplier, rawValueText = $"{value:F1}", mode = EGaugeMode.Normal, compareValue = value, hasCompareValue = true };
    }

    // 감소형 스탯(쿨다운/딜레이 등) 반전 게이지 — 강화율 0~1을 채운다. baseValue는 미강화 원본값, floor는 formula의 하한값(도달 시 100%)
    private static ShipStatGaugeEntry MakeReverseGauge(string label, float value, float baseValue, float floor, string valueText)
    {
        float fillAmount;
        if (baseValue <= floor)
        {
            fillAmount = 1f;
        }
        else
        {
            float reduceRange = baseValue - floor;
            float reducedAmount = baseValue - value;
            fillAmount = UnityEngine.Mathf.Clamp01(reducedAmount / reduceRange);
        }

        return new ShipStatGaugeEntry { label = label, rawValueText = valueText, mode = EGaugeMode.Reverse, reverseFillAmount = fillAmount, compareValue = value, hasCompareValue = true };
    }

    private static ShipStatGaugeEntry MakeValueOnly(string label, string valueText)
    {
        return new ShipStatGaugeEntry { label = label, gaugeMax = 0f, rawValueText = valueText, mode = EGaugeMode.None };
    }

    // 슬롯이 여러 개인 비-DPS 스탯(탄약/체력/침묵시간 등)을 최소~최대 범위 텍스트로 압축. 슬롯이 1개거나 값이 동일하면 단일 숫자만 표시
    private static ShipStatGaugeEntry MakeMinMaxValueOnly(string label, float[] values, string numberFormat, string suffix)
    {
        float min = values[0];
        float max = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] < min) min = values[i];
            if (values[i] > max) max = values[i];
        }

        string valueText = UnityEngine.Mathf.Approximately(min, max)
            ? $"{min.ToString(numberFormat)}{suffix}"
            : $"{min.ToString(numberFormat)}~{max.ToString(numberFormat)}{suffix}";

        return MakeValueOnly(label, valueText);
    }
}
