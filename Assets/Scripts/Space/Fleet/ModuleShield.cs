//------------------------------------------------------------------------------
// 실드 모듈 — 슬롯/3D 배치 없이 함체(ModuleHull)에 논리적으로만 붙는 방어막.
// 게이지 충전은 전술 토글과 무관하게 항상 시도됨(자원이 있을 때만 ApplyRegenTick으로 충전) — UIPanelBattle.Co_DrainResource 참고.
// 전술 토글(idx=4)은 "흡수(피해감소) 효과 작동 여부"만 결정 — SpaceShip.ApplyShieldAbsorption/IsShieldActive 참고
using UnityEngine;

public class ModuleShield : ModuleBase
{
    [SerializeField] private ModuleHull m_parentBody;

    private float m_gauge;
    private float m_gaugeMax;
    private float m_regenRate; // 초당 게이지 회복량

    // 아키타입 패시브(게이지 +N%, 회복 +N%) 미반영 원본값 — RefreshPassiveStats()가 이 값 기준으로 m_gaugeMax/m_regenRate를 다시 계산
    private float m_baseGaugeMax;
    private float m_baseRegenRate;

    // 최소 게이지 소모 비율 — 데미지당 게이지 소모 감소를 아무리 쌓아도 흡수 1당 이 비율 아래로는 내려가지 않음
    private const float k_minDamageCostRatio = 0.1f;

    public void SetParentBody(ModuleHull parentBody)
    {
        m_parentBody = parentBody;
    }

    public void InitializeModuleShield(string shieldSubType)
    {
        m_parentBody = GetComponentInParent<ModuleHull>();
        AutoDetectFleetInfo();

        if (string.IsNullOrEmpty(shieldSubType) == true)
        {
            m_baseGaugeMax = 0f;
            m_baseRegenRate = 0f;
            m_gaugeMax = 0f;
            m_gauge = 0f;
            m_regenRate = 0f;
            return;
        }

        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(shieldSubType);
        if (moduleData == null)
        {
            Debug.LogError("Failed to restore module data for ModuleShield");
            m_baseGaugeMax = 0f;
            m_baseRegenRate = 0f;
            m_gaugeMax = 0f;
            m_gauge = 0f;
            m_regenRate = 0f;
            return;
        }

        // 아키타입 단위 영구 모듈강화(M6) — 이 함체 아키타입의 실드 레벨(최소 1)에 해당하는 행(shield_{level}_1)의 게이지/회복량, 행이 없으면 장착 서브타입 원본
        int archetypeLevel = m_parentBody != null ? ArchetypeUpgradeLevelLookup.GetLevel(ResolveOwnerFleet(), m_parentBody.GetHullArchetype(), EModuleType.shield) : 1;
        ModuleData levelModuleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable($"shield_{archetypeLevel}_1");
        ModuleData sourceData = levelModuleData != null ? levelModuleData : moduleData;

        m_baseGaugeMax = sourceData.shieldGauge;
        m_baseRegenRate = sourceData.shieldRegenRate;
        m_gaugeMax = m_baseGaugeMax;
        m_gauge = m_gaugeMax;
        m_regenRate = m_baseRegenRate;
        RefreshPassiveStats();
    }

    // 아키타입 패시브(실드 게이지 +N%, 실드 회복 +N%)를 원본값에 반영 — 게이지 비율은 유지(최대치가 변해도 채워진 정도는 그대로)
    public void RefreshPassiveStats()
    {
        if (m_baseGaugeMax <= 0f) return;

        float gaugeRatio = m_gaugeMax > 0f ? m_gauge / m_gaugeMax : 1f;
        float passiveGaugeMultiplier = 1f + GetPassiveEffect(HullArchetypePassiveEffects.k_shieldGaugePercent) / 100f;
        float passiveRegenMultiplier = 1f + GetPassiveEffect(HullArchetypePassiveEffects.k_shieldRegenPercent) / 100f;
        m_gaugeMax = m_baseGaugeMax * passiveGaugeMultiplier;
        m_gauge = m_gaugeMax * gaugeRatio;
        m_regenRate = m_baseRegenRate * passiveRegenMultiplier;
    }

    // 흡수 데미지 1당 게이지 소모 비율 — 아키타입 패시브(데미지당 게이지 소모 -N%)가 1에서 깎음
    private float GetDamageCostRatio()
    {
        float reductionPercent = GetPassiveEffect(HullArchetypePassiveEffects.k_shieldDamageCostReductionPercent);
        return Mathf.Max(1f - reductionPercent / 100f, k_minDamageCostRatio);
    }

    public bool IsEquipped()
    {
        return m_gaugeMax > 0f;
    }

    public float GetGauge()
    {
        return m_gauge;
    }

    public float GetGaugeMax()
    {
        return m_gaugeMax;
    }

    // 빔 피격 시 호출 — 토글 ON 여부 판단은 호출부(SpaceShip.TakeDamage) 책임. 흡수한 만큼(<= incomingDamage)을 반환, 나머지는 호출부가 체력에 그대로 적용
    public float AbsorbBeamDamage(float incomingDamage)
    {
        if (IsEquipped() == false || m_gauge <= 0f) return 0f;

        // 게이지 소모 비율이 1 미만이면 같은 게이지로 더 많은 데미지를 흡수함(흡수 가능량 = 게이지 ÷ 소모 비율)
        float costRatio = GetDamageCostRatio();
        float absorbableDamage = m_gauge / costRatio;
        float absorbed = Mathf.Min(absorbableDamage, incomingDamage);
        float gaugeCost = absorbed * costRatio;
        m_gauge = Mathf.Max(0f, m_gauge - gaugeCost);
        return absorbed;
    }

    // 전술 토글이 실질 효과를 가지는지(=게이지가 있어 실제로 방어가 발동되는지) 판정용 — 전술력 소모 가드에 사용
    public bool HasGaugeToDefend()
    {
        return IsEquipped() == true && m_gauge > 0f;
    }

    // 이번 피격 데미지를 게이지가 전량 흡수할 수 있는지 — ProjectileBeam이 raycast 단계에서 빔 궤적을 실드 표면/함체 표면 중 어디서 끊을지 미리 판단하는 데 사용
    public bool CanFullyAbsorb(float incomingDamage)
    {
        float requiredGauge = incomingDamage * GetDamageCostRatio();
        return IsEquipped() == true && m_gauge >= requiredGauge;
    }

    // 토글과 무관하게 UIPanelBattle.Co_DrainResource가 1초 간격으로 호출(자원이 있을 때만) — m_regenRate는 이미 초당 단위라 deltaTime 계산 불필요
    public void ApplyRegenTick()
    {
        m_gauge = Mathf.Min(m_gaugeMax, m_gauge + m_regenRate);
    }

    // 지원 능력(halcyon 실드 충전 부스트)용 — 기본 회복량(초당) 대비 bonusRatio만큼 추가 충전, 실제로 늘어난 게이지 양을 반환
    public float ApplyBoostRegen(float bonusRatio)
    {
        if (IsEquipped() == false || bonusRatio <= 0f) return 0f;

        float gaugeBefore = m_gauge;
        float boostAmount = m_regenRate * bonusRatio;
        m_gauge = Mathf.Min(m_gaugeMax, m_gauge + boostAmount);
        return m_gauge - gaugeBefore;
    }

    // 즉시효과 보상카드용 — 현재 게이지에 gaugeMax*healRatio만큼 가산(ApplyRegenTick과 달리 고정량이 아닌 비율)
    public void HealGaugeByRatio(float healRatio)
    {
        if (healRatio <= 0f) return;
        m_gauge = Mathf.Min(m_gaugeMax, m_gauge + m_gaugeMax * healRatio);
    }

    // 함체 교체(SpaceShip.ApplyHealthRatio)/존런 퇴각·포기 롤백 시 호출 — 두 경우 모두 "진입 직전" 또는 "갓 생성된 새 함체" 기준이라 항상 풀게이지가 맞음
    public void ResetGaugeToFull()
    {
        m_gauge = m_gaugeMax;
    }

    public float GetGaugeRatio()
    {
        return m_gaugeMax > 0f ? m_gauge / m_gaugeMax : 1f;
    }

    // 앱 재시작 시 서버에 저장된 실드 게이지 비율을 그대로 복구하는 용도 — ResetGaugeToFull과 달리 절대값이 아닌 임의 비율을 받음
    public void SetGaugeRatio(float ratio)
    {
        m_gauge = m_gaugeMax * Mathf.Clamp01(ratio);
    }
}
