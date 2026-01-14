//------------------------------------------------------------------------------
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
    
    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);
        
        if (m_health <= 0)
        {
            // 부모 바디에서 이 엔진 제거
            if (m_parentBody != null)
                m_parentBody.RemoveEngine(this);
            
            // 비활성화
            gameObject.SetActive(false);
        }
    }

    public override CapabilityProfile GetCapabilityProfile()
    {
        CapabilityProfile stats = new CapabilityProfile();

        if (m_health <= 0) return stats;

        stats.engineSpeed = GetEngineSpeed();
        stats.totalEngines = 1;

        return stats;
    }

    public override EModuleType GetModuleType()
    {
        return m_moduleInfo.ModuleType;
    }
    public override EModuleSubType GetModuleSubType()
    {
        return m_moduleInfo.ModuleSubType;
    }
    public override EModuleSlotType GetModuleSlotType()
    {
        return m_moduleInfo.ModuleSlotType;
    }
    public override int GetModuleTypePacked()
    {
        return m_moduleInfo.moduleTypePacked;
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
        ModuleData moduleData = DataManager.Instance.RestoreModuleData(m_moduleInfo.moduleTypePacked, newLevel);
        if (moduleData == null)
        {
            Debug.LogError($"Failed to restore module data for level {newLevel}");
            return;
        }

        // 스탯 갱신
        m_healthMax = moduleData.m_health;
        m_health = Mathf.Min(m_health, m_healthMax);
        m_movementSpeed = moduleData.m_movementSpeed;
        m_upgradeCost = moduleData.m_upgradeCost;

        Debug.Log($"ModuleEngine leveled up to {newLevel}: HP={m_healthMax}, MovementSpeed={m_movementSpeed}");
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

        // 서버 데이터로부터 완전한 모듈 데이터 복원
        var moduleData = DataManager.Instance.RestoreModuleData(m_moduleInfo.ModuleSubType, m_moduleInfo.moduleLevel);
        if (moduleData == null)
        {
            Debug.LogError("Failed to restore module data for ModuleEngine");
            return;
        }

        // 복원된 데이터로 스탯 설정
        m_health = moduleData.m_health;
        m_healthMax = moduleData.m_health;
        m_attackPower = 0.0f; // 엔진은 공격하지 않음

        // 엔진 전용 스탯 설정
        m_movementSpeed = moduleData.m_movementSpeed;

        // 업그레이드 비용 설정
        m_upgradeCost = moduleData.m_upgradeCost;

        // 함대 정보 자동 설정
        AutoDetectFleetInfo();

        // 부모 바디에 이 엔진 등록
        if (m_parentBody != null)
        {
            m_parentBody.AddEngine(this);
        }
    }

    
    
    // 엔진이 작동 가능한 상태인지 체크
    public bool IsOperational()
    {
        return m_health > 0;
    }
    
    // 현재 제공하는 엔진 속도 (체력에 따라 감소 가능)
    public float GetEngineSpeed()
    {
        if (m_health <= 0) return 0f;

        // 체력 비율에 따른 성능 감소 (선택적)
        float healthRatio = m_health / m_healthMax;
        return m_movementSpeed * healthRatio;
    }

    // 엔진 스탯 Getter (원본 값)
    public float GetBaseEngineSpeed() { return m_movementSpeed; }

    // 파괴 시 정리
    private void OnDestroy()
    {
        if (m_parentBody != null)
        {
            m_parentBody.RemoveEngine(this);
        }
    }

    public override string GetUpgradeComparisonText()
    {
        ModuleData currentStats = DataManager.Instance.RestoreModuleData(m_moduleInfo.ModuleSubType, m_moduleInfo.moduleLevel);
        ModuleData upgradeStats = DataManager.Instance.RestoreModuleData(m_moduleInfo.ModuleSubType, m_moduleInfo.moduleLevel + 1);

        if (currentStats == null)
            return "Current module data not found.";

        if (upgradeStats == null)
            return "Upgrade not available (max level reached).";

        string comparison = $"=== UPGRADE COMPARISON ===\n";
        comparison += $"Level: {currentStats.m_moduleLevel} -> {upgradeStats.m_moduleLevel}\n";
        comparison += $"HP: {currentStats.m_health:F0} -> {upgradeStats.m_health:F0}\n";
        comparison += $"Engine Speed: {currentStats.m_movementSpeed:F1} -> {upgradeStats.m_movementSpeed:F1}\n";
        string costString = $"Cost: Tech Level {currentStats.m_upgradeCost.techLevel}";
        if (currentStats.m_upgradeCost.mineral > 0)
            costString += $", Mineral {currentStats.m_upgradeCost.mineral}";
        if (currentStats.m_upgradeCost.mineralRare > 0)
            costString += $", MineralRare {currentStats.m_upgradeCost.mineralRare}";
        if (currentStats.m_upgradeCost.mineralExotic > 0)
            costString += $", MineralExotic {currentStats.m_upgradeCost.mineralExotic}";
        if (currentStats.m_upgradeCost.mineralDark > 0)
            costString += $", MineralDark {currentStats.m_upgradeCost.mineralDark}";
        comparison += costString;

        return comparison;
    }
}
