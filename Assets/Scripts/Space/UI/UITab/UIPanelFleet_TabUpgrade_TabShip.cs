using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelFleet_TabUpgrade_TabShip : UITabBase
{
    [HideInInspector] public SpaceFleet m_myFleet;
    private SpaceShip m_selectedShip;
    private ModuleBase m_selectedModule;

    public TextMeshProUGUI m_textTop;
    public TextMeshProUGUI m_textShipStats;

    public Button m_backButton;

    // 부모 탭 시스템 참조
    [HideInInspector] public TabSystem m_tabSystem_TabUpgrade;

    public override void InitializeUITab()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;
        
        m_myFleet = character.GetOwnedFleet();
        if (m_myFleet == null) return;

        m_backButton.onClick.AddListener(() => m_tabSystem_TabUpgrade.SwitchToTab(0));
        
        EventManager.Subscribe_SpaceShipSelected_TabUpgrade(OnSpaceShipSelected);
    }
    private void OnSpaceShipSelected(SpaceShip ship)
    {
        m_selectedShip = ship;
    }

    public override void OnTabActivated()
    {
        EventManager.Subscribe_SpaceShipModuleSelected(OnModuleSelected);
        EventManager.Subscribe_ShipChange(OnShipChanged);

        // 함선 관리 모드로 전환
        CameraController.Instance.m_currentMode = ECameraControllerMode.Manage_Ship;

        if (m_selectedShip != null)
        {
            m_selectedShip.m_shipOutline.enabled = true;
            CameraController.Instance.SetTargetOfCameraController(m_selectedShip.transform);
        }

        InitializeUI();
    }

    public override void OnTabDeactivated()
    {
        EventManager.Unsubscribe_SpaceShipModuleSelected(OnModuleSelected);
        EventManager.Unsubscribe_ShipChange(OnShipChanged);
        
        // 평소 카메라 모드로 전환
        CameraController.Instance.m_currentMode = ECameraControllerMode.Normal;

        if (m_selectedShip != null)
            m_selectedShip.m_shipOutline.enabled = false;

        InitializeUI();
    }

    private void InitializeUI()
    {
        if (m_myFleet != null)
            m_myFleet.ClearAllSelectedModule();
        
        if( m_selectedShip == null)
            m_selectedShip = m_myFleet.m_ships[0];
        
        m_selectedModule = null;

        if (m_textTop != null)
            m_textTop.text = "Ship Management";

        UpdateShipStatsDisplay();
        
    }

    

    private void OnModuleSelected(SpaceShip ship, ModuleBase module)
    {
        if( m_selectedShip != ship) return;
        if (module == null) return;
        if (m_myFleet == null) return;
        
        m_selectedModule = module;
        m_selectedShip.SetSelectedModule_SpaceShip(ship, module);

        UpdateShipStatsDisplay();

        //m_tabSystem_TabUpgrade.SwitchToTab(2);
    }

    private void OnShipChanged()
    {
        UpdateShipStatsDisplay();
    }
    
    private void UpdateShipStatsDisplay()
    {
        if (m_textShipStats == null) return;

        SpaceShipStats statsOrg = m_selectedShip.m_spaceShipStatsOrg;
        SpaceShipStats statsCur = m_selectedShip.m_spaceShipStatsCur;
        
        string shipStatsText = $"=== SHIP STATS ===\n" +
                            $"Health: {statsCur.totalHealth:F0} / {statsOrg.totalHealth:F0}\n" +
                            $"Attack: {statsCur.totalAttackPower:F1} / {statsOrg.totalAttackPower:F1}\n" +
                            $"Speed: {statsCur.totalMovementSpeed:F1} / {statsOrg.totalMovementSpeed:F1}\n" +
                            $"Rotation: {statsCur.totalRotationSpeed:F1} / {statsOrg.totalRotationSpeed:F1}\n" +
                            $"Cargo: {statsCur.totalCargoCapacity:F0} / {statsOrg.totalCargoCapacity:F0}\n" +
                            $"Weapons: {statsCur.totalWeapons}\n" +
                            $"Engines: {statsCur.totalEngines}";
        
        m_textShipStats.text = shipStatsText;

        if (m_selectedModule == null) return;
        string moduleStatsCompareText = m_selectedModule.GetUpgradeComparisonText();
        m_textShipStats.text += "\n\n" + moduleStatsCompareText;
    }

}
