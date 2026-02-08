using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class UIPanelFleet_TabUpgrade : UITabBase
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

        // 기존 아이템 제거
        for (int i = m_scrollViewFormationContent.childCount - 1; i >= 0; i--)
            Destroy(m_scrollViewFormationContent.GetChild(i).gameObject);

        var formationTypes = System.Enum.GetValues(typeof(EFormationType));
        foreach (EFormationType formationType in formationTypes)
        {
            GameObject item = Instantiate(m_scrollViewFormationItem, m_scrollViewFormationContent);
            if (item == null) continue;

            item.name = formationType.ToString();
            ScrollViewFormationItem scrollViewItem = item.GetComponent<ScrollViewFormationItem>();
            if (scrollViewItem == null) continue;

            EFormationType captured = formationType;
            scrollViewItem.InitializeScrollViewFormationItem(
                () => OnFormationItemSelected(captured),
                formationType.ToString()
            );
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

        SetOrCreateFleetStatRow("ship", $"{fleet.m_ships.Count}");
        SetOrCreateFleetStatRow("attack_power", $"{statsCur.attackDps:F1} / {statsOrg.attackDps:F1}");
        SetOrCreateFleetStatRow("health_power", $"{statsCur.hp:F0} / {statsOrg.hp:F0}");
        SetOrCreateFleetStatRow("speed_power", $"{statsCur.engineSpeed:F1} / {statsOrg.engineSpeed:F1}");
        SetOrCreateFleetStatRow("cargo_power", $"{statsCur.cargoCapacity:F0} / {statsOrg.cargoCapacity:F0}");
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
            "Add Ship",
            "Do you want to add a new ship to your fleet?",
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

                EventManager.TriggerFleetChange();

                // selectedModuleText.text = "Success add ship";
            }
            else
            {
                // selectedModuleText.text = "Failed to add ship";
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