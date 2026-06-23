using UnityEngine;
using System.Collections;

public class ProjectileBeam : ProjectileBase
{
    private LineRenderer m_lineRenderer;
    [SerializeField] private float m_beamWidth = 0.1f;
    [SerializeField] private float m_beamSpeed = 20f;
    
    [SerializeField] private Color m_beamColor = Color.cyan;
    [SerializeField] private float m_uvScrollSpeed = 2f;
    [SerializeField] private ParticleSystem m_scatterParticle; // 빔 소멸 시 흩어지는 파티클
    [SerializeField] private int m_scatterParticleCount = 20; // 흩어지는 파티클 개수

    private EffectBase m_headEffect;
    private EffectBase m_muzzleEffect;
    private EffectBase m_hitEffect;

    
    private Vector3 m_direction;
    private Vector3 m_beamHeadPos;
    private Vector3 m_beamTailPos;
    
    // MaterialPropertyBlock으로 인스턴스별 색상/UV 제어 (new Material() 대체)
    private MaterialPropertyBlock m_mpb;
    private static readonly int k_colorId = Shader.PropertyToID("_Color");
    private static readonly int k_mainTexSTId = Shader.PropertyToID("_MainTex_ST");

    private float m_uvOffset;
    private Coroutine m_lifeCycleCoroutine;
    private float m_lifeTime;
    private const float MAX_LIFE_TIME = 5f;
    //private static readonly WaitForSeconds s_scatterWait = new WaitForSeconds(0.5f);

    [SerializeField] private float m_beamHoldTime = 0.3f; // 히트 후 빔이 유지되는 시간
    [SerializeField] private float m_beamDissolveTime = 0.4f; // 빔이 사라지는 시간
    [SerializeField] private int m_beamSegments = 10; // dissolve용 세그먼트 수

    // Dissolve용 사전 할당 (스폰마다 new 방지)
    private float[] m_segmentAlpha;
    private int[] m_dissolveOrder;
    private AnimationCurve m_widthCurve;
    // 풀 재사용 시 travel 단계 widthCurve 초기화용 (2키 flat 커브)
    private AnimationCurve m_flatWidthCurve;

    private void Awake()
    {
        m_mpb = new MaterialPropertyBlock();
        m_segmentAlpha = new float[m_beamSegments + 1];
        m_dissolveOrder = new int[m_beamSegments + 1];
        m_widthCurve = new AnimationCurve();
        for (int i = 0; i <= m_beamSegments; i++)
            m_widthCurve.AddKey((float)i / m_beamSegments, 1f);
        m_flatWidthCurve = new AnimationCurve();
        m_flatWidthCurve.AddKey(0f, 1f);
        m_flatWidthCurve.AddKey(1f, 1f);
    }

    public override void InitializeProjectile(Transform firePointTransform, ModuleBase target, float damage, ModuleData moduleData,
                          Color color, ModuleBase sourceModuleBase, Vector3 initialDirection, float ejectSpeed, float projectileWidth)
    {
        base.InitializeProjectile(firePointTransform, target, damage, moduleData, color, sourceModuleBase, initialDirection, ejectSpeed, projectileWidth);

        m_beamHeadPos = m_firePointTransform.position;
        m_beamTailPos = m_firePointTransform.position;
        m_beamWidth = projectileWidth;
        m_beamSpeed = moduleData.projectileSpeed;
        m_beamColor = color;
        

        if (m_lineRenderer == null)
            m_lineRenderer = GetComponent<LineRenderer>();

        if (m_lineRenderer == null)
            m_lineRenderer = gameObject.AddComponent<LineRenderer>();

        m_lineRenderer.positionCount = 2;
        // 풀 재사용 시 dissolve 잔여 widthCurve를 2키 flat으로 초기화
        m_flatWidthCurve.MoveKey(0, new Keyframe(0f, m_beamWidth));
        m_flatWidthCurve.MoveKey(1, new Keyframe(1f, m_beamWidth));
        m_lineRenderer.widthCurve = m_flatWidthCurve;
        m_lineRenderer.startWidth = m_beamWidth;
        m_lineRenderer.endWidth = m_beamWidth/* * 0.8f*/;
        m_lineRenderer.enabled = true;

        // SpaceFleet/BeamAdditive 셰이더의 _Color 프로퍼티로 색상 설정
        m_lineRenderer.GetPropertyBlock(m_mpb);
        m_mpb.SetColor(k_colorId, m_beamColor);
        m_lineRenderer.SetPropertyBlock(m_mpb);

        m_uvOffset = 0f;
        m_lifeTime = 0f;


        if (m_headEffect == null)
        {
            m_headEffect = ObjectManager.Instance.m_poolManager.Get<EffectBase>(EPoolName.EFFECT_BEAM_HEAD);
            m_headEffect.transform.position = m_firePointTransform.position;
            m_headEffect.PlayEffect();
        }
        
        if (m_muzzleEffect == null)
        {
            m_muzzleEffect = ObjectManager.Instance.m_poolManager.Get<EffectBase>(EPoolName.EFFECT_BEAM_MUZZLE);    
            m_muzzleEffect.transform.position = m_firePointTransform.position;
            m_muzzleEffect.PlayEffect();
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

        // 1단계: 빔 연장 (빠르게 목표까지) - 시각적으로 타겟 위치까지 도달
        float totalDistance = Vector3.Distance(m_beamTailPos, targetPosition);
        float currentLength = 0f;

        while (currentLength < totalDistance)
        {
            m_lifeTime += Time.deltaTime;
            if (m_lifeTime >= MAX_LIFE_TIME)
            {
                if (!gameObject.activeInHierarchy) yield break;
                yield return StartCoroutine(BeamScatterAndReturn());
                yield break;
            }

            // 함선 회전 시 발사구 위치 추적
            if (m_firePointTransform != null)
            {
                m_beamTailPos = m_firePointTransform.position;
                if (m_muzzleEffect != null)
                    m_muzzleEffect.transform.position = m_beamTailPos;
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
            m_uvOffset += m_uvScrollSpeed * Time.deltaTime;
            m_lineRenderer.GetPropertyBlock(m_mpb);
            m_mpb.SetVector(k_mainTexSTId, new Vector4(1f, 1f, m_uvOffset, 0f));
            m_lineRenderer.SetPropertyBlock(m_mpb);

            yield return null;
        }

        // 2단계: 데미지 처리
        if (hitTarget != null)
        {
            hitTarget.TakeDamage(m_damage, finalHitPoint);
            SoundManager.Instance.PlayFX(EFx.Beam_Impact1, finalHitPoint);
        }

        // TakeDamage→전멸→CleanupAllProjectiles 동기 체인으로 이미 ReturnToPool 됐을 수 있음
        if (gameObject.activeInHierarchy == false) yield break;

        if (m_hitEffect == null)
        {
            m_hitEffect = ObjectManager.Instance.m_poolManager.Get<EffectBase>(EPoolName.EFFECT_BEAM_HIT);
            m_hitEffect.transform.position = finalHitPoint;
        }

        // 3단계: 흩어지며 소멸
        yield return StartCoroutine(BeamScatterAndReturn());
    }

    // 빔이 흩어지며 사라지는 코루틴
    private IEnumerator BeamScatterAndReturn()
    {
        // 1단계: 히트 후 빔 유지 (잠시 머무름)
        float holdTimer = 0f;
        while (holdTimer < m_beamHoldTime)
        {
            holdTimer += Time.deltaTime;
            if (m_firePointTransform != null)
            {
                m_beamTailPos = m_firePointTransform.position;
                m_lineRenderer.SetPosition(1, m_beamTailPos);
                if (m_muzzleEffect != null)
                    m_muzzleEffect.transform.position = m_beamTailPos;
            }
            yield return null;
        }

        // 2단계: 빔 dissolve + 파티클 흩어짐
        EmitScatterParticles(m_beamTailPos, m_beamHeadPos);
        yield return StartCoroutine(DissolveBeam());

        // LineRenderer 비활성화
        m_lineRenderer.enabled = false;

        if (m_muzzleEffect != null)
        {
            m_muzzleEffect.ReturnEffect();
            m_muzzleEffect = null;
        }

        if (m_hitEffect != null)
        {
            m_hitEffect.ReturnEffect();
            m_hitEffect = null;    
        }

        // 파티클이 완전히 사라질 때까지 대기
        yield return new WaitForSeconds(0.3f);


        ReturnToPool();
    }

    // 빔이 부분부분 타들어가며 사라지는 효과
    private IEnumerator DissolveBeam()
    {
        Vector3 start = m_beamTailPos;
        Vector3 end = m_beamHeadPos;

        // LineRenderer를 여러 세그먼트로 설정 (사전 할당 배열 재사용)
        m_lineRenderer.positionCount = m_beamSegments + 1;
        for (int i = 0; i <= m_beamSegments; i++)
        {
            float t = (float)i / m_beamSegments;
            m_lineRenderer.SetPosition(i, Vector3.Lerp(start, end, t));
            m_segmentAlpha[i] = 1f;
            m_dissolveOrder[i] = i;
        }

        // 각 세그먼트가 사라지는 순서 셔플 (랜덤)
        for (int i = m_dissolveOrder.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (m_dissolveOrder[i], m_dissolveOrder[j]) = (m_dissolveOrder[j], m_dissolveOrder[i]);
        }

        // Width curve로 세그먼트별 두께 조절 (m_widthCurve 재사용, MoveKey로 GC 없이 갱신)
        float dissolveTimer = 0f;

        while (dissolveTimer < m_beamDissolveTime)
        {
            dissolveTimer += Time.deltaTime;
            float progress = dissolveTimer / m_beamDissolveTime;

            // 진행도에 따라 세그먼트들을 순차적으로 사라지게
            int segmentsToDissolve = Mathf.FloorToInt(progress * (m_beamSegments + 1));
            for (int i = 0; i < segmentsToDissolve && i < m_dissolveOrder.Length; i++)
            {
                int segIdx = m_dissolveOrder[i];
                m_segmentAlpha[segIdx] = Mathf.Max(0f, m_segmentAlpha[segIdx] - Time.deltaTime * 5f);
            }

            // 키프레임 값만 갱신 (Keyframe은 struct, MoveKey는 내부 배열 재할당 없음)
            for (int i = 0; i <= m_beamSegments; i++)
            {
                float t = (float)i / m_beamSegments;
                m_widthCurve.MoveKey(i, new Keyframe(t, m_beamWidth * m_segmentAlpha[i]));
            }
            m_lineRenderer.widthCurve = m_widthCurve;

            yield return null;
        }

        // 완전히 투명하게
        m_lineRenderer.startWidth = 0f;
        m_lineRenderer.endWidth = 0f;
    }

    // 빔 경로를 따라 파티클 흩뿌리기
    [SerializeField] private float m_scatterParticleSize = 0.1f;
    [SerializeField] private float m_scatterParticleSpeed = 0.1f;
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
            emitParams.startSize = m_scatterParticleSize * Random.Range(1f, 2f);

            // 빔 방향에서 수직으로 흩어지게
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector3 scatterDir = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, beamDir) * perpendicular;
            emitParams.velocity = scatterDir * m_scatterParticleSpeed * Random.Range(1f, 2f);

            m_scatterParticle.Emit(emitParams, 1);
        }
    }


    public void ReturnToPool()
    {
        if (m_lifeCycleCoroutine != null)
        {
            StopCoroutine(m_lifeCycleCoroutine);
            m_lifeCycleCoroutine = null;
        }

        if (m_headEffect != null)
        {
            m_headEffect.ReturnEffect();
            m_headEffect = null;
        }

        if (m_muzzleEffect != null)
        {
            m_muzzleEffect.ReturnEffect();
            m_muzzleEffect = null;
        }

        if (m_hitEffect != null)
        {
            m_hitEffect.ReturnEffect();
            m_hitEffect = null;
        }

        if (m_scatterParticle != null)
        {
            m_scatterParticle.Stop(true);
            m_scatterParticle.Clear();
        }

        m_lineRenderer.enabled = false;
        ObjectManager.Instance.m_poolManager.Return(EPoolName.PROJECTILE_BEAM, this);
    }
}
