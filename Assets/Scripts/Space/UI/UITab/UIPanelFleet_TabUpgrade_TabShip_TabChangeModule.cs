using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelFleet_TabUpgrade_TabShip_TabChangeModule : UITabBase
{
    // [HideInInspector] public SpaceFleet m_myFleet;
    // private SpaceShip m_selectedShip;
    // private ModuleBase m_selectedModule;

    // public TextMeshProUGUI m_textTop;
    // public TextMeshProUGUI m_textShipStats;

    // public Button m_upgradeModuleButton;
    // public Button m_backButton;

    // [SerializeField] private m_tabUpgradeModule;

    // // 부모 탭 시스템 참조
    // [HideInInspector] public TabSystem m_tabSystemParent;

    // public override void InitializeUITab()
    // {
    //     var character = DataManager.Instance.m_currentCharacter;
    //     if (character == null || character.GetOwnedFleet() == null)
    //     {
    //         m_textShipStats.text = "No character or No Fleet";
    //         return;
    //     }
    //     m_myFleet = character.GetOwnedFleet();

    //     m_upgradeModuleButton.onClick.AddListener(UpgradeModule);

    //     if (m_backButton != null)
    //         m_backButton.onClick.AddListener(OnBackButtonClicked);
    // }

    // private void OnBackButtonClicked()
    // {
    //     if (m_tabSystemParent != null)
    //         m_tabSystemParent.SwitchToTab(0);
    // }

    // public override void OnTabActivated()
    // {
    //     InitializeUI();

    //     // 함선 관리 모드로 전환
    //     CameraController.Instance.m_currentMode = ECameraControllerMode.Manage_Ship;

    //     EventManager.Subscribe_SpaceShipModuleSelected(OnModuleSelected);
    //     EventManager.Subscribe_ShipChange(OnShipChanged);

    //     if (m_selectedShip != null)
    //     {
    //         m_selectedShip.m_shipOutline.enabled = true;
    //         CameraController.Instance.SetTargetOfCameraController(m_selectedShip.transform);
    //     }
    // }

    // public override void OnTabDeactivated()
    // {
    //     InitializeUI();

    //     // 평소 카메라 모드로 전환
    //     CameraController.Instance.m_currentMode = ECameraControllerMode.Normal;

    //     EventManager.Unsubscribe_SpaceShipModuleSelected(OnModuleSelected);
    //     EventManager.Unsubscribe_ShipChange(OnShipChanged);

    //     if (m_selectedShip != null)
    //         m_selectedShip.m_shipOutline.enabled = false;
    // }

    // private void InitializeUI()
    // {
    //     if (m_myFleet != null)
    //         m_myFleet.ClearAllSelections();
        
    //     if( m_selectedShip == null)
    //         m_selectedShip = m_myFleet.m_ships[0];
        
    //     m_selectedModule = null;


    //     if (m_textTop != null)
    //         m_textTop.text = "Ship Management";

    //     UpdateShipStatsDisplay();
        
    // }

    
    // private void OnModuleSelected(SpaceShip ship, ModuleBase module)
    // {
    //     if( m_selectedShip != ship) return;
    //     if (module == null) return;
    //     if (m_myFleet == null) return;
        
    //     m_selectedModule = module;
    //     m_selectedShip.SetSelectedModule_SpaceShip(ship, module);

    //     UpdateShipStatsDisplay();

    // }

    // private void OnShipChanged()
    // {
    //     UpdateShipStatsDisplay();
    // }
    
    // private void UpdateShipStatsDisplay()
    // {
    //     if (m_textShipStats == null) return;

    //     SpaceShipStats statsOrg = m_selectedShip.m_spaceShipStatsOrg;
    //     SpaceShipStats statsCur = m_selectedShip.m_spaceShipStatsCur;
        
    //     string shipStatsText = $"=== SHIP STATS ===\n" +
    //                         $"Health: {statsCur.totalHealth:F0} / {statsOrg.totalHealth:F0}\n" +
    //                         $"Attack: {statsCur.totalAttackPower:F1} / {statsOrg.totalAttackPower:F1}\n" +
    //                         $"Speed: {statsCur.totalMovementSpeed:F1} / {statsOrg.totalMovementSpeed:F1}\n" +
    //                         $"Rotation: {statsCur.totalRotationSpeed:F1} / {statsOrg.totalRotationSpeed:F1}\n" +
    //                         $"Cargo: {statsCur.totalCargoCapacity:F0} / {statsOrg.totalCargoCapacity:F0}\n" +
    //                         $"Weapons: {statsCur.totalWeapons}\n" +
    //                         $"Engines: {statsCur.totalEngines}";
        
    //     m_textShipStats.text = shipStatsText;

    //     if (m_selectedModule == null) return;
    //     string moduleStatsCompareText = m_selectedModule.GetUpgradeComparisonText();
    //     m_textShipStats.text += "\n\n" + moduleStatsCompareText;

    // }

    // private void UpgradeModule()
    // {
        
    // }


}
