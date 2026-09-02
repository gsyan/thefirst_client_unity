//------------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EAircraftState
{
    None,
    LaunchStraight,
    MoveToTarget,
    Dogfight,
    AttackShip,
    //Reposition,
    ReturnToApproach,
    Docking
}

public abstract class AircraftBase : MonoBehaviour
{
    [SerializeField] protected Transform m_firePoint;
    [SerializeField] protected Transform m_targetModule;
    [SerializeField] protected ModuleHangar m_moduleHangar;
    [SerializeField] protected AircraftInfo m_aircraftInfo;

    // Body 교체 시 새 hangar를 찾기 위한 정보
    [SerializeField] protected SpaceShip m_carrierShip;
    [SerializeField] protected EModuleType m_hangarModuleType;
    [SerializeField] protected EModuleSubType m_hangarModuleSubType;
    [SerializeField] protected int m_hangarSlotIndex;
    public ETeam m_team = ETeam.TeamA; // 초기화 시 캐싱, 모함 소멸 후 null이 돼도 판별 가능
    public DogfightSphere m_dogfightSphere = null;

    private EngineFlameColorizer[] m_engineFlames;

    public void LeaveDogfightSphere()
    {
        if (m_dogfightSphere == null) return;
        m_dogfightSphere.LeaveDogFightSphere(this);
        m_dogfightSphere = null;
    }

    private SpaceShip m_harassedShip = null;  // 현재 교란 중인 함선 (AttackShip 페이즈 전용)
    private float m_harassDelay = 0f;

    private void RegisterHarass(SpaceShip ship)
    {
        m_harassedShip = ship;
        m_harassDelay  = m_aircraftInfo != null ? m_aircraftInfo.airDisrupt : 0f;
        ship.RegisterHarassingAircraft(this, m_harassDelay);
    }

    private void UnregisterHarass()
    {
        if (m_harassedShip == null) return;
        m_harassedShip.UnregisterHarassingAircraft(this, m_harassDelay);
        m_harassedShip = null;
    }
    

    [SerializeField] protected float m_repositionMinDistanceMultiplier = 1.5f;
    [SerializeField] protected float m_repositionMaxDistanceMultiplier = 2.5f;

    [SerializeField] protected float m_lastAttackTime;

    [SerializeField] protected EAircraftState m_state = EAircraftState.None;
    [SerializeField] protected Vector3 m_randomOffset;
    [SerializeField] protected Coroutine m_lifeCycleCoroutine;

    [SerializeField] protected Vector3 m_currentDirection;         // ★ 현재 진행 방향 (normalized, velocity처럼 사용)

    [SerializeField] protected HangarFlightPath m_flightPath;     // 함체 HangarSlot에 정의된 사출/귀환 경로

    [SerializeField] protected Transform[] m_firePointBeamList;
    [SerializeField] protected Transform[] m_firePointMissileList;

    protected ModuleData m_moduleData;
    protected EPoolName m_missilePoolName = EPoolName.PROJECTILE_MISSILE_SMALL;

    public virtual void InitializeAirCraft(Transform firePointTransform, Transform target, AircraftInfo aircraftInfo, ModuleHangar moduleHangar, Color color)
    {
        m_firePoint = firePointTransform;
        m_targetModule = target;
        m_aircraftInfo = aircraftInfo;
        m_moduleHangar = moduleHangar;
        m_flightPath = ResolveFlightPath();
        // 미사일 발사 전용 — speed(발사체 이동속도로 재사용), airDisrupt만 사용
        m_moduleData = new ModuleData { speed = aircraftInfo.airSpeed * 2f, airDisrupt = aircraftInfo.airDisrupt };

        //m_aircraftInfo.attackPower = 0f; // test
        //m_aircraftInfo.moveSpeed = 100f; // test

        // Body 교체 시 새 hangar를 찾기 위한 정보 저장
        if (moduleHangar != null)
        {
            m_carrierShip = moduleHangar.GetSpaceShip();
            if (moduleHangar.m_moduleSlot != null)
            {
                m_hangarSlotIndex = moduleHangar.m_moduleSlot.m_moduleSlotInfo.slotIndex;
                m_hangarModuleType = moduleHangar.m_moduleSlot.m_moduleSlotInfo.moduleType;
            }
        }

        m_team = m_carrierShip != null && m_carrierShip.m_ownerFleet != null ? m_carrierShip.m_ownerFleet.m_team : ETeam.TeamA;

        if (m_engineFlames == null)
            m_engineFlames = GetComponentsInChildren<EngineFlameColorizer>(true);
        bool isEnemyColor = ObjectManager.Instance.IsEnemyOfMyTeam(m_carrierShip != null ? m_carrierShip.m_ownerFleet : null);
        for (int i = 0; i < m_engineFlames.Length; i++)
            m_engineFlames[i].SetEnemyColor(isEnemyColor);

        EventManager.Subscribe_ShipBodyChanged(OnShipBodyChanged);

        m_lastAttackTime = 0f;

        m_randomOffset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.2f, 0.2f), Random.Range(-0.5f, 0.5f));
        m_state = EAircraftState.LaunchStraight;

        if (m_lifeCycleCoroutine != null)
            StopCoroutine(m_lifeCycleCoroutine);
        m_lifeCycleCoroutine = StartCoroutine(AircraftLifeCycle());

    }

    // 적 함재기이고 모함 참조가 null이면 함대 소멸로 간주
    private bool IsCarrierFleetDestroyed()
    {
        return m_carrierShip == null || m_carrierShip.m_ownerFleet == null;
    }

    private const float k_maxDistFromCarrier = 100f;

    protected virtual IEnumerator AircraftLifeCycle()
    {
        while (m_aircraftInfo.airHealth > 0)
        {
            // 모함과 거리가 너무 멀면 강제 귀환
            if (m_carrierShip != null)
            {
                float distFromCarrier = Vector3.Distance(transform.position, m_carrierShip.transform.position);
                if (distFromCarrier > k_maxDistFromCarrier)
                {
                    ReturnToPool();
                    yield break;
                }
            }

            //DebugOverlay.Instance.SetText($"m_state: {m_state}");

            switch (m_state)
            {
                case EAircraftState.LaunchStraight:
                    yield return Phase_LaunchStraight();
                    break;
                case EAircraftState.MoveToTarget:
                    yield return Phase_MoveToTarget();
                    break;
                case EAircraftState.Dogfight:
                    yield return Phase_Dogfight();
                    break;
                case EAircraftState.AttackShip:
                    yield return Phase_AttackShip();
                    break;
                case EAircraftState.ReturnToApproach:
                    yield return Phase_ReturnToApproach();
                    break;
                case EAircraftState.Docking:
                    yield return Phase_Docking();
                    break;
            }
            yield return null;
        }

        ReturnToPool();
    }

    protected virtual IEnumerator Phase_LaunchStraight()
    {
        Transform launchContainer = m_flightPath != null ? m_flightPath.LaunchPath : null;
        if (launchContainer == null || launchContainer.childCount == 0)
        {
            Debug.LogError("Phase_LaunchStraight: launchContainer null or empty!");
            yield break;
        }

        // WP 리스트 구성, 가장 가까운 WP에서 시작 (AttackShip의 FindClosestOutlineIndex 방식)
        List<Transform> waypoints = new();
        for (int i = 0; i < launchContainer.childCount; i++)
            waypoints.Add(launchContainer.GetChild(i));

        int currentIndex = 0; // 0번 자식이 가장 가까운 것
        m_currentDirection = transform.forward.normalized;
        while (currentIndex < waypoints.Count)
        {
            if (IsCarrierFleetDestroyed() == true) { ReturnToPool(); yield break; }
            if (waypoints[currentIndex] == null) { ReturnToPool(); yield break; }

            Vector3 toWp = (waypoints[currentIndex].position - transform.position).normalized;
            if (Vector3.Dot(transform.forward, toWp) < 0f)
            {
                currentIndex++;
                continue;
            }

            SmoothRotateAndMove(toWp, angularMultiplier: 20f, avoidance: CalculateAvoidance(), speedMultiplier: 1f);
            yield return null;
        }

        m_state = EAircraftState.MoveToTarget;
    }

    protected virtual IEnumerator Phase_MoveToTarget()
    {
        Vector3 attackApproachPoint = Vector3.zero;
        if (m_targetModule != null)
        {
            SpaceShip targetShip = m_targetModule.GetComponentInParent<SpaceShip>();
            attackApproachPoint = GetLateralShieldVertex(targetShip);
        }

        m_currentDirection = transform.forward.normalized;
        while (true)
        {
            if (IsCarrierFleetDestroyed() == true) { ReturnToPool(); yield break; }
            AircraftBase enemyAircraft = DetectEnemyAircraft();
            if (enemyAircraft != null)
            {
                m_state = EAircraftState.Dogfight;
                yield break;
            }

            if (m_targetModule == null || m_targetModule.gameObject.activeSelf == false)
            {
                // 탄약이 남아있으면 모함 타겟으로 재할당 시도
                if (TryReassignTarget())
                {
                    m_state = EAircraftState.MoveToTarget;
                    yield break;
                }
                m_state = EAircraftState.ReturnToApproach;
                yield break;
            }

            Vector3 toTarget       = (attackApproachPoint - transform.position).normalized;
            float distToApproach   = Vector3.Distance(transform.position, attackApproachPoint);
            float dotValue         = Vector3.Dot(transform.forward, toTarget);
            if (dotValue < 0.0f && distToApproach < 3f)
            {
                m_state = EAircraftState.AttackShip;
                yield break;
            }

            SmoothRotateAndMove(toTarget, angularMultiplier: 20f, avoidance: CalculateAvoidance(), speedMultiplier: 1f);

            yield return null;
        }
    }
    // 함재기 접근 방향 기준 측면 꼭지점 반환 — 충돌 없이 자연스럽게 그리드 진입
    Vector3 GetLateralShieldVertex(SpaceShip targetShip)
    {
        ShieldGrid grid = targetShip != null ? targetShip.m_shieldGrid : null;
        if (grid == null || grid.m_vertices == null || grid.m_vertices.Count == 0)
        {
            Debug.LogError("GetLateralShieldVertex: ShieldGrid 없음!");
            return transform.position;
        }

        // 함재기 → 함선 접근 방향
        Vector3 approachDir = (targetShip.transform.position - transform.position).normalized;

        // |Dot(approachDir, vertexInward)| < 0.4 → 측면 꼭지점만 추림
        List<ShieldVertex> candidates = new();
        for (int i = 0; i < grid.m_vertices.Count; i++)
        {
            Vector3 vertexInward = (targetShip.transform.position - grid.m_vertices[i].GetPosition()).normalized;
            if (Mathf.Abs(Vector3.Dot(approachDir, vertexInward)) < 0.4f)
                candidates.Add(grid.m_vertices[i]);
        }

        if (candidates.Count == 0)
        {
            Debug.LogError("GetLateralShieldVertex: 측면 꼭지점 없음!");
            return transform.position;
        }

        return candidates[Random.Range(0, candidates.Count)].GetPosition();
    }


    private const float k_dogfightSphereRadius = 2.5f;

    protected virtual IEnumerator Phase_Dogfight()
    {
        AircraftBase currentDogfightTarget = DetectEnemyAircraft();
        if (currentDogfightTarget == null)
        {
            m_state = EAircraftState.MoveToTarget;
            yield break;
        }

        // 스피어 설정 — 상대가 이미 스피어를 가지면 합류, 없으면 새로 생성
        if (currentDogfightTarget.m_dogfightSphere != null)
        {
            m_dogfightSphere = currentDogfightTarget.m_dogfightSphere;
        }
        else
        {
            Vector3 center = (transform.position + currentDogfightTarget.transform.position) * 0.5f;
            m_dogfightSphere = DogfightSphere.Get(center, k_dogfightSphereRadius);
        }
        m_dogfightSphere.Join(this);

        while (true)
        {
            if (IsCarrierFleetDestroyed() == true) { ReturnToPool(); yield break; }
            if (currentDogfightTarget.m_aircraftInfo.airHealth <= 0)
            {
                LeaveDogfightSphere();
                m_state = EAircraftState.MoveToTarget;
                yield break;
            }

            Vector3 targetDir = (currentDogfightTarget.transform.position - transform.position).normalized;

            // 구체 경계를 벗어났을 때만 안쪽 방향으로 약하게 유도
            float distFromCenter = Vector3.Distance(transform.position, m_dogfightSphere.center);
            if (distFromCenter > m_dogfightSphere.radius)
            {
                Vector3 inwardDir = (m_dogfightSphere.center - transform.position).normalized;
                float   overRatio = Mathf.Clamp01((distFromCenter - m_dogfightSphere.radius) / m_dogfightSphere.radius);
                targetDir = Vector3.Lerp(targetDir, inwardDir, overRatio * 2f).normalized;
            }

            SmoothRotateAndMove(targetDir, angularMultiplier: 20f, avoidance: Vector3.zero, speedMultiplier: 1f);

            // 공격 범위 안이면 공격
            float distance = Vector3.Distance(transform.position, currentDogfightTarget.transform.position);
            if (distance <= m_aircraftInfo.airAttackRange && Time.time >= m_lastAttackTime + m_aircraftInfo.airAttackCool)
            {
                currentDogfightTarget.TakeDamage(m_aircraftInfo.airAttack);
                m_lastAttackTime = Time.time;
            }

            yield return null;
        }
    }

    // 방향 회전 + 이동 공통 처리 — avoidance 전달 시 방향 블렌딩, m_currentDirection 갱신 후 이동
    private void SmoothRotateAndMove(Vector3 targetDirection, float angularMultiplier, Vector3 avoidance, float speedMultiplier)
    {
        if (avoidance.sqrMagnitude > 0.001f)
            targetDirection = (targetDirection + avoidance).normalized;
        if (targetDirection.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, m_aircraftInfo.airSpeed * angularMultiplier * Time.deltaTime);

        m_currentDirection = transform.forward;
        transform.position += m_currentDirection * (m_aircraftInfo.airSpeed * speedMultiplier * Time.deltaTime);
    }

    // biasAxis 방향으로 편향하여 회전 (격납고 바깥쪽 우회 경로용)
    protected virtual IEnumerator Phase_AttackShip()
    {
        if (m_targetModule == null)
        {
            if (TryReassignTarget()) { m_state = EAircraftState.MoveToTarget; yield break; }
            m_state = EAircraftState.ReturnToApproach; yield break;
        }
        SpaceShip targetShip = m_targetModule.GetComponentInParent<SpaceShip>();
        if (targetShip == null) { m_state = EAircraftState.ReturnToApproach; yield break; }

        ShieldGrid targetShieldGrid = targetShip.m_shieldGrid;
        if (targetShieldGrid == null || targetShieldGrid.m_vertices == null || targetShieldGrid.m_vertices.Count == 0)
        {
            Debug.LogWarning("No ShieldGrid vertices on target ship!");
            m_state = EAircraftState.ReturnToApproach; yield break;
        }

        RegisterHarass(targetShip);

        List<ShieldVertex> points = targetShieldGrid.m_vertices;
        // 시작 시, 가장 가까운 포인트 찾기
        int currentIndex = FindClosestOutlineIndex(points, transform.position);
        m_currentDirection = transform.forward.normalized;

        while (true)
        {
            if (IsCarrierFleetDestroyed() == true) { ReturnToPool(); yield break; }
            // 종료 조건
            // 1) 탄약 소진 시 무조건 귀환
            if (m_aircraftInfo.airAmmo <= 0)
            {
                UnregisterHarass();
                m_state = EAircraftState.ReturnToApproach;
                yield break;
            }
            // 2) 목표 상실 시 모함 타겟으로 재할당 시도
            if (m_targetModule == null)
            {
                UnregisterHarass();
                if (TryReassignTarget() == true)
                {
                    m_state = EAircraftState.MoveToTarget;
                    yield break;
                }
                m_state = EAircraftState.ReturnToApproach;
                yield break;
            }
            // 3) 도그 파이팅
            AircraftBase enemyAircraft = DetectEnemyAircraft();
            if (enemyAircraft != null)
            {
                UnregisterHarass();
                m_state = EAircraftState.Dogfight;
                yield break;
            }

            Vector3 currentPoint = points[currentIndex].transform.position;

            Vector3 toTarget = (currentPoint - transform.position).normalized;
            float dotValue = Vector3.Dot(transform.forward, toTarget);
            // 포인트를 거의 지나쳤으면 다음 포인트 선택
            if(dotValue < 0.0f)
            {
                currentIndex = GetNextIndexByAlignment(points, currentIndex, m_currentDirection);
            }

            SmoothRotateAndMove(toTarget, angularMultiplier: 20f, avoidance: Vector3.zero, speedMultiplier: 1f);

            // 공격 처리
            if (Time.time >= m_lastAttackTime + m_aircraftInfo.airAttackCool)
                PerformAttack();

            yield return null;
        }
    }
    int FindClosestOutlineIndex(List<ShieldVertex> points, Vector3 pos)
    {
        int bestIndex = 0;
        float bestDist = float.MaxValue;

        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] == null) continue;
            float d = (points[i].transform.position - pos).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    int GetNextIndexByAlignment(List<ShieldVertex> points, int current, Vector3 forward)
    {
        float savedDot = -1f;
        int savedNeighbor = current;

        foreach (int neighborIdx in points[current].neighborIndices)
        {
            if (neighborIdx == current || neighborIdx >= points.Count) continue;
            var neighbor = points[neighborIdx];
            if (neighbor == null) continue;

            float tempDot = Vector3.Dot(forward, neighbor.transform.position - points[current].transform.position);
            if (tempDot > savedDot)
            {
                savedDot = tempDot;
                savedNeighbor = neighborIdx;
            }
        }

        return savedNeighbor;
    }

    const float k_returnMinSpeed = 0.5f;
    // 격납고 출입구 앞 접근 지점까지 ReturnPath WP를 따라 비행, 완료 시 Docking으로 전환
    protected virtual IEnumerator Phase_ReturnToApproach()
    {
        if (m_firePoint == null || m_moduleHangar == null)
        {
            if (TryResolveHangarRefs() == false) { ReturnToPool(); yield break; }
        }

        m_currentDirection = transform.forward.normalized;

        Transform returnContainer = m_flightPath != null ? m_flightPath.ReturnPath : null;
        if (returnContainer == null || returnContainer.childCount == 0)
        {
            ReturnToPool();
            yield break;
        }

        // WP 리스트 구성
        List<Transform> waypoints = new();
        for (int i = 0; i < returnContainer.childCount; i++)
            waypoints.Add(returnContainer.GetChild(i));

        int currentIndex = 0;
        while (currentIndex < waypoints.Count)
        {
            if (IsCarrierFleetDestroyed() == true) { ReturnToPool(); yield break; }
            // 종료 조건
            // 1) 복귀시 필요한 정보 없으면, 다른 격납고 찾기
            if (m_firePoint == null || m_moduleHangar == null || waypoints[currentIndex] == null)
            {
                if (TryResolveHangarRefs() == false) { ReturnToPool(); yield break; }
                m_state = EAircraftState.ReturnToApproach;
                yield break;
            }
            // 2) 도그 파이팅
            AircraftBase enemyAircraft = DetectEnemyAircraft();
            if (enemyAircraft != null)
            {
                m_state = EAircraftState.Dogfight;
                yield break;
            }

            Vector3 toWp = (waypoints[currentIndex].position - transform.position).normalized;
            // 첫 WP는 진입 각도 불확실 → 거리 기준, 이후 WP는 dot 기준
            bool wpReached = currentIndex == 0
                ? Vector3.Distance(transform.position, waypoints[0].position) < 1f
                : Vector3.Dot(transform.forward, toWp) <= 0f;
            if (wpReached)
            {
                //Debug.Log($"{gameObject.name} wp currentIndex:{currentIndex} done");
                currentIndex++;
                continue;
            }

            // WP 진행에 따라 1.0 → k_returnMinSpeed 로 선형 감속
            float t = waypoints.Count > 1 ? (float)currentIndex / (waypoints.Count - 1) : 1f;
            float speedMult = Mathf.Lerp(1f, k_returnMinSpeed, t);
            SmoothRotateAndMove(toWp, angularMultiplier: 40f, avoidance: Vector3.zero, speedMultiplier: speedMult);
            yield return null;
        }

        m_state = EAircraftState.Docking;
        //Debug.Log($"{gameObject.name} m_state:{m_state}");
    }

    // 출입구 방향(-m_firePoint.forward)으로 감속 진입, 완료 시 귀환 처리
    protected virtual IEnumerator Phase_Docking()
    {
        //Debug.Log("Phase_Docking");
        m_currentDirection = transform.forward.normalized;
        while (true)
        {
            yield return null;

            if (IsCarrierFleetDestroyed() == true) { ReturnToPool(); yield break; }
            if (m_firePoint == null || m_moduleHangar == null || m_moduleHangar == null)
            {
                if (TryResolveHangarRefs() == false) { ReturnToPool(); yield break; }
                continue;
            }

            // 격납고 진입 방향(-firePoint.forward) 기준으로 firePoint를 지나쳤으면 종료
            Vector3 toDock = (m_firePoint.position - transform.position).normalized;
            if (Vector3.Dot(-m_firePoint.forward, toDock) <= 0f)
            {
                ReturnToPool();
                yield break;
            }

            SmoothRotateAndMove(toDock, angularMultiplier: 40f, avoidance: Vector3.zero, speedMultiplier: k_returnMinSpeed);
        }
    }

    // m_moduleHangar(ModuleHangar)의 슬롯(함체)에서 HangarFlightPath를 탐색해 캐시
    private HangarFlightPath ResolveFlightPath()
    {
        if (m_moduleHangar == null || m_moduleHangar.m_moduleSlot == null) return null;
        return m_moduleHangar.m_moduleSlot.GetComponentInChildren<HangarFlightPath>();
    }

    // 함체 교체 이벤트 수신 — 내 모함이면 flightPath 갱신
    private void OnShipBodyChanged(SpaceShip ship)
    {
        if (ship != m_carrierShip) return;
        TryResolveShipBodyRefs();
    }

    // 함체 교체 후 flightPath 갱신 (hangar는 살아있음)
    private bool TryResolveShipBodyRefs()
    {
        m_flightPath = ResolveFlightPath();
        return m_flightPath != null;
    }

    // 격납고 교체 후 hangar, firePoint 갱신 (flightPath는 살아있음)
    private bool TryResolveHangarRefs()
    {
        if (m_carrierShip == null) return false;

        foreach (var body in m_carrierShip.m_moduleHulls)
        {
            ModuleSlot newSlot = body.FindModuleSlot(m_hangarModuleType, m_hangarSlotIndex);
            if (newSlot == null || newSlot.transform.childCount == 0) continue;

            ModuleHangar newHangar = newSlot.GetComponentInChildren<ModuleHangar>();
            if (newHangar == null) continue;

            LauncherAircraft launcher = newHangar.GetComponentInChildren<LauncherAircraft>();
            if (launcher == null) continue;

            m_moduleHangar = newHangar;
            m_firePoint = launcher.GetFirePoint();
            return true;
        }

        return false;
    }

    protected Vector3 CalculateAvoidance()
    {
        Vector3 avoidanceDir = Vector3.zero;
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, m_aircraftInfo.airAvoidRadius);

        foreach (Collider col in nearbyObjects)
        {
            if (col.gameObject == gameObject) continue;

            AircraftBase otherAircraft = col.GetComponent<AircraftBase>();
            if (otherAircraft != null && otherAircraft.m_moduleHangar == m_moduleHangar)
            {
                Vector3 awayDir = transform.position - col.transform.position;
                float distance = awayDir.magnitude;
                if (distance > 0.01f)
                    avoidanceDir += awayDir.normalized / distance;
            }
        }

        return avoidanceDir.normalized;
    }

    protected AircraftBase DetectEnemyAircraft()
    {
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, m_aircraftInfo.airDetectRadius);

        foreach (Collider col in nearbyObjects)
        {
            AircraftBase otherAircraft = col.GetComponentInParent<AircraftBase>();
            if (otherAircraft != null && otherAircraft.m_team != m_team && otherAircraft.m_aircraftInfo.airHealth > 0)
                return otherAircraft;
        }

        return null;
    }

    protected virtual void PerformAttack()
    {
        if (m_targetModule == null || m_moduleData == null) return;

        ProjectileMissile missile = ObjectManager.Instance.m_poolManager.Get<ProjectileMissile>(m_missilePoolName);
        if (missile == null) return;

        DamageInfo damageInfo = new DamageInfo
        {
            baseDamage       = m_aircraftInfo.airAttack,
            attackMultiplier = m_aircraftInfo.airAttackMultiplier,
            damageType       = EDamageType.Aircraft,
        };

        missile.transform.SetPositionAndRotation(m_firePointMissileList[0].position, m_firePointMissileList[0].rotation);
        missile.SetPoolName(m_missilePoolName);
        missile.InitializeProjectileMissile(m_firePointMissileList[0], m_targetModule, damageInfo, m_moduleData, m_moduleHangar, -m_firePointMissileList[0].up, 1f); // m_targetModule은 Transform

        m_aircraftInfo.airAmmo--;
        m_lastAttackTime = Time.time;
    }
    public virtual void TakeDamage(float damage)
    {
        // 전투 종료 처리 중 데미지 차단
        if (ObjectManager.Instance.m_isBattleEnding == true) return;
        m_aircraftInfo.airHealth -= damage;
        if (m_aircraftInfo.airHealth <= 0)
        {
            m_aircraftInfo.airHealth = 0;
            EffectBase explosion = ObjectManager.Instance.m_poolManager.Get<EffectBase>(EPoolName.EFFECT_EXPLOSION_MISSILE_SMALL);
            if (explosion != null)
            {
                explosion.transform.position = transform.position;
                explosion.PlayEffect();
            }
            SoundManager.Instance.PlayFX(EFx.Explosion_Aircraft_Missile, transform.position);
            ReturnToPool();
        }
    }

    // 목표 상실 시 타겟 재할당. 1순위: 모함 타겟, 2순위: 적 함대에서 직접 탐색
    private bool TryReassignTarget()
    {
        if (m_aircraftInfo.airAmmo <= 0) return false;

        // 1순위: 모함의 현재 타겟
        if (m_moduleHangar != null)
        {
            ModuleHull carrierTarget = m_moduleHangar.GetCurrentTarget();
            if (carrierTarget != null)
            {
                m_targetModule = carrierTarget.transform;
                return true;
            }
        }

        // 2순위: 적 함대에서 직접 탐색 (모함 타겟이 아직 갱신 안 됐을 때)
        if (m_carrierShip == null || m_carrierShip.m_ownerFleet == null) return false;

        // 적 함대에서 살아있는 모듈 직접 탐색
        SpaceFleet enemyFleet = null;
        List<SpaceFleet> opposingFleets = ObjectManager.Instance.GetOpposingTeamFleets(m_carrierShip.m_ownerFleet.m_team);
        for (int i = 0; i < opposingFleets.Count; i++)
        {
            if (opposingFleets[i] != null && opposingFleets[i].IsValidCombatTarget() == true)
            {
                enemyFleet = opposingFleets[i];
                break;
            }
        }

        if (enemyFleet == null) return false;

        ModuleHull enemyBody = enemyFleet.GetRandomAliveBodyPart();
        if (enemyBody == null) return false;

        m_targetModule = enemyBody.transform;
        return true;
    }

    // 강제로 귀환 상태로 전환 (안전지역 진입 시 호출)
    public void ForceReturnToCarrier()
    {
        if (m_state == EAircraftState.None) return;
        m_state = EAircraftState.ReturnToApproach;
    }

    // 귀환 연출 없이 즉시 풀로 반환 (튜토리얼 정리 등 즉시 제거가 필요할 때 사용)
    public void ForceReturnToPoolImmediate()
    {
        ReturnToPool();
    }

    protected virtual void ReturnToPool()
    {
        if (m_aircraftInfo != null)
        {
            m_aircraftInfo.airHealth      = 0f;
            m_aircraftInfo.lastReturnTime = Time.time;
            m_aircraftInfo.isReady        = false;
        }
        if (m_moduleHangar != null)
            m_moduleHangar.ReturnAircraft(m_aircraftInfo);
        LeaveDogfightSphere();
        UnregisterHarass();
        EventManager.Unsubscribe_ShipBodyChanged(OnShipBodyChanged);
    }

    public virtual void Start() { }



#if UNITY_EDITOR
    [HideInInspector] public bool bShowGizmos = true;
    private void OnDrawGizmos()
    {
        if (bShowGizmos == false) return;

        if (m_state == EAircraftState.MoveToTarget && m_targetModule != null)
            DrawDonut(m_targetModule.transform, m_aircraftInfo.airAttackRange);

        if (m_dogfightSphere != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
            Gizmos.DrawSphere(m_dogfightSphere.center, m_dogfightSphere.radius);
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.8f);
            Gizmos.DrawWireSphere(m_dogfightSphere.center, m_dogfightSphere.radius);
        }
    }

    private void DrawDonut(Transform target, float radius)
    {
        // 1. 지금 이 함재기 → 타겟 방향 기준 로컬 좌표계 생성
        Vector3 forward = (target.position - transform.position).normalized;

        Vector3 worldUp = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(forward, worldUp)) > 0.9f)
            worldUp = Vector3.right;

        Vector3 right = Vector3.Normalize(Vector3.Cross(worldUp, forward));
        Vector3 up = Vector3.Normalize(Vector3.Cross(forward, right));

        // 2. 세그먼트 수
        int segments = 64;

        Vector3 prevPoint = Vector3.zero;

        Gizmos.color = Color.cyan;

        for (int i = 0; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;

            // Local donut offset
            Vector3 localOffset = new Vector3(
                Mathf.Cos(angle),
                Mathf.Sin(angle),
                0f
            ) * radius;

            // Convert to world
            Vector3 worldOffset =
                right * localOffset.x +
                up * localOffset.y +
                forward * localOffset.z;

            Vector3 worldPos = target.position + worldOffset;

            if (i > 0)
                Gizmos.DrawLine(prevPoint, worldPos);

            prevPoint = worldPos;
        }
    }

#endif
}
