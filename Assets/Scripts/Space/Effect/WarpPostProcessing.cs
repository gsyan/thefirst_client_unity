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

    // Skybox blend (6 Sided)
    private static readonly int BlendID = Shader.PropertyToID("_Blend");
    private static readonly string[] FaceNamesA = { "_FrontTexA", "_BackTexA", "_LeftTexA", "_RightTexA", "_UpTexA", "_DownTexA" };
    private static readonly string[] FaceNamesB = { "_FrontTexB", "_BackTexB", "_LeftTexB", "_RightTexB", "_UpTexB", "_DownTexB" };
    private static readonly string[] FaceNames = { "_FrontTex", "_BackTex", "_LeftTex", "_RightTex", "_UpTex", "_DownTex" };
    private Material m_skyboxBlendInstance;  // 런타임 인스턴스 (에셋 보호용)

    // Warp sequence
    private Coroutine m_warpCoroutine;
    private bool m_isWarping = false;
    
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

        // 런타임 인스턴스 생성 (원본 에셋 보호)
        if (RenderSettings.skybox != null && m_skyboxBlendInstance == null)
        {
            m_skyboxBlendInstance = new Material(RenderSettings.skybox);
            RenderSettings.skybox = m_skyboxBlendInstance;
        }
        
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

    #region Skybox Blend

    // 블렌드 대상 스카이박스 설정 (워프 시작 시 호출)
    public void SetSkyboxBlendTarget(Material targetSkyboxMaterial)
    {
        if (m_skyboxBlendInstance == null || targetSkyboxMaterial == null) return;

        // m_skyboxBlendInstance B슬롯에 타겟 스카이박스
        CopyFaceTextures(targetSkyboxMaterial, FaceNames, FaceNamesB);

        m_skyboxBlendInstance.SetFloat(BlendID, 0f);
        
    }

    // 소스 머티리얼의 A슬롯 텍스처를 대상 슬롯에 복사
    private void CopyFaceTextures(Material source, string[] sourceNames, string[] destNames)
    {
        if (source == null || m_skyboxBlendInstance == null) return;

        for (int i = 0; i < FaceNamesA.Length; i++)
        {
            var tex = source.GetTexture(sourceNames[i]);
            if (tex != null)
                m_skyboxBlendInstance.SetTexture(destNames[i], tex);
        }
    }

    // 블렌드 값 설정 (0 = A, 1 = B)
    public void SetSkyboxBlend(float t)
    {
        if (m_skyboxBlendInstance == null) return;

        m_skyboxBlendInstance.SetFloat(BlendID, Mathf.Clamp01(t));
    }

    // 블렌드 완료 (B를 새 기준으로 설정)
    public void FinalizeSkyboxBlend()
    {
        if (m_skyboxBlendInstance == null) return;

        // m_skyboxBlendInstance B 슬롯 을 A 슬롯에 설정
        CopyFaceTextures(m_skyboxBlendInstance, FaceNamesB, FaceNamesA);
        m_skyboxBlendInstance.SetFloat(BlendID, 0f);
    }

    #endregion

    #region Warp Sequence

    // 함대 단위 워프 시퀀스 시작
    public void StartWarpSequence(Material targetSkyboxMaterial, System.Action onWarpComplete = null)
    {
        if (m_isWarping) return;

        SetSkyboxBlendTarget(targetSkyboxMaterial);

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
        FinalizeSkyboxBlend();
    }

    private System.Collections.IEnumerator WarpSequenceCoroutine(System.Action onWarpComplete)
    {
        m_isWarping = true;

        // Warp 시작 시점: 적 스폰 중지, 함재기 귀환 명령, 안전지역 -> zone 
        ObjectManager.Instance.StopEnemySpawning();
        ObjectManager.Instance.OrderAllAircraftReturn();

        float elapsed = 0f;
        while (elapsed < m_warpChargeTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / m_warpChargeTime;
            float easedT = EaseOutQuad(t);

            SetWarpIntensity(easedT * m_chargePhaseMaxIntensity);

            yield return null;
        }

        // Phase 2: 워프 중 (PP: chargeMax → 1.0, Skybox 블렌드)
        elapsed = 0f;
        bool cleanedUp = false;
        while (elapsed < m_warpDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / m_warpDuration;
            float intensity = Mathf.Clamp01(t + m_chargePhaseMaxIntensity);

            SetWarpIntensity(intensity);
            SetSkyboxBlend(intensity);

            // 워프 중반에 잔여 오브젝트 1회 정리
            if (t > 0.5f && cleanedUp == false)
            {
                cleanedUp = true;
                ObjectManager.Instance.CleanupAllProjectiles();
                ObjectManager.Instance.RemoveAllEnemyFleets();
            }

            yield return null;
        }

        // Phase 3: 워프 종료 (PP 감소)
        elapsed = 0f;
        while (elapsed < m_warpExitTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / m_warpExitTime;
            float easedT = EaseInQuad(t);

            SetWarpIntensity(1f - easedT);

            yield return null;
        }

        // 완료 처리
        FinalizeSkyboxBlend();
        SetWarpIntensity(0f);

        m_isWarping = false;
        m_warpCoroutine = null;

        onWarpComplete?.Invoke();
    }

    private float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    private float EaseInQuad(float t) => t * t;

    #endregion

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ResetToOriginal();

        // 런타임 인스턴스 정리
        if (m_skyboxBlendInstance != null)
        {
            Destroy(m_skyboxBlendInstance);
            m_skyboxBlendInstance = null;
        }
    }
}
