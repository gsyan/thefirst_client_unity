// 함대 탭 UI — 함대 스탯, 진형 선택, 함선 선택 그리드(ShipSelector)를 관리
// 함선 추가 버튼은 ShipSelector 그리드의 마지막 셀로 통합
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

    [Header("함선 선택 그리드")]
    [SerializeField] private RectTransform m_shipGridContainer1;             // GridLayoutGroup 부착
    [SerializeField] private RectTransform m_shipGridContainer2;             // GridLayoutGroup 부착
    [SerializeField] private RectTransform m_shipGridContainer3;             // GridLayoutGroup 부착
    [SerializeField] private GameObject m_shipSelectorPrefab;               // ShipSelector 프리팹
    [SerializeField] private Button m_addShipButton;                        // 그리드 마지막 셀(씬에서 배치)

    private Character m_myCharacter;
    private SpaceFleet m_myFleet;
    private ShipSelector m_selectedShipSelector;

    private readonly Dictionary<string, RowLabelValue> m_fleetStatRows = new();
    private readonly List<ScrollViewFormationItem> m_formationItemPool = new List<ScrollViewFormationItem>();
    private readonly List<ScrollViewFormationItem> m_formationItemActive = new List<ScrollViewFormationItem>();

    private readonly List<ShipSelector> m_shipSelectorPool = new();
    private readonly List<ShipSelector> m_shipSelectorActive = new();
    private Transform m_shipSelectorPoolHolder;

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

        var poolHolderGO = new GameObject("_ShipSelectorPool");
        poolHolderGO.transform.SetParent(transform, false);
        m_shipSelectorPoolHolder = poolHolderGO.transform;

        PopulateFormationScrollView();
        PopulateShipSelectorGrid();

        m_addShipButton.onClick.AddListener(OnAddShipButtonClicked);

        EventManager.Subscribe_AddShip(OnShipAdded);
        EventManager.Subscribe_FleetUpdateHP(OnFleetHPUpdated);
        EventManager.Subscribe_SpaceShipSelected(OnSpaceShipSelected);
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();

        UpdateFleetStatsDisplay();
        RefreshShipHealthDisplay();
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
    }

    // ── ShipSelector 그리드 ────────────────────────────────────────────

    private const int SHIPS_PER_ROW = 3;

    private void PopulateShipSelectorGrid()
    {
        if (m_shipSelectorPrefab == null || m_myFleet == null) return;

        // 활성 셀 전체 풀로 반환
        for (int i = 0; i < m_shipSelectorActive.Count; i++)
        {
            m_shipSelectorActive[i].gameObject.SetActive(false);
            m_shipSelectorActive[i].transform.SetParent(m_shipSelectorPoolHolder, false);
        }
        m_shipSelectorPool.AddRange(m_shipSelectorActive);
        m_shipSelectorActive.Clear();
        m_selectedShipSelector = null;

        int shipCount = m_myFleet.m_ships.Count;
        int maxShips  = DataManager.Instance.m_dataTableConfig.gameSettings.maxShipsPerFleet;
        bool atMax    = shipCount >= maxShips;

        // 추가 버튼 포함한 총 아이템 수로 행 표시 여부 결정
        int totalItems = shipCount + (atMax ? 0 : 1);
        if (m_shipGridContainer1 != null) m_shipGridContainer1.gameObject.SetActive(totalItems > 0);
        if (m_shipGridContainer2 != null) m_shipGridContainer2.gameObject.SetActive(totalItems > SHIPS_PER_ROW);
        if (m_shipGridContainer3 != null) m_shipGridContainer3.gameObject.SetActive(totalItems > SHIPS_PER_ROW * 2);

        for (int i = 0; i < shipCount; i++)
        {
            SpaceShip ship = m_myFleet.m_ships[i];
            RectTransform container = GetRowContainer(i);
            if (container == null) continue;

            ShipSelector selector = GetOrCreateShipSelector();
            selector.transform.SetParent(container, false);

            SpaceShip captured = ship;
            selector.Initialize(ship, () => OnShipSelectorClicked(captured));
            selector.gameObject.SetActive(true);
            m_shipSelectorActive.Add(selector);
        }

        // 추가 버튼: 다음 빈 슬롯의 행 마지막으로
        if (m_addShipButton != null)
        {
            if (atMax)
            {
                m_addShipButton.gameObject.SetActive(false);
            }
            else
            {
                RectTransform addContainer = GetRowContainer(shipCount);
                m_addShipButton.transform.SetParent(addContainer, false);
                m_addShipButton.gameObject.SetActive(true);
                m_addShipButton.transform.SetAsLastSibling();
            }
        }
    }

    // itemIndex 기준으로 어느 행 컨테이너에 배치할지 반환
    private RectTransform GetRowContainer(int itemIndex)
    {
        int row = itemIndex / SHIPS_PER_ROW;
        if (row == 0) return m_shipGridContainer1;
        if (row == 1) return m_shipGridContainer2;
        if (row == 2) return m_shipGridContainer3;
        return null;
    }

    private ShipSelector GetOrCreateShipSelector()
    {
        if (m_shipSelectorPool.Count > 0)
        {
            ShipSelector s = m_shipSelectorPool[^1];
            m_shipSelectorPool.RemoveAt(m_shipSelectorPool.Count - 1);
            return s;
        }
        var go = Instantiate(m_shipSelectorPrefab);
        return go.GetComponent<ShipSelector>();
    }

    private void RefreshShipHealthDisplay()
    {
        if (m_myFleet == null) return;

        // 함선 수 불일치 또는 Ship 참조 파괴(RebuildFleet/RestoreDestroyedShips 이후) 시 그리드 재구성
        bool needsRebuild = m_shipSelectorActive.Count != m_myFleet.m_ships.Count;
        if (needsRebuild == false)
        {
            for (int i = 0; i < m_shipSelectorActive.Count; i++)
            {
                if (m_shipSelectorActive[i].Ship == null)
                {
                    needsRebuild = true;
                    break;
                }
            }
        }

        if (needsRebuild == true)
        {
            PopulateShipSelectorGrid();
            return;
        }

        for (int i = 0; i < m_shipSelectorActive.Count; i++)
            m_shipSelectorActive[i].RefreshHealth();
    }

    private void OnShipSelectorClicked(SpaceShip ship)
    {
        if (m_selectedShipSelector != null)
            m_selectedShipSelector.SetSelected(false);

        for (int i = 0; i < m_shipSelectorActive.Count; i++)
        {
            if (m_shipSelectorActive[i].Ship == ship)
            {
                m_selectedShipSelector = m_shipSelectorActive[i];
                m_selectedShipSelector.SetSelected(true);
                break;
            }
        }

        // 카메라 타겟 지정 + UITabShip 자동 전환 (UIPanelSpace.OnShipSelectedAutoTabSwitch 구독 중)
        EventManager.Trigger_SpaceShipSelected(ship);
    }

    // ── 진형 스크롤뷰 ─────────────────────────────────────────────────

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

    // ── 함대 스탯 ─────────────────────────────────────────────────────

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
        SetOrCreateFleetStatRow("repair_power", $"{statsCur.repair_power:F0}/{statsOrg.repair_power:F0}");
        SetOrCreateFleetStatRow("aircraft_attack_power", $"{statsCur.aircraft_attack_power:F0}/{statsOrg.aircraft_attack_power:F0}");
        SetOrCreateFleetStatRow("aircraft_count", $"{statsCur.aircraft_count:F0}/{statsOrg.aircraft_count:F0}");
        SetOrCreateFleetStatRow("aircraft_launch_count", $"{statsCur.aircraft_launch_count:F0}/{statsOrg.aircraft_launch_count:F0}");
    }

    private void SetOrCreateFleetStatRow(string label, string value)
    {
        if (m_fleetStatsContainer == null || m_rowLabelValuePrefab == null)
            return;

        if (m_fleetStatRows.TryGetValue(label, out RowLabelValue existingRow))
        {
            existingRow.SetValues(value);
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

    // ── 이벤트 핸들러 ─────────────────────────────────────────────────

    // 다른 탭에서 함선이 바뀔 때 ShipSelector 선택 상태 동기화
    private void OnSpaceShipSelected(SpaceShip ship)
    {
        if (m_selectedShipSelector != null && m_selectedShipSelector.Ship == ship) return;

        if (m_selectedShipSelector != null)
            m_selectedShipSelector.SetSelected(false);

        m_selectedShipSelector = null;
        for (int i = 0; i < m_shipSelectorActive.Count; i++)
        {
            if (m_shipSelectorActive[i].Ship == ship)
            {
                m_selectedShipSelector = m_shipSelectorActive[i];
                m_selectedShipSelector.SetSelected(true);
                break;
            }
        }
    }

    private void OnShipAdded()
    {
        UpdateFleetStatsDisplay();
        PopulateShipSelectorGrid();
    }

    private void OnFleetHPUpdated()
    {
        UpdateFleetStatsDisplay();
        RefreshShipHealthDisplay();
    }

    // ── 함선 추가 ─────────────────────────────────────────────────────

    private void OnAddShipButtonClicked()
    {
        if (m_myCharacter == null) return;

        var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;
        CostStruct cost = gameSettings.GetAddShipCost(m_myFleet.m_ships.Count);

        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("fleet_add_ship"),
            LocalizationManager.Instance.Get("popup_message_add_ship"),
            null, null, cost,
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
        });
    }

    private ServerErrorCode CanAddShip()
    {
        if (m_myCharacter == null) return ServerErrorCode.CLIENT_CanAddShip_CHARACTER_NOT_FOUND;

        var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;
        if (m_myCharacter.m_ownedFleet == null) return ServerErrorCode.FLEET_NOT_FOUND;
        int currentShipCount = m_myCharacter.m_ownedFleet.m_ships.Count;
        if (currentShipCount >= gameSettings.maxShipsPerFleet) return ServerErrorCode.CLIENT_CanAddShip_FLEET_MAX_SHIPS_REACHED;

        CostStruct cost = gameSettings.GetAddShipCost(currentShipCount);
        if (m_myCharacter.m_characterInfo.techLevel < cost.techLevel) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_TECH_LEVEL;
        if (m_myCharacter.m_characterInfo.mineral < cost.mineral) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL;
        if (m_myCharacter.m_characterInfo.mineralRare < cost.mineralRare) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL_RARE;
        if (m_myCharacter.m_characterInfo.mineralExotic < cost.mineralExotic) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL_EXOTIC;
        if (m_myCharacter.m_characterInfo.mineralDark < cost.mineralDark) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL_DARK;

        return ServerErrorCode.SUCCESS;
    }
}
