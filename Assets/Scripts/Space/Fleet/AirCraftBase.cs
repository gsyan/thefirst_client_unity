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
    Reposition,
    ReturnToCarrier
}

public abstract class AircraftBase : MonoBehaviour
{
    [SerializeField] protected Transform m_firePoint;
    [SerializeField] protected ModuleBase m_targetModule;
    [SerializeField] protected ModuleBase m_sourceModule;
    [SerializeField] protected ModuleHanger m_moduleHanger;
    [SerializeField] protected AircraftInfo m_aircraftInfo;

    // Body 교체 시 새 hanger를 찾기 위한 정보
    [SerializeField] protected SpaceShip m_carrierShip;
    [SerializeField] protected int m_hangerSlotIndex;
    [SerializeField] protected int m_hangerModuleTypePacked;

    [SerializeField] protected float m_repositionMinDistanceMultiplier = 1.5f;
    [SerializeField] protected float m_repositionMaxDistanceMultiplier = 2.5f;

    [SerializeField] protected float m_lastAttackTime;

    [SerializeField] protected EAircraftState m_state = EAircraftState.None;
    [SerializeField] protected Vector3 m_launchStartPos;
    [SerializeField] protected Vector3 m_randomOffset;
    [SerializeField] protected Coroutine m_lifeCycleCoroutine;

    [SerializeField] protected Vector3 m_currentDirection;         // ★ 현재 진행 방향 (normalized, velocity처럼 사용)
    
    public virtual void InitializeAirCraft(Transform firePointTransform, ModuleBase target, AircraftInfo aircraftInfo, ModuleHanger moduleHanger, Color color, ModuleBase sourceModuleBase)
    {
        m_firePoint = firePointTransform;
        m_targetModule = target;
        m_aircraftInfo = aircraftInfo;
        m_moduleHanger = moduleHanger;
        m_sourceModule = sourceModuleBase;

        //m_aircraftInfo.attackPower = 0f; // test
        //m_aircraftInfo.moveSpeed = 100f; // test

        // Body 교체 시 새 hanger를 찾기 위한 정보 저장
        if (moduleHanger != null)
        {
            m_carrierShip = moduleHanger.GetSpaceShip();
            if (moduleHanger.m_moduleSlot != null)
            {
                m_hangerSlotIndex = moduleHanger.m_moduleSlot.m_moduleSlotInfo.slotIndex;
                m_hangerModuleTypePacked = moduleHanger.m_moduleSlot.m_moduleTypePacked;
            }
        }

        m_lastAttackTime = 0f;

        m_launchStartPos = transform.position;
        m_randomOffset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.2f, 0.2f), Random.Range(-0.5f, 0.5f));
        m_state = EAircraftState.LaunchStraight;

        if (m_lifeCycleCoroutine != null)
            StopCoroutine(m_lifeCycleCoroutine);
        m_lifeCycleCoroutine = StartCoroutine(AircraftLifeCycle());

    }

    protected virtual IEnumerator AircraftLifeCycle()
    {
        while (m_aircraftInfo.health > 0)
        {
            //DebugOverlay.Instance.SetText($"m_state: {m_state}");

            switch (m_state)
            {
                case EAircraftState.LaunchStraight:
                    yield return LaunchStraightPhase();
                    break;
                case EAircraftState.MoveToTarget:
                    yield return MoveToTargetPhase();
                    break;
                case EAircraftState.Dogfight:
                    yield return DogfightPhase();
                    break;
                case EAircraftState.AttackShip:
                    yield return AttackShipPhase();
                    //yield return AttackShipPhase_OnlyShpere();
                    break;
                case EAircraftState.Reposition:
                    yield return RepositionPhase();
                    break;
                case EAircraftState.ReturnToCarrier:
                    yield return ReturnToCarrierPhase();
                    break;
            }
            yield return null;
        }

        ReturnToPool();
    }

    protected virtual IEnumerator LaunchStraightPhase()
    {
        Vector3 targetPos = m_launchStartPos + transform.forward * m_aircraftInfo.launchStraightDistance + m_randomOffset;
        while (true)
        {
            Vector3 toTarget = (targetPos - transform.position).normalized;
            float dotValue = Vector3.Dot(transform.forward, toTarget);
            if(dotValue < 0.0f)
                break;
                        
            Vector3 avoidanceDir = CalculateAvoidance();
            Vector3 moveDir = toTarget + avoidanceDir;
            moveDir.Normalize();
            transform.position += moveDir * m_aircraftInfo.moveSpeed * Time.deltaTime;
            if (moveDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, m_aircraftInfo.moveSpeed * Time.deltaTime);
            }

            
            yield return null;
        }

        m_state = EAircraftState.MoveToTarget;
    }

    protected virtual IEnumerator MoveToTargetPhase()
    {
        Vector3 attackApproachPoint = Vector3.zero;
        if (m_targetModule != null)
            attackApproachPoint = GetRelativeVirticalDonutPoint(m_targetModule.transform, m_aircraftInfo.attackRange * 0.8f, m_aircraftInfo.attackRange * 1.2f);

        while (true)
        {
            AircraftBase enemyAircraft = DetectEnemyAircraft();
            if (enemyAircraft != null)
            {
                m_state = EAircraftState.Dogfight;
                yield break;
            }

            if (m_targetModule == null || m_targetModule.gameObject.activeSelf == false)
            {
                m_state = EAircraftState.ReturnToCarrier;
                yield break;
            }

            Vector3 toTarget = (attackApproachPoint - transform.position).normalized;
            float dotValue = Vector3.Dot(transform.forward, toTarget);
            if(dotValue < 0.0f)
            {
                // ★★★ 전환 시 현재 방향 저장 → 부드러운 연결!
                m_currentDirection = transform.forward.normalized;
                m_state = EAircraftState.AttackShip;
                yield break;
            }

            Vector3 targetDir = (attackApproachPoint - transform.position).normalized;
            Vector3 avoidanceDir = CalculateAvoidance();
            Vector3 finalMoveDir = targetDir;

            if (avoidanceDir.sqrMagnitude > 0.01f)
                finalMoveDir = (targetDir + avoidanceDir).normalized;

            transform.position += transform.forward * m_aircraftInfo.moveSpeed * Time.deltaTime;

            Quaternion targetRotation = Quaternion.LookRotation(finalMoveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, m_aircraftInfo.moveSpeed * Time.deltaTime);

            yield return null;
        }
    }
    Vector3 GetRelativeVirticalDonutPoint(Transform target, float minRadius, float maxRadius)
    {
        // 1. 타겟을 향하는 방향
        Vector3 forward = (target.position - transform.position).normalized;

        // 2. forward 와 최대한 가까운 Up 벡터를 선택
        Vector3 worldUp = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(forward, worldUp)) > 0.9f)
            worldUp = Vector3.right;

        // 3. 로컬 좌표계의 right, up 생성
        Vector3 right = Vector3.Normalize(Vector3.Cross(worldUp, forward));
        Vector3 up = Vector3.Normalize(Vector3.Cross(forward, right));

        // 4. 도넛 반경
        float radius = Random.Range(minRadius, maxRadius);

        // 5. 도넛의 각도 (0~360)
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;

        // 6. 로컬 도넛 오프셋 (right-up 평면에서의 원)
        Vector3 localOffset = new Vector3(
            Mathf.Cos(angle),
            Mathf.Sin(angle),
            0f
        ) * radius;

        // 7. 로컬 offset → 월드 변환
        Vector3 worldOffset =
            right   * localOffset.x +
            up      * localOffset.y +
            forward * localOffset.z;

        // 8. 최종 도넛 목표 지점
        return target.position + worldOffset;
    }


    protected virtual IEnumerator DogfightPhase()
    {
        AircraftBase currentDogfightTarget = DetectEnemyAircraft();
        if (currentDogfightTarget == null) {
            m_state = EAircraftState.MoveToTarget;
            yield break;
        }

        while (true)
        {
            if (currentDogfightTarget == null || currentDogfightTarget.m_aircraftInfo.health <= 0)
            {
                m_state = EAircraftState.MoveToTarget;
                yield break;
            }

            Vector3 moveDir = (currentDogfightTarget.transform.position - transform.position).normalized;

            transform.position += moveDir * m_aircraftInfo.moveSpeed * Time.deltaTime;
            if (moveDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, m_aircraftInfo.moveSpeed * Time.deltaTime);
            }

            float distance = Vector3.Distance(transform.position, currentDogfightTarget.transform.position);
            if (distance <= m_aircraftInfo.attackRange && Time.time >= m_lastAttackTime + m_aircraftInfo.attackCooldown)
            {
                currentDogfightTarget.TakeDamage(m_aircraftInfo.attackPower);
                m_lastAttackTime = Time.time;
            }

            yield return null;
        }
    }

    // 공통 SmoothRotate 함수 - 방향 업데이트와 회전을 함께 처리
    private void SmoothRotate(Vector3 targetDirection)
    {
        if (targetDirection.sqrMagnitude < 0.001f) return;

        // moveSpeed를 각속도로 변환 (moveSpeed * 0.5 = 초당 회전 각도)
        float angularSpeed = m_aircraftInfo.moveSpeed * 1.0f;

        // 목표 방향으로 회전 (한 번만)
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, angularSpeed * Time.deltaTime);

        // 이동 방향은 실제 선체 방향과 동일하게
        m_currentDirection = transform.forward;
    }

    protected virtual IEnumerator AttackShipPhase()
    {
        if (m_targetModule == null) { m_state = EAircraftState.ReturnToCarrier; yield break; }
        SpaceShip targetShip = m_targetModule.GetSpaceShip();
        if (targetShip == null) { m_state = EAircraftState.ReturnToCarrier; yield break; }
        
        AirCraftPathGrid targetShipAirCraftPathGrid = targetShip.m_airCraftPathGrid;
        if (targetShipAirCraftPathGrid == null || targetShipAirCraftPathGrid.m_aircraftPathPoints == null || targetShipAirCraftPathGrid.m_aircraftPathPoints.Count == 0)
        {
            Debug.LogWarning("No outlineInfos on target ship!");
            //m_state = EAircraftState.ReturnToCarrier; yield break;
            yield break;
        }

        List<AirCraftPathPoint> points = targetShipAirCraftPathGrid.m_aircraftPathPoints;
        // 시작 시, 가장 가까운 포인트 찾기
        int currentIndex = FindClosestOutlineIndex(points, transform.position);
        m_currentDirection = transform.forward.normalized;

        while (true)
        {
            // 종료 조건
            if (m_targetModule == null || !m_targetModule.gameObject.activeSelf || m_aircraftInfo.ammo <= 0)
            {
                m_state = EAircraftState.ReturnToCarrier;
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

            // 방향 업데이트 및 회전 (SmoothRotate에서 처리)
            SmoothRotate(toTarget);

            // 이동
            transform.position += m_currentDirection * m_aircraftInfo.moveSpeed * Time.deltaTime * 0.5f;

            // 공격 처리
            if (Time.time >= m_lastAttackTime + m_aircraftInfo.attackCooldown)
                PerformAttack();

            yield return null;
        }
    }
    int FindClosestOutlineIndex(List<AirCraftPathPoint> points, Vector3 pos)
    {
        int bestIndex = 0;
        float bestDist = float.MaxValue;

        for (int i = 0; i < points.Count; i++)
        {
            float d = (points[i].transform.position - pos).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                bestIndex = i;
            }
        }
        return bestIndex;
    }

    int GetNextIndexByAlignment(List<AirCraftPathPoint> points, int current, Vector3 forward)
    {
        float savedDot = -1f;
        int savedNeighbor = 0;
        foreach(var neighbor in points[current].neighbors)
        {
            if( neighbor == points[current] ) continue;

            float tempDot = Vector3.Dot(forward, neighbor.transform.position - points[current].transform.position);
            if( tempDot > savedDot)
            {
                savedDot = tempDot;
                savedNeighbor = neighbor.index;
            }
        }

        return savedNeighbor;
    }

    protected virtual IEnumerator RepositionPhase()
    {
        float repositionDistance = Random.Range(m_aircraftInfo.attackRange * m_repositionMinDistanceMultiplier, m_aircraftInfo.attackRange * m_repositionMaxDistanceMultiplier);
        Vector3 repositionDir = transform.forward;

        if (m_targetModule != null)
        {
            // 적 함선으로부터 멀어지는 방향 벡터
            Vector3 awayFromShipDir = (transform.position - m_targetModule.transform.position).normalized;

            // 현재 진행 방향과 적 함선 반대 방향 사이의 각도 계산
            float angleBetween = Vector3.Angle(transform.forward, awayFromShipDir);

            // 회피 기동을 위해 0~90도 사이의 랜덤한 각도를 더해줌
            // 단, 총 회전 각도가 90도를 넘지 않도록 제한
            float randomTurnAngle = Random.Range(0f, 90f - Mathf.Clamp(angleBetween, 0, 90));

            // 회전 방향 결정 (오른쪽 또는 왼쪽)
            float turnDirection = (Random.value > 0.5f) ? 1f : -1f;

            // 최종 회피 방향 계산
            repositionDir = Quaternion.AngleAxis(randomTurnAngle * turnDirection, Vector3.up) * awayFromShipDir;
        }

        Vector3 startPosition = transform.position;

        while (Vector3.Distance(transform.position, startPosition) < repositionDistance)
        {
            transform.position += repositionDir * m_aircraftInfo.moveSpeed * Time.deltaTime;
            Quaternion targetRotation = Quaternion.LookRotation(repositionDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, m_aircraftInfo.moveSpeed * Time.deltaTime);
            yield return null;
        }

        m_state = EAircraftState.MoveToTarget;
    }

    protected virtual IEnumerator ReturnToCarrierPhase()
    {
        if (m_firePoint == null || m_sourceModule == null || m_moduleHanger == null)
        {
            ReturnToPool();
            yield break;
        }

        // 적 함선의 gridpoint를 이용한 귀환 경로 설정
        bool useGridPath = false;
        List<AirCraftPathPoint> points = null;
        int currentIndex = 0;

        if (m_targetModule != null)
        {
            SpaceShip targetShip = m_targetModule.GetSpaceShip();
            if (targetShip != null)
            {
                AirCraftPathGrid targetShipAirCraftPathGrid = targetShip.m_airCraftPathGrid;
                if (targetShipAirCraftPathGrid != null && targetShipAirCraftPathGrid.m_aircraftPathPoints != null && targetShipAirCraftPathGrid.m_aircraftPathPoints.Count > 0)
                {
                    points = targetShipAirCraftPathGrid.m_aircraftPathPoints;
                    currentIndex = FindClosestOutlineIndex(points, transform.position);
                    useGridPath = true;
                }
            }
        }

        m_currentDirection = transform.forward.normalized;
        while (true)
        {
            Vector3 toCarrier = (m_firePoint.position - transform.position).normalized;
            float dotValue = Vector3.Dot(transform.forward, toCarrier);
            float distanceToCarrier = Vector3.Distance(transform.position, m_firePoint.position);

            // 목표를 지나쳤으면 도착으로 판정 (기존 로직 유지)
            if (dotValue < 0.0f && distanceToCarrier < m_aircraftInfo.attackRange)
                break;

            Vector3 targetDirection = toCarrier;

            // gridpoint를 이용한 경로 이동
            if (useGridPath && points != null)
            {
                Vector3 currentPoint = points[currentIndex].transform.position;
                Vector3 toPoint = (currentPoint - transform.position).normalized;

                // 현재 gridpoint가 모함 방향과 일치하는지 확인 (dot > 0.3 = 약 72도 이내)
                float alignmentToCarrier = Vector3.Dot(toPoint, toCarrier);

                if (alignmentToCarrier > 0.3f) // 모함 방향과 어느정도 일치하면 gridpoint 따라가기
                {
                    targetDirection = toPoint;

                    // 포인트를 지나쳤으면 다음 포인트로
                    float dotToPoint = Vector3.Dot(transform.forward, toPoint);
                    if (dotToPoint < 0.0f)
                        currentIndex = GetNextIndexByAlignment(points, currentIndex, m_currentDirection);
                }
                else // 모함 방향과 맞지 않으면 직행 모드로 전환
                {
                    useGridPath = false;
                    targetDirection = toCarrier;
                }
            }

            // 자연스러운 방향 전환 및 회전
            SmoothRotate(targetDirection);

            // 이동
            transform.position += m_currentDirection * m_aircraftInfo.moveSpeed * Time.deltaTime;

            yield return null;

            // 다음 프레임에서 귀환 중 모함의 body가 교체되어 m_firePoint가 파괴되었는지 체크
            if (m_firePoint == null)
            {
                // 저장된 SpaceShip과 슬롯 정보로 새로운 ModuleHanger와 firePoint 찾기 시도
                if (TryFindNewHangerAndFirePoint())
                {
                    Debug.Log("[AircraftBase] Found new hanger after body replacement. Continuing return.");
                    continue;
                }
                else
                {
                    Debug.LogWarning("[AircraftBase] Carrier firePoint destroyed and cannot find new hanger. Returning to pool.");
                    ReturnToPool();
                    yield break;
                }
            }
        }

        // 최종 귀환 시에도 한 번 더 체크
        if (m_moduleHanger != null)
            m_moduleHanger.ReturnAircraft(m_aircraftInfo);

        ReturnToPool();
    }

    // Body 교체 후 새로운 ModuleHanger와 firePoint를 찾는 메서드
    private bool TryFindNewHangerAndFirePoint()
    {
        // 저장된 SpaceShip과 슬롯 정보로 새 hanger 찾기
        if (m_carrierShip == null) return false;

        // SpaceShip의 모든 body를 순회하며 같은 슬롯의 ModuleHanger 찾기
        foreach (var body in m_carrierShip.m_moduleBodys)
        {
            ModuleSlot newSlot = body.FindModuleSlot(m_hangerModuleTypePacked, m_hangerSlotIndex);
            if (newSlot != null && newSlot.transform.childCount > 0)
            {
                ModuleHanger newHanger = newSlot.GetComponentInChildren<ModuleHanger>();
                if (newHanger != null)
                {
                    // 새로운 hanger의 launcher 찾기
                    LauncherAircraft launcher = newHanger.GetComponentInChildren<LauncherAircraft>();
                    if (launcher != null)
                    {
                        m_moduleHanger = newHanger;
                        m_sourceModule = newHanger;
                        m_firePoint = launcher.GetFirePoint();
                        return true;
                    }
                }
            }
        }

        return false;
    }

    protected Vector3 CalculateAvoidance()
    {
        Vector3 avoidanceDir = Vector3.zero;
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, m_aircraftInfo.avoidanceRadius);

        foreach (Collider col in nearbyObjects)
        {
            if (col.gameObject == gameObject) continue;

            AircraftBase otherAircraft = col.GetComponent<AircraftBase>();
            if (otherAircraft != null && otherAircraft.m_sourceModule == m_sourceModule)
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
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, m_aircraftInfo.detectionRadius);

        foreach (Collider col in nearbyObjects)
        {
            AircraftBase otherAircraft = col.GetComponent<AircraftBase>();
            if (otherAircraft != null && otherAircraft.m_sourceModule != m_sourceModule && otherAircraft.m_aircraftInfo.health > 0)
                return otherAircraft;
        }

        return null;
    }

    protected virtual void PerformAttack()
    {
        if (m_targetModule == null) return;

        SpaceShip targetShip = m_targetModule.GetSpaceShip();
        if (targetShip != null)
        {
            targetShip.TakeDamage(m_aircraftInfo.attackPower);
            m_aircraftInfo.ammo--;
            m_lastAttackTime = Time.time;
        }
    }
    public virtual void TakeDamage(float damage)
    {
        m_aircraftInfo.health -= damage;
        if (m_aircraftInfo.health <= 0)
        {
            m_aircraftInfo.health = 0;
            ReturnToPool();
        }
    }

    protected virtual void ReturnToPool()
    {}

    public virtual void Start() { }



#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (m_targetModule == null)
            return;

        float minRadius = m_aircraftInfo.attackRange * 0.8f;
        float maxRadius = m_aircraftInfo.attackRange * 1.2f;

        if(m_state == EAircraftState.MoveToTarget)
            DrawDonut(m_targetModule.transform, minRadius, maxRadius);
        else if (m_state == EAircraftState.AttackShip)
        {
            DrawAttackPhase();
            //DrawAttackPhase_Sphere();
        }
    }

    private void DrawDonut(Transform target, float minRadius, float maxRadius)
    {
        // 1. 지금 이 함재기 → 타겟 방향 기준 로컬 좌표계 생성
        Vector3 forward = (target.position - transform.position).normalized;

        Vector3 worldUp = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(forward, worldUp)) > 0.9f)
            worldUp = Vector3.right;

        Vector3 right = Vector3.Normalize(Vector3.Cross(worldUp, forward));
        Vector3 up = Vector3.Normalize(Vector3.Cross(forward, right));

        // 2. 도넛의 두 반경 중간값을 사용해서 기본 원을 그림
        float radius = (minRadius + maxRadius) * 0.5f;

        // 3. 세그먼트 수
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

    private void DrawAttackPhase()
    {
        
    }
    // private void DrawAttackPhase_Sphere()
    // {
    //     if (m_state != EAircraftState.AttackShip || m_targetModule == null) return;

    //     Vector3 toShip = m_targetModule.transform.position - transform.position;
    //     float distance = toShip.magnitude;

    //     // 1. 함재기 주위: 안전 거리 스피어 (빨강: 너무 가까우면 여기서 피함)
    //     Gizmos.color = distance < m_safeDistance ? Color.red : Color.magenta;
    //     Gizmos.DrawWireSphere(transform.position, m_safeDistance);

    //     // 2. 목표 함선 주위: 이상적 선회 거리 (초록: 이 거리 유지 목표)
    //     Gizmos.color = Color.green;
    //     Gizmos.DrawWireSphere(m_targetModule.transform.position, m_idealDistance);

    //     // 3. 함재기 ↔ 함선 연결선 (두께 있음 + 색상으로 상태 표시)
    //     Gizmos.color = distance < m_safeDistance * 0.9f ? Color.red : 
    //                 distance < m_safeDistance * 1.4f ? Color.yellow : Color.cyan;
    //     Gizmos.DrawLine(transform.position, m_targetModule.transform.position);

    //     // 4. 현재 진행 방향 화살표 (파랑)
    //     Gizmos.color = Color.blue;
    //     Gizmos.DrawRay(transform.position, m_currentDirection * 5f);  // 5m 길이

    //     // 5. 현재 거리 표시 (Gizmos에 텍스트 없음 → SceneView에서 HandleUtility로 간단 라벨)
    //     // #if UNITY_EDITOR
    //     // UnityEditor.Handles.color = Gizmos.color;
    //     // UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, 
    //     //                         $"{distance:F1}m", 
    //     //                         UnityEditor.GUI.skin.label);
    //     // #endif

    //     // 6. 피함/접선 방향 (너무 가까울 때만 표시)
    //     if (distance < m_safeDistance * 1.4f)
    //     {
    //         Vector3 tangent = Vector3.Cross(toShip.normalized, Vector3.up).normalized;
    //         if (Vector3.Dot(tangent, m_currentDirection) < 0) tangent = -tangent;
    //         Gizmos.color = Color.red;
    //         Gizmos.DrawRay(transform.position, tangent * 8f);
    //     }
    // }

#endif
}
