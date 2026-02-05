//------------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 함선 개별 워프 이펙트 (엔진 글로우, 스피드라인)
public class WarpEffectShip : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpaceShip m_spaceShip;

    [Header("Engine Glow Settings")]
    [SerializeField] private float m_normalGlowIntensity = 5f;
    [SerializeField] private float m_warpGlowIntensity = 20f;

    [Header("Speed Line Settings")]
    [SerializeField] private bool m_useSpeedLines = true;

    [Header("Warp Duration")]
    [SerializeField] private float m_warpChargeTime = 0.5f;
    [SerializeField] private float m_warpDuration = 3f;
    [SerializeField] private float m_warpExitTime = 0.5f;

    // 내부 상태
    private List<Renderer> m_engineRenderers = new List<Renderer>();
    private EffectBase m_speedLineEffect;
    private MaterialPropertyBlock m_propBlock;
    private Coroutine m_warpCoroutine;
    private Coroutine m_speedLineFadeCoroutine;
    private bool m_isWarping = false;
    private float m_currentGlowIntensity;
    private Bounds m_shipBounds;

    private static readonly int GlowIntensityID = Shader.PropertyToID("_GlowIntensity");
    private bool m_initialized = false;

    public float WarpChargeTime => m_warpChargeTime;
    public float WarpDuration => m_warpDuration;
    public float WarpExitTime => m_warpExitTime;

    private void Awake()
    {
        m_propBlock = new MaterialPropertyBlock();
        m_currentGlowIntensity = m_normalGlowIntensity;

        if (m_spaceShip == null)
            m_spaceShip = GetComponent<SpaceShip>();
    }

    public void InitializeWarpEffect()
    {
        if (m_initialized) return;
        m_initialized = true;

        if (m_spaceShip == null)
            m_spaceShip = GetComponent<SpaceShip>();

        CollectEngineRenderers();

        m_shipBounds = CommonUtility.CalculateRendererBounds(transform, excludeParticles: true, excludeTrails: true, excludeDisabled: false);
    }

    // 엔진 렌더러 수집
    private void CollectEngineRenderers()
    {
        m_engineRenderers.Clear();
        if (m_spaceShip == null) return;

        foreach (var body in m_spaceShip.m_moduleBodys)
        {
            if (body == null) continue;

            Renderer[] renderers = body.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterial != null &&
                    renderer.sharedMaterial.shader.name == "SpaceFleet/EngineFlame")
                {
                    m_engineRenderers.Add(renderer);
                }
            }
        }
    }

    // 스피드 라인 이펙트 - 풀에서 가져오기
    private void GetSpeedLineFromPool()
    {
        if (m_speedLineEffect != null) return;

        m_speedLineEffect = ObjectManager.Instance.m_poolManager.Get<EffectBase>(EPoolName.EFFECT_WARP_SPEEDLINES);
        if (m_speedLineEffect == null) return;

        UpdateSpeedLineTransform();
        ApplyShipBoundsToParticle(m_speedLineEffect);
    }

    // 스피드라인 위치/회전 동기화
    private void UpdateSpeedLineTransform()
    {
        if (m_speedLineEffect == null) return;

        m_speedLineEffect.transform.SetPositionAndRotation(transform.position, transform.rotation);
    }

    // 파티클 Shape 크기를 ship bounds에 맞게 조절
    private void ApplyShipBoundsToParticle(EffectBase effect)
    {
        if (!effect.TryGetComponent(out ParticleSystem ps)) return;

        var shape = ps.shape;
        if (shape.shapeType != ParticleSystemShapeType.Box) return;

        Vector3 localSize = transform.InverseTransformVector(m_shipBounds.size);
        localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

        shape.scale = localSize;
    }

    // 스피드 라인 이펙트 - 풀에 반환
    private void ReturnSpeedLineToPool()
    {
        if (m_speedLineEffect == null) return;

        m_speedLineEffect.Stop();
        m_speedLineEffect.ReturnToPool_Effect();
        m_speedLineEffect = null;
    }

    // 워프 시작 (함선 개별 효과만)
    public void StartWarp(System.Action onWarpComplete = null)
    {
        if (m_isWarping) return;

        if (m_warpCoroutine != null)
            StopCoroutine(m_warpCoroutine);

        m_warpCoroutine = StartCoroutine(WarpSequence(onWarpComplete));
    }

    // 워프 중단
    public void StopWarp()
    {
        if (m_warpCoroutine != null)
        {
            StopCoroutine(m_warpCoroutine);
            m_warpCoroutine = null;
        }

        if (m_speedLineFadeCoroutine != null)
        {
            StopCoroutine(m_speedLineFadeCoroutine);
            m_speedLineFadeCoroutine = null;
        }

        m_isWarping = false;
        SetEngineGlow(m_normalGlowIntensity);
        ReturnSpeedLineToPool();
    }

    // 워프 시퀀스 (함선 개별 효과만)
    private IEnumerator WarpSequence(System.Action onWarpComplete)
    {
        m_isWarping = true;

        // Phase 1: 워프 차지
        float elapsed = 0f;
        while (elapsed < m_warpChargeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / m_warpChargeTime;
            float easedT = EaseOutQuad(t);

            float glow = Mathf.Lerp(m_normalGlowIntensity, m_warpGlowIntensity, easedT);
            SetEngineGlow(glow);

            yield return null;
        }

        // 스피드라인 시작
        SetSpeedLinesActive(true);

        // Phase 2: 워프 중
        elapsed = 0f;
        while (elapsed < m_warpDuration)
        {
            elapsed += Time.deltaTime;

            // 엔진 글로우 펄스
            float pulse = 1f + Mathf.Sin(elapsed * 20f) * 0.2f;
            SetEngineGlow(m_warpGlowIntensity * pulse);

            yield return null;
        }

        // Phase 3: 워프 종료
        elapsed = 0f;
        while (elapsed < m_warpExitTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / m_warpExitTime;
            float easedT = EaseInQuad(t);

            float glow = Mathf.Lerp(m_warpGlowIntensity, m_normalGlowIntensity, easedT);
            SetEngineGlow(glow);

            yield return null;
        }

        // 스피드라인 종료
        SetSpeedLinesActive(false);

        m_isWarping = false;
        m_warpCoroutine = null;

        onWarpComplete?.Invoke();
    }

    // 엔진 글로우 설정
    private void SetEngineGlow(float intensity)
    {
        m_currentGlowIntensity = intensity;

        foreach (var renderer in m_engineRenderers)
        {
            if (renderer == null) continue;

            renderer.GetPropertyBlock(m_propBlock);
            m_propBlock.SetFloat(GlowIntensityID, intensity);
            renderer.SetPropertyBlock(m_propBlock);
        }
    }

    // 스피드 라인 On/Off
    private void SetSpeedLinesActive(bool active)
    {
        if (!m_useSpeedLines) return;

        if (active)
        {
            if (m_speedLineFadeCoroutine != null)
            {
                StopCoroutine(m_speedLineFadeCoroutine);
                m_speedLineFadeCoroutine = null;
            }

            GetSpeedLineFromPool();
            if (m_speedLineEffect != null)
                m_speedLineEffect.Play();
        }
        else
        {
            if (m_speedLineEffect != null && m_speedLineFadeCoroutine == null)
                m_speedLineFadeCoroutine = StartCoroutine(FadeOutSpeedLine());
        }
    }

    // 스피드라인 fade out 후 풀 반환
    private IEnumerator FadeOutSpeedLine()
    {
        if (m_speedLineEffect == null) yield break;

        if (m_speedLineEffect.TryGetComponent(out ParticleSystem ps))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            float lifetime = ps.main.startLifetime.constantMax;
            yield return new WaitForSeconds(lifetime);
        }

        ReturnSpeedLineToPool();
        m_speedLineFadeCoroutine = null;
    }

    private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    private float EaseInQuad(float t) => t * t;

    public bool IsWarping => m_isWarping;

    private void OnDestroy()
    {
        if (m_speedLineFadeCoroutine != null)
            m_speedLineFadeCoroutine = null;

        ReturnSpeedLineToPool();
    }
}
