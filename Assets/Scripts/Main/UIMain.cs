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
        // TutorialManager는 DontDestroyOnLoad라 로그아웃 등으로 MainScene에 돌아와도 인스턴스가 안 죽어서
        // EventManager.UnsubscribeAll()로 지워진 구독이 복구 안 되고 이전 세션 상태가 남아있음 —
        // MainScene 진입은 항상 새 세션의 시작점이므로 여기서 초기화 (최초 실행 시엔 이미 기본값이라 무해함)
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.ResetForLogout();

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

                    // SpaceScene 진입 전 튜토리얼 진행도 미리 확보 — 지크프리트 함대 스폰 여부를 씬 로드 전에 결정 가능
                    if (TutorialManager.Instance != null)
                        TutorialManager.Instance.ApplyProgressList(response.data.progressList);

                    if (response.data.commanderInfo != null)
                    {
                        DataManager.Instance.SetCommanderInfo(response.data.commanderInfo);
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