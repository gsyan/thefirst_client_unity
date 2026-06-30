// ------------------------------------------------------------
using System.Collections;
using UnityEngine;

public class SoundManager : MonoSingleton<SoundManager>
{
    private const int    FX_POOL_SIZE         = 8;
    private const float  DEFAULT_BGM_VOLUME  = 0.7f;
    private const float  DEFAULT_FX_VOLUME   = 1.0f;
    private const float  BGM_CROSSFADE_TIME  = 2.0f;
    private const float  FX_CULL_DISTANCE    = 800f;
    private const string PREFS_BGM_VOLUME    = "SoundBGMVolume";
    private const string PREFS_FX_VOLUME     = "SoundFXVolume";
    private const string BGM_RESOURCE_PATH   = "Sound/BGM/";
    private const string FX_RESOURCE_PATH    = "Sound/FX/";

    // true 이면 거리 컬링 적용
    private static bool IsCullable(EFx fx)
    {
        return fx != EFx.Explosion_Ship
            && fx != EFx.Ship_Warp;
    }

    // 크로스페이드용 BGM 소스 2개 — 번갈아 사용
    private AudioSource[] m_bgmSources;
    private int           m_activeBgmIndex     = 0;
    private AudioSource[] m_fxSources;
    private AudioSource   m_stageClearFxSource;
    private int           m_stageClearFxPriority = int.MaxValue; // 현재 재생 중인 FX enum 값 (낮을수록 우선순위 높음)
    private float         m_bgmVolume;
    private float         m_fxVolume;
    private EBgm          m_currentBgm         = EBgm.None;
    private Coroutine     m_crossfadeCoroutine = null;
    private bool          m_initialized        = false;

    public void InitializeSoundManager()
    {
        OnInitialize();
    }

    protected override void OnInitialize()
    {
        if (m_initialized == true) return;
        m_initialized = true;

        m_bgmVolume = PlayerPrefs.GetFloat(PREFS_BGM_VOLUME, DEFAULT_BGM_VOLUME);
        m_fxVolume  = PlayerPrefs.GetFloat(PREFS_FX_VOLUME,  DEFAULT_FX_VOLUME);

        CreateBgmSources();
        CreateFxPool();

        EventManager.Subscribe_MyFleetStateChanged(OnFleetStateChanged);
    }

    protected override void OnDestroy()
    {
        EventManager.Unsubscribe_MyFleetStateChanged(OnFleetStateChanged);
        base.OnDestroy();
    }

    private void OnFleetStateChanged(EUnitState state)
    {
        bool isBattle  = state.IsBattleState();
        EBgm targetBgm = isBattle ? EBgm.Battle : EBgm.Space;
        PlayBGM(targetBgm);
    }

    // ─── BGM ─────────────────────────────────────────────────────────────────

    public void PlayBGM(EBgm bgm)
    {
        if (bgm == EBgm.None)
        {
            StopBGM();
            return;
        }

        AudioSource activeSource = m_bgmSources[m_activeBgmIndex];
        if (m_currentBgm == bgm && activeSource.isPlaying == true)
            return;

        string clipName = bgm.ToString().ToLower();
        string path     = BGM_RESOURCE_PATH + "bgm_" + clipName;
        AudioClip clip  = Resources.Load<AudioClip>(path);

        if (clip == null)
        {
            Debug.LogWarning($"[SoundManager] BGM 클립 없음: {path}");
            return;
        }

        m_currentBgm = bgm;

        if (m_crossfadeCoroutine != null)
            StopCoroutine(m_crossfadeCoroutine);
        m_crossfadeCoroutine = StartCoroutine(CrossfadeBGM(clip));
    }

    public void StopBGM()
    {
        m_currentBgm = EBgm.None;
        if (m_crossfadeCoroutine != null)
            StopCoroutine(m_crossfadeCoroutine);
        foreach (AudioSource src in m_bgmSources)
            src.Stop();
    }

    public void SetBGMVolume(float volume)
    {
        m_bgmVolume = Mathf.Clamp01(volume);
        // 현재 재생 중인 소스에만 반영
        m_bgmSources[m_activeBgmIndex].volume = m_bgmVolume;
        PlayerPrefs.SetFloat(PREFS_BGM_VOLUME, m_bgmVolume);
    }

    public float GetBGMVolume()
    {
        return m_bgmVolume;
    }

    // ─── FX ──────────────────────────────────────────────────────────────────

    // 전체 재생 — retrigger: true면 같은 FX 재생 중일 때 즉시 재시작
    public void PlayFX(EFx fx, bool retrigger = false)
    {
        if (fx == EFx.None)
            return;

        if (IsStageClearFx(fx) == true)
        {
            PlayStageClearFX(fx);
            return;
        }

        AudioClip clip = LoadFxClip(fx);
        if (clip == null)
            return;

        AudioSource source = GetAvailableFxSource(fx, retrigger);
        if (source == null)
            return;

        SetFxSourcePriority(source, fx);
        source.spatialBlend = 0f;
        source.volume       = m_fxVolume;
        source.clip         = clip;
        source.time         = 0f;
        source.Play();
    }

    private static bool IsStageClearFx(EFx fx)
    {
        return fx == EFx.Commander_Level_Up
            || fx == EFx.Stage_Clear_First
            || fx == EFx.Stage_Clear;
    }

    private void PlayStageClearFX(EFx fx)
    {
        int incomingPriority = (int)fx;
        bool isPlaying       = m_stageClearFxSource.isPlaying;

        if (isPlaying == true && incomingPriority >= m_stageClearFxPriority)
            return;

        AudioClip clip = LoadFxClip(fx);
        if (clip == null)
            return;

        m_stageClearFxSource.Stop();
        m_stageClearFxPriority      = incomingPriority;
        m_stageClearFxSource.volume = m_fxVolume;
        m_stageClearFxSource.clip   = clip;
        m_stageClearFxSource.time   = 0f;
        m_stageClearFxSource.Play();
    }

    // 3D 위치 재생 — spatialBlend=1, 거리 감쇠 적용
    public void PlayFX(EFx fx, Vector3 worldPos, bool retrigger = false)
    {
        if (fx == EFx.None)
            return;

        // 거리 컬링 — Explosion_Ship 제외
        if (IsCullable(fx) == true)
        {
            float dist = Vector3.Distance(Camera.main.transform.position, worldPos);
            if (dist > FX_CULL_DISTANCE)
                return;
        }

        AudioClip clip = LoadFxClip(fx);
        if (clip == null)
            return;

        AudioSource source = GetAvailableFxSource(fx, retrigger);
        if (source == null)
            return;

        SetFxSourcePriority(source, fx);
        source.transform.position = worldPos;
        source.spatialBlend       = 1f;
        source.volume             = m_fxVolume;
        source.clip               = clip;
        source.time               = 0f;
        source.Play();
    }

    // 구간 재생 (startTime: 시작 초, duration: 재생 길이 초)
    public void PlayFX(EFx fx, float startTime, float duration)
    {
        if (fx == EFx.None)
            return;

        AudioClip clip = LoadFxClip(fx);
        if (clip == null)
            return;

        AudioSource source = GetAvailableFxSource(fx);
        if (source == null)
            return;

        SetFxSourcePriority(source, fx);
        source.spatialBlend = 0f;
        source.volume       = m_fxVolume;
        source.clip         = clip;
        source.time         = Mathf.Clamp(startTime, 0f, clip.length);
        source.Play();

        StartCoroutine(StopAfterDuration(source, duration));
    }

    public void SetFXVolume(float volume)
    {
        m_fxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PREFS_FX_VOLUME, m_fxVolume);
    }

    public float GetFXVolume()
    {
        return m_fxVolume;
    }

    // ─── Private ─────────────────────────────────────────────────────────────

    private void CreateBgmSources()
    {
        m_bgmSources = new AudioSource[2];
        for (int i = 0; i < 2; i++)
        {
            GameObject obj = new GameObject("BGMSource_" + i);
            obj.transform.SetParent(transform);
            AudioSource src   = obj.AddComponent<AudioSource>();
            src.playOnAwake   = false;
            src.loop          = true;
            src.volume        = 0f;
            m_bgmSources[i]   = src;
        }
    }

    private void CreateFxPool()
    {
        m_fxSources         = new AudioSource[FX_POOL_SIZE];
        m_fxSourcePriority  = new int[FX_POOL_SIZE];
        GameObject fxRoot   = new GameObject("FXPool");
        fxRoot.transform.SetParent(transform);

        for (int i = 0; i < FX_POOL_SIZE; i++)
        {
            GameObject fxObj = new GameObject("FXSource_" + i);
            fxObj.transform.SetParent(fxRoot.transform);
            AudioSource src  = fxObj.AddComponent<AudioSource>();
            src.playOnAwake  = false;
            src.spatialBlend = 0f;
            src.rolloffMode  = AudioRolloffMode.Logarithmic;
            src.minDistance  = 5f;
            src.maxDistance  = 1000f;
            m_fxSources[i]   = src;
        }

        GameObject stageClearObj     = new GameObject("StageClearFXSource");
        stageClearObj.transform.SetParent(transform);
        AudioSource stageClearSrc    = stageClearObj.AddComponent<AudioSource>();
        stageClearSrc.playOnAwake    = false;
        stageClearSrc.spatialBlend   = 0f;
        m_stageClearFxSource         = stageClearSrc;
        m_stageClearFxPriority       = int.MaxValue;
    }

    private IEnumerator CrossfadeBGM(AudioClip newClip)
    {
        int nextIndex      = 1 - m_activeBgmIndex;
        AudioSource fadeIn  = m_bgmSources[nextIndex];
        AudioSource fadeOut = m_bgmSources[m_activeBgmIndex];

        fadeIn.clip   = newClip;
        fadeIn.volume = 0f;
        fadeIn.Play();

        float elapsed = 0f;
        while (elapsed < BGM_CROSSFADE_TIME)
        {
            elapsed += Time.deltaTime;
            float t         = Mathf.Clamp01(elapsed / BGM_CROSSFADE_TIME);
            fadeIn.volume   = m_bgmVolume * t;
            fadeOut.volume  = m_bgmVolume * (1f - t);
            yield return null;
        }

        fadeOut.Stop();
        fadeOut.volume  = 0f;
        fadeIn.volume   = m_bgmVolume;
        m_activeBgmIndex = nextIndex;
        m_crossfadeCoroutine = null;
    }

    // 각 소스가 현재 재생 중인 FX 우선순위 (steal 판단용)
    // 우선순위: Explosion_Ship(3) > Explosion_Missile류(2) > 발사/임팩트음(1)
    private int[] m_fxSourcePriority;

    // fx: 재생하려는 FX (우선순위 비교용). 빈 슬롯이 있으면 바로 반환.
    // 풀 소진 시 — EFx enum 값이 클수록 낮은 우선순위 → steal 대상.
    // 동일 우선순위면 가장 오래 재생된 것을 steal.
    // 모든 소스가 더 높은 우선순위(더 작은 값)면 재생 포기(null 반환).
    private AudioSource GetAvailableFxSource(EFx fx = EFx.None, bool retrigger = false)
    {
        int incomingPriority    = (int)fx;
        AudioSource stealTarget = null;
        int highestValue        = int.MinValue; // enum 값이 클수록 낮은 우선순위
        float longestTime       = -1f;

        for (int i = 0; i < FX_POOL_SIZE; i++)
        {
            if (m_fxSources[i].isPlaying == false)
                return m_fxSources[i];

            // retrigger: 같은 FX 재생 중이면 즉시 steal
            if (retrigger == true && m_fxSourcePriority[i] == incomingPriority)
            {
                m_fxSources[i].Stop();
                return m_fxSources[i];
            }

            int srcPriority = m_fxSourcePriority[i];
            if (srcPriority > incomingPriority ||
                (srcPriority == incomingPriority && m_fxSources[i].time > longestTime))
            {
                if (srcPriority > highestValue ||
                    (srcPriority == highestValue && m_fxSources[i].time > longestTime))
                {
                    highestValue   = srcPriority;
                    longestTime    = m_fxSources[i].time;
                    stealTarget    = m_fxSources[i];
                }
            }
        }

        if (stealTarget == null)
            return null;

        stealTarget.Stop();
        return stealTarget;
    }

    private void SetFxSourcePriority(AudioSource source, EFx fx)
    {
        for (int i = 0; i < FX_POOL_SIZE; i++)
        {
            if (m_fxSources[i] == source)
            {
                m_fxSourcePriority[i] = (int)fx;
                return;
            }
        }
    }

    private AudioClip LoadFxClip(EFx fx)
    {
        string clipName = fx.ToString().ToLower();
        string path     = FX_RESOURCE_PATH + "fx_" + clipName;
        AudioClip clip  = Resources.Load<AudioClip>(path);

        if (clip == null)
            Debug.LogWarning($"[SoundManager] FX 클립 없음: {path}");

        return clip;
    }

    private IEnumerator StopAfterDuration(AudioSource source, float duration)
    {
        yield return new WaitForSeconds(duration);

        if (source.isPlaying == true)
            source.Stop();
    }
}
