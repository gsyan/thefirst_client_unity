using System;
using UnityEngine;

// Android 네이티브 Google Sign-In 브릿지
// UnitySendMessage 수신 대상 → GameObject 이름 "GoogleSignInBridge" (MonoSingleton 규칙)
public class GoogleSignInBridge : MonoSingleton<GoogleSignInBridge>
{
    private const string WEB_CLIENT_ID =
        "527468162306-m77vtlkevpa42hf41arcodjmcio5fs85.apps.googleusercontent.com";

    private Action<string> m_onSuccess;
    private Action<string> m_onFailure;

    // NetworkManager에서 호출
    public void RequestSignIn(Action<string> onSuccess, Action<string> onFailure)
    {
        if (m_onSuccess != null)
        {
            Debug.LogWarning("[GoogleSignInBridge] 이미 로그인 진행 중");
            return;
        }

        m_onSuccess = onSuccess;
        m_onFailure = onFailure;

#if UNITY_ANDROID && !UNITY_EDITOR
        using var cls = new AndroidJavaClass("com.fidforge.thefirst.GoogleSignInActivity");
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        cls.CallStatic("startSignIn", activity, WEB_CLIENT_ID);
#else
        Debug.LogWarning("[GoogleSignInBridge] Android 전용. 에디터에서는 동작 안 함");
        InvokeFailure("not_android");
#endif
    }

    // Android UnitySendMessage 콜백
    public void OnSignInSuccess(string idToken)
    {
        var cb = m_onSuccess;
        m_onSuccess = null;
        m_onFailure = null;
        cb?.Invoke(idToken);
    }

    public void OnSignInFailure(string error)
    {
        Debug.LogError($"[GoogleSignInBridge] 실패: {error}");
        InvokeFailure(error);
    }

    private void InvokeFailure(string error)
    {
        var cb = m_onFailure;
        m_onSuccess = null;
        m_onFailure = null;
        cb?.Invoke(error);
    }
}
