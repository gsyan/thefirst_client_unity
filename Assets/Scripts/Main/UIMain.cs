//------------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIMain : UIManager
{
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public TMP_InputField characterNameInput;
    public Button registerButton;
    public Button loginButton;
    public Button googleLoginButton;
    public Button createCharacterButton;
    public Button getCharactersButton;
    public Button selectCharacterButton;

    private List<CharacterResponse> m_characterList = new List<CharacterResponse>();

    private void Start()
    {
        registerButton.onClick.AddListener(() => Register());
        loginButton.onClick.AddListener(() => Login(null, null));
        if (googleLoginButton != null)
        {
            googleLoginButton.onClick.AddListener(() => GoogleLogin());
        }
        // createCharacterButton.onClick.AddListener(() => CreateCharacter(null));
        // getCharactersButton.onClick.AddListener(() => GetCharacters());
        // selectCharacterButton.onClick.AddListener(() => SelectCharacter());
    }

    private void Register()
    {
        string email = emailInput.text;
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            m_resultText.text = "Email and password are required!";
            return;
        }

        if (m_resultText != null)
            m_resultText.text = "Processing...";

        NetworkManager.Instance.Register(email, password, (response) => {
            ServerErrorCode errorCode = (ServerErrorCode)response.errorCode;
            string message = "";
            if (errorCode == ServerErrorCode.SUCCESS)
            {
                message = ErrorCodeMapping.Messages[errorCode];
                Debug.Log($"Registration successful: {message}");

                //Login(email, password); // Attempt automatic login
            }
            else
            {
                message = ErrorCodeMapping.GetMessage(response.errorCode);
                Debug.LogError($"Registration failed - ErrorCode: {errorCode}, Message: {message}");
            }
            if (m_resultText != null)
                m_resultText.text = $"Result: {message}";
        });
    }

    private void Login(string email, string password)
    {
        if (email == null)
            email = emailInput.text;
        if (password == null)
            password = passwordInput.text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            m_resultText.text = "Email and password are required!";
            return;
        }

        if (m_resultText != null)
            m_resultText.text = "Processing...";

        NetworkManager.Instance.Login(email, password, (response) => {
            ServerErrorCode errorCode = (ServerErrorCode)response.errorCode;
            string message = "";
            if (errorCode == ServerErrorCode.SUCCESS)
            {
                message = ErrorCodeMapping.Messages[errorCode];
                Debug.Log($"Login successful: {message}");
                GetCharacters();
            }
            else
            {
                message = ErrorCodeMapping.GetMessage(response.errorCode);
                Debug.LogError($"Login failed - ErrorCode: {errorCode}, Message: {message}");
            }
            if (m_resultText != null)
                m_resultText.text = $"Result: {message}";
        });
    }

    private void GoogleLogin()
    {
        if (m_resultText != null)
            m_resultText.text = "Processing Google Login...";

        NetworkManager.Instance.GoogleLogin((response) => {
            ServerErrorCode errorCode = (ServerErrorCode)response.errorCode;
            string message = "";
            if (errorCode == ServerErrorCode.SUCCESS)
            {
                message = ErrorCodeMapping.Messages[errorCode];
                Debug.Log($"Google Login successful: {message}");
                Debug.Log($"Access Token received: {response.data?.accessToken}");

                GetCharacters();
            }
            else
            {
                message = ErrorCodeMapping.GetMessage(response.errorCode);
                Debug.LogError($"Google Login failed - ErrorCode: {errorCode}, Message: {message}");
            }
            if (m_resultText != null)
                m_resultText.text = $"Result: {message}";
        });
    }

    public void GetCharacters()
    {
        if (m_resultText != null)
            m_resultText.text = "Processing...";

        NetworkManager.Instance.GetCharacters((response) =>
        {
            ServerErrorCode errorCode = (ServerErrorCode)response.errorCode;
            string message = "";
            if (errorCode == ServerErrorCode.SUCCESS)
            {
                message = ErrorCodeMapping.Messages[errorCode];
                Debug.Log($"Get characters successful: {message}");
                
                if (response.data != null)  // Character list accessible from response.data
                {
                    m_characterList = response.data;
                    Debug.Log($"Found {response.data.Count} characters:");
                    foreach (var character in response.data)
                    {
                        Debug.Log($"- {character.characterName} (ID: {character.characterId})");
                    }

                    if (m_characterList.Count > 0)
                    {
                        SelectCharacter(m_characterList[0].characterId); // Select first character
                    }
                    else
                    {
                        CreateCharacter("HiddenCharacter_" + emailInput.text); // Automatically create character
                    }
                }
                else
                {
                    Debug.Log("Characters List error.");
                }
            }
            else
            {
                message = ErrorCodeMapping.GetMessage(response.errorCode);
                Debug.LogError($"Get characters failed - ErrorCode: {errorCode}, Message: {message}");
            }
            if (m_resultText != null)
                m_resultText.text = $"Result: {message}";
        });
    }

    private void CreateCharacter(string name)
    {
        if( name == null)
            name = characterNameInput.text;

        if (string.IsNullOrEmpty(name))
        {
            m_resultText.text = "Character name is required!";
            return;
        }

        if (m_resultText != null)
            m_resultText.text = "Processing...";

        NetworkManager.Instance.CreateCharacter(name, (response) => {
            ServerErrorCode errorCode = (ServerErrorCode)response.errorCode;
            string message = "";
            if (errorCode == ServerErrorCode.SUCCESS)
            {
                message = ErrorCodeMapping.Messages[errorCode];
                Debug.Log($"Character creation successful: {message}");
                
                // Created character information accessible from response.data
                if (response.data != null)
                {
                    Debug.Log($"Created character: {response.data.characterName} (ID: {response.data.characterId})");
                }
                
                GetCharacters(); // Refresh character list
            }
            else
            {
                message = ErrorCodeMapping.GetMessage(response.errorCode);
                Debug.LogError($"Character creation failed - ErrorCode: {errorCode}, Message: {message}");
            }
            if (m_resultText != null)
                m_resultText.text = $"Result: {message}";
        });
    }

    

    private void SelectCharacter(long characterId = 0)
    {
        // Use first character if characterId is 0
        if (characterId == 0)
        {
            if (m_characterList.Count > 0)
            {
                characterId = m_characterList[0].characterId;
            }
            else
            {
                if (m_resultText != null)
                    m_resultText.text = "No characters available to select!";
                return;
            }
        }
        
        if (m_resultText != null)
            m_resultText.text = "Processing...";

        NetworkManager.Instance.SelectCharacter(characterId, (response) => {
            ServerErrorCode errorCode = (ServerErrorCode)response.errorCode;
            string message = "";
            if (errorCode == ServerErrorCode.SUCCESS)
            {
                message = ErrorCodeMapping.Messages[errorCode];
                Debug.Log($"Character selection successful: {message}, loading SpaceScene");
                
                // New access token information accessible from response.data
                if (response.data != null)
                {
                    Debug.Log($"New Access Token received: {response.data.accessToken}");
                    
                    // Save fleet information to DataManager
                    if (response.data.activeFleetInfo != null)
                    {
                        DataManager.Instance.SetFleetData(response.data.activeFleetInfo);
                        Debug.Log($"Fleet data saved to DataManager: {response.data.activeFleetInfo.fleetName} with {response.data.activeFleetInfo.ships?.Length ?? 0} ships");
                    }
                    else
                    {
                        Debug.LogWarning("No active fleet data received from server");
                        DataManager.Instance.ClearFleetData();
                    }

                    // Save character information to DataManager
                    if (response.data.characterInfo != null)
                    {
                        DataManager.Instance.SetCharacterInfo(response.data.characterInfo);

                        // Set researched modules to Character
                        if (response.data.researchedModuleTypePackeds != null)
                        {
                            DataManager.Instance.m_currentCharacter.SetResearchedModules(response.data.researchedModuleTypePackeds);
                            Debug.Log($"Researched modules loaded: {response.data.researchedModuleTypePackeds.Length} modules");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("No character status data received from server");
                        DataManager.Instance.ClearCharacterData();
                    }
                }

                LoadingManager.LoadSceneWithLoading("SpaceScene");
            }
            else
            {
                message = ErrorCodeMapping.GetMessage(response.errorCode);
                Debug.LogError($"Character selection failed - ErrorCode: {errorCode}, Message: {message}");
            }
            if (m_resultText != null)
                m_resultText.text = $"Result: {message}";
        });
    }

}