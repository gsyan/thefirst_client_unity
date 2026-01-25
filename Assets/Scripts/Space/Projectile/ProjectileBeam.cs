using UnityEngine;
using System.Collections;

public class ProjectileBeam : ProjectileBase
{
    [SerializeField] private LineRenderer m_lineRenderer;
    [SerializeField] private float m_beamWidth = 0.1f;
    [SerializeField] private float m_beamSpeed = 20f;
    
    [SerializeField] private Material m_beamMaterialPrefab;
    [SerializeField] private Color m_beamColor = Color.cyan;
    [SerializeField] private float m_uvScrollSpeed = 2f;
    [SerializeField] private ParticleSystem m_scatterParticle; // 빔 소멸 시 흩어지는 파티클
    [SerializeField] private int m_scatterParticleCount = 20; // 흩어지는 파티클 개수
    private EffectBase m_headEffect;

    
    private Vector3 m_direction;
    private Vector3 m_beamHeadPos;
    private Vector3 m_beamTailPos;
    private float m_toalBeamLength;
    
    private SpaceShip m_currentTarget = null;
    
    private Material m_beamMaterial;
    private float m_uvOffset;
    private Coroutine m_lifeCycleCoroutine;
    private float m_lifeTime;
    private const float MAX_LIFE_TIME = 5f;
    private float m_hitEffectTime;
    private static readonly WaitForSeconds s_scatterWait = new WaitForSeconds(0.5f);

    [SerializeField] private float m_beamHoldTime = 0.3f; // 히트 후 빔이 유지되는 시간
    [SerializeField] private float m_beamDissolveTime = 0.4f; // 빔이 사라지는 시간
    [SerializeField] private int m_beamSegments = 10; // dissolve용 세그먼트 수

    public override void InitializeProjectile(Transform firePointTransform, ModuleBase target, float damage, ModuleData moduleData,
                          Color color, ModuleBase sourceModuleBase)
    {
        base.InitializeProjectile(firePointTransform, target, damage, moduleData, color, sourceModuleBase);
        
        m_beamHeadPos = m_firePointTransform.position;
        m_beamTailPos = m_firePointTransform.position;
        m_beamWidth = moduleData.m_projectileWidth;
        m_beamSpeed = moduleData.m_projectileSpeed;        
        m_beamColor = color;        
        

        if (m_lineRenderer == null)
            m_lineRenderer = GetComponent<LineRenderer>();

        if (m_lineRenderer == null)
            m_lineRenderer = gameObject.AddComponent<LineRenderer>();

        m_lineRenderer.positionCount = 2;
        m_lineRenderer.startWidth = m_beamWidth;
        m_lineRenderer.endWidth = m_beamWidth/* * 0.8f*/;
        m_lineRenderer.enabled = true;

        if (m_beamMaterialPrefab != null)
        {
            m_beamMaterial = new Material(m_beamMaterialPrefab);
            m_beamMaterial.color = m_beamColor;
            m_lineRenderer.material = m_beamMaterial;
        }
        else if (m_lineRenderer.material != null)
        {
            m_beamMaterial = m_lineRenderer.material;
            m_beamMaterial.color = m_beamColor;
        }

        m_toalBeamLength = 0f;
        m_currentTarget = null;
        m_uvOffset = 0f;
        m_lifeTime = 0f;
        m_hitEffectTime = Time.time;

        if (m_headEffect == null)
        {
            m_headEffect = ObjectManager.Instance.m_poolManager.Get<EffectBase>(EPoolName.EFFECT_BEAM_HEAD);
            m_headEffect.Play();
            m_headEffect.transform.position = m_firePointTransform.position;
        }
        
        if (m_lifeCycleCoroutine != null)
            StopCoroutine(m_lifeCycleCoroutine);

        m_lifeCycleCoroutine = StartCoroutine(BeamLifeCycle());
    }

    private const int m_layerShield = 13;
    private LayerMask m_layerMaskShield = 1 << m_layerShield;

    //빠르게 목표까지 도달 → 데미지 → 흩어지며 소멸
    private IEnumerator BeamLifeCycle()
    {
        Vector3 targetPosition = m_target != null ? m_target.transform.position : m_firePointTransform.position + m_firePointTransform.forward * 100f;
        m_beamTailPos = m_firePointTransform.position;
        m_direction = (targetPosition - m_beamTailPos).normalized;
        float maxDistance = Vector3.Distance(m_beamTailPos, targetPosition) + 10f; // 여유 거리

        LayerMask pickMask = ~m_layerMaskShield;
        Vector3 finalHitPoint = targetPosition;
        SpaceShip hitTarget = null;

        // Raycast로 충돌 지점 미리 계산
        if (Physics.Raycast(m_beamTailPos, m_direction, out RaycastHit hit, maxDistance, pickMask))
        {
            hitTarget = hit.collider.GetComponentInParent<SpaceShip>();
            if (hitTarget != null && m_sourceShip != null)
            {
                SpaceFleet myFleet = m_sourceShip.GetComponentInParent<SpaceFleet>();
                SpaceFleet targetFleet = hitTarget.GetComponentInParent<SpaceFleet>();
                if (myFleet != null && targetFleet != null && myFleet == targetFleet)
                    hitTarget = null; // 아군이면 무시
            }
            finalHitPoint = hit.point;
        }

        // 1단계: 빔 연장 (빠르게 목표까지)
        float totalDistance = Vector3.Distance(m_beamTailPos, finalHitPoint);
        float currentLength = 0f;

        while (currentLength < totalDistance)
        {
            m_lifeTime += Time.deltaTime;
            if (m_lifeTime >= MAX_LIFE_TIME)
            {
                yield return StartCoroutine(BeamScatterAndReturn());
                yield break;
            }

            float frameMove = m_beamSpeed * Time.deltaTime;
            currentLength = Mathf.Min(currentLength + frameMove, totalDistance);
            m_beamHeadPos = m_beamTailPos + m_direction * currentLength;

            // LineRenderer 업데이트
            m_lineRenderer.SetPosition(0, m_beamHeadPos);
            m_lineRenderer.SetPosition(1, m_beamTailPos);

            if (m_headEffect != null)
                m_headEffect.transform.position = m_beamHeadPos;

            // UV 스크롤
            if (m_beamMaterial != null)
            {
                m_uvOffset += m_uvScrollSpeed * Time.deltaTime;
                m_beamMaterial.SetTextureOffset("_MainTex", new Vector2(m_uvOffset, 0));
            }

            yield return null;
        }

        // 2단계: 히트 처리
        if (hitTarget != null)
        {
            hitTarget.TakeDamage(m_damage);
            ObjectManager.Instance.m_poolManager.GetEffect_Play_AutoReturn(EPoolName.EFFECT_BEAM_HIT, finalHitPoint);
        }

        // 3단계: 흩어지며 소멸
        yield return StartCoroutine(BeamScatterAndReturn());
    }

    // 빔이 흩어지며 사라지는 코루틴
    private IEnumerator BeamScatterAndReturn()
    {
        // 헤드 이펙트 먼저 제거
        if (m_headEffect != null)
        {
            m_headEffect.ReturnToPool();
            m_headEffect = null;
        }

        // 1단계: 히트 후 빔 유지 (잠시 머무름)
        float holdTimer = 0f;
        while (holdTimer < m_beamHoldTime)
        {
            holdTimer += Time.deltaTime;
            yield return null;
        }

        // 2단계: 빔 dissolve + 파티클 흩어짐
        EmitScatterParticles(m_beamTailPos, m_beamHeadPos);
        yield return StartCoroutine(DissolveBeam());

        // LineRenderer 비활성화
        m_lineRenderer.enabled = false;

        // 파티클이 완전히 사라질 때까지 대기
        yield return s_scatterWait;

        ReturnToPool();
    }

    // 빔이 부분부분 타들어가며 사라지는 효과
    private IEnumerator DissolveBeam()
    {
        Vector3 start = m_beamTailPos;
        Vector3 end = m_beamHeadPos;

        // LineRenderer를 여러 세그먼트로 설정
        m_lineRenderer.positionCount = m_beamSegments + 1;
        float[] segmentAlpha = new float[m_beamSegments + 1];
        for (int i = 0; i <= m_beamSegments; i++)
        {
            float t = (float)i / m_beamSegments;
            m_lineRenderer.SetPosition(i, Vector3.Lerp(start, end, t));
            segmentAlpha[i] = 1f;
        }

        // 각 세그먼트가 사라지는 순서 (랜덤)
        int[] dissolveOrder = new int[m_beamSegments + 1];
        for (int i = 0; i <= m_beamSegments; i++) dissolveOrder[i] = i;
        for (int i = dissolveOrder.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (dissolveOrder[i], dissolveOrder[j]) = (dissolveOrder[j], dissolveOrder[i]);
        }

        // Width curve로 세그먼트별 두께 조절
        float dissolveTimer = 0f;
        AnimationCurve widthCurve = new AnimationCurve();

        while (dissolveTimer < m_beamDissolveTime)
        {
            dissolveTimer += Time.deltaTime;
            float progress = dissolveTimer / m_beamDissolveTime;

            // 진행도에 따라 세그먼트들을 순차적으로 사라지게
            int segmentsToDissolve = Mathf.FloorToInt(progress * (m_beamSegments + 1));
            for (int i = 0; i < segmentsToDissolve && i < dissolveOrder.Length; i++)
            {
                int segIdx = dissolveOrder[i];
                segmentAlpha[segIdx] = Mathf.Max(0f, segmentAlpha[segIdx] - Time.deltaTime * 5f);
            }

            // Width curve 업데이트
            widthCurve.keys = new Keyframe[0];
            for (int i = 0; i <= m_beamSegments; i++)
            {
                float t = (float)i / m_beamSegments;
                widthCurve.AddKey(t, m_beamWidth * segmentAlpha[i]);
            }
            m_lineRenderer.widthCurve = widthCurve;

            yield return null;
        }

        // 완전히 투명하게
        m_lineRenderer.startWidth = 0f;
        m_lineRenderer.endWidth = 0f;
    }

    // 빔 경로를 따라 파티클 흩뿌리기
    [SerializeField] private float m_scatterParticleSize = 5f;
    [SerializeField] private float m_scatterParticleSpeed = 10f;
    private void EmitScatterParticles(Vector3 start, Vector3 end)
    {
        if (m_scatterParticle == null) return;

        var emitParams = new ParticleSystem.EmitParams();
        Vector3 beamDir = (end - start).normalized;

        // 빔에 수직인 평면에서 랜덤 방향 계산용
        Vector3 perpendicular = Vector3.Cross(beamDir, Vector3.up);
        if (perpendicular.sqrMagnitude < 0.01f)
            perpendicular = Vector3.Cross(beamDir, Vector3.right);
        perpendicular.Normalize();

        for (int i = 0; i < m_scatterParticleCount; i++)
        {
            float t = (float)i / m_scatterParticleCount;
            emitParams.position = Vector3.Lerp(start, end, t);
            emitParams.startColor = m_beamColor;
            emitParams.startSize = m_scatterParticleSize * Random.Range(0.5f, 1.5f);

            // 빔 방향에서 수직으로 흩어지게
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 scatterDir = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, beamDir) * perpendicular;
            emitParams.velocity = scatterDir * m_scatterParticleSpeed * Random.Range(0.5f, 1.5f);

            m_scatterParticle.Emit(emitParams, 1);
        }
    }

    // source 모듈(발사한 무기)의 콜라이더를 제외한 유효한 RaycastHit 반환
    private static readonly RaycastHit[] s_raycastHitBuffer = new RaycastHit[16];
    private RaycastHit? GetValidRaycastHit(Vector3 origin, Vector3 direction, float distance)
    {
        int hitCount = Physics.RaycastNonAlloc(origin, direction, s_raycastHitBuffer, distance);
        if (hitCount == 0) return null;

        // 거리순 정렬
        System.Array.Sort(s_raycastHitBuffer, 0, hitCount, s_raycastHitComparer);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = s_raycastHitBuffer[i];

            // source 모듈 자신의 콜라이더인지 확인
            if (m_sourceModuleBase != null)
            {
                // hit된 오브젝트가 source 모듈의 자식인지 확인
                if (hit.collider.transform.IsChildOf(m_sourceModuleBase.transform))
                    continue;
            }

            // 유효한 hit 발견
            return hit;
        }

        return null;
    }

    private static readonly RaycastHitDistanceComparer s_raycastHitComparer = new RaycastHitDistanceComparer();
    private class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    private void ReturnToPool()
    {
        if (m_lifeCycleCoroutine != null)
        {
            StopCoroutine(m_lifeCycleCoroutine);
            m_lifeCycleCoroutine = null;
        }

        if (m_headEffect != null)
        {
            m_headEffect.ReturnToPool();
            m_headEffect = null;
        }

        m_lineRenderer.enabled = false;
        ObjectManager.Instance.m_poolManager.Return(EPoolName.PROJECTILE_BEAM, this);
    }
}
