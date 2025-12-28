using UnityEngine;
using System.Collections;

public class ProjectileMissile : ProjectileBase
{
    private Vector3 m_saveTargetPosition;
    private Coroutine m_lifeCycleCoroutine;
    private float m_lifeTime;
    private const float MAX_LIFE_TIME = 10f;
    private const float ROTATION_SPEED = 90f;
    private ModuleData m_moduleData;
    private float m_currentSpeed;
    private float m_acceleration;
    private float m_initialFlightDuration;
    private Vector3 m_initialDirection;

    public override void InitializeProjectile(Transform firePointTransform, ModuleBase target, float damage, ModuleData moduleData, Color color, ModuleBase sourceModuleBase)
    {
        base.InitializeProjectile(firePointTransform, target, damage, moduleData, color, sourceModuleBase);
        m_moduleData = moduleData;
        m_lifeTime = 0.0f;
        m_currentSpeed = 0.0f;
        m_acceleration = m_moduleData.m_projectileSpeed * 0.1f;
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
                ReturnToPool();
                yield break;
            }

            if (m_target != null)
                m_saveTargetPosition = m_target.transform.position;

            Vector3 targetDirection = (m_saveTargetPosition - transform.position).normalized;
            float dotProduct = Vector3.Dot(transform.forward, targetDirection);
            float applyDot = dotProduct * dotProduct;
            m_currentSpeed = m_moduleData.m_projectileSpeed * Mathf.Max(0.1f, applyDot);
            
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

            float distanceToTarget = Vector3.Distance(transform.position, m_saveTargetPosition);
            if (distanceToTarget < 1f)
            {
                if (m_target != null)
                {
                    SpaceShip targetShip = m_target.GetSpaceShip();                    
                    if (targetShip != null)
                        targetShip.TakeDamage(m_damage);
                }
                ReturnToPool();
                yield break;
            }

            yield return null;
        }
    }

    private void ReturnToPool()
    {
        if (m_lifeCycleCoroutine != null)
        {
            StopCoroutine(m_lifeCycleCoroutine);
            m_lifeCycleCoroutine = null;
        }
        
        ObjectManager.Instance.m_poolManager.GetEffect_Play_AutoReturn(EPoolName.EFFECT_MISSILE_HIT, transform.position);

        ObjectManager.Instance.m_poolManager.Return(EPoolName.PROJECTILE_MISSILE, this);
    }
}
