//------------------------------------------------------------------------------
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class CustomException : Exception
{
    public ServerErrorCode ErrorCode { get; }

    public CustomException(ServerErrorCode errorCode)
        : base(ErrorCodeMapping.GetMessage((int)errorCode))
    {
        ErrorCode = errorCode;
    }
}

public static class ApiServerUrl
{
    // Dev server
    public const string Dev     = "http://localhost:8080/api";
    //public const string Dev     = "http://192.168.50.51:8080/api";

    // test server
    public const string Test    = "http://192.168.50.61:8080/api";

    // release server
    //public const string Release = "https://168.110.100.27/api";
    public const string Release = "https://www.fidforge.com/api";
}

public class ApiClient
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private string m_baseUrl = ApiServerUrl.Dev;
#else
    private string m_baseUrl = ApiServerUrl.Release;
#endif

    public void SetBaseUrl(string url)
    {
        m_baseUrl = url;
    }

    public string GetBaseUrl()
    {
        return m_baseUrl;
    }

    private string accessToken;
    private string refreshToken;

    #region Core Methods ------------------------------------------------------------------------------------------
    public void SetAccessToken(string token)
    {
        accessToken = token;
    }

    public void SetTokens(string access, string refresh)
    {
        accessToken = access;
        refreshToken = refresh;
        PlayerPrefs.SetString("RefreshToken", EncryptToken(refreshToken));
        PlayerPrefs.Save();
    }

    public string GetRefreshToken()
    {
        return refreshToken;
    }

    public void LoadRefreshToken()
    {
        string storedValue = PlayerPrefs.GetString("RefreshToken", "");
        refreshToken = DecryptToken(storedValue);

        // 복호화 실패(기기 변경, 구버전 평문 저장 등) — 조용히 폐기하고 재로그인 유도
        bool bDecryptFailed = string.IsNullOrEmpty(storedValue) == false && string.IsNullOrEmpty(refreshToken) == true;
        if (bDecryptFailed == true)
        {
            PlayerPrefs.DeleteKey("RefreshToken");
            PlayerPrefs.Save();
        }
    }

    public void ClearTokens()
    {
        accessToken = "";
        refreshToken = "";
        PlayerPrefs.DeleteKey("RefreshToken");
        PlayerPrefs.Save();
    }

    // 기기 종속 키(deviceUniqueIdentifier + 솔트)로 파생한 AES 키. 순수 파일 덤프/백업 유출 방지용
    // (앱 바이너리 정적 분석에는 대응하지 못함 — 이번 범위의 한계)
    private static readonly byte[] s_tokenSalt = Encoding.UTF8.GetBytes("thefirst_client_refresh_token_v1");

    private static byte[] DeriveTokenKey()
    {
        string keySource = SystemInfo.deviceUniqueIdentifier;
        using var deriveBytes = new Rfc2898DeriveBytes(keySource, s_tokenSalt, 10000, HashAlgorithmName.SHA256);
        return deriveBytes.GetBytes(32);
    }

    private static string EncryptToken(string plainText)
    {
        if (string.IsNullOrEmpty(plainText) == true)
            return "";

        byte[] key = DeriveTokenKey();
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        byte[] combinedBytes = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, combinedBytes, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, combinedBytes, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(combinedBytes);
    }

    // 복호화 실패 시 예외를 삼키고 빈 문자열 반환 — 호출부에서 "토큰 없음"과 동일하게 처리되어 크래시 없이 로그인 화면으로 유도됨
    private static string DecryptToken(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText) == true)
            return "";

        try
        {
            byte[] combinedBytes = Convert.FromBase64String(cipherText);

            byte[] key = DeriveTokenKey();
            using var aes = Aes.Create();
            aes.Key = key;

            int ivLength = aes.BlockSize / 8;
            byte[] ivBytes = new byte[ivLength];
            Buffer.BlockCopy(combinedBytes, 0, ivBytes, 0, ivLength);
            aes.IV = ivBytes;

            using var decryptor = aes.CreateDecryptor();
            byte[] plainBytes = decryptor.TransformFinalBlock(combinedBytes, ivLength, combinedBytes.Length - ivLength);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ApiClient] RefreshToken 복호화 실패, 토큰 폐기: {e.Message}");
            return "";
        }
    }

    // 서버가 살아있는지 체크 (health check)
    public async Task<bool> CheckServerAliveAsync()
    {
        try
        {
            using var request = UnityWebRequest.Get(m_baseUrl);
            request.timeout = 3;
            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            // 연결 자체가 실패한 경우만 false (ConnectionError)
            // 4xx, 5xx 응답은 서버가 살아있다는 의미
            bool isServerAlive = request.result != UnityWebRequest.Result.ConnectionError;
            // request.result 403은 서버가 "너 인증 없어" 라고 거절한 것이므로 서버가 정상 동작 중
            //Debug.Log($"[ServerCheck] URL: {m_baseUrl}, Result: {request.result}, Code: {request.responseCode}, Alive: {isServerAlive}");
            return isServerAlive;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ServerCheck] Exception: {e.Message}");
            return false;
        }
    }

    private async Task SendRequestAsync(UnityWebRequest request)
    {
        //Debug.Log($"[API Request] URL: {request.url}, Method: {request.method}");

        var operation = request.SendWebRequest();
        while (!operation.isDone)
            await Task.Yield();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[API Request] URL: {request.url}, Method: {request.method}");
            string errorText = request.downloadHandler?.text ?? request.error;
            Debug.LogError($"[API Error] Result: {request.result}, Error: {request.error}, ResponseCode: {request.responseCode}, Response: {errorText}");
            ServerErrorCode errorCode = GetHttpErrorCode(request.responseCode);
            throw new CustomException(errorCode);
        }

        //Debug.Log($"[API Success] ResponseCode: {request.responseCode}");
    }
    private ServerErrorCode GetHttpErrorCode(long responseCode)
    {
        switch (responseCode)
        {
            case 400: return ServerErrorCode.HTTP_BAD_REQUEST_400;
            case 401: return ServerErrorCode.HTTP_UNAUTHORIZED_401;
            case 403: return ServerErrorCode.HTTP_FORBIDDEN_403;
            case 404: return ServerErrorCode.HTTP_NOT_FOUND_404;
            case 500: return ServerErrorCode.HTTP_SERVER_ERROR_500;
            default: return ServerErrorCode.UNKNOWN_ERROR;
        }
    }

    // requireAuth인데 accessToken이 비어있으면 요청 없이 즉시 에러 반환
    private async Task<ApiResponse<TRes>> PostAsync<TRes>(string path, object requestBody, bool requireAuth = true)
    {
        if (requireAuth == true && string.IsNullOrEmpty(accessToken) == true)
            return ApiResponse<TRes>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        byte[] bodyBytes = requestBody == null ? new byte[0] : Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(requestBody));

        using var webRequest = new UnityWebRequest($"{m_baseUrl}{path}", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(bodyBytes);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        if (requireAuth == true)
            webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<TRes>>(webRequest.downloadHandler.text);
    }

    private async Task<ApiResponse<TRes>> GetAsync<TRes>(string path, bool requireAuth = true)
    {
        if (requireAuth == true && string.IsNullOrEmpty(accessToken) == true)
            return ApiResponse<TRes>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}{path}", "GET");
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        if (requireAuth == true)
            webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<TRes>>(webRequest.downloadHandler.text);
    }

    private async Task<ApiResponse<TRes>> DeleteAsync<TRes>(string path)
    {
        if (string.IsNullOrEmpty(accessToken) == true)
            return ApiResponse<TRes>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}{path}", "DELETE");
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<TRes>>(webRequest.downloadHandler.text);
    }
    #endregion

    #region Server Status API Methods -----------------------------------------------------------------------------
    // 버전 체크 + 점검 상태를 한 번에 조회 (왕복 최소화)
    public async Task<ApiResponse<ServerStatusResponse>> CheckServerStatusAsync(int versionCode)
    {
        var requestDto = new ServerStatusRequest { versionCode = versionCode };
        return await PostAsync<ServerStatusResponse>("/status", requestDto, requireAuth: false);
    }
    #endregion

    #region Authentication API Methods ----------------------------------------------------------------------------
    public async Task<ApiResponse<string>> SignUpAsync(string email, string password)
    {
        var requestDto = new SignUpRequest { email = email, password = password };
        return await PostAsync<string>("/account/signup", requestDto, requireAuth: false);
    }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(string email, string password)
    {
        var requestDto = new LoginRequest { email = email, password = password };
        var response = await PostAsync<AuthResponse>("/account/login", requestDto, requireAuth: false);

        if (response.errorCode == 0)
            SetTokens(response.data.accessToken, response.data.refreshToken);

        return response;
    }

    public async Task<ApiResponse<AuthResponse>> RefreshAccessTokenAsync()
    {
        if (string.IsNullOrEmpty(refreshToken) == true) return ApiResponse<AuthResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        var requestDto = new RefreshTokenRequest { refreshToken = refreshToken };
        var response = await PostAsync<AuthResponse>("/account/refresh", requestDto, requireAuth: false);

        if (response.errorCode == 0)
            SetTokens(response.data.accessToken, response.data.refreshToken);
        else
            ClearTokens();

        return response;
    }

    public async Task<ApiResponse<AuthResponse>> GoogleLoginAsync(string idToken)
    {
        var requestDto = new GoogleLoginRequest { idToken = idToken };
        var response = await PostAsync<AuthResponse>("/account/google-login", requestDto, requireAuth: false);

        if (response.errorCode == 0)
            SetTokens(response.data.accessToken, response.data.refreshToken);

        return response;
    }

    public async Task<ApiResponse<AuthResponse>> GuestLoginAsync(string guestId)
    {
        var requestDto = new GuestLoginRequest { guestId = guestId };
        var response = await PostAsync<AuthResponse>("/account/guest-login", requestDto, requireAuth: false);

        if (response.errorCode == 0)
            SetTokens(response.data.accessToken, response.data.refreshToken);

        return response;
    }

    // 서버 측 활성 리프레시 토큰 세션을 즉시 폐기 (로컬 토큰 삭제 전에 호출할 것)
    public async Task<ApiResponse<string>> LogoutAsync()
    {
        return await PostAsync<string>("/account/logout", null);
    }

    public async Task<ApiResponse<string>> DeleteAccountAsync()
    {
        if (string.IsNullOrEmpty(refreshToken) == true) return ApiResponse<string>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        var response = await DeleteAsync<string>("/account/delete");

        if (response.errorCode == 0)
            ClearTokens();

        return response;
    }

    // 현재 로그인된 계정에 구글 계정을 연동
    public async Task<ApiResponse<AuthResponse>> LinkGoogleAsync(string idToken)
    {
        var requestDto = new LinkGoogleRequest { idToken = idToken };
        return await PostAsync<AuthResponse>("/account/link-google", requestDto);
    }

    // 현재 계정의 구글 연동 해제 (게스트 상태로 복귀)
    public async Task<ApiResponse<UnlinkGoogleResponse>> UnlinkGoogleAsync()
    {
        return await PostAsync<UnlinkGoogleResponse>("/account/unlink-google", null);
    }

    public async Task<ApiResponse<CommanderResponse>> CreateCommanderAsync(string commanderName)
    {
        var requestDto = new CommanderCreateRequest { commanderName = commanderName };
        return await PostAsync<CommanderResponse>("/commander/create", requestDto);
    }

    public async Task<ApiResponse<bool>> ValidateCommanderNameAsync(string name)
    {
        var requestDto = new CommanderValidateNameRequest { name = name };
        return await PostAsync<bool>("/commander/validate-name", requestDto);
    }

    public async Task<ApiResponse<CommanderRenameResponse>> RenameCommanderAsync(CommanderRenameRequest renameRequest)
    {
        return await PostAsync<CommanderRenameResponse>("/commander/rename", renameRequest);
    }

    public async Task<ApiResponse<RedeemCodeResponse>> RedeemCodeAsync(RedeemCodeRequest redeemCodeRequest)
    {
        return await PostAsync<RedeemCodeResponse>("/redeem-code", redeemCodeRequest);
    }

    public async Task<ApiResponse<List<CommanderResponse>>> GetAllCommandersAsync()
    {
        return await GetAsync<List<CommanderResponse>>("/commander/commanders");
    }

    public async Task<ApiResponse<AuthResponse>> SelectCommanderAsync(long commanderId)
    {
        var response = await PostAsync<AuthResponse>($"/commander/select-commander/{commanderId}", null);

        if (response.errorCode == 0)
            SetTokens(response.data.accessToken, response.data.refreshToken);

        return response;
    }
    #endregion

    #region Development API Methods -------------------------------------------------------------------------------
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public async Task<ApiResponse<string>> ExecuteDevCommandAsync(string command, string[] parameters)
    {
        var requestDto = new DevCommandRequest { command = command, @params = parameters };
        return await PostAsync<string>("/dev/command", requestDto);
    }
#endif
    #endregion

    #region Fleet Upgrade API Methods -----------------------------------------------------------------------------
    public async Task<ApiResponse<AddShipResponse>> AddShipAsync(AddShipRequest request)
    {
        return await PostAsync<AddShipResponse>("/fleet/add-ship", request);
    }

    public async Task<ApiResponse<ChangeFormationResponse>> ChangeFormationAsync(ChangeFormationRequest request)
    {
        return await PostAsync<ChangeFormationResponse>("/fleet/change-formation", request);
    }

    public async Task<ApiResponse<ChangeTacticOptionsResponse>> ChangeTacticOptionsAsync(ChangeTacticOptionsRequest request)
    {
        return await PostAsync<ChangeTacticOptionsResponse>("/fleet/change-tactic-options", request);
    }

    public async Task<ApiResponse<ShipResetRemoveResponse>> ResetAndRemoveShipAsync(ShipResetRemoveRequest request)
    {
        return await PostAsync<ShipResetRemoveResponse>("/fleet/reset-ship", request);
    }

    public async Task<ApiResponse<string>> FleetHealthSaveAsync(FleetHealthSaveRequest request)
    {
        return await PostAsync<string>("/fleet/save-health", request);
    }

    public async Task<ApiResponse<string>> PlaceFleetPresetShipAsync(FleetPresetPlaceShipRequest request)
    {
        return await PostAsync<string>("/fleet/preset/place-ship", request);
    }

    public async Task<ApiResponse<string>> SetFleetPresetShipFrontAsync(FleetPresetSetFrontRequest request)
    {
        return await PostAsync<string>("/fleet/preset/set-front", request);
    }

    public async Task<ApiResponse<SetFleetPresetSlotModulesResponse>> SetFleetPresetSlotModulesAsync(SetFleetPresetSlotModulesRequest request)
    {
        return await PostAsync<SetFleetPresetSlotModulesResponse>("/fleet/preset/set-modules", request);
    }

    public async Task<ApiResponse<FleetInstantRepairResponse>> FleetInstantRepairAsync()
    {
        return await PostAsync<FleetInstantRepairResponse>("/fleet/instant-repair", null);
    }

    #endregion

    #region Zone Battle API Methods -------------------------------------------------------------------------------
    public async Task<ApiResponse<PvpClaimSeasonRewardResponse>> PvpClaimSeasonRewardAsync()
    {
        return await PostAsync<PvpClaimSeasonRewardResponse>("/pvp/pvp-season/claim-reward", new PvpClaimSeasonRewardRequest());
    }

    #endregion

    #region Exploration Grid API Methods --------------------------------------------------------------------------
    public async Task<ApiResponse<EnterExplorationCellResponse>> EnterExplorationCellAsync(EnterExplorationCellRequest request)
    {
        return await PostAsync<EnterExplorationCellResponse>("/exploration/enter-cell", request);
    }

    public async Task<ApiResponse<ClearExplorationCellResponse>> ClearExplorationCellAsync(ClearExplorationCellRequest request)
    {
        return await PostAsync<ClearExplorationCellResponse>("/exploration/clear-cell", request);
    }

    public async Task<ApiResponse<ConfirmRewardCardResponse>> ConfirmRewardCardAsync(ConfirmRewardCardRequest request)
    {
        return await PostAsync<ConfirmRewardCardResponse>("/exploration/confirm-reward-card", request);
    }

    public async Task<ApiResponse<GetActiveZoneRunProgressResponse>> GetActiveZoneRunProgressAsync(GetActiveZoneRunProgressRequest request)
    {
        return await PostAsync<GetActiveZoneRunProgressResponse>("/exploration/active-run-progress", request);
    }

    public async Task<ApiResponse<EscapeExplorationZoneResponse>> EscapeExplorationZoneAsync(EscapeExplorationZoneRequest request)
    {
        return await PostAsync<EscapeExplorationZoneResponse>("/exploration/escape-zone", request);
    }

    public async Task<ApiResponse<AbandonZoneRunResponse>> AbandonZoneRunAsync(AbandonZoneRunRequest request)
    {
        return await PostAsync<AbandonZoneRunResponse>("/exploration/abandon-run", request);
    }

    public async Task<ApiResponse<IncreaseCommandPowerMaxResponse>> IncreaseCommandPowerMaxAsync(IncreaseCommandPowerMaxRequest request)
    {
        return await PostAsync<IncreaseCommandPowerMaxResponse>("/exploration/increase-command-power", request);
    }

    public async Task<ApiResponse<UnlockShipPresetResponse>> UnlockShipPresetAsync(UnlockShipPresetRequest request)
    {
        return await PostAsync<UnlockShipPresetResponse>("/exploration/unlock-ship-preset", request);
    }

    #endregion

    #region Heartbeat API Methods ---------------------------------------------------------------------------------
    public async Task<ApiResponse<HeartbeatResponse>> HeartbeatAsync()
    {
        return await PostAsync<HeartbeatResponse>("/zone/heartbeat", new HeartbeatRequest());
    }
    #endregion

    #region PvP API Methods --------------------------------------------------------------------------------------
    public async Task<ApiResponse<PvpListResponse>> PvpListAsync(PvpListRequest request)
    {
        return await PostAsync<PvpListResponse>("/pvp/list", request);
    }

    public async Task<ApiResponse<PvpRefreshResponse>> PvpRefreshAsync(PvpRefreshRequest request)
    {
        return await PostAsync<PvpRefreshResponse>("/pvp/refresh", request);
    }

    public async Task<ApiResponse<PvpBattleStartResponse>> PvpBattleStartAsync(PvpBattleStartRequest request)
    {
        return await PostAsync<PvpBattleStartResponse>("/pvp/battle/start", request);
    }

    public async Task<ApiResponse<PvpBattleResultResponse>> PvpBattleResultAsync(PvpBattleResultRequest request)
    {
        return await PostAsync<PvpBattleResultResponse>("/pvp/battle/result", request);
    }

    public async Task<ApiResponse<PvpRankingResponse>> PvpRankingAsync(PvpRankingRequest request)
    {
        return await PostAsync<PvpRankingResponse>("/ranking/pvp", request);
    }

    public async Task<ApiResponse<ZoneRankingResponse>> ZoneRankingAsync(ZoneRankingRequest request)
    {
        return await PostAsync<ZoneRankingResponse>("/ranking/zone", request);
    }

    public async Task<ApiResponse<PvpMyRankResponse>> PvpMyRankAsync(PvpMyRankRequest request)
    {
        return await PostAsync<PvpMyRankResponse>("/ranking/pvp/my-rank", request);
    }
    #endregion

    #region VIP API Methods ---------------------------------------------------------------------------------------
    public async Task<ApiResponse<VipStatusResponse>> PurchaseVipAsync(VipPurchaseRequest request)
    {
        return await PostAsync<VipStatusResponse>("/iap/vip/purchase", request);
    }

    public async Task<ApiResponse<VipStatusResponse>> GetVipStatusAsync()
    {
        return await GetAsync<VipStatusResponse>("/iap/vip/status");
    }

    public async Task<ApiResponse<DailyClaimResponse>> ClaimVipDailyRewardAsync()
    {
        return await PostAsync<DailyClaimResponse>("/iap/vip/daily-reward", null);
    }

#if UNITY_EDITOR
    public async Task<ApiResponse<VipStatusResponse>> DebugForceVipAsync()
    {
        return await PostAsync<VipStatusResponse>("/iap/debug/vip/force", null);
    }
#endif
    #endregion

    #region Progress API Methods ----------------------------------------------------------------------------------
    public async Task<ApiResponse<ProgressInfo>> SaveProgressAsync(ProgressSaveRequest request)
    {
        return await PostAsync<ProgressInfo>("/progress/save", request);
    }

    public async Task<ApiResponse<ProgressListResponse>> GetProgressListAsync(string category)
    {
        return await GetAsync<ProgressListResponse>($"/progress/{category}");
    }
    #endregion
}
