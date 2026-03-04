// AdMob 리워드 광고 로드/표시 관리
// SDK: Google Mobile Ads Unity Plugin 필요
using System;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;

public class AdManager : MonoSingleton<AdManager>
{
    [SerializeField] private AdConfig m_adConfig;

    private RewardedAd _rewardedAd;
    private Action<bool> _onRewardedAdClosed; // true = 보상 지급, false = 취소/실패

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
            m_adConfig = Resources.Load<AdConfig>("DataTable/AdConfig");
            if (m_adConfig == null)
            {
                Debug.LogError("[AdManager] AdConfig를 찾을 수 없습니다.");
                return;
            }
        }

        var requestConfig = new RequestConfiguration();
#if DEVELOPMENT_BUILD
        for (int i = 0; i < m_adConfig.testDeviceIds.Count; i++)
            requestConfig.TestDeviceIds.Add(m_adConfig.testDeviceIds[i]);
#endif
        MobileAds.SetRequestConfiguration(requestConfig);

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

    private void RegisterRewardedAdEvents(RewardedAd ad)
    {
        ad.OnAdFullScreenContentClosed += () =>
        {
            Dispatch(LoadRewardedAd); // 다음 광고 미리 로드
        };

        ad.OnAdFullScreenContentFailed += error =>
        {
            Dispatch(() =>
            {
                Debug.LogWarning($"[AdManager] 리워드 광고 표시 실패: {error}");
                _onRewardedAdClosed?.Invoke(false);
                _onRewardedAdClosed = null;
                LoadRewardedAd();
            });
        };
    }

    /// <summary>리워드 광고 표시. callback: true=보상 지급, false=취소/실패</summary>
    public void ShowRewardedAd(Action<bool> callback)
    {
        if (_rewardedAd == null || _rewardedAd.CanShowAd() == false)
        {
            Debug.LogWarning("[AdManager] 리워드 광고 준비 안 됨");
            callback?.Invoke(false);
            LoadRewardedAd();
            return;
        }

        _onRewardedAdClosed = callback;

        _rewardedAd.Show(reward =>
        {
            Dispatch(() =>
            {
                Debug.Log($"[AdManager] 보상 지급: {reward.Type} x{reward.Amount}");
                _onRewardedAdClosed?.Invoke(true);
                _onRewardedAdClosed = null;
            });
        });
    }

    public bool IsRewardedAdReady => _rewardedAd != null && _rewardedAd.CanShowAd();

    protected override void OnDestroy()
    {
        _rewardedAd?.Destroy();
        base.OnDestroy();
    }
}
