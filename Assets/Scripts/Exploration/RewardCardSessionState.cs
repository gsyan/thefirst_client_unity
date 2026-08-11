// 이번 존 런에서 선택 확정한 보상카드 지속버프 누적 상태 — 존 런 시작 시 초기화, 탈출/포기 시 폐기(세션 스코프)
// Dictionary<ECardEffectType, float> 하나로 관리 — 카드(강도/종류)가 늘어나도 ApplyCard는 수정 불필요,
// ApplyToShipStats도 ShipFinalStats에 새 필드가 생길 때만 매핑 한 줄 추가하면 됨(효과 enum 추가 자체는 매핑 불필요)
using System.Collections.Generic;

public class RewardCardSessionState
{
    private readonly Dictionary<ECardEffectType, float> m_multipliers = new Dictionary<ECardEffectType, float>();

    public void Reset()
    {
        m_multipliers.Clear();
    }

    // 즉시효과(isPersistent == false) 카드는 여기 누적하지 않음 — 선택 즉시 별도 처리(회복 등)로 소모됨
    public void ApplyCard(RewardCardData card)
    {
        if (card == null) return;
        if (card.isPersistent == false) return;

        float current = GetMultiplier(card.effectType);
        m_multipliers[card.effectType] = current * (1f + card.value1);
    }

    public float GetMultiplier(ECardEffectType effectType)
    {
        float value;
        bool found = m_multipliers.TryGetValue(effectType, out value);
        return found ? value : 1f;
    }

    // 함선 최종 스탯(ShipStatCalculator.Calculate 결과)에 지속버프 배율을 곱한다 — 값 타입(struct)이라 결과를 새로 만들어 반환
    public ShipFinalStats ApplyToShipStats(ShipFinalStats stats)
    {
        stats.health *= GetMultiplier(ECardEffectType.Buff_ShipHealth);

        ScaleArray(stats.beamAttacks, GetMultiplier(ECardEffectType.Buff_BeamAttack));
        ScaleCoolArray(stats.beamAttackCools, GetMultiplier(ECardEffectType.Buff_BeamFireRate));
        ScaleArray(stats.missileAttacks, GetMultiplier(ECardEffectType.Buff_MissileAttack));
        ScaleCoolArray(stats.missileAttackCools, GetMultiplier(ECardEffectType.Buff_MissileFireRate));
        ScaleArray(stats.missileSilenceTimes, GetMultiplier(ECardEffectType.Buff_MissileSilence));
        ScaleArray(stats.hangarShipAttacks, GetMultiplier(ECardEffectType.Buff_HangarShipAttack));
        ScaleArray(stats.hangarFighterAttacks, GetMultiplier(ECardEffectType.Buff_HangarFighterAttack));

        return stats;
    }

    private void ScaleArray(float[] values, float multiplier)
    {
        if (values == null) return;
        for (int i = 0; i < values.Length; i++)
            values[i] *= multiplier;
    }

    // 쿨다운은 낮을수록 빠름 — 연사속도 배율(1+value1)만큼 나눠서 쿨다운을 줄임
    private void ScaleCoolArray(float[] values, float multiplier)
    {
        if (values == null || multiplier <= 0f) return;
        for (int i = 0; i < values.Length; i++)
            values[i] /= multiplier;
    }

    public float GetExplorationPointRateMultiplier() { return GetMultiplier(ECardEffectType.Buff_ExplorationPointRate); }
}
