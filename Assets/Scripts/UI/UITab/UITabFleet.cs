// 함대 탭 UI — Fleet Stats(2행 압축), 함선 선택 그리드, Formation 하단 바 + 교체 팝업 관리
// 함선 추가 버튼은 ShipSelector 그리드의 마지막 셀로 통합
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NUnit.Framework.Constraints;

public class UITabFleet : UITabBase
{
    [Header("Fleet Stats (상단 2행)")]
    // 1행: ATK / HP / SPD / REPAIR — 아이콘은 추후 TMP Sprite로 교체
    [SerializeField] private TMP_Text m_textFleetStats1;
    // 2행: 함재기 능력 — aircraft_count == 0 이면 숨김
    [SerializeField] private TMP_Text m_textFleetStats2;

    [Header("함선 선택 그리드")]
    [SerializeField] private RectTransform m_shipGridContainer1;
    [SerializeField] private RectTransform m_shipGridContainer2;
    [SerializeField] private RectTransform m_shipGridContainer3;
    [SerializeField] private GameObject m_shipSelectorPrefab;
    [SerializeField] private Button m_addShipButton;
    [SerializeField] private TMP_Text m_textAddShipCost;    // + 버튼에 표시할 비용 텍스트

    [Header("함선 액션 버튼 (선택 시 활성)")]
    [SerializeField] private Button m_btnShipManage;    // 함선 관리 탭으로 이동
    [SerializeField] private Button m_btnShipRepair;    // 집중 수리 (추후 구현)

    [Header("Formation 하단 바")]
    [SerializeField] private TMP_Text m_textCurrentFormation;   // 현재 진형명
    [SerializeField] private Button m_btnFormationChange;       // [교체 ▶] 버튼

    private Character m_myCharacter;
    private SpaceFleet m_myFleet;
    private ShipSelector m_selectedShipSelector;

    private readonly List<ShipSelector> m_shipSelectorPool = new();
    private readonly List<ShipSelector> m_shipSelectorActive = new();
    private Transform m_shipSelectorPoolHolder;

    private Vector2 m_shipButtonSpacing = new Vector2(8f, 8f);

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

        m_addShipButton.onClick.AddListener(OnAddShipButtonClicked);
        m_btnFormationChange.onClick.AddListener(OnFormationChangeClicked);

        if (m_btnShipManage != null) m_btnShipManage.onClick.AddListener(OnShipManageClicked);
        if (m_btnShipRepair != null) m_btnShipRepair.onClick.AddListener(OnShipRepairClicked);

        PopulateShipSelectorGrid();
        UpdateShipActionButtons();

        EventManager.Subscribe_AddShip(OnShipAdded);
        EventManager.Subscribe_FleetUpdateHP(OnFleetHPUpdated);
        EventManager.Subscribe_SpaceShipSelected(OnSpaceShipSelected);
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        UpdateFleetStatsDisplay();
        UpdateCurrentFormationText();
        RefreshShipHealthDisplay();
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
    }

    // ── Fleet Stats ────────────────────────────────────────────────────

    private void UpdateFleetStatsDisplay()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null || character.GetOwnedFleet() == null) return;

        SpaceFleet fleet = character.GetOwnedFleet();
        CapabilityProfile statsOrg = fleet.GetFleetCapabilityProfile(false);
        CapabilityProfile statsCur = fleet.GetFleetCapabilityProfile(true);

        // 1행: 함선 전투력 합산 (아이콘 플레이스홀더 — 추후 TMP Sprite 태그로 교체)
        if (m_textFleetStats1 != null)
            m_textFleetStats1.text =
                $"ATK {statsCur.attack_power:F0}  " +
                $"HP {statsCur.health_power:F0}/{statsOrg.health_power:F0}  " +
                $"SPD {statsCur.speed_power:F0}  " +
                $"REP {statsCur.repair_power:F0}";

        // 2행: 함재기 (보유 시만 표시)
        if (m_textFleetStats2 != null)
        {
            bool hasAircraft = statsOrg.aircraft_count > 0;
            m_textFleetStats2.gameObject.SetActive(hasAircraft);
            if (hasAircraft)
                m_textFleetStats2.text =
                    $"AIR-ATK {statsCur.aircraft_attack_power:F0}  " +
                    $"AIR {statsCur.aircraft_count:F0}/{statsOrg.aircraft_count:F0}  " +
                    $"LAUNCH {statsCur.aircraft_launch_count:F0}";
        }
    }

    // ── Formation ──────────────────────────────────────────────────────

    private void OnFormationChangeClicked()
    {
        if (m_myFleet == null) return;
        UIManager.Instance.ShowFormationPopup(m_myFleet.m_currentFormationType, OnFormationSelected);
    }

    private void OnFormationSelected(EFormationType formationType)
    {
        if (m_myFleet == null) return;
        m_myFleet.ChangeFormation(formationType);
        // 낙관적 업데이트 — 서버 응답 전에 UI 먼저 갱신
        if (m_textCurrentFormation != null)
            m_textCurrentFormation.text = LocalizationManager.Instance.Get(formationType.ToString());
    }

    private void UpdateCurrentFormationText()
    {
        if (m_textCurrentFormation == null || m_myFleet == null) return;
        m_textCurrentFormation.text = LocalizationManager.Instance.Get(m_myFleet.m_currentFormationType.ToString());
    }

    // ── ShipSelector 그리드 ────────────────────────────────────────────

    private const int SHIPS_PER_ROW = 3;

    private void PopulateShipSelectorGrid()
    {
        if (m_shipSelectorPrefab == null || m_myFleet == null) return;

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
            SetCellAnchor(selector.GetComponent<RectTransform>(), i % SHIPS_PER_ROW);

            SpaceShip captured = ship;
            selector.Initialize(ship, () => OnShipSelectorClicked(captured));
            selector.gameObject.SetActive(true);
            m_shipSelectorActive.Add(selector);
        }

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
                SetCellAnchor(m_addShipButton.GetComponent<RectTransform>(), shipCount % SHIPS_PER_ROW);
                m_addShipButton.gameObject.SetActive(true);
                m_addShipButton.transform.SetAsLastSibling();

                if (m_textAddShipCost != null)
                {
                    CostStruct cost = DataManager.Instance.m_dataTableConfig.gameSettings.GetAddShipCost(shipCount);
                    m_textAddShipCost.text = $"M {CommonUtility.FormatBigNumber(cost.mineral)}";
                }
            }
        }
    }

    private void SetCellAnchor(RectTransform rt, int indexInRow)
    {
        float xMin = indexInRow / (float)SHIPS_PER_ROW;
        float xMax = (indexInRow + 1) / (float)SHIPS_PER_ROW;
        rt.anchorMin = new Vector2(xMin, 0f);
        rt.anchorMax = new Vector2(xMax, 1f);
        rt.offsetMin = new Vector2(m_shipButtonSpacing.x, m_shipButtonSpacing.y);
        rt.offsetMax = new Vector2(-m_shipButtonSpacing.x, -m_shipButtonSpacing.y);
    }

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

    private void UpdateShipActionButtons()
    {
        bool hasSelection = m_selectedShipSelector != null;
        if (m_btnShipManage != null) m_btnShipManage.interactable = hasSelection;
        if (m_btnShipRepair != null) m_btnShipRepair.interactable = hasSelection;
    }

    private void OnShipManageClicked()
    {
        m_tabSystemParent.SwitchToTabByName("tab_ship");
    }

    private void OnShipRepairClicked()
    {
        // TODO: 집중 수리 구현
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

        UpdateShipActionButtons();

        // 카메라 타겟 지정
        EventManager.Trigger_SpaceShipSelected(ship);
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────────────────

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

        UpdateShipActionButtons();
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
            LocalizationManager.Instance.Get("fleet_add_ship_name"),
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

        var request = new AddShipRequest { fleetId = null };

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
        var techLevel = m_myCharacter.GetTechLevel();
        if (techLevel < cost.techLevel) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_TECH_LEVEL;
        if (m_myCharacter.m_characterInfo.mineral < cost.mineral) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL;
        if (m_myCharacter.m_characterInfo.mineralRare < cost.mineralRare) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL_RARE;
        if (m_myCharacter.m_characterInfo.mineralExotic < cost.mineralExotic) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL_EXOTIC;
        if (m_myCharacter.m_characterInfo.mineralDark < cost.mineralDark) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL_DARK;

        return ServerErrorCode.SUCCESS;
    }
}
