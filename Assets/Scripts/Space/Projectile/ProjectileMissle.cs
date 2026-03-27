// 미사일 발사체 - 유도 비행, 터널링 방지 Raycast, 풀 반환 처리
using UnityEngine;
using System.Collections;

public class ProjectileMissile : ProjectileBase
{
    private static int s_raycastMask = 0;
    private Vector3 m_saveTargetPosition;
    private Coroutine m_lifeCycleCoroutine;
    private float m_lifeTime;
    private const float MAX_LIFE_TIME = 10f;
    private const float ROTATION_SPEED = 90f;
    private ModuleData m_moduleData;
    private float m_currentSpeed;
    private float m_initialFlightDuration;
    private Vector3 m_initialDirection;
    private EPoolName m_poolName;
    private Vector3 m_prevPosition;

    public void SetPoolName(EPoolName poolName) { m_poolName = poolName; }

    public override void InitializeProjectile(Transform firePointTransform, ModuleBase target, float damage, ModuleData moduleData, Color color, ModuleBase sourceModuleBase)
    {
        base.InitializeProjectile(firePointTransform, target, damage, moduleData, color, sourceModuleBase);
        m_moduleData = moduleData;
        m_lifeTime = 0.0f;
        m_currentSpeed = 0.0f;
        m_prevPosition = transform.position;
        m_initialFlightDuration = Random.Range(0.1f, 0.5f);
        m_initialDirection = transform.forward;
        if (m_lifeCycleCoroutine != null) StopCoroutine(m_lifeCycleCoroutine);
        m_lifeCycleCoroutine = StartCoroutine(MissleLifeCycle());
    }

    private IEnumerator MissleLifeCycle()
    {
        while (true)
        {
            m_lifeTime += Time.deltaTime;
            if (m_lifeTime >= MAX_LIFE_TIME)
            {
                ReturnToPool(showHitEffect: false);
                yield break;
            }

            if (m_target != null)
                m_saveTargetPosition = m_target.transform.position;

            Vector3 targetDirection = (m_saveTargetPosition - transform.position).normalized;
            float dotProduct = Vector3.Dot(transform.forward, targetDirection);
            float applyDot = dotProduct * dotProduct;
            m_currentSpeed = m_moduleData.projectileSpeed * Mathf.Max(0.1f, applyDot);

            m_prevPosition = transform.position;

            if (m_lifeTime < m_initialFlightDuration)
            {
                transform.position += m_initialDirection * m_currentSpeed * Time.deltaTime;
            }
            else
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, ROTATION_SPEED * Time.deltaTime);
                transform.position += transform.forward * m_currentSpeed * Time.deltaTime;
            }

            // 이전 위치→현재 위치 raycast로 충돌 감지 (터널링 방지)
            Vector3 moveVec = transform.position - m_prevPosition;
            // Shield 레이어 제외 (방어막은 추후 별도 처리 예정)
            if (s_raycastMask == 0)
                s_raycastMask = ~LayerMask.GetMask("Shield");
            if (Physics.Raycast(m_prevPosition, moveVec.normalized, out RaycastHit hit, moveVec.magnitude, s_raycastMask))
            {
                SpaceShip hitShip = hit.collider.GetComponentInParent<SpaceShip>();
                if (hitShip != null && (m_sourceShip == null || hitShip.m_myFleet != m_sourceShip.m_myFleet))
                {
                    hitShip.TakeDamage(m_damage);
                    ReturnToPool();
                    yield break;
                }
            }

            yield return null;
        }
    }

    public void ReturnToPool(bool showHitEffect = true)
    {
        if (m_lifeCycleCoroutine != null)
        {
            StopCoroutine(m_lifeCycleCoroutine);
            m_lifeCycleCoroutine = null;
        }

        if (showHitEffect && gameObject.activeInHierarchy)
            ObjectManager.Instance.m_poolManager.GetEffect_Play_AutoReturn(EPoolName.EFFECT_MISSILE_HIT, transform.position);

        ObjectManager.Instance.m_poolManager.Return(m_poolName, this);
    }
}
