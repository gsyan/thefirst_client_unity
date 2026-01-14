using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;




public class UIPanelFleet_TabUpgrade_TabFleet : UITabBase
{
    [HideInInspector] public SpaceFleet m_myFleet;
    private SpaceShip m_selectedShip;
    [SerializeField] private TextMeshProUGUI m_textTop;
    [SerializeField] private TextMeshProUGUI m_textFleetStats;
    [SerializeField] private RectTransform m_scrollViewShipsContent;
    [SerializeField] private GameObject m_scrollViewShipItem;       // 프리팹
    [SerializeField] private GameObject m_scrollViewShipItemAdd;    // 프리팹
    private GameObject m_addButtonItem;         // Add 버튼 아이템 참조
    private ScrollViewShipItem m_selectedScrollViewShipItem;    // 현재 선택된 스크롤 뷰 아이템
    [SerializeField] private UIPanelFleet_TabUpgrade_TabShip m_panelShipInfo;
    
    public override void InitializeUITab()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null || character.GetOwnedFleet() == null)
        {
            m_textFleetStats.text = "No character or No Fleet";
            return;
        }
        m_myFleet = character.GetOwnedFleet();

        if(m_scrollViewShipsContent != null && m_scrollViewShipItem != null)
        {
            for(int i = 0; i < m_myFleet.m_ships.Count; i++)
            {
                GameObject item = Instantiate(m_scrollViewShipItem, m_scrollViewShipsContent);
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

        if (m_selectedShip != null)
        {
            m_selectedShip.m_shipOutline.enabled = true;
            CameraController.Instance.SetTargetOfCameraController(m_selectedShip.transform);
        }
    }

    public override void OnTabDeactivated()
    {
        InitializeUI();

        EventManager.Unsubscribe_FleetChange(OnFleetChanged);

        if (m_selectedShip != null)
            m_selectedShip.m_shipOutline.enabled = false;
    }

    private void InitializeUI()
    {
        if (m_myFleet != null)
            m_myFleet.ClearAllSelectedModule();
        

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
        CapabilityProfile statsOrg = fleet.GetTotalStats(false);
        CapabilityProfile statsCur = fleet.GetTotalStats(true);

        m_textFleetStats.text = $"=== FLEET STATS ===\n" +
                              $"Ships: {fleet.m_ships.Count}\n" +
                              $"Att(dps): {statsCur.attackDps:F1} / {statsOrg.attackDps:F1}\n" +
                              $"HP: {statsCur.hp:F0} / {statsOrg.hp:F0}\n" +                              
                              $"Sp: {statsCur.engineSpeed:F1} / {statsOrg.engineSpeed:F1}\n" +
                              $"Car: {statsCur.cargoCapacity:F0} / {statsOrg.cargoCapacity:F0}\n" +
                              $"Weapons: {statsCur.totalWeapons}\n" +
                              $"Engines: {statsCur.totalEngines}";

                              
    }
    private void UpdateScrollViewShips()
    {
        // 함선이 추가되었는지 확인
        if (m_myFleet != null && m_addButtonItem != null && m_scrollViewShipsContent != null)
        {
            // 현재 UI에 표시된 함선 수 (Add 버튼 제외)
            int currentShipItemCount = m_scrollViewShipsContent.childCount - 1;

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
                GameObject shipItem = Instantiate(m_scrollViewShipItem, m_scrollViewShipsContent);
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
        m_addButtonItem = Instantiate(m_scrollViewShipItemAdd, m_scrollViewShipsContent);
        if (m_addButtonItem != null)
        {
            var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;
            CostStruct cost = gameSettings.GetAddShipCost(m_myFleet.m_ships.Count);

            // 비용 문자열 생성 (0이 아닌 광물만 표시)
            string costText = "Add Ship (";
            bool firstItem = true;

            if (cost.mineral > 0)
            {
                costText += $"Mineral: {cost.mineral}";
                firstItem = false;
            }
            if (cost.mineralRare > 0)
            {
                if (!firstItem) costText += ", ";
                costText += $"Rare: {cost.mineralRare}";
                firstItem = false;
            }
            if (cost.mineralExotic > 0)
            {
                if (!firstItem) costText += ", ";
                costText += $"Exotic: {cost.mineralExotic}";
                firstItem = false;
            }
            if (cost.mineralDark > 0)
            {
                if (!firstItem) costText += ", ";
                costText += $"Dark: {cost.mineralDark}";
            }
            costText += ")";

            m_addButtonItem.GetComponent<ScrollViewShipItemAdd>().InitializeScrollViewShipItemAdd(
                costText,
                AddShip
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
        if (selectedItem == m_selectedScrollViewShipItem) return;
        if (m_selectedShip == ship) return;

        // 이전에 선택된 아이템의 관리 버튼 숨김
        if (m_selectedScrollViewShipItem != null && m_selectedScrollViewShipItem != selectedItem)
            m_selectedScrollViewShipItem.SetSelected_ScrollViewShipItem(false);        
        
        // 이전에 포커스된 함선의 아웃라인 비활성화
        if (m_selectedShip != null)
            m_selectedShip.m_shipOutline.enabled = false;        
        
        // 선택 함선 업데이트
        m_selectedShip = ship;
        EventManager.TriggerSpaceShipSelected_TabUpgrade(m_selectedShip);

        // 선택 스크롤 뷰 아이템 업데이트
        m_selectedScrollViewShipItem = selectedItem;
        // 선택 함선의 아웃라인 활성화
        m_selectedShip.m_shipOutline.enabled = true;        
        // 카메라 포커스
        CameraController.Instance.SetTargetOfCameraController(m_selectedShip.transform);
    }

    private void AddShip()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;
        
        if (CanAddShip() != ServerErrorCode.SUCCESS)
        {
            // selectedModuleText.text = "Failed to add ship";
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
                character.UpdateMineral(response.data.costRemainInfo.remainMineral);
                character.UpdateMineralRare(response.data.costRemainInfo.remainMineralRare);
                character.UpdateMineralExotic(response.data.costRemainInfo.remainMineralExotic);
                character.UpdateMineralDark(response.data.costRemainInfo.remainMineralDark);
                DataManager.Instance.SetCharacterData(character.m_characterInfo);

                if (response.data.updatedFleetInfo != null)
                    DataManager.Instance.SetFleetData(response.data.updatedFleetInfo);

                if (response.data.newShipInfo != null && character.m_ownedFleet != null)
                {
                    ObjectManager.Instance.m_myFleet.CreateSpaceShipFromData(response.data.newShipInfo);
                    ObjectManager.Instance.m_myFleet.UpdateShipFormation(ObjectManager.Instance.m_myFleet.m_currentFormationType, false);
                }

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
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return ServerErrorCode.CLIENT_CanAddShip_CHARACTER_NOT_FOUND;

        var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;
        if (character.m_ownedFleet == null) return ServerErrorCode.FLEET_NOT_FOUND;
        int currentShipCount = character.m_ownedFleet.m_ships.Count;
        if (currentShipCount >= gameSettings.maxShipsPerFleet) return ServerErrorCode.CLIENT_CanAddShip_FLEET_MAX_SHIPS_REACHED;

        CostStruct cost = gameSettings.GetAddShipCost(currentShipCount);
        // tech 레벨 체크
        if( character.m_characterInfo.techLevel < cost.techLevel) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_TECH_LEVEL;
        // 모든 광물 타입 체크
        if (character.m_characterInfo.mineral < cost.mineral) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL;
        if (character.m_characterInfo.mineralRare < cost.mineralRare) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL_RARE;
        if (character.m_characterInfo.mineralExotic < cost.mineralExotic) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL_EXOTIC;
        if (character.m_characterInfo.mineralDark < cost.mineralDark) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL_DARK;

        return ServerErrorCode.SUCCESS;
    }


}
