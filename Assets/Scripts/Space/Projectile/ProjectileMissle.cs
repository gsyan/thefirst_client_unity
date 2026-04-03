// 미사일 발사체 - 콜드런치(Eject→Steering→Homing) 3단계, 유도 비행, 터널링 방지 Raycast, 풀 반환 처리
using UnityEngine;
using System.Collections;


public class ProjectileMissile : ProjectileBase
{
    private enum EFlightPhase { Eject, Steering, Homing }

    private static int s_raycastMask = 0;
    private Vector3 m_saveTargetPosition;   // 목표 소멸 시 콜드런치 기간 방향 유지용
    private Coroutine m_lifeCycleCoroutine;
    private float m_lifeTime;
    private const float MAX_LIFE_TIME = 10f;
    private const float ROTATION_SPEED = 90f;
    private const float EJECT_BURST_ROTATION_SPEED = 200f; // Eject 후반 노즐 버스트 회전속도
    private const float COLD_LAUNCH_DOT_THRESHOLD = 0.7f;  // 이 이상이면 Steering 전환 카운트 시작
    private const float COLD_LAUNCH_HOLD_TIME = 0.3f;      // dot 충족 후 이 시간 유지되면 Steering 전환
    private const float EJECT_SPEED = 1f;                  // Eject 단계 고정 속도
    private ModuleData m_moduleData;
    private float m_currentSpeed;
    private float m_initialFlightDuration;
    private Vector3 m_initialDirection;
    private EPoolName m_poolName;
    private Vector3 m_prevPosition;
    private EFlightPhase m_phase;
    private float m_dotMetTime;  // dot 충족 시작 시각 (-1 = 미충족)

    [Header("Trail Particles")]
    [SerializeField] private GameObject m_burstTail;   // 항속 메인 추진
    [SerializeField] private GameObject m_burstUp;
    [SerializeField] private GameObject m_burstDown;
    [SerializeField] private GameObject m_burstLeft;
    [SerializeField] private GameObject m_burstRight;

    public void SetPoolName(EPoolName poolName) { m_poolName = poolName; }

    public override void InitializeProjectile(Transform firePointTransform, ModuleBase target, float damage, ModuleData moduleData, Color color, ModuleBase sourceModuleBase)
    {
        base.InitializeProjectile(firePointTransform, target, damage, moduleData, color, sourceModuleBase);
        m_moduleData = moduleData;
        m_lifeTime = 0.0f;
        m_currentSpeed = 0.0f;
        m_prevPosition = transform.position;
        m_initialFlightDuration = Random.Range(0.5f, 0.6f);
        m_initialDirection = transform.forward;
        m_phase = EFlightPhase.Eject;
        m_dotMetTime = -1f;
        SetBurstSide(false);
        SetBurstTail(false);

        // 콜드런치 시작 시점의 목표 위치 저장 (목표 소멸해도 방향 유지)
        m_saveTargetPosition = target != null ? target.transform.position : firePointTransform.position + firePointTransform.forward * 50f;

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

            // 목표가 살아있으면 위치 갱신
            if (m_target != null)
                m_saveTargetPosition = m_target.transform.position;

            m_prevPosition = transform.position;

            // --- Phase: Eject ---
            // 발사 직후 발사대 방향으로 고정 속도 이동, 회전 없음
            if (EFlightPhase.Eject == m_phase)
            {
                transform.position += m_initialDirection * EJECT_SPEED * Time.deltaTime;

                // initialFlightDuration 이후: 노즐 버스트로 목표 방향 회전, dot 충족 시 Steering 전환
                if (m_lifeTime >= m_initialFlightDuration)
                {
                    Vector3 toTarget = (m_saveTargetPosition - transform.position).normalized;
                    Quaternion targetRotation = Quaternion.LookRotation(toTarget);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, EJECT_BURST_ROTATION_SPEED * Time.deltaTime);
                    SetBurstSide(true);

                    if (Vector3.Dot(transform.forward, toTarget) >= COLD_LAUNCH_DOT_THRESHOLD)
                    {
                        if (m_dotMetTime < 0f) m_dotMetTime = m_lifeTime;
                        if (m_lifeTime - m_dotMetTime >= COLD_LAUNCH_HOLD_TIME)
                        {
                            SetBurstSide(false);
                            SetBurstTail(true);
                            m_phase = EFlightPhase.Steering;
                        }
                    }
                    else
                    {
                        m_dotMetTime = -1f; // dot 조건 이탈 시 리셋
                    }
                }
            }
            // --- Phase: Steering ---
            // 저속 추진 + 노즐 회전으로 목표 방향 정렬, dot >= threshold 되면 Homing 전환
            else if (EFlightPhase.Steering == m_phase)
            {
                Vector3 toTarget = (m_saveTargetPosition - transform.position).normalized;
                Quaternion targetRotation = Quaternion.LookRotation(toTarget);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, ROTATION_SPEED * Time.deltaTime);

                // 저속 전진 (노즐 추진력)
                float steeringSpeed = m_moduleData.projectileSpeed * 0.15f;
                transform.position += transform.forward * steeringSpeed * Time.deltaTime;

                float dot = Vector3.Dot(transform.forward, toTarget);
                if (dot >= COLD_LAUNCH_DOT_THRESHOLD)
                    m_phase = EFlightPhase.Homing;
            }
            // --- Phase: Homing ---
            // 목표 추적 유도 비행
            else
            {
                Vector3 targetDirection = (m_saveTargetPosition - transform.position).normalized;
                float dotProduct = Vector3.Dot(transform.forward, targetDirection);
                float applyDot = dotProduct * dotProduct;
                m_currentSpeed = m_moduleData.projectileSpeed * Mathf.Max(0.1f, applyDot);

                Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                Quaternion newRotation = Quaternion.RotateTowards(transform.rotation, targetRotation, ROTATION_SPEED * Time.deltaTime);
                transform.SetPositionAndRotation(transform.position + newRotation * Vector3.forward * m_currentSpeed * Time.deltaTime, newRotation);

                // 타겟 위치를 지나쳤으면 폭파
                Vector3 toTarget = m_saveTargetPosition - transform.position;
                if (Vector3.Dot(transform.forward, toTarget) < 0f)
                {
                    ReturnToPool();
                    yield break;
                }
            }

            // 이전 위치→현재 위치 raycast로 충돌 감지 (터널링 방지)
            Vector3 moveVec = transform.position - m_prevPosition;
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

    private void SetBurstSide(bool active)
    {
        m_burstUp.SetActive(active);
        m_burstDown.SetActive(active);
        m_burstLeft.SetActive(active);
        m_burstRight.SetActive(active);
    }

    private void SetBurstTail(bool active)
    {
        m_burstTail.SetActive(active);
    }

    // private static void SetParticle(ParticleSystem ps, bool active)
    // {
    //     if (ps == null) return;
    //     if (active) ps.Play();
    //     else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    // }

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
