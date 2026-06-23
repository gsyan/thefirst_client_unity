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
        RefreshShipBounds();

        EventManager.Subscribe_ShipBodyChanged(OnShipBodyChanged);
    }

    // 함체 교체 이벤트 — bounds 및 파티클 Shape 갱신
    private void OnShipBodyChanged(SpaceShip ship)
    {
        if (ship != m_spaceShip) return;

        CollectEngineRenderers();
        RefreshShipBounds();

        if (m_speedLineEffect != null)
            ApplyShipBoundsToParticle(m_speedLineEffect);
    }

    private void RefreshShipBounds()
    {
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

        var emission = ps.emission;
        float sizeRef = Mathf.Max(localSize.x, localSize.y);
        // 기본 emission.rateOverTime = 20, 기본 함체 크기 1.5정도, 곱하기 함체 사이즈에 보정, 맥스 40
        emission.rateOverTime = Mathf.Min(20.0f * (sizeRef / 1.5f), 40.0f);

        // 파티클 생성 기준점을 함선 z 크기만큼 앞으로 오프셋
        //shape.position = new Vector3(0, 0, localSize.z * 0.5f);
    }

    // 스피드 라인 이펙트 - 풀에 반환
    private void ReturnSpeedLineToPool()
    {
        if (m_speedLineEffect == null) return;

        m_speedLineEffect.StopEffect();
        m_speedLineEffect.ReturnEffect();
        m_speedLineEffect = null;
    }

    // 함선 추가 시 워프 진입 이펙트 — 목적지 도착까지 엔진 글로우 + 스피드라인 유지
    public void StartApproachWarp()
    {
        if (m_warpCoroutine != null)
            StopCoroutine(m_warpCoroutine);
        m_warpCoroutine = StartCoroutine(ApproachWarpSequence());
    }

    private IEnumerator ApproachWarpSequence()
    {
        m_isWarping = true;
        SetEngineGlow(m_warpGlowIntensity);
        SetSpeedLinesActive(true);
        SoundManager.Instance.PlayFX(EFx.Ship_Warp, transform.position);

        // 함선이 목적지에 도착할 때까지 스피드라인 위치 동기화
        while (m_spaceShip != null && m_spaceShip.m_formationMoveState == FormationMoveState.Moving)
        {
            UpdateSpeedLineTransform();
            yield return null;
        }

        SetEngineGlow(m_normalGlowIntensity);
        SetSpeedLinesActive(false);
        m_isWarping = false;
        m_warpCoroutine = null;
    }

    // fleet 오브젝트 이동 중 워프 이펙트 유지 — StopWarp() 호출 시 종료
    public void StartFleetWarpIn()
    {
        if (m_warpCoroutine != null) StopCoroutine(m_warpCoroutine);
        m_warpCoroutine = StartCoroutine(FleetWarpInSequence());
    }

    private IEnumerator FleetWarpInSequence()
    {
        m_isWarping = true;
        SetEngineGlow(m_warpGlowIntensity);
        SetSpeedLinesActive(true);
        SoundManager.Instance.PlayFX(EFx.Ship_Warp, transform.position);

        while (m_isWarping == true)
        {
            UpdateSpeedLineTransform();
            yield return null;
        }

        SetEngineGlow(m_normalGlowIntensity);
        SetSpeedLinesActive(false);
        m_warpCoroutine = null;
    }

    // 워프 중단 (긴급 중단 / WarpPostProcessing.StopWarpSequence 호출 시)
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

    // WarpPostProcessing에서 Phase별 직접 제어용
    public float NormalGlowIntensity => m_normalGlowIntensity;
    public float WarpGlowIntensity   => m_warpGlowIntensity;

    // 엔진 글로우 설정
    public void SetEngineGlow(float intensity)
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
    public void SetSpeedLinesActive(bool active)
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
                m_speedLineEffect.PlayEffect();
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

    public bool IsWarping => m_isWarping;

    private void OnDestroy()
    {
        EventManager.Unsubscribe_ShipBodyChanged(OnShipBodyChanged);

        if (m_speedLineFadeCoroutine != null)
            m_speedLineFadeCoroutine = null;

        ReturnSpeedLineToPool();
    }
}
