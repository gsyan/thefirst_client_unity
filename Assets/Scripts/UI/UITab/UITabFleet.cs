using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class UIPanelFleet_TabUpgrade : UITabBase
{
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
        
    }

    public override void OnTabActivated()
    {
        InitializeUI();
    }

    public override void OnTabDeactivated()
    {
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