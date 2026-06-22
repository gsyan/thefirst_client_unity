// NetworkManager — 서버 API 호출 관리, 토큰 자동 갱신(401 retry) 포함
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
        //PlayerPrefs.DeleteAll();
        m_apiClient = new ApiClient();
        m_apiClient.LoadRefreshToken();
    }
    #endregion

    private ApiClient m_apiClient;
    private UIManager m_uIManager;

    // 동시 401 발생 시 refresh 중복 호출 방지 — 진행 중인 task를 공유
    private Task<ApiResponse<AuthResponse>> m_pendingRefreshTask = null;

    private NetworkReachability m_networkStatus;
    private bool m_bConnected = false;

    private bool m_useFirebaseAuth = false;
    private bool m_autoLoginAttempted = false;

    // 하트비트 간격 (초) — 서버에서 lastOnlineAt 갱신용
    private const float HeartbeatInterval = 30f;
    private bool m_heartbeatStarted = false;
    private int m_heartbeatFailCount = 0;
    private const int HeartbeatMaxFail = 3;
    // 복귀 즉시 하트비트 중복 방지 (OnApplicationPause/OnApplicationFocus 동시 발동 대응)
    private float m_lastResumeHeartbeatTime = -999f;
    private const float ResumeHeartbeatCool = 5f;

    // 인터넷 체크 코루틴 중복 실행 방지 (10초 타이머와의 충돌)
    private bool m_checkingInternetAccess = false;
    // Dev 빌드 서버 선택 팝업: 한 번만 표시
    private bool m_serverSelectShown = false;

    public void OnChangeScene()
    {
        if (SceneManager.GetActiveScene().name == "MainScene")
        {
            GameObject.Find("UICanvas")?.TryGetComponent(out m_uIManager);
            m_bConnected = false;
            m_heartbeatStarted = false;
            m_bNetworkPopupShown = false;
            m_checkingInternetAccess = false;
            m_serverSelectShown = false;
            InvokeRepeating(nameof(CheckConnection), 0f, 10f);
        }
        else if (SceneManager.GetActiveScene().name == "SpaceScene")
        {
            GameObject.Find("UICanvas")?.TryGetComponent(out m_uIManager);
        }
        else if (SceneManager.GetActiveScene().name == "LoadingScene")
            GameObject.Find("UICanvas")?.TryGetComponent(out m_uIManager);
    }

    // 로그인 수확 팝업 처리 완료 후 ObjectManager에서 호출
    public void StartHeartbeat()
    {
        m_heartbeatStarted = true;
        m_heartbeatFailCount = 0;
        CancelInvoke(nameof(Heartbeat));
        InvokeRepeating(nameof(Heartbeat), HeartbeatInterval, HeartbeatInterval);
    }

    void CheckConnection()
    {
        if (m_bConnected == true) return;

        m_networkStatus = Application.internetReachability;
        if (m_networkStatus == NetworkReachability.NotReachable)
        {
            m_bConnected = false;
            ShowFatalErrorPopup("Network Error", "Please check your internet connection.\nThe app will close.");
        }
        else
            StartCoroutine(CheckInternetAccess());
    }

    private bool m_bNetworkPopupShown = false;

    private void ShowFatalErrorPopup(string title, string message)
    {
        if (m_bNetworkPopupShown) return;
        m_bNetworkPopupShown = true;

        CancelInvoke(nameof(CheckConnection));

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title        = title,
            message      = message,
            autoCloseSec = 5f,
            onConfirm    = () => {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            },
        });
    }

    // Check if internet is actually working
    IEnumerator CheckInternetAccess()
    {
        if (m_checkingInternetAccess == true) yield break;
        m_checkingInternetAccess = true;

        using (UnityEngine.Networking.UnityWebRequest request =
            UnityEngine.Networking.UnityWebRequest.Get("https://www.google.com"))
        {
            request.timeout = 3; // 3 second limit
            yield return request.SendWebRequest();

            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                m_checkingInternetAccess = false;
                ShowFatalErrorPopup("Internet Error", "Please check your internet connection.\nThe app will close.");
                yield break;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (m_serverSelectShown == false)
        {
            m_serverSelectShown = true;
            bool serverChosen = false;
            UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
            {
                title        = "서버 선택",
                message      = "접속할 서버를 선택하세요.",
                cancelText1  = "DEV",
                cancelText2  = "localhost:8080",
                confirmText1 = "TEST",
                confirmText2 = "dev.fidforge.com",
                onCancel = () =>
                {
                    m_apiClient.SetBaseUrl(ApiServerUrl.Dev);
                    serverChosen = true;
                },
                onConfirm = () =>
                {
                    m_apiClient.SetBaseUrl(ApiServerUrl.Test);
                    serverChosen = true;
                },
            });
            yield return new WaitUntil(() => serverChosen == true);
        }
#endif

        // 서버 체크
        var serverCheckTask = m_apiClient.CheckServerAliveAsync();
        while (!serverCheckTask.IsCompleted)
            yield return null;

        if (serverCheckTask.Result == false)
        {
            m_checkingInternetAccess = false;
            ShowFatalErrorPopup("Server Not Found", "The server is currently unavailable.\nPlease try again later.");
            yield break;
        }

        // 버전 체크 — 서버 접속 직후, 로그인 전
        var versionCheckTask = m_apiClient.CheckVersionAsync(GetAppVersionCode());
        while (!versionCheckTask.IsCompleted)
            yield return null;

        if (versionCheckTask.IsFaulted == false && versionCheckTask.Result.errorCode == 0)
        {
            VersionCheckResponse versionData = versionCheckTask.Result.data;
            //if (versionData.updateRequired == true)
            if (true) // 로컬라이즈 테스트용 임시 강제 실행
            {
                m_checkingInternetAccess = false;
                string title   = LocalizationManager.Instance.Get("UIPopupMessage_VersionUpdateTitle");
                string message = LocalizationManager.Instance.Get("UIPopupMessage_VersionUpdateMessage", (object)versionData.minVersionName);
                string btnText = LocalizationManager.Instance.Get("UIPopupMessage_VersionUpdateButton");
                UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
                {
                    title        = title,
                    message      = message,
                    confirmText1 = btnText,
                    onConfirm    = () => {
                        Application.OpenURL("market://details?id=com.fidforge.thefirst");
                    },
                });
                yield break;
            }
        }

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
                            uiMain.GetCommanders();
                    }
                    else
                    {
                        // 에러 처리
                        switch ((ServerErrorCode)response.errorCode)
                        {
                            case ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL:
                                UIManager.Instance.ShowPanel("UIPanelLoginType");
                                break;
                            case ServerErrorCode.REFRESH_TOKEN_FAIL_INVALID_TOKEN:
                            case ServerErrorCode.REFRESH_TOKEN_FAIL_EMPTY_TOKEN:
                            case ServerErrorCode.REFRESH_TOKEN_FAIL_ACCOUNT_NOT_FOUND:
                            case ServerErrorCode.HTTP_UNAUTHORIZED_401:
                                // 토큰 삭제 필요
                                PlayerPrefs.DeleteKey("RefreshToken");
                                PlayerPrefs.Save();
                                UIManager.Instance.ShowPanel("UIPanelLoginType");
                                break;
                            case ServerErrorCode.HTTP_SERVER_ERROR_500:
                            case ServerErrorCode.UNKNOWN_ERROR:
                                // 서버 에러 - 토큰 유지, 로그만 기록
                                Debug.LogWarning($"AutoLogin failed with server error: {response.errorCode}");
                                break;
                            default:
                                // 기타 에러
                                Debug.LogError($"AutoLogin failed with error: {response.errorCode}");
                                break;
                        }
                    }
                });
            }
            else
            {
                AutoLogin(null);
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
                    // 동시에 여러 401이 발생해도 refresh는 한 번만 — 진행 중인 task 공유
                    if (m_pendingRefreshTask == null || m_pendingRefreshTask.IsCompleted == true)
                        m_pendingRefreshTask = m_apiClient.RefreshAccessTokenAsync();
                    Task<ApiResponse<AuthResponse>> refreshTask = m_pendingRefreshTask;
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
        StartCoroutine(RunAsync(() => m_apiClient.LoginAsync(email, password), onComplete));
    }

    public void GoogleLogin(System.Action<ApiResponse<AuthResponse>> onComplete = null)
    {
        if (m_bConnected == false) return;
        if (m_useFirebaseAuth == true)
            GoogleLoginFirebase(onComplete);
        else
            StartCoroutine(GoogleWithIdTokenCoroutine(
                onComplete,
                m_apiClient.GoogleLoginAsync,
                ServerErrorCode.CLIENT_LOGIN_GOOGLE_FAIL_AUTHENTICATION_TIMEOUT,
                ServerErrorCode.CLIENT_LOGIN_GOOGLE_FAIL_EXTRACT_AUTHENTICATION));
    }

    public void GuestLogin(System.Action<ApiResponse<AuthResponse>> onComplete = null)
    {
        if (m_bConnected == false) return;

        // PlayerPrefs에서 guestId 가져오기, 없으면 새로 생성
        string guestId = PlayerPrefs.GetString("GuestId", "");
        if (string.IsNullOrEmpty(guestId))
        {
            guestId = System.Guid.NewGuid().ToString();
            PlayerPrefs.SetString("GuestId", guestId);
            PlayerPrefs.Save();
        }
        
        StartCoroutine(RunAsync(() => m_apiClient.GuestLoginAsync(guestId), onComplete));
    }

    // 현재 로그인된 계정에 구글 계정 연동
    public void LinkGoogle(System.Action<ApiResponse<AuthResponse>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(GoogleWithIdTokenCoroutine(
            onComplete,
            m_apiClient.LinkGoogleAsync,
            ServerErrorCode.CLIENT_LOGIN_GOOGLE_FAIL_AUTHENTICATION_TIMEOUT,
            ServerErrorCode.CLIENT_LOGIN_GOOGLE_FAIL_EXTRACT_AUTHENTICATION));
    }

    // 구글 연동 해제 — 성공 시 guestId를 PlayerPrefs에 저장
    public void UnlinkGoogle(Action<ApiResponse<UnlinkGoogleResponse>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.UnlinkGoogleAsync(), onComplete));
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

    // Google idToken 획득 후 지정된 API 호출 — 로그인/연동 공용
    private IEnumerator GoogleWithIdTokenCoroutine(
        Action<ApiResponse<AuthResponse>> onComplete,
        Func<string, Task<ApiResponse<AuthResponse>>> apiCall,
        ServerErrorCode timeoutError,
        ServerErrorCode extractError)
    {
        string idToken = null;
        int tokenError = 0;
        yield return StartCoroutine(GetGoogleIdTokenCoroutine(
            (token, err) => { idToken = token; tokenError = err; },
            timeoutError,
            extractError));

        if (tokenError != 0)
        {
            onComplete?.Invoke(ApiResponse<AuthResponse>.error(tokenError));
            yield break;
        }

        yield return StartCoroutine(RunAsync(() => apiCall(idToken), onComplete));
    }

    // Google idToken 획득 공통 헬퍼 — PC: 시스템 브라우저, Mobile: 네이티브 Google Sign-In
    private IEnumerator GetGoogleIdTokenCoroutine(
        Action<string, int> onTokenResult,
        ServerErrorCode timeoutError,
        ServerErrorCode extractError)
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        yield return StartCoroutine(GetGoogleIdTokenForPCCoroutine(onTokenResult, timeoutError, extractError));
#else
        bool done = false;
        string resultToken = null;
        int resultErr = 0;

        GoogleSignInBridge.Instance.RequestSignIn(
            (token) => { resultToken = token; done = true; },
            (error) => { resultErr = (int)extractError; done = true; }
        );

        float elapsed = 0f;
        while (done == false && elapsed < 120f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (done == false)
        {
            Debug.LogError("[Google SignIn] Timeout");
            onTokenResult(null, (int)timeoutError);
            yield break;
        }

        if (string.IsNullOrEmpty(resultToken))
        {
            onTokenResult(null, resultErr);
            yield break;
        }

        onTokenResult(resultToken, 0);
#endif
    }

#if UNITY_EDITOR || UNITY_STANDALONE
    // PC: 시스템 브라우저 + HttpListener
    // fragment(#)는 서버로 전달되지 않으므로, JS로 /token?id_token=... 으로 재리다이렉트
    private IEnumerator GetGoogleIdTokenForPCCoroutine(
        Action<string, int> onTokenResult,
        ServerErrorCode timeoutError,
        ServerErrorCode extractError)
    {
        const int port = 5678;
        string idToken = null;
        bool done = false;

        var listener = new System.Net.HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");

        try { listener.Start(); }
        catch (Exception e)
        {
            Debug.LogError($"[Google OAuth PC] HttpListener 시작 실패: {e.Message}");
            onTokenResult(null, (int)extractError);
            yield break;
        }

        // 시스템 브라우저로 구글 로그인 열기
        string clientId = "527468162306-m77vtlkevpa42hf41arcodjmcio5fs85.apps.googleusercontent.com";
        string redirectUri = $"http://localhost:{port}/auth";
        string nonce = Guid.NewGuid().ToString("N");
        string authUrl =
            "https://accounts.google.com/o/oauth2/v2/auth" +
            "?client_id=" + clientId +
            "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
            "&response_type=id_token" +
            "&scope=openid%20email%20profile" +
            "&nonce=" + nonce;

        Application.OpenURL(authUrl);
        Debug.Log("[Google OAuth PC] 브라우저 열기: " + authUrl);

        // 백그라운드에서 두 번의 요청 처리
        Task.Run(async () =>
        {
            try
            {
                // 1st: /auth#id_token=... → fragment를 query로 재리다이렉트하는 HTML 반환
                var ctx = await listener.GetContextAsync();
                byte[] html = System.Text.Encoding.UTF8.GetBytes(
                    "<html><body><script>" +
                    "window.location.href='/token?' + window.location.hash.substring(1);" +
                    "</script>잠시만 기다려주세요...</body></html>");
                ctx.Response.ContentType = "text/html; charset=utf-8";
                ctx.Response.ContentLength64 = html.Length;
                await ctx.Response.OutputStream.WriteAsync(html, 0, html.Length);
                ctx.Response.Close();

                // 2nd: /token?id_token=... → 토큰 추출
                ctx = await listener.GetContextAsync();
                idToken = ctx.Request.QueryString["id_token"];
                byte[] closeHtml = System.Text.Encoding.UTF8.GetBytes(
                    "<html><body><h3>로그인 완료. 게임으로 돌아가세요.</h3></body></html>");
                ctx.Response.ContentType = "text/html; charset=utf-8";
                ctx.Response.ContentLength64 = closeHtml.Length;
                await ctx.Response.OutputStream.WriteAsync(closeHtml, 0, closeHtml.Length);
                ctx.Response.Close();
            }
            catch (Exception e)
            {
                if (listener.IsListening)
                    Debug.LogError($"[Google OAuth PC] 리스너 에러: {e.Message}");
            }
            finally { done = true; }
        });

        // 최대 5분 대기
        float elapsed = 0f;
        while (done == false && elapsed < 300f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (listener.IsListening)
            listener.Stop();

        if (done == false)
        {
            Debug.LogError("[Google OAuth PC] Timeout");
            onTokenResult(null, (int)timeoutError);
            yield break;
        }

        if (string.IsNullOrEmpty(idToken))
        {
            Debug.LogError("[Google OAuth PC] idToken 추출 실패");
            onTokenResult(null, (int)extractError);
            yield break;
        }

        onTokenResult(idToken, 0);
    }
#endif


    public void CreateCommander(string name, System.Action<ApiResponse<CommanderResponse>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() =>  m_apiClient.CreateCommanderAsync(name), onComplete));
    }

    public void GetCommanders(System.Action<ApiResponse<System.Collections.Generic.List<CommanderResponse>>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() =>  m_apiClient.GetAllCommandersAsync(), onComplete));
    }

    public void SelectCommander(long commanderId, System.Action<ApiResponse<AuthResponse>> onComplete = null)
    {
        if (m_bConnected == false) return;
        // SelectCommander는 단순히 선택만 하는 게 아니라, commanderId가 포함된 새로운 토큰을 받기 위한 API
        StartCoroutine(RunAsync(() => m_apiClient.SelectCommanderAsync(commanderId), onComplete));
    }

    public void ValidateCommanderName(string name, Action<ApiResponse<bool>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ValidateCommanderNameAsync(name), onComplete));
    }

    public void RenameCommander(CommanderRenameRequest request, Action<ApiResponse<CommanderRenameResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.RenameCommanderAsync(request), onComplete));
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

    public void ChangeTacticOptions(ChangeTacticOptionsRequest request, System.Action<ApiResponse<ChangeTacticOptionsResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ChangeTacticOptionsAsync(request), onComplete));
    }

    public void UnlockModule(ModuleUnlockRequest request, Action<ApiResponse<ModuleUnlockResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.UnlockModuleAsync(request), onComplete));
    }

    public void LevelUpModule(ModuleLevelChangeRequest request, Action<ApiResponse<ModuleLevelChangeResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.LevelUpModuleAsync(request), onComplete));
    }

    public void LevelDownModule(ModuleLevelChangeRequest request, Action<ApiResponse<ModuleLevelChangeResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.LevelDownModuleAsync(request), onComplete));
    }

    public void ModuleGradeUp(ModuleGradeChangeRequest request, Action<ApiResponse<ModuleGradeChangeResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ModuleGradeUpAsync(request), onComplete));
    }

    public void ModuleGradeDown(ModuleGradeChangeRequest request, Action<ApiResponse<ModuleGradeChangeResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ModuleGradeDownAsync(request), onComplete));
    }

    public void ModuleUnlockMineral(ModuleUnlockRequest request, Action<ApiResponse<ModuleUnlockResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ModuleUnlockMineralAsync(request), onComplete));
    }

    public void ModuleLevelUpMineral(ModuleLevelChangeRequest request, Action<ApiResponse<ModuleLevelChangeResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ModuleLevelUpMineralAsync(request), onComplete));
    }

    public void ModuleLevelDownMineral(ModuleLevelChangeRequest request, Action<ApiResponse<ModuleLevelChangeResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ModuleLevelDownMineralAsync(request), onComplete));
    }

    public void ModuleGradeUpMineral(ModuleGradeChangeRequest request, Action<ApiResponse<ModuleGradeChangeResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ModuleGradeUpMineralAsync(request), onComplete));
    }

    public void ModuleGradeDownMineral(ModuleGradeChangeRequest request, Action<ApiResponse<ModuleGradeChangeResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ModuleGradeDownMineralAsync(request), onComplete));
    }

    public void ModuleResetMineral(ModuleResetRequest request, Action<ApiResponse<ModuleResetResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ModuleResetMineralAsync(request), onComplete));
    }



    // public void AddModuleBody(ModuleBodyAddRequest request, System.Action<ApiResponse<ShipInfo>> onComplete)
    // {
    //     if (m_bConnected == false) return;
    //     StartCoroutine(RunAsync(() => m_apiClient.AddModuleBodyAsync(request), onComplete));
    // }

    public void ResetModule(ModuleResetRequest request, Action<ApiResponse<ModuleResetResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ResetModuleAsync(request), onComplete));
    }

    public void ResetAndRemoveShip(ShipResetRemoveRequest request, System.Action<ApiResponse<ShipResetRemoveResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ResetAndRemoveShipAsync(request), onComplete));
    }

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

    public void FleetHealthSave(FleetHealthSaveRequest request)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.FleetHealthSaveAsync(request), null));
    }

    public void FleetInstantRepair(System.Action<ApiResponse<FleetInstantRepairResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.FleetInstantRepairAsync(), onComplete));
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

        // 게스트 ID 삭제 - 재로그인 시 새 계정으로 시작되도록
        PlayerPrefs.DeleteKey("GuestId");
        PlayerPrefs.DeleteKey("DevMineralClickCount");
        PlayerPrefs.Save();
    }

    public void DeleteAccount(System.Action<ApiResponse<string>> onComplete = null)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.DeleteAccountAsync(), onComplete));
    }

    public void ClearZoneStage(ClearZoneStageRequest request, System.Action<ApiResponse<ClearZoneStageResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ClearZoneStageAsync(request), onComplete));
    }

    public void ClaimZoneReward(ClaimZoneRewardRequest request, System.Action<ApiResponse<ClaimZoneRewardResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ClaimZoneRewardAsync(request), onComplete));
    }

    public void ClaimPendingStageRewards(System.Action<ApiResponse<PendingStageRewardResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ClaimPendingStageRewardsAsync(), onComplete));
    }

    public void GetStageEnemies(GetStageEnemiesRequest request, System.Action<ApiResponse<GetStageEnemiesResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.GetStageEnemiesAsync(request), onComplete));
    }

    public void PurchaseVip(VipPurchaseRequest request, System.Action<ApiResponse<VipStatusResponse>> onComplete)
    {
        StartCoroutine(RunAsync(() => m_apiClient.PurchaseVipAsync(request), onComplete));
    }

    public void GetVipStatus(System.Action<ApiResponse<VipStatusResponse>> onComplete)
    {
        StartCoroutine(RunAsync(() => m_apiClient.GetVipStatusAsync(), onComplete));
    }

    public void ClaimVipDailyReward(System.Action<ApiResponse<DailyClaimResponse>> onComplete)
    {
        StartCoroutine(RunAsync(() => m_apiClient.ClaimVipDailyRewardAsync(), onComplete));
    }

#if UNITY_EDITOR
    public void DebugForceVip(System.Action<ApiResponse<VipStatusResponse>> onComplete)
    {
        StartCoroutine(RunAsync(() => m_apiClient.DebugForceVipAsync(), onComplete));
    }
#endif

    public void Heartbeat()
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.HeartbeatAsync(), OnHeartbeatResponse));
    }

    // 앱 복귀 즉시 발송 — OnApplicationPause(false)/OnApplicationFocus(true) 중복 방지 쿨다운 포함
    private void HeartbeatOnResume()
    {
        if (m_heartbeatStarted == false) return;
        if (Time.realtimeSinceStartup - m_lastResumeHeartbeatTime < ResumeHeartbeatCool) return;
        m_lastResumeHeartbeatTime = Time.realtimeSinceStartup;
        Heartbeat();
    }

    private void OnHeartbeatResponse(ApiResponse<HeartbeatResponse> response)
    {
        if (response.errorCode == 0)
        {
            m_heartbeatFailCount = 0;
            return;
        }

        m_heartbeatFailCount++;
        Debug.LogWarning($"Heartbeat failed ({m_heartbeatFailCount}/{HeartbeatMaxFail}) errorCode={response.errorCode}");

        if (m_heartbeatFailCount >= HeartbeatMaxFail)
        {
            Debug.LogError("Heartbeat max failures reached. Returning to MainScene.");
            CancelInvoke(nameof(Heartbeat));
            m_heartbeatStarted = false;
            m_heartbeatFailCount = 0;
            LoadingManager.LoadSceneWithLoading("MainScene");
        }
    }

    // 백그라운드 전환 시 하트비트 중단, 복귀 시 재개 (StartHeartbeat 이후에만 동작)
    private void OnApplicationPause(bool pauseStatus)
    {
        //Debug.Log($"NetworkManager/OnApplicationPause ({pauseStatus})");
        if (pauseStatus)
        {
            if (m_heartbeatStarted) Heartbeat(); // 백그라운드 직전 하트비트 전송
            CancelInvoke(nameof(Heartbeat));
        }
        else
        {
            CancelInvoke(nameof(Heartbeat));
            HeartbeatOnResume(); // 복귀 즉시 발송
            InvokeRepeating(nameof(Heartbeat), HeartbeatInterval, HeartbeatInterval);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // OnApplicationPause가 누락되는 경우 보완
        if (hasFocus) HeartbeatOnResume();
    }

    // PvP API
    public void PvpList(PvpListRequest request, System.Action<ApiResponse<PvpListResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.PvpListAsync(request), onComplete));
    }

    public void PvpRefresh(PvpRefreshRequest request, System.Action<ApiResponse<PvpRefreshResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.PvpRefreshAsync(request), onComplete));
    }

    public void PvpBattleStart(PvpBattleStartRequest request, System.Action<ApiResponse<PvpBattleStartResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.PvpBattleStartAsync(request), onComplete));
    }

    public void PvpBattleResult(PvpBattleResultRequest request, System.Action<ApiResponse<PvpBattleResultResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.PvpBattleResultAsync(request), onComplete));
    }

    public void PvpRanking(PvpRankingRequest request, System.Action<ApiResponse<PvpRankingResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.PvpRankingAsync(request), onComplete));
    }

    public void PvpMyRank(PvpMyRankRequest request, System.Action<ApiResponse<PvpMyRankResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.PvpMyRankAsync(request), onComplete));
    }

    public void ZoneRanking(ZoneRankingRequest request, System.Action<ApiResponse<ZoneRankingResponse>> onComplete)
    {
        if (m_bConnected == false) return;
        StartCoroutine(RunAsync(() => m_apiClient.ZoneRankingAsync(request), onComplete));
    }

    public ApiClient GetApiClient()
    {
        return m_apiClient;
    }

    private int GetAppVersionCode()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        var context     = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        var pkgManager  = context.Call<AndroidJavaObject>("getPackageManager");
        string pkgName  = context.Call<string>("getPackageName");
        var pkgInfo     = pkgManager.Call<AndroidJavaObject>("getPackageInfo", pkgName, 0);
        int versionCode = pkgInfo.Get<int>("versionCode");
        return versionCode;
#else
        return 0;
#endif
    }
}
