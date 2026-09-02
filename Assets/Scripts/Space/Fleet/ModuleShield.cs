//------------------------------------------------------------------------------
// 실드 모듈 — 슬롯/3D 배치 없이 함체(ModuleHull)에 논리적으로만 붙는 방어막.
// 게이지는 토글 on/off와 무관하게 항상 자연 충전됨. 전술 토글(idx=3)은 "방어 기능 사용"의 스위치로,
// ON 상태에서 게이지가 있을 때 빔 피격을 막아주는 대신 매초 전술력을 소모함 — UIPanelBattle.Co_DrainTacticPower 참고
// 충전은 매 프레임 갱신하지 않고, 게이지를 읽거나 소모하는 시점에 경과시간만큼 한 번에 계산(lazy) — Update 남용 방지
using UnityEngine;

public class ModuleShield : ModuleBase
{
    [SerializeField] private ModuleHull m_parentBody;

    private float m_gauge;
    private float m_gaugeMax;
    private float m_regenRate; // 초당 게이지 회복량
    private float m_lastRegenTime; // 마지막으로 게이지를 정산한 Time.time

    public void SetParentBody(ModuleHull parentBody)
    {
        m_parentBody = parentBody;
    }

    public void InitializeModuleShield(EModuleSubType shieldSubType)
    {
        m_parentBody = GetComponentInParent<ModuleHull>();
        AutoDetectFleetInfo();

        if (shieldSubType == EModuleSubType.none)
        {
            m_gaugeMax = 0f;
            m_gauge = 0f;
            m_regenRate = 0f;
            m_lastRegenTime = Time.time;
            return;
        }

        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(shieldSubType);
        if (moduleData == null)
        {
            Debug.LogError("Failed to restore module data for ModuleShield");
            m_gaugeMax = 0f;
            m_gauge = 0f;
            m_regenRate = 0f;
            m_lastRegenTime = Time.time;
            return;
        }

        m_gaugeMax = moduleData.shieldGauge;
        m_gauge = m_gaugeMax;
        m_regenRate = moduleData.shieldRegenRate;
        m_lastRegenTime = Time.time;
    }

    public bool IsEquipped()
    {
        return m_gaugeMax > 0f;
    }

    // 마지막 정산 이후 경과시간만큼 게이지를 충전(상한 클램프) — 게이지를 조회/소비하는 진입점마다 먼저 호출
    private void CatchUpRegen()
    {
        if (IsEquipped() == false) return;

        float elapsed = Time.time - m_lastRegenTime;
        m_lastRegenTime = Time.time;
        if (elapsed <= 0f || m_gauge >= m_gaugeMax) return;

        m_gauge = Mathf.Min(m_gaugeMax, m_gauge + m_regenRate * elapsed);
    }

    public float GetGauge()
    {
        CatchUpRegen();
        return m_gauge;
    }

    public float GetGaugeMax()
    {
        return m_gaugeMax;
    }

    // 빔 피격 시 호출 — 토글 ON 여부 판단은 호출부(SpaceShip.TakeDamage) 책임. 흡수한 만큼(<= incomingDamage)을 반환, 나머지는 호출부가 체력에 그대로 적용
    public float AbsorbBeamDamage(float incomingDamage)
    {
        CatchUpRegen();
        if (IsEquipped() == false || m_gauge <= 0f) return 0f;

        float absorbed = Mathf.Min(m_gauge, incomingDamage);
        m_gauge -= absorbed;
        return absorbed;
    }

    // 전술 토글이 실질 효과를 가지는지(=게이지가 있어 실제로 방어가 발동되는지) 판정용 — 전술력 소모 가드에 사용
    public bool HasGaugeToDefend()
    {
        CatchUpRegen();
        return IsEquipped() == true && m_gauge > 0f;
    }

    // 이번 피격 데미지를 게이지가 전량 흡수할 수 있는지 — ProjectileBeam이 raycast 단계에서 빔 궤적을 실드 표면/함체 표면 중 어디서 끊을지 미리 판단하는 데 사용
    public bool CanFullyAbsorb(float incomingDamage)
    {
        CatchUpRegen();
        return IsEquipped() == true && m_gauge >= incomingDamage;
    }

    // 함체 교체(SpaceShip.ApplyHealthRatio)/존런 퇴각·포기 롤백 시 호출 — 두 경우 모두 "진입 직전" 또는 "갓 생성된 새 함체" 기준이라 항상 풀게이지가 맞음
    public void ResetGaugeToFull()
    {
        m_gauge = m_gaugeMax;
        m_lastRegenTime = Time.time;
    }
}
