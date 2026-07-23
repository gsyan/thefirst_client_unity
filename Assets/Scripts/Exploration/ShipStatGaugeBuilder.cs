// ShipPresetData → 게이지 표시용 스탯 항목 리스트 변환 — 팝업(UIPopupShipStats)과 함대편성 성능 컬럼(UITabFleetComposition)이 공유
// 증가형 스탯(공격력/체력/실드게이지 등)은 게이지로, 감소형(쿨다운/딜레이 등)은 값만 표시하도록 항목을 구성
using System.Collections.Generic;

public struct ShipStatGaugeEntry
{
    public string label;
    public float value;
    public float gaugeMax; // <=0 이면 게이지 없이 rawValueText만 표시
    public string rawValueText;
}

public static class ShipStatGaugeBuilder
{
    // 증가형 스탯 게이지의 만렙 기준 — "기본 능력치의 20배" (정확한 밸런스 수치 아님, 임시)
    private const float k_gaugeMaxMultiplier = 20f;

    public static List<ShipStatGaugeEntry> Build(ShipPresetData preset)
    {
        ModuleData bodyModuleData = null;
        if (System.Enum.TryParse(preset.prefabName, out EModuleSubType bodySubType))
            bodyModuleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(bodySubType);

        ShipStatFormulaSettings formula = DataManager.Instance.m_dataTableConfig.gameSettings.shipStatFormula;
        ShipFinalStats stats = ShipStatCalculator.Calculate(preset.statAllocation, formula, bodyModuleData);

        List<ShipStatGaugeEntry> entries = new();

        float baseHealth = bodyModuleData != null ? bodyModuleData.health : 0f;
        float baseTurnRate = bodyModuleData != null ? bodyModuleData.turnRate : 0f;
        float baseRepair = bodyModuleData != null ? bodyModuleData.repair : 0f;

        entries.Add(MakeGauge("체력", stats.health, baseHealth));
        entries.Add(MakeGauge("선회력", stats.turnRate, baseTurnRate));
        entries.Add(MakeGauge("수리력", stats.repair, baseRepair));

        for (int i = 0; i < stats.beamModuleSubType.Length; i++)
        {
            string slotLabel = $"빔 #{i + 1}";
            entries.Add(MakeGauge($"{slotLabel} 공격력", stats.beamAttacks[i], formula.beam.baseAttack));
            entries.Add(MakeValueOnly($"{slotLabel} 연사 쿨다운", $"{stats.beamAttackCools[i]:F2}s"));
            entries.Add(MakeValueOnly($"{slotLabel} 발사체 속도", $"{stats.beamProjectileSpeeds[i]:F1}"));
        }

        for (int i = 0; i < stats.missileModuleSubType.Length; i++)
        {
            string slotLabel = $"미사일 #{i + 1}";
            entries.Add(MakeGauge($"{slotLabel} 공격력", stats.missileAttacks[i], formula.missile.baseAttack));
            entries.Add(MakeValueOnly($"{slotLabel} 연사 쿨다운", $"{stats.missileAttackCools[i]:F2}s"));
            entries.Add(MakeValueOnly($"{slotLabel} 발사체 속도", $"{stats.missileProjectileSpeeds[i]:F1}"));
            entries.Add(MakeValueOnly($"{slotLabel} 침묵 시간", $"{stats.missileSilenceTimes[i]:F1}s"));
        }

        for (int i = 0; i < stats.hangarModuleSubType.Length; i++)
        {
            string slotLabel = $"격납고 #{i + 1}";
            entries.Add(MakeGauge($"{slotLabel} 함선 공격력", stats.hangarShipAttacks[i], formula.hangar.baseShipAttack));
            entries.Add(MakeGauge($"{slotLabel} 전투기 공격력", stats.hangarFighterAttacks[i], formula.hangar.baseFighterAttack));
            entries.Add(MakeGauge($"{slotLabel} 탄약", stats.hangarAmmos[i], formula.hangar.baseAmmo));
            entries.Add(MakeGauge($"{slotLabel} 함재기 체력", stats.hangarHealths[i], formula.hangar.baseHealth));
        }

        if (stats.shieldInstalled == true)
        {
            entries.Add(MakeGauge("실드 게이지", stats.shieldGauge, formula.shield.baseGauge));
            entries.Add(MakeValueOnly("실드 재가동 딜레이", $"{stats.shieldDelay:F2}s"));
            entries.Add(MakeGauge("실드 회복속도", stats.shieldRegenRate, formula.shield.baseRegenRate));
        }

        for (int i = 0; i < stats.interceptorDelays.Length; i++)
        {
            string slotLabel = $"요격체 #{i + 1}";
            entries.Add(MakeValueOnly($"{slotLabel} 보충 딜레이", $"{stats.interceptorDelays[i]:F2}s"));
            entries.Add(MakeGauge($"{slotLabel} 회복속도", stats.interceptorRegenRates[i], formula.interceptor.baseRegenRate));
        }

        return entries;
    }

    private static ShipStatGaugeEntry MakeGauge(string label, float value, float baseValue)
    {
        return new ShipStatGaugeEntry { label = label, value = value, gaugeMax = baseValue * k_gaugeMaxMultiplier };
    }

    private static ShipStatGaugeEntry MakeValueOnly(string label, string valueText)
    {
        return new ShipStatGaugeEntry { label = label, gaugeMax = 0f, rawValueText = valueText };
    }
}
