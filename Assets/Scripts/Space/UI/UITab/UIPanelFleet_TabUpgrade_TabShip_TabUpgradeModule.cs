using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelFleet_TabUpgrade_TabShip_TabUpgradeModule : UITabBase
{
    [HideInInspector] public SpaceFleet m_myFleet;
    // private SpaceShip m_selectedShip;
    // private ModuleBase m_selectedModule;

    // public TextMeshProUGUI m_textTop;
    public TextMeshProUGUI m_moduleUpgradeStatsText;

    [SerializeField] private Button m_upgradeModuleButton;
    [SerializeField] private Button m_changeModuleButton;
    // public Button m_backButton;

    // [SerializeField] private m_tabUpgradeModule;

    // 부모 탭 시스템 참조
    [HideInInspector] public TabSystem m_tabSystem_TabUpgrade_TabShip;

    public override void InitializeUITab()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;
        
        m_myFleet = character.GetOwnedFleet();
        if (m_myFleet == null) return;

        m_upgradeModuleButton.onClick.AddListener(UpgradeModule);
        m_changeModuleButton.onClick.AddListener(() => m_tabSystem_TabUpgrade_TabShip.SwitchToTab(2));


    }

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

    

    


    private void UpgradeModule()
    {
        // if (m_selectedModule == null || m_selectedShip == null) return;

        // // Validate resources and upgrade availability
        // if (!CanUpgrade(out string validationMessage))
        // {
        //     Debug.LogWarning($"Upgrade blocked: {validationMessage}");
        //     // if (selectedModuleText != null)
        //     //     selectedModuleText.text = $"Upgrade failed: {validationMessage}";
        //     return;
        // }

        // string partsInfo = GetPartsUpgradeInfo(m_selectedModule);
        // Debug.Log($"Requesting upgrade for {partsInfo} on ship {m_selectedShip.name}");

        // // Create upgrade request
        // var upgradeRequest = new ModuleUpgradeRequest
        // {
        //     shipId = m_selectedShip.m_shipInfo.id
        //     ,currentLevel = moduleBase.GetModuleLevel()
        //     ,targetLevel = moduleBase.GetModuleLevel() + 1
        //     ,bodyIndex = moduleBase.GetModuleBodyIndex()
        //     ,moduleType = moduleBase.GetModuleType().ToString()
        // };

        // // Send upgrade request to server
        // NetworkManager.Instance.UpgradeModule(upgradeRequest, OnUpgradeResponse);
    }
    
    private void OnUpgradeResponse(ApiResponse<ModuleUpgradeResponse> response)
    {
        // Character character = DataManager.Instance.m_currentCharacter;
        // if (character == null) return;
        
        // if (response.errorCode == 0 && response.data.success)
        // {
        //     if (response.data.costRemainInfo != null)
        //     {
        //         var characterInfo = DataManager.Instance.m_currentCharacter.GetInfo();
        //         character.UpdateMineral(response.data.costRemainInfo.remainMineral);
        //         character.UpdateMineralRare(response.data.costRemainInfo.remainMineralRare);
        //         character.UpdateMineralExotic(response.data.costRemainInfo.remainMineralExotic);
        //         character.UpdateMineralDark(response.data.costRemainInfo.remainMineralDark);
        //         DataManager.Instance.SetCharacterData(characterInfo);
        //     }

        //     // Update local data
        //     if (m_selectedModule != null)
        //     {
        //         m_selectedModule.SetModuleLevel(response.data.newLevel);
        //         // Update stats if provided
        //         if (response.data.newStats != null)
        //         {
        //             m_selectedModule.m_health = response.data.newStats.health;
        //         }
        //     }
            
        //     // Refresh UI
        //     UpdatePanelShipInfo();
        //     //UpdateFleetStatsDisplay();

        //     // Show success message
        //     // if (selectedModuleText != null)
        //     //     selectedModuleText.text = $"Upgrade successful! {response.data.message}";
        // }
        // else
        // {
        //     string errorMessage = response.data?.message ?? response.errorMessage ?? "Upgrade failed";
        //     Debug.LogError($"Upgrade failed: {errorMessage}");
            
        //     // Show error message
        //     // if (selectedModuleText != null)
        //     //     selectedModuleText.text = $"Upgrade failed: {errorMessage}";
        // }
    }

}
