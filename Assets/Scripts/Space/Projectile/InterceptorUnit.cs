// 요격체(인터셉터) 유닛 — 평소엔 소유 함선 전방 원형 궤도를 돌다가, ModuleInterceptor가 적 미사일을 배정하면
// 궤도를 이탈해 미사일의 현재 위치+속도로 예측한 지점을 향해 유도 비행, 명중 시 자신과 미사일 모두 소멸한다.
// PoolManager(EPoolName.PROJECTILE_INTERCEPTOR)로 풀링됨 — ProjectileMissile의 유도(Phase_Homing) 방식을 참고해 구현.
using System.Collections;
using UnityEngine;

public class InterceptorUnit : MonoBehaviour
{
    private enum EInterceptorState { Orbiting, Chasing }

    private const float ORBIT_ANGULAR_SPEED = 90f; // deg/s
    private const float CHASE_ROTATION_SPEED = 480f; // deg/s
    private const float CHASE_MOVE_SPEED = 40f;
    private const float HIT_DISTANCE = 1.5f;

    private ModuleInterceptor m_owner;
    private int m_slotIndex;
    private Transform m_shipTransform;
    private float m_orbitRadius;
    private float m_orbitForwardOffset;
    private float m_orbitAngle;
    private EInterceptorState m_state;
    private ProjectileMissile m_target;
    private Coroutine m_lifeCycleCoroutine;

    public void Initialize(ModuleInterceptor owner, int slotIndex, Transform shipTransform, float orbitRadius, float orbitForwardOffset, int maxCount)
    {
        m_owner = owner;
        m_slotIndex = slotIndex;
        m_shipTransform = shipTransform;
        m_orbitRadius = orbitRadius;
        m_orbitForwardOffset = orbitForwardOffset;
        m_orbitAngle = maxCount > 0 ? slotIndex * (360f / maxCount) : 0f;
        m_state = EInterceptorState.Orbiting;
        m_target = null;

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
        m_state = EInterceptorState.Chasing;
    }

    private IEnumerator LifeCycle()
    {
        while (true)
        {
            if (m_state == EInterceptorState.Orbiting)
            {
                if (m_shipTransform == null)
                {
                    ReturnToPoolImmediate();
                    yield break;
                }
                TickOrbit();
            }
            else
            {
                if (TickChase() == true) yield break;
            }
            yield return null;
        }
    }

    private void TickOrbit()
    {
        m_orbitAngle += ORBIT_ANGULAR_SPEED * Time.deltaTime;
        float rad = m_orbitAngle * Mathf.Deg2Rad;
        Vector3 radial = (Mathf.Cos(rad) * m_shipTransform.right + Mathf.Sin(rad) * m_shipTransform.up) * m_orbitRadius;

        transform.position = m_shipTransform.position + m_shipTransform.forward * m_orbitForwardOffset + radial;
        transform.rotation = Quaternion.LookRotation(m_shipTransform.forward, m_shipTransform.up);
    }

    // 미사일의 현재 위치+속도로 예측 지점을 계산해 그쪽을 향해 유도 비행(리드 추적) — ProjectileMissile.Phase_Homing의 단순 추적과 달리 미리 앞을 겨냥함
    private bool TickChase()
    {
        if (m_target == null || m_target.gameObject.activeInHierarchy == false)
        {
            ReturnToOrbit();
            return false;
        }

        Vector3 toMissile = m_target.transform.position - transform.position;
        float distance = toMissile.magnitude;
        float leadTime = CHASE_MOVE_SPEED > 0f ? distance / CHASE_MOVE_SPEED : 0f;
        Vector3 predictedPoint = m_target.transform.position + m_target.GetVelocity() * leadTime;

        Vector3 toPredicted = (predictedPoint - transform.position).normalized;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPredicted), CHASE_ROTATION_SPEED * Time.deltaTime);
        transform.position += transform.forward * CHASE_MOVE_SPEED * Time.deltaTime;

        if (distance <= HIT_DISTANCE)
        {
            HandleInterceptSuccess();
            return true;
        }
        return false;
    }

    // 배정된 미사일이 다른 이유로 먼저 사라진 경우(다른 인터셉터/빔에 요격되는 등) — 궤도로 복귀
    private void ReturnToOrbit()
    {
        m_target = null;
        m_state = EInterceptorState.Orbiting;
    }

    private void HandleInterceptSuccess()
    {
        SoundManager.Instance.PlayFX(EFx.Explosion_Missile, transform.position);

        EffectBase effect = ObjectManager.Instance.m_poolManager.Get<EffectBase>(EPoolName.EFFECT_EXPLOSION_MISSILE_SMALL);
        effect.transform.position = transform.position;
        effect.PlayEffect();

        m_target.ReturnToPool(showHitEffect: false);

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
        ObjectManager.Instance.m_poolManager.Return(EPoolName.PROJECTILE_INTERCEPTOR, this);
    }
}
