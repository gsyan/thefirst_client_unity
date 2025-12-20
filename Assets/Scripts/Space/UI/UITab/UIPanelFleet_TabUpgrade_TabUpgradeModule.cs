using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelFleet_TabUpgrade_TabUpgradeModule : UITabBase
{
    private SpaceFleet m_myFleet;
    private SpaceShip m_selectedShip;
    private ModuleBase m_selectedModule;

    public TextMeshProUGUI m_textTop;
    public TextMeshProUGUI m_moduleUpgradeStatsText;

    
    [SerializeField] private Button m_backButton;
    [SerializeField] private Button m_changeModuleButton;
    [SerializeField] private Button m_upgradeModuleButton;

    // 부모 탭 시스템 참조
    [HideInInspector] public TabSystem m_tabSystem_TabUpgrade;

    public override void InitializeUITab()
    {
        if (m_textTop != null)
            m_textTop.text = "Module Upgrade";

        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;
        
        m_myFleet = character.GetOwnedFleet();
        if (m_myFleet == null) return;

        m_backButton.onClick.AddListener(() => m_tabSystem_TabUpgrade.SwitchToTab(1));
        m_changeModuleButton.onClick.AddListener(() => m_tabSystem_TabUpgrade.SwitchToTab(3));
        m_upgradeModuleButton.onClick.AddListener(UpgradeModule);

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
        if( m_selectedShip == null)
            m_selectedShip = m_myFleet.m_ships[0];
        
        if (m_myFleet != null)
            m_myFleet.ClearAllSelectedModule();
        m_selectedModule = null;

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

    }

    private void OnShipChanged()
    {
        UpdateShipStatsDisplay();
    }
    
    private void UpdateShipStatsDisplay()
    {
        // if (m_moduleUpgradeStatsText == null) return;

        // SpaceShipStats statsOrg = m_selectedShip.m_spaceShipStatsOrg;
        // SpaceShipStats statsCur = m_selectedShip.m_spaceShipStatsCur;
        
        // string shipStatsText = $"=== SHIP STATS ===\n" +
        //                     $"Health: {statsCur.totalHealth:F0} / {statsOrg.totalHealth:F0}\n" +
        //                     $"Attack: {statsCur.totalAttackPower:F1} / {statsOrg.totalAttackPower:F1}\n" +
        //                     $"Speed: {statsCur.totalMovementSpeed:F1} / {statsOrg.totalMovementSpeed:F1}\n" +
        //                     $"Rotation: {statsCur.totalRotationSpeed:F1} / {statsOrg.totalRotationSpeed:F1}\n" +
        //                     $"Cargo: {statsCur.totalCargoCapacity:F0} / {statsOrg.totalCargoCapacity:F0}\n" +
        //                     $"Weapons: {statsCur.totalWeapons}\n" +
        //                     $"Engines: {statsCur.totalEngines}";
        
        // m_moduleUpgradeStatsText.text = shipStatsText;

        // if (m_selectedModule == null) return;
        // string moduleStatsCompareText = m_selectedModule.GetUpgradeComparisonText();
        // m_moduleUpgradeStatsText.text += "\n\n" + moduleStatsCompareText;

    }

    

    


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
