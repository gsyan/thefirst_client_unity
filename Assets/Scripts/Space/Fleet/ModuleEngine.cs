//------------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;


public class ModuleEngine : ModuleBase
{
    [SerializeField] private ModuleBody m_parentBody;
    public ModuleInfo m_moduleInfo;

    // 엔진 전용 스탯
    [SerializeField] private float m_movementSpeed;

    public override void Start()
    {
        // 추가 초기화가 필요하면 여기에
    }

    public override void RestartCoroutines()
    {
        // 엔진은 현재 코루틴을 사용하지 않음
        // 향후 코루틴 추가 시 여기에 구현
    }

    

    public override void Attack(SpaceShip target)
    {
        // 엔진은 공격하지 않음
        // base.Attack 호출하지 않음
    }
    
    public override CapabilityProfile GetModuleCapabilityProfile(bool bByInfo)
    {
        if (bByInfo == true) return CommonUtility.GetModuleCapabilityProfile(m_moduleInfo);

        CapabilityProfile stats = new CapabilityProfile();
        stats.speed_power = m_movementSpeed;
        stats.totalEngines = 1;
        return stats;
    }

    public override EModuleType GetModuleType()
    {
        return m_moduleInfo.moduleType;
    }
    public override EModuleSubType GetModuleSubType()
    {
        return m_moduleInfo.moduleSubType;
    }
    public override int GetModuleSlotIndex()
    {
        return m_moduleInfo.slotIndex;
    }
    public override int GetModuleLevel()
    {
        return m_moduleInfo.moduleLevel;
    }
    public override void SetModuleLevel(int level)
    {
        m_moduleInfo.moduleLevel = level;
    }

    public override void ApplyModuleLevelUp(int newLevel)
    {
        // 레벨 설정
        SetModuleLevel(newLevel);

        // 새 레벨의 ModuleData 가져오기
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, newLevel);
        if (moduleData == null) return;
        
        // 스탯 갱신
        m_healthMax = moduleData.health;
        m_health = Mathf.Min(m_health, m_healthMax);
        m_movementSpeed = moduleData.speed;
        m_upgradeCost = moduleData.upgradeCost;
    }

    public override int GetModuleBodyIndex()
    {
        return m_moduleInfo.bodyIndex;
    }
    public override void SetModuleBodyIndex(int bodyIndex)
    {
        m_moduleInfo.bodyIndex = bodyIndex;
    }

    public void InitializeModuleEngine(ModuleInfo moduleInfo, ModuleBody parentBody, ModuleSlot moduleSlot)
    {
        m_moduleInfo = moduleInfo;
        m_moduleSlot = moduleSlot;
        m_parentBody = parentBody;
        SetUnlockedSubTypes(moduleInfo.unlockedSubTypes);

        // 서버 데이터로부터 완전한 모듈 데이터 복원
        var moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, m_moduleInfo.moduleLevel);
        if (moduleData == null)
        {
            Debug.LogError("Failed to restore module data for ModuleEngine");
            return;
        }

        // 복원된 데이터로 스탯 설정
        m_health = moduleData.health;
        m_healthMax = moduleData.health;
        m_attackPower = 0.0f; // 엔진은 공격하지 않음

        // 엔진 전용 스탯 설정
        m_movementSpeed = moduleData.speed;

        // 업그레이드 비용 설정
        m_upgradeCost = moduleData.upgradeCost;

        // 함대 정보 자동 설정
        AutoDetectFleetInfo();

        // 부모 바디에 이 엔진 등록
        if (m_parentBody != null)
        {
            m_parentBody.AddEngine(this);
        }
    }

    public override void SetModuleStatRows(List<RowLabelValue> statRows)
    {
        EModuleSubType subType = GetModuleSubType();
        int currentLevel = GetModuleLevel();
        ModuleData moduleDataCurrent = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, currentLevel);
        if (moduleDataCurrent == null) return;

        statRows[1].SetRow("level", $"{currentLevel}");
        statRows[2].SetRow("speed_power", $"{moduleDataCurrent.speed:F0}");
    }

    // 파괴 시 정리
    private void OnDestroy()
    {
        if (m_parentBody != null)
            m_parentBody.RemoveEngine(this);
    }

}
