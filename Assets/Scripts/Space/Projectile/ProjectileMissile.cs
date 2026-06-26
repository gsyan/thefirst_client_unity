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
    private MeshRenderer m_meshRenderer;
    private float m_halfLength = 0.5f; // 미사일 절반 길이 (Awake에서 MeshRenderer bounds로 계산)
    private Vector3 m_saveTargetPosition;
    private Coroutine m_lifeCycleCoroutine;
    private float m_lifeTime;
    private const float MAX_LIFE_TIME = 10f;

    private float m_ejectSpeed = 1f;
    private const float STEERING_ROTATION_SPEED          = 160f;
    private const float STEERING_ROTATION_SPEED_INTERCEPT = 360f; // 미사일 요격 시 빠른 선회
    private float m_currentSteeringSpeed = STEERING_ROTATION_SPEED;

    private const float COLD_LAUNCH_DOT_THRESHOLD = 0.85f;
    private const float BURST_TAIL_DOT_THRESHOLD = 0.95f;
    private const float STEERING_BRAKE_RATE = 8f; // 콜드런치 속도 감속 계수

    private float m_missileSpeed;
    private float m_silenceTime;
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
        m_meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (m_meshRenderer != null)
            m_halfLength = m_meshRenderer.bounds.size.z * 0.5f;
    }

    public void SetPoolName(EPoolName poolName) { m_poolName = poolName; }

    public void InitializeProjectileMissile(Transform firePointTransform, Transform target, DamageInfo damageInfo, ModuleData moduleData,
     ModuleBase sourceModuleBase, Vector3 initialDirection, float ejectSpeed, float explosionMultiplier = 1f)
    {
        SetCommonData(firePointTransform, target, damageInfo, sourceModuleBase);
        m_missileSource = (sourceModuleBase is ModuleHanger) ? EMissileSource.Aircraft : EMissileSource.Ship;

        // 함재기 미사일은 요격 대상에서 제외
        if (m_missileSource != EMissileSource.Aircraft)
        {
            bool isEnemy = m_sourceShip != null && m_sourceShip.m_ownerFleet != null && m_sourceShip.m_ownerFleet.IsEnemy;
            ObjectManager.Instance.RegisterMissile(this, isEnemy);
        }
        m_missileSpeed = moduleData.projectileSpeed;
        m_silenceTime  = moduleData.silenceTime;
        m_splashRadius = moduleData.splashRadius * explosionMultiplier;
        m_ejectSpeed = ejectSpeed;
        m_lifeTime = 0.0f;
        m_prevPosition = transform.position;

        m_phase = EFlightPhase.Eject;

        m_rb.linearVelocity = initialDirection.normalized * m_ejectSpeed * 5;
        m_rb.angularVelocity = Vector3.zero;

        SetBurstSideAll(false);
        SetBurstTail(false);
        m_prevLocalDir = Vector3.zero;

        m_saveTargetPosition = target != null ? target.position : firePointTransform.position + firePointTransform.forward * 50f;

        // 타겟이 미사일이면 빠른 선회 적용
        bool targetIsMissile = target != null && target.GetComponent<ProjectileMissile>() != null;
        m_currentSteeringSpeed = targetIsMissile == true ? STEERING_ROTATION_SPEED_INTERCEPT : STEERING_ROTATION_SPEED;

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
        if (m_target != null && m_target.gameObject.activeInHierarchy == true)
            m_saveTargetPosition = m_target.position;
        else
        {
            m_target = FindNewTarget();
            // 함선으로 타겟 전환 시 일반 선회 속도로 복원
            m_currentSteeringSpeed = STEERING_ROTATION_SPEED;
        }
        return true;
    }

    private Transform FindNewTarget()
    {
        if (m_sourceShip == null) return null;

        bool isEnemySource = m_sourceShip.m_ownerFleet != null && m_sourceShip.m_ownerFleet.IsEnemy;
        Vector3 myPos = transform.position;
        Transform nearest = null;
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

    private void SearchFleetForNearestBody(SpaceFleet fleet, Vector3 myPos, ref Transform nearest, ref float nearestSqrDist)
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
                    nearest = body.transform;
                }
            }
        }
    }

    private static readonly Collider[]   s_overlapResults = new Collider[32];
    private static readonly RaycastHit[] s_raycastHits    = new RaycastHit[16];

    private bool CheckCollision()
    {
        Vector3 moveVec = transform.position - m_prevPosition;
        if (moveVec.sqrMagnitude < 0.0001f) return false;

        if (s_raycastMask == 0)
            s_raycastMask = ~LayerMask.GetMask("Shield");

        int hitCount = Physics.RaycastNonAlloc(m_prevPosition, moveVec.normalized, s_raycastHits, moveVec.magnitude, s_raycastMask, QueryTriggerInteraction.Collide);

        // 자기 자신 제외 + 가장 가까운 히트 선택
        RaycastHit bestHit = default;
        float bestDist = float.MaxValue;
        bool hasBestHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit h = s_raycastHits[i];
            ProjectileMissile selfCheck = h.collider.GetComponentInParent<ProjectileMissile>();
            if (selfCheck == this) continue;
            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                bestHit = h;
                hasBestHit = true;
            }
        }

        if (hasBestHit == true)
        {
            RaycastHit hit = bestHit;

            // 적 미사일 요격 체크 (함선 충돌보다 먼저)
            ProjectileMissile hitMissile = hit.collider.GetComponentInParent<ProjectileMissile>();
            if (hitMissile != null)
            {
                bool isMysideFriendly = m_sourceShip != null && m_sourceShip.m_ownerFleet != null && m_sourceShip.m_ownerFleet.IsEnemy == false;
                bool hitMissileIsEnemy = isMysideFriendly
                    ? ObjectManager.Instance.m_enemyMissiles.Contains(hitMissile)
                    : ObjectManager.Instance.m_friendlyMissiles.Contains(hitMissile);
                if (hitMissileIsEnemy == true)
                {
                    SoundManager.Instance.PlayFX(EFx.Explosion_Missile, hit.point);
                    hitMissile.ReturnToPool(hitPosition: hit.point);
                    ReturnToPool(hitPosition: hit.point);
                    return true;
                }
            }

            SpaceShip hitShip = hit.collider.GetComponentInParent<SpaceShip>();
            if (hitShip != null && (m_sourceShip == null || hitShip.m_ownerFleet != m_sourceShip.m_ownerFleet))
            {
                if (m_splashRadius > 0f)
                    ApplySplashDamage(hit.point, hitShip);
                else
                    hitShip.TakeDamage(m_damageInfo, hit.point);

                hitShip.ApplySilenceToRandomModule(m_silenceTime);

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
        directHitShip.TakeDamage(m_damageInfo, center);

        int count = Physics.OverlapSphereNonAlloc(center, m_splashRadius, s_overlapResults, s_raycastMask);
        for (int i = 0; i < count; i++)
        {
            SpaceShip ship = s_overlapResults[i].GetComponentInParent<SpaceShip>();
            if (ship == null) continue;
            if (ship == directHitShip) continue;
            if (m_sourceShip != null && ship.m_ownerFleet == m_sourceShip.m_ownerFleet) continue;

            float dist = Vector3.Distance(center, s_overlapResults[i].transform.position);
            float splashRatio = 1.0f - 0.5f * (dist / m_splashRadius);
            // 폭발 중심에서 콜라이더 표면까지의 가장 가까운 지점 = 실제 피탄 지점
            Vector3 splashHitPoint = s_overlapResults[i].ClosestPoint(center);
            DamageInfo splashDamageInfo = new DamageInfo
            {
                baseDamage       = m_damageInfo.baseDamage * splashRatio,
                attackMultiplier = m_damageInfo.attackMultiplier,
            };
            ship.TakeDamage(splashDamageInfo, splashHitPoint);
        }
    }

    // 꼬리가 발사구를 벗어났으면 Steering 전환
    private void Phase_Eject()
    {
        if (m_firePointTransform == null)
        {
            m_phase = EFlightPhase.Steering;
            return;
        }
        Vector3 fromLaunch  = transform.position - m_firePointTransform.position;
        float   exitedDist  = Vector3.Dot(fromLaunch, transform.forward);
        if (exitedDist >= m_halfLength)
            m_phase = EFlightPhase.Steering;
    }

    // 추진 없이 방향 제어만 → dot >= threshold 되면 Homing 전환
    private void Phase_Steering()
    {
        Vector3 toTarget = (m_saveTargetPosition - transform.position).normalized;
        Quaternion newRot = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget), m_currentSteeringSpeed * Time.deltaTime);
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
        Quaternion newRot = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toTarget), m_currentSteeringSpeed * Time.deltaTime);
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
        {
            // 타겟이 미사일이었으면 함선으로 재탐색, 그 외는 소멸
            bool targetIsMissile = m_target != null && m_target.GetComponent<ProjectileMissile>() != null;
            if (targetIsMissile == true)
                m_target = FindNewTarget();
            else
                ReturnToPool();
        }
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
        if (gameObject.activeInHierarchy == false) return;

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

        ObjectManager.Instance.UnregisterMissile(this);
        ObjectManager.Instance.m_poolManager.Return(m_poolName, this);
    }
}
