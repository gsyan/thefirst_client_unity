//------------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
//using System.Numerics;
using UnityEngine;


// 능력치 프로파일 구조체 (함선/함대의 전투 및 작전 능력)
[System.Serializable]
public struct CapabilityProfile
{
    // 기존 능력치 (하위 호환성 유지 - deprecated)
    public int totalWeapons;
    
    // 세부 전투 능력치
    public float attack;                // 공격력
    public float health;                // 체력
    public float speed;                 // 속력 (이동+회전 통합)
    public float repair;                // 수리 능력
    public int airAttack;               // 함재기 공격력
    public int airCount;                // 함재기 수
}

public class SpaceShip : MonoBehaviour
{
    [SerializeField] public ShipInfo m_shipInfo;
    [SerializeField] public List<ModuleBody> m_moduleBodys = new List<ModuleBody>();
    [SerializeField] public SpaceShip m_targetShip;
    [SerializeField] public CapabilityProfile m_spaceShipStatsOrg;
    [SerializeField] public CapabilityProfile m_spaceShipStatsCur;

    public SpaceFleet m_myFleet;
    public EUnitState m_shipState;
    [HideInInspector] public Outline m_shipOutline;

    // Zone 적 전용 — 함선별 스탯 배율 (InitializeSpaceShip 전에 세팅, 기본값 1.0)
    public float m_bodyMultiplier    = 1.0f;
    public float m_beamMultiplier    = 1.0f;
    public float m_missileMultiplier = 1.0f;
    public float m_hangerMultiplier  = 1.0f;

    private GaugeBars m_gaugeBars;
    public ShieldGrid m_shieldGrid;

    virtual protected void Start()
    {
        InitializeGaugeDisplay();
    }

    private void InitializeGaugeDisplay()
    {
        m_gaugeBars = GetComponent<GaugeBars>();
        if (m_gaugeBars == null)
            m_gaugeBars = gameObject.AddComponent<GaugeBars>();
    }

    public void InitializeSpaceShip(SpaceFleet fleet, ShipInfo shipInfo)
    {
        m_myFleet = fleet;
        m_shipInfo = shipInfo;
        if (shipInfo.bodies == null || shipInfo.bodies.Count == 0) return;
        foreach (ModuleBodyInfo bodyInfo in shipInfo.bodies)
            InitSpaceShipBody(bodyInfo, null);

        m_spaceShipStatsOrg = GetShipCapabilityProfile(true);
        m_spaceShipStatsCur = GetShipCapabilityProfile(false);

        SetupSelectedModuleVisualing();
        
        // ShieldGrid, 지금은 바디가 오직 하나...
        m_shieldGrid = m_moduleBodys[0].GetComponent<ShieldGrid>();
        if (m_shieldGrid != null)
            m_shieldGrid.InitFormationRelay(this);
        
        // Outline 미리 설정
        m_shipOutline = gameObject.AddComponent<Outline>();
        m_shipOutline.OutlineMode = Outline.Mode.OutlineAll;
        m_shipOutline.OutlineColor = Color.cyan;
        m_shipOutline.OutlineWidth = 5f;
        m_shipOutline.enabled = false; // 기본은 꺼둠

    }

   // Body 초기화 (기존 모듈 재사용 가능)
    private ModuleBody InitSpaceShipBody(ModuleBodyInfo bodyInfo, List<ModuleBase> savedModules)
    {
        GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(bodyInfo.moduleType.ToString(), bodyInfo.moduleSubType.ToString());
        if (modulePrefab == null)
        {
            Debug.LogError("No prefab");
            return null;  
        } 

        GameObject bodyObj = Instantiate(modulePrefab, transform.position, transform.rotation);
        bodyObj.transform.SetParent(transform);

        ModuleBody moduleBody = bodyObj.GetComponent<ModuleBody>();
        if (moduleBody == null)
            moduleBody = bodyObj.AddComponent<ModuleBody>();

        moduleBody.InitializeModuleBody(bodyInfo, savedModules);
        m_moduleBodys.Add(moduleBody);
        moduleBody.ApplyShipStateToModule(); // 모듈 변경시를 위해 필요
        return moduleBody;
    }

    private ModuleBody m_currentTargetBody;
    private Coroutine m_findTargetModuleBodyCoroutine;
    private Coroutine m_rotationCoroutine;
    private Coroutine m_returnRotationCoroutine;
    private const float k_angularSpeedMult = 2f;
    private List<ModuleBody> m_candidateBodies = new List<ModuleBody>();
    private readonly WaitForSeconds m_waitOneSecond = new WaitForSeconds(1.0f);
    
    public void ApplyFleetStateToShip()
    {
        // 진형 이동 중이면 도착 후 FormationMovementLoop에서 재호출됨
        if (m_formationMoveState == FormationMoveState.Moving) return;

        switch (m_myFleet.m_fleetState)
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
            case EUnitState.Battle:
                m_shipState = EUnitState.Battle;
                if (m_returnRotationCoroutine != null)
                {
                    StopCoroutine(m_returnRotationCoroutine);
                    m_returnRotationCoroutine = null;
                }
                if (m_findTargetModuleBodyCoroutine == null)
                    m_findTargetModuleBodyCoroutine = StartCoroutine(FindTargetModuleBody());
                if (m_rotationCoroutine == null)
                    m_rotationCoroutine = StartCoroutine(RotateTowardTarget());
                break;
            default:
                m_shipState = EUnitState.Idle;
                StopAutoCombat();
                break;
        }
        
        foreach (ModuleBody body in m_moduleBodys)
            body.ApplyShipStateToModule();
    }



    public void StopAutoCombat()
    {
        if (m_findTargetModuleBodyCoroutine != null)
        {
            StopCoroutine(m_findTargetModuleBodyCoroutine);
            m_findTargetModuleBodyCoroutine = null;
        }
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

    private IEnumerator FindTargetModuleBody()
    {
        while (true)
        {
            // 현재 타겟이 살아있으면 유지 — 죽었거나 없을 때만 재선택
            bool currentTargetAlive = m_currentTargetBody != null
                && m_currentTargetBody.gameObject.activeSelf
                && m_currentTargetBody.m_health > 0;

            if (currentTargetAlive == false)
            {
                CollectCandidateEnemyBodies(m_candidateBodies);
                ModuleBody best = FindMinAngleBody(m_candidateBodies);

                if (best == null)
                {
                    yield return m_waitOneSecond;
                    continue;
                }

                m_currentTargetBody = best;
                m_targetShip = best.GetComponentInParent<SpaceShip>();

                foreach (ModuleBody body in m_moduleBodys)
                {
                    if (body != null && body.m_health > 0)
                        body.SetTarget(m_currentTargetBody);
                }
            }

            yield return m_waitOneSecond;
        }
    }

    private void CollectCandidateEnemyBodies(List<ModuleBody> result)
    {
        result.Clear();
        if (m_myFleet != null && m_myFleet.IsEnemy == true)
        {
            SpaceFleet myFleet = ObjectManager.Instance.m_myFleet;
            if (myFleet == null) return;
            foreach (SpaceShip ship in myFleet.m_ships)
            {
                if (ship == null || ship.IsAlive() == false) continue;
                foreach (ModuleBody body in ship.m_moduleBodys)
                {
                    if (body != null && body.m_health > 0)
                        result.Add(body);
                }
            }
        }
        else
        {
            List<SpaceFleet> enemyFleets = ObjectManager.Instance.m_enemyFleets;
            foreach (SpaceFleet fleet in enemyFleets)
            {
                if (fleet == null || fleet.IsFleetAlive() == false || fleet.m_fleetState != EUnitState.Battle) continue;
                foreach (SpaceShip ship in fleet.m_ships)
                {
                    if (ship == null || ship.IsAlive() == false || ship.IsWarping == true) continue;
                    foreach (ModuleBody body in ship.m_moduleBodys)
                    {
                        if (body != null && body.m_health > 0)
                            result.Add(body);
                    }
                }
            }
        }
    }

    private ModuleBody FindMinAngleBody(List<ModuleBody> candidates)
    {
        ModuleBody best = null;
        float bestAngle = float.MaxValue;
        Vector3 forward = transform.forward;
        Vector3 myPos = transform.position;

        foreach (ModuleBody body in candidates)
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
                    Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized);
                    float angularSpeed = m_spaceShipStatsCur.speed * k_angularSpeedMult;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, angularSpeed * Time.deltaTime);
                }
            }
            yield return null;
        }
    }

    private IEnumerator ReturnToFleetForward()
    {
        Vector3 fleetForward = m_myFleet != null ? m_myFleet.transform.forward : Vector3.forward;
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

    virtual public void TakeDamage(float attackPower)
    {
        // 이미 죽었다면 리턴
        if (IsAlive() == false) return;
        // 살아있는 바디 중 하나에 랜덤으로 데미지 분산 (또는 첫 번째 바디에)
        ModuleBody targetBody = GetRandomAliveBody();
        if (targetBody != null)
        {
            targetBody.TakeDamage(attackPower);
        }

        // 전체 함선 체력 재계산
        m_spaceShipStatsCur = GetShipCapabilityProfile(false);

        EventManager.Trigger_FleetUpdateHP();
        EventManager.Trigger_ShipUpdateHP();
        
        // 데이미 처리 후 살았다면 이후 로직 생략
        if (IsAlive() == true) return;
        // 코루틴 중지
        StopAllCoroutines();        
        // SpaceFleet에서 자신을 제거
        SpaceFleet parentFleet = GetComponentInParent<SpaceFleet>();
        if (parentFleet != null)
            parentFleet.RemoveShip(this);
        // 폭발 이펙트 생성
        EffectBase effect = ObjectManager.Instance.m_poolManager.Get<EffectBase>(EPoolName.EFFECT_EXPLOSION_SHIP);
        effect.transform.position = transform.position;
        effect.PlayEffect();
        // 파괴 처리
        Destroy(gameObject);
    }

    // 함선이 살아있는지 확인
    public bool IsAlive()
    {
        return m_spaceShipStatsCur.health > 0 && HasAliveBodies();
    }

    // 살아있는 바디가 있는지 확인
    private bool HasAliveBodies()
    {
        foreach (ModuleBody body in m_moduleBodys)
        {
            if (body != null && body.m_health > 0)
            {
                return true;
            }
        }
        return false;
    }

    // 살아있는 바디 중 랜덤 선택
    private ModuleBody GetRandomAliveBody()
    {
        List<ModuleBody> aliveBodies = new List<ModuleBody>();
        foreach (ModuleBody body in m_moduleBodys)
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
    public ModuleBody FindModuleBodyByIndex(int bodyIndex)
    {
        foreach (ModuleBody body in m_moduleBodys)
        {
            if (body != null && body.m_moduleBodyInfo.bodyIndex == bodyIndex)
            {
                return body;
            }
        }
        return null;
    }

    // bodyIndex, moduleTypePacked, slotIndex로 특정 모듈 찾기
    public ModuleBase FindModule(int bodyIndex, EModuleType moduleType, int slotIndex)
    {
        ModuleBody body = FindModuleBodyByIndex(bodyIndex);
        if (body == null) return null;

        if (moduleType == EModuleType.body)
            return body;

        return body.FindModule(moduleType, slotIndex);
    }

    public void SetModuleInvestedModulePoint(int bodyIndex, EModuleType moduleType, int slotIndex, int modulePoint)
    {
        ModuleBase module = FindModule(bodyIndex, moduleType, slotIndex);
        if (module == null) return;
        module.SetInvestedModulePoint(modulePoint);
    }

    // 함선의 능력치 프로파일 계산
    // bByInfo = true: Info 기반 계산 (최대 스펙)
    // bByInfo = false: 실제 상태 기반 계산 (현재 체력/상태 반영)
    public CapabilityProfile GetShipCapabilityProfile(bool bByInfo = true)
    {
        if (bByInfo == true) return CommonUtility.GetShipCapabilityProfile(m_shipInfo);

        CapabilityProfile stats = new CapabilityProfile();
        foreach (ModuleBody body in m_moduleBodys)
        {
            if (body != null && body.m_health > 0)
            {
                CapabilityProfile bodyStats = body.GetModuleCapabilityProfile(false);
                stats.totalWeapons += bodyStats.totalWeapons;
                stats.attack += bodyStats.attack;
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
        foreach (ModuleBody body in m_moduleBodys)
        {
            if (body != null)
            {
                SetupSelectedModuleVisual(body);

                // Setup SelectedModuleVisual for all modules in slots
                foreach (ModuleSlot slot in body.m_moduleSlots)
                {
                    if (slot != null && slot.transform.childCount > 0)
                    {
                        ModuleBase module = slot.GetComponentInChildren<ModuleBase>();
                        if (module != null)
                            SetupSelectedModuleVisual(module);
                    }
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
        if (m_myFleet == null) return;
        if (this != ship) return;
        m_myFleet.ClearAllSelectedModule();
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
    public void MoveToFormation(Vector3 target, float speedMult = 1f)
    {
        m_formationTarget = target;
        m_movementSpeedMult = speedMult;
        m_bWarp = true;
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
        if (other == null || other.m_myFleet != m_myFleet) return;

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
                float dot_arrival = Vector3.Dot(Vector3.forward, toTarget);
                if (dot_arrival <= 0)
                {
                    transform.localPosition = m_formationTarget;
                    m_formationMoveState = FormationMoveState.Arrived;
                    m_formationCoroutine = null;
                    // 진형 도달 후 함대 상태 적용 (워프 진입 시 전투 개시 방지)
                    ApplyFleetStateToShip();
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
        foreach (ModuleBody body in m_moduleBodys)
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

    // module unlock (외부 호출용 - 모듈 해금 UI에서 사용)
    public void Apply_UnlockModule(int bodyIndex, EModuleType moduleType, EModuleSubType moduleSubType, int slotIndex,
                                    int investedModulePoint = 0)
    {
        ModuleBody body = FindModuleBodyByIndex(bodyIndex);
        if (body == null)
        {
            Debug.LogError($"Body not found: shipId={m_shipInfo.id}, bodyIndex={bodyIndex}");
            return;
        }

        int moduleLevel = 1;

        bool success = body.ReplaceModuleInSlot(moduleType, moduleSubType, moduleLevel, slotIndex);
        if (!success)
        {
            Debug.LogError($"Failed to unlock module: moduleType={moduleType}, slotIndex={slotIndex}");
            return;
        }

        // 언락 비용을 investedModulePoint에 반영
        ModuleSlot slot = body.FindModuleSlot(moduleType, slotIndex);
        if (slot != null)
        {
            ModuleBase newModule = slot.GetComponentInChildren<ModuleBase>();
            if (newModule != null)
                newModule.SetInvestedModulePoint(investedModulePoint);
        }

        // 함선 스탯 업데이트
        UpdateShipStats();

        // Outline 갱신 (새로 생성된 모듈들을 포함하도록)
        if (m_shipOutline != null)
             m_shipOutline.RefreshOutline();

        // 모듈 selectedModuleVisual 갱신 (새로 생성된 모듈들을 포함하도록)
        RefreshSelectedModuleVisuals();

        //Debug.Log($"Module unlocked: Ship={m_shipInfo.id}, Body={bodyIndex}, Slot={slotIndex}, Type={moduleType}");
    }

    // 모듈을 플레이스홀더 상태로 복귀 (리셋용)
    public void Apply_ResetModuleToPlaceholder(int bodyIndex, EModuleType moduleType, int slotIndex)
    {
        ModuleBody body = FindModuleBodyByIndex(bodyIndex);
        if (body == null) return;

        body.ResetModuleToPlaceholder(moduleType, slotIndex);
        UpdateShipStats();
        if (m_shipOutline != null) m_shipOutline.RefreshOutline();
        RefreshSelectedModuleVisuals();
    }

    // module 교체 (외부 호출용 - 모듈 교체 UI에서 사용)
    public void ApplyModuleChange(int bodyIndex, EModuleType moduleType, EModuleSubType moduleSubTypeNew, int slotIndex, int moduleNewLevel, List<EModuleSubType> newUnlockedSubTypes = null)
    {
        if (moduleType == EModuleType.body)
        {
            // Body 교체 처리 — 완료 후 relay 재설정 및 크기 변경 이벤트 발화
            ChangeModuleBody(bodyIndex, moduleType, moduleSubTypeNew, moduleNewLevel);
            m_shieldGrid = m_moduleBodys.Count > 0 ? m_moduleBodys[0].GetComponent<ShieldGrid>() : null;
            if (m_shieldGrid != null)
                m_shieldGrid.InitFormationRelay(this);
            EventManager.Trigger_ShipBodyChanged(this);
        }
        else
        {
            // 일반 모듈 교체
            ModuleBody body = FindModuleBodyByIndex(bodyIndex);
            if (body == null) return;
            bool success = body.ReplaceModuleInSlot(moduleType, moduleSubTypeNew, moduleNewLevel, slotIndex);
            if (success == false)
            {
                Debug.LogError($"Failed to replace module: moduleTypeNew={moduleType}");
                return;
            }
            // 전투 중 교체된 모듈에 현재 타겟 재전파
            if (m_currentTargetBody != null && m_currentTargetBody.m_health > 0)
                body.SetTarget(m_currentTargetBody);
        }

        // 서버에서 받은 unlock 목록으로 새 모듈 갱신
        if (newUnlockedSubTypes != null)
        {
            ModuleBase newModule = FindModule(bodyIndex, moduleType, slotIndex);
            if (newModule != null)
                newModule.SetUnlockedSubTypes(newUnlockedSubTypes);
        }

        // Outline 갱신 (새로 생성된 모듈들을 포함하도록)
        if (m_shipOutline != null)
            m_shipOutline.RefreshOutline();

        // 모듈 selectedModuleVisual 갱신 (새로 생성된 모듈들을 포함하도록)
        RefreshSelectedModuleVisuals();

        // SpaceShip 통계 업데이트
        UpdateShipStats();
    }
    private void ChangeModuleBody(int bodyIndex, EModuleType moduleTypeNew, EModuleSubType moduleSubTypeNew, int moduleLevel)
    {
        ModuleBody oldBody = FindModuleBodyByIndex(bodyIndex);
        if (oldBody == null) return;
        
        ModuleBodyInfo newBodyInfo = new ModuleBodyInfo
        {
            moduleType = moduleTypeNew,
            moduleSubType = moduleSubTypeNew,
            moduleLevel = moduleLevel,
            bodyIndex = bodyIndex,
            beams = oldBody.m_moduleBodyInfo.beams,
            missiles = oldBody.m_moduleBodyInfo.missiles,
            hangers = oldBody.m_moduleBodyInfo.hangers
        };

        ReplaceBodyWhilePreservingModules(oldBody, newBodyInfo);

        // m_shipInfo.bodies의 해당 항목을 새 ModuleBodyInfo로 교체 (FleetInfo까지 동일 참조이므로 함께 갱신됨)
        if (m_shipInfo.bodies != null)
        {
            for (int i = 0; i < m_shipInfo.bodies.Count; i++)
            {
                if (m_shipInfo.bodies[i].bodyIndex == bodyIndex)
                {
                    m_shipInfo.bodies[i] = newBodyInfo;
                    break;
                }
            }
        }
    }
    // Body 교체 시 기존 모듈을 보존하는 메서드
    private void ReplaceBodyWhilePreservingModules(ModuleBody oldBody, ModuleBodyInfo newBodyInfo)
    {
        // 1. 기존 body의 모든 모듈 수집 (ModulePlaceholder 제외)
        List<ModuleBase> savedModules = new List<ModuleBase>();
        foreach (var slot in oldBody.m_moduleSlots)
        {
            ModuleBase module = slot.GetComponentInChildren<ModuleBase>();
            if (module != null && (module is ModulePlaceholder) == false)
            {
                // 슬롯에서 모듈 분리 (파괴 방지)
                module.transform.SetParent(null);
                module.gameObject.SetActive(false); // 임시로 비활성화
                savedModules.Add(module);
            }
        }

        // 2. 교체 전 HP 비율 저장
        float healthRatio = oldBody.GetHealthRatio();

        // 3. 삭제 전 이벤트 발행 (oldBody 아직 유효)
        EventManager.TriggerModuleReplaced(oldBody, null);

        // 4. 기존 body 제거
        m_moduleBodys.Remove(oldBody);
        DestroyImmediate(oldBody.gameObject);

        // 5. 새 body 생성 (저장된 모듈 재배치)
        ModuleBody newBody = InitSpaceShipBody(newBodyInfo, savedModules);

        // 6. 새 body에 이전 HP 비율 적용
        if (newBody != null)
        {
            newBody.m_health = newBody.m_healthMax * healthRatio;
            EventManager.TriggerModuleReplaced(null, newBody);
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
