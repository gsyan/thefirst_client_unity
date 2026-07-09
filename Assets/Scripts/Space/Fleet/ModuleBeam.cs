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
    [SerializeField] private float m_attackCool;

    [SerializeField] private float m_lastAttackTime;

    // 발사대 관련
    [SerializeField] private List<LauncherBase> m_launchers = new List<LauncherBase>();

    private ModuleBody m_currentTarget;
    private Coroutine m_autoAttackCoroutine;
    private Animator m_animator;
    private const float k_beamFireAngle = 5f;

    // Body 교체 시 기존 모듈 승계용 — 새 부모 body로 갱신
    public void SetParentBody(ModuleBody parentBody)
    {
        m_parentBody = parentBody;
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






    public void InitializeModuleBeam(ModuleInfo moduleInfo, ModuleBody parentBody, ModuleSlot moduleSlot)
    {
        m_moduleInfo = moduleInfo;
        m_parentBody = parentBody;
        m_moduleSlot = moduleSlot;
        SetInvestedModulePoint(moduleInfo.investedModulePoint);
        SetAddShipModulePoint(moduleInfo.addShipModulePoint);
        SetInvestedMineral(moduleInfo.investedMineral);

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
        m_attack = moduleData.attack;
        m_attackFireCount = moduleData.attackFireCount;
        m_attackCool = moduleData.attackCool;

        // 업그레이드 비용 설정
        m_modulePointCostLevelup = moduleData.modulePointCost;

        m_lastAttackTime = 0f;

        // 함대 정보 자동 설정
        AutoDetectFleetInfo();

        // Zone 적 함선일 때 체력·공격력에 배율 적용
        if (m_ownerFleet != null && m_ownerFleet.IsZoneEnemy == true)
        {
            m_health    *= m_ownerShip.m_beamMultiplier;
            m_healthMax *= m_ownerShip.m_beamMultiplier;
            m_attack    *= m_ownerShip.m_beamMultiplier;
        }

        // 무기 서브 타입 초기화
        InitializeByModuleSlot(moduleData);

        // 부모 바디에 이 무기 등록
        if (m_parentBody != null)
            m_parentBody.AddBeam(this);

        m_animator = GetComponentInChildren<Animator>();
    }

    private void InitializeByModuleSlot(ModuleData moduleData)
    {
        Vector3 slotScale = m_moduleSlot != null ? m_moduleSlot.transform.lossyScale : Vector3.one;
        slotScale *= 0.1f;
        for(int i=0; i< moduleData.attackFireCount; i++)
        {
            LauncherBeam launcher = gameObject.AddComponent<LauncherBeam>();
            launcher.InitializeLauncherBeam(moduleData, i, m_ownerFleet != null && ObjectManager.Instance.IsEnemyOfMyTeam(m_ownerFleet), slotScale);
            m_launchers.Add(launcher);
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

    public override float GetLastAttackTime() { return m_lastAttackTime; }
    public override void SetLastAttackTime(float t) { m_lastAttackTime = t; }

    private IEnumerator AutoAttack()
    {
        while (true)
        {
            if (m_moduleState.IsBattleState() == false) { yield return null; continue; }

            if (IsSilenced() == false && m_currentTarget != null && m_currentTarget.m_health > 0)
            {
                float harassDelay = m_ownerShip != null ? m_ownerShip.GetHarassAdditionalCool() : 0f;
                if (Time.time >= m_lastAttackTime + m_attackCool + harassDelay)
                {
                    bool isFacing = true;
                    if (m_ownerShip != null)
                        isFacing = m_ownerShip.IsFacingTarget(m_currentTarget.transform.position, k_beamFireAngle);
                    if (isFacing == true)
                    {
                        ExecuteAttackOnTarget(m_currentTarget);
                        m_lastAttackTime = Time.time;
                    }
                }
            }

            yield return null;
        }
    }
    
    private void ExecuteAttackOnTarget(ModuleBody target)
    {
        if (m_animator != null)
            m_animator.SetTrigger("Fire");

        float shipCountMultiplier = m_ownerFleet != null ? m_ownerFleet.GetShipCountAttackMultiplier() : 1f;
        float formationMultiplier = m_ownerFleet != null ? m_ownerFleet.GetFormationAttackMultiplier() : 1f;
        DamageInfo damageInfo = new DamageInfo
        {
            baseDamage       = m_attack,
            attackMultiplier = shipCountMultiplier * formationMultiplier,
            damageType       = EDamageType.Beam,
        };

        Vector3 hitPoint = target.GetRandomHitPoint();

        foreach (var launcher in m_launchers)
        {
            if (launcher == null) continue;
            launcher.FireAtTarget(target.transform, damageInfo, this, hitPoint);
        }
    }

    public override CapabilityProfile GetModuleCapabilityProfile(bool bByInfo)
    {
        if (bByInfo == true) return CommonUtility.GetModuleCapabilityProfile(m_moduleInfo);

        CapabilityProfile stats = new CapabilityProfile();
        stats.totalWeapons = 1;
        stats.attack = m_attack * m_attackFireCount;// 공격력 × 발사 개수
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
        m_attack = moduleData.attack;
        m_attackCool = moduleData.attackCool;
        m_attackFireCount = moduleData.attackFireCount;
        m_modulePointCostLevelup = moduleData.modulePointCost;
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
        float remaining = (m_lastAttackTime + m_attackCool) - Time.time;
        return Mathf.Max(0f, remaining);
    }
    
    // 무기 스탯 Getter들
    public int GetAttackFireCount() { return m_attackFireCount; }
    public float GetAttackCoolTime() { return m_attackCool; }


    // 파괴 시 정리
    private void OnDestroy()
    {
        if (m_parentBody != null)
            m_parentBody.RemoveBeam(this);
    }

}
