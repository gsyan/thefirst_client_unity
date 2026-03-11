//------------------------------------------------------------------------------
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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

public class ApiClient
{
#if UNITY_EDITOR
    // 유니티 에디터에서 실행될 때 사용할 URL (로컬 개발 서버)
    private readonly string baseUrl = "http://localhost:8080/api";
    //private readonly string baseUrl = "http://192.168.0.51:8080/api";
#elif DEVELOPMENT_BUILD
    // 개발 빌드(Development Build)에서 사용할 URL (개발 테스트 서버)
    //private readonly string baseUrl = "http://192.168.0.61:8080/api";
    private readonly string baseUrl = "https://www.fidforge.com/api";
#else
    // 출시 빌드(Release Build)에서 사용할 URL (실제 서비스 서버)
    private readonly string baseUrl = "https://www.fidforge.com/api";
    //private readonly string baseUrl = "http://192.168.0.51:8080/api";
#endif

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
            using var request = UnityWebRequest.Get(baseUrl);
            request.timeout = 3;
            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            // 연결 자체가 실패한 경우만 false (ConnectionError)
            // 4xx, 5xx 응답은 서버가 살아있다는 의미
            bool isServerAlive = request.result != UnityWebRequest.Result.ConnectionError;
            Debug.Log($"[ServerCheck] URL: {baseUrl}, Result: {request.result}, Code: {request.responseCode}, Alive: {isServerAlive}");
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

    #region Authentication API Methods ----------------------------------------------------------------------------
    public async Task<ApiResponse<string>> SignUpAsync(string email, string password)
    {
        var requestDto = new SignUpRequest { email = email, password = password };
        string json = JsonConvert.SerializeObject(requestDto);
        Debug.Log($"SignUp JSON: {json}");

        using var request = new UnityWebRequest($"{baseUrl}/account/signup", "POST");
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

        using var request = new UnityWebRequest($"{baseUrl}/account/login", "POST");
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

        using var request = new UnityWebRequest($"{baseUrl}/account/refresh", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        await SendRequestAsync(request);
        Debug.Log($"[RefreshToken] Raw response: {request.downloadHandler.text}");  // ← 추가
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

        using var request = new UnityWebRequest($"{baseUrl}/account/google-login", "POST");
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
        Debug.Log($"GuestLogin JSON: {json}");

        using var request = new UnityWebRequest($"{baseUrl}/account/guest-login", "POST");
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

        using var request = new UnityWebRequest($"{baseUrl}/account/delete", "DELETE");
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

        using var request = new UnityWebRequest($"{baseUrl}/account/link-google", "POST");
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

        using var request = new UnityWebRequest($"{baseUrl}/account/unlink-google", "POST");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<UnlinkGoogleResponse>>(request.downloadHandler.text);
    }

    public async Task<ApiResponse<CharacterResponse>> CreateCharacterAsync(string characterName)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<CharacterResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        var requestDto = new CharacterCreateRequest { characterName = characterName };
        string json = JsonConvert.SerializeObject(requestDto);
        Debug.Log($"CreateCharacter JSON: {json}");

        using var request = new UnityWebRequest($"{baseUrl}/character/create", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<CharacterResponse>>(request.downloadHandler.text);
    }

    public async Task<ApiResponse<bool>> ValidateCharacterNameAsync(string name)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<bool>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(new CharacterValidateNameRequest { name = name });

        using var request = new UnityWebRequest($"{baseUrl}/character/validate-name", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<bool>>(request.downloadHandler.text);
    }

    public async Task<ApiResponse<CharacterRenameResponse>> RenameCharacterAsync(CharacterRenameRequest renameRequest)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<CharacterRenameResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(renameRequest);
        Debug.Log($"RenameCharacter JSON: {json}");

        using var request = new UnityWebRequest($"{baseUrl}/character/rename", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        var response = JsonConvert.DeserializeObject<ApiResponse<CharacterRenameResponse>>(request.downloadHandler.text);
        Debug.Log($"RenameCharacter Response: {request.downloadHandler.text}");
        return response;
    }

    public async Task<ApiResponse<List<CharacterResponse>>> GetAllCharactersAsync()
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<List<CharacterResponse>>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        using var request = new UnityWebRequest($"{baseUrl}/character/characters", "GET");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<List<CharacterResponse>>>(request.downloadHandler.text);
    }

    public async Task<ApiResponse<AuthResponse>> SelectCharacterAsync(long characterId)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<AuthResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        using var request = new UnityWebRequest($"{baseUrl}/character/select-character/{characterId}", "POST");
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

        using var request = new UnityWebRequest($"{baseUrl}/dev/command", "POST");
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

        using var webRequest = new UnityWebRequest($"{baseUrl}/fleet/add-ship", "POST");
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

        using var webRequest = new UnityWebRequest($"{baseUrl}/fleet/change-formation", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<ChangeFormationResponse>>(webRequest.downloadHandler.text);
    }

    public async Task<ApiResponse<ModuleUpgradeResponse>> UpgradeModuleAsync(ModuleUpgradeRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ModuleUpgradeResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"Module Upgrade Request: {json}");

        using var webRequest = new UnityWebRequest($"{baseUrl}/fleet/upgrade-module", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<ModuleUpgradeResponse>>(webRequest.downloadHandler.text);
        Debug.Log($"Module Upgrade Response: {webRequest.downloadHandler.text}");
        return response;
    }

    public async Task<ApiResponse<ModuleChangeResponse>> ChangeModuleAsync(ModuleChangeRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ModuleChangeResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"Module Change Request: {json}");

        using var webRequest = new UnityWebRequest($"{baseUrl}/fleet/change-module", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<ModuleChangeResponse>>(webRequest.downloadHandler.text);
        Debug.Log($"Module Change Response: {webRequest.downloadHandler.text}");
        return response;
    }

    public async Task<ApiResponse<ModuleUnlockResponse>> UnlockModuleAsync(ModuleUnlockRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ModuleUnlockResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"Module Unlock Request: {json}");
        
        using var webRequest = new UnityWebRequest($"{baseUrl}/fleet/unlock-module", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<ModuleUnlockResponse>>(webRequest.downloadHandler.text);
        Debug.Log($"Module Unlock Response: {webRequest.downloadHandler.text}");
        return response;
    }

    public async Task<ApiResponse<TechLevelResearchResponse>> ResearchTechLevelAsync(TechLevelResearchRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<TechLevelResearchResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"TechLevel Research Request: {json}");

        using var webRequest = new UnityWebRequest($"{baseUrl}/fleet/research-tech-level", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<TechLevelResearchResponse>>(webRequest.downloadHandler.text);
        Debug.Log($"TechLevel Research Response: {webRequest.downloadHandler.text}");
        return response;
    }

    // public async Task<ApiResponse<ShipInfo>> AddModuleBodyAsync(ModuleBodyAddRequest request)
    // {
    //     if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ShipInfo>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

    //     string json = JsonConvert.SerializeObject(request);
    //     Debug.Log($"Add ModuleBody Request: {json}");

    //     using var webRequest = new UnityWebRequest($"{baseUrl}/fleet/add-modulebody", "POST");
    //     webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
    //     webRequest.downloadHandler = new DownloadHandlerBuffer();
    //     webRequest.SetRequestHeader("Content-Type", "application/json");
    //     webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

    //     await SendRequestAsync(webRequest);

    //     var response = JsonConvert.DeserializeObject<ApiResponse<ShipInfo>>(webRequest.downloadHandler.text);
    //     return response;
    // }

    public async Task<ApiResponse<ShipInfo>> RemoveModuleBodyAsync(ModuleBodyRemoveRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ShipInfo>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{baseUrl}/fleet/remove-modulebody", "DELETE");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<ShipInfo>>(webRequest.downloadHandler.text);
        return response;
    }

    public async Task<ApiResponse<ShipInfo>> InstallModuleAsync(ModuleInstallRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ShipInfo>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{baseUrl}/fleet/install-module", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<ShipInfo>>(webRequest.downloadHandler.text);
        return response;
    }

    // public async Task<ApiResponse<FleetStatsResponse>> GetFleetStatsAsync(FleetStatsRequest request)
    // {
    //     if (string.IsNullOrEmpty(accessToken)) return ApiResponse<FleetStatsResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

    //     string queryParam = $"?fleetId={request.fleetId}";

    //     using var webRequest = new UnityWebRequest($"{baseUrl}/fleet/stats{queryParam}", "GET");
    //     webRequest.downloadHandler = new DownloadHandlerBuffer();
    //     webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

    //     await SendRequestAsync(webRequest);

    //     var response = JsonConvert.DeserializeObject<ApiResponse<FleetStatsResponse>>(webRequest.downloadHandler.text);
    //     return response;
    // }
    #endregion

    #region Zone Battle API Methods -------------------------------------------------------------------------------
    public async Task<ApiResponse<ZoneClearResponse>> ClearZoneAsync(ZoneClearRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ZoneClearResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"Zone Clear Request: {json}");

        using var webRequest = new UnityWebRequest($"{baseUrl}/zone/clear", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<ZoneClearResponse>>(webRequest.downloadHandler.text);
        Debug.Log($"Zone Clear Response: {webRequest.downloadHandler.text}");
        return response;
    }

    public async Task<ApiResponse<ZoneCollectResponse>> CollectZoneAsync(ZoneCollectRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ZoneCollectResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"Zone Collect Request: {json}");

        using var webRequest = new UnityWebRequest($"{baseUrl}/zone/collect", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<ZoneCollectResponse>>(webRequest.downloadHandler.text);
        Debug.Log($"Zone Collect Response: {webRequest.downloadHandler.text}");
        return response;
    }

    public async Task<ApiResponse<ZoneKillResponse>> KillZoneEnemyAsync(ZoneKillRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ZoneKillResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{baseUrl}/zone/kill", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<ZoneKillResponse>>(webRequest.downloadHandler.text);
    }
    #endregion

    #region Heartbeat API Methods ---------------------------------------------------------------------------------
    public async Task<ApiResponse<HeartbeatResponse>> HeartbeatAsync()
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<HeartbeatResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(new HeartbeatRequest());

        using var webRequest = new UnityWebRequest($"{baseUrl}/zone/heartbeat", "POST");
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

        using var webRequest = new UnityWebRequest($"{baseUrl}/pvp/list", "POST");
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

        using var webRequest = new UnityWebRequest($"{baseUrl}/pvp/refresh", "POST");
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

        using var webRequest = new UnityWebRequest($"{baseUrl}/pvp/battle/start", "POST");
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

        using var webRequest = new UnityWebRequest($"{baseUrl}/pvp/battle/result", "POST");
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

        using var webRequest = new UnityWebRequest($"{baseUrl}/ranking/pvp", "POST");
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
        using var webRequest = new UnityWebRequest($"{baseUrl}/ranking/zone", "POST");
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

        using var webRequest = new UnityWebRequest($"{baseUrl}/ranking/pvp/my-rank", "POST");
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

    #region Progress API Methods ----------------------------------------------------------------------------------
    public async Task<ApiResponse<ProgressInfo>> SaveProgressAsync(ProgressSaveRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ProgressInfo>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);

        using var webRequest = new UnityWebRequest($"{baseUrl}/progress/save", "POST");
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

        using var webRequest = new UnityWebRequest($"{baseUrl}/progress/{category}", "GET");
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);
        return JsonConvert.DeserializeObject<ApiResponse<ProgressListResponse>>(webRequest.downloadHandler.text);
    }
    #endregion
}
