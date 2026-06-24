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
    [SerializeField] protected ModuleBase m_targetModule;
    [SerializeField] protected ModuleHanger m_moduleHanger;
    [SerializeField] protected AircraftInfo m_aircraftInfo;

    // Body 교체 시 새 hanger를 찾기 위한 정보
    [SerializeField] protected SpaceShip m_carrierShip;
    [SerializeField] protected EModuleType m_hangerModuleType;
    [SerializeField] protected EModuleSubType m_hangerModuleSubType;
    [SerializeField] protected int m_hangerSlotIndex;
    protected bool m_isEnemyAircraft = false; // 초기화 시 캐싱, 모함 소멸 후 null이 돼도 판별 가능
    

    [SerializeField] protected float m_repositionMinDistanceMultiplier = 1.5f;
    [SerializeField] protected float m_repositionMaxDistanceMultiplier = 2.5f;

    [SerializeField] protected float m_lastAttackTime;

    [SerializeField] protected EAircraftState m_state = EAircraftState.None;
    [SerializeField] protected Vector3 m_randomOffset;
    [SerializeField] protected Coroutine m_lifeCycleCoroutine;

    [SerializeField] protected Vector3 m_currentDirection;         // ★ 현재 진행 방향 (normalized, velocity처럼 사용)

    [SerializeField] protected HangerFlightPath m_flightPath;     // 함체 HangerSlot에 정의된 사출/귀환 경로

    [SerializeField] protected Transform[] m_firePointBeamList;
    [SerializeField] protected Transform[] m_firePointMissileList;

    protected ModuleData m_moduleData;
    protected EPoolName m_missilePoolName = EPoolName.PROJECTILE_MISSILE_SMALL;

    public virtual void InitializeAirCraft(Transform firePointTransform, ModuleBase target, AircraftInfo aircraftInfo, ModuleHanger moduleHanger, Color color)
    {
        m_firePoint = firePointTransform;
        m_targetModule = target;
        m_aircraftInfo = aircraftInfo;
        m_moduleHanger = moduleHanger;
        m_flightPath = ResolveFlightPath();
        // 미사일 발사 전용 — projectileSpeed만 사용 (함재기 속도 * 2)
        m_moduleData = new ModuleData { projectileSpeed = aircraftInfo.airSpeed * 2f };

        //m_aircraftInfo.attackPower = 0f; // test
        //m_aircraftInfo.moveSpeed = 100f; // test

        // Body 교체 시 새 hanger를 찾기 위한 정보 저장
        if (moduleHanger != null)
        {
            m_carrierShip = moduleHanger.GetSpaceShip();
            if (moduleHanger.m_moduleSlot != null)
            {
                m_hangerSlotIndex = moduleHanger.m_moduleSlot.m_moduleSlotInfo.slotIndex;
                m_hangerModuleType = moduleHanger.m_moduleSlot.m_moduleSlotInfo.moduleType;
            }
        }

        m_isEnemyAircraft = m_carrierShip != null && m_carrierShip.m_ownerFleet != null && m_carrierShip.m_ownerFleet.IsEnemy;

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
        if (m_isEnemyAircraft == false) return false;
        return m_carrierShip == null || m_carrierShip.m_ownerFleet == null;
    }

    protected virtual IEnumerator AircraftLifeCycle()
    {
        while (m_aircraftInfo.airHealth > 0)
        {

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

            SmoothRotateAndMove(toWp, 1f, CalculateAvoidance(), 1f);
            yield return null;
        }

        m_state = EAircraftState.MoveToTarget;
    }

    protected virtual IEnumerator Phase_MoveToTarget()
    {
        Vector3 attackApproachPoint = Vector3.zero;
        if (m_targetModule != null)
        {
            SpaceShip targetShip = m_targetModule.GetSpaceShip();
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

            Vector3 toTarget = (attackApproachPoint - transform.position).normalized;
            float dotValue = Vector3.Dot(transform.forward, toTarget);
            if(dotValue < 0.0f)
            {
                m_state = EAircraftState.AttackShip;
                yield break;
            }

            SmoothRotateAndMove(toTarget, 1f, CalculateAvoidance(), 1f);

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


    protected virtual IEnumerator Phase_Dogfight()
    {
        AircraftBase currentDogfightTarget = DetectEnemyAircraft();
        if (currentDogfightTarget == null) {
            m_state = EAircraftState.MoveToTarget;
            yield break;
        }

        m_currentDirection = transform.forward.normalized;
        while (true)
        {
            if (IsCarrierFleetDestroyed() == true) { ReturnToPool(); yield break; }
            if (currentDogfightTarget == null || currentDogfightTarget.m_aircraftInfo.airHealth <= 0)
            {
                m_state = EAircraftState.MoveToTarget;
                yield break;
            }

            Vector3 moveDir = (currentDogfightTarget.transform.position - transform.position).normalized;
            SmoothRotateAndMove(moveDir, 1f, Vector3.zero, 1f);

            float distance = Vector3.Distance(transform.position, currentDogfightTarget.transform.position);
            if (distance <= m_aircraftInfo.airAttackRange && Time.time >= m_lastAttackTime + m_aircraftInfo.airAttackCool)
            {
                currentDogfightTarget.TakeDamage(m_aircraftInfo.airAttack);
                m_lastAttackTime = Time.time;
            }

            yield return null;
        }
    }

    // 이동 속도(airSpeed)와 회전 각속도를 분리 — 스케일 변경 후 airSpeed가 작아져도 회전은 독립적으로 조정
    protected float m_angularSpeedMultiplier = 20f;
    // 방향 회전 + 이동 공통 처리 — avoidance 전달 시 방향 블렌딩, m_currentDirection 갱신 후 이동
    private void SmoothRotateAndMove(Vector3 targetDirection, float angularMultiplier, Vector3 avoidance, float speedMultiplier)
    {
        if (avoidance.sqrMagnitude > 0.001f)
            targetDirection = (targetDirection + avoidance).normalized;
        if (targetDirection.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, m_aircraftInfo.airSpeed * m_angularSpeedMultiplier * angularMultiplier * Time.deltaTime);

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
        SpaceShip targetShip = m_targetModule.GetSpaceShip();
        if (targetShip == null) { m_state = EAircraftState.ReturnToApproach; yield break; }
        
        ShieldGrid targetShieldGrid = targetShip.m_shieldGrid;
        if (targetShieldGrid == null || targetShieldGrid.m_vertices == null || targetShieldGrid.m_vertices.Count == 0)
        {
            Debug.LogWarning("No ShieldGrid vertices on target ship!");
            yield break;
        }

        List<ShieldVertex> points = targetShieldGrid.m_vertices;
        // 시작 시, 가장 가까운 포인트 찾기
        int currentIndex = FindClosestOutlineIndex(points, transform.position);
        m_currentDirection = transform.forward.normalized;

        while (true)
        {
            if (IsCarrierFleetDestroyed() == true) { ReturnToPool(); yield break; }
            // 종료 조건: 탄약 소진 시 무조건 귀환
            if (m_aircraftInfo.airAmmo <= 0)
            {
                m_state = EAircraftState.ReturnToApproach;
                yield break;
            }
            // 목표 상실 시 모함 타겟으로 재할당 시도
            if (m_targetModule == null || !m_targetModule.gameObject.activeSelf)
            {
                if (TryReassignTarget())
                {
                    m_state = EAircraftState.MoveToTarget;
                    yield break;
                }
                m_state = EAircraftState.ReturnToApproach;
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

            SmoothRotateAndMove(toTarget, 1f, Vector3.zero, 0.5f);

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
        if (m_firePoint == null || m_moduleHanger == null || m_moduleHanger == null)
        {
            if (TryResolveHangerRefs() == false) { ReturnToPool(); yield break; }
        }

        m_currentDirection = transform.forward.normalized;

        Transform returnContainer = m_flightPath != null ? m_flightPath.ReturnPath : null;
        if (returnContainer == null || returnContainer.childCount == 0)
        {
            Debug.LogError("Phase_ReturnToApproach: returnContainer null or empty!");
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
            yield return null;

            if (IsCarrierFleetDestroyed() == true) { ReturnToPool(); yield break; }
            if (m_firePoint == null || m_moduleHanger == null || m_moduleHanger == null || waypoints[currentIndex] == null)
            {
                if (TryResolveHangerRefs() == false) { ReturnToPool(); yield break; }
                m_state = EAircraftState.ReturnToApproach;
                yield break;
            }

            Vector3 toWp = (waypoints[currentIndex].position - transform.position).normalized;
            // 첫 WP는 진입 각도 불확실 → 거리 기준, 이후 WP는 dot 기준
            bool wpReached = currentIndex == 0
                ? Vector3.Distance(transform.position, waypoints[0].position) < 1f
                : Vector3.Dot(transform.forward, toWp) < 0f;
            if (wpReached)
            {
                //Debug.Log($"{gameObject.name} wp currentIndex:{currentIndex} done");
                currentIndex++;
                continue;
            }

            // WP 진행에 따라 1.0 → k_returnMinSpeed 로 선형 감속
            float t = waypoints.Count > 1 ? (float)currentIndex / (waypoints.Count - 1) : 1f;
            float speedMult = Mathf.Lerp(1f, k_returnMinSpeed, t);
            SmoothRotateAndMove(toWp, 2f, Vector3.zero, speedMult);
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
            if (m_firePoint == null || m_moduleHanger == null || m_moduleHanger == null)
            {
                if (TryResolveHangerRefs() == false) { ReturnToPool(); yield break; }
                continue;
            }

            // 격납고 진입 방향(-firePoint.forward) 기준으로 firePoint를 지나쳤으면 종료
            Vector3 toDock = (m_firePoint.position - transform.position).normalized;
            if (Vector3.Dot(-m_firePoint.forward, toDock) < 0f)
            {
                if (m_moduleHanger != null)
                    m_moduleHanger.ReturnAircraft(m_aircraftInfo);
                ReturnToPool();
                yield break;
            }

            SmoothRotateAndMove(toDock, 2f, Vector3.zero, k_returnMinSpeed);
        }
    }

    // m_moduleHanger(ModuleHanger)의 슬롯(함체)에서 HangerFlightPath를 탐색해 캐시
    private HangerFlightPath ResolveFlightPath()
    {
        if (m_moduleHanger == null || m_moduleHanger.m_moduleSlot == null) return null;
        return m_moduleHanger.m_moduleSlot.GetComponentInChildren<HangerFlightPath>();
    }

    // 함체 교체 이벤트 수신 — 내 모함이면 flightPath 갱신
    private void OnShipBodyChanged(SpaceShip ship)
    {
        if (ship != m_carrierShip) return;
        TryResolveShipBodyRefs();
    }

    // 함체 교체 후 flightPath 갱신 (hanger는 살아있음)
    private bool TryResolveShipBodyRefs()
    {
        m_flightPath = ResolveFlightPath();
        return m_flightPath != null;
    }

    // 격납고 교체 후 hanger, firePoint 갱신 (flightPath는 살아있음)
    private bool TryResolveHangerRefs()
    {
        if (m_carrierShip == null) return false;

        foreach (var body in m_carrierShip.m_moduleBodys)
        {
            ModuleSlot newSlot = body.FindModuleSlot(m_hangerModuleType, m_hangerSlotIndex);
            if (newSlot == null || newSlot.transform.childCount == 0) continue;

            ModuleHanger newHanger = newSlot.GetComponentInChildren<ModuleHanger>();
            if (newHanger == null) continue;

            LauncherAircraft launcher = newHanger.GetComponentInChildren<LauncherAircraft>();
            if (launcher == null) continue;

            m_moduleHanger = newHanger;
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
            if (otherAircraft != null && otherAircraft.m_moduleHanger == m_moduleHanger)
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
            AircraftBase otherAircraft = col.GetComponent<AircraftBase>();
            if (otherAircraft != null && otherAircraft.m_moduleHanger != m_moduleHanger && otherAircraft.m_aircraftInfo.airHealth > 0)
                return otherAircraft;
        }

        return null;
    }

    protected virtual void PerformAttack()
    {
        if (m_targetModule == null || m_moduleData == null) return;

        ProjectileMissile missile = ObjectManager.Instance.m_poolManager.Get<ProjectileMissile>(m_missilePoolName);
        if (missile == null) return;

        missile.transform.SetPositionAndRotation(m_firePointMissileList[0].position, m_firePointMissileList[0].rotation);
        missile.SetPoolName(m_missilePoolName);
        missile.InitializeProjectileMissile(m_firePointMissileList[0], m_targetModule, m_aircraftInfo.airAttack, m_moduleData, m_moduleHanger, -m_firePointMissileList[0].up, 1f);

        m_aircraftInfo.airAmmo--;
        m_lastAttackTime = Time.time;
    }
    public virtual void TakeDamage(float damage)
    {
        m_aircraftInfo.airHealth -= damage;
        if (m_aircraftInfo.airHealth <= 0)
        {
            m_aircraftInfo.airHealth = 0;
            ReturnToPool();
        }
    }

    // 목표 상실 시 타겟 재할당. 1순위: 모함 타겟, 2순위: 적 함대에서 직접 탐색
    private bool TryReassignTarget()
    {
        if (m_aircraftInfo.airAmmo <= 0) return false;

        // 1순위: 모함의 현재 타겟
        if (m_moduleHanger != null)
        {
            ModuleBody carrierTarget = m_moduleHanger.GetCurrentTarget();
            if (carrierTarget != null)
            {
                m_targetModule = carrierTarget;
                return true;
            }
        }

        // 2순위: 적 함대에서 직접 탐색 (모함 타겟이 아직 갱신 안 됐을 때)
        if (m_carrierShip == null || m_carrierShip.m_ownerFleet == null) return false;

        // 적 함대에서 살아있는 모듈 직접 탐색
        SpaceFleet enemyFleet = null;
        if (m_carrierShip.m_ownerFleet.IsEnemy)
        {
            enemyFleet = ObjectManager.Instance.m_myFleet;
        }
        else
        {
            List<SpaceFleet> enemyFleets = ObjectManager.Instance.m_enemyFleets;
            for (int i = 0; i < enemyFleets.Count; i++)
            {
                if (enemyFleets[i] != null && enemyFleets[i].IsFleetAlive())
                {
                    enemyFleet = enemyFleets[i];
                    break;
                }
            }
        }

        if (enemyFleet == null) return false;

        ModuleBody enemyBody = enemyFleet.GetRandomAliveBodyPart();
        if (enemyBody == null) return false;

        m_targetModule = enemyBody;
        return true;
    }

    // 강제로 귀환 상태로 전환 (안전지역 진입 시 호출)
    public void ForceReturnToCarrier()
    {
        if (m_state == EAircraftState.None) return;
        m_state = EAircraftState.ReturnToApproach;
    }

    protected virtual void ReturnToPool()
    {
        EventManager.Unsubscribe_ShipBodyChanged(OnShipBodyChanged);
    }

    public virtual void Start() { }



#if UNITY_EDITOR
    [HideInInspector] public bool bShowGizmos = true;
    private void OnDrawGizmos()
    {
        if (bShowGizmos == false) return;
        if (m_targetModule == null)
            return;

        if(m_state == EAircraftState.MoveToTarget)
            DrawDonut(m_targetModule.transform, m_aircraftInfo.airAttackRange);
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
