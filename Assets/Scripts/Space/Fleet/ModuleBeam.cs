//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ModuleBeam : ModuleBase
{
    [SerializeField] private ModuleHull m_parentBody;
    public ModuleInfo m_moduleInfo;

    // 무기 전용 스탯
    [SerializeField] private float m_attackCool;

    [SerializeField] private float m_lastAttackTime;

    // 보상카드 지속버프 미반영 원본값 — RefreshRewardCardBuff()가 이 값에 현재 버프 배율을 다시 곱해 m_attack/m_attackCool을 갱신
    private float m_baseAttack;
    private float m_baseAttackCool;

    // 발사대 관련
    [SerializeField] private List<LauncherBase> m_launchers = new List<LauncherBase>();

    private ModuleHull m_currentTarget;
    private Coroutine m_autoAttackCoroutine;
    private Animator m_animator;
    private const float k_beamFireAngle = 5f;

    // Body 교체 시 기존 모듈 승계용 — 새 부모 body로 갱신
    public void SetParentBody(ModuleHull parentBody)
    {
        m_parentBody = parentBody;
    }

    // 보상카드 지속버프 배율이 바뀔 때마다(카드 선택/런 종료 초기화) 호출 — m_baseAttack/m_baseAttackCool을 기준으로 다시 계산
    public void RefreshRewardCardBuff()
    {
        m_attack = m_baseAttack * GetRewardCardBuffMultiplier(ECardEffectType.Buff_BeamAttack);
        m_attackCool = m_baseAttackCool / GetRewardCardBuffMultiplier(ECardEffectType.Buff_BeamFireRate);
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
    public void InitializeModuleBeam(ModuleInfo moduleInfo, ModuleHull parentBody, ModuleSlot moduleSlot, float? attackOverride = null)
    {
        m_moduleInfo = moduleInfo;
        m_parentBody = parentBody;
        m_moduleSlot = moduleSlot;

        // 서버 데이터로부터 완전한 모듈 데이터 복원
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType);
        if (moduleData == null)
        {
            Debug.LogError("Failed to restore module data for ModuleBeam");
            return;
        }

        // 복원된 데이터로 스탯 설정 — 발사수/쿨다운/체력은 테이블(티어) 기준, 공격력만 프리셋 계산값 있으면 그걸로 대체
        m_health = moduleData.health;
        m_healthMax = moduleData.health;
        m_baseAttack = attackOverride ?? moduleData.attack;
        m_baseAttackCool = moduleData.attackCool;

        m_lastAttackTime = 0f;

        // 함대 정보 자동 설정
        AutoDetectFleetInfo();

        // 보상카드 지속버프(내 함대만 배율 1 이상) 반영 — m_attack/m_attackCool을 여기서 처음 세팅
        RefreshRewardCardBuff();

        // Zone 적 함선일 때 체력·공격력에 배율 적용
        if (m_ownerFleet != null && m_ownerFleet.IsZoneEnemy == true)
        {
            m_health    *= m_ownerShip.m_healthMultiplier;
            m_healthMax *= m_ownerShip.m_healthMultiplier;
            m_attack    *= m_ownerShip.m_attackMultiplier;
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
        LauncherBeam launcher = gameObject.AddComponent<LauncherBeam>();
        launcher.InitializeLauncherBeam(moduleData, 0, m_ownerFleet != null && ObjectManager.Instance.IsEnemyOfMyTeam(m_ownerFleet), slotScale);
        m_launchers.Add(launcher);
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
    
    private void ExecuteAttackOnTarget(ModuleHull target)
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
        stats.beamAttack = m_attack;
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
        float remaining = (m_lastAttackTime + m_attackCool) - Time.time;
        return Mathf.Max(0f, remaining);
    }
    
    // 무기 스탯 Getter들
    public float GetAttackCoolTime() { return m_attackCool; }


    // 파괴 시 정리
    private void OnDestroy()
    {
        if (m_parentBody != null)
            m_parentBody.RemoveBeam(this);
    }

}
