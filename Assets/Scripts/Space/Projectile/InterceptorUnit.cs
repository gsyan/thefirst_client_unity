// 요격체(인터셉터) 유닛 — 평소엔 소유 함선 전방 원형 궤도를 돌다가, ModuleInterceptor가 적 미사일을 배정하면
// 궤도를 이탈해 함체를 지킬 수 있는 고정된 차단 지점(궤도 중심점 기준 전방)으로 이동해 대기한다.
// 활성화 중엔 궤도 중심점(함체 자식 오브젝트, 없으면 함체 피벗)의 자식으로 붙어서 로컬 좌표로만 움직임 —
// 함대가 셀 이동 등으로 순간이동해도 부모를 따라 같이 움직이므로 월드 좌표 기준 MoveTowards로는 못 따라잡는 문제가 없음.
// 명중 판정은 이 유닛이 직접 하지 않음 — Collider를 달아두면 ProjectileMissile.CheckCollision의 기존 스윕
// 레이캐스트 충돌 시스템이 미사일 쪽에서 감지해 ConsumeByMissileHit()을 호출해준다(터널링 방지, 미사일-대-미사일
// 충돌 분기와 동일한 방식). PoolManager(EPoolName.PROJECTILE_INTERCEPTOR)로 풀링됨 — 반납 시 ObjectPool.Return이
// 원래 풀 부모로 다시 SetParent 해주므로, 활성 중에 부모를 바꿔써도 안전(이미 그렇게 쓰도록 만들어진 풀).
using System.Collections;
using UnityEngine;

public class InterceptorUnit : MonoBehaviour
{
    private enum EInterceptorState { Orbiting, Blocking }

    private const float ORBIT_ANGULAR_SPEED = 90f; // deg/s
    private const float BLOCK_ROTATION_SPEED = 480f; // deg/s
    private const float BLOCK_MOVE_SPEED = 2f;
    private const float BLOCK_POINT_FORWARD_OFFSET = 0.5f; // 궤도 중심점 기준 차단 지점까지의 전방(로컬 Z) 거리
    private const float BLOCK_POINT_LATERAL_SPACING = 1f; // 요격체가 여러 대일 때 차단 지점이 겹치지 않도록 슬롯 인덱스별로 좌우로 벌림
    private const float BLEND_DURATION = 0.5f; // 궤도 목표 ↔ 차단 목표 전환에 걸리는 시간 — 상태 전환 시 목표점이 즉시 바뀌지 않고 이 시간에 걸쳐 보간되어 이동 방향이 부드럽게 꺾임

    private ModuleInterceptor m_owner;
    private int m_slotIndex;
    private int m_maxCount;
    private Transform m_shipTransform; // 회전(정면 바라보기) 계산에만 사용 — 위치는 부모(m_orbitCenter) 기준 로컬 좌표로만 다룸
    private Transform m_orbitCenter; // 궤도 중심점 — 함체 프리팹에 배치된 오브젝트(없으면 함체 피벗 그대로). 활성 중엔 이 오브젝트의 자식으로 붙음
    private float m_orbitRadius;
    private float m_orbitAngle;
    private float m_blendWeight; // 0 = 순수 궤도 목표, 1 = 순수 차단 목표
    private EInterceptorState m_state;
    private ProjectileMissile m_target;
    private Coroutine m_lifeCycleCoroutine;

    public void Initialize(ModuleInterceptor owner, int slotIndex, Transform shipTransform, Transform orbitCenter, float orbitRadius, int maxCount)
    {
        m_owner = owner;
        m_slotIndex = slotIndex;
        m_maxCount = maxCount;
        m_shipTransform = shipTransform;
        m_orbitCenter = orbitCenter;
        m_orbitRadius = orbitRadius;
        m_orbitAngle = maxCount > 0 ? slotIndex * (360f / maxCount) : 0f;
        m_blendWeight = 0f;
        m_state = EInterceptorState.Orbiting;
        m_target = null;

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

    public bool IsIdle()
    {
        return m_state == EInterceptorState.Orbiting;
    }

    public void AssignTarget(ProjectileMissile missile)
    {
        if (m_state != EInterceptorState.Orbiting) return;

        m_target = missile;
        missile.MarkClaimedByInterceptor(this);
        m_state = EInterceptorState.Blocking;
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

            // 상태와 무관하게 항상 진행 — Blocking 중에도 궤도 각도가 계속 흘러야 복귀 시점의 목표 좌표가 "지금"에 맞음
            m_orbitAngle += ORBIT_ANGULAR_SPEED * Time.deltaTime;

            if (m_state == EInterceptorState.Blocking && (m_target == null || m_target.gameObject.activeInHierarchy == false))
                ReturnToOrbit();

            UpdateBlendWeight();
            UpdatePosition();
            UpdateRotation();

            yield return null;
        }
    }

    // 궤도 중심점 기준 로컬 좌표(원 궤적) — 부모가 곧 궤도 중심이라 셀 이동 등 부모의 월드 이동/회전은 신경 쓸 필요 없음
    private Vector3 ComputeOrbitLocalPosition()
    {
        float rad = m_orbitAngle * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rad) * m_orbitRadius, Mathf.Sin(rad) * m_orbitRadius, 0f);
    }

    // 슬롯 인덱스별로 좌우 간격을 두어 요격체가 여러 대여도 같은 차단 지점에 겹치지 않게 함
    private Vector3 ComputeBlockLocalPosition()
    {
        float lateralOffset = (m_slotIndex - (m_maxCount - 1) * 0.5f) * BLOCK_POINT_LATERAL_SPACING;
        return new Vector3(lateralOffset, 0f, BLOCK_POINT_FORWARD_OFFSET);
    }

    // 상태(Orbiting/Blocking)가 바뀌어도 목표 가중치를 즉시 0/1로 대입하지 않고 BLEND_DURATION에 걸쳐 서서히 이동시켜서,
    // UpdatePosition이 향하는 목표 지점 자체가 궤도 위치↔차단 위치 사이를 부드럽게 넘어가게 함(이동 방향이 확 꺾이지 않음)
    private void UpdateBlendWeight()
    {
        float targetWeight = m_state == EInterceptorState.Blocking ? 1f : 0f;
        float blendSpeed = 1f / BLEND_DURATION;
        m_blendWeight = Mathf.MoveTowards(m_blendWeight, targetWeight, blendSpeed * Time.deltaTime);
    }

    // 궤도 위치와 차단 위치를 블렌드한 지점을 목표로 MoveTowards — 명중 판정은 미사일 쪽
    // ProjectileMissile.CheckCollision이 이 유닛의 Collider에 레이캐스트로 맞았을 때 ConsumeByMissileHit()을
    // 호출해서 처리함(터널링 방지를 위해 미사일의 기존 스윕 충돌 시스템에 올라탐)
    private void UpdatePosition()
    {
        Vector3 orbitLocalPos = ComputeOrbitLocalPosition();
        Vector3 blockLocalPos = ComputeBlockLocalPosition();
        Vector3 blendedLocalPos = Vector3.Lerp(orbitLocalPos, blockLocalPos, m_blendWeight);
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, blendedLocalPos, BLOCK_MOVE_SPEED * Time.deltaTime);
    }

    // 상태와 무관하게 항상 함선 정면만 바라봄(타겟을 따로 바라보지 않음)
    private void UpdateRotation()
    {
        Quaternion targetRotation = Quaternion.LookRotation(m_shipTransform.forward, m_shipTransform.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, BLOCK_ROTATION_SPEED * Time.deltaTime);
    }

    // 배정된 미사일이 다른 이유로 먼저 사라진 경우(다른 인터셉터/빔에 요격되는 등) — 궤도로 복귀
    private void ReturnToOrbit()
    {
        m_target = null;
        m_state = EInterceptorState.Orbiting;
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
        m_target = null;
        m_state = EInterceptorState.Orbiting;

        // 플레이 정지 등으로 ObjectManager가 먼저 파괴된 상태면 풀 반납 자체가 무의미하므로 스킵
        if (ObjectManager.Instance == null) return;
        ObjectManager.Instance.m_poolManager.Return(EPoolName.PROJECTILE_INTERCEPTOR, this);
    }
}
