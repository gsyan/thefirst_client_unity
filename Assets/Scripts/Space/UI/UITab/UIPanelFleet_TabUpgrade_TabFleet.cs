using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;


public class UIPanelFleet_TabUpgrade_TabFleet : UITabBase
{
    [HideInInspector] public SpaceFleet m_myFleet;
    private SpaceShip m_selectedShip;
    private ModuleBase m_selectedModule;
    private SpaceShip m_focusedShip;

    public TextMeshProUGUI m_textTop;
    public TextMeshProUGUI m_textFleetStats;

    public RectTransform scrollViewShipsContent;
    public GameObject scrollViewShipItem;
    public GameObject scrollViewShipItemAdd;

    private GameObject m_addButtonItem; // Add 버튼 아이템 참조
    private ScrollViewShipItem m_selectedShipItem; // 현재 선택된 아이템

    public UIPanelFleet_TabUpgrade_TabShip m_panelShipInfo;

    // 부모 탭 시스템 참조
    [HideInInspector] public TabSystem m_tabSystemParent;

    public override void InitializeUITab()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null || character.GetOwnedFleet() == null)
        {
            m_textFleetStats.text = "No character or No Fleet";
            return;
        }
        m_myFleet = character.GetOwnedFleet();

        if(scrollViewShipItem != null)
        {
            for(int i = 0; i < m_myFleet.m_ships.Count; i++)
            {
                GameObject item = Instantiate(scrollViewShipItem, scrollViewShipsContent);
                if( item != null)
                {
                    int index = i; // 클로저 문제 방지
                    SpaceShip ship = m_myFleet.m_ships[index];
                    ScrollViewShipItem scrollViewItem = item.GetComponent<ScrollViewShipItem>();
                    scrollViewItem.InitializeScrollViewShipItem(
                        ship.m_shipInfo.shipName,
                        () => OnShipItemSelected(scrollViewItem, ship),
                        () => OnManageShipClicked(ship)
                    );
                }                    
            }
            MakeAddShipButtonItem();
        }
    }

    public override void OnTabActivated()
    {
        InitializeUI();

        EventManager.Subscribe_FleetChange(OnFleetChanged);

        if (m_focusedShip != null)
        {
            m_focusedShip.m_shipOutline.enabled = true;
            CameraController.Instance.SetTargetOfCameraController(m_focusedShip.transform);
        }
    }

    public override void OnTabDeactivated()
    {
        InitializeUI();

        EventManager.Unsubscribe_FleetChange(OnFleetChanged);

        if (m_focusedShip != null)
            m_focusedShip.m_shipOutline.enabled = false;
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
                // 새 함선이 추가됨
                SpaceShip newShip = m_myFleet.m_ships[^1]; // 마지막 함선

                // 1. 기존 Add 버튼 삭제
                if (m_addButtonItem != null)
                {
                    Destroy(m_addButtonItem);
                    m_addButtonItem = null;
                }

                // 2. 새로운 함선 아이템 추가
                GameObject shipItem = Instantiate(scrollViewShipItem, scrollViewShipsContent);
                if (shipItem != null)
                {
                    ScrollViewShipItem scrollViewItem = shipItem.GetComponent<ScrollViewShipItem>();
                    scrollViewItem.InitializeScrollViewShipItem(
                        newShip.m_shipInfo.shipName,
                        () => OnShipItemSelected(scrollViewItem, newShip),
                        () => OnManageShipClicked(newShip)
                    );
                }

                // 3. 새로운 Add 버튼 추가
                MakeAddShipButtonItem();
            }
        }
    }
    private void MakeAddShipButtonItem()
    {
        if(m_myFleet.m_ships.Count >= DataManager.Instance.m_dataTableConfig.gameSettings.maxShipsPerFleet)
            return;
        // 새로운 Add 버튼 생성
        m_addButtonItem = Instantiate(scrollViewShipItemAdd, scrollViewShipsContent);
        if (m_addButtonItem != null)
        {
            var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;
            m_addButtonItem.GetComponent<ScrollViewShipItemAdd>().InitializeScrollViewShipItemAdd(
                $"Add Ship cost, money: {gameSettings.shipAddMoneyCost}, mineral: {gameSettings.shipAddMineralCost} "
                , AddShip
            );
        }
    }

    private void OnManageShipClicked(SpaceShip ship)
    {
        if (m_tabSystemParent != null)
            m_tabSystemParent.SwitchToTab(1);
    }

    private void OnShipItemSelected(ScrollViewShipItem selectedItem, SpaceShip ship)
    {
        if (selectedItem == null || ship == null) return;
        if (selectedItem == m_selectedShipItem) return;
        if (m_focusedShip == ship) return;

        // 이전에 선택된 아이템의 관리 버튼 숨김
        if (m_selectedShipItem != null && m_selectedShipItem != selectedItem)
            m_selectedShipItem.SetSelected_ScrollViewShipItem(false);            
        // 현재 선택된 아이템 업데이트
        m_selectedShipItem = selectedItem;

        // 이전에 포커스된 함선의 아웃라인 비활성화
        if (m_focusedShip != null)
            m_focusedShip.m_shipOutline.enabled = false;
        m_focusedShip = ship;

        // 새로운 함선의 아웃라인 활성화
        ship.m_shipOutline.enabled = true;

        m_panelShipInfo.m_selectedShip = ship;

        // 카메라 포커스
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
