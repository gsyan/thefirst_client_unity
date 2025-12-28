using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class UIPanelFleet_TabUpgrade : UITabBase
{
    [Header("Tab System")]
    [SerializeField] private TabSystem m_tabSystem;

    // Private fields
    [HideInInspector] public SpaceFleet m_myFleet;
    private SpaceShip m_selectedShip;
    private ModuleBase m_selectedModule;

    public override void InitializeUITab()
    {
        InitializeUIPanelFleetUpgrade();
    }
    private void InitializeUIPanelFleetUpgrade()
    {
        if (m_myFleet == null)
            m_myFleet = DataManager.Instance.m_currentCharacter.GetOwnedFleet();

        if (m_myFleet == null) return;
        m_myFleet.m_panelFleet_TabUpgrade = this;
        
        for (int i = 0; i < m_tabSystem.tabs.Count; i++)
        {
            var tabData = m_tabSystem.tabs[i];
            if (tabData.tabPanel != null)
            {
                UITabBase tabBase = tabData.tabPanel.GetComponent<UITabBase>();
                if (tabBase == null) continue;
                tabBase.InitializeUITab();
                tabData.onActivate = tabBase.OnTabActivated;
                tabData.onDeactivate = tabBase.OnTabDeactivated;

                // 탭 참조 저장 및 부모 패널 설정
                tabBase.m_tabSystemParent = m_tabSystem;

                // 탭 버튼 숨기기
                if (tabData.tabButton != null)
                    tabData.tabButton.gameObject.SetActive(false);
            }
        }

    }

    public override void OnTabActivated()
    {
        m_tabSystem.ForceActivateTab();
        
        InitializeUI();
    }

    public override void OnTabDeactivated()
    {
        m_tabSystem.ForceDeactivateTab();
        
        InitializeUI();
    }

    private void InitializeUI()
    {
        // if (panelShipInfo != null)
        //     panelShipInfo.SetActive(false);

        // if (selectedModuleText != null)
        //     selectedModuleText.text = "Click on a module to select it";

        if (m_myFleet != null)
            m_myFleet.ClearAllSelectedModule();
        
        //UpdateFleetStatsDisplay();
    }
    
}