// 즉시 발사 빔 투사체 - F3D 머티리얼 + 틱 데미지, 타겟 사망 시 짧은 유지 후 종료
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class ProjectileBeamInstant : ProjectileBase
{
    private LineRenderer m_lineRenderer;

    // 머즐/임팩트 파티클 자식 Transform (없어도 동작)
    [SerializeField] private Transform m_rayMuzzle;
    [SerializeField] private Transform m_rayImpact;

    [SerializeField] private float m_holdTime = 1.0f;
    [SerializeField] private float m_tickInterval = 0.1f;
    [SerializeField] private float m_targetDeadDelay = 0.1f;

    // UV 애니메이션 (F3DBeam 동일 로직)
    [SerializeField] private float m_uvFrequency = 6f;
    [SerializeField] private float m_uvAmplitude = 0.05f;

    private float m_uvTime;
    private MaterialPropertyBlock m_mpb;
    private static readonly int k_offsetId = Shader.PropertyToID("_Offset");

    private Coroutine m_lifeCycleCoroutine;

    private void Awake()
    {
        m_lineRenderer = GetComponent<LineRenderer>();
        m_mpb = new MaterialPropertyBlock();
    }

    public override void InitializeProjectile(Transform firePointTransform, ModuleBase target, float damage,
                                              ModuleData moduleData, Color color, ModuleBase sourceModuleBase, Vector3 initialDirection)
    {
        base.InitializeProjectile(firePointTransform, target, damage, moduleData, color, sourceModuleBase, initialDirection);

        m_uvTime = Random.Range(0f, 100f);

        m_lineRenderer.useWorldSpace = true;
        m_lineRenderer.positionCount = 2;
        m_lineRenderer.enabled = true;

        if (m_lifeCycleCoroutine != null) StopCoroutine(m_lifeCycleCoroutine);
        m_lifeCycleCoroutine = StartCoroutine(BeamLifeCycle());
    }

    private IEnumerator BeamLifeCycle()
    {
        // 아군 체크 후 데미지 대상 SpaceShip 확정
        SpaceShip targetShip = m_target != null ? m_target.GetComponentInParent<SpaceShip>() : null;
        if (targetShip != null && m_sourceShip != null)
        {
            SpaceFleet myFleet = m_sourceShip.GetComponentInParent<SpaceFleet>();
            SpaceFleet targetFleet = targetShip.GetComponentInParent<SpaceFleet>();
            if (myFleet != null && targetFleet != null && myFleet == targetFleet)
                targetShip = null;
        }

        float tickDamage = m_holdTime > 0f ? m_damage * (m_tickInterval / m_holdTime) : m_damage;
        float elapsed = 0f;
        float tickAccum = 0f;

        while (elapsed < m_holdTime)
        {
            if (m_target == null || m_target.gameObject.activeInHierarchy == false)
            {
                yield return StartCoroutine(TargetDeadDelay());
                yield break;
            }

            elapsed += Time.deltaTime;
            tickAccum += Time.deltaTime;

            UpdateBeamPositions();
            UpdateUvAnimation();

            if (tickAccum >= m_tickInterval)
            {
                tickAccum -= m_tickInterval;
                if (targetShip != null)
                    targetShip.TakeDamage(tickDamage);
            }

            yield return null;
        }

        ReturnToPool();
    }

    private IEnumerator TargetDeadDelay()
    {
        float t = 0f;
        while (t < m_targetDeadDelay)
        {
            t += Time.deltaTime;
            UpdateUvAnimation();
            yield return null;
        }
        ReturnToPool();
    }

    private void UpdateBeamPositions()
    {
        if (m_firePointTransform == null || m_target == null) return;

        Vector3 origin = m_firePointTransform.position;
        Vector3 end = m_target.transform.position;

        m_lineRenderer.SetPosition(0, origin);
        m_lineRenderer.SetPosition(1, end);

        if (m_rayMuzzle != null) m_rayMuzzle.position = origin;
        if (m_rayImpact != null) m_rayImpact.position = end;
    }

    private void UpdateUvAnimation()
    {
        m_uvTime += Time.deltaTime;
        float v = 0.5f + Mathf.Sin(m_uvTime * m_uvFrequency) * m_uvAmplitude;
        m_lineRenderer.GetPropertyBlock(m_mpb);
        m_mpb.SetVector(k_offsetId, new Vector4(v, 0f, 0f, 0f));
        m_lineRenderer.SetPropertyBlock(m_mpb);
    }

    public void ReturnToPool()
    {
        if (m_lifeCycleCoroutine != null)
        {
            StopCoroutine(m_lifeCycleCoroutine);
            m_lifeCycleCoroutine = null;
        }

        m_lineRenderer.enabled = false;
        ObjectManager.Instance.m_poolManager.Return(EPoolName.PROJECTILE_BEAM_INSTANT, this);
    }
}
