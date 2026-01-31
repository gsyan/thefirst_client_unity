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
    private void Start()
    {
        InitializeUIManager();
        NetworkManager.Instance.OnChangeScene();
    }

    public override void InitializeUIManager()
    {
        const string PANEL_Main_PREFAB_PATH = "Prefabs/UI/Panel_Main";

        // Load all prefabs from the Panel folder
        GameObject[] panelPrefabs = Resources.LoadAll<GameObject>(PANEL_Main_PREFAB_PATH);

        if (panelPrefabs == null || panelPrefabs.Length == 0)
        {
            Debug.LogWarning($"No panel prefabs found in {PANEL_Main_PREFAB_PATH}");
            return;
        }

        foreach (GameObject prefab in panelPrefabs)
        {
            GameObject panelInstance = Instantiate(prefab, transform);
            panelInstance.name = prefab.name; // Remove "(Clone)" suffix
            
            var panelBase = panelInstance.GetComponent<UIPanelBase>();
            if(panelBase != null)
            {
                panelBase.panelName = prefab.name;
                panelBase.InitializeUIPanel();
            }
                
            AddPanel(panelBase);
        }

        InitializePanels();
        ShowMainPanel();
    }

    private List<CharacterResponse> m_characterList = new List<CharacterResponse>();

    public void GetCharacters()
    {
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
                        //CreateCharacter("HiddenCharacter_" + emailInput.text); // Automatically create character
                        Debug.LogError("No characters found.");
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
        });
    }

    private void CreateCharacter(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        
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
        });
    }

    

    private void SelectCharacter(long characterId = 0)
    {
        // Use first character if characterId is 0
        if (characterId == 0)
        {
            if (m_characterList.Count > 0)
                characterId = m_characterList[0].characterId;
            else
                return;
        }

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
                        DataManager.Instance.SetFleetData(response.data.activeFleetInfo);
                    else
                        DataManager.Instance.ClearFleetData();

                    // Save character information to DataManager
                    if (response.data.characterInfo != null)
                    {
                        DataManager.Instance.SetCharacterInfo(response.data.characterInfo);

                        // Set researched modules to Character
                        if (response.data.researchedModuleTypes != null)
                        {
                            DataManager.Instance.m_currentCharacter.SetResearchedModules(response.data.researchedModuleTypes);
                            Debug.Log($"Researched modules loaded: {response.data.researchedModuleTypes.Length} modules");
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
        });
    }

}