//------------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 함선 중심 워프 이펙트 컨트롤러
public class WarpEffect : MonoBehaviour
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
    [SerializeField] private float m_warpDuration = 2f;
    [SerializeField] private float m_warpExitTime = 0.3f;

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

        Debug.Log($"[WarpEffect] Init - Engines:{m_engineRenderers.Count}, Bounds:{m_shipBounds.size}");
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

        // 부모는 바꾸지 않고 position/rotation만 동기화
        UpdateSpeedLineTransform();

        // 파티클 Shape Box 크기를 ship bounds에 맞게 조절
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

        // ship bounds를 로컬 좌표로 변환
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

    // 워프 시작
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

        // fade out 코루틴도 정리
        if (m_speedLineFadeCoroutine != null)
        {
            StopCoroutine(m_speedLineFadeCoroutine);
            m_speedLineFadeCoroutine = null;
        }

        m_isWarping = false;
        SetEngineGlow(m_normalGlowIntensity);
        ReturnSpeedLineToPool();
    }

    // 워프 시퀀스
    private IEnumerator WarpSequence(System.Action onWarpComplete)
    {
        m_isWarping = true;

        // Phase 1: 워프 차지
        float elapsed = 0f;
        while (elapsed < m_warpChargeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / m_warpChargeTime;
            float glow = Mathf.Lerp(m_normalGlowIntensity, m_warpGlowIntensity, EaseOutQuad(t));
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
            float glow = Mathf.Lerp(m_warpGlowIntensity, m_normalGlowIntensity, EaseInQuad(t));
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

    // 스피드 라인 On/Off (풀 방식)
    private void SetSpeedLinesActive(bool active)
    {
        if (!m_useSpeedLines) return;

        if (active)
        {
            // fade out 코루틴 중이면 취소
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
            // 방출 중지 후 fade out
            if (m_speedLineEffect != null && m_speedLineFadeCoroutine == null)
                m_speedLineFadeCoroutine = StartCoroutine(FadeOutSpeedLine());
        }
    }

    // 스피드라인 fade out 후 풀 반환
    private IEnumerator FadeOutSpeedLine()
    {
        if (m_speedLineEffect == null) yield break;

        // 파티클 방출만 중지 (기존 파티클은 수명대로 사라짐)
        if (m_speedLineEffect.TryGetComponent(out ParticleSystem ps))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            // 파티클 수명만큼 대기
            float lifetime = ps.main.startLifetime.constantMax;
            yield return new WaitForSeconds(lifetime);
        }

        // 풀에 반환
        ReturnSpeedLineToPool();
        m_speedLineFadeCoroutine = null;
    }

    private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    private float EaseInQuad(float t) => t * t;

    public bool IsWarping => m_isWarping;

    private void OnDestroy()
    {
        // 코루틴 정리
        if (m_speedLineFadeCoroutine != null)
            m_speedLineFadeCoroutine = null;

        // 스피드라인은 풀에 반환
        ReturnSpeedLineToPool();
    }
}
