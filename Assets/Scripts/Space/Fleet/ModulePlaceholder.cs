//------------------------------------------------------------------------------
using UnityEngine;


public class ModulePlaceholder : ModuleBase
{
    [SerializeField] private ModuleBody m_parentBody;
    public ModuleInfo m_moduleInfo;
    
    

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
        return m_moduleInfo.moduleSubType;
    }
    public override int GetModuleSlotIndex()
    {
        return m_moduleSlot.m_moduleSlotInfo.slotIndex;
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
            moduleSubType = EModuleSubType.none,
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
    
    // 플레이스홀더: 슬롯 타입에 맞는 레벨1 기준 수치 표시
    public override void SetModuleStatRows(System.Collections.Generic.List<RowLabelValue> statRows)
    {
        EModuleType moduleType = GetModuleType();
        EModuleSubType subType = CommonUtility.GetDefaultSubType(moduleType);
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, 1);
        if (moduleData == null) return;

        statRows[1].SetValues("1");

        
        if (moduleType == EModuleType.engine)
        {
            statRows[2].SetRow("speed_power", $"{moduleData.m_movementSpeed:F0}");
            statRows[3].SetRow("empty_text", "");
            statRows[4].SetRow("empty_text", "");
            statRows[5].SetRow("empty_text", "");
            statRows[6].SetRow("empty_text", "");
        }
        else if (moduleType == EModuleType.beam || moduleType == EModuleType.missile)
        {
            statRows[2].SetRow("attack_power", $"{moduleData.m_attackPower:F0}");
            statRows[3].SetRow("empty_text", "");
            statRows[4].SetRow("empty_text", "");
            statRows[5].SetRow("empty_text", "");
            statRows[6].SetRow("empty_text", "");
        }
        else if (moduleType == EModuleType.hanger)
        {
            statRows[2].SetRow("aircraft_attack_power", $"{moduleData.m_aircraftAttackPower:F0}");
            statRows[3].SetRow("aircraft_health_power", $"{moduleData.m_aircraftHealth:F0}");
            statRows[4].SetRow("aircraft_speed_power", $"{moduleData.m_aircraftSpeed:F0}");
            statRows[5].SetRow("aircraft_count", $"{moduleData.m_hangarCapability}");
            statRows[6].SetRow("aircraft_launch_count", $"{moduleData.m_launchCount}");
        }
    }


}
