//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ModuleBeam : ModuleBase
{
    [SerializeField] private ModuleBody m_parentBody;
    public ModuleInfo m_moduleInfo;

    // 무기 전용 스탯
    [SerializeField] private int m_attackFireCount;
    [SerializeField] private float m_attackCoolTime;

    [SerializeField] private float m_lastAttackTime;

    // 발사대 관련
    [SerializeField] private List<LauncherBase> m_launchers = new List<LauncherBase>();

    private ModuleBody m_currentTarget;
    private Coroutine m_autoAttackCoroutine;


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






    public void InitializeModuleBeam(ModuleInfo moduleInfo, ModuleBody parentBody, ModuleSlot moduleSlot)
    {
        m_moduleInfo = moduleInfo;
        m_parentBody = parentBody;
        m_moduleSlot = moduleSlot;

        // 서버 데이터로부터 완전한 모듈 데이터 복원
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, m_moduleInfo.moduleLevel);
        if (moduleData == null)
        {
            Debug.LogError("Failed to restore module data for ModuleBeam");
            return;
        }

        // 복원된 데이터로 스탯 설정
        m_health = moduleData.m_health;
        m_healthMax = moduleData.m_health;
        m_attackPower = moduleData.m_attackPower;
        m_attackFireCount = moduleData.m_attackFireCount;
        m_attackCoolTime = moduleData.m_attackCoolTime;

        // 업그레이드 비용 설정
        m_upgradeCost = moduleData.m_upgradeCost;

        m_lastAttackTime = 0f;

        // 무기 서브 타입 초기화
        InitializeSubType(moduleData);

        // 함대 정보 자동 설정
        AutoDetectFleetInfo();

        // 부모 바디에 이 무기 등록
        if (m_parentBody != null)
            m_parentBody.AddBeam(this);
    }

    private void InitializeSubType(ModuleData moduleData)
    {
        switch (m_moduleInfo.moduleSubType)
        {
            case EModuleSubType.Beam_Standard:
            case EModuleSubType.Beam_Advanced:
                for(int i=0; i< moduleData.m_attackFireCount; i++)
                {
                    LauncherBeam launcher = gameObject.AddComponent<LauncherBeam>();
                    launcher.InitializeLauncherBeam(moduleData);
                    m_launchers.Add(launcher);
                }
                break;
            default:
                break;
        }
    }


    public override void Start()
    {
        m_autoAttackCoroutine = StartCoroutine(AutoAttack());
    }

    public override void RestartCoroutines()
    {
        if (m_autoAttackCoroutine != null)
        {
            StopCoroutine(m_autoAttackCoroutine);
        }
        m_autoAttackCoroutine = StartCoroutine(AutoAttack());
    }

    private IEnumerator AutoAttack()
    {
        while (true)
        {
            if (m_health <= 0)
            {
                yield return null;
                continue;
            }

            if( m_moduleState != EModuleState.Battle ) yield return null;

            if (m_currentTarget != null && m_currentTarget.m_health > 0)
            {
                if (Time.time >= m_lastAttackTime + m_attackCoolTime)
                {
                    ExecuteAttackOnTarget(m_currentTarget);
                    m_lastAttackTime = Time.time;
                }
            }

            yield return null;
        }
    }
    
    private void ExecuteAttackOnTarget(ModuleBody target)
    {
        foreach (var launcher in m_launchers)
        {
            if (launcher != null)
                launcher.FireAtTarget(target, m_attackPower, this);
        }
    }

    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);
        
        if (m_health <= 0)
        {
            // 부모 바디에서 이 무기 제거
            if (m_parentBody != null)
                m_parentBody.RemoveBeam(this);
            
            // 비활성화
            gameObject.SetActive(false);
        }
    }

    public override CapabilityProfile GetModuleCapabilityProfile(bool bByInfo)
    {
        if (bByInfo == true) return CommonUtility.GetModuleCapabilityProfile(m_moduleInfo);

        CapabilityProfile stats = new CapabilityProfile();
        if (m_health <= 0) return stats;
        stats.hp = m_health;
        stats.totalWeapons = 1;
        // DPS 계산: 공격력 × 발사 개수 / 쿨타임
        if (m_attackCoolTime > 0)
            stats.attackDps = m_attackPower * m_attackFireCount / m_attackCoolTime;
        return stats;
    }

    

    public override void ApplyModuleLevelUp(int newLevel)
    {
        // 레벨 설정
        SetModuleLevel(newLevel);

        // 새 레벨의 ModuleData 가져오기
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, newLevel);
        if (moduleData == null)
        {
            Debug.LogError($"Failed to restore module data for level {newLevel}");
            return;
        }

        // 스탯 갱신
        m_healthMax = moduleData.m_health;
        m_health = Mathf.Min(m_health, m_healthMax); // 현재 체력이 최대치를 넘지 않도록
        m_attackPower = moduleData.m_attackPower;
        m_attackFireCount = moduleData.m_attackFireCount;
        m_attackCoolTime = moduleData.m_attackCoolTime;
        m_upgradeCost = moduleData.m_upgradeCost;

        Debug.Log($"ModuleBeam leveled up to {newLevel}: HP={m_healthMax}, AttackPower={m_attackPower}, FireCount={m_attackFireCount}, CoolTime={m_attackCoolTime}");
    }

    public override int GetModuleBodyIndex()
    {
        return m_moduleInfo.bodyIndex;
    }
    public override void SetModuleBodyIndex(int bodyIndex)
    {
        m_moduleInfo.bodyIndex = bodyIndex;
    }

    



    

    public void SetTarget(ModuleBody target)
    {
        m_currentTarget = target;
    }

    

    // 무기가 공격 가능한 상태인지 체크
    public bool CanAttack()
    {
        return m_health > 0 && Time.time >= m_lastAttackTime + m_attackCoolTime;
    }
    
    // 다음 공격까지 남은 시간
    public float GetRemainingCoolTime()
    {
        float remaining = (m_lastAttackTime + m_attackCoolTime) - Time.time;
        return Mathf.Max(0f, remaining);
    }
    
    // 무기 스탯 Getter들
    public int GetAttackFireCount() { return m_attackFireCount; }
    public float GetAttackCoolTime() { return m_attackCoolTime; }

    // 체력 비율 반환 (체력에 따른 성능 감소 계산용)
    public float GetHealthRatio()
    {
        return m_healthMax > 0 ? m_health / m_healthMax : 0f;
    }
    
    // 파괴 시 정리
    private void OnDestroy()
    {
        if (m_parentBody != null)
            m_parentBody.RemoveBeam(this);
    }

    public override string GetUpgradeComparisonText()
    {
        ModuleData currentStats = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, m_moduleInfo.moduleLevel);
        ModuleData upgradeStats = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, m_moduleInfo.moduleLevel + 1);

        if (currentStats == null)
            return "Current module data not found.";

        if (upgradeStats == null)
            return "Upgrade not available (max level reached).";

        string comparison = $"=== UPGRADE COMPARISON ===\n";
        comparison += $"Level: {currentStats.m_moduleLevel} -> {upgradeStats.m_moduleLevel}\n";
        comparison += $"HP: {currentStats.m_health:F0} -> {upgradeStats.m_health:F0}\n";
        comparison += $"Attack Power: {currentStats.m_attackPower:F1} -> {upgradeStats.m_attackPower:F1}\n";
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
