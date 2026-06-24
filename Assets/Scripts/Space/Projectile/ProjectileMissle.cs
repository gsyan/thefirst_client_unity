// 미사일 발사체 - Rigidbody 물리 기반, 콜드런치(Eject→Steering→Homing) 3단계, 관성 있는 유도 비행
using UnityEngine;
using System.Collections;
using System.Collections.Generic;


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

    private float m_ejectSpeed = 1f;
    private const float STEERING_ROTATION_SPEED = 160f;

    private const float COLD_LAUNCH_DOT_THRESHOLD = 0.85f;
    private const float BURST_TAIL_DOT_THRESHOLD = 0.95f;
    private const float STEERING_BRAKE_RATE = 4f; // 콜드런치 속도 감속 계수

    private float m_missileSpeed;
    private float m_initialFlightDuration;
    private EPoolName m_poolName;
    private Vector3 m_prevPosition;
    private EFlightPhase m_phase;
    private Vector3 m_prevLocalDir;
    private float m_splashRadius;
    private EMissileSource m_missileSource;

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

    public void InitializeProjectileMissile(Transform firePointTransform, ModuleBase target, float damage, ModuleData moduleData,
     ModuleBase sourceModuleBase, Vector3 initialDirection, float ejectSpeed)
    {
        SetCommonData(firePointTransform, target, damage, sourceModuleBase);
        m_missileSource = (sourceModuleBase is ModuleHanger) ? EMissileSource.Aircraft : EMissileSource.Ship;
        m_missileSpeed = moduleData.projectileSpeed;
        m_splashRadius = moduleData.splashRadius;
        m_ejectSpeed = ejectSpeed;
        m_lifeTime = 0.0f;
        m_prevPosition = transform.position;
        m_initialFlightDuration = Random.Range(0.05f, 0.05f);

        m_phase = EFlightPhase.Eject;

        m_rb.linearVelocity = initialDirection.normalized * m_ejectSpeed * 5;
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
        else
            m_target = FindNewTarget();
        return true;
    }

    private ModuleBase FindNewTarget()
    {
        if (m_sourceShip == null) return null;

        bool isEnemySource = m_sourceShip.m_ownerFleet != null && m_sourceShip.m_ownerFleet.IsEnemy;
        Vector3 myPos = transform.position;
        ModuleBase nearest = null;
        float nearestSqrDist = float.MaxValue;

        if (isEnemySource)
        {
            SpaceFleet playerFleet = ObjectManager.Instance.m_myFleet;
            if (playerFleet == null || playerFleet.IsFleetAlive() == false) return null;
            SearchFleetForNearestBody(playerFleet, myPos, ref nearest, ref nearestSqrDist);
        }
        else
        {
            List<SpaceFleet> enemyFleets = ObjectManager.Instance.m_enemyFleets;
            for (int i = 0; i < enemyFleets.Count; i++)
            {
                SpaceFleet fleet = enemyFleets[i];
                if (fleet == null || fleet.IsFleetAlive() == false) continue;
                SearchFleetForNearestBody(fleet, myPos, ref nearest, ref nearestSqrDist);
            }
        }
        return nearest;
    }

    private void SearchFleetForNearestBody(SpaceFleet fleet, Vector3 myPos, ref ModuleBase nearest, ref float nearestSqrDist)
    {
        for (int i = 0; i < fleet.m_ships.Count; i++)
        {
            SpaceShip ship = fleet.m_ships[i];
            if (ship == null || ship.IsAlive() == false) continue;
            for (int j = 0; j < ship.m_moduleBodys.Count; j++)
            {
                ModuleBody body = ship.m_moduleBodys[j];
                if (body == null || body.m_health <= 0) continue;
                float sqrDist = (body.transform.position - myPos).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = body;
                }
            }
        }
    }

    private static readonly Collider[] s_overlapResults = new Collider[32];

    private bool CheckCollision()
    {
        Vector3 moveVec = transform.position - m_prevPosition;
        if (s_raycastMask == 0)
            s_raycastMask = ~LayerMask.GetMask("Shield");

        if (Physics.Raycast(m_prevPosition, moveVec.normalized, out RaycastHit hit, moveVec.magnitude, s_raycastMask))
        {
            SpaceShip hitShip = hit.collider.GetComponentInParent<SpaceShip>();
            if (hitShip != null && (m_sourceShip == null || hitShip.m_ownerFleet != m_sourceShip.m_ownerFleet))
            {
                if (m_splashRadius > 0f)
                    ApplySplashDamage(hit.point, hitShip);
                else
                    hitShip.TakeDamage(m_damage, hit.point);

                EFx hitFx = (m_missileSource == EMissileSource.Aircraft) ? EFx.Explosion_Aircraft_Missile : EFx.Explosion_Missile;
                SoundManager.Instance.PlayFX(hitFx, hit.point);

                ReturnToPool(hitPosition: hit.point);
                return true;
            }
        }
        m_prevPosition = transform.position;
        return false;
    }

    // 직격 대상 100%, 범위 내 나머지는 거리 비례 선형 감쇄(100%~50%)
    private void ApplySplashDamage(Vector3 center, SpaceShip directHitShip)
    {
        // 직격 함선은 미사일 실제 충돌 지점(center == hit.point)을 그대로 사용
        directHitShip.TakeDamage(m_damage, center);

        int count = Physics.OverlapSphereNonAlloc(center, m_splashRadius, s_overlapResults, s_raycastMask);
        for (int i = 0; i < count; i++)
        {
            SpaceShip ship = s_overlapResults[i].GetComponentInParent<SpaceShip>();
            if (ship == null) continue;
            if (ship == directHitShip) continue;
            if (m_sourceShip != null && ship.m_ownerFleet == m_sourceShip.m_ownerFleet) continue;

            float dist = Vector3.Distance(center, s_overlapResults[i].transform.position);
            float ratio = 1.0f - 0.5f * (dist / m_splashRadius);
            // 폭발 중심에서 콜라이더 표면까지의 가장 가까운 지점 = 실제 피탄 지점
            Vector3 splashHitPoint = s_overlapResults[i].ClosestPoint(center);
            ship.TakeDamage(m_damage * ratio, splashHitPoint);
        }
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

        m_rb.linearVelocity = Vector3.Lerp(m_rb.linearVelocity, Vector3.zero, STEERING_BRAKE_RATE * Time.deltaTime);

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
            float currentSpeed = m_rb.linearVelocity.magnitude;
            m_rb.linearVelocity = toTarget * (currentSpeed + m_missileSpeed * Time.deltaTime);
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
            EffectBase effect = ObjectManager.Instance.m_poolManager.Get<EffectBase>(EPoolName.EFFECT_EXPLOSION_MISSILE_SMALL);
            effect.transform.position = effectPos;
            effect.transform.localScale = m_splashRadius > 0f
                ? Vector3.one * (1f + m_splashRadius * 0.1f)
                : Vector3.one;
            effect.PlayEffect();
        }

        ObjectManager.Instance.m_poolManager.Return(m_poolName, this);
    }
}
