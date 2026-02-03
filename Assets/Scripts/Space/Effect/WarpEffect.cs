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

    [Header("Trail Settings")]
    [SerializeField] private Color m_trailColor = new Color(0.2f, 0.9f, 1f, 1f);
    [SerializeField] private float m_trailWidth = 0.5f;
    [SerializeField] private float m_trailTime = 1f;
    [SerializeField] private Material m_trailMaterial;

    [Header("Distortion Settings")]
    [SerializeField] private bool m_useDistortion = true;
    [SerializeField] private Color m_distortionColor = new Color(0.2f, 0.9f, 1f, 1f);
    [SerializeField] private float m_distortionScale = 1.5f;  // 함선 bounds 기준 배율

    [Header("Warp Duration")]
    [SerializeField] private float m_warpChargeTime = 0.5f;   // 워프 진입 준비
    [SerializeField] private float m_warpDuration = 2f;       // 워프 지속
    [SerializeField] private float m_warpExitTime = 0.3f;     // 워프 종료

    // 내부 상태
    private List<Renderer> m_engineRenderers = new List<Renderer>();
    private List<TrailRenderer> m_warpTrails = new List<TrailRenderer>();
    private GameObject m_distortionSphere;
    private Material m_distortionMaterial;
    private MaterialPropertyBlock m_propBlock;
    private Coroutine m_warpCoroutine;
    private bool m_isWarping = false;
    private float m_currentGlowIntensity;

    private static readonly int GlowIntensityID = Shader.PropertyToID("_GlowIntensity");

    private void Awake()
    {
        m_propBlock = new MaterialPropertyBlock();
        m_currentGlowIntensity = m_normalGlowIntensity;

        if (m_spaceShip == null)
            m_spaceShip = GetComponent<SpaceShip>();
    }

    private void Start()
    {
        CollectEngineRenderers();
        CreateWarpTrails();
        if (m_useDistortion)
            CreateDistortionSphere();
    }

    // 엔진 렌더러 수집 (EngineFlame 셰이더 사용하는 것들)
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

    // 워프 트레일 생성 (엔진 위치에)
    private void CreateWarpTrails()
    {
        if (m_spaceShip == null) return;

        // 트레일 머티리얼이 없으면 기본 생성
        if (m_trailMaterial == null)
        {
            Shader trailShader = Shader.Find("SpaceFleet/Trail");
            if (trailShader != null)
            {
                m_trailMaterial = new Material(trailShader);
                m_trailMaterial.SetColor("_Color", m_trailColor);
                m_trailMaterial.SetFloat("_Intensity", 3f);
            }
        }

        // 엔진 렌더러 위치에 트레일 생성
        foreach (var engineRenderer in m_engineRenderers)
        {
            GameObject trailObj = new GameObject("WarpTrail");
            trailObj.transform.SetParent(engineRenderer.transform);
            trailObj.transform.localPosition = Vector3.zero;

            TrailRenderer trail = trailObj.AddComponent<TrailRenderer>();
            trail.material = m_trailMaterial;
            trail.startWidth = m_trailWidth;
            trail.endWidth = 0f;
            trail.time = m_trailTime;
            trail.startColor = m_trailColor;
            trail.endColor = new Color(m_trailColor.r, m_trailColor.g, m_trailColor.b, 0f);
            trail.emitting = false;

            // 곡선 설정 (시작은 굵고 끝은 가늘게)
            AnimationCurve widthCurve = new AnimationCurve();
            widthCurve.AddKey(0f, 1f);
            widthCurve.AddKey(0.5f, 0.5f);
            widthCurve.AddKey(1f, 0f);
            trail.widthCurve = widthCurve;

            m_warpTrails.Add(trail);
        }
    }

    // 공간 왜곡 스피어 생성
    private void CreateDistortionSphere()
    {
        if (m_spaceShip == null) return;

        // 함선 바운드 계산
        Bounds shipBounds = m_spaceShip.CalculateShipBounds();
        float sphereSize = Mathf.Max(shipBounds.size.x, shipBounds.size.y, shipBounds.size.z) * m_distortionScale;

        // 스피어 생성
        m_distortionSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        m_distortionSphere.name = "WarpDistortionSphere";
        m_distortionSphere.transform.SetParent(transform);
        m_distortionSphere.transform.localPosition = Vector3.zero;
        m_distortionSphere.transform.localScale = Vector3.one * sphereSize;

        // 콜라이더 제거
        Collider col = m_distortionSphere.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // 왜곡 머티리얼 생성
        Shader distortionShader = Shader.Find("SpaceFleet/WarpDistortion");
        if (distortionShader != null)
        {
            m_distortionMaterial = new Material(distortionShader);
            m_distortionMaterial.SetColor("_RingColor", m_distortionColor);
            m_distortionMaterial.SetColor("_GlowColor", m_distortionColor * 0.5f);
            m_distortionSphere.GetComponent<Renderer>().material = m_distortionMaterial;
        }

        // 초기에는 비활성화
        m_distortionSphere.SetActive(false);
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

        m_isWarping = false;
        SetEngineGlow(m_normalGlowIntensity);
        SetTrailEmitting(false);
        SetDistortionActive(false);
    }

    // 워프 시퀀스 코루틴
    private IEnumerator WarpSequence(System.Action onWarpComplete)
    {
        m_isWarping = true;

        // Phase 1: 워프 차지 (엔진 글로우 증가 + 왜곡 시작)
        SetDistortionActive(true);
        float elapsed = 0f;
        while (elapsed < m_warpChargeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / m_warpChargeTime;

            // 엔진 글로우 증가
            float glow = Mathf.Lerp(m_normalGlowIntensity, m_warpGlowIntensity, EaseOutQuad(t));
            SetEngineGlow(glow);

            // 왜곡 강도 증가
            SetDistortionIntensity(t);

            yield return null;
        }

        // 트레일 시작
        SetTrailEmitting(true);

        // Phase 2: 워프 중 (최대 글로우 유지 + 펄스)
        elapsed = 0f;
        while (elapsed < m_warpDuration)
        {
            elapsed += Time.deltaTime;

            // 펄스 효과
            float pulse = 1f + Mathf.Sin(elapsed * 20f) * 0.2f;
            SetEngineGlow(m_warpGlowIntensity * pulse);

            yield return null;
        }

        // Phase 3: 워프 종료 (글로우 감소)
        elapsed = 0f;
        while (elapsed < m_warpExitTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / m_warpExitTime;

            // 엔진 글로우 감소
            float glow = Mathf.Lerp(m_warpGlowIntensity, m_normalGlowIntensity, EaseInQuad(t));
            SetEngineGlow(glow);

            // 왜곡 강도 감소
            SetDistortionIntensity(1f - t);

            yield return null;
        }

        // 왜곡 종료
        SetDistortionActive(false);

        // 트레일 종료 (fade out을 위해 잠시 후 끔)
        yield return new WaitForSeconds(m_trailTime);
        SetTrailEmitting(false);

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

    // 트레일 On/Off
    private void SetTrailEmitting(bool emitting)
    {
        foreach (var trail in m_warpTrails)
        {
            if (trail != null)
                trail.emitting = emitting;
        }
    }

    // 왜곡 효과 On/Off
    private void SetDistortionActive(bool active)
    {
        if (m_distortionSphere != null)
            m_distortionSphere.SetActive(active);
    }

    // 왜곡 강도 설정 (0~1)
    private void SetDistortionIntensity(float intensity)
    {
        if (m_distortionMaterial == null) return;

        m_distortionMaterial.SetFloat("_RingIntensity", 5f * intensity);
        m_distortionMaterial.SetFloat("_GlowIntensity", 2f * intensity);
    }

    // Easing 함수
    private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    private float EaseInQuad(float t) => t * t;

    public bool IsWarping => m_isWarping;

    private void OnDestroy()
    {
        // 생성한 트레일 오브젝트 정리
        foreach (var trail in m_warpTrails)
        {
            if (trail != null)
                Destroy(trail.gameObject);
        }
        m_warpTrails.Clear();

        // 왜곡 스피어 정리
        if (m_distortionSphere != null)
            Destroy(m_distortionSphere);

        // 머티리얼 정리
        if (m_distortionMaterial != null)
            Destroy(m_distortionMaterial);
        if (m_trailMaterial != null)
            Destroy(m_trailMaterial);
    }
}
