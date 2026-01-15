//------------------------------------------------------------------------------
using UnityEngine;


public class ModulePlaceholder : ModuleBase
{
    [SerializeField] private ModuleBody m_parentBody;
    public ModuleInfo m_moduleInfo;
    
    

    public override string GetUpgradeComparisonText()
    {
        string comparison = $"Empty Slot. Select Module First";   
        return comparison;
    }

    public override void ApplyShipStateToModule()
    {
        // 플레이스홀더는 상태 변화에 반응하지 않음
    }

    public override void TakeDamage(float damage)
    {
        // 플레이스홀더는 데미지를 받지 않음
    }


    public override EModuleType GetModuleType()
    {
        return m_moduleSlot.m_moduleSlotInfo.moduleType;
    }
    public override EModuleSubType GetModuleSubType()
    {
        return m_moduleSlot.m_moduleSlotInfo.moduleSubType;
    }
    public override int GetModuleBodyIndex()
    {
        return m_moduleInfo.bodyIndex;
    }



    public void InitializeModulePlaceholder(ModuleBody parentBody, ModuleSlot moduleSlot)
    {
        m_moduleInfo = new ModuleInfo
        {
            moduleType = moduleSlot.m_moduleSlotInfo.moduleType,
            moduleSubType = moduleSlot.m_moduleSlotInfo.moduleSubType,
            moduleLevel = 0,
            bodyIndex = parentBody.GetModuleBodyIndex(),
            slotIndex = moduleSlot.m_moduleSlotInfo.slotIndex
        };
        m_moduleSlot = moduleSlot;
        m_parentBody = parentBody;


        // 함대 정보 자동 설정
        AutoDetectFleetInfo();

        // 플레이스홀더는 체력이나 공격력이 없음
        m_health = 0f;
        m_healthMax = 0f;
        m_attackPower = 0f;
    }
    


}
