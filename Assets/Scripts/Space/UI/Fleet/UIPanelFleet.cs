using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// public enum FleetManagementTab
// {
//     Admiral,
//     Formation,
//     Upgrade
// }

public class UIPanelFleet : MonoBehaviour
{
    public UIManager m_UIManager;

    [Header("Tab System")]
    public TabSystem m_tabSystem;
    public UIPanelFleetUpgrade upgradePanel;
    public UIPanelFleetFormation formationPanel;
    public UIPanelFleetAdmiral admiralPanel;  
    
    [Header("Manual Tab Setup (Alternative)")]
    public Button closeButton;
    
    [HideInInspector] public SpaceFleet m_myFleet;

    public void InitializeUIPanelFleet()
    {
        if (m_myFleet == null)
            m_myFleet = DataManager.Instance.m_currentCharacter.GetOwnedFleet();

        upgradePanel.InitializeUIPanelFleetUpgrade();
        //m_tabSystem.tabs[0].onActivate = upgradePanel.OnTabActivated;
        //m_tabSystem.tabs[0].onDeactivate = upgradePanel.OnTabDeactivated;
        
        formationPanel.InitializeUIPanelFleetFormation();
        m_tabSystem.tabs[1].onActivate = formationPanel.OnTabActivated;
        m_tabSystem.tabs[1].onDeactivate = formationPanel.OnTabDeactivated;
        
        admiralPanel.InitializeUIPanelFleetAdmiral();
        m_tabSystem.tabs[2].onActivate = admiralPanel.OnTabActivated;
        m_tabSystem.tabs[2].onDeactivate = admiralPanel.OnTabDeactivated;

        if (closeButton != null && m_UIManager != null)
            closeButton.onClick.AddListener(OnClose);
    }
    
    private void OnClose()
    {
        if (m_UIManager != null)
            m_UIManager.ShowMainPanel();
    }
}

