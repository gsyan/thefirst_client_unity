using System.Collections.Generic;
using UnityEngine;

// 지원 능력 — phoenix(아군 수리) / halcyon(아군 실드 충전 부스트). 함선 1척당 인스턴스 1개, SpaceShip.Co_Support가 틱마다 Tick() 호출
// 동시 대상 수 = 패시브 효과(ally_repair_slot / ally_shield_charge_slot) 합, 0이면 그 능력 없음. 플레이어 함대 함선만 작동
// 대상 선정: 체력(실드) 비율이 가장 낮은 아군(자기 제외, 이미 대상인 함선 제외)을 잡고, 해제 비율에 도달할 때까지 유지한 뒤 다시 탐색 — 매 틱 대상을 갈아타지 않게 함
// 수리량은 이 함선의 수리력(repair 스탯, 초당), 실드 충전은 대상의 기본 실드 회복량에 설정 비율을 더한 값
// 지원 1회(대상 1척, 1틱)마다 전투자원 소모, 자원이 부족하면 그 대상은 이번 틱에 지원하지 않음. 대상 함선에는 지원이 이어지는 동안 반짝임 이펙트가 붙음
public class ShipSupportAbility
{
    private const string k_effectRepairSlot = "ally_repair_slot";
    private const string k_effectShieldChargeSlot = "ally_shield_charge_slot";

    private static readonly Color k_repairColor = new Color(0.3f, 1f, 0.4f);        // 수리: 초록
    private static readonly Color k_shieldChargeColor = new Color(0.3f, 0.8f, 1f);  // 실드 충전: 하늘색

    private readonly SpaceShip m_ship;
    private readonly List<SpaceShip> m_repairTargets = new List<SpaceShip>();
    private readonly List<SpaceShip> m_shieldTargets = new List<SpaceShip>();
    private readonly Dictionary<SpaceShip, EffectBase> m_repairEffects = new Dictionary<SpaceShip, EffectBase>();
    private readonly Dictionary<SpaceShip, EffectBase> m_shieldEffects = new Dictionary<SpaceShip, EffectBase>();
    private readonly List<SpaceShip> m_removeBuffer = new List<SpaceShip>();

    public ShipSupportAbility(SpaceShip ship)
    {
        m_ship = ship;
    }

    public void Tick()
    {
        if (m_ship.m_ownerFleet == null) return;
        if (m_ship.m_ownerFleet.HasPassiveSource() == false) return; // 패시브 진행도를 아는 함대(내 함대/PVP 상대)만 — 동시 대상 수가 패시브로 정해짐

        SupportAbilitySettings settings = DataManager.Instance.m_dataTableConfig.gameSettings.supportAbility;
        int repairSlotCount = Mathf.RoundToInt(HullArchetypePassiveEffects.SumEffect(m_ship, k_effectRepairSlot));
        int shieldSlotCount = Mathf.RoundToInt(HullArchetypePassiveEffects.SumEffect(m_ship, k_effectShieldChargeSlot));

        TickRepair(settings, repairSlotCount);
        TickShieldCharge(settings, shieldSlotCount);
    }

    // 대상과 이펙트를 모두 해제 — 전투 종료/이 함선 파괴 시
    public void ClearAll()
    {
        m_repairTargets.Clear();
        m_shieldTargets.Clear();
        SyncEffects(m_repairTargets, m_repairEffects, k_repairColor);
        SyncEffects(m_shieldTargets, m_shieldEffects, k_shieldChargeColor);
    }

    private void TickRepair(SupportAbilitySettings settings, int slotCount)
    {
        if (slotCount <= 0)
        {
            m_repairTargets.Clear();
            SyncEffects(m_repairTargets, m_repairEffects, k_repairColor);
            return;
        }

        float releaseRatio = settings.repairReleaseRatioPercent / 100f;
        for (int i = m_repairTargets.Count - 1; i >= 0; i--)
        {
            SpaceShip target = m_repairTargets[i];
            bool isInvalid = target == null || target.IsAlive() == false;
            bool isRepairedEnough = isInvalid == false && target.GetHealthRatio() >= releaseRatio;
            bool isOverSlot = i >= slotCount;
            if (isInvalid == true || isRepairedEnough == true || isOverSlot == true)
                m_repairTargets.RemoveAt(i);
        }

        while (m_repairTargets.Count < slotCount)
        {
            SpaceShip candidate = FindLowestHealthAlly(m_repairTargets);
            if (candidate == null) break;
            m_repairTargets.Add(candidate);
        }

        // 수리량 = 이 함선(phoenix)의 수리력(repair 스탯, 초당) — 수리력이 0이면 아군을 수리할 수 없어 자원도 쓰지 않음
        float repairPower = m_ship.GetRepairPower();
        if (repairPower > 0f)
        {
            for (int i = 0; i < m_repairTargets.Count; i++)
            {
                SpaceShip target = m_repairTargets[i];
                if (target.GetHealthRatio() >= 1f) continue;

                bool isPaid = m_ship.m_ownerFleet.TryChargeRepairTacticCost(settings.repairCostPerTick);
                if (isPaid == false) break;

                float healedAmount = target.HealByAmount(repairPower);
                BattleStatTracker.AddShipArchetypeCounter(m_ship, BattleStatTracker.k_counterAllyRepair, healedAmount);
            }
        }

        SyncEffects(m_repairTargets, m_repairEffects, k_repairColor);
    }

    private void TickShieldCharge(SupportAbilitySettings settings, int slotCount)
    {
        if (slotCount <= 0)
        {
            m_shieldTargets.Clear();
            SyncEffects(m_shieldTargets, m_shieldEffects, k_shieldChargeColor);
            return;
        }

        float releaseRatio = settings.shieldChargeReleaseRatioPercent / 100f;
        for (int i = m_shieldTargets.Count - 1; i >= 0; i--)
        {
            SpaceShip target = m_shieldTargets[i];
            bool isInvalid = target == null || target.IsAlive() == false || target.GetEquippedShield() == null;
            bool isChargedEnough = isInvalid == false && target.GetShieldRatio() >= releaseRatio;
            bool isOverSlot = i >= slotCount;
            if (isInvalid == true || isChargedEnough == true || isOverSlot == true)
                m_shieldTargets.RemoveAt(i);
        }

        while (m_shieldTargets.Count < slotCount)
        {
            SpaceShip candidate = FindLowestShieldAlly(m_shieldTargets);
            if (candidate == null) break;
            m_shieldTargets.Add(candidate);
        }

        float bonusRatio = settings.shieldChargeBonusPercent / 100f;
        for (int i = 0; i < m_shieldTargets.Count; i++)
        {
            SpaceShip target = m_shieldTargets[i];
            ModuleShield shield = target.GetEquippedShield();
            if (shield == null || shield.GetGaugeRatio() >= 1f) continue;

            bool isPaid = m_ship.m_ownerFleet.TryChargeShieldTacticCost(settings.shieldChargeCostPerTick);
            if (isPaid == false) break;

            float chargedAmount = shield.ApplyBoostRegen(bonusRatio);
            BattleStatTracker.AddShipArchetypeCounter(m_ship, BattleStatTracker.k_counterAllyShieldCharge, chargedAmount);
        }

        SyncEffects(m_shieldTargets, m_shieldEffects, k_shieldChargeColor);
    }

    // 체력 비율이 1 미만인 아군 중 가장 낮은 함선 — 자기 자신/죽은 함선/이미 대상인 함선 제외, 없으면 null
    private SpaceShip FindLowestHealthAlly(List<SpaceShip> excluded)
    {
        SpaceShip lowest = null;
        float lowestRatio = 1f;
        List<SpaceShip> fleetShips = m_ship.m_ownerFleet.m_ships;
        for (int i = 0; i < fleetShips.Count; i++)
        {
            SpaceShip candidate = fleetShips[i];
            if (candidate == null || candidate == m_ship || candidate.IsAlive() == false) continue;
            if (excluded.Contains(candidate) == true) continue;

            float ratio = candidate.GetHealthRatio();
            if (ratio >= lowestRatio) continue;

            lowest = candidate;
            lowestRatio = ratio;
        }
        return lowest;
    }

    // 실드가 장착되어 있고 게이지 비율이 1 미만인 아군 중 가장 낮은 함선 — 자기 자신/죽은 함선/이미 대상인 함선 제외, 없으면 null
    private SpaceShip FindLowestShieldAlly(List<SpaceShip> excluded)
    {
        SpaceShip lowest = null;
        float lowestRatio = 1f;
        List<SpaceShip> fleetShips = m_ship.m_ownerFleet.m_ships;
        for (int i = 0; i < fleetShips.Count; i++)
        {
            SpaceShip candidate = fleetShips[i];
            if (candidate == null || candidate == m_ship || candidate.IsAlive() == false) continue;
            if (candidate.GetEquippedShield() == null) continue;
            if (excluded.Contains(candidate) == true) continue;

            float ratio = candidate.GetShieldRatio();
            if (ratio >= lowestRatio) continue;

            lowest = candidate;
            lowestRatio = ratio;
        }
        return lowest;
    }

    // 현재 대상 목록과 이펙트 목록을 맞춤 — 대상에서 빠진 함선의 이펙트는 반납, 새 대상에는 생성. 같은 대상이 계속되면 이펙트도 그대로 유지(깜빡임 없음)
    private void SyncEffects(List<SpaceShip> targets, Dictionary<SpaceShip, EffectBase> effects, Color color)
    {
        m_removeBuffer.Clear();
        foreach (KeyValuePair<SpaceShip, EffectBase> pair in effects)
        {
            if (targets.Contains(pair.Key) == false)
                m_removeBuffer.Add(pair.Key);
        }

        for (int i = 0; i < m_removeBuffer.Count; i++)
        {
            SpaceShip removed = m_removeBuffer[i];
            EffectBase effect = effects[removed];
            if (effect != null)
                effect.ReturnEffect();
            effects.Remove(removed);
        }

        for (int i = 0; i < targets.Count; i++)
        {
            SpaceShip target = targets[i];
            if (effects.ContainsKey(target) == true) continue;

            effects[target] = SpawnEffect(target, color);
        }
    }

    // 대상 함선 위치에 반짝임 이펙트를 만들어 함선의 자식으로 붙임 — 함선이 움직여도 따라가고, 크기는 함선 크기에 맞춤(풀/프리팹이 없으면 이펙트만 생략)
    private EffectBase SpawnEffect(SpaceShip target, Color color)
    {
        EffectBase effect = ObjectManager.Instance.m_poolManager.Get<EffectBase>(EPoolName.EFFECT_SUPPORT_SPARKLE);
        if (effect == null) return null;

        Bounds targetBounds = target.CalculateShipBounds();
        effect.transform.position = targetBounds.center;
        effect.transform.localScale = Vector3.one * targetBounds.extents.magnitude;
        effect.transform.SetParent(target.transform, true);
        effect.SetColor(color);
        effect.PlayEffect();
        return effect;
    }
}
