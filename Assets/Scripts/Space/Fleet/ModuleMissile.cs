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

    // 발사 시점에 쓰는 침묵시간 — 아키타입 패시브(사일런스 지속시간 +N%, 100이면 ×2)를 곱함
    public float GetFinalSilenceTime()
    {
        float passiveSilenceMultiplier = 1f + GetPassiveEffect(HullArchetypePassiveEffects.k_missileSilenceTimePercent) / 100f;
        return m_finalSilenceTime * passiveSilenceMultiplier;
    }

    // 발사 시점에 쓰는 폭발 범위 배율 — 아키타입 패시브(폭발 범위 +N%)
    public float GetBlastRadiusMultiplier()
    {
        return 1f + GetPassiveEffect(HullArchetypePassiveEffects.k_missileBlastRadiusPercent) / 100f;
    }

    // 발사 시점에 쓰는 미사일 속도 배율 — 아키타입 패시브(미사일 속도 +N%)
    public float GetSpeedMultiplier()
    {
        return 1f + GetPassiveEffect(HullArchetypePassiveEffects.k_missileSpeedPercent) / 100f;
    }

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
        // 아키타입 패시브(미사일 공격력 +N%)는 보상카드 배율과 곱해짐 — 패시브 효과가 바뀔 때도 같은 갱신 경로로 반영
        float passiveAttackMultiplier = 1f + GetPassiveEffect(HullArchetypePassiveEffects.k_missileAttackPercent) / 100f;
        m_attack = m_baseAttack * GetRewardCardBuffMultiplier(ECardEffectType.Buff_MissileAttack) * passiveAttackMultiplier;
        float delayReductionRatio = GetRewardCardBuffMultiplier(ECardEffectType.Buff_MissileFireRate) - 1f;
        float delayRatio = Mathf.Max(1f - delayReductionRatio, k_minDelayRatio);
        m_attackCoolTime = m_baseAttackCoolTime * delayRatio;
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

        // 아키타입 단위 영구 모듈강화(M6) — 이 함체의 아키타입+이 미사일 슬롯(slotIndex) 기준 레벨(최소 1)에 해당하는 티어 데이터(missile_{level}_1)를 그대로 사용
        // 존 적은 강화 개념이 없어 장착한 적 전용 무기 행을 그대로 사용(ResolveWeaponLevelData)
        int archetypeLevel = parentBody != null ? ArchetypeUpgradeLevelLookup.GetLevel(ResolveOwnerFleet(), parentBody.GetHullArchetype(), EModuleType.missile, m_moduleInfo.slotIndex) : 1;
        ModuleData levelModuleData = ResolveWeaponLevelData(moduleData, EModuleType.missile, archetypeLevel);
        float levelAttack = levelModuleData.attack;
        float levelAttackCool = levelModuleData.attackCool;
        float levelSilenceTime = levelModuleData.silenceTime;
        SetCritStats(levelModuleData);
        float baseAttack = attackOverride ?? levelAttack;
        float baseAttackCool = attackCoolOverride ?? levelAttackCool;
        float baseSilenceTime = silenceTimeOverride ?? levelSilenceTime;
        m_baseAttack = baseAttack;
        m_baseAttackCoolTime = baseAttackCool;
        m_finalSilenceTime = baseSilenceTime;
        if (parentBody != null)
            SetModuleLevel(archetypeLevel);

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
        // 재초기화(함선 편집 미리보기) 때 발사기가 중복 등록되면 한 번에 여러 발이 나가므로 최초 1회만 생성
        if (m_launchers.Count > 0) return;

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
        // 미사일 전술 토글이 꺼져 있으면 발사 자체를 하지 않음(쿨다운은 그대로 흘러감 — 토글을 다시 켜면 바로 쏠 수 있음)
        if (m_ownerFleet != null && m_ownerFleet.IsMissileTacticOn() == false) return;

        // 발사 1건당 자원 과금 — 여유가 없으면 이번 발사는 하지 않음(토글은 유지)
        if (m_ownerFleet != null)
        {
            int tacticMissileCost = DataManager.Instance.m_dataTableConfig.gameSettings.tactic.tacticMissileCost;
            if (m_ownerFleet.TryChargeMissileTacticCost(tacticMissileCost) == false) return;
        }

        float shipCountMultiplier = m_ownerFleet != null ? m_ownerFleet.GetShipCountAttackMultiplier() : 1f;
        float formationMultiplier = m_ownerFleet != null ? m_ownerFleet.GetFormationAttackMultiplier() : 1f;
        float berserkerMultiplier = m_ownerFleet != null ? m_ownerFleet.GetBerserkerAttackMultiplier() : 1f;

        // 치명타 — 발사 1회에 한 번 판정(폭발 범위 피해에도 같은 배율이 이어짐)
        float critMultiplier = RollCritMultiplier(HullArchetypePassiveEffects.k_missileCritChance);

        DamageInfo damageInfo = new DamageInfo
        {
            baseDamage       = m_attack,
            attackMultiplier = shipCountMultiplier * formationMultiplier * berserkerMultiplier * critMultiplier,
            damageType       = EDamageType.Missile,
        };

        float blastRadiusMultiplier = GetBlastRadiusMultiplier();
        PassiveAudit.RecordDetail(m_ownerShip, $"미사일{m_moduleInfo.slotIndex}발사", $"공격={m_attack} 함선수={shipCountMultiplier} 진형={formationMultiplier} 버서커={berserkerMultiplier} 치명타={critMultiplier}(기본확률 {m_critChancePercent}%) 범위배율={blastRadiusMultiplier} 속도배율={GetSpeedMultiplier()} 침묵={GetFinalSilenceTime()}");
        BattleStatTracker.MarkMissileFired(m_ownerShip);
        foreach (var launcher in m_launchers)
        {
            if (launcher != null)
                launcher.FireAtTarget(target.transform, damageInfo, this, default, blastRadiusMultiplier);
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
