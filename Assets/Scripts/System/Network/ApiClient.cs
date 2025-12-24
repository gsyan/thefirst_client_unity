//------------------------------------------------------------------------------
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class ApiClient
{
#if UNITY_EDITOR
    // 유니티 에디터에서 실행될 때 사용할 URL (로컬 개발 서버)
    private readonly string baseUrl = "http://localhost:8080/api";
#elif DEVELOPMENT_BUILD
    // 개발 빌드(Development Build)에서 사용할 URL (개발 테스트 서버)
    private readonly string baseUrl = "http://192.168.0.61:8080/api";
#else
    // 출시 빌드(Release Build)에서 사용할 URL (실제 서비스 서버)
    private readonly string baseUrl = "https://www.fidforge.com/api";
    //private readonly string baseUrl = "http://192.168.0.51:8080/api";
#endif

    private string accessToken;

    #region Core Methods ------------------------------------------------------------------------------------------
    public void SetAccessToken(string token)
    {
        accessToken = token;
        Debug.Log($"SetAccessToken: {accessToken}");
    }

    private async Task SendRequestAsync(UnityWebRequest request)
    {
        var operation = request.SendWebRequest();
        while (!operation.isDone)
        {
            await Task.Yield();
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            string errorText = request.downloadHandler?.text ?? request.error;
            int errorCode = request.responseCode == 401 ? (int)ServerErrorCode.LOGIN_FAIL_REASON1 : (int)ServerErrorCode.UNKNOWN_ERROR;
            Debug.LogError($"Request failed: {request.error} - {errorText}");
            throw new Exception($"Request failed: {errorText} (Code: {errorCode})");
        }
    }
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
        return JsonConvert.DeserializeObject<ApiResponse<AuthResponse>>(request.downloadHandler.text);
    }

    public async Task<ApiResponse<AuthResponse>> RefreshTokenAsync(string refreshToken)
    {
        var requestDto = new RefreshTokenRequest { refreshToken = refreshToken };
        string json = JsonConvert.SerializeObject(requestDto);
        Debug.Log($"RefreshToken JSON: {json}");

        using var request = new UnityWebRequest($"{baseUrl}/account/refresh", "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<AuthResponse>>(request.downloadHandler.text);
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
        return JsonConvert.DeserializeObject<ApiResponse<AuthResponse>>(request.downloadHandler.text);
    }

    public async Task<ApiResponse<string>> DeleteAccountAsync()
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogError("AccessToken is null or empty");
            throw new Exception("AccessToken is not set");
        }

        using var request = new UnityWebRequest($"{baseUrl}/account/delete", "DELETE");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<string>>(request.downloadHandler.text);
    }

    public async Task<ApiResponse<CharacterResponse>> CreateCharacterAsync(string characterName)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogError("AccessToken is null or empty");
            throw new Exception("AccessToken is not set");
        }

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
        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogError("AccessToken is null or empty");
            throw new Exception("AccessToken is not set");
        }

        using var request = new UnityWebRequest($"{baseUrl}/character/characters", "GET");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<List<CharacterResponse>>>(request.downloadHandler.text);
    }

    public async Task<ApiResponse<AuthResponse>> SelectCharacterAsync(long characterId)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogError("AccessToken is null or empty");
            throw new Exception("AccessToken is not set");
        }

        using var request = new UnityWebRequest($"{baseUrl}/character/select-character/{characterId}", "POST");
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(request);
        return JsonConvert.DeserializeObject<ApiResponse<AuthResponse>>(request.downloadHandler.text);
    }
    #endregion

    #region Development API Methods -------------------------------------------------------------------------------
    public async Task<ApiResponse<string>> ExecuteDevCommandAsync(string command, string[] parameters)
    {
        if (string.IsNullOrEmpty(accessToken)) throw new Exception("AccessToken is not set");

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
        if (string.IsNullOrEmpty(accessToken)) throw new Exception("AccessToken is not set");

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"Add Ship Request: {json}");

        using var webRequest = new UnityWebRequest($"{baseUrl}/fleet/add-ship", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Add Ship Request failed: {webRequest.error}");
            return new ApiResponse<AddShipResponse> { errorCode = -1, errorMessage = webRequest.error };
        }

        string responseText = webRequest.downloadHandler.text;
        Debug.Log($"Add Ship Response: {responseText}");

        var response = JsonConvert.DeserializeObject<ApiResponse<AddShipResponse>>(responseText);
        return response ?? new ApiResponse<AddShipResponse> { errorCode = -1, errorMessage = "Failed to parse response" };
    }

    public async Task<ApiResponse<ChangeFormationResponse>> ChangeFormationAsync(ChangeFormationRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) throw new Exception("AccessToken is not set");

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"Change Formation Request: {json}");

        using var webRequest = new UnityWebRequest($"{baseUrl}/fleet/change-formation", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        try
        {
            await webRequest.SendWebRequest();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Change Formation Exception: {e.Message}");
            return new ApiResponse<ChangeFormationResponse> { errorCode = -1, errorMessage = e.Message };
        }

        string responseText = webRequest.downloadHandler.text;
        Debug.Log($"Change Formation Response Code: {webRequest.responseCode}, Text: {responseText}");

        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Change Formation Request failed: {webRequest.error}");
            return new ApiResponse<ChangeFormationResponse> { errorCode = -1, errorMessage = webRequest.error };
        }

        var response = JsonConvert.DeserializeObject<ApiResponse<ChangeFormationResponse>>(responseText);
        return response ?? new ApiResponse<ChangeFormationResponse> { errorCode = -1, errorMessage = "Failed to parse response" };
    }

    public async Task<ApiResponse<ModuleUpgradeResponse>> UpgradeModuleAsync(ModuleUpgradeRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) throw new Exception("AccessToken is not set");

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
        if (string.IsNullOrEmpty(accessToken)) throw new Exception("AccessToken is not set");

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
        if (string.IsNullOrEmpty(accessToken)) throw new Exception("AccessToken is not set");

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
        if (string.IsNullOrEmpty(accessToken)) throw new Exception("AccessToken is not set");

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

    public async Task<ApiResponse<ShipInfo>> AddModuleBodyAsync(ModuleBodyAddRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) throw new Exception("AccessToken is not set");

        string json = JsonConvert.SerializeObject(request);
        Debug.Log($"Add ModuleBody Request: {json}");

        using var webRequest = new UnityWebRequest($"{baseUrl}/fleet/add-modulebody", "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<ShipInfo>>(webRequest.downloadHandler.text);
        return response;
    }

    public async Task<ApiResponse<ShipInfo>> RemoveModuleBodyAsync(ModuleBodyRemoveRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) throw new Exception("AccessToken is not set");

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
        if (string.IsNullOrEmpty(accessToken)) throw new Exception("AccessToken is not set");

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

    public async Task<ApiResponse<FleetStatsResponse>> GetFleetStatsAsync(FleetStatsRequest request)
    {
        if (string.IsNullOrEmpty(accessToken)) throw new Exception("AccessToken is not set");

        string queryParam = $"?fleetId={request.fleetId}";

        using var webRequest = new UnityWebRequest($"{baseUrl}/fleet/stats{queryParam}", "GET");
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");

        await SendRequestAsync(webRequest);

        var response = JsonConvert.DeserializeObject<ApiResponse<FleetStatsResponse>>(webRequest.downloadHandler.text);
        return response;
    }
    #endregion
}
