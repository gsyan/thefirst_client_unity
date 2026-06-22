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
        base.InitializeUIManager();

        const string PANEL_Main_PREFAB_PATH = "Prefabs/UI/Panel_Main";

        // Load all prefabs from the Panel folder
        GameObject[] panelPrefabs = ResourceManager.Instance.LoadAll<GameObject>(PANEL_Main_PREFAB_PATH);

        if (panelPrefabs == null || panelPrefabs.Length == 0)
        {
            Debug.LogWarning($"No panel prefabs found in {PANEL_Main_PREFAB_PATH}");
            return;
        }

        foreach (GameObject prefab in panelPrefabs)
        {
            GameObject panelInstance = Instantiate(prefab, m_generalContainer);
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

    private List<CommanderResponse> m_commanderList = new List<CommanderResponse>();

    public void GetCommanders()
    {
        NetworkManager.Instance.GetCommanders((response) =>
        {
            ServerErrorCode errorCode = (ServerErrorCode)response.errorCode;
            string message = "";
            if (errorCode == ServerErrorCode.SUCCESS)
            {
                message = ErrorCodeMapping.Messages[errorCode];

                if (response.data != null)
                {
                    m_commanderList = response.data;

                    if (m_commanderList.Count > 0)
                    {
                        SelectCommander(m_commanderList[0].commanderId);
                    }
                    else
                    {
                        Debug.LogError("No commanders found.");
                    }
                }
                else
                {
                    Debug.Log("Commanders List error.");
                }
            }
            else
            {
                message = ErrorCodeMapping.GetMessage(response.errorCode);
                Debug.LogError($"Get commanders failed - ErrorCode: {errorCode}, Message: {message}");
            }
        });
    }

    private void SelectCommander(long commanderId = 0)
    {
        if (commanderId == 0)
        {
            if (m_commanderList.Count > 0)
                commanderId = m_commanderList[0].commanderId;
            else
                return;
        }

        NetworkManager.Instance.SelectCommander(commanderId, (response) => {
            ServerErrorCode errorCode = (ServerErrorCode)response.errorCode;
            string message = "";
            if (errorCode == ServerErrorCode.SUCCESS)
            {
                message = ErrorCodeMapping.Messages[errorCode];

                if (response.data != null)
                {
                    if (response.data.activeFleetInfo != null)
                        DataManager.Instance.SetFleetData(response.data.activeFleetInfo);
                    else
                        DataManager.Instance.ClearFleetData();

                    DataManager.Instance.m_isGoogleLinked = response.data.bGoogleLinked;
                    IAPManager.Instance.ApplyVipStatus(response.data.vipStatus);

                    if (response.data.commanderInfo != null)
                    {
                        DataManager.Instance.SetCommanderInfo(response.data.commanderInfo);

                        if (response.data.researchedIds != null)
                            DataManager.Instance.m_currentCommander.SetCompletedResearchIds(response.data.researchedIds);
                    }
                    else
                    {
                        Debug.LogWarning("No commander status data received from server");
                        DataManager.Instance.ClearCommanderData();
                    }
                }

                LoadingManager.LoadSceneWithLoading("SpaceScene");
            }
            else
            {
                message = ErrorCodeMapping.GetMessage(response.errorCode);
                Debug.LogError($"Commander selection failed - ErrorCode: {errorCode}, Message: {message}");
            }
        });
    }

}