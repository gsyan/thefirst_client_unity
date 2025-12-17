//------------------------------------------------------------------------------
using UnityEngine;

public class ModuleEngine : ModuleBase
{
    [SerializeField] private ModuleBody m_parentBody;
    public ModuleEngineInfo m_moduleEngineInfo;

    // 엔진 전용 스탯
    [SerializeField] private float m_movementSpeed;
    [SerializeField] private float m_rotationSpeed;

    override public void Start()
    {
        // 추가 초기화가 필요하면 여기에
    }

    

    override public void Attack(SpaceShip target)
    {
        // 엔진은 공격하지 않음
        // base.Attack 호출하지 않음
    }
    
    override public void TakeDamage(float damage)
    {
        base.TakeDamage(damage);
        
        if (m_health <= 0)
        {
            Debug.Log($"[{GetFleetName()}] ModuleEngine[Body{m_moduleEngineInfo.bodyIndex}-Slot{m_moduleSlot.m_slotIndex}] destroyed!");
            
            // 부모 바디에서 이 엔진 제거
            if (m_parentBody != null)
            {
                m_parentBody.RemoveEngine(this);
            }
            
            // 비활성화
            gameObject.SetActive(false);
        }
    }

    public override EModuleType GetModuleType()
    {
        return EModuleType.Engine;
    }
    public override int GetPackedModuleType()
    {
        return m_moduleEngineInfo.moduleType;
    }
    public override int GetModuleLevel()
    {
        return m_moduleEngineInfo.moduleLevel;
    }
    override public void SetModuleLevel(int level)
    {
        m_moduleEngineInfo.moduleLevel = level;
    }
    override public int GetModuleBodyIndex()
    {
        return m_moduleEngineInfo.bodyIndex;
    }
    override public void SetModuleBodyIndex(int bodyIndex)
    {
        m_moduleEngineInfo.bodyIndex = bodyIndex;
    }

    public void InitializeModuleEngine(ModuleEngineInfo moduleEngineInfo, ModuleBody parentBody, ModuleSlot slot)
    {
        m_moduleEngineInfo = moduleEngineInfo;
        m_moduleSlot = slot;

        m_parentBody = parentBody;

        // 서버 데이터로부터 완전한 모듈 데이터 복원
        var moduleData = DataManager.Instance.RestoreEngineModuleData(m_moduleEngineInfo.ModuleSubType, m_moduleEngineInfo.moduleLevel);
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
        m_rotationSpeed = moduleData.m_rotationSpeed;

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
    
    // 현재 제공하는 이동 속도 (체력에 따라 감소 가능)
    public float GetMovementSpeed()
    {
        if (m_health <= 0) return 0f;

        // 체력 비율에 따른 성능 감소 (선택적)
        float healthRatio = m_health / m_healthMax;
        return m_movementSpeed * healthRatio;
    }

    // 현재 제공하는 회전 속도 (체력에 따라 감소 가능)
    public float GetRotationSpeed()
    {
        if (m_health <= 0) return 0f;

        // 체력 비율에 따른 성능 감소 (선택적)
        float healthRatio = m_health / m_healthMax;
        return m_rotationSpeed * healthRatio;
    }
    
    // 엔진 스탯 Getter들 (원본 값)
    public float GetBaseMovementSpeed() { return m_movementSpeed; }
    public float GetBaseRotationSpeed() { return m_rotationSpeed; }

    // 파괴 시 정리
    private void OnDestroy()
    {
        if (m_parentBody != null)
        {
            m_parentBody.RemoveEngine(this);
        }
    }

    override public string GetUpgradeComparisonText()
    {
        var currentStats = DataManager.Instance.RestoreEngineModuleData(m_moduleEngineInfo.ModuleSubType, m_moduleEngineInfo.moduleLevel);
        var upgradeStats = DataManager.Instance.RestoreEngineModuleData(m_moduleEngineInfo.ModuleSubType, m_moduleEngineInfo.moduleLevel + 1);

        if (currentStats == null)
            return "Current module data not found.";

        if (upgradeStats == null)
            return "Upgrade not available (max level reached).";

        string comparison = $"=== UPGRADE COMPARISON ===\n";
        comparison += $"Level: {currentStats.m_level} -> {upgradeStats.m_level}\n";
        comparison += $"HP: {currentStats.m_health:F0} -> {upgradeStats.m_health:F0}\n";
        comparison += $"Speed: {currentStats.m_movementSpeed:F1} -> {upgradeStats.m_movementSpeed:F1}\n";
        comparison += $"Rotation: {currentStats.m_rotationSpeed:F1} -> {upgradeStats.m_rotationSpeed:F1}\n";
        string costString = $"Cost: Tech Level {currentStats.m_upgradeCost.techLevel}";
        if (currentStats.m_upgradeCost.mineral > 0)
            costString += $", Mineral {currentStats.m_upgradeCost.mineral}";
        if (currentStats.m_upgradeCost.mineralRare > 0)
            costString += $", MineralRare {currentStats.m_upgradeCost.mineral}";
        if (currentStats.m_upgradeCost.mineralExotic > 0)
            costString += $", MineralExotic {currentStats.m_upgradeCost.mineral}";
        if (currentStats.m_upgradeCost.mineralDark > 0)
            costString += $", MineralDark {currentStats.m_upgradeCost.mineral}";
        comparison += costString;

        return comparison;
    }
}
