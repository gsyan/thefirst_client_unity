// 미사일 모듈 — 자동 공격, 발사대 관리, 커버 애니메이터 제어
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ModuleMissile : ModuleBase
{
    [SerializeField] private ModuleHull m_parentBody;
    public ModuleInfo m_moduleInfo;

    // 무기 전용 스탯
    [SerializeField] private float m_attackCoolTime;

    [SerializeField] private float m_lastAttackTime;

    // 보상카드 지속버프 미반영 원본값 — RefreshRewardCardBuff()가 이 값에 현재 버프 배율을 다시 곱해 m_attack/m_attackCoolTime을 갱신
    private float m_baseAttack;
    private float m_baseAttackCoolTime;

    // 강화 포인트 반영을 마친 침묵시간(보상카드 버프 미반영) — ProjectileMissile이 발사 시 기본값으로 사용
    private float m_finalSilenceTime;
    public float GetFinalSilenceTime() { return m_finalSilenceTime; }

    // 발사대 관련
    [SerializeField] private List<LauncherBase> m_launchers = new List<LauncherBase>();

    // 커버 애니메이터
    //private Animator m_coverAnimator;
    private static readonly int HASH_IS_IN_COMBAT = Animator.StringToHash("IsInCombat");

    private ModuleHull m_currentTarget;
    private Coroutine m_autoAttackCoroutine;

    // Body 교체 시 기존 모듈 승계용 — 새 부모 body로 갱신
    public void SetParentBody(ModuleHull parentBody)
    {
        m_parentBody = parentBody;
    }

    // 보상카드 지속버프 배율이 바뀔 때마다(카드 선택/런 종료 초기화) 호출 — m_baseAttack/m_baseAttackCoolTime을 기준으로 다시 계산
    public void RefreshRewardCardBuff()
    {
        m_attack = m_baseAttack * GetRewardCardBuffMultiplier(ECardEffectType.Buff_MissileAttack);
        m_attackCoolTime = m_baseAttackCoolTime / GetRewardCardBuffMultiplier(ECardEffectType.Buff_MissileFireRate);
    }

    public override EModuleType GetModuleType()
    {
        return m_moduleInfo.moduleType;
    }
    public override string GetModuleSubType()
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






    // attackOverride: 성능포인트 프리셋 기반 스폰 시 테이블 공격력 대신 사용할 계산값 (null이면 기존처럼 테이블값 그대로 사용)
    public void InitializeModuleMissile(ModuleInfo moduleInfo, ModuleHull parentBody, ModuleSlot moduleSlot, float? attackOverride = null, float? attackCoolOverride = null, float? silenceTimeOverride = null)
    {
        m_moduleInfo = moduleInfo;
        m_parentBody = parentBody;
        m_moduleSlot = moduleSlot;

        // 서버 데이터로부터 완전한 모듈 데이터 복원
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType);
        if (moduleData == null)
        {
            Debug.LogError("Failed to restore module data for ModuleMissile");
            return;
        }

        // 복원된 데이터로 스탯 설정 — 체력은 테이블(티어) 기준, 공격력/쿨다운은 프리셋 계산값 있으면 그걸로 대체
        m_health = moduleData.health;
        m_healthMax = moduleData.health;
        m_baseAttack = attackOverride ?? moduleData.attack;
        m_baseAttackCoolTime = attackCoolOverride ?? moduleData.attackCool;
        m_finalSilenceTime = silenceTimeOverride ?? moduleData.silenceTime;

        m_lastAttackTime = 0f;

        // 무기 서브 타입 초기화
        InitializeByModuleSlot(moduleData);

        // 함대 정보 자동 설정
        AutoDetectFleetInfo();

        // 보상카드 지속버프(내 함대만 배율 1 이상) 반영 — m_attack/m_attackCoolTime을 여기서 처음 세팅
        RefreshRewardCardBuff();

        // Zone 적 함선일 때 체력·공격력에 배율 적용
        if (m_ownerFleet != null && m_ownerFleet.IsZoneEnemy == true)
        {
            m_health    *= m_ownerShip.m_healthMultiplier;
            m_healthMax *= m_ownerShip.m_healthMultiplier;
            m_attack    *= m_ownerShip.m_attackMultiplier;
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
        LauncherMissile launcher = gameObject.AddComponent<LauncherMissile>();
        launcher.InitializeLauncherMissile(moduleData, 0, poolName, ejectSpeed, slotScale);
        m_launchers.Add(launcher);
    }

    // 서브타입별 미사일 프리팹 풀 결정 — 현재는 외형 타입이 t1 하나뿐이라 고정값
    private static EPoolName GetMissilePoolName(string subType)
    {
        return EPoolName.PROJECTILE_MISSILE_MEDIUM;
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

            if (IsSilenced() == false && m_currentTarget != null && m_currentTarget.m_health > 0)
            {
                float missileHarassDelay = m_ownerShip != null ? m_ownerShip.GetHarassAdditionalCool() : 0f;

                if (m_isAttackSignalFired == false)
                {
                    if (Time.time >= m_lastAttackTime + m_attackCoolTime + missileHarassDelay)
                    {
                        ArmAttackSignal();
                        // bool isMyFleet = m_ownerFleet != null && ObjectManager.Instance.IsEnemyOfMyTeam(m_ownerFleet) == false;
                        // if (isMyFleet == true)
                        //     Debug.Log($"[BeamJitter] t={Time.time:F4} slot={m_moduleInfo.slotIndex} offset={m_attackPhaseOffset:F4}");
                    }
                }
                else if (Time.time >= m_attackSignalArmedTime + m_attackPhaseOffset)
                {
                    // bool isMyFleet = m_ownerFleet != null && ObjectManager.Instance.IsEnemyOfMyTeam(m_ownerFleet) == false;
                    // if (isMyFleet == true)
                    //     Debug.Log($"[BeamJitter] t={Time.time:F4} slot={m_moduleInfo.slotIndex}");

                    ExecuteAttackOnTarget(m_currentTarget);
                    m_lastAttackTime = m_attackSignalArmedTime;
                    m_attackPhaseOffset = 0f;
                    m_isAttackSignalFired = false;
                }
            }

            yield return null;
        }
    }

    private void ExecuteAttackOnTarget(ModuleHull target)
    {
        float shipCountMultiplier = m_ownerFleet != null ? m_ownerFleet.GetShipCountAttackMultiplier() : 1f;
        float formationMultiplier = m_ownerFleet != null ? m_ownerFleet.GetFormationAttackMultiplier() : 1f;
        float tacticMultiplier    = m_ownerFleet != null ? m_ownerFleet.GetMissileTacticAttackMultiplier() : 1f;

        // 전술 보너스가 실제로 적용 중일 때만 발사 1건당 과금 — 여유가 없으면 이번 발사부터 보너스 없이(토글은 TryChargeMissileTacticCost가 이미 꺼둠) 그대로 발사
        if (tacticMultiplier > 1f)
        {
            int tacticMissileCost = DataManager.Instance.m_dataTableConfig.gameSettings.tactic.tacticMissileCost;
            if (m_ownerFleet.TryChargeMissileTacticCost(tacticMissileCost) == false)
                tacticMultiplier = 1f;
        }

        DamageInfo damageInfo = new DamageInfo
        {
            baseDamage       = m_attack,
            attackMultiplier = shipCountMultiplier * formationMultiplier * tacticMultiplier,
            damageType       = EDamageType.Missile,
        };

        foreach (var launcher in m_launchers)
        {
            if (launcher != null)
                launcher.FireAtTarget(target.transform, damageInfo, this);
        }
    }

    public override CapabilityProfile GetModuleCapabilityProfile(bool bByInfo)
    {
        if (bByInfo == true) return CommonUtility.GetModuleCapabilityProfile(m_moduleInfo);

        CapabilityProfile stats = new CapabilityProfile();
        stats.totalWeapons = 1;
        stats.missileAttack = m_attack;
        return stats;
    }

    

    public override int GetModuleHullIndex()
    {
        return m_moduleInfo.hullIndex;
    }
    public override void SetModuleHullIndex(int hullIndex)
    {
        m_moduleInfo.hullIndex = hullIndex;
    }

    public void SetTarget(ModuleHull target)
    {
        m_currentTarget = target;
    }

    // 다음 공격까지 남은 시간
    public float GetRemainingCoolTime()
    {
        float threshold = m_lastAttackTime + m_attackCoolTime;
        if (m_isAttackSignalFired == true)
            threshold = m_attackSignalArmedTime + m_attackPhaseOffset;
        return Mathf.Max(0f, threshold - Time.time);
    }
    
    // 무기 스탯 Getter들
    public override float GetAttackCoolTime() { return m_attackCoolTime; }

    // 파괴 시 정리
    private void OnDestroy()
    {
        if (m_parentBody != null)
            m_parentBody.RemoveMissile(this);
    }

}
