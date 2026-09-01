// 함체(ModuleData, body) → 스탯 표시 항목 리스트 변환 — 배치가능 함체 클릭 시 뜨는 UIPopupConfirm(stat 섹션)과 함대편성 성능 컬럼(UIPanelFleet)이 공유
// 게이지 없이 라벨 : 값 텍스트로만 표시(만렙 기준이 임의값이라 게이지로는 의미가 애매함)
// 빔/미사일/격납고처럼 슬롯이 여러 개인 카테고리는 슬롯 수와 무관하게 항목 수를 고정하기 위해 종합 표시한다
// - DPS로 흡수 가능한 스탯(공격력+쿨다운)은 슬롯 전체 합산 DPS 1줄로 압축
// - DPS로 흡수 불가능한 스탯(탄약/체력/침묵시간/교란 등)은 슬롯 간 최소~최대 범위로 압축
using System.Collections.Generic;
using UnityEngine;

public struct ShipStatRowEntry
{
    public string label;
    public float value;                 // isNumericValue == true 전용 — F1 포맷으로 표시
    public string rawValueText;         // isNumericValue == false 전용 — 이미 포맷된 텍스트 그대로 표시
    public bool isNumericValue;         // true: 값+버프증감(SetStatRow), false: 텍스트 그대로(SetValueOnly)
    public float compareValue;          // 표시 방식과 무관하게 항상 채워지는 순수 수치 — 프리셋 비교(diff) 계산 전용
    public bool hasCompareValue;        // 슬롯 여러 개를 min~max 범위로 압축한 항목은 단일 수치가 아니라 비교 불가 — false
    public string buffDiffText;         // isNumericValue == true 전용, 보상카드 버프로 늘어난 만큼(리치텍스트 색상 포함, 예: "<color=#4CD97B>(+12.0)</color>") — 버프 없으면 null
}

public static class ShipStatGaugeBuilder
{
    // actualModules를 생략하면(null) 빈 로드아웃(전 슬롯 미장착)을 그대로 씀 — 함체 후보 목록/비교 미리보기용
    // actualModules를 넘기면 그 함선이 실제로 장착한 모듈 구성(로드아웃)을 반영 — 배치된 함선의 현재 성능 표시용
    // healthMultiplier/attackMultiplier는 Zone 적 함대 열람 시에만 1이 아님 — ModuleBeam/Body/Hangar.cs의 Zone 배율 적용 규칙과 반드시 동일하게 유지할 것
    // (체력/수리력=healthMultiplier, 공격력 계열=attackMultiplier, 선회력은 배율 미적용)
    // applyBuffs를 넘기면 보상카드 지속버프 배율을 표시값에만 반영(배열 mutate 없이 스칼라만 스케일) — 존 런 중 대치 화면 전용, 그 외 호출부는 null 유지
    public static List<ShipStatRowEntry> Build(ModuleData hullData, ModuleBodyInfo actualModules = null, float healthMultiplier = 1f, float attackMultiplier = 1f, RewardCardSessionState applyBuffs = null)
    {
        DataTableModule moduleTable = DataManager.Instance.m_dataTableModule;

        ShipStatFormulaSettings formula = DataManager.Instance.m_dataTableConfig.gameSettings.shipStatFormula;
        ShipStatAllocation allocation = ShipStatAllocation.BuildFromModuleBodyInfo(formula.maxModuleSlots, actualModules);
        ShipFinalStats stats = ShipStatCalculator.Calculate(allocation, formula, hullData, moduleTable);

        List<ShipStatRowEntry> entries = new();

        LocalizationManager loc = LocalizationManager.Instance;

        // 보상카드 지속버프 배율 — applyBuffs가 null이면 전부 1(버프 없음)
        float healthBuffMult = applyBuffs != null ? applyBuffs.GetMultiplier(ECardEffectType.Buff_ShipHealth) : 1f;
        float beamDpsBuffMult = applyBuffs != null ? applyBuffs.GetMultiplier(ECardEffectType.Buff_BeamAttack) * applyBuffs.GetMultiplier(ECardEffectType.Buff_BeamFireRate) : 1f;
        float missileDpsBuffMult = applyBuffs != null ? applyBuffs.GetMultiplier(ECardEffectType.Buff_MissileAttack) * applyBuffs.GetMultiplier(ECardEffectType.Buff_MissileFireRate) : 1f;
        float hangarShipDpsBuffMult = applyBuffs != null ? applyBuffs.GetMultiplier(ECardEffectType.Buff_HangarAttackToShip) : 1f;
        float hangarFighterDpsBuffMult = applyBuffs != null ? applyBuffs.GetMultiplier(ECardEffectType.Buff_HangarAttackToFighter) : 1f;

        entries.Add(MakeNumericStat(loc.Get("UIFleet_Stats_Health"), stats.health * healthMultiplier, healthBuffMult));
        entries.Add(MakeNumericStat(loc.Get("UIFleet_Stats_TurnRate"), stats.turnRate));
        entries.Add(MakeNumericStat(loc.Get("UIFleet_Stats_Repair"), stats.repair * healthMultiplier));

        if (stats.beamModuleSubType.Length > 0)
        {
            float beamDps = SumDps(stats.beamAttacks, stats.beamAttackCools) * attackMultiplier;
            entries.Add(MakeNumericStat(loc.Get("UIFleet_Stats_BeamDps"), beamDps, beamDpsBuffMult));
        }

        if (stats.missileModuleSubType.Length > 0)
        {
            float missileDps = SumDps(stats.missileAttacks, stats.missileAttackCools) * attackMultiplier;
            entries.Add(MakeNumericStat(loc.Get("UIFleet_Stats_MissileDps"), missileDps, missileDpsBuffMult));
        }

        if (stats.missileSilenceTimes.Length > 0)
            entries.Add(MakeMinMaxValueOnly(loc.Get("UIFleet_Stats_MissileSilenceTime"), stats.missileSilenceTimes, "F1", "s"));

        if (stats.hangarModuleSubType.Length > 0)
        {
            entries.Add(MakeNumericStat(loc.Get("UIFleet_Stats_FighterAttackPowerToShip"), Sum(stats.hangarShipAttacks) * attackMultiplier, hangarShipDpsBuffMult));
            entries.Add(MakeNumericStat(loc.Get("UIFleet_Stats_FighterAttackPowerToFighter"), Sum(stats.hangarFighterAttacks) * attackMultiplier, hangarFighterDpsBuffMult));
            entries.Add(MakeMinMaxValueOnly(loc.Get("UIFleet_Stats_FighterAmmo"), stats.hangarAmmos, "F0", ""));
            entries.Add(MakeMinMaxValueOnly(loc.Get("UIFleet_Stats_FighterHealth"), ScaleArray(stats.hangarHealths, healthMultiplier), "F0", ""));
            entries.Add(MakeMinMaxValueOnly(loc.Get("UIFleet_Stats_FighterDisrupt"), stats.hangarAirDisrupts, "F2", "s"));
        }

        if (stats.shieldInstalled == true)
        {
            entries.Add(MakeNumericStat(loc.Get("UIFleet_Stats_ShieldGauge"), stats.shieldGauge));
            entries.Add(MakeCompareText(loc.Get("UIFleet_Stats_ShieldRestartDelay"), stats.shieldDelay, $"{stats.shieldDelay:F2}s"));
            entries.Add(MakeNumericStat(loc.Get("UIFleet_Stats_ShieldRegenRate"), stats.shieldRegenRate));
        }

        // 요격체는 모듈 1개만 장착 가능(추가 배치 없음) — 슬롯 종합 없이 그대로 표시
        for (int i = 0; i < stats.interceptorDelays.Length; i++)
        {
            entries.Add(MakeCompareText(loc.Get("UIFleet_Stats_InterceptorReloadDelay"), stats.interceptorDelays[i], $"{stats.interceptorDelays[i]:F2}s"));
            entries.Add(MakeNumericStat(loc.Get("UIFleet_Stats_InterceptorRegenRate"), stats.interceptorRegenRates[i]));
        }

        return entries;
    }

    // entry를 baselineByLabel(라벨→기준 스탯)과 비교해 리치텍스트 diff 반환, 변화 없거나 비교 불가면 null.
    // 화면엔 소수 1자리로 버림된 값이 보이므로(UIStatRow.SetStatRow), diff도 원본 float가 아니라 버림된 값 기준으로
    // 계산해야 표시값과 어긋나지 않음(예: 3.3→6.7이면 +3.4가 나와야지 원본 float 차이인 +3.3이 나오면 안 됨)
    public static string BuildDiffText(ShipStatRowEntry entry, Dictionary<string, ShipStatRowEntry> baselineByLabel)
    {
        if (entry.hasCompareValue == false) return null;
        if (baselineByLabel == null) return null;

        float baselineValue = 0f;
        if (baselineByLabel.TryGetValue(entry.label, out ShipStatRowEntry baselineEntry) == true)
        {
            if (baselineEntry.hasCompareValue == false) return null;
            baselineValue = baselineEntry.compareValue;
        }

        float entryFloored = CommonUtility.FloorToDecimals(entry.compareValue, 1);
        float baselineFloored = CommonUtility.FloorToDecimals(baselineValue, 1);
        float diff = entryFloored - baselineFloored;
        if (diff == 0f) return null;

        string sign = diff > 0f ? "+" : "";
        string color = diff > 0f ? "red" : "blue";
        return $"<color={color}>({sign}{diff:F1})</color>";
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

    // buffMultiplier: 보상카드 지속버프 배율(기본 1=버프 없음) — value에 곱해 최종값을 만들고, 늘어난 만큼은 buffDiffText로 별도 표시
    private static ShipStatRowEntry MakeNumericStat(string label, float value, float buffMultiplier = 1f)
    {
        float finalValue = value * buffMultiplier;
        string buffDiffText = buffMultiplier != 1f ? BuildBuffDiffText(finalValue, value) : null;
        return new ShipStatRowEntry { label = label, value = finalValue, isNumericValue = true, compareValue = finalValue, hasCompareValue = true, buffDiffText = buffDiffText };
    }

    // 화면에 보이는 값(UIStatRow.SetStatRow, 버림 처리됨)과 같은 버림 규칙으로 diff를 계산해야
    // "원본값 + diff == 최종값"이 항상 성립함. 보상카드 지속버프는 항상 증가(양수)이므로 부호 없이 녹색 "(+n.n)"으로 고정 표기
    private static string BuildBuffDiffText(float finalValue, float baseValue)
    {
        float finalFloored = CommonUtility.FloorToDecimals(finalValue, 1);
        float baseFloored = CommonUtility.FloorToDecimals(baseValue, 1);
        float diff = finalFloored - baseFloored;
        return $"<color=#4CD97B>(+{diff:F1})</color>";
    }

    // 감소형 스탯(쿨다운/딜레이 등) — 이미 포맷된 텍스트로 표시하되, 프리셋 비교(diff)를 위해 순수 수치는 compareValue로 남김
    private static ShipStatRowEntry MakeCompareText(string label, float value, string valueText)
    {
        return new ShipStatRowEntry { label = label, rawValueText = valueText, isNumericValue = false, compareValue = value, hasCompareValue = true };
    }

    private static ShipStatRowEntry MakeValueOnly(string label, string valueText)
    {
        return new ShipStatRowEntry { label = label, rawValueText = valueText, isNumericValue = false };
    }

    // 슬롯이 여러 개인 비-DPS 스탯(탄약/체력/침묵시간 등)을 최소~최대 범위 텍스트로 압축. 슬롯이 1개거나 값이 동일하면 단일 숫자만 표시
    private static ShipStatRowEntry MakeMinMaxValueOnly(string label, float[] values, string numberFormat, string suffix)
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
