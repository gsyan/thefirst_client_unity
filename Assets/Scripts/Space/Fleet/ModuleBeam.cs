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
        SetUnlockedSubTypes(moduleInfo.unlockedSubTypes);

        // 서버 데이터로부터 완전한 모듈 데이터 복원
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, m_moduleInfo.moduleLevel);
        if (moduleData == null)
        {
            Debug.LogError("Failed to restore module data for ModuleBeam");
            return;
        }

        // 복원된 데이터로 스탯 설정
        m_health = moduleData.health;
        m_healthMax = moduleData.health;
        m_attackPower = moduleData.attackPower;
        m_attackFireCount = moduleData.attackFireCount;
        m_attackCoolTime = moduleData.attackCoolTime;

        // 업그레이드 비용 설정
        m_upgradeCost = moduleData.upgradeCost;

        m_lastAttackTime = 0f;

        // 함대 정보 자동 설정
        AutoDetectFleetInfo();

        // 무기 서브 타입 초기화
        InitializeSubType(moduleData);

        // 부모 바디에 이 무기 등록
        if (m_parentBody != null)
            m_parentBody.AddBeam(this);
    }

    private void InitializeSubType(ModuleData moduleData)
    {
        switch (m_moduleInfo.moduleSubType)
        {
            case EModuleSubType.beam_t1_std_ver1:
            case EModuleSubType.beam_t1_adv_ver1:
                for(int i=0; i< moduleData.attackFireCount; i++)
                {
                    LauncherBeam launcher = gameObject.AddComponent<LauncherBeam>();
                    launcher.InitializeLauncherBeam(moduleData, i, m_myFleet != null && m_myFleet.IsEnemy);
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
            {
                launcher.FireAtTarget(target, m_attackPower, this);
            }
                
        }
    }

    public override CapabilityProfile GetModuleCapabilityProfile(bool bByInfo)
    {
        if (bByInfo == true) return CommonUtility.GetModuleCapabilityProfile(m_moduleInfo);

        CapabilityProfile stats = new CapabilityProfile();
        stats.totalWeapons = 1;
        stats.attack_power = m_attackPower * m_attackFireCount;// 공격력 × 발사 개수
        return stats;
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
        m_attackPower = moduleData.attackPower;
        m_attackCoolTime = moduleData.attackCoolTime;
        m_attackFireCount = moduleData.attackFireCount;
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

    public void SetTarget(ModuleBody target)
    {
        m_currentTarget = target;
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


    public override void SetModuleStatRows(List<RowLabelValue> statRows)
    {
        EModuleSubType subType = GetModuleSubType();
        int currentLevel = GetModuleLevel();
        ModuleData moduleDataCurrent = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, currentLevel);
        if (moduleDataCurrent == null) return;

        statRows[1].SetRow("level", $"{currentLevel}");
        statRows[2].SetRow("attack_power", $"{moduleDataCurrent.attackPower:F0}");
    }


    // 파괴 시 정리
    private void OnDestroy()
    {
        if (m_parentBody != null)
            m_parentBody.RemoveBeam(this);
    }

}
