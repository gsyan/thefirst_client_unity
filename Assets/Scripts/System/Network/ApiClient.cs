//------------------------------------------------------------------------------
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst.Intrinsics;
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
    //public const string Dev     = "http://localhost:8080/api";
    public const string Dev     = "http://192.168.0.51:8080/api";

    // test server
    public const string Test    = "http://192.168.0.61:8080/api";
    
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
        PlayerPrefs.SetString("RefreshToken", refreshToken);
        PlayerPrefs.Save();
    }

    public string GetRefreshToken()
    {
        return refreshToken;
    }

    public void LoadRefreshToken()
    {
        refreshToken = PlayerPrefs.GetString("RefreshToken", "");
    }

    public void ClearTokens()
    {
        accessToken = "";
        refreshToken = "";
        PlayerPrefs.DeleteKey("RefreshToken");
        PlayerPrefs.Save();
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
    private ServerErrorCode GetHttpErrorCode(long responseCode) => responseCode switch
    {
        400 => ServerErrorCode.HTTP_BAD_REQUEST_400,
        401 => ServerErrorCode.HTTP_UNAUTHORIZED_401,
        403 => ServerErrorCode.HTTP_FORBIDDEN_403,
        404 => ServerErrorCode.HTTP_NOT_FOUND_404,
        500 => ServerErrorCode.HTTP_SERVER_ERROR_500,
        _ => ServerErrorCode.UNKNOWN_ERROR
    };
    #endregion

    #region Server Status API Methods -----------------------------------------------------------------------------
    // 버전 체크 + 점검 상태를 한 번에 조회 (왕복 최소화)
    public async Task<ApiResponse<ServerStatusResponse>> CheckServerStatusAsync(int versionCode)
    {
        var requestDto = new ServerStatusRequest { versionCode = versionCode };
        string json = JsonConvert.SerializeObject(requestDto);

        using var request = new UnityWebRequest($"{m_baseUrl}/status", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<ServerStatusResponse>>(request.downloadHandler.text);
    }
    #endregion

    #region Authentication API Methods ----------------------------------------------------------------------------
    public async Task<ApiResponse<string>> SignUpAsync(string email, string password)
    {
        var requestDto = new SignUpRequest { email = email, password = password };
        string json = JsonConvert.SerializeObject(requestDto);
        Debug.Log($"SignUp JSON: {json}");

        using var request = new UnityWebRequest($"{m_baseUrl}/account/signup", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<string>>(request.downloadHandler.text);
    }

    public async Task<ApiResponse<AuthResponse>> LoginAsync(string email, string password)
    {
        var requestDto = new LoginRequest { email = email, password = password };
        string json = JsonConvert.SerializeObject(requestDto);
        Debug.Log($"Login JSON: {json}");

        using var request = new UnityWebRequest($"{m_baseUrl}/account/login", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        await SendRequestAsync(request);
        var response = JsonConvert.DeserializeObject<ApiResponse<AuthResponse>>(request.downloadHandler.text);

        if (response.errorCode == 0)
            SetTokens(response.data.accessToken, response.data.refreshToken);

        return response;
    }

    public async Task<ApiResponse<AuthResponse>> RefreshAccessTokenAsync()
    {
        if (string.IsNullOrEmpty(refreshToken) == true) return ApiResponse<AuthResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        var requestDto = new RefreshTokenRequest { refreshToken = refreshToken };
        string json = JsonConvert.SerializeObject(requestDto);

        using var request = new UnityWebRequest($"{m_baseUrl}/account/refresh", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        await SendRequestAsync(request);
        CommonUtility.DebugLog($"[RefreshToken] Raw response: {request.downloadHandler.text}");
        var response = JsonConvert.DeserializeObject<ApiResponse<AuthResponse>>(request.downloadHandler.text);

        if (response.errorCode == 0)
            SetTokens(response.data.accessToken, response.data.refreshToken);
        else
            ClearTokens();

        return response;
    }

    public async Task<ApiResponse<AuthResponse>> GoogleLoginAsync(string idToken)
    {
        var requestDto = new GoogleLoginRequest { idToken = idToken };
        string json = JsonConvert.SerializeObject(requestDto);
        Debug.Log($"GoogleLogin JSON: {json}");

        using var request = new UnityWebRequest($"{m_baseUrl}/account/google-login", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        await SendRequestAsync(request);
        var response = JsonConvert.DeserializeObject<ApiResponse<AuthResponse>>(request.downloadHandler.text);

        if (response.errorCode == 0)
            SetTokens(response.data.accessToken, response.data.refreshToken);

        return response;
    }

    public async Task<ApiResponse<AuthResponse>> GuestLoginAsync(string guestId)
    {
        var requestDto = new GuestLoginRequest { guestId = guestId };
        string json = JsonConvert.SerializeObject(requestDto);
        CommonUtility.DebugLog($"[GuestLogin] {json}");

        using var request = new UnityWebRequest($"{m_baseUrl}/account/guest-login", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        await SendRequestAsync(request);
        var response = JsonConvert.DeserializeObject<ApiResponse<AuthResponse>>(request.downloadHandler.text);

        if (response.errorCode == 0)
            SetTokens(response.data.accessToken, response.data.refreshToken);

        return response;
    }

    public async Task<ApiResponse<string>> DeleteAccountAsync()
    {
        if (string.IsNullOrEmpty(refreshToken) == true) return ApiResponse<string>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        using var request = new UnityWebRequest($"{m_baseUrl}/account/delete", "DELETE");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        var response = JsonConvert.DeserializeObject<ApiResponse<string>>(request.downloadHandler.text);

        if (response.errorCode == 0)
            ClearTokens();

        return response;
    }

    // 현재 로그인된 계정에 구글 계정을 연동
    public async Task<ApiResponse<AuthResponse>> LinkGoogleAsync(string idToken)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<AuthResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        var requestDto = new LinkGoogleRequest { idToken = idToken };
        string json = JsonConvert.SerializeObject(requestDto);

        using var request = new UnityWebRequest($"{m_baseUrl}/account/link-google", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<AuthResponse>>(request.downloadHandler.text);
    }

    // 현재 계정의 구글 연동 해제 (게스트 상태로 복귀)
    public async Task<ApiResponse<UnlinkGoogleResponse>> UnlinkGoogleAsync()
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<UnlinkGoogleResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        using var request = new UnityWebRequest($"{m_baseUrl}/account/unlink-google", "POST");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<UnlinkGoogleResponse>>(request.downloadHandler.text);
    }

    public async Task<ApiResponse<CommanderResponse>> CreateCommanderAsync(string commanderName)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<CommanderResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        var requestDto = new CommanderCreateRequest { commanderName = commanderName };
        string json = JsonConvert.SerializeObject(requestDto);
        Debug.Log($"CreateCommander JSON: {json}");

        using var request = new UnityWebRequest($"{m_baseUrl}/commander/create", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<CommanderResponse>>(request.downloadHandler.text);
    }

    public async Task<ApiResponse<bool>> ValidateCommanderNameAsync(string name)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<bool>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(new CommanderValidateNameRequest { name = name });

        using var request = new UnityWebRequest($"{m_baseUrl}/commander/validate-name", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<bool>>(request.downloadHandler.text);
    }

    public async Task<ApiResponse<CommanderRenameResponse>> RenameCommanderAsync(CommanderRenameRequest renameRequest)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<CommanderRenameResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(renameRequest);
        Debug.Log($"RenameCommander JSON: {json}");

        using var request = new UnityWebRequest($"{m_baseUrl}/commander/rename", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        var response = JsonConvert.DeserializeObject<ApiResponse<CommanderRenameResponse>>(request.downloadHandler.text);
        Debug.Log($"RenameCommander Response: {request.downloadHandler.text}");
        return response;
    }

    public async Task<ApiResponse<RedeemCodeResponse>> RedeemCodeAsync(RedeemCodeRequest redeemCodeRequest)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<RedeemCodeResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(redeemCodeRequest);

        using var request = new UnityWebRequest($"{m_baseUrl}/redeem-code", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<RedeemCodeResponse>>(request.downloadHandler.text);
    }

    public async Task<ApiResponse<List<CommanderResponse>>> GetAllCommandersAsync()
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<List<CommanderResponse>>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        using var request = new UnityWebRequest($"{m_baseUrl}/commander/commanders", "GET");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<List<CommanderResponse>>>(request.downloadHandler.text);
    }

    public async Task<ApiResponse<AuthResponse>> SelectCommanderAsync(long commanderId)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<AuthResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        using var request = new UnityWebRequest($"{m_baseUrl}/commander/select-commander/{commanderId}", "POST");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        var response = JsonConvert.DeserializeObject<ApiResponse<AuthResponse>>(request.downloadHandler.text);

        if (response.errorCode == 0)
            SetTokens(response.data.accessToken, response.data.refreshToken);

        return response;
    }
    #endregion

    #region Development API Methods -------------------------------------------------------------------------------
    public async Task<ApiResponse<string>> ExecuteDevCommandAsync(string command, string[] parameters)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<string>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        var requestDto = new DevCommandRequest { command = command, @params = parameters };
        string json = JsonConvert.SerializeObject(requestDto);

        using var request = new UnityWebRequest($"{m_baseUrl}/dev/command", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<string>>(request.downloadHandler.text);
    }
    #endregion

    #region Fleet Upgrade API Methods -----------------------------------------------------------------------------
    public async Task<ApiResponse<AddShipResponse>> AddShipAsync(AddShipRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<AddShipResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"Add Ship Request: {json}");

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/fleet/add-ship", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<AddShipResponse>>(webRequest.downloadHandler.text);
    }

    public async Task<ApiResponse<ChangeFormationResponse>> ChangeFormationAsync(ChangeFormationRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ChangeFormationResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"Change Formation Request: {json}");

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/fleet/change-formation", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<ChangeFormationResponse>>(webRequest.downloadHandler.text);
    }

    public async Task<ApiResponse<ChangeTacticOptionsResponse>> ChangeTacticOptionsAsync(ChangeTacticOptionsRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ChangeTacticOptionsResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/fleet/change-tactic-options", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<ChangeTacticOptionsResponse>>(webRequest.downloadHandler.text);
    }

    // public async Task<ApiResponse<ShipInfo>> AddModuleBodyAsync(ModuleBodyAddRequest request)
    // {
    //     if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ShipInfo>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

    //     string json = JsonConvert.SerializeObject(request);
    //     Debug.Log($"Add ModuleBody Request: {json}");

    //     using var webRequest = new UnityWebRequest($"{m_baseUrl}/fleet/add-modulebody", "POST");
    //     webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
    //     webRequest.downloadHandler = new DownloadHandlerBuffer();
    //     webRequest.SetRequestHeader("Content-Type", "application/json");
    //     webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

    //     await SendRequestAsync(webRequest);

    //     var response = JsonConvert.DeserializeObject<ApiResponse<ShipInfo>>(webRequest.downloadHandler.text);
    //     return response;
    // }

    public async Task<ApiResponse<ShipResetRemoveResponse>> ResetAndRemoveShipAsync(ShipResetRemoveRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ShipResetRemoveResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"Ship ResetRemove Request: {json}");

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/fleet/reset-ship", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<ShipResetRemoveResponse>>(webRequest.downloadHandler.text);
        Debug.Log($"Ship ResetRemove Response: {webRequest.downloadHandler.text}");
        return response;
    }

    // public async Task<ApiResponse<FleetStatsResponse>> GetFleetStatsAsync(FleetStatsRequest request)
    // {
    //     if (string.IsNullOrEmpty(accessToken)) return ApiResponse<FleetStatsResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

    //     string queryParam = $"?fleetId={request.fleetId}";

    //     using var webRequest = new UnityWebRequest($"{m_baseUrl}/fleet/stats{queryParam}", "GET");
    //     webRequest.downloadHandler = new DownloadHandlerBuffer();
    //     webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

    //     await SendRequestAsync(webRequest);

    //     var response = JsonConvert.DeserializeObject<ApiResponse<FleetStatsResponse>>(webRequest.downloadHandler.text);
    //     return response;
    // }
    public async Task<ApiResponse<object>> FleetHealthSaveAsync(FleetHealthSaveRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<object>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/fleet/save-health", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<object>>(webRequest.downloadHandler.text);
    }

    public async Task<ApiResponse<object>> PlaceFleetPresetShipAsync(FleetPresetPlaceShipRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<object>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/fleet/preset/place-ship", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<object>>(webRequest.downloadHandler.text);
    }

    public async Task<ApiResponse<object>> SetFleetPresetShipFrontAsync(FleetPresetSetFrontRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<object>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/fleet/preset/set-front", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<object>>(webRequest.downloadHandler.text);
    }

    public async Task<ApiResponse<FleetInstantRepairResponse>> FleetInstantRepairAsync()
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<FleetInstantRepairResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/fleet/instant-repair", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(new byte[0]);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<FleetInstantRepairResponse>>(webRequest.downloadHandler.text);
    }

    #endregion

    #region Zone Battle API Methods -------------------------------------------------------------------------------
    public async Task<ApiResponse<ClearZoneStageResponse>> ClearZoneStageAsync(ClearZoneStageRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ClearZoneStageResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/zone/clear-stage", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<ClearZoneStageResponse>>(webRequest.downloadHandler.text);
    }


    public async Task<ApiResponse<ClaimZoneRewardResponse>> ClaimZoneRewardAsync(ClaimZoneRewardRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ClaimZoneRewardResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/zone/claim-reward", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<ClaimZoneRewardResponse>>(webRequest.downloadHandler.text);
    }


    public async Task<ApiResponse<PvpClaimSeasonRewardResponse>> PvpClaimSeasonRewardAsync()
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<PvpClaimSeasonRewardResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(new PvpClaimSeasonRewardRequest());

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/pvp/pvp-season/claim-reward", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<PvpClaimSeasonRewardResponse>>(webRequest.downloadHandler.text);
    }

    public async Task<ApiResponse<PendingStageRewardResponse>> ClaimPendingStageRewardsAsync()
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<PendingStageRewardResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(new PendingStageRewardRequest());

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/zone/claim-pending-rewards", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<PendingStageRewardResponse>>(webRequest.downloadHandler.text);
    }


    public async Task<ApiResponse<GetStageEnemiesResponse>> GetStageEnemiesAsync(GetStageEnemiesRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<GetStageEnemiesResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/zone/get-stage-enemies", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<GetStageEnemiesResponse>>(webRequest.downloadHandler.text);
    }

    #endregion

    #region Exploration Grid API Methods --------------------------------------------------------------------------
    public async Task<ApiResponse<EnterExplorationCellResponse>> EnterExplorationCellAsync(EnterExplorationCellRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<EnterExplorationCellResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/exploration/enter-cell", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<EnterExplorationCellResponse>>(webRequest.downloadHandler.text);
    }

    public async Task<ApiResponse<ClearExplorationCellResponse>> ClearExplorationCellAsync(ClearExplorationCellRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ClearExplorationCellResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/exploration/clear-cell", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<ClearExplorationCellResponse>>(webRequest.downloadHandler.text);
    }

    public async Task<ApiResponse<GetActiveZoneRunProgressResponse>> GetActiveZoneRunProgressAsync(GetActiveZoneRunProgressRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<GetActiveZoneRunProgressResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/exploration/active-run-progress", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<GetActiveZoneRunProgressResponse>>(webRequest.downloadHandler.text);
    }

    public async Task<ApiResponse<EscapeExplorationZoneResponse>> EscapeExplorationZoneAsync(EscapeExplorationZoneRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<EscapeExplorationZoneResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/exploration/escape-zone", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<EscapeExplorationZoneResponse>>(webRequest.downloadHandler.text);
    }

    public async Task<ApiResponse<AbandonZoneRunResponse>> AbandonZoneRunAsync(AbandonZoneRunRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<AbandonZoneRunResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/exploration/abandon-run", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<AbandonZoneRunResponse>>(webRequest.downloadHandler.text);
    }

    public async Task<ApiResponse<IncreaseCommandPowerMaxResponse>> IncreaseCommandPowerMaxAsync(IncreaseCommandPowerMaxRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<IncreaseCommandPowerMaxResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/exploration/increase-command-power", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<IncreaseCommandPowerMaxResponse>>(webRequest.downloadHandler.text);
    }

    public async Task<ApiResponse<UnlockShipPresetResponse>> UnlockShipPresetAsync(UnlockShipPresetRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<UnlockShipPresetResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/exploration/unlock-ship-preset", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<UnlockShipPresetResponse>>(webRequest.downloadHandler.text);
    }

    #endregion

    #region Heartbeat API Methods ---------------------------------------------------------------------------------
    public async Task<ApiResponse<HeartbeatResponse>> HeartbeatAsync()
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<HeartbeatResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(new HeartbeatRequest());

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/zone/heartbeat", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<HeartbeatResponse>>(webRequest.downloadHandler.text);
    }
    #endregion

    #region PvP API Methods --------------------------------------------------------------------------------------
    public async Task<ApiResponse<PvpListResponse>> PvpListAsync(PvpListRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<PvpListResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"PvP List Request: {json}");

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/pvp/list", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<PvpListResponse>>(webRequest.downloadHandler.text);
        Debug.Log($"PvP List Response: {webRequest.downloadHandler.text}");
        return response;
    }

    public async Task<ApiResponse<PvpRefreshResponse>> PvpRefreshAsync(PvpRefreshRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<PvpRefreshResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"PvP Refresh Request: {json}");

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/pvp/refresh", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<PvpRefreshResponse>>(webRequest.downloadHandler.text);
        Debug.Log($"PvP Refresh Response: {webRequest.downloadHandler.text}");
        return response;
    }

    public async Task<ApiResponse<PvpBattleStartResponse>> PvpBattleStartAsync(PvpBattleStartRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<PvpBattleStartResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"PvP Battle Start Request: {json}");

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/pvp/battle/start", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<PvpBattleStartResponse>>(webRequest.downloadHandler.text);
        Debug.Log($"PvP Battle Start Response: {webRequest.downloadHandler.text}");
        return response;
    }

    public async Task<ApiResponse<PvpBattleResultResponse>> PvpBattleResultAsync(PvpBattleResultRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<PvpBattleResultResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"PvP Battle Result Request: {json}");

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/pvp/battle/result", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<PvpBattleResultResponse>>(webRequest.downloadHandler.text);
        Debug.Log($"PvP Battle Result Response: {webRequest.downloadHandler.text}");
        return response;
    }

    public async Task<ApiResponse<PvpRankingResponse>> PvpRankingAsync(PvpRankingRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<PvpRankingResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"PvP Ranking Request: {json}");

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/ranking/pvp", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<PvpRankingResponse>>(webRequest.downloadHandler.text);
        Debug.Log($"PvP Ranking Response: {webRequest.downloadHandler.text}");
        return response;
    }

    public async Task<ApiResponse<ZoneRankingResponse>> ZoneRankingAsync(ZoneRankingRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ZoneRankingResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        using var webRequest = new UnityWebRequest($"{m_baseUrl}/ranking/zone", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<ZoneRankingResponse>>(webRequest.downloadHandler.text);
        Debug.Log($"Zone Ranking Response: {webRequest.downloadHandler.text}");
        return response;
    }

    public async Task<ApiResponse<PvpMyRankResponse>> PvpMyRankAsync(PvpMyRankRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<PvpMyRankResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"PvP My Rank Request: {json}");

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/ranking/pvp/my-rank", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<PvpMyRankResponse>>(webRequest.downloadHandler.text);
        Debug.Log($"PvP My Rank Response: {webRequest.downloadHandler.text}");
        return response;
    }
    #endregion

    #region VIP API Methods ---------------------------------------------------------------------------------------
    public async Task<ApiResponse<VipStatusResponse>> PurchaseVipAsync(VipPurchaseRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<VipStatusResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/iap/vip/purchase", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        Debug.Log($"[ApiClient] PurchaseVip raw={webRequest.downloadHandler.text}");
        return JsonConvert.DeserializeObject<ApiResponse<VipStatusResponse>>(webRequest.downloadHandler.text);
    }

    public async Task<ApiResponse<VipStatusResponse>> GetVipStatusAsync()
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<VipStatusResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/iap/vip/status", "GET");
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<VipStatusResponse>>(webRequest.downloadHandler.text);
    }

    public async Task<ApiResponse<DailyClaimResponse>> ClaimVipDailyRewardAsync()
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<DailyClaimResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/iap/vip/daily-reward", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(new byte[0]);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<DailyClaimResponse>>(webRequest.downloadHandler.text);
    }

#if UNITY_EDITOR
    public async Task<ApiResponse<VipStatusResponse>> DebugForceVipAsync()
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<VipStatusResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/iap/debug/vip/force", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(new byte[0]);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<VipStatusResponse>>(webRequest.downloadHandler.text);
    }
#endif
    #endregion

    #region Progress API Methods ----------------------------------------------------------------------------------
    public async Task<ApiResponse<ProgressInfo>> SaveProgressAsync(ProgressSaveRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ProgressInfo>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/progress/save", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<ProgressInfo>>(webRequest.downloadHandler.text);
    }

    public async Task<ApiResponse<ProgressListResponse>> GetProgressListAsync(string category)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ProgressListResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        using var webRequest = new UnityWebRequest($"{m_baseUrl}/progress/{category}", "GET");
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<ProgressListResponse>>(webRequest.downloadHandler.text);
    }
    #endregion
}
