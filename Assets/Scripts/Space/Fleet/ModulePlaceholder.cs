//------------------------------------------------------------------------------
using UnityEngine;

public class ModulePlaceholder : ModuleBase
{
    public override EModuleType GetModuleType()
    {
        if (m_moduleSlot != null)
        {
            // 슬롯의 타입을 기반으로 모듈 타입 반환
            return (EModuleType)m_moduleSlot.m_moduleType;
        }
        return EModuleType.None;
    }

    public void InitializeModulePlaceholder(ModuleSlot slot)
    {
        m_moduleSlot = slot;

        // 함대 정보 자동 설정
        AutoDetectFleetInfo();

        // 플레이스홀더는 체력이나 공격력이 없음
        m_health = 0f;
        m_healthMax = 0f;
        m_attackPower = 0f;
    }

    public override void ApplyShipStateToModule()
    {
        // 플레이스홀더는 상태 변화에 반응하지 않음
    }

    public override void TakeDamage(float damage)
    {
        // 플레이스홀더는 데미지를 받지 않음
    }
}
