using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class UIPanelFleet_TabUpgrade_TabFleet : UITabBase
{
    [HideInInspector] public SpaceFleet m_myFleet;
    private SpaceShip m_selectedShip;
    private ModuleBase m_selectedModule;
    private SpaceShip m_previouslyFocusedShip;

    public TextMeshProUGUI m_textTop;
    public TextMeshProUGUI m_textFleetStats;

    public RectTransform scrollViewShipsContent;
    public GameObject scrollViewShipsItem;

    private GameObject m_addButtonItem; // Add 버튼 아이템 참조

    public UIPanelFleet_TabUpgrade_TabShip m_panelShipInfo;

    public override void InitializeUITab()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null || character.GetOwnedFleet() == null)
        {
            m_textFleetStats.text = "No character or No Fleet";
            return;
        }
        m_myFleet = character.GetOwnedFleet();

        if( scrollViewShipsItem != null)
        {
            for(int i = 0; i < m_myFleet.m_ships.Count; i++)
            {
                GameObject item = Instantiate(scrollViewShipsItem, scrollViewShipsContent);
                if( item != null)
                {
                    int index = i; // 클로저 문제 방지
                    SpaceShip ship = m_myFleet.m_ships[index];
                    item.GetComponent<ScrollViewShipsItem>().InitializeScrollViewShipsItem_SelectButton(ship.m_shipInfo.shipName, () => FocusCameraOnShip(ship));
                }                    
            }
            MakeAddShipButtonItem();
        }
    }

    public override void OnTabActivated()
    {
        InitializeUI();

        EventManager.Subscribe_FleetChange(OnFleetChanged);
    }

    public override void OnTabDeactivated()
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
        UpdateScrollViewShips();        
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
    private void UpdateScrollViewShips()
    {
        // 함선이 추가되었는지 확인
        if (m_myFleet != null && m_addButtonItem != null && scrollViewShipsContent != null)
        {
            // 현재 UI에 표시된 함선 수 (Add 버튼 제외)
            int currentShipItemCount = scrollViewShipsContent.childCount - 1;

            // 실제 함선 수와 비교
            if (m_myFleet.m_ships.Count > currentShipItemCount)
            {
                // 새 함선이 추가됨 - 기존 Add 버튼을 Select 버튼으로 변경
                SpaceShip newShip = m_myFleet.m_ships[^1]; // 마지막 함선

                // 기존 Add 버튼 아이템을 Select 버튼으로 재초기화
                if (m_addButtonItem.TryGetComponent<ScrollViewShipsItem>(out var scrollViewItem))
                {
                    scrollViewItem.InitializeScrollViewShipsItem_SelectButton(
                        newShip.m_shipInfo.shipName,
                        () => FocusCameraOnShip(newShip)
                    );

                    MakeAddShipButtonItem();
                }
            }
        }
    }
    private void MakeAddShipButtonItem()
    {
        if(m_myFleet.m_ships.Count >= DataManager.Instance.m_dataTableConfig.gameSettings.maxShipsPerFleet)
            return;
        // 새로운 Add 버튼 생성
        m_addButtonItem = Instantiate(scrollViewShipsItem, scrollViewShipsContent);
        if (m_addButtonItem != null)
        {
            var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;
            m_addButtonItem.GetComponent<ScrollViewShipsItem>().InitializeScrollViewShipsItem_AddButton(
                $"Add Ship cost, money: {gameSettings.shipAddMoneyCost}, mineral: {gameSettings.shipAddMineralCost} ",
                AddShip
            );
        }
    }

    private void FocusCameraOnShip(SpaceShip ship)
    {
        if (ship == null) return;

        // 이전에 포커스된 함선의 아웃라인 비활성화
        if (m_previouslyFocusedShip != null && m_previouslyFocusedShip != ship)
        {
            Outline prevOutline = m_previouslyFocusedShip.GetComponent<Outline>();
            if (prevOutline != null)
                prevOutline.enabled = false;
        }

        // 새로운 함선의 아웃라인 활성화
        Outline outline = ship.GetComponent<Outline>();
        if (outline != null)
            outline.enabled = true;

        m_previouslyFocusedShip = ship;
        m_panelShipInfo.m_selectedShip = ship;

        CameraController.Instance.SetTargetOfCameraController(ship.transform);
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
