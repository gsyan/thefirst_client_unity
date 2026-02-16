using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class UITabFleet : UITabBase
{
    [SerializeField] private TMP_Text  m_textFleetStatus;
    [SerializeField] private RectTransform m_fleetStatsContainer;           // VerticalLayoutGroup 필요
    [SerializeField] private GameObject m_rowLabelValuePrefab;              // RowLabelValue 프리팹
    [SerializeField] private RectTransform m_scrollViewFormationContent;
    [SerializeField] private GameObject m_scrollViewFormationItem;          // 프리팹    
    [SerializeField] private RectTransform m_rightContainer;                // VerticalLayoutGroup 필요
    [SerializeField] private Button m_addShipButton;

    private Character m_myCharacter;
    private SpaceFleet m_myFleet;
    private SpaceShip m_selectedShip;
    
    private readonly System.Collections.Generic.Dictionary<string, RowLabelValue> m_fleetStatRows = new();
    private readonly List<ScrollViewFormationItem> m_formationItemPool = new List<ScrollViewFormationItem>();
    private readonly List<ScrollViewFormationItem> m_formationItemActive = new List<ScrollViewFormationItem>();

    public override void InitializeUITab()
    {
        InitializeUITabFleet();
    }
    private void InitializeUITabFleet()
    {
        m_myCharacter = DataManager.Instance.m_currentCharacter;
        if (m_myCharacter == null || m_myCharacter.GetOwnedFleet() == null) return;
        m_myFleet = m_myCharacter.GetOwnedFleet();
        if (m_myFleet == null) return;

        PopulateFormationScrollView();

        m_addShipButton.onClick.AddListener(OnAddShipButtonClicked);

        EventManager.Subscribe_AddShip(UpdateFleetStatsDisplay);
        EventManager.Subscribe_FleetUpdateHP(UpdateFleetStatsDisplay);
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();

        UpdateFleetStatsDisplay();
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
    }

    private void PopulateFormationScrollView()
    {
        if (m_scrollViewFormationContent == null || m_scrollViewFormationItem == null) return;
        if (m_myFleet == null) return;

        // 활성 아이템 비활성화
        for (int i = 0; i < m_formationItemActive.Count; i++)
            m_formationItemActive[i].gameObject.SetActive(false);
        m_formationItemActive.Clear();

        int poolIndex = 0;
        var formationTypes = System.Enum.GetValues(typeof(EFormationType));
        foreach (EFormationType formationType in formationTypes)
        {
            ScrollViewFormationItem scrollViewItem;
            if (poolIndex < m_formationItemPool.Count)
            {
                scrollViewItem = m_formationItemPool[poolIndex];
                scrollViewItem.gameObject.SetActive(true);
            }
            else
            {
                var item = Instantiate(m_scrollViewFormationItem, m_scrollViewFormationContent);
                item.name = formationType.ToString();
                scrollViewItem = item.GetComponent<ScrollViewFormationItem>();
                m_formationItemPool.Add(scrollViewItem);
            }

            EFormationType captured = formationType;
            scrollViewItem.InitializeScrollViewFormationItem(
                () => OnFormationItemSelected(captured),
                formationType.ToString()
            );
            m_formationItemActive.Add(scrollViewItem);
            poolIndex++;
        }
    }

    private void OnFormationItemSelected(EFormationType formationType)
    {
        if (m_myFleet == null) return;
        m_myFleet.ChangeFormation(formationType);
    }

    private void UpdateFleetStatsDisplay()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null || character.GetOwnedFleet() == null)
            return;

        SpaceFleet fleet = character.GetOwnedFleet();
        CapabilityProfile statsOrg = fleet.GetFleetCapabilityProfile(false);
        CapabilityProfile statsCur = fleet.GetFleetCapabilityProfile(true);

        SetOrCreateFleetStatRow("fleet_ship_count", $"{fleet.m_ships.Count}");
        SetOrCreateFleetStatRow("attack_power", $"{statsCur.attack_power:F0}/{statsOrg.attack_power:F0}");
        SetOrCreateFleetStatRow("health_power", $"{statsCur.health_power:F0}/{statsOrg.health_power:F0}");
        SetOrCreateFleetStatRow("speed_power", $"{statsCur.speed_power:F0}/{statsOrg.speed_power:F0}");
        SetOrCreateFleetStatRow("cargo_power", $"{statsCur.cargo_capacity:F0}/{statsOrg.cargo_capacity:F0}");
        SetOrCreateFleetStatRow("repair_power", $"{statsCur.repair_power:F0}/{statsOrg.repair_power:F0}");
    }

    private void SetOrCreateFleetStatRow(string label, string value)
    {
        if (m_fleetStatsContainer == null || m_rowLabelValuePrefab == null)
            return;

        if (m_fleetStatRows.TryGetValue(label, out RowLabelValue existingRow))
        {
            existingRow.SetValue(value);
            return;
        }

        GameObject rowObj = Instantiate(m_rowLabelValuePrefab, m_fleetStatsContainer);
        rowObj.name = $"FleetRow_{label}";

        RowLabelValue row = rowObj.GetComponent<RowLabelValue>();
        if (row != null)
        {
            row.SetRow(label, value);
            m_fleetStatRows.Add(label, row);
        }
    }

    // Add 버튼 클릭 시 Confirm 팝업 표시
    private void OnAddShipButtonClicked()
    {
        if (m_myCharacter == null) return;

        var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;
        CostStruct cost = gameSettings.GetAddShipCost(m_myFleet.m_ships.Count);

        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("add_ship"),
            LocalizationManager.Instance.Get("popup_message_add_ship"),
            cost,
            AddShip
        );
    }

    private void AddShip()
    {
        if (m_myCharacter == null) return;

        ServerErrorCode errorCode = CanAddShip();
        if (errorCode != ServerErrorCode.SUCCESS)
        {
            ShowResultMessage($"{errorCode}", 3f);
            return;
        }

        // Request ship addition to server
        var request = new AddShipRequest
        {
            fleetId = null // Add to current active fleet
        };

        NetworkManager.Instance.AddShip(request, (response) =>
        {
            if (response.errorCode == 0)
            {
                m_myCharacter.UpdateMineral(response.data.costRemainInfo.remainMineral);
                m_myCharacter.UpdateMineralRare(response.data.costRemainInfo.remainMineralRare);
                m_myCharacter.UpdateMineralExotic(response.data.costRemainInfo.remainMineralExotic);
                m_myCharacter.UpdateMineralDark(response.data.costRemainInfo.remainMineralDark);
                DataManager.Instance.SaveCharacterInfoToPlayerPrefs();

                if (response.data.updatedFleetInfo != null)
                    DataManager.Instance.SetFleetData(response.data.updatedFleetInfo);

                if (response.data.newShipInfo != null && m_myCharacter.m_ownedFleet != null)
                    // smoothSpawn=true: 기함 뒤에서 스폰 후 진형으로 이동
                    ObjectManager.Instance.m_myFleet.CreateSpaceShipFromData(response.data.newShipInfo, true);
                
                EventManager.Trigger_AddShip();

            }
            else
            {

            }
        });
    }
        
    private ServerErrorCode CanAddShip()
    {
        if (m_myCharacter == null) return ServerErrorCode.CLIENT_CanAddShip_CHARACTER_NOT_FOUND;

        var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;
        if (m_myCharacter.m_ownedFleet == null) return ServerErrorCode.FLEET_NOT_FOUND;
        int currentShipCount = m_myCharacter.m_ownedFleet.m_ships.Count;
        if (currentShipCount >= gameSettings.m_maxShipsPerFleet) return ServerErrorCode.CLIENT_CanAddShip_FLEET_MAX_SHIPS_REACHED;

        CostStruct cost = gameSettings.GetAddShipCost(currentShipCount);
        // tech 레벨 체크
        if( m_myCharacter.m_characterInfo.techLevel < cost.techLevel) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_TECH_LEVEL;
        // 모든 광물 타입 체크
        if (m_myCharacter.m_characterInfo.mineral < cost.mineral) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL;
        if (m_myCharacter.m_characterInfo.mineralRare < cost.mineralRare) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL_RARE;
        if (m_myCharacter.m_characterInfo.mineralExotic < cost.mineralExotic) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL_EXOTIC;
        if (m_myCharacter.m_characterInfo.mineralDark < cost.mineralDark) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL_DARK;

        return ServerErrorCode.SUCCESS;
    }



}