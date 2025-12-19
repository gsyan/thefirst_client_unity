using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelFleet_TabUpgrade_TabShip_TabShip : UITabBase
{
    [HideInInspector] public SpaceFleet m_myFleet;
    // private SpaceShip m_selectedShip;
    // private ModuleBase m_selectedModule;

    // 부모 탭 시스템 참조
    [HideInInspector] public TabSystem m_tabSystem_TabUpgrade_TabShip;

    public override void InitializeUITab()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;
        
        m_myFleet = character.GetOwnedFleet();
        if (m_myFleet == null) return;

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
        
    }
}
