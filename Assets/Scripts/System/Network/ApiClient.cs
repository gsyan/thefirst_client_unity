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
    private readonly string baseUrl = "http://192.168.0.61:8080/api";
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

    private async Task SendRequestAsync(UnityWebRequest request)
    {
        var operation = request.SendWebRequest();
        while (!operation.isDone)
            await Task.Yield();

        if (request.result != UnityWebRequest.Result.Success)
        {
            //string errorText = request.downloadHandler?.text ?? request.error;
            //Debug.LogError($"Request failed: {request.error} - {errorText} (HTTP {request.responseCode})");
            ServerErrorCode errorCode = GetHttpErrorCode(request.responseCode);
            throw new CustomException(errorCode);
        }
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

    public async Task<ApiResponse<ModuleResearchResponse>> ResearchModuleAsync(ModuleResearchRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) return ApiResponse<ModuleResearchResponse>.error((int)ServerErrorCode.CLIENT_REFRESH_TOKEN_NULL);

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"Module Research Request: {json}");

        using var webRequest = new UnityWebRequest($"{baseUrl}/fleet/research-module", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<ModuleResearchResponse>>(webRequest.downloadHandler.text);
        Debug.Log($"Module Research Response: {webRequest.downloadHandler.text}");
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
}
