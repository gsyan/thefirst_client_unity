// AdMob 리워드 광고 로드/표시 관리
// SDK: Google Mobile Ads Unity Plugin 필요
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;

public enum EAdResult { Rewarded, UserClosed, Failed }

public class AdManager : MonoSingleton<AdManager>
{
    [SerializeField] private AdConfig m_adConfig;

    private RewardedAd _rewardedAd;
    private Action<EAdResult> _onRewardedAdClosed;
    private bool _rewardEarned;                   // 보상 수령 여부 (Closed 시 구분용)
    private bool _isDeviceAllowed = true;         // dev 빌드에서 비테스트 기기 차단용

    // AdMob 콜백은 백그라운드 스레드에서 오므로 메인 스레드로 전달
    private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();

    private void Update()
    {
        while (_mainThreadQueue.Count > 0)
        {
            Action action;
            lock (_mainThreadQueue)
                action = _mainThreadQueue.Dequeue();
            action?.Invoke();
        }
    }

    private void Dispatch(Action action)
    {
        lock (_mainThreadQueue)
            _mainThreadQueue.Enqueue(action);
    }

    protected override void OnInitialize()
    {
        if (m_adConfig == null)
        {
            m_adConfig = ResourceManager.Instance.Load<AdConfig>("DataTable/AdConfig");
            if (m_adConfig == null)
            {
                Debug.LogError("[AdManager] AdConfig를 찾을 수 없습니다.");
                return;
            }
        }

        StartCoroutine(RequestConsentThenInitAds());
    }

    private IEnumerator RequestConsentThenInitAds()
    {
        bool consentDone = false;

        var consentParams = new ConsentRequestParameters();
#if DEVELOPMENT_BUILD
        // 개발 빌드: EEA 지역으로 강제하여 동의 팝업 테스트 가능
        consentParams = new ConsentRequestParameters
        {
            ConsentDebugSettings = new ConsentDebugSettings
            {
                DebugGeography = DebugGeography.EEA,
            }
        };
#endif

        ConsentInformation.Update(consentParams, error =>
        {
            if (error != null)
            {
                Debug.LogWarning($"[AdManager] UMP 동의 정보 요청 실패: {error}");
                consentDone = true;
                return;
            }

            bool isConsentRequired = ConsentInformation.IsConsentFormAvailable();
            if (isConsentRequired == false)
            {
                consentDone = true;
                return;
            }

            ConsentForm.Load((form, loadError) =>
            {
                if (loadError != null)
                {
                    Debug.LogWarning($"[AdManager] UMP 동의 폼 로드 실패: {loadError}");
                    consentDone = true;
                    return;
                }

                form.Show(showError =>
                {
                    if (showError != null)
                        Debug.LogWarning($"[AdManager] UMP 동의 폼 표시 실패: {showError}");
                    consentDone = true;
                });
            });
        });

        yield return new WaitUntil(() => consentDone == true);

        var requestConfig = new RequestConfiguration();
#if DEVELOPMENT_BUILD
        for (int i = 0; i < m_adConfig.testDeviceIds.Count; i++)
            requestConfig.TestDeviceIds.Add(m_adConfig.testDeviceIds[i]);
        MobileAds.SetRequestConfiguration(requestConfig);

        // dev 빌드: allowedDeviceIds 목록에 있는 기기만 광고 로드
        string deviceId = SystemInfo.deviceUniqueIdentifier;
        _isDeviceAllowed = m_adConfig.allowedDeviceIds.Contains(deviceId);
        Debug.Log($"[AdManager] DeviceId={deviceId}, Allowed={_isDeviceAllowed}");
        if (_isDeviceAllowed == true)
            InitializeMobileAds();
#else
        MobileAds.SetRequestConfiguration(requestConfig);
        InitializeMobileAds();
#endif
    }

    private void InitializeMobileAds()
    {
        _isDeviceAllowed = true;
        MobileAds.Initialize(_ =>
        {
            Debug.Log("[AdManager] MobileAds 초기화 완료");
            Dispatch(LoadRewardedAd);
        });
    }

    private void LoadRewardedAd()
    {
        _rewardedAd?.Destroy();
        _rewardedAd = null;

        string adUnitId = m_adConfig.GetRewardedAdUnitId();
        if (string.IsNullOrEmpty(adUnitId))
        {
            Debug.LogWarning("[AdManager] Ad Unit ID가 비어있습니다.");
            return;
        }

        var request = new AdRequest();
        RewardedAd.Load(adUnitId, request, (ad, error) =>
        {
            if (error != null)
            {
                Debug.LogWarning($"[AdManager] 리워드 광고 로드 실패: {error}");
                Dispatch(() => StartCoroutine(RetryLoadAfterDelay(30f)));
                return;
            }
            Dispatch(() =>
            {
                _rewardedAd = ad;
                RegisterRewardedAdEvents(ad);
                Debug.Log("[AdManager] 리워드 광고 로드 완료");
            });
        });
    }

    // 광고 미준비 상태에서 입장 허용 후 즉시 재로드 요청
    public void RequestLoad()
    {
        if (IsRewardedAdReady == false)
            LoadRewardedAd();
    }

    private IEnumerator RetryLoadAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        LoadRewardedAd();
    }

    private void RegisterRewardedAdEvents(RewardedAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            Dispatch(() =>
            {
                // 보상 없이 닫힌 경우 = 유저가 직접 닫음
                EAdResult result = _rewardEarned ? EAdResult.Rewarded : EAdResult.UserClosed;
                _onRewardedAdClosed?.Invoke(result);
                _onRewardedAdClosed = null;
                LoadRewardedAd();
            });
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Dispatch(() =>
            {
                Debug.LogWarning($"[AdManager] 리워드 광고 표시 실패: {error}");
                _onRewardedAdClosed?.Invoke(EAdResult.Failed);
                _onRewardedAdClosed = null;
                LoadRewardedAd();
            });
        };
    }

    /// <summary>리워드 광고 표시. callback: Rewarded=보상완료, UserClosed=유저닫음, Failed=표시실패</summary>
    public void ShowRewardedAd(Action<EAdResult> callback)
    {
        if (_rewardedAd == null || _rewardedAd.CanShowAd() == false)
        {
            Debug.LogWarning("[AdManager] 리워드 광고 준비 안 됨");
            callback?.Invoke(EAdResult.Failed);
            LoadRewardedAd();
            return;
        }

        _rewardEarned = false;
        _onRewardedAdClosed = callback;

        _rewardedAd.Show(reward =>
        {
            Dispatch(() =>
            {
                Debug.Log($"[AdManager] 보상 지급: {reward.Type} x{reward.Amount}");
                _rewardEarned = true;
                _onRewardedAdClosed?.Invoke(EAdResult.Rewarded);
                _onRewardedAdClosed = null;
            });
        });
    }

    public bool IsRewardedAdReady => _isDeviceAllowed && _rewardedAd != null && _rewardedAd.CanShowAd();

    public void LogAdReadyStatus(string tag = "")
    {
        bool canShow = _rewardedAd != null && _rewardedAd.CanShowAd();
        Debug.LogWarning($"{tag} AdReady=false | DeviceAllowed={_isDeviceAllowed} | AdLoaded={_rewardedAd != null} | CanShow={canShow}");
    }

    // 개발자 테스트용 — 광고 스킵 플래그 (PlayerPrefs 키: DevSkipAd)
    public static bool s_devSkipAd;

    protected override void OnDestroy()
    {
        _rewardedAd?.Destroy();
        base.OnDestroy();
    }
}
