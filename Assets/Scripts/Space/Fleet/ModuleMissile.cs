// 미사일 모듈 — 자동 공격, 발사대 관리, 커버 애니메이터 제어
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ModuleMissile : ModuleBase
{
    [SerializeField] private ModuleBody m_parentBody;
    public ModuleInfo m_moduleInfo;

    // 무기 전용 스탯
    [SerializeField] private int m_attackFireCount;
    [SerializeField] private float m_attackCoolTime;

    [SerializeField] private float m_lastAttackTime;

    // 발사대 관련
    [SerializeField] private List<LauncherBase> m_launchers = new List<LauncherBase>();

    // 커버 애니메이터
    //private Animator m_coverAnimator;
    private static readonly int HASH_IS_IN_COMBAT = Animator.StringToHash("IsInCombat");

    private ModuleBody m_currentTarget;
    private Coroutine m_autoAttackCoroutine;

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






    public void InitializeModuleMissile(ModuleInfo moduleInfo, ModuleBody parentBody, ModuleSlot moduleSlot)
    {
        m_moduleInfo = moduleInfo;
        m_parentBody = parentBody;
        m_moduleSlot = moduleSlot;
        SetInvestedModulePoint(moduleInfo.investedModulePoint);
        SetInvestedMineral(moduleInfo.investedMineral);

        // 서버 데이터로부터 완전한 모듈 데이터 복원
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, m_moduleInfo.moduleLevel);
        if (moduleData == null)
        {
            Debug.LogError("Failed to restore module data for ModuleMissile");
            return;
        }

        // 복원된 데이터로 스탯 설정
        m_health = moduleData.health;
        m_healthMax = moduleData.health;
        m_attack = moduleData.attack;
        m_attackFireCount = moduleData.attackFireCount;
        m_attackCoolTime = moduleData.attackCool;

        // 업그레이드 비용 설정
        m_modulePointCostLevelup = moduleData.modulePointCost;

        m_lastAttackTime = 0f;

        // 무기 서브 타입 초기화
        InitializeByModuleSlot(moduleData);

        // 함대 정보 자동 설정
        AutoDetectFleetInfo();

        // Zone 적 함선일 때 체력·공격력에 배율 적용
        if (m_ownerFleet != null && m_ownerFleet.IsZoneEnemy == true)
        {
            m_health    *= m_ownerShip.m_missileMultiplier;
            m_healthMax *= m_ownerShip.m_missileMultiplier;
            m_attack    *= m_ownerShip.m_missileMultiplier;
        }

        // 부모 바디에 이 무기 등록
        if (m_parentBody != null)
            m_parentBody.AddMissile(this);

        //m_coverAnimator = GetComponentInChildren<Animator>(true);
    }

    private void InitializeByModuleSlot(ModuleData moduleData)
    {
        EPoolName poolName = GetMissilePoolName(m_moduleInfo.moduleSubType);
        float ejectSpeed = m_moduleSlot != null ? m_moduleSlot.m_missileEjectSpeed * m_moduleSlot.transform.lossyScale.x : 1f;
        Vector3 slotScale = m_moduleSlot != null ? m_moduleSlot.transform.lossyScale : Vector3.one;
        for (int i = 0; i < moduleData.attackFireCount; i++)
        {
            LauncherMissile launcher = gameObject.AddComponent<LauncherMissile>();
            launcher.InitializeLauncherMissile(moduleData, i, poolName, ejectSpeed, slotScale);
            m_launchers.Add(launcher);
        }
    }

    // 서브타입별 미사일 프리팹 풀 결정
    private static EPoolName GetMissilePoolName(EModuleSubType subType)
    {
        switch (subType)
        {
            case EModuleSubType.missile_t1_m1:  return EPoolName.PROJECTILE_MISSILE_MEDIUM;
            case EModuleSubType.missile_t2_m1:  return EPoolName.PROJECTILE_MISSILE_MEDIUM;
            default:                                  return EPoolName.PROJECTILE_MISSILE_MEDIUM;
        }
    }


    public override void ApplyShipStateToModule()
    {
        base.ApplyShipStateToModule();
        // if (m_coverAnimator != null)
        //     m_coverAnimator.SetBool(HASH_IS_IN_COMBAT, m_moduleState == EUnitState.Battle);
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

            float missileHarassDelay = m_ownerShip != null ? m_ownerShip.GetHarassAdditionalCool() : 0f;
            if (IsSilenced() == false && Time.time >= m_lastAttackTime + m_attackCoolTime + missileHarassDelay)
            {
                // 적 미사일 우선 요격
                bool isFriendly = m_ownerFleet != null && m_ownerFleet.IsEnemy == false;
                ProjectileMissile interceptTarget = ObjectManager.Instance.GetNearestEnemyMissile(transform.position, isFriendly);
                if (interceptTarget != null)
                {
                    ExecuteInterceptOnMissile(interceptTarget);
                    m_lastAttackTime = Time.time;
                }
                else if (m_currentTarget != null && m_currentTarget.m_health > 0)
                {
                    ExecuteAttackOnTarget(m_currentTarget);
                    m_lastAttackTime = Time.time;
                }
            }

            yield return null;
        }
    }

    private void ExecuteInterceptOnMissile(ProjectileMissile target)
    {
        DamageInfo damageInfo = new DamageInfo
        {
            baseDamage       = m_attack,
            attackMultiplier = 1f,
        };

        foreach (var launcher in m_launchers)
        {
            if (launcher != null)
                launcher.FireAtTarget(target.transform, damageInfo, this);
        }
    }

    private void ExecuteAttackOnTarget(ModuleBody target)
    {
        bool missileTacticOn = m_ownerFleet != null && m_ownerFleet.m_fleetInfo != null && (m_ownerFleet.m_fleetInfo.tacticOptions & 2) != 0;
        GameSettings settings = DataManager.Instance.m_dataTableConfig.gameSettings;
        float tacticMultiplier    = missileTacticOn == true ? settings.missileTacticDamageMultiplier : 1f;
        float shipCountMultiplier = m_ownerFleet != null ? m_ownerFleet.GetShipCountAttackMultiplier() : 1f;
        float formationMultiplier = m_ownerFleet != null ? m_ownerFleet.GetFormationAttackMultiplier() : 1f;
        DamageInfo damageInfo = new DamageInfo
        {
            baseDamage       = m_attack,
            attackMultiplier = tacticMultiplier * shipCountMultiplier * formationMultiplier,
            damageType       = EDamageType.Missile,
        };

        float explosionMultiplier = missileTacticOn == true ? settings.missileTacticExplosionMultiplier : 1f;

        foreach (var launcher in m_launchers)
        {
            if (launcher != null)
                launcher.FireAtTarget(target.transform, damageInfo, this, explosionMultiplier: explosionMultiplier);
        }
    }

    public override CapabilityProfile GetModuleCapabilityProfile(bool bByInfo)
    {
        if (bByInfo == true) return CommonUtility.GetModuleCapabilityProfile(m_moduleInfo);

        CapabilityProfile stats = new CapabilityProfile();
        stats.totalWeapons = 1;
        stats.attack = m_attack * m_attackFireCount; // 공격력 × 발사 개수
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
        m_healthMax = moduleData.health;
        m_health = Mathf.Min(m_health, m_healthMax);
        m_attack = moduleData.attack;
        m_attackCoolTime = moduleData.attackCool;
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
        float remaining = (m_lastAttackTime + m_attackCoolTime) - Time.time;
        return Mathf.Max(0f, remaining);
    }
    
    // 무기 스탯 Getter들
    public int GetAttackFireCount() { return m_attackFireCount; }
    public float GetAttackCoolTime() { return m_attackCoolTime; }

    // 파괴 시 정리
    private void OnDestroy()
    {
        if (m_parentBody != null)
            m_parentBody.RemoveMissile(this);
    }

}
