using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelFleetInfo : MonoBehaviour
{
    [HideInInspector] public SpaceFleet m_myFleet;
    private SpaceShip m_selectedShip;
    private ModuleBase m_selectedModule;

    public TextMeshProUGUI m_textTop;
    public TextMeshProUGUI m_textFleetStats;

    public Button m_addShipButton;

    public void InitializeUIPanelFleetInfo()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null || character.GetOwnedFleet() == null)
        {
            m_textFleetStats.text = "No character or No Fleet";
            return;
        }
        m_myFleet = character.GetOwnedFleet();

        m_addShipButton.onClick.AddListener(AddShip);
    }

    public void OnTabActivated()
    {
        InitializeUI();

        EventManager.Subscribe_FleetChange(OnFleetChanged);
    }

    public void OnTabDeactivated()
    {
        InitializeUI();
        
        EventManager.Unsubscribe_FleetChange(OnFleetChanged);
    }



    private void InitializeUI()
    {
        if (m_myFleet != null)
            m_myFleet.ClearAllSelections();
        

        if (m_textTop != null)
             m_textTop.text = "Fleet Management";

        UpdateFleetStatsDisplay();
        
    }

    private void OnFleetChanged()
    {
        UpdateFleetStatsDisplay();
    }

    private void UpdateFleetStatsDisplay()
    {
        if (m_textFleetStats == null) return;

        var character = DataManager.Instance.m_currentCharacter;
        if (character == null || character.GetOwnedFleet() == null)
        {
            m_textFleetStats.text = "No fleet data available";
            return;
        }

        SpaceFleet fleet = character.GetOwnedFleet();
        SpaceShipStats statsOrg = fleet.GetTotalStats(false);
        SpaceShipStats statsCur = fleet.GetTotalStats(true);

        m_textFleetStats.text = $"=== FLEET STATS ===\n" +
                              $"Ships: {fleet.m_ships.Count}\n" +
                              $"Health: {statsCur.totalHealth:F0} / {statsOrg.totalHealth:F0}\n" +
                              $"Attack: {statsCur.totalAttackPower:F1} / {statsOrg.totalAttackPower:F1}\n" +
                              $"Speed: {statsCur.totalMovementSpeed:F1} / {statsOrg.totalMovementSpeed:F1}\n" +
                              $"Rotation: {statsCur.totalRotationSpeed:F1} / {statsOrg.totalRotationSpeed:F1}\n" +
                              $"Cargo: {statsCur.totalCargoCapacity:F0} / {statsOrg.totalCargoCapacity:F0}\n" +
                              $"Weapons: {statsCur.totalWeapons}\n" +
                              $"Engines: {statsCur.totalEngines}";
    }

    private void AddShip()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null)
        {
            // if (selectedModuleText != null)
            //     selectedModuleText.text = "No character data available";
            return;
        }

        if (character.CanAddShip() == false)
        {
            var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;
            string reason = "";

            if (character.GetOwnedFleet() == null)
                reason = "No fleet";
            else if (character.GetOwnedFleet().m_ships.Count >= gameSettings.maxShipsPerFleet)
                reason = $"Max ships reached ({gameSettings.maxShipsPerFleet})";
            else if (character.GetMoney() < gameSettings.shipAddMoneyCost)
                reason = $"Need {gameSettings.shipAddMoneyCost} money (have {character.GetMoney()})";
            else if (character.GetMineral() < gameSettings.shipAddMineralCost)
                reason = $"Need {gameSettings.shipAddMineralCost} mineral (have {character.GetMineral()})";

            // if (selectedModuleText != null)
            //      selectedModuleText.text = $"Cannot add ship: {reason}";
            return;
        }

        character.AddNewShip((success) =>
        {
            if (success)
            {
                //if (selectedModuleText != null)
                //     selectedModuleText.text = "New ship added to fleet!";
            }
            else
            {
                // if (selectedModuleText != null)
                //     selectedModuleText.text = "Failed to add ship";
            }
        });
    }
    


}
