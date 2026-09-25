// 요격체(인터셉터) 유닛 — 평소엔 소유 함선 전방 원형 궤도를 돌다가, ModuleInterceptor가 가드로 지정하면 같은 궤적을 유지한 채 반경만 점차 줄여 궤도 중심점 근처에서 계속 돈다.
// 가드 중엔 콜라이더 반경이 커져 ProjectileMissile.CheckCollision의 스윕 레이가 적 미사일을 잡는다(ConsumeByMissileHit). 활성 중엔 궤도 중심점의 자식으로 붙어 로컬 좌표로만 움직이며 PoolManager(EPoolName.PROJECTILE_INTERCEPTOR)로 풀링됨.
using System.Collections;
using UnityEngine;

public class InterceptorUnit : MonoBehaviour
{
    private enum EInterceptorState { Orbiting, Guarding }

    private const float ORBIT_ANGULAR_SPEED = 90f; // deg/s
    private const float MOVE_SPEED = 2f;
    private const float ROTATION_SPEED = 480f; // deg/s
    private const float RADIUS_CHANGE_SPEED = 1f; // 궤도 ↔ 가드 전환 시 중심점으로부터의 거리가 변하는 속도(단위/초)
    private const float GUARD_RADIUS_MIN = 0.1f; // 가드 중 유닛마다 이 범위의 반경으로 중심점 주위를 계속 돎
    private const float GUARD_RADIUS_MAX = 0.5f;
    private const float GUARD_COLLIDER_WORLD_RADIUS = 3f;

    private ModuleInterceptor m_owner;
    private int m_slotIndex;
    private Transform m_shipTransform; // 회전(정면 바라보기) 계산에만 사용 — 위치는 부모(m_orbitCenter) 기준 로컬 좌표로만 다룸
    private Transform m_orbitCenter; // 궤도 중심점 — 함체 프리팹에 배치된 오브젝트(없으면 함체 피벗 그대로). 활성 중엔 이 오브젝트의 자식으로 붙음
    private float m_orbitRadius;
    private float m_guardRadius;
    private float m_currentRadius; // 현재 중심점으로부터의 궤도 반경 — 상태에 따라 m_orbitRadius/m_guardRadius를 향해 서서히 변함
    private float m_orbitAngle;
    private EInterceptorState m_state;
    private Coroutine m_lifeCycleCoroutine;
    private SphereCollider m_collider;
    private float m_baseColliderRadius;

    private void Awake()
    {
        m_collider = GetComponent<SphereCollider>();
        m_baseColliderRadius = m_collider.radius;
    }

    public void Initialize(ModuleInterceptor owner, int slotIndex, Transform shipTransform, Transform orbitCenter, float orbitRadius, int maxCount)
    {
        m_owner = owner;
        m_slotIndex = slotIndex;
        m_shipTransform = shipTransform;
        m_orbitCenter = orbitCenter;
        m_orbitRadius = orbitRadius;
        m_currentRadius = orbitRadius;
        m_orbitAngle = maxCount > 0 ? slotIndex * (360f / maxCount) : 0f;
        m_state = EInterceptorState.Orbiting;
        m_collider.radius = m_baseColliderRadius;

        // 궤도 중심점의 자식으로 붙여서 이후 위치는 전부 로컬 좌표로만 계산 — 부모(함체)가 셀 이동으로 순간이동해도
        // 자식은 자동으로 같이 이동하므로 월드 좌표 MoveTowards의 "못 따라잡음" 문제가 애초에 생기지 않음
        Transform parent = m_orbitCenter != null ? m_orbitCenter : m_shipTransform;
        transform.SetParent(parent, worldPositionStays: false);

        // 최초 스폰 시점엔 스무스 이동 없이 바로 궤도 위치로 스냅(상태 전환 때만 스무스하게 이어지면 됨)
        transform.localPosition = ComputeOrbitLocalPosition();
        transform.rotation = Quaternion.LookRotation(m_shipTransform.forward, m_shipTransform.up);

        if (m_lifeCycleCoroutine != null) StopCoroutine(m_lifeCycleCoroutine);
        m_lifeCycleCoroutine = StartCoroutine(LifeCycle());
    }

    // ModuleInterceptor.RefreshGuard가 호출 — 가드로 지정되면 콜라이더를 월드 반경 GUARD_COLLIDER_WORLD_RADIUS로 키우고 궤도 반경을 줄이기 시작함(이미 가드면 무시)
    public void EnterGuard()
    {
        if (m_state == EInterceptorState.Guarding) return;
        m_state = EInterceptorState.Guarding;
        m_guardRadius = Random.Range(GUARD_RADIUS_MIN, GUARD_RADIUS_MAX);

        float lossyScale = transform.lossyScale.x;
        m_collider.radius = GUARD_COLLIDER_WORLD_RADIUS / lossyScale;
    }

    // 가드 역할을 다른 유닛에게 넘길 때 호출 — 콜라이더를 즉시 원래 크기로 복원하고 궤도 반경을 되돌림(이미 궤도면 무시)
    public void LeaveGuard()
    {
        if (m_state == EInterceptorState.Orbiting) return;
        m_state = EInterceptorState.Orbiting;
        m_collider.radius = m_baseColliderRadius;
    }

    private IEnumerator LifeCycle()
    {
        while (true)
        {
            if (m_shipTransform == null)
            {
                ReturnToPoolImmediate();
                yield break;
            }

            // 상태와 무관하게 궤도 각도는 항상 진행 — 가드 중에도 같은 궤적을 그리며 돌고, 반경만 달라짐
            m_orbitAngle += ORBIT_ANGULAR_SPEED * Time.deltaTime;

            UpdateRadius();
            UpdatePosition();
            UpdateRotation();

            yield return null;
        }
    }

    // 상태에 따라 목표 반경(궤도 반경/가드 반경)을 향해 서서히 변경 — 궤도를 벗어나 중심점으로 직행하지 않고 같은 궤적에서 안쪽으로 감겨 들어감
    private void UpdateRadius()
    {
        float targetRadius = m_state == EInterceptorState.Guarding ? m_guardRadius : m_orbitRadius;
        m_currentRadius = Mathf.MoveTowards(m_currentRadius, targetRadius, RADIUS_CHANGE_SPEED * Time.deltaTime);
    }

    // 궤도 중심점 기준 로컬 좌표(원 궤적) — 부모가 곧 궤도 중심이라 셀 이동 등 부모의 월드 이동/회전은 신경 쓸 필요 없음
    private Vector3 ComputeOrbitLocalPosition()
    {
        float rad = m_orbitAngle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rad) * m_currentRadius, Mathf.Sin(rad) * m_currentRadius, 0f);
    }

    // 현재 반경의 궤도 위치를 목표로 MoveTowards — 명중 판정은 미사일 쪽
    // ProjectileMissile.CheckCollision이 이 유닛의 Collider에 레이캐스트로 맞았을 때 ConsumeByMissileHit()을
    // 호출해서 처리함(터널링 방지를 위해 미사일의 기존 스윕 충돌 시스템에 올라탐)
    private void UpdatePosition()
    {
        Vector3 orbitLocalPos = ComputeOrbitLocalPosition();
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, orbitLocalPos, MOVE_SPEED * Time.deltaTime);
    }

    // 상태와 무관하게 항상 함선 정면만 바라봄
    private void UpdateRotation()
    {
        Quaternion targetRotation = Quaternion.LookRotation(m_shipTransform.forward, m_shipTransform.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, ROTATION_SPEED * Time.deltaTime);
    }

    // ProjectileMissile.CheckCollision이 이 유닛의 Collider에 명중했을 때 외부에서 호출 — 미사일 쪽에서
    // 이펙트/자기 자신 파괴를 이미 처리하므로, 여기선 요격체 자신의 풀 반환/자리 비움만 처리
    public SpaceFleet GetOwnerFleet()
    {
        return m_owner != null ? m_owner.GetOwnerFleet() : null;
    }

    public void ConsumeByMissileHit()
    {
        ModuleInterceptor owner = m_owner;
        int consumedIndex = m_slotIndex;
        ReturnToPoolImmediate();
        owner.OnUnitConsumed(consumedIndex);
    }

    public void ReturnToPoolImmediate()
    {
        if (m_lifeCycleCoroutine != null)
        {
            StopCoroutine(m_lifeCycleCoroutine);
            m_lifeCycleCoroutine = null;
        }
        m_state = EInterceptorState.Orbiting;
        m_collider.radius = m_baseColliderRadius;

        // 플레이 정지 등으로 ObjectManager가 먼저 파괴된 상태면 풀 반납 자체가 무의미하므로 스킵
        if (ObjectManager.Instance == null) return;
        ObjectManager.Instance.m_poolManager.Return(EPoolName.PROJECTILE_INTERCEPTOR, this);
    }
}
