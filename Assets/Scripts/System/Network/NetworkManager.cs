//------------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
public class NetworkManager : MonoSingleton<NetworkManager>
{
    #region MonoSingleton ---------------------------------------------------------------
    protected override void OnInitialize()
    {
        m_apiClient = new ApiClient();
        m_apiClient.LoadRefreshToken();

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

    private NetworkReachability m_networkStatus;
    private bool m_bConnected = false;

    private bool m_useFirebaseAuth = false;
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
    


    private IEnumerator RunAsync<T>(Func<Task<ApiResponse<T>>> taskFunc, System.Action<ApiResponse<T>> onComplete, int maxRetries = 2)
    {
        int retryCount = 0;
        Task<ApiResponse<T>> task = null;
        ApiResponse<T> response = null;

        while (retryCount <= maxRetries)
        {
            task = taskFunc();
            while (task.IsCompleted == false)
                yield return null;

            if (task.IsFaulted == true)
            {
                // CustomException에서 ErrorCode 추출
                var taskException = task.Exception?.InnerException;
                ServerErrorCode errorCode = ServerErrorCode.UNKNOWN_ERROR;
                if (taskException is CustomException customEx)
                    errorCode = customEx.ErrorCode;

                // HTTP 401 에러이고 재시도 가능한 경우
                if (errorCode == ServerErrorCode.HTTP_UNAUTHORIZED_401 && retryCount < maxRetries)
                {
                    retryCount++;
                    // RefreshToken 호출
                    Task<ApiResponse<AuthResponse>> refreshTask = m_apiClient.RefreshAccessTokenAsync();
                    while (refreshTask.IsCompleted == false)
                        yield return null;

                    if (refreshTask.IsFaulted == true)
                    {
                        Debug.LogError("refreshTask.IsFaulted == true)");
                        var refreshException = refreshTask.Exception?.InnerException;
                        ServerErrorCode refreshErrorCode = ServerErrorCode.UNKNOWN_ERROR;
                        if (refreshException is CustomException refreshCustomEx)
                            refreshErrorCode = refreshCustomEx.ErrorCode;
                        response = ApiResponse<T>.error((int)refreshErrorCode);
                        break;
                    }

                    if (refreshTask.Result.errorCode != 0)
                    {
                        Debug.LogError("refreshTask.Result.errorCode != 0");
                        response = ApiResponse<T>.error(refreshTask.Result.errorCode);
                        break;
                    }

                    Debug.Log("Token refreshed successfully, retrying original request...");
                    continue; // 재시도
                }

                // 401이 아니거나 재시도 횟수 초과
                response = ApiResponse<T>.error((int)errorCode);
                break;
            }
            else
            {
                response = task.Result ?? ApiResponse<T>.error((int)ServerErrorCode.CLIENT_RUNASYNC_FAIL_UNKONW);
                break;
            }
        }

        // Execute callback
        onComplete?.Invoke(response);
    }

    public void Register(string email, string password, System.Action<ApiResponse<string>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() =>  m_apiClient.SignUpAsync(email, password), onComplete));
    }

    public void Login(string email, string password, System.Action<ApiResponse<AuthResponse>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(async () => {
            try
            {
                var response = await m_apiClient.LoginAsync(email, password);
                if (response.errorCode == 0)
                {
                    m_apiClient.SetTokens(response.data.accessToken, response.data.refreshToken);
                }
                else
                {
                    string errorMessage = ErrorCodeMapping.GetMessage(response.errorCode);
                    Debug.LogError($"Login failed: {errorMessage} (Code: {response.errorCode})");
                    throw new Exception(errorMessage);
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
        //         StartCoroutine(RunAsync(async () => {
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
        //                 m_apiClient.SetTokens(response.data.accessToken, response.data.refreshToken);
        //             }
        //             else
        //             {
        //                 string errorMessage = ErrorCodeMapping.GetMessage(response.errorCode);
        //                 Debug.LogError($"Google Login failed on game server: {errorMessage} (Code: {response.errorCode})");
        //             }

        //             // 콜백 실행
        //             onComplete?.Invoke(response);
        //         }
        //         catch (Exception e)
        //         {
        //             Debug.LogError($"Google login failed: {e.Message}");
        //             onComplete?.Invoke(ApiResponse<AuthResponse>.error((int)ServerErrorCode.UNKNOWN_ERROR));
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
            onComplete?.Invoke(ApiResponse<AuthResponse>.error((int)ServerErrorCode.LOGIN_GOOGLE_FAIL_AUTHENTICATION_TIMEOUT));
            yield break;
        }

        if (string.IsNullOrEmpty(authToken))
        {
            Debug.LogError("[Google OAuth] No token extracted from redirect");
            onComplete?.Invoke(ApiResponse<AuthResponse>.error((int)ServerErrorCode.LOGIN_GOOGLE_FAIL_EXTRACT_AUTHENTICATION));
            yield break;
        }

        // 6) 서버로 Google 로그인 요청
        Debug.Log("[Google OAuth] Sending token to backend...");
        yield return StartCoroutine(RunAsync(async () =>
        {
            try
            {
                var response = await m_apiClient.GoogleLoginAsync(authToken);
                if (response.errorCode == 0)
                {
                    m_apiClient.SetTokens(response.data.accessToken, response.data.refreshToken);
                }
                else
                {
                    string errorMessage = ErrorCodeMapping.GetMessage(response.errorCode);
                    Debug.LogError($"Google Login Failed: {errorMessage}");
                }
                return response;
            }
            catch (Exception e)
            {
                Debug.LogError($"GoogleLoginAsync Exception: {e.Message}");
                return ApiResponse<AuthResponse>.error((int)ServerErrorCode.UNKNOWN_ERROR);
            }
        }, onComplete));
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
        StartCoroutine(RunAsync(() =>  m_apiClient.CreateCharacterAsync(name), onComplete));
    }

    public void GetCharacters(System.Action<ApiResponse<System.Collections.Generic.List<CharacterResponse>>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() =>  m_apiClient.GetAllCharactersAsync(), onComplete));        
    }

    public void SelectCharacter(long characterId, System.Action<ApiResponse<AuthResponse>> onComplete = null)
    {
        if (m_bConnected == false) return;
        // SelectCharacter는 단순히 선택만 하는 게 아니라, characterId가 포함된 새로운 토큰을 받기 위한 API
        StartCoroutine(RunAsync(async () => {
            try
            {
                var response = await m_apiClient.SelectCharacterAsync(characterId);
                if (response.errorCode == 0)
                {
                    m_apiClient.SetTokens(response.data.accessToken, response.data.refreshToken);

                    if (response.data.activeFleetInfo != null)
                        Debug.Log($"Active Fleet: {response.data.activeFleetInfo.fleetName} with {response.data.activeFleetInfo.ships?.Length ?? 0} ships");
                    else
                        Debug.Log("No active fleet found for this character");
                }
                else
                {
                    string errorMessage = ErrorCodeMapping.GetMessage(response.errorCode);
                    Debug.LogError($"Select Character failed: {errorMessage} (Code: {response.errorCode})");
                    throw new Exception(errorMessage);
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
        StartCoroutine(RunAsync(() => m_apiClient.AddShipAsync(request), onComplete));
    }

    public void ChangeFormation(ChangeFormationRequest request, System.Action<ApiResponse<ChangeFormationResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ChangeFormationAsync(request), onComplete));
    }

    public void UpgradeModule(ModuleUpgradeRequest request, System.Action<ApiResponse<ModuleUpgradeResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.UpgradeModuleAsync(request), onComplete));
    }

    public void ChangeModule(ModuleChangeRequest request, System.Action<ApiResponse<ModuleChangeResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ChangeModuleAsync(request), onComplete));
    }

    public void UnlockModule(ModuleUnlockRequest request, System.Action<ApiResponse<ModuleUnlockResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.UnlockModuleAsync(request), onComplete));
    }

    public void ResearchModule(ModuleResearchRequest request, System.Action<ApiResponse<ModuleResearchResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ResearchModuleAsync(request), onComplete));
    }

    // public void AddModuleBody(ModuleBodyAddRequest request, System.Action<ApiResponse<ShipInfo>> onComplete)
    // {
    //     if (m_bConnected == false) return;
    //     StartCoroutine(RunAsync(() => m_apiClient.AddModuleBodyAsync(request), onComplete));
    // }

    public void RemoveModuleBody(ModuleBodyRemoveRequest request, System.Action<ApiResponse<ShipInfo>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.RemoveModuleBodyAsync(request), onComplete));
    }

    public void InstallModule(ModuleInstallRequest request, System.Action<ApiResponse<ShipInfo>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.InstallModuleAsync(request), onComplete));
    }

    // public void GetFleetStats(FleetStatsRequest request, System.Action<ApiResponse<FleetStatsResponse>> onComplete)
    // {
    //     if (m_bConnected == false) return;
    //     StartCoroutine(RunAsync(() => m_apiClient.GetFleetStatsAsync(request), onComplete));
    // }

    public void ExecuteDevCommand(string command, string[] parameters, System.Action<ApiResponse<string>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ExecuteDevCommandAsync(command, parameters), onComplete));
    }


    public void AutoLogin(System.Action<ApiResponse<AuthResponse>> onComplete = null)
    {
        if (m_autoLoginAttempted == true) return;
        m_autoLoginAttempted = true;
        StartCoroutine(RunAsync(() => m_apiClient.RefreshAccessTokenAsync(), onComplete));
    }

    public void Logout()
    {
        m_apiClient.ClearTokens();
        m_autoLoginAttempted = false;
    }

    public void DeleteAccount(System.Action<ApiResponse<string>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(async () => {
            try
            {
                var response = await m_apiClient.DeleteAccountAsync();
                if (response.errorCode == 0)
                    m_apiClient.ClearTokens();
                return response;
            }
            catch (Exception e)
            {
                return ApiResponse<string>.error((int)ServerErrorCode.CLIENT_DELETE_ACCOUNT_FAIL);
            }
        }, onComplete));
    }

    public ApiClient GetApiClient()
    {
        return m_apiClient;
    }
}
