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

    [Header("Warp Settings")]
    [SerializeField] private float m_warpChromaticAberration = 0.6f;
    [SerializeField] private float m_warpBloomIntensity = 2f;
    [SerializeField] private float m_warpSaturation = 20f;
    [SerializeField] private float m_warpLensDistortion = -0.5f;  // 음수 = 화면 외곽 안으로 (FOV 증가 느낌)

    [Header("FOV Settings")]
    [SerializeField] private float m_warpFOV = 15f;  // 워프 시 FOV 감소량

    [Header("Radial Blur Settings")]
    [SerializeField] private float m_warpRadialBlurIntensity = 0.1f;  // 워프 시 방사형 블러 강도


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

        // if (m_chromaticAberration != null)
        //     m_chromaticAberration.intensity.value = Mathf.Lerp(m_originalChromaticAberration, m_warpChromaticAberration, t);

        // if (m_bloom != null)
        //     m_bloom.intensity.value = Mathf.Lerp(m_originalBloomIntensity, m_warpBloomIntensity, t);

        // if (m_colorAdjustments != null)
        //     m_colorAdjustments.saturation.value = Mathf.Lerp(m_originalSaturation, m_warpSaturation, t);

        // 외곽 날아가는 효과 (음수 = 안으로, 양수 = 바깥으로)
        // if (m_lensDistortion != null)
        //     m_lensDistortion.intensity.value = Mathf.Lerp(m_originalLensDistortion, m_warpLensDistortion, t);

        // FOV 감소 (외곽이 날아가는 느낌)
        if (m_mainCamera != null)
            m_mainCamera.fieldOfView = Mathf.Lerp(m_originalFOV, m_originalFOV - m_warpFOV, t);

        // Radial Blur (방사형 모션 블러)
        if (RadialBlurFeature.Instance != null)
            RadialBlurFeature.Instance.SetIntensity(m_warpRadialBlurIntensity * t);
    }

    // 즉시 원본으로 복원
    public void ResetToOriginal()
    {
        SetWarpIntensity(0f);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ResetToOriginal();
    }
}
