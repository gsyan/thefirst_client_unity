//------------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
//using System.Numerics;
using UnityEngine;
using UnityEngine.Rendering.Universal;


// 능력치 프로파일 구조체 (함선/함대의 전투 및 작전 능력)
[System.Serializable]
public struct CapabilityProfile
{
    // 기존 능력치 (하위 호환성 유지 - deprecated)
    public int totalWeapons;
    
    // 세부 전투 능력치
    public float health;                // 체력
    public float speed;                 // 속력 (이동+회전 통합)
    public float repair;                // 수리 능력
    public float beamAttack;            // 빔 공격력 합계
    public float missileAttack;         // 미사일 공격력 합계
    public float airAttack;              // 함재기 공격력
    public int airCount;                // 함재기 수
}

// 자동 타겟팅 후보 필터링 룰 — 조합 가능(bitmask), 신규 룰은 값 추가 + SpaceShip.ApplyTargetingRules()에 처리 분기만 추가하면 됨
[Flags]
public enum ETargetingRule
{
    None = 0,
    FlagshipLast = 1 << 0, // 기함 아닌 함선이 하나라도 남아있으면 기함을 후보에서 제외, 기함만 남으면 타격 허용
}

public class SpaceShip : MonoBehaviour
{
    [SerializeField] public ShipInfo m_shipInfo;
    [SerializeField] public List<ModuleHull> m_moduleHulls = new List<ModuleHull>();
    [SerializeField] public SpaceShip m_targetShip;
    [SerializeField] public CapabilityProfile m_spaceShipStatsOrg;
    [SerializeField] public CapabilityProfile m_spaceShipStatsCur;

    public SpaceFleet m_ownerFleet;
    public EUnitState m_shipState;
    [HideInInspector] public Outline m_shipOutline;

    // 자동 타겟팅 후보 필터링 룰 (기본값 None = 기존 동작과 동일). 시네마틱 등 특수 전투용 함선에만 설정
    public ETargetingRule m_targetingRule = ETargetingRule.None;

    // 전투 피격으로 실제 파괴되지 않도록 막는 체력 비율 하한선 (기본값 0 = 하한 없음, 기존 동작과 동일). 튜토리얼 지크프리트 기함처럼 스크립트로만 파괴되어야 하는 함선에 설정
    public float m_minHealthRatio = 0f;

    // Zone 적 전용 — 함선별 스탯 배율 (InitializeSpaceShip 전에 세팅, 기본값 1.0)
    public float m_healthMultiplier = 1.0f;
    public float m_attackMultiplier = 1.0f;

    private GaugeBars m_gaugeBars;
    public ShieldGrid m_shieldGrid;

    // 체력 단계별 화재 이펙트 (50% / 30% / 10% 이하 피격 시 각각 생성)
    private EffectBase m_fireEffect50 = null;
    private EffectBase m_fireEffect30 = null;
    private EffectBase m_fireEffect10 = null;

    // 피격 스코치 마크 (함선당 최대 개수 제한)
    private const int k_maxScorchMarks = 6;
    private const float k_scorchHoldTime = 1.0f;
    private const float k_scorchFadeTime = 0.5f;
    private readonly List<EffectBase> m_scorchMarks = new List<EffectBase>();
    private readonly List<Coroutine> m_scorchCoroutines = new List<Coroutine>();
    private readonly List<DecalProjector> m_scorchDecals = new List<DecalProjector>();

    virtual protected void Start()
    {
        InitializeGaugeDisplay();
        EventManager.Subscribe_ModuleHullDestroyed(OnModuleHullDestroyed);
        EventManager.Subscribe_SpaceShipSelected(OnGlobalShipSelected);
        EventManager.Subscribe_EmptySpaceTapped(OnGlobalEmptySpaceTapped);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_ModuleHullDestroyed(OnModuleHullDestroyed);
        EventManager.Unsubscribe_SpaceShipSelected(OnGlobalShipSelected);
        EventManager.Unsubscribe_EmptySpaceTapped(OnGlobalEmptySpaceTapped);
    }

    // 함선 단위 선택 하이라이트 — 모듈 단위 선택(SetSelectedModule)과 별개로, 함선 전체 윤곽선(Outline)을 켜고 끔
    public void SetShipSelected(bool selected)
    {
        if (m_shipOutline != null)
            m_shipOutline.enabled = selected;
    }

    private void OnGlobalShipSelected(SpaceShip ship)
    {
        SetShipSelected(this == ship);
    }

    private void OnGlobalEmptySpaceTapped()
    {
        SetShipSelected(false);
    }

    private void OnModuleHullDestroyed(ModuleHull destroyedBody)
    {
        if (m_currentTargetBody != destroyedBody) return;
        StartFindingTargets();
    }

    private void InitializeGaugeDisplay()
    {
        m_gaugeBars = GetComponent<GaugeBars>();
        if (m_gaugeBars == null)
            m_gaugeBars = gameObject.AddComponent<GaugeBars>();
    }

    // statOverride: 성능포인트 프리셋 기반 스폰 시 테이블 값 대신 사용할 계산값. null이면 기존처럼 테이블값 그대로 사용
    public void InitializeSpaceShip(SpaceFleet fleet, ShipInfo shipInfo, ShipFinalStats? statOverride = null)
    {
        m_ownerFleet = fleet;
        m_shipInfo = shipInfo;
        if (shipInfo.hulls == null || shipInfo.hulls.Count == 0) return;
        foreach (ModuleHullInfo bodyInfo in shipInfo.hulls)
            InitSpaceShipBody(bodyInfo, null, statOverride);

        m_spaceShipStatsOrg = GetShipCapabilityProfile(true);
        m_spaceShipStatsCur = GetShipCapabilityProfile(false);

        SetupSelectedModuleVisualing();
        
        // ShieldGrid, 지금은 바디가 오직 하나...
        m_shieldGrid = m_moduleHulls[0].GetComponent<ShieldGrid>();
        if (m_shieldGrid != null)
            m_shieldGrid.InitFormationRelay(this);
        
        // Outline 미리 설정
        m_shipOutline = gameObject.AddComponent<Outline>();//
        m_shipOutline.OutlineMode = Outline.Mode.OutlineAll;
        m_shipOutline.OutlineColor = Color.cyan;
        m_shipOutline.OutlineWidth = 5f;
        m_shipOutline.enabled = false; // 기본은 꺼둠

    }

   // Body 초기화 (기존 모듈 재사용 가능)
    private ModuleHull InitSpaceShipBody(ModuleHullInfo bodyInfo, List<ModuleBase> savedModules, ShipFinalStats? statOverride = null)
    {
        GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(bodyInfo.moduleType.ToString(), bodyInfo.moduleSubType);
        if (modulePrefab == null)
        {
            Debug.LogError("No prefab");
            return null;
        }

        GameObject bodyObj = Instantiate(modulePrefab, transform.position, transform.rotation);
        bodyObj.transform.SetParent(transform);

        ModuleHull moduleHull = bodyObj.GetComponent<ModuleHull>();
        if (moduleHull == null)
            moduleHull = bodyObj.AddComponent<ModuleHull>();

        moduleHull.InitializeModuleHull(bodyInfo, savedModules, statOverride);
        m_moduleHulls.Add(moduleHull);
        moduleHull.ApplyShipStateToModule(); // 모듈 변경시를 위해 필요
        return moduleHull;
    }

    private ModuleHull m_currentTargetBody;
    private Coroutine m_rotationCoroutine;
    private Coroutine m_returnRotationCoroutine;
    private const float k_angularSpeedMult = 2f;
    private List<ModuleHull> m_candidateBodies = new List<ModuleHull>();

    public void ApplyFleetStateToShip()
    {

        switch (m_ownerFleet.m_fleetState)
        {
            case EUnitState.Idle:
                m_shipState = EUnitState.Idle;
                StopAutoCombat();
                break;
            case EUnitState.Move:
                m_shipState = EUnitState.Move;
                StopAutoCombat();
                break;
            case EUnitState.Warp:
                m_shipState = EUnitState.Warp;
                StopAutoCombat();
                break;
            case EUnitState.BattleExploration:
            case EUnitState.BattlePvp:
                m_shipState = m_ownerFleet.m_fleetState;
                if (m_returnRotationCoroutine != null)
                {
                    StopCoroutine(m_returnRotationCoroutine);
                    m_returnRotationCoroutine = null;
                }
                // FindTargetModuleHull는 SpaceFleet.StartCombat()에서 양쪽 모두 배틀 상태 후 시작
                if (m_rotationCoroutine == null)
                    m_rotationCoroutine = StartCoroutine(RotateTowardTarget());
                break;
            default:
                m_shipState = EUnitState.Idle;
                StopAutoCombat();
                break;
        }
        
        foreach (ModuleHull body in m_moduleHulls)
            body.ApplyShipStateToModule();
    }

    // 보상카드 지속버프가 바뀔 때(카드 선택/런 종료 초기화) 호출 — 이미 스폰된 이 함선의 모든 바디/무기 모듈에 최신 배율을 다시 반영
    public void RefreshRewardCardBuffs()
    {
        foreach (ModuleHull body in m_moduleHulls)
            if (body != null) body.RefreshRewardCardBuff();
    }

    public void StartFindingTargets()
    {
        FindTargetModuleHull();
    }

    public void StopAutoCombat()
    {
        if (m_rotationCoroutine != null)
        {
            StopCoroutine(m_rotationCoroutine);
            m_rotationCoroutine = null;
        }
        // 전투 종료 후 함대 전방으로 복귀
        if (m_returnRotationCoroutine != null)
            StopCoroutine(m_returnRotationCoroutine);
        m_returnRotationCoroutine = StartCoroutine(ReturnToFleetForward());
    }

    // 1단계: 최소회전으로 교전 상대 함대(팀) 탐색 → 2단계: 그 함대 안에서 최소회전 바디 탐색
    private void FindTargetModuleHull()
    {
        SpaceFleet targetFleet = FindMinAngleFleet(GetOpposingFleets());
        if (targetFleet == null) return;

        m_candidateBodies.Clear();
        ApplyTargetingRules(targetFleet, m_candidateBodies);
        ModuleHull best = FindMinAngleBody(m_candidateBodies);
        if (best == null) return;

        m_currentTargetBody = best;
        m_targetShip = m_currentTargetBody.GetShip();

        foreach (ModuleHull body in m_moduleHulls)
        {
            if (body != null && body.m_health > 0)
                body.SetTarget(m_currentTargetBody);
        }
    }

    // targetFleet에서 후보 바디를 수집한 뒤, m_targetingRule에 설정된 룰들을 순서대로 적용해 걸러냄. 신규 룰 추가 시 여기에 분기만 추가
    private void ApplyTargetingRules(SpaceFleet targetFleet, List<ModuleHull> result)
    {
        CollectBodiesFromFleet(targetFleet, result);

        if ((m_targetingRule & ETargetingRule.FlagshipLast) != 0)
            ApplyFlagshipLastRule(targetFleet, result);
    }

    // 기함 아닌 함선이 후보에 하나라도 있으면 기함 바디를 후보에서 제외 (기함만 남았을 때만 타격 허용)
    // targetFleet.GetFlagship()은 원래 기함이 죽으면 살아있는 다음 함선으로 자동 승계되므로 그 결과를 그대로 사용
    private void ApplyFlagshipLastRule(SpaceFleet targetFleet, List<ModuleHull> candidates)
    {
        SpaceShip currentFlagship = targetFleet != null ? targetFleet.GetFlagship() : null;
        if (currentFlagship == null) return;

        bool bFind = false;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].GetShip() != currentFlagship)
            {
                bFind = true;
                break;
            }
        }
        // 기함 아닌 함선이 하나도 없으면(=기함만 남음) candidates를 건드리지 않고 그대로 반환 → 기함이 후보에 남아 타격 대상이 됨
        if (bFind == false) return;

        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            if (candidates[i].GetShip() == currentFlagship)
                candidates.RemoveAt(i);
        }
    }

    private static readonly List<SpaceFleet> s_emptyOpposingFleets = new List<SpaceFleet>();

    private List<SpaceFleet> GetOpposingFleets()
    {
        if (m_ownerFleet == null) return s_emptyOpposingFleets;
        return ObjectManager.Instance.GetOpposingTeamFleets(m_ownerFleet.m_team);
    }

    private SpaceFleet FindMinAngleFleet(List<SpaceFleet> candidates)
    {
        SpaceFleet best = null;
        float bestAngle = float.MaxValue;
        Vector3 forward = transform.forward;
        Vector3 myPos = transform.position;

        foreach (SpaceFleet fleet in candidates)
        {
            if (fleet == null || fleet.IsValidCombatTarget() == false) continue;

            Vector3 toFleet = (fleet.transform.position - myPos).normalized;
            float angle = Vector3.Angle(forward, toFleet);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = fleet;
            }
        }
        return best;
    }

    private void CollectBodiesFromFleet(SpaceFleet opposingFleet, List<ModuleHull> result)
    {
        if (opposingFleet == null || opposingFleet.IsValidCombatTarget() == false) return;
        foreach (SpaceShip ship in opposingFleet.m_ships)
        {
            if (ship == null || ship.IsAlive() == false || ship.IsWarping == true) continue;
            foreach (ModuleHull body in ship.m_moduleHulls)
            {
                if (body != null && body.m_health > 0)
                    result.Add(body);
            }
        }
    }

    private ModuleHull FindMinAngleBody(List<ModuleHull> candidates)
    {
        ModuleHull best = null;
        float bestAngle = float.MaxValue;
        Vector3 forward = transform.forward;
        Vector3 myPos = transform.position;

        foreach (ModuleHull body in candidates)
        {
            Vector3 toBody = (body.transform.position - myPos).normalized;
            float angle = Vector3.Angle(forward, toBody);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = body;
            }
        }
        return best;
    }

    private IEnumerator RotateTowardTarget()
    {
        while (true)
        {
            if (m_currentTargetBody != null && m_currentTargetBody.m_health > 0)
            {
                Vector3 toTarget = m_currentTargetBody.transform.position - transform.position;
                if (toTarget.sqrMagnitude > 0.001f)
                {
                    float angularSpeed = k_angularSpeedMult;
                    bool isFlagship = IsFlagship();
                    if (isFlagship == true && m_ownerFleet != null)
                    {
                        // 기함: 함대 transform을 서서히 적 방향으로 회전
                        Vector3 dir = toTarget;
                        dir.y = 0f;
                        Quaternion targetFleetRot = Quaternion.LookRotation(dir.normalized);
                        m_ownerFleet.transform.rotation = Quaternion.RotateTowards(m_ownerFleet.transform.rotation, targetFleetRot, angularSpeed * Time.deltaTime);
                    }
                    else
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, angularSpeed * Time.deltaTime);
                    }
                }
            }
            yield return null;
        }
    }

    private IEnumerator ReturnToFleetForward()
    {
        Vector3 fleetForward = m_ownerFleet != null ? m_ownerFleet.transform.forward : Vector3.forward;
        Quaternion targetRotation = Quaternion.LookRotation(fleetForward);
        while (true)
        {
            float angularSpeed = m_spaceShipStatsCur.speed * k_angularSpeedMult;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, angularSpeed * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, targetRotation) < 0.5f)
            {
                transform.rotation = targetRotation;
                m_returnRotationCoroutine = null;
                yield break;
            }
            yield return null;
        }
    }

    public bool IsFacingTarget(Vector3 targetPos, float angleThreshold)
    {
        Vector3 toTarget = (targetPos - transform.position).normalized;
        return Vector3.Angle(transform.forward, toTarget) <= angleThreshold;
    }

    // 함재기 교란 — AttackShip 페이즈 유지 중인 함재기 목록과 합산 딜레이
    private List<AircraftBase> m_harassingAircrafts = new List<AircraftBase>();
    public float m_totalHarassDelay = 0f;

    public void RegisterHarassingAircraft(AircraftBase aircraft, float delay)
    {
        if (m_harassingAircrafts.Contains(aircraft) == false)
        {
            m_harassingAircrafts.Add(aircraft);
            m_totalHarassDelay += delay;
        }
    }

    public void UnregisterHarassingAircraft(AircraftBase aircraft, float delay)
    {
        if (m_harassingAircrafts.Remove(aircraft) == true)
            m_totalHarassDelay = Mathf.Max(0f, m_totalHarassDelay - delay);
    }

    // 현재 교란 중인 함재기들의 합산 추가 쿨타임 (초)
    public float GetHarassAdditionalCool()
    {
        return m_totalHarassDelay;
    }

    public void ApplySilenceToRandomModule(float duration)
    {
        if (duration <= 0f) return;

        var free    = new System.Collections.Generic.List<ModuleBase>();
        var silenced = new System.Collections.Generic.List<ModuleBase>();

        foreach (ModuleHull body in m_moduleHulls)
        {
            if (body == null || body.m_health <= 0) continue;
            foreach (ModuleBeam    m in body.m_beams)
            {
                if (m == null) continue;
                if (m.IsSilenced() == true)
                    silenced.Add(m);
                else
                    free.Add(m);
            }
            foreach (ModuleMissile m in body.m_missiles)
            {
                if (m == null) continue;
                if (m.IsSilenced() == true)
                    silenced.Add(m);
                else
                    free.Add(m);
            }
            foreach (ModuleHangar m in body.m_hangars)
            {
                if (m == null) continue;
                if (m.IsSilenced() == true)
                    silenced.Add(m);
                else
                    free.Add(m);
            }
        }

        if (free.Count > 0)
        {
            free[UnityEngine.Random.Range(0, free.Count)].ApplySilence(duration);
            return;
        }

        // 모두 침묵 중이면 종료 시간이 가장 짧은 것 선택
        if (silenced.Count == 0) return;

        ModuleBase shortest = silenced[0];
        for (int i = 1; i < silenced.Count; i++)
        {
            if (silenced[i].GetSilenceEndTime() < shortest.GetSilenceEndTime())
                shortest = silenced[i];
        }
        shortest.ApplySilence(duration);
    }

    virtual public void TakeDamage(DamageInfo damageInfo, Vector3 hitPosition)
    {
        TakeDamage(damageInfo, hitPosition, out _);
    }

    // wasFullyShielded: 실드가 원본 데미지를 전량 흡수해 체력에 전혀 닿지 않았는지 — 호출부(ProjectileBeam 등)가 EFFECT_BEAM_HIT 스폰 여부를 판단하는 데 사용
    virtual public void TakeDamage(DamageInfo damageInfo, Vector3 hitPosition, out bool wasFullyShielded)
    {
        wasFullyShielded = false;

        // 전투 종료 처리 중 데미지 차단
        if (ObjectManager.Instance.m_isBattleEnding == true) return;
        // 이미 죽었다면 리턴
        if (IsAlive() == false) return;

        float incomingDamage = damageInfo.GetFinalDamage();

        // 진형 데미지 차감 (x_defensive 전용)
        if (m_ownerFleet != null)
        {
            float reduction = m_ownerFleet.GetFormationDefenseReduction();
            incomingDamage *= (1f - reduction);
        }

        // 체력 비율 하한선이 설정된 함선(튜토리얼 기함 등)은 그 이하로 실제 파괴되지 않도록 데미지를 클램프
        if (m_minHealthRatio > 0f)
        {
            float totalHealth = 0f;
            float totalHealthMax = 0f;
            foreach (ModuleHull body in m_moduleHulls)
            {
                if (body == null) continue;
                totalHealth += body.m_health;
                totalHealthMax += body.m_healthMax;
            }

            float floorHealth = m_minHealthRatio * totalHealthMax;
            float allowedDamage = totalHealth - floorHealth;
            incomingDamage = Mathf.Clamp(incomingDamage, 0f, Mathf.Max(0f, allowedDamage));
        }

        // 살아있는 바디 중 하나에 랜덤으로 데미지 분산 (또는 첫 번째 바디에)
        ModuleHull targetBody = GetRandomAliveBody();
        if (targetBody != null)
        {
            if (damageInfo.damageType == EDamageType.Beam)
            {
                float damageBeforeShield = incomingDamage;
                incomingDamage = ApplyShieldAbsorption(targetBody, incomingDamage, hitPosition);
                wasFullyShielded = damageBeforeShield > 0f && incomingDamage <= 0f;
            }

            targetBody.TakeDamage(incomingDamage);
        }

        // 전체 함선 체력 재계산
        m_spaceShipStatsCur = GetShipCapabilityProfile(false);

        EventManager.Trigger_FleetUpdateHP();
        EventManager.Trigger_ShipUpdateHP();

        // 화재 이펙트: 체력 비율이 임계값 이하로 떨어진 시점에 하나씩 생성
        if (IsAlive() == true)
        {
            UpdateFireEffects(hitPosition);
            // 실드가 완전히 막았으면 함체 표면에 실제로 닿지 않았으므로 피탄 자국(데칼)도 남기지 않음
            if (damageInfo.damageType == EDamageType.Beam && wasFullyShielded == false)
                SpawnScorchMark(hitPosition);
            return;
        }

        HandleShipDestroyed();
    }

    // 전투 피격으로 인한 사망 처리 — 폭발 이펙트/사운드 후 파괴
    private void HandleShipDestroyed()
    {
        ClearAllFireEffects();
        ClearAllScorchMarks();
        // 코루틴 중지
        StopAllCoroutines();
        // 전투 중 파괴 — 슬롯 null 처리 (인덱스 유지, UI 파괴 표시용)
        SpaceFleet parentFleet = GetComponentInParent<SpaceFleet>();
        if (parentFleet != null)
            parentFleet.SetShipNullified(this);
        // 폭발 이펙트 생성
        EffectBase effect = ObjectManager.Instance.m_poolManager.Get<EffectBase>(EPoolName.EFFECT_EXPLOSION_SHIP);
        effect.transform.position = transform.position;
        effect.PlayEffect();
        SoundManager.Instance.PlayFX(EFx.Explosion_Ship, transform.position);
        // 파괴 처리
        Destroy(gameObject);
    }

    // 전투 데미지가 아니라 연출(시네마틱)로 함선을 강제 파괴 — m_minHealthRatio 하한과 무관하게 즉시 파괴
    public void DestroyForCinematic()
    {
        HandleShipDestroyed();
    }

    private void UpdateFireEffects(Vector3 hitPosition)
    {
        if (m_spaceShipStatsOrg.health <= 0f) return;
        float ratio = m_spaceShipStatsCur.health / m_spaceShipStatsOrg.health;
        if (ratio < 0.5f && m_fireEffect50 == null) SpawnFireEffect(ref m_fireEffect50, hitPosition);
        if (ratio < 0.3f && m_fireEffect30 == null) SpawnFireEffect(ref m_fireEffect30, hitPosition);
        if (ratio < 0.1f && m_fireEffect10 == null) SpawnFireEffect(ref m_fireEffect10, hitPosition);
    }

    public void CheckFireEffects()
    {
        if (m_spaceShipStatsOrg.health <= 0f) return;
        float ratio = m_spaceShipStatsCur.health / m_spaceShipStatsOrg.health;
        if (ratio >= 0.5f) ReturnFireEffect(ref m_fireEffect50);
        if (ratio >= 0.3f) ReturnFireEffect(ref m_fireEffect30);
        if (ratio >= 0.1f) ReturnFireEffect(ref m_fireEffect10);
    }

    private void SpawnFireEffect(ref EffectBase slot, Vector3 position)
    {
        slot = ObjectManager.Instance.m_poolManager.Get<EffectBase>(EPoolName.EFFECT_FIRE_ON_SHIP);
        slot.transform.SetParent(transform, false);
        slot.transform.position = position;
        slot.PlayEffect();
    }

    private void ReturnFireEffect(ref EffectBase slot)
    {
        if (slot == null) return;
        // loop=false 자동 반환 후 슬롯이 dangling reference가 된 경우 이중 반환 방지
        if (slot.gameObject.activeInHierarchy == true)
            slot.ReturnEffect();
        slot = null;
    }

    private void ClearAllFireEffects()
    {
        ReturnFireEffect(ref m_fireEffect50);
        ReturnFireEffect(ref m_fireEffect30);
        ReturnFireEffect(ref m_fireEffect10);
    }

    public void SpawnScorchMark(Vector3 hitPosition)
    {
        if (m_scorchMarks.Count >= k_maxScorchMarks)
            ReturnScorchMarkAt(0);

        EffectBase mark = ObjectManager.Instance.m_poolManager.Get<EffectBase>(EPoolName.EFFECT_SCORCH_MARK);
        DecalProjector decal = mark.GetComponent<DecalProjector>();
        mark.transform.SetParent(transform, true);
        Vector3 dirToCenter = transform.position - hitPosition;
        // 방향을 빔 방향과 같게
        Vector3 outwardDir = -dirToCenter.normalized;
        float projectionDepth = decal.size.z;
        // 위치는 뎁스 반 만큼 뒤로 뺀다. 그러면 타격점 근처의 솟아난 부분까지 데칼 효과가 나타나게되어 품질이 좋다
        mark.transform.position = hitPosition + outwardDir * (projectionDepth * 0.5f);
        mark.transform.rotation = Quaternion.LookRotation(dirToCenter);
        mark.PlayEffect();
        Coroutine co = StartCoroutine(ScorchMarkLifeCycle(decal, mark));
        m_scorchMarks.Add(mark);
        m_scorchCoroutines.Add(co);
        m_scorchDecals.Add(decal);
    }

    private IEnumerator ScorchMarkLifeCycle(DecalProjector decal, EffectBase mark)
    {
        yield return new WaitForSeconds(k_scorchHoldTime);

        float elapsed = 0f;
        while (elapsed < k_scorchFadeTime)
        {
            elapsed += Time.deltaTime;
            if (decal != null)
                decal.fadeFactor = Mathf.Lerp(1f, 0f, elapsed / k_scorchFadeTime);
            yield return null;
        }

        int idx = m_scorchMarks.IndexOf(mark);
        if (idx >= 0)
        {
            m_scorchMarks.RemoveAt(idx);
            m_scorchCoroutines.RemoveAt(idx);
            m_scorchDecals.RemoveAt(idx);
        }
        if (decal != null) decal.fadeFactor = 1f;
        mark.ReturnEffect();
    }

    private void ReturnScorchMarkAt(int index)
    {
        if (m_scorchCoroutines[index] != null) StopCoroutine(m_scorchCoroutines[index]);
        DecalProjector decal = m_scorchDecals[index];
        if (decal != null) decal.fadeFactor = 1f;
        m_scorchMarks[index].ReturnEffect();
        m_scorchMarks.RemoveAt(index);
        m_scorchCoroutines.RemoveAt(index);
        m_scorchDecals.RemoveAt(index);
    }

    private void ClearAllScorchMarks()
    {
        for (int i = m_scorchMarks.Count - 1; i >= 0; i--)
            ReturnScorchMarkAt(i);
    }

    // 이 함선이 함대의 현재 기함인지 확인 (기함 승계 반영)
    public bool IsFlagship()
    {
        return m_ownerFleet.GetFlagship() == this;
    }

    // 함선이 살아있는지 확인
    public bool IsAlive()
    {
        return m_spaceShipStatsCur.health > 0 && HasAliveBodies();
    }

    // 함선 HP 비율 (모든 바디 합산)
    public float GetHealthRatio()
    {
        float total = 0f, max = 0f;
        foreach (ModuleHull body in m_moduleHulls)
        {
            if (body == null) continue;
            total += body.m_health;
            max   += body.m_healthMax;
        }
        return max > 0f ? total / max : 1f;
    }

    // 함선 실드 게이지 비율 (모든 바디 합산) — GetHealthRatio와 동일한 집계 방식
    public float GetShieldRatio()
    {
        float total = 0f, max = 0f;
        foreach (ModuleHull body in m_moduleHulls)
        {
            if (body == null || body.m_shield == null) continue;
            total += body.m_shield.GetGauge();
            max   += body.m_shield.GetGaugeMax();
        }
        return max > 0f ? total / max : 1f;
    }

    // 존 런 중 함선 종류 교체(ObjectManager.ReplaceMyFleetShipAt) 시 이전 함선의 체력 비율을 새 함선에 적용하거나,
    // 퇴각/포기 시(UIPanelExplorationGrid.RestorePreviousCellAfterRetreat) 진입 직전 상태로 롤백하는 데 사용
    // 두 경우 모두 실드는 갓 생성된 새 함체 또는 진입 직전 기준이라 항상 풀게이지로 리셋
    public void ApplyHealthRatio(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        foreach (ModuleHull body in m_moduleHulls)
        {
            if (body == null) continue;
            body.m_health = body.m_healthMax * ratio;
            if (body.m_shield != null)
                body.m_shield.ResetGaugeToFull();
        }
        UpdateShipStatCur();
        CheckFireEffects();
    }

    // 앱 재시작 시 서버에 저장된 체력/실드 스냅샷을 그대로 복구하는 용도 — ApplyHealthRatio와 달리 실드를 풀게이지로 리셋하지 않고 저장된 비율을 그대로 적용
    public void ApplyHealthAndShieldRatio(float healthRatio, float shieldRatio)
    {
        healthRatio = Mathf.Clamp01(healthRatio);
        shieldRatio = Mathf.Clamp01(shieldRatio);
        foreach (ModuleHull body in m_moduleHulls)
        {
            if (body == null) continue;
            body.m_health = body.m_healthMax * healthRatio;
            if (body.m_shield != null)
                body.m_shield.SetGaugeRatio(shieldRatio);
        }
        UpdateShipStatCur();
        CheckFireEffects();
    }

    // 살아있는 바디가 있는지 확인
    private bool HasAliveBodies()
    {
        foreach (ModuleHull body in m_moduleHulls)
        {
            if (body != null && body.m_health > 0)
            {
                return true;
            }
        }
        return false;
    }

    // 실드 토글 비트 인덱스 — EventManager.OnTacticOptionsChanged/UIPanelBattle 주석과 동일 규칙(0=수리, 1=미사일, 2=함재기, 3=실드)
    private const int k_shieldTacticBit = 1 << 3;

    // 지금 이 함선의 실드가 incomingDamage를 전량 막아낼 수 있는 상태인지(장착 + 게이지 충분 + 전술 토글 ON) — ProjectileBeam이
    // raycast 단계에서 빔 궤적을 실드 표면에서 끊을지(완전 방어) 함체 표면까지 보낼지(관통) 미리 판단하는 데 사용.
    // 게이지가 있어도 이번 데미지를 다 못 막으면(관통 발생) false를 반환해 빔이 함체 표면까지 도달하게 함
    // 내가 직접 조작하는 함대(fleet_source_player)만 토글 상태를 따르고, 그 외(적/PvP 상대/시네마틱)는 상시 ON으로 취급
    public bool IsShieldActive(float incomingDamage)
    {
        ModuleHull targetBody = GetRandomAliveBody();
        if (targetBody == null) return false;

        ModuleShield shield = targetBody.m_shield;
        if (shield == null || shield.CanFullyAbsorb(incomingDamage) == false) return false;

        if (m_ownerFleet != null && m_ownerFleet.m_fleetSource == EFleetSource.fleet_source_player)
            return (m_ownerFleet.m_fleetInfo.tacticOptions & k_shieldTacticBit) != 0;

        return true;
    }

    // 빔 피격 시 실드가 흡수하는 만큼 차감한 나머지 데미지를 반환 — 실드 미장착/게이지 0/토글 OFF면 원본 그대로 반환
    private float ApplyShieldAbsorption(ModuleHull targetBody, float incomingDamage, Vector3 hitPosition)
    {
        ModuleShield shield = targetBody.m_shield;
        if (shield == null || shield.IsEquipped() == false) return incomingDamage;

        bool isShieldOn = true;
        if (m_ownerFleet != null && m_ownerFleet.m_fleetSource == EFleetSource.fleet_source_player)
            isShieldOn = (m_ownerFleet.m_fleetInfo.tacticOptions & k_shieldTacticBit) != 0;

        if (isShieldOn == false) return incomingDamage;

        float absorbed = shield.AbsorbBeamDamage(incomingDamage);
        if (absorbed > 0f)
            SpawnShieldHitEffect(targetBody, hitPosition);

        return incomingDamage - absorbed;
    }

    // hitPosition(함체 명중 지점)을 실드 중심 기준 방향으로 ShieldCollider 표면까지 레이캐스트해 실제 표면 지점을 구해 파동 이펙트 배치
    // (히트스캔 빔은 함체 트랜스폼 위치를 그대로 넘겨줘 실드 타원체 표면과 정확히 일치하지 않음 — 레이캐스트로 보정)
    private void SpawnShieldHitEffect(ModuleHull targetBody, Vector3 hitPosition)
    {
        if (m_shieldGrid == null) return;

        Vector3 shieldCenter = m_shieldGrid.transform.position;
        Vector3 direction = (hitPosition - shieldCenter).normalized;
        if (direction == Vector3.zero) direction = Vector3.up;

        Vector3 surfacePoint = hitPosition;
        Vector3 surfaceNormal = direction;

        int layer = LayerMask.NameToLayer(m_shieldGrid.shieldLayerName);
        if (layer >= 0)
        {
            int layerMask = 1 << layer;
            Vector3 rayOrigin = shieldCenter - direction * 0.01f;
            if (Physics.Raycast(rayOrigin, direction, out RaycastHit hit, 10000f, layerMask, QueryTriggerInteraction.Collide) == true)
            {
                surfacePoint = hit.point;
                surfaceNormal = hit.normal;
            }
        }

        EffectShieldHit effect = ObjectManager.Instance.m_poolManager.Get<EffectShieldHit>(EPoolName.EFFECT_SHIELD_HIT);
        if (effect != null)
            effect.PlayAt(surfacePoint, surfaceNormal);

        // 실드 표면 전체를 타고 퍼지는 격자 파동 — 임팩트 섬광(EffectShieldHit)과 병행 표시
        m_shieldGrid.PlayHitWave(surfacePoint);
    }

    // 살아있는 바디 중 랜덤 선택
    private ModuleHull GetRandomAliveBody()
    {
        List<ModuleHull> aliveBodies = new List<ModuleHull>();
        foreach (ModuleHull body in m_moduleHulls)
        {
            if (body != null && body.m_health > 0)
            {
                aliveBodies.Add(body);
            }
        }

        if (aliveBodies.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, aliveBodies.Count);
            return aliveBodies[randomIndex];
        }

        return null;
    }

    public void UpdateShipStatCur()
    {
        m_spaceShipStatsCur = GetShipCapabilityProfile(false);
    }

    public void UpdateShipStats()
    {
        m_spaceShipStatsOrg = GetShipCapabilityProfile(true);
        m_spaceShipStatsCur = GetShipCapabilityProfile(false);
    }

    // 인덱스로 바디 찾기
    public ModuleHull FindModuleHullByIndex(int hullIndex)
    {
        foreach (ModuleHull body in m_moduleHulls)
        {
            if (body != null && body.m_moduleHullInfo.hullIndex == hullIndex)
            {
                return body;
            }
        }
        return null;
    }

    // hullIndex, moduleTypePacked, slotIndex로 특정 모듈 찾기
    public ModuleBase FindModule(int hullIndex, EModuleType moduleType, int slotIndex)
    {
        ModuleHull body = FindModuleHullByIndex(hullIndex);
        if (body == null) return null;

        if (moduleType == EModuleType.hull)
            return body;

        return body.FindModule(moduleType, slotIndex);
    }

    // 함선의 능력치 프로파일 계산
    // bByInfo = true: Info 기반 계산 (최대 스펙)
    // bByInfo = false: 실제 상태 기반 계산 (현재 체력/상태 반영)
    public CapabilityProfile GetShipCapabilityProfile(bool bByInfo = true)
    {
        if (bByInfo == true) return CommonUtility.GetShipCapabilityProfile(m_shipInfo);

        CapabilityProfile stats = new CapabilityProfile();
        foreach (ModuleHull body in m_moduleHulls)
        {
            if (body != null && body.m_health > 0)
            {
                CapabilityProfile bodyStats = body.GetModuleCapabilityProfile(false);
                stats.totalWeapons += bodyStats.totalWeapons;
                stats.beamAttack += bodyStats.beamAttack;
                stats.missileAttack += bodyStats.missileAttack;
                stats.health += bodyStats.health;
                stats.speed += bodyStats.speed;
                stats.repair += bodyStats.repair;
                stats.airAttack += bodyStats.airAttack;
                stats.airCount += bodyStats.airCount;
            }
        }
        return stats;
    }


    #region Display migration ============================================================
    // Private fields
    private List<SelectedModuleVisual> m_selectedModuleVisuals = new List<SelectedModuleVisual>();
    private ModuleBase m_selectedModule = null;

    private void SetupSelectedModuleVisualing()
    {
        // Setup SelectedModuleVisual for parts bodies
        foreach (ModuleHull body in m_moduleHulls)
        {
            if (body != null)
            {
                SetupSelectedModuleVisual(body);

                // Setup SelectedModuleVisual for all modules in slots — Placeholder(빈 슬롯)도 선택 시 그 위치에 하이라이트를 보여줘야 하므로 포함
                foreach (ModuleSlot slot in body.m_moduleSlots)
                {
                    ModuleBase module = body.FindModuleOrPlaceholder(slot.m_moduleSlotInfo.moduleType, slot.m_moduleSlotInfo.slotIndex);
                    if (module != null)
                        SetupSelectedModuleVisual(module);
                }
            }
        }
    }

    private void SetupSelectedModuleVisual(ModuleBase moduleBase)
    {
        // Add SelectedModuleVisual component
        SelectedModuleVisual selectedModuleVisual = moduleBase.gameObject.AddComponent<SelectedModuleVisual>();
        selectedModuleVisual.InitializeSelectedModuleVisual(this, moduleBase);
        m_selectedModuleVisuals.Add(selectedModuleVisual);
    }


    private Bounds CalculatePartsBounds(ModuleBase partsBase)
    {
        Bounds bounds = new Bounds(partsBase.transform.position, Vector3.one);

        // Include all child renderers
        Renderer[] renderers = partsBase.GetComponentsInChildren<Renderer>();
        bool hasRenderers = false;

        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.enabled)
            {
                if (!hasRenderers)
                {
                    bounds = renderer.bounds;
                    hasRenderers = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        // Ensure minimum size for interaction
        if (!hasRenderers || bounds.size.magnitude < 1f)
        {
            bounds.center = partsBase.transform.position;
            bounds.size = Vector3.one * 1.5f;
        }

        return bounds;
    }

    public void SetSelectedModule(SpaceShip ship, ModuleBase module)
    {
        if (m_ownerFleet == null) return;
        if (this != ship) return;
        m_ownerFleet.ClearAllSelectedModule();
        m_selectedModule = module;
        UpdateSelectedModuleVisual();
    }

    public void ClearSelectedModule()
    {
        m_selectedModule = null;
        UpdateSelectedModuleVisual();
    }

    private void UpdateSelectedModuleVisual()
    {
        foreach (var selectedModuleVisual in m_selectedModuleVisuals)
        {
            if (selectedModuleVisual != null)
            {
                bool isSelected = (selectedModuleVisual.ModuleBase == m_selectedModule);
                selectedModuleVisual.SetSelected(isSelected);
            }
        }
    }
    #endregion


    #region Formation Movement
    public FormationMoveState m_formationMoveState = FormationMoveState.Idle;
    private Vector3 m_formationTarget;
    private Vector3 m_avoidanceAccum;      // OnShieldTriggerStay에서 프레임마다 누적
    private Coroutine m_formationCoroutine;
    private float m_movementSpeedMult = 1f; // 스폰 진입 시 빠른 속도 배율
    public bool m_bWarp = false; // true = 워핑 진입 중, false = 워프 종료
    public bool IsWarping => m_bWarp == true && m_formationMoveState == FormationMoveState.Moving;
    private const float WARP_STOP_DIST = 3f; // 워프 이펙트 종료 & 속도 리셋 거리

    [Header("Formation Avoidance")]
    [Tooltip("이 값 이상이면 회피 시작 (침투 깊이 기준, 0~1)")]
    private float m_avoidActivateThreshold = 0.001f;
    [Tooltip("avoidWeight를 이 값으로 remap — 낮출수록 작은 겹침에도 강하게 회피")]
    private float m_avoidWeightScale = 2f;

    // fleet이 CalculateFormationTargets()로 계산한 목적지를 전달받아 이동 시작
    // speedMult: 1.0 = 일반, 10+ = 스폰 워프 진입용 고속
    public void MoveToFormation(Vector3 target, bool bWarp, float speedMult = 1f)
    {
        m_formationTarget = target;
        m_movementSpeedMult = speedMult;
        m_bWarp = bWarp;
        m_formationMoveState = FormationMoveState.Moving;

        if (m_formationCoroutine != null)
            StopCoroutine(m_formationCoroutine);
        m_formationCoroutine = StartCoroutine(FormationMovementLoop());
    }

    public void StopFormationMovement()
    {
        m_formationMoveState = FormationMoveState.Idle;
        if (m_formationCoroutine != null)
        {
            StopCoroutine(m_formationCoroutine);
            m_formationCoroutine = null;
        }
        m_avoidanceAccum = Vector3.zero;
    }

    // 실드 트리거 릴레이에서 호출 — 인덱스 우선권 기반 회피 벡터 누적
    public void OnShieldTriggerStay(SpaceShip other, float penetrationDepth)
    {
        if (m_formationMoveState != FormationMoveState.Moving) return;
        if (other == null || other.m_ownerFleet != m_ownerFleet) return;

        // 내 인덱스가 낮으면 우선권 있음 → 상대가 피함
        if (m_shipInfo.positionIndex <= other.m_shipInfo.positionIndex) return;

        Vector3 awayDir = (transform.localPosition - other.transform.localPosition);
        float dist = awayDir.magnitude;
        if (dist < 0.001f) return;

        // 침투 깊이 비례 회피 방향 누적 (depth=0이면 무시, depth클수록 강하게 밀어냄)
        m_avoidanceAccum += awayDir.normalized * penetrationDepth;
    }

    private IEnumerator FormationMovementLoop()
    {
        // 로컬 스페이스 기준 — 진입 방향은 항상 로컬 +Z
        Vector3 WarpStopPos = m_formationTarget + (-Vector3.forward * WARP_STOP_DIST);
        Vector3 startToTarget = m_formationTarget - transform.localPosition;
        while (m_formationMoveState == FormationMoveState.Moving)
        {
            Vector3 currentPos = transform.localPosition;
            float dist = (m_formationTarget - currentPos).magnitude;
            Vector3 toTarget = (m_formationTarget - currentPos).normalized;
            if (m_bWarp == true)
            {
                Vector3 toWarpStop = (WarpStopPos - currentPos).normalized;
                float dot = Vector3.Dot(Vector3.forward, toWarpStop);
                // 워프 종료 지점 — 이펙트 끄고 속도 즉시 일반으로 리셋
                if (m_bWarp == true && dot <= 0)
                {
                    m_bWarp = false;
                    m_movementSpeedMult = 1f;
                    if (TryGetComponent(out WarpEffectShip warpEffect))
                        warpEffect.StopWarp();
                }
            }
            else
            {
                // 초기 접근 방향 대비 반전 = 목표 통과 → 스냅
                float dot_arrival = Vector3.Dot(startToTarget, toTarget);
                if (dot_arrival <= 0)
                {
                    transform.localPosition = m_formationTarget;
                    m_formationMoveState = FormationMoveState.Arrived;
                    m_formationCoroutine = null;
                    ApplyFleetStateToShip();
                    if (m_ownerFleet != null && m_ownerFleet.m_fleetState.IsBattleState() == true)
                        StartFindingTargets();
                    yield break;
                }
            }

            // 목표 방향 + 이번 프레임 누적 회피 벡터 혼합
            Vector3 avoidDir  = m_avoidanceAccum;
            m_avoidanceAccum  = Vector3.zero;

            // 침투 깊이가 클수록 회피 비율 증가 (0=목표 방향, 1=완전 회피)
            float avoidWeight = Mathf.Clamp01(avoidDir.magnitude * m_avoidWeightScale);
            Vector3 finalDir = avoidWeight > m_avoidActivateThreshold
                ? Vector3.Lerp(toTarget, avoidDir.normalized, avoidWeight).normalized
                : toTarget;

            // 워프 중에는 풀스피드, 워프 종료 후 바로 평속
            float speed = m_spaceShipStatsCur.speed * m_movementSpeedMult;

            // 오버슈팅 방지
            float moveDist = Mathf.Min(speed * Time.deltaTime, dist);
            transform.localPosition = currentPos + finalDir * moveDist;

            yield return null;
        }
    }

    public Bounds CalculateShipBounds()
    {
        Bounds bounds = CommonUtility.CalculateRendererBounds(transform, excludeParticles: true, excludeTrails: true, excludeDisabled: false);

        // 렌더러가 없으면 기본 크기
        if (bounds.size == Vector3.zero)
            bounds.size = Vector3.one * 2f;

        return bounds;
    }

    // 진형 배치 전용 — 실드 기준 (gridGap = 실드 사이 실제 간격)
    public Bounds CalculateFormationBounds()
        => new Bounds(transform.position, m_shieldGrid.GetFormationExtents() * 2f);

    // 모듈 교체 후 ModuleVisual 갱신 (효율적으로)
    public void RefreshSelectedModuleVisuals()
    {
        // 1. 파괴된 모듈의 selectedModuleVisual 만 리스트에서 제거
        m_selectedModuleVisuals.RemoveAll(h => h == null || h.ModuleBase == null);

        // 2. Body 모듈 확인 및 추가
        foreach (ModuleHull body in m_moduleHulls)
        {
            if (body == null) continue;

            // Body에 SelectedModuleVisual가 없으면 추가 (새로 생성된 Body)
            if (body.GetComponent<SelectedModuleVisual>() == null)
                SetupSelectedModuleVisual(body);

            // 3. 각 슬롯의 모듈 확인
            foreach (ModuleSlot slot in body.m_moduleSlots)
            {
                if (slot == null || slot.transform.childCount == 0) continue;

                ModuleBase module = slot.GetComponentInChildren<ModuleBase>();

                // 이미 selectedModuleVisual 가 있는지 확인 (SelectedModuleVisual 컴포넌트로 체크)
                if (module != null && module.GetComponent<SelectedModuleVisual>() == null)
                {
                    // 새로 생성된 모듈이므로 selectedModuleVisual 추가
                    SetupSelectedModuleVisual(module);
                }
            }
        }
    }

    // private void OnDrawGizmos()
    // {
    //     Bounds shipBounds = CalculateShipBounds();

    //     Gizmos.color = Color.yellow;
    //     Gizmos.DrawWireCube(shipBounds.center, shipBounds.size);

    //     if (m_formationMoveState == FormationMoveState.Idle) return;

    //     Vector3 shipSize = shipBounds.size;
    //     float castRadius = Mathf.Max(shipSize.x, shipSize.y, shipSize.z) * 0.5f;

    //     Gizmos.color = Color.cyan;
    //     Gizmos.DrawWireSphere(m_formationTarget, castRadius);

    //     Gizmos.color = Color.green;
    //     Gizmos.DrawLine(transform.position, m_formationTarget);
    // }
    #endregion
}
