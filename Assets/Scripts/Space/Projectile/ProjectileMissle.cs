// 미사일 발사체 - Rigidbody 물리 기반, 콜드런치(Eject→Steering→Homing) 3단계, 관성 있는 유도 비행
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class ProjectileMissile : ProjectileBase
{
    private enum EFlightPhase { Eject, Steering, Homing }

    private static int s_raycastMask = 0;
    private static readonly WaitForFixedUpdate s_waitFixedUpdate = new();

    private Rigidbody m_rb;
    private Vector3 m_saveTargetPosition;
    private Coroutine m_lifeCycleCoroutine;
    private float m_lifeTime;
    private const float MAX_LIFE_TIME = 10f;

    private const float EJECT_SPEED = 1f;
    private const float STEERING_ROTATION_SPEED = 160f;
    private const float HOMING_TURN_RATE = 4f;

    private const float COLD_LAUNCH_DOT_THRESHOLD = 0.85f;
    private const float BURST_TAIL_DOT_THRESHOLD = 0.95f;

    private ModuleData m_moduleData;
    private float m_initialFlightDuration;
    private EPoolName m_poolName;
    private Vector3 m_prevPosition;
    private EFlightPhase m_phase;
    private Vector3 m_prevLocalDir;

    [Header("Trail Particles")]
    [SerializeField] private GameObject m_burstTail;
    [SerializeField] private BurstNozzle m_burstUp;
    [SerializeField] private BurstNozzle m_burstDown;
    [SerializeField] private BurstNozzle m_burstLeft;
    [SerializeField] private BurstNozzle m_burstRight;

    private void Awake()
    {
        m_rb = GetComponent<Rigidbody>();
    }

    public void SetPoolName(EPoolName poolName) { m_poolName = poolName; }

    public override void InitializeProjectile(Transform firePointTransform, ModuleBase target, float damage, ModuleData moduleData, Color color, ModuleBase sourceModuleBase, Vector3 initialDirection)
    {
        base.InitializeProjectile(firePointTransform, target, damage, moduleData, color, sourceModuleBase, initialDirection);
        m_moduleData = moduleData;
        m_lifeTime = 0.0f;
        m_prevPosition = transform.position;
        m_initialFlightDuration = Random.Range(0.5f, 0.6f);

        m_phase = EFlightPhase.Eject;


        m_rb.linearVelocity = initialDirection.normalized * EJECT_SPEED;
        m_rb.angularVelocity = Vector3.zero;

        SetBurstSideAll(false);
        SetBurstTail(false);
        m_prevLocalDir = Vector3.zero;

        m_saveTargetPosition = target != null ? target.transform.position : firePointTransform.position + firePointTransform.forward * 50f;

        if (m_lifeCycleCoroutine != null) StopCoroutine(m_lifeCycleCoroutine);
        m_lifeCycleCoroutine = StartCoroutine(MissleLifeCycle());
    }

    private IEnumerator MissleLifeCycle()
    {
        while (true)
        {
            if (TickCommon() == false) yield break;

            switch (m_phase)
            {
                case EFlightPhase.Eject:    Phase_Eject();    break;
                case EFlightPhase.Steering: Phase_Steering(); break;
                case EFlightPhase.Homing:   Phase_Homing();   break;
            }

            if (CheckCollision() == true) yield break;
            yield return s_waitFixedUpdate;
        }
    }

    private bool TickCommon()
    {
        m_lifeTime += Time.deltaTime;
        if (m_lifeTime >= MAX_LIFE_TIME)
        {
            ReturnToPool(showHitEffect: false);
            return false;
        }
        if (m_target != null)
            m_saveTargetPosition = m_target.transform.position;
        return true;
    }

    private bool CheckCollision()
    {
        Vector3 moveVec = transform.position - m_prevPosition;
        if (s_raycastMask == 0)
            s_raycastMask = ~LayerMask.GetMask("Shield");

#if UNITY_EDITOR
        Debug.DrawRay(m_prevPosition, moveVec, Color.red, 0.5f);
#endif

        if (Physics.Raycast(m_prevPosition, moveVec.normalized, out RaycastHit hit, moveVec.magnitude, s_raycastMask))
        {
#if UNITY_EDITOR
            Debug.DrawRay(m_prevPosition, moveVec.normalized * hit.distance, Color.yellow, 1f);
            Debug.DrawLine(hit.point, hit.point + hit.normal * 0.3f, Color.cyan, 1f);
#endif
            SpaceShip hitShip = hit.collider.GetComponentInParent<SpaceShip>();
            if (hitShip != null && (m_sourceShip == null || hitShip.m_myFleet != m_sourceShip.m_myFleet))
            {
                hitShip.TakeDamage(m_damage);
                ReturnToPool(hitPosition: hit.point);
                return true;
            }
        }
        m_prevPosition = transform.position;
        return false;
    }

    // 발사 방향 고정 이동 → initialFlightDuration 경과 즉시 Steering 전환
    private void Phase_Eject()
    {
        if (m_lifeTime >= m_initialFlightDuration)
            m_phase = EFlightPhase.Steering;
    }

    // 추진 없이 방향 제어만 → dot >= threshold 되면 Homing 전환
    private void Phase_Steering()
    {
        Vector3 toTarget = (m_saveTargetPosition - transform.position).normalized;
        Quaternion newRot = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget), STEERING_ROTATION_SPEED * Time.deltaTime);
        m_rb.MoveRotation(newRot);
        UpdateBurstDirectional(toTarget);

        if (Vector3.Dot(transform.forward, toTarget) >= COLD_LAUNCH_DOT_THRESHOLD)
            m_phase = EFlightPhase.Homing;
    }

    // velocity lerp 유도 비행, dot >= threshold 시 메인 엔진 점화
    private void Phase_Homing()
    {
        Vector3 toTarget = (m_saveTargetPosition - transform.position).normalized;
        Quaternion newRot = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget), STEERING_ROTATION_SPEED * Time.deltaTime);
        m_rb.MoveRotation(newRot);
        UpdateBurstDirectional(toTarget);

        if (Vector3.Dot(transform.forward, toTarget) >= BURST_TAIL_DOT_THRESHOLD)
            SetBurstTail(true);

        if (m_burstTail.activeSelf == true)
        {
            Vector3 desiredVelocity = toTarget * m_moduleData.projectileSpeed;
            m_rb.linearVelocity = Vector3.Lerp(m_rb.linearVelocity, desiredVelocity, HOMING_TURN_RATE * Time.deltaTime);
        }

        if (Vector3.Dot(transform.forward, toTarget) < 0f)
            ReturnToPool();
    }

    // 회전 중 해당 노즐 ON/OFF, 정렬 완료 순간 반대 노즐 Pulse로 역추진 표현
    private void UpdateBurstDirectional(Vector3 toTarget)
    {
        Vector3 local = transform.InverseTransformDirection(toTarget);
        const float threshold = 0.15f;
        const float pulseDuration = 0.08f;

        // 정렬 완료 순간(threshold 초과→이하) 역추진 Pulse
        if (m_prevLocalDir.y < -threshold && local.y >= -threshold) m_burstDown.Pulse(pulseDuration);
        if (m_prevLocalDir.y >  threshold && local.y <=  threshold) m_burstUp.Pulse(pulseDuration);
        if (m_prevLocalDir.x >  threshold && local.x <=  threshold) m_burstRight.Pulse(pulseDuration);
        if (m_prevLocalDir.x < -threshold && local.x >= -threshold) m_burstLeft.Pulse(pulseDuration);

        // 회전 중이면 ON, 아니면 OFF (펄스 중이면 BurstNozzle이 무시)
        if (local.y < -threshold) m_burstUp.TurnOn();    else m_burstUp.TurnOff();
        if (local.y >  threshold) m_burstDown.TurnOn();  else m_burstDown.TurnOff();
        if (local.x >  threshold) m_burstLeft.TurnOn();  else m_burstLeft.TurnOff();
        if (local.x < -threshold) m_burstRight.TurnOn(); else m_burstRight.TurnOff();

        m_prevLocalDir = local;
    }

    private void SetBurstSideAll(bool active)
    {
        if (active == true)
        {
            m_burstUp.TurnOn();
            m_burstDown.TurnOn();
            m_burstLeft.TurnOn();
            m_burstRight.TurnOn();
        }
        else
        {
            m_burstUp.ResetNozzle();
            m_burstDown.ResetNozzle();
            m_burstLeft.ResetNozzle();
            m_burstRight.ResetNozzle();
        }
    }

    private void SetBurstTail(bool active)
    {
        m_burstTail.SetActive(active);
    }

    public void ReturnToPool(bool showHitEffect = true, Vector3 hitPosition = default)
    {
        if (m_lifeCycleCoroutine != null)
        {
            StopCoroutine(m_lifeCycleCoroutine);
            m_lifeCycleCoroutine = null;
        }

        if (showHitEffect && gameObject.activeInHierarchy)
        {
            Vector3 effectPos = hitPosition == default ? transform.position : hitPosition;
            ObjectManager.Instance.m_poolManager.GetEffect_Play_AutoReturn(EPoolName.EFFECT_EXPLOSION_MISSILE_SMALL, effectPos);
        }

        ObjectManager.Instance.m_poolManager.Return(m_poolName, this);
    }
}
