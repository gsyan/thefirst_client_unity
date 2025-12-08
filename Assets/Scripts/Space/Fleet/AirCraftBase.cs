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

    [SerializeField] protected float m_moveSpeed = 10f;
    [SerializeField] protected float m_rotationSpeed = 240f; // 120
    [SerializeField] protected float m_launchStraightDistance = 5f;
    [SerializeField] protected float m_detectionRadius = 3f;
    [SerializeField] protected float m_avoidanceRadius = 0.5f;
    [SerializeField] protected float m_attackRange = 2f;
    [SerializeField] protected float m_attackCooldown = 1f;
    [SerializeField] protected float m_repositionMinDistanceMultiplier = 1.5f;
    [SerializeField] protected float m_repositionMaxDistanceMultiplier = 2.5f;

    [SerializeField] protected float m_lastAttackTime;

    [SerializeField] protected EAircraftState m_state = EAircraftState.None;
    [SerializeField] protected Vector3 m_launchStartPos;
    [SerializeField] protected Vector3 m_randomOffset;
    [SerializeField] protected Coroutine m_lifeCycleCoroutine;

    [SerializeField] protected float m_orbitAngle;
    [SerializeField] protected float m_targetOrbitAngle;
    [SerializeField] protected float m_orbitRadius = 2f;
    [SerializeField] protected float m_orbitSpeed = 90f;
    [SerializeField] protected float m_orbitAngleLerpSpeed = 2f;
    // 선회 방향 플래그: true -> 시계 방향, false -> 반시계 방향
    [SerializeField] protected bool m_clockwise = false;

    [SerializeField] protected float m_sphereRadius = 1.0f;        // 함재기 안전 반경 (충돌 예측 크기), 2~4 (클수록 안전/넓게 돔)
    [SerializeField] protected float m_lookaheadTime = 1.0f;       // 예측 시간 (초) → 더 길면 부드러움↑, 짧으면 민첩↑, 0.8~1.5 (짧음=민첩, 길음=부드러움)
    [SerializeField] protected float m_directionLerpSpeed = 4f;    // 방향 변화 스무스 속도 (높을수록 빠름), 2~5 (낮음=안정, 높음=빠른 반응)
    [SerializeField] protected Vector3 m_currentDirection;         // ★ 현재 진행 방향 (normalized, velocity처럼 사용)
    

    public virtual void InitializeAirCraft(Transform firePointTransform, ModuleBase target, AircraftInfo aircraftInfo, ModuleHanger moduleHanger, Color color, ModuleBase sourceModuleBase)
    {
        m_firePoint = firePointTransform;
        m_targetModule = target;
        m_aircraftInfo = aircraftInfo;
        m_moduleHanger = moduleHanger;
        m_sourceModule = sourceModuleBase;

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
        Vector3 targetPos = m_launchStartPos + transform.forward * m_launchStraightDistance + m_randomOffset;
        while (true)
        {
            Vector3 toTarget = (targetPos - transform.position).normalized;
            float dotValue = Vector3.Dot(transform.forward, toTarget);
            if(dotValue < 0.0f)
                break;
                        
            Vector3 avoidanceDir = CalculateAvoidance();
            Vector3 moveDir = toTarget + avoidanceDir;
            moveDir.Normalize();
            transform.position += moveDir * m_moveSpeed * Time.deltaTime;
            if (moveDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, m_rotationSpeed * Time.deltaTime);
            }

            
            yield return null;
        }

        m_state = EAircraftState.MoveToTarget;
    }

    protected virtual IEnumerator MoveToTargetPhase()
    {
        Vector3 attackApproachPoint = Vector3.zero;
        if (m_targetModule != null)
        {
            attackApproachPoint = GetRelativeVirticalDonutPoint(m_targetModule.transform, m_attackRange * 0.8f, m_attackRange * 1.2f);
        }

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

            transform.position += transform.forward * m_moveSpeed * Time.deltaTime;

            Quaternion targetRotation = Quaternion.LookRotation(finalMoveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, m_rotationSpeed * Time.deltaTime);

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

            transform.position += moveDir * m_moveSpeed * Time.deltaTime;
            if (moveDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, m_rotationSpeed * Time.deltaTime);
            }

            float distance = Vector3.Distance(transform.position, currentDogfightTarget.transform.position);
            if (distance <= m_attackRange && Time.time >= m_lastAttackTime + m_attackCooldown)
            {
                currentDogfightTarget.TakeDamage(m_aircraftInfo.attackPower);
                m_lastAttackTime = Time.time;
            }

            yield return null;
        }
    }


    protected float m_safeDistance = 1.5f;        // 이 거리 안으로 들어오면 피함
    protected float m_idealDistance = 2.0f;       // 이 거리 정도 유지하려고 함
    protected float m_attractionStrength = 1.2f;
    protected float m_repulsionStrength = 3.5f;

    protected virtual IEnumerator AttackShipPhase_OnlyShpere()
    {
        if (m_targetModule == null) yield break;

        m_currentDirection = transform.forward.normalized;

        while (true)
        {
            // 종료 조건
            if (m_targetModule == null || !m_targetModule.gameObject.activeSelf || m_aircraftInfo.ammo <= 0)
            {
                m_state = EAircraftState.ReturnToCarrier;
                yield break;
            }

            Vector3 toShip = m_targetModule.transform.position - transform.position;
            float distance = toShip.magnitude;
            Vector3 desiredDirection = m_currentDirection;

            if (distance < m_safeDistance)  // 너무 가까움 → 강하게 피하기
            {
                // 함선에서 멀어지는 방향 + 접선 방향으로 틀어서 원운동 유도
                Vector3 repulsion = -toShip.normalized;
                Vector3 tangent = Vector3.Cross(toShip, m_currentDirection + Vector3.up).normalized;
                
                // 현재 방향과 접선 방향이 반대면 뒤집기
                if (Vector3.Dot(tangent, m_currentDirection) < 0) tangent = -tangent;

                desiredDirection = Vector3.Lerp(repulsion, tangent, 0.7f).normalized;
            }
            else if (distance < m_safeDistance * 1.4f)  // 적당히 가까움 → 선회 유지
            {
                // 접선 방향으로 살짝 틀어서 원운동 만들기
                Vector3 tangent = Vector3.Cross(toShip, Vector3.up).normalized;
                if (Vector3.Dot(tangent, m_currentDirection) < 0) tangent = -tangent;

                desiredDirection = tangent;
            }
            else  // 멀면 → 함선 쪽으로 끌려가기
            {
                Vector3 attraction = toShip.normalized;
                desiredDirection = attraction;
            }

            // 최종 방향 부드럽게 적용
            m_currentDirection = Vector3.Lerp(m_currentDirection, desiredDirection.normalized, 
                                            m_directionLerpSpeed * Time.deltaTime).normalized;

            transform.position += m_currentDirection * m_moveSpeed * Time.deltaTime;
            SmoothRotate(m_currentDirection);

            if (Time.time >= m_lastAttackTime + m_attackCooldown)
                PerformAttack();

            yield return null;
        }
    }
    // 공통 SmoothRotate 함수 (반드시 클래스 안에 있어야 함!)
    private void SmoothRotate(Vector3 desiredForward)
    {
        if (desiredForward.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(desiredForward);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, m_rotationSpeed * Time.deltaTime * 0.1f);
        // 0.1f → 조정 가능: 0.05f = 느긋, 0.2f = 민첩
    }

    protected virtual IEnumerator AttackShipPhase()
    {
        if (m_targetModule == null) { m_state = EAircraftState.ReturnToCarrier; yield break; }
        SpaceShip targetShip = m_targetModule.GetSpaceShip();
        if (targetShip == null) { m_state = EAircraftState.ReturnToCarrier; yield break; }
        
        OutlineScanner targetShipOutlineScanner = targetShip.m_outlineScanner;
        if (targetShipOutlineScanner == null || targetShipOutlineScanner.m_outlinePointInfos == null || targetShipOutlineScanner.m_outlinePointInfos.Count == 0)
        {
            Debug.LogWarning("No outlineInfos on target ship!");
            //m_state = EAircraftState.ReturnToCarrier; yield break;
            yield break;
        }

        List<ModuleOutlinePointInfo> points = targetShipOutlineScanner.m_outlinePointInfos;
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

            // 방향 스무싱
            m_currentDirection = Vector3.Lerp(m_currentDirection, toTarget, m_directionLerpSpeed * Time.deltaTime).normalized;

            // 이동
            transform.position += m_currentDirection * m_moveSpeed * Time.deltaTime * 0.5f;

            // 회전
            SmoothRotate(m_currentDirection);

            // 공격 처리
            if (Time.time >= m_lastAttackTime + m_attackCooldown)
                PerformAttack();

            yield return null;
        }
    }
    int FindClosestOutlineIndex(List<ModuleOutlinePointInfo> points, Vector3 pos)
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

    int GetNextIndexByAlignment(List<ModuleOutlinePointInfo> points, int current, Vector3 forward)
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
        float repositionDistance = Random.Range(m_attackRange * m_repositionMinDistanceMultiplier, m_attackRange * m_repositionMaxDistanceMultiplier);
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
            transform.position += repositionDir * m_moveSpeed * Time.deltaTime;
            Quaternion targetRotation = Quaternion.LookRotation(repositionDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, m_rotationSpeed * Time.deltaTime);
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

        while (Vector3.Distance(transform.position, m_firePoint.position) > 0.5f)
        {
            Vector3 moveDir = (m_firePoint.position - transform.position).normalized;

            transform.position += moveDir * m_moveSpeed * Time.deltaTime;
            if (moveDir != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, m_rotationSpeed * Time.deltaTime);
            }

            yield return null;
        }

        m_moduleHanger.ReturnAircraft(m_aircraftInfo);
        ReturnToPool();
    }

    protected Vector3 CalculateAvoidance()
    {
        Vector3 avoidanceDir = Vector3.zero;
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, m_avoidanceRadius);

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
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, m_detectionRadius);

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
    // private void OnDrawGizmos()
    // {
    //     if (m_targetModule == null)
    //         return;

    //     float minRadius = m_attackRange * 0.8f;
    //     float maxRadius = m_attackRange * 1.2f;

    //     if(m_state == EAircraftState.MoveToTarget)
    //         DrawDonut(m_targetModule.transform, minRadius, maxRadius);
    //     else if (m_state == EAircraftState.AttackShip)
    //     {
    //         DrawAttackPhase();
    //         //DrawAttackPhase_Sphere();
    //     }
    // }

    // private void DrawDonut(Transform target, float minRadius, float maxRadius)
    // {
    //     // 1. 지금 이 함재기 → 타겟 방향 기준 로컬 좌표계 생성
    //     Vector3 forward = (target.position - transform.position).normalized;

    //     Vector3 worldUp = Vector3.up;
    //     if (Mathf.Abs(Vector3.Dot(forward, worldUp)) > 0.9f)
    //         worldUp = Vector3.right;

    //     Vector3 right = Vector3.Normalize(Vector3.Cross(worldUp, forward));
    //     Vector3 up = Vector3.Normalize(Vector3.Cross(forward, right));

    //     // 2. 도넛의 두 반경 중간값을 사용해서 기본 원을 그림
    //     float radius = (minRadius + maxRadius) * 0.5f;

    //     // 3. 세그먼트 수
    //     int segments = 64;

    //     Vector3 prevPoint = Vector3.zero;

    //     Gizmos.color = Color.cyan;

    //     for (int i = 0; i <= segments; i++)
    //     {
    //         float angle = (float)i / segments * Mathf.PI * 2f;

    //         // Local donut offset
    //         Vector3 localOffset = new Vector3(
    //             Mathf.Cos(angle),
    //             Mathf.Sin(angle),
    //             0f
    //         ) * radius;

    //         // Convert to world
    //         Vector3 worldOffset =
    //             right * localOffset.x +
    //             up * localOffset.y +
    //             forward * localOffset.z;

    //         Vector3 worldPos = target.position + worldOffset;

    //         if (i > 0)
    //             Gizmos.DrawLine(prevPoint, worldPos);

    //         prevPoint = worldPos;
    //     }
    // }

    // private void DrawAttackPhase()
    // {
        
    // }
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
