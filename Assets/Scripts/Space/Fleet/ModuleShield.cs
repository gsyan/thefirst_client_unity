//------------------------------------------------------------------------------
// 실드 모듈 — 슬롯/3D 배치 없이 함체(ModuleHull)에 논리적으로만 붙는 방어막.
// 게이지는 자동으로 충전되지 않고 전술 토글(idx=3)을 켰을 때만 ApplyRegenTick으로 충전됨 — UIPanelBattle.Co_DrainTacticPower 참고.
// 같은 토글이 "방어 기능 사용" 스위치도 겸함 — ON 상태에서 게이지가 있을 때 빔 피격을 막아주는 대신 매초 전술력을 소모함
using UnityEngine;

public class ModuleShield : ModuleBase
{
    [SerializeField] private ModuleHull m_parentBody;

    private float m_gauge;
    private float m_gaugeMax;
    private float m_regenRate; // 초당 게이지 회복량

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
            m_gaugeMax = 0f;
            m_gauge = 0f;
            m_regenRate = 0f;
            return;
        }

        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(shieldSubType);
        if (moduleData == null)
        {
            Debug.LogError("Failed to restore module data for ModuleShield");
            m_gaugeMax = 0f;
            m_gauge = 0f;
            m_regenRate = 0f;
            return;
        }

        m_gaugeMax = moduleData.shieldGauge;
        m_gauge = m_gaugeMax;
        m_regenRate = moduleData.shieldRegenRate;
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

        float absorbed = Mathf.Min(m_gauge, incomingDamage);
        m_gauge -= absorbed;
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
        return IsEquipped() == true && m_gauge >= incomingDamage;
    }

    // 전술 토글(실드) ON 상태에서 UIPanelBattle.Co_DrainTacticPower가 1초 간격으로 호출 — m_regenRate는 이미 초당 단위라 deltaTime 계산 불필요
    public void ApplyRegenTick()
    {
        m_gauge = Mathf.Min(m_gaugeMax, m_gauge + m_regenRate);
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
