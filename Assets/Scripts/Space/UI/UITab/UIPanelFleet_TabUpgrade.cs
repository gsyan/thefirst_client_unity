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

    // Tab references
    private UIPanelFleet_TabUpgrade_TabFleet m_tabFleet;
    private UIPanelFleet_TabUpgrade_TabShip m_tabShip;


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
                if (tabBase is UIPanelFleet_TabUpgrade_TabFleet fleetTab)
                {
                    m_tabFleet = fleetTab;
                    m_tabFleet.m_tabSystem_TabUpgrade = m_tabSystem;
                }
                else if (tabBase is UIPanelFleet_TabUpgrade_TabShip shipTab)
                {
                    m_tabShip = shipTab;
                    m_tabShip.m_tabSystem_TabUpgrade = m_tabSystem;
                }

                // 탭 버튼 숨기기
                if (tabData.tabButton != null)
                    tabData.tabButton.gameObject.SetActive(false);
            }
        }

    }

    public override void OnTabActivated()
    {
        InitializeUI();

        m_tabSystem.ForceActivateTab();
    }

    public override void OnTabDeactivated()
    {
        InitializeUI();

        m_tabSystem.ForceDeactivateTab();
    }

    private void InitializeUI()
    {
        // if (panelShipInfo != null)
        //     panelShipInfo.SetActive(false);

        // if (selectedModuleText != null)
        //     selectedModuleText.text = "Click on a module to select it";

        if (m_myFleet != null)
            m_myFleet.ClearAllSelections();
        
        //UpdateFleetStatsDisplay();
    }
    
    

    public void OnModuleSelected(SpaceShip ship, ModuleBase moduleBase)
    {
        m_selectedShip = ship;
        m_selectedModule = moduleBase;

        UpdatePanelShipInfo();
        //UpdateFleetStatsDisplay();

        // if (selectedModuleText != null)
        // {
        //     string moduleInfo = GetModuleDisplayInfo(ship, moduleBase);
        //     selectedModuleText.text = moduleInfo;
        // }
    }

    private void UpdatePanelShipInfo()
    {
        //if (m_tabShipInfo == null || m_selectedShip == null) return;

        // panelShipInfo.SetActive(true);

        // // Update ship information
        // if (moduleNameText != null)
        //     moduleNameText.text = m_selectedShip.name;

        // if (moduleStatsText != null)
        // {
        //     if (m_selectedModule != null)
        //     {
        //         moduleStatsText.text = m_selectedModule.GetUpgradeComparisonText();
        //     }
        //     else
        //     {
        //         var stats = m_selectedShip.GetTotalStats();
        //         moduleStatsText.text = stats.ToString();
        //     }
        // }

        // UpdateUpgradeButton();
    }


    private void RequestUpgrade()
    {
        if (m_selectedModule == null || m_selectedShip == null) return;

        // Validate resources and upgrade availability
        if (!CanUpgrade(out string validationMessage))
        {
            Debug.LogWarning($"Upgrade blocked: {validationMessage}");
            // if (selectedModuleText != null)
            //     selectedModuleText.text = $"Upgrade failed: {validationMessage}";
            return;
        }

        string partsInfo = GetPartsUpgradeInfo(m_selectedModule);
        Debug.Log($"Requesting upgrade for {partsInfo} on ship {m_selectedShip.name}");

        // Create upgrade request
        var upgradeRequest = CreateUpgradeRequest(m_selectedModule);

        // Send upgrade request to server
        NetworkManager.Instance.UpgradeModule(upgradeRequest, OnUpgradeResponse);
    }
    
    private void OnUpgradeResponse(ApiResponse<ModuleUpgradeResponse> response)
    {
        Character character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;
        
        if (response.errorCode == 0 && response.data.success)
        {
            if (response.data.costRemainInfo != null)
            {
                var characterInfo = DataManager.Instance.m_currentCharacter.GetInfo();
                character.UpdateMineral(response.data.costRemainInfo.remainMineral);
                character.UpdateMineralRare(response.data.costRemainInfo.remainMineralRare);
                character.UpdateMineralExotic(response.data.costRemainInfo.remainMineralExotic);
                character.UpdateMineralDark(response.data.costRemainInfo.remainMineralDark);
                DataManager.Instance.SetCharacterData(characterInfo);
            }

            // Update local data
            if (m_selectedModule != null)
            {
                m_selectedModule.SetModuleLevel(response.data.newLevel);
                // Update stats if provided
                if (response.data.newStats != null)
                {
                    m_selectedModule.m_health = response.data.newStats.health;
                }
            }
            
            // Refresh UI
            UpdatePanelShipInfo();
            //UpdateFleetStatsDisplay();

            // Show success message
            // if (selectedModuleText != null)
            //     selectedModuleText.text = $"Upgrade successful! {response.data.message}";
        }
        else
        {
            string errorMessage = response.data?.message ?? response.errorMessage ?? "Upgrade failed";
            Debug.LogError($"Upgrade failed: {errorMessage}");
            
            // Show error message
            // if (selectedModuleText != null)
            //     selectedModuleText.text = $"Upgrade failed: {errorMessage}";
        }
    }
    
    private string GetModuleDisplayInfo(SpaceShip ship, ModuleBase moduleBase)
    {
        if (moduleBase == null)
            return $"Selected: {ship.name}";

        if (moduleBase is ModuleBody body)
            return $"Selected: {ship.name} - Body[{body.m_moduleBodyInfo.bodyIndex}]";
        else if (moduleBase is ModuleWeapon weapon)
            return $"Selected: {ship.name} - Weapon[{weapon.m_classId}]";
        else if (moduleBase is ModuleEngine engine)
            return $"Selected: {ship.name} - Engine[{engine.m_classId}]";
        else
            return $"Selected: {ship.name} - {moduleBase.GetType().Name}";
    }


    private string GetPartsUpgradeInfo(ModuleBase moduleBase)
    {
        if (moduleBase is ModuleBody body)
            return $"ModuleBody[{body.m_moduleBodyInfo.bodyIndex}]";
        else if (moduleBase is ModuleWeapon weapon)
            return $"ModuleWeapon[{weapon.m_classId}]";
        else if (moduleBase is ModuleEngine engine)
            return $"ModuleEngine[{engine.m_classId}]";
        else
            return $"{moduleBase.GetType().Name}[{moduleBase.m_classId}]";
    }

    private ModuleUpgradeRequest CreateUpgradeRequest(ModuleBase moduleBase)
    {
        var upgradeRequest = new ModuleUpgradeRequest
        {
            shipId = m_selectedShip.m_shipInfo.id
            ,currentLevel = moduleBase.GetModuleLevel()
            ,targetLevel = moduleBase.GetModuleLevel() + 1
            ,bodyIndex = moduleBase.GetModuleBodyIndex()
            ,moduleType = moduleBase.GetModuleType().ToString()
        };

        return upgradeRequest;
    }

    private bool CanUpgrade(out string validationMessage)
    {
        validationMessage = "";

        if (m_selectedModule == null)
        {
            validationMessage = "No module selected";
            return false;
        }

        var upgradeStats = DataManager.Instance.RestoreModuleDataByType(m_selectedModule.GetPackedModuleType(), m_selectedModule.GetModuleLevel() + 1);
        if (upgradeStats == null)
        {
            validationMessage = "Max level reached";
            return false;
        }

        var character = DataManager.Instance.m_currentCharacter;
        if (character == null)
        {
            validationMessage = "Character data not available";
            return false;
        }

        CostStruct cost;
        if (DataManager.Instance.GetModuleUpgradeCost(m_selectedModule.GetPackedModuleType(), m_selectedModule.GetModuleLevel(), out cost) == false)
        {
            validationMessage = "Failed to get upgrade cost";
            return false;
        }

        int playerTechLevel = character.GetTechLevel();
        long playerMineral = character.GetMineral();
        long playerMineralRare = character.GetMineralRare();
        long playerMineralExotic = character.GetMineralExotic();
        long playerMineralDark = character.GetMineralDark();
    
        if (playerTechLevel < cost.techLevel)
        {
            validationMessage = $"Insufficient tech level (need {cost.techLevel} tech level, current {playerTechLevel})";
            return false;
        }
        if (playerMineral < cost.mineral)
        {
            validationMessage = $"Insufficient mineral (need {cost.mineral}, have {playerMineral})";
            return false;
        }
        if (playerMineralRare < cost.mineralRare)
        {
            validationMessage = $"Insufficient mineralRare (need {cost.mineralRare}, have {playerMineralRare})";
            return false;
        }
        if (playerMineralExotic < cost.mineralExotic)
        {
            validationMessage = $"Insufficient mineralExotic (need {cost.mineralExotic}, have {playerMineralExotic})";
            return false;
        }
        if (playerMineralDark < cost.mineralDark)
        {
            validationMessage = $"Insufficient mineralDark (need {cost.mineralDark}, have {playerMineralDark})";
            return false;
        }

        return true;
    }

    private void UpdateUpgradeButton()
    {
        // if (upgradeButton == null) return;

        // bool canUpgrade = CanUpgrade(out string validationMessage);

        // // Enable/disable button based on validation
        // upgradeButton.interactable = canUpgrade;

        // // Change button color to indicate state
        // var buttonImage = upgradeButton.GetComponent<Image>();
        // if (buttonImage != null)
        // {
        //     buttonImage.color = canUpgrade ? Color.white : Color.gray;
        // }

        // // Update button text if it has a Text component
        // var buttonText = upgradeButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        // if (buttonText != null)
        // {
        //     if (canUpgrade)
        //     {
        //         buttonText.text = "Upgrade";
        //     }
        //     else
        //     {
        //         buttonText.text = $"Cannot Upgrade ({validationMessage})";
        //     }
        // }
    }

    // private void OnResourceChanged(ResourceChangeEvent resourceEvent)
    // {
    //     UpdateUpgradeButton();
    // }

    
    
}