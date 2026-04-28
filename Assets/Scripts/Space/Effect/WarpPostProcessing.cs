//------------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 워프 시 Post-Processing 효과 제어 (싱글톤)
public class WarpPostProcessing : MonoSingleton<WarpPostProcessing>
{
    protected override bool ShouldDontDestroyOnLoad => false;

    [Header("Volume Reference")]
    [SerializeField] private Volume m_volume;

    [Header("Warp Sequence Timing")]
    [SerializeField] private float m_warpChargeTime = 0.5f;
    [SerializeField] private float m_warpDuration = 3f;
    [SerializeField] private float m_warpExitTime = 0.5f;
    [SerializeField, Range(0f, 1f)] private float m_chargePhaseMaxIntensity = 0.1f;

    // 원본값 저장
    private float m_originalChromaticAberration;
    private float m_originalBloomIntensity;
    private float m_originalSaturation;
    private float m_originalLensDistortion;
    private float m_originalFOV;

    private Camera m_mainCamera;

    private ChromaticAberration m_chromaticAberration;
    private Bloom m_bloom;
    private ColorAdjustments m_colorAdjustments;
    private LensDistortion m_lensDistortion;

    private bool m_initialized = false;

    // Warp sequence
    private Coroutine m_warpCoroutine;
    private bool m_isWarping = false;
    private System.Collections.Generic.List<WarpEffectShip> m_warpEffects;

    public bool IsWarping => m_isWarping;

    protected override void OnInitialize()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (m_initialized) return;

        // Volume 자동 탐색
        if (m_volume == null)
            m_volume = FindFirstObjectByType<Volume>();

        if (m_volume == null || m_volume.profile == null)
        {
            Debug.LogWarning("[WarpPostProcessing] Volume not found");
            return;
        }

        // 효과 컴포넌트 캐싱 및 원본값 저장
        if (m_volume.profile.TryGet(out m_chromaticAberration))
            m_originalChromaticAberration = m_chromaticAberration.intensity.value;

        if (m_volume.profile.TryGet(out m_bloom))
            m_originalBloomIntensity = m_bloom.intensity.value;

        if (m_volume.profile.TryGet(out m_colorAdjustments))
            m_originalSaturation = m_colorAdjustments.saturation.value;

        if (m_volume.profile.TryGet(out m_lensDistortion))
            m_originalLensDistortion = m_lensDistortion.intensity.value;

        // 카메라 캐싱
        m_mainCamera = Camera.main;
        if (m_mainCamera != null)
            m_originalFOV = m_mainCamera.fieldOfView;

        m_initialized = true;
    }

    // 워프 강도 설정 (0 = 평상시, 1 = 워프 최대)
    public void SetWarpIntensity(float t)
    {
        if (!m_initialized) Initialize();

        t = Mathf.Clamp01(t);
    }

    // 즉시 원본으로 복원
    public void ResetToOriginal()
    {
        SetWarpIntensity(0f);
    }

    #region Warp Sequence

    // 함대 단위 워프 시퀀스 시작 — warpEffects: 함선별 글로우/스피드라인 제어 대상
    public void StartWarpSequence(
        System.Collections.Generic.List<WarpEffectShip> warpEffects,
        System.Action onWarpComplete = null)
    {
        if (m_isWarping) return;

        m_warpEffects = warpEffects;

        if (m_warpCoroutine != null)
            StopCoroutine(m_warpCoroutine);

        m_warpCoroutine = StartCoroutine(WarpSequenceCoroutine(onWarpComplete));
    }

    // 워프 시퀀스 중단
    public void StopWarpSequence()
    {
        if (m_warpCoroutine != null)
        {
            StopCoroutine(m_warpCoroutine);
            m_warpCoroutine = null;
        }

        m_isWarping = false;
        SetWarpIntensity(0f);

        if (m_warpEffects != null)
        {
            foreach (var we in m_warpEffects)
                if (we != null) we.StopWarp();
            m_warpEffects = null;
        }
    }

    private System.Collections.IEnumerator WarpSequenceCoroutine(System.Action onWarpComplete)
    {
        m_isWarping = true;

        // Warp 시작 시점: 적 스폰 중지, 함재기 귀환 명령
        ObjectManager.Instance.StopEnemySpawning();
        ObjectManager.Instance.OrderAllAircraftReturn();

        // Phase 1: 차지 — 엔진 글로우 증가
        float elapsed = 0f;
        while (elapsed < m_warpChargeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / m_warpChargeTime;
            float easedT = EaseOutQuad(t);

            SetWarpIntensity(easedT * m_chargePhaseMaxIntensity);
            SetShipGlow(easedT);

            yield return null;
        }

        // Phase 2: 워프 중 — 스피드라인 On, PP 강화
        SetShipSpeedLines(true);
        // 스피드라인이 시작되면 적/투사체 정리
        ObjectManager.Instance.CleanupAllProjectiles();
        ObjectManager.Instance.RemoveAllEnemyFleets();

        elapsed = 0f;
        while (elapsed < m_warpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / m_warpDuration;
            float intensity = Mathf.Clamp01(t + m_chargePhaseMaxIntensity);

            SetWarpIntensity(intensity);

            // 엔진 글로우 펄스
            float pulse = 1f + Mathf.Sin(elapsed * 20f) * 0.2f;
            SetShipGlowAbsolute(pulse);

            yield return null;
        }

        // Phase 3: 워프 종료 — 스피드라인 Off, PP 감소, 글로우 복원
        SetShipSpeedLines(false);
        elapsed = 0f;
        while (elapsed < m_warpExitTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / m_warpExitTime;
            float easedT = EaseInQuad(t);

            SetWarpIntensity(1f - easedT);
            SetShipGlow(1f - easedT);

            yield return null;
        }

        // 완료 처리
        SetWarpIntensity(0f);
        SetShipGlow(0f);

        m_isWarping = false;
        m_warpCoroutine = null;
        m_warpEffects = null;

        onWarpComplete?.Invoke();
    }

    // t=0: 노멀 글로우, t=1: 워프 글로우
    private void SetShipGlow(float t)
    {
        if (m_warpEffects == null) return;
        foreach (var we in m_warpEffects)
        {
            if (we == null) continue;
            float glow = Mathf.Lerp(we.NormalGlowIntensity, we.WarpGlowIntensity, t);
            we.SetEngineGlow(glow);
        }
    }

    // 워프 중 펄스 — 워프 글로우에 배율 적용
    private void SetShipGlowAbsolute(float multiplier)
    {
        if (m_warpEffects == null) return;
        foreach (var we in m_warpEffects)
        {
            if (we == null) continue;
            we.SetEngineGlow(we.WarpGlowIntensity * multiplier);
        }
    }

    private void SetShipSpeedLines(bool active)
    {
        if (m_warpEffects == null) return;
        foreach (var we in m_warpEffects)
        {
            if (we == null) continue;
            we.SetSpeedLinesActive(active);
        }
    }

    private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    private float EaseInQuad(float t) => t * t;

    #endregion

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ResetToOriginal();
    }
}
