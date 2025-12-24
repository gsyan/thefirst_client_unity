//------------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static ApiClient;

public static class TaskExtensions
{
    public static IEnumerator WrapToCoroutine(this Task task)
    {
        while (!task.IsCompleted)
            yield return null;
        if (task.IsFaulted)
            Debug.LogError($"Task failed: {task.Exception}");
    }
}

public class NetworkManager : MonoSingleton<NetworkManager>
{
    #region MonoSingleton ---------------------------------------------------------------
    protected override void OnInitialize()
    {
        m_apiClient = new ApiClient();

        m_refreshToken = PlayerPrefs.GetString("RefreshToken", "");

        if (SceneManager.GetActiveScene().name == "MainScene")
            GameObject.Find("UICanvas")?.TryGetComponent(out m_uIManager);
        else if (SceneManager.GetActiveScene().name == "SpaceScene")
            GameObject.Find("UICanvas")?.TryGetComponent(out m_uIManager);
        else if (SceneManager.GetActiveScene().name == "LoadingScene")
            GameObject.Find("UICanvas")?.TryGetComponent(out m_uIManager);
    }
    #endregion

    private ApiClient m_apiClient;
    private UIManager m_uIManager;

    private string m_refreshToken;
    private int m_characterCreateRetryCount = 0;
    private const int m_characterCreateMaxRetries = 2;

    private NetworkReachability m_networkStatus;
    private bool m_bConnected = false;
    private Queue<Action> m_pendingRequests = new Queue<Action>();

    private bool m_useFirebaseAuth = true;
    private bool m_autoLoginAttempted = false;

    void Start()
    {
        // Stop operation in background: future thread implementation
        InvokeRepeating(nameof(CheckConnection), 0f, 10f); // Check every 10 seconds
    }

    void CheckConnection()
    {
        if (m_bConnected == true) return;

        m_networkStatus = Application.internetReachability;
        if (m_networkStatus == NetworkReachability.NotReachable)
        {
            m_bConnected = false;
        }
        else
        {
            StartCoroutine(CheckInternetAccess());
        }
    }

    // Check if internet is actually working
    IEnumerator CheckInternetAccess()
    {
        using (UnityEngine.Networking.UnityWebRequest request =
            UnityEngine.Networking.UnityWebRequest.Get("https://www.google.com"))
        {
            request.timeout = 3; // 3 second limit
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                if (m_bConnected == false)
                {
                    m_bConnected = true;
                    yield return StartCoroutine(ProcessPendingRequestsAsync().WrapToCoroutine());

                    if (m_autoLoginAttempted == false && string.IsNullOrEmpty(m_refreshToken) == false)
                    {
                        if (SceneManager.GetActiveScene().name == "MainScene")
                        {
                            AutoLogin((response) => {
                                if (response.errorCode == 0 && m_uIManager != null)
                                {
                                    UIMain uiMain = m_uIManager as UIMain;
                                    if (uiMain != null)
                                        uiMain.GetCharacters();
                                }
                            });
                        }
                        else
                        {
                            AutoLogin(null);
                        }
                    }
                }
            }
        }
    }
    

    private async Task ProcessPendingRequestsAsync()
    {
        while (m_pendingRequests.Count > 0)
        {
            try
            {
                var action = m_pendingRequests.Dequeue();
                await Task.Run(action); // Asynchronous execution
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to process queued request: {e.Message}");
            }
        }
    }


    private IEnumerator RunAsync<T>(Func<Task<ApiResponse<T>>> taskFunc)
    {
        if (m_uIManager?.m_resultText != null)
            m_uIManager.m_resultText.text = "Processing...";
        Task<ApiResponse<T>> task = taskFunc();
        while (!task.IsCompleted)
        {
            yield return null;
        }
        if (task.IsFaulted)
        {
            string errorMessage = task.Exception?.InnerException?.Message ?? "Unknown error";
            Debug.LogError($"Task failed: {errorMessage} - StackTrace: {task.Exception?.StackTrace}");
            if (m_uIManager?.m_resultText != null)
                m_uIManager.m_resultText.text = $"Error: {errorMessage}";
            yield break; // End coroutine on error
        }
        var response = task.Result;
        if (response != null)
        {
            if (response.errorCode == 0)
            {
                Debug.Log($"Operation succeeded: {typeof(T).Name} retrieved");
                if (m_uIManager?.m_resultText != null)
                    m_uIManager.m_resultText.text = $"Operation succeeded: {typeof(T).Name} retrieved";
            }
            else
            {
                Debug.LogError($"Operation failed: {response.errorMessage} (Code: {response.errorCode})");
                if (m_uIManager?.m_resultText != null)
                    m_uIManager.m_resultText.text = $"Operation failed: {response.errorMessage} (Code: {response.errorCode})";
            }
        }
    }

    private IEnumerator RunAsyncWithCallback<T>(Func<Task<ApiResponse<T>>> taskFunc, System.Action<ApiResponse<T>> onComplete)
    {
        Task<ApiResponse<T>> task = taskFunc();
        while (!task.IsCompleted)
            yield return null;

        ApiResponse<T> response;

        if (task.IsFaulted)
        {
            string errorMessage = task.Exception?.InnerException?.Message ?? "Unknown error";
            response = ApiResponse<T>.error((int)ServerErrorCode.UNKNOWN_ERROR, errorMessage);
        }
        else
        {
            response = task.Result ?? ApiResponse<T>.error((int)ServerErrorCode.UNKNOWN_ERROR, "Invalid server response");
        }

        // Execute callback
        onComplete?.Invoke(response);
    }

    public void Register(string email, string password, System.Action<ApiResponse<string>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(async () => {
            try
            {
                var response = await m_apiClient.SignUpAsync(email, password);
                if (response.errorCode == 0)
                {
                    Debug.Log($"Signed up: {response.data}");
                }
                else
                {
                    Debug.LogError($"SignUp failed: {response.errorMessage} (Code: {response.errorCode})");
                    throw new Exception(response.errorMessage);
                }
                return response;
            }
            catch (Exception e)
            {
                Debug.LogError($"SignUp failed: {e.Message}");
                throw;
            }
        }, onComplete));
    }

    public void Login(string email, string password, System.Action<ApiResponse<AuthResponse>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(async () => {
            try
            {
                var response = await m_apiClient.LoginAsync(email, password);
                if (response.errorCode == 0)
                {
                    m_apiClient.SetAccessToken(response.data.accessToken);
                    m_refreshToken = response.data.refreshToken;
                    PlayerPrefs.SetString("RefreshToken", m_refreshToken);
                    PlayerPrefs.Save();
                    Debug.Log($"=== Login Success === Email: {email}");
                }
                else
                {
                    Debug.LogError($"Login failed: {response.errorMessage} (Code: {response.errorCode})");
                    throw new Exception(response.errorMessage);
                }
                return response;
            }
            catch (Exception e)
            {
                Debug.LogError($"Login failed: {e.Message}");
                throw;
            }
        }, onComplete));
    }

    public void GoogleLogin(System.Action<ApiResponse<AuthResponse>> onComplete = null)
    {
        if (m_bConnected == false) return;

        if (m_useFirebaseAuth == true)
        {
            GoogleLoginFirebase(onComplete);
        }
        else
        {
            StartCoroutine(GoogleLoginCoroutine(onComplete));
        }
    }

    private void GoogleLoginFirebase(System.Action<ApiResponse<AuthResponse>> onComplete = null)
    {
        // FirebaseAuthManager.Instance.SignInWithGPGS(
        //     (firebaseIdToken) =>
        //     {
        //         StartCoroutine(RunAsyncWithCallback(async () => {
        //             try
        //             {
        //                 var response = await m_apiClient.GoogleLoginAsync(firebaseIdToken);
        //                 if (response.errorCode == 0)
        //                 {
        //                     m_apiClient.SetAccessToken(response.data.accessToken);
        //                     m_refreshToken = response.data.refreshToken;
        //                     PlayerPrefs.SetString("RefreshToken", m_refreshToken);
        //                     PlayerPrefs.Save();
        //                 }
        //                 return response;
        //             }
        //             catch (Exception e)
        //             {
        //                 return ApiResponse<AuthResponse>.error((int)ServerErrorCode.UNKNOWN_ERROR, e.Message);
        //             }
        //         }, onComplete));
        //     },
        //     () =>
        //     {
        //         onComplete?.Invoke(ApiResponse<AuthResponse>.error((int)ServerErrorCode.UNKNOWN_ERROR, "Firebase authentication failed"));
        //     }
        // );
    }

    private void GoogleLoginWebView(System.Action<ApiResponse<AuthResponse>> onComplete = null)
    {
        StartCoroutine(GoogleLoginCoroutine(onComplete));

        // FirebaseAuthManager.Instance.SignInWithGoogle(
        //     async (firebaseUser) =>
        //     {
        //         try
        //         {
        //             // 1) Firebase ID Token 획득
        //             string idToken = await firebaseUser.TokenAsync(true);
        //             Debug.Log($"[GoogleLogin] Firebase ID Token: {idToken}");

        //             // 2) 내 게임 서버에 Google Login 요청
        //             var response = await m_apiClient.GoogleLoginAsync(idToken);

        //             // 3) 성공 시 Access/Refresh Token 저장
        //             if (response.errorCode == 0)
        //             {
        //                 m_apiClient.SetAccessToken(response.data.accessToken);
        //                 m_refreshToken = response.data.refreshToken;
        //                 PlayerPrefs.SetString("RefreshToken", m_refreshToken);
        //                 PlayerPrefs.Save();

        //                 Debug.Log($"=== Google Login Success === User: {firebaseUser.DisplayName}");
        //             }
        //             else
        //             {
        //                 Debug.LogError($"Google Login failed on game server: {response.errorMessage} (Code: {response.errorCode})");
        //             }

        //             // 콜백 실행
        //             onComplete?.Invoke(response);
        //         }
        //         catch (Exception e)
        //         {
        //             Debug.LogError($"Google login failed: {e.Message}");
        //             onComplete?.Invoke(ApiResponse<AuthResponse>.error((int)ServerErrorCode.UNKNOWN_ERROR, e.Message));
        //         }
        //     },
        //     () =>
        //     {
        //         Debug.LogError("Google Login cancelled or Firebase stage failed.");
        //         onComplete?.Invoke(ApiResponse<AuthResponse>.error((int)ServerErrorCode.UNKNOWN_ERROR, "Firebase authentication failed."));
        //     }
        // );
    }

    private IEnumerator GoogleLoginCoroutine(System.Action<ApiResponse<AuthResponse>> onComplete)
    {
        // 1) WebView 생성
        var webViewGO = new GameObject("GoogleLoginWebView");
        var webView = webViewGO.AddComponent<WebViewObject>();

        bool redirectDetected = false;
        string authToken = null;

        // 2) WebView 초기화 + redirect 감지
        webView.Init(
            cb: (msg) =>
            {
                Debug.Log($"[WebView Msg] {msg}");
            },
            err: (msg) =>
            {
                Debug.LogError($"[WebView Error] {msg}");
            },
            started: (url) =>
            {
                Debug.Log($"[WebView Started] {url}");
                // Check redirect in started callback
                if (url.StartsWith("https://thefirst-fd116.firebaseapp.com/__/auth/handler"))
                {
                    Debug.Log("[Google OAuth] Redirect URL Captured in started!");
                    redirectDetected = true;
                    authToken = ExtractToken(url);
                }
            },
            hooked: (url) =>
            {
                Debug.Log($"[WebView Hooked] {url}");

                // Redirect URL 감지
                if (url.StartsWith("https://thefirst-fd116.firebaseapp.com/__/auth/handler"))
                {
                    Debug.Log("[Google OAuth] Redirect URL Captured!");

                    redirectDetected = true;
                    authToken = ExtractToken(url);

                    Debug.Log("[Google OAuth] Extracted Token => " + authToken);
                }
                else
                {
                    Debug.Log("[Google OAuth] Hooked but not redirect URL.");
                }
            },
            // claude 추천
            enableWKWebView: true,
            wkContentMode: 0,
            // CRITICAL FIX: Set User-Agent to regular Chrome browser
            ua: "Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Mobile Safari/537.36"


        );
        
        // claude 추천
        // Set margins (full screen)
        int margin = 0;
        webView.SetMargins(margin, margin, margin, margin);


        webView.SetVisibility(true);

        // 3) Google OAuth URL 로딩
        string clientId = "527468162306-m77vtlkevpa42hf41arcodjmcio5fs85.apps.googleusercontent.com"; // YOUR_WEB_CLIENT_ID.apps.googleusercontent.com
        string redirectUri = "https://thefirst-fd116.firebaseapp.com/__/auth/handler"; // https://localhost/auth/callback
        string scope = "openid%20email%20profile";
        string nonce = System.Guid.NewGuid().ToString("N");

        string authUrl =
            "https://accounts.google.com/o/oauth2/v2/auth" +
            "?client_id=" + clientId +
            "&redirect_uri=" + redirectUri +
            "&response_type=id_token" +      // id_token 방식 사용
            "&scope=" + scope +
            "&nonce=" + nonce;

        Debug.Log("[Google OAuth] Loading URL: " + authUrl);
        webView.LoadURL(authUrl);

        // claude 추천
        // Wait for redirect
        float timeout = 300f; // 5 minutes
        float elapsed = 0f;


        // 4) redirect 될 때까지 기다리기
        while (redirectDetected == false && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }            

        // 5) WebView 닫기
        webView.SetVisibility(false);
        GameObject.Destroy(webViewGO);

        if (redirectDetected == false)
        {
            Debug.LogError("[Google OAuth] Timeout - no redirect detected");
            onComplete?.Invoke(ApiResponse<AuthResponse>.error(
                (int)ServerErrorCode.UNKNOWN_ERROR,
                "Authentication timeout"
            ));
            yield break;
        }

        if (string.IsNullOrEmpty(authToken))
        {
            Debug.LogError("[Google OAuth] No token extracted from redirect");
            onComplete?.Invoke(ApiResponse<AuthResponse>.error(
                (int)ServerErrorCode.UNKNOWN_ERROR,
                "Failed to extract authentication token"
            ));
            yield break;
        }

        // 6) 서버로 Google 로그인 요청
        Debug.Log("[Google OAuth] Sending token to backend...");
        var task = m_apiClient.GoogleLoginAsync(authToken);
        yield return task.WrapToCoroutine();

        if (task.Exception == null)
        {
            var res = task.Result;

            if (res != null && res.errorCode == 0)
            {
                Debug.Log("=== Google Login Success ===");
                m_apiClient.SetAccessToken(res.data.accessToken);
                m_refreshToken = res.data.refreshToken;
                PlayerPrefs.SetString("RefreshToken", m_refreshToken);
                PlayerPrefs.Save();
                onComplete?.Invoke(res);
            }
            else
            {
                Debug.LogError($"Google Login Failed: {res?.errorMessage}");
                onComplete?.Invoke(res);
            }
        }
        else
        {
            Debug.LogError("GoogleLoginAsync Exception: " + task.Exception);
            onComplete?.Invoke(ApiResponse<AuthResponse>.error(
                (int)ServerErrorCode.UNKNOWN_ERROR,
                task.Exception.Message
            ));
        }
    }

    private string ExtractToken(string url)
    {
        Debug.Log("[ExtractToken] Raw URL => " + url);

        if (string.IsNullOrEmpty(url))
            return null;

        // 1) fragment(#) 처리
        int hashIndex = url.IndexOf('#');
        if (hashIndex >= 0)
        {
            string fragment = url.Substring(hashIndex + 1); // id_token=...&...
            Debug.Log("[ExtractToken] Fragment => " + fragment);
            var parameters = fragment.Split('&');
            foreach (var param in parameters)
            {
                if (param.StartsWith("id_token="))
                {
                    string token = param.Substring("id_token=".Length);
                    Debug.Log("[ExtractToken] Found id_token in fragment.");
                    return token;
                }

                if (param.StartsWith("code="))
                {
                    string code = param.Substring("code=".Length);
                    Debug.Log("[ExtractToken] Found code in fragment.");
                    return code;
                }
            }
        }

        // 2) query(?) 처리
        int qIndex = url.IndexOf('?');
        if (qIndex >= 0)
        {
            string query = url.Substring(qIndex + 1); // id_token=...&...
            Debug.Log("[ExtractToken] Query => " + query);

            var parameters = query.Split('&');
            foreach (var param in parameters)
            {
                if (param.StartsWith("id_token="))
                {
                    string token = param.Substring("id_token=".Length);
                    Debug.Log("[ExtractToken] Found id_token in query.");
                    return token;
                }

                if (param.StartsWith("code="))
                {
                    string code = param.Substring("code=".Length);
                    Debug.Log("[ExtractToken] Found code in query.");
                    return code;
                }
            }
        }

        Debug.LogError("[ExtractToken] No id_token or code found → URL did NOT contain token.");
        return null;
    }


    public void CreateCharacter(string name, System.Action<ApiResponse<CharacterResponse>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(async () => {
            try
            {
                var response = await m_apiClient.CreateCharacterAsync(name);
                if (response == null)
                {
                    Debug.LogError("Response is null");
                    return ApiResponse<CharacterResponse>.error((int)ServerErrorCode.CHARACTER_CREATE_FAIL_REASON1, "Invalid server response");
                }
                if (response.errorCode == 0)
                {
                    Debug.Log($"Character created: {response.data.characterName}, ID: {response.data.characterId}");
                }
                else
                {
                    Debug.LogError($"Character creation failed: {response.errorMessage} (Code: {response.errorCode})");
                }
                return response;
            }
            catch (Exception e)
            {
                Debug.LogError($"Character creation failed: {e.Message}");
                if (e.Message.Contains("401") && m_characterCreateRetryCount < m_characterCreateMaxRetries)
                {
                    m_characterCreateRetryCount++;
                    await RefreshAccessTokenAsync();
                    return await m_apiClient.CreateCharacterAsync(name);
                }
                m_characterCreateRetryCount = 0;
                return ApiResponse<CharacterResponse>.error((int)ServerErrorCode.UNKNOWN_ERROR, e.Message);
            }
        }, onComplete));
    }

    public void GetCharacters(System.Action<ApiResponse<System.Collections.Generic.List<CharacterResponse>>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(async () => {
            try
            {
                var response = await m_apiClient.GetAllCharactersAsync();
                if (response == null)
                    return ApiResponse<List<CharacterResponse>>.error((int)ServerErrorCode.UNKNOWN_ERROR, "Invalid server response");
                if (response.errorCode == 0)
                {
                    Debug.Log("Characters retrieved successfully");
                    foreach (var character in response.data)
                    {
                        Debug.Log($"Character: {character.characterName}, ID: {character.characterId}");
                    }
                }
                else
                {
                    Debug.LogError($"GetAllCharacters failed: {response.errorMessage} (Code: {response.errorCode})");
                    throw new Exception(response.errorMessage);
                }
                return response;
            }
            catch (Exception e)
            {
                Debug.LogError($"GetAllCharacters failed: {e.Message}");
                throw;
            }
        }, onComplete));
    }

    public void SelectCharacter(long characterId, System.Action<ApiResponse<AuthResponse>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(async () => {
            try
            {
                var response = await m_apiClient.SelectCharacterAsync(characterId);
                if (response == null)
                {
                    Debug.LogError("Response is null");
                    return ApiResponse<AuthResponse>.error((int)ServerErrorCode.UNKNOWN_ERROR, "Invalid server response");
                }
                if (response.errorCode == 0)
                {
                    m_apiClient.SetAccessToken(response.data.accessToken);
                    m_refreshToken = response.data.refreshToken;
                    PlayerPrefs.SetString("RefreshToken", m_refreshToken);
                    PlayerPrefs.Save();

                    Debug.Log($"Select Character - Access Token: {response.data.accessToken}");
                    if (response.data.activeFleetInfo != null)
                    {
                        Debug.Log($"Active Fleet: {response.data.activeFleetInfo.fleetName} with {response.data.activeFleetInfo.ships?.Length ?? 0} ships");
                    }
                    else
                    {
                        Debug.Log("No active fleet found for this character");
                    }
                }
                else
                {
                    Debug.LogError($"Select Character failed: {response.errorMessage} (Code: {response.errorCode})");
                    throw new Exception(response.errorMessage);
                }
                return response;
            }
            catch (Exception e)
            {
                Debug.LogError($"Select Character failed: {e.Message}");
                throw;
            }
        }, onComplete));
    }

    public void AddShip(AddShipRequest request, System.Action<ApiResponse<AddShipResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(() => m_apiClient.AddShipAsync(request), onComplete));
    }

    public void ChangeFormation(ChangeFormationRequest request, System.Action<ApiResponse<ChangeFormationResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(() => m_apiClient.ChangeFormationAsync(request), onComplete));
    }

    public void UpgradeModule(ModuleUpgradeRequest request, System.Action<ApiResponse<ModuleUpgradeResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(() => m_apiClient.UpgradeModuleAsync(request), onComplete));
    }

    public void ChangeModule(ModuleChangeRequest request, System.Action<ApiResponse<ModuleChangeResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(() => m_apiClient.ChangeModuleAsync(request), onComplete));
    }

    public void UnlockModule(ModuleUnlockRequest request, System.Action<ApiResponse<ModuleUnlockResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(() => m_apiClient.UnlockModuleAsync(request), onComplete));
    }

    public void ResearchModule(ModuleResearchRequest request, System.Action<ApiResponse<ModuleResearchResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(() => m_apiClient.ResearchModuleAsync(request), onComplete));
    }

    public void AddModuleBody(ModuleBodyAddRequest request, System.Action<ApiResponse<ShipInfo>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(() => m_apiClient.AddModuleBodyAsync(request), onComplete));
    }

    public void RemoveModuleBody(ModuleBodyRemoveRequest request, System.Action<ApiResponse<ShipInfo>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(() => m_apiClient.RemoveModuleBodyAsync(request), onComplete));
    }

    public void InstallModule(ModuleInstallRequest request, System.Action<ApiResponse<ShipInfo>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(() => m_apiClient.InstallModuleAsync(request), onComplete));
    }

    public void GetFleetStats(FleetStatsRequest request, System.Action<ApiResponse<FleetStatsResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(() => m_apiClient.GetFleetStatsAsync(request), onComplete));
    }

    public void ExecuteDevCommand(string command, string[] parameters, System.Action<ApiResponse<string>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(() => m_apiClient.ExecuteDevCommandAsync(command, parameters), onComplete));
    }

    public async Task<ApiResponse<AuthResponse>> RefreshAccessTokenAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(m_refreshToken))
            {
                throw new Exception("No refresh token available");
            }
            var response = await m_apiClient.RefreshTokenAsync(m_refreshToken);
            if (response.errorCode == 0)
            {
                m_apiClient.SetAccessToken(response.data.accessToken);
                m_refreshToken = response.data.refreshToken;
                PlayerPrefs.SetString("RefreshToken", m_refreshToken);
                PlayerPrefs.Save();
                Debug.Log($"Token refreshed: New Access Token: {response.data.accessToken}");
            }
            else
            {
                Debug.LogError($"Token refresh failed: {response.errorMessage} (Code: {response.errorCode})");
                throw new Exception(response.errorMessage);
            }
            return response;
        }
        catch (Exception e)
        {
            Debug.LogError($"Token refresh failed: {e.Message}");
            throw;
        }
    }

    public void AutoLogin(System.Action<ApiResponse<AuthResponse>> onComplete = null)
    {
        if (m_autoLoginAttempted) return;
        if (string.IsNullOrEmpty(m_refreshToken))
        {
            onComplete?.Invoke(ApiResponse<AuthResponse>.error((int)ServerErrorCode.INVALID_TOKEN, "No saved login"));
            return;
        }

        m_autoLoginAttempted = true;
        StartCoroutine(RunAsyncWithCallback(async () => {
            try
            {
                var response = await RefreshAccessTokenAsync();
                return response;
            }
            catch (Exception e)
            {
                ClearLoginData();
                return ApiResponse<AuthResponse>.error((int)ServerErrorCode.INVALID_TOKEN, e.Message);
            }
        }, onComplete));
    }

    private void ClearLoginData()
    {
        m_refreshToken = "";
        PlayerPrefs.DeleteKey("RefreshToken");
        PlayerPrefs.Save();
        m_apiClient.SetAccessToken(null);
    }

    public void Logout()
    {
        ClearLoginData();
        m_autoLoginAttempted = false;
    }

    public void DeleteAccount(System.Action<ApiResponse<string>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsyncWithCallback(async () => {
            try
            {
                var response = await m_apiClient.DeleteAccountAsync();
                if (response.errorCode == 0)
                {
                    ClearLoginData();
                }
                return response;
            }
            catch (Exception e)
            {
                return ApiResponse<string>.error((int)ServerErrorCode.UNKNOWN_ERROR, e.Message);
            }
        }, onComplete));
    }

    public ApiClient GetApiClient()
    {
        return m_apiClient;
    }
}
