using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelShipInfo : MonoBehaviour
{
    [HideInInspector] public SpaceFleet m_myFleet;
    private SpaceShip m_selectedShip;
    private ModuleBase m_selectedModule;

    public TextMeshProUGUI m_textTop;
    public TextMeshProUGUI m_textShipStats;

    public Button m_upgradeModuleButton;

    public void InitializeUIPanelShipInfo()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null || character.GetOwnedFleet() == null)
        {
            m_textShipStats.text = "No character or No Fleet";
            return;
        }
        m_myFleet = character.GetOwnedFleet();

        m_upgradeModuleButton.onClick.AddListener(UpgradeModule);
    }

    public void OnTabActivated()
    {
        InitializeUI();
        EventManager.Subscribe_ShipChange(OnShipChanged);
    }

    public void OnTabDeactivated()
    {
        InitializeUI();        
        EventManager.Unsubscribe_ShipChange(OnShipChanged);
    }

    private void InitializeUI()
    {
        

        if (m_myFleet != null)
            m_myFleet.ClearAllSelections();
        
        if( m_selectedShip == null)
            m_selectedShip = m_myFleet.m_ships[0];


        if (m_textTop != null)
            m_textTop.text = "Ship Management";

        UpdateShipStatsDisplay();
        
    }

    

    private void OnShipChanged()
    {
        UpdateShipStatsDisplay();
    }
    
    private void UpdateShipStatsDisplay()
    {
        if (m_textShipStats == null) return;

        if (m_selectedModule != null)
        {
            m_textShipStats.text = m_selectedModule.GetUpgradeComparisonText();
        }
        else
        {
            SpaceShipStats statsOrg = m_selectedShip.m_spaceShipStatsOrg;
            SpaceShipStats statsCur = m_selectedShip.m_spaceShipStatsCur;

            m_textShipStats.text = $"=== SHIP STATS ===\n" +
                                $"Health: {statsCur.totalHealth:F0} / {statsOrg.totalHealth:F0}\n" +
                                $"Attack: {statsCur.totalAttackPower:F1} / {statsOrg.totalAttackPower:F1}\n" +
                                $"Speed: {statsCur.totalMovementSpeed:F1} / {statsOrg.totalMovementSpeed:F1}\n" +
                                $"Rotation: {statsCur.totalRotationSpeed:F1} / {statsOrg.totalRotationSpeed:F1}\n" +
                                $"Cargo: {statsCur.totalCargoCapacity:F0} / {statsOrg.totalCargoCapacity:F0}\n" +
                                $"Weapons: {statsCur.totalWeapons}\n" +
                                $"Engines: {statsCur.totalEngines}";
        }

    }

    private void UpgradeModule()
    {
        
    }


}
