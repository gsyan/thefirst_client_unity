// AdMob 설정 ScriptableObject — Ad Unit ID 및 테스트 기기 목록 관리
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AdConfig", menuName = "Custom/AdConfig")]
public class AdConfig : ScriptableObject
{
    [Header("Ad Unit ID")]
    public string androidRewardedAdUnitId;
    public string iosRewardedAdUnitId;

    [Header("Test Device IDs (개발/에디터 전용)")]
    public List<string> testDeviceIds = new List<string>();

    public string GetRewardedAdUnitId()
    {
#if UNITY_ANDROID
        return androidRewardedAdUnitId;
#elif UNITY_IOS
        return iosRewardedAdUnitId;
#else
        return string.Empty;
#endif
    }
}
