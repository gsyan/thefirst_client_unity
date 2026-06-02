using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UITabFleet : UITabBase
{
    [Header("함선 선택 UI 부모")]
    [SerializeField] private GameObject m_shipSelectorsObj;
    private ShipSelector[] m_shipSelectors;

    [SerializeField] private GameObject m_addShipContainer;
    [SerializeField] private Button m_addShipButton;
    [SerializeField] private TMP_Text m_currentShipCountStatText;

    [Header("함선 액션 버튼 (선택 시 활성)")]
    [SerializeField] private Button m_btnShipRepair;    // 집중 수리 (추후 구현)

    [SerializeField] private Button m_fleetManageButton;

    private Character m_myCharacter;
    private SpaceFleet m_myFleet;
    private ShipSelector m_selectedShipSelector;

    public override void InitializeUITab()
    {
        InitializeUITabFleet();
    }

    private void InitializeUITabFleet()
    {
        m_myCharacter = DataManager.Instance.m_currentCharacter;
        if (m_myCharacter == null || m_myCharacter.GetOwnedFleet() == null) return;
        m_myFleet = m_myCharacter.GetOwnedFleet();

        if (m_shipSelectorsObj != null)
            m_shipSelectors = m_shipSelectorsObj.GetComponentsInChildren<ShipSelector>(true);
        if (m_myFleet == null) return;

        m_fleetManageButton.onClick.AddListener(OnFleetTacticsButtonClicked);

        if (m_addShipButton != null) m_addShipButton.onClick.AddListener(OnAddShipButtonClicked);
        if (m_btnShipRepair != null) m_btnShipRepair.onClick.AddListener(OnShipRepairClicked);
        PopulateShipSelectorGrid();
        UpdateShipActionButtons();

        EventManager.Subscribe_FleetShipCountChanged(OnShipCountChanged);
        EventManager.Subscribe_FleetUpdateHP(OnFleetHPUpdated);
        EventManager.Subscribe_SpaceShipSelected(OnSpaceShipSelected);
        EventManager.Subscribe_TechLevelChanged(OnTechLevelChanged);
        EventManager.Subscribe_ShipStatsChanged(OnShipStatsChanged);
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        SetOtherTabsVisible(false, includeSelf: true);
        UpdateTechLevelDisplay();
        RefreshShipHealthDisplay();

        // 선택된 함선이 없으면 살아있는 첫 함선 자동 선택
        if (m_selectedShipSelector == null && m_myFleet != null && m_myFleet.m_ships.Count > 0)
        {
            SpaceShip firstShip = m_myFleet.GetFirstAliveShip();
            if (firstShip != null)
                OnShipSelectorClicked(firstShip);
            return;
        }

        // Fleet 탭 진입 시 선택된 함선 아웃라인 활성화
        if (m_selectedShipSelector != null && m_selectedShipSelector.Ship != null)
            m_selectedShipSelector.Ship.m_shipOutline.enabled = true;
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
        SetOtherTabsVisible(true, includeSelf: true);

        // Fleet 탭 벗어날 때 모든 함선 아웃라인 비활성화
        if (m_myFleet == null) return;
        for (int i = 0; i < m_myFleet.m_ships.Count; i++)
        {
            if (m_myFleet.m_ships[i] == null) continue;
            m_myFleet.m_ships[i].m_shipOutline.enabled = false;
        }
    }

    // ── Tech Level ────────────────────────────────────────────────────

    private void UpdateTechLevelDisplay()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        int currentLevel = character.GetTechLevel();
        
        int maxShips = DataManager.Instance.m_dataTableResearch.GetShipCount(currentLevel);
    }

    // currentLevel+1 ~ toLevel 까지 순차적으로 API 호출
    private void ResearchTechLevelsSequentially(int fromLevel, int toLevel)
    {
        var techList = DataManager.Instance.m_dataTableResearch.TechLevelDataList;
        var node = techList.Find(n => n.targetTechLevel == fromLevel);
        if (node == null) return;

        var character = DataManager.Instance.m_currentCharacter;
        if (character.CheckEnoughTechPoint(node.pointCost) == false)
        {
            ShowErrorMessage(LocalizationManager.Instance.Get("error_insufficient_resources"));
            return;
        }

        var request = new TechLevelResearchRequest { researchId = node.researchId };
        NetworkManager.Instance.ResearchTechLevel(request, response =>
        {
            OnSequentialTechLevelResponse(response, fromLevel, toLevel);
        });
    }

    private void OnSequentialTechLevelResponse(ApiResponse<TechLevelResearchResponse> response, int completedLevel, int toLevel)
    {
        if (response.errorCode != 0)
        {
            string errorMessage = ErrorCodeMapping.GetMessage(response.errorCode);
            ShowErrorMessage($"Research failed: {errorMessage}");
            return;
        }

        DataManager.Instance.m_currentCharacter.UpdateTechPoint(response.data.techPointRemain);
        if (response.data.researchedIds != null)
            DataManager.Instance.m_currentCharacter.SetCompletedResearchIds(response.data.researchedIds);

        UpdateTechLevelDisplay();

        int nextLevel = completedLevel + 1;
        if (nextLevel <= toLevel)
            ResearchTechLevelsSequentially(nextLevel, toLevel);
    }

    private void OnTechLevelChanged(int techLevel)
    {
        UpdateTechLevelDisplay();
    }

    // ── Fleet Stats ────────────────────────────────────────────────────

    // ── Formation ──────────────────────────────────────────────────────

    private void OnFleetTacticsButtonClicked()
    {
        if (m_tabSystemParent == null) return;
        m_tabSystemParent.SwitchToTabByName("tab_fleettactics");
    }

    // ── ShipSelector 그리드 ────────────────────────────────────────────

    private void PopulateShipSelectorGrid()
    {
        if (m_shipSelectors == null || m_myFleet == null) return;

        m_selectedShipSelector = null;

        int shipCount  = m_myFleet.m_ships.Count;
        int maxInCsv   = DataManager.Instance.m_dataTableResearch.GetMaxShipCountInCsv();
        bool canAdd    = shipCount < maxInCsv;

        for (int i = 0; i < m_shipSelectors.Length; i++)
        {
            if (m_shipSelectors[i] == null) continue;

            if (i < shipCount)
            {
                m_shipSelectors[i].gameObject.SetActive(true);
                SpaceShip captured = m_myFleet.m_ships[i];
                m_shipSelectors[i].InitializeShipSelector(captured, () => OnShipSelectorClicked(captured), OnShipManageClicked);
            }
            else
            {
                m_shipSelectors[i].gameObject.SetActive(false);
            }
        }

        if (m_addShipContainer != null) m_addShipContainer.SetActive(canAdd);
        UpdateShipCountDisplay();

        if (m_shipSelectorsObj != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_shipSelectorsObj.GetComponent<RectTransform>());
    }

    private void UpdateShipCountDisplay()
    {
        if (m_currentShipCountStatText == null || m_myFleet == null) return;
        int current = m_myFleet.m_ships.Count;
        int max = DataManager.Instance.m_dataTableResearch.GetMaxShipCountInCsv();
        m_currentShipCountStatText.text = $"{current} / {max}";
    }

    private void RefreshShipHealthDisplay()
    {
        if (m_shipSelectors == null) return;

        for (int i = 0; i < m_shipSelectors.Length; i++)
        {
            if (m_shipSelectors[i] == null || m_shipSelectors[i].gameObject.activeSelf == false) continue;

            if (m_shipSelectors[i].Ship == null)
            {
                // 전투 중 파괴된 함선 — 파괴 상태 표시
                m_shipSelectors[i].SetDestroyedState();
                if (m_selectedShipSelector == m_shipSelectors[i])
                    SelectAliveShipFallback();
            }
            else
            {
                m_shipSelectors[i].RefreshHealth();
            }
        }
    }

    // 살아있는 함선으로 선택 변경 (기함 우선, 인덱스 순)
    private void SelectAliveShipFallback()
    {
        if (m_selectedShipSelector != null)
        {
            m_selectedShipSelector.SetSelected(false);
            m_selectedShipSelector = null;
        }

        for (int i = 0; i < m_shipSelectors.Length; i++)
        {
            if (m_shipSelectors[i] == null || m_shipSelectors[i].gameObject.activeSelf == false) continue;
            SpaceShip ship = m_shipSelectors[i].Ship;
            if (ship != null && ship.IsAlive() == true)
            {
                OnShipSelectorClicked(ship);
                return;
            }
        }

        UpdateShipActionButtons();
    }

    private void UpdateShipActionButtons()
    {
        bool hasSelection = m_selectedShipSelector != null;
        if (m_btnShipRepair != null) m_btnShipRepair.interactable = hasSelection;
    }

    private void OnShipManageClicked()
    {
        if (m_selectedShipSelector == null || m_selectedShipSelector.Ship == null) return;
        m_tabSystemParent.SwitchToTabByName("tab_ship");
    }

    private void OnShipRepairClicked()
    {
        // TODO: 집중 수리 구현
    }

    private void OnShipSelectorClicked(SpaceShip ship)
    {
        if (m_selectedShipSelector != null)
        {
            m_selectedShipSelector.SetSelected(false);
            if (m_selectedShipSelector.Ship != null)
                m_selectedShipSelector.Ship.m_shipOutline.enabled = false;
        }

        for (int i = 0; i < m_shipSelectors.Length; i++)
        {
            if (m_shipSelectors[i] != null && m_shipSelectors[i].Ship == ship)
            {
                m_selectedShipSelector = m_shipSelectors[i];
                m_selectedShipSelector.SetSelected(true);
                if (m_selectedShipSelector.Ship != null)
                    m_selectedShipSelector.Ship.m_shipOutline.enabled = true;
                break;
            }
        }

        UpdateShipActionButtons();

        // 카메라 타겟 지정 (파괴된 함선은 이벤트 전달 생략)
        if (ship != null)
            EventManager.Trigger_SpaceShipSelected(ship);
    }

    // ── 이벤트 핸들러 ─────────────────────────────────────────────────

    private void OnSpaceShipSelected(SpaceShip ship)
    {
        if (m_selectedShipSelector != null && m_selectedShipSelector.Ship == ship) return;

        if (m_selectedShipSelector != null)
            m_selectedShipSelector.SetSelected(false);

        m_selectedShipSelector = null;
        for (int i = 0; i < m_shipSelectors.Length; i++)
        {
            if (m_shipSelectors[i] != null && m_shipSelectors[i].Ship == ship)
            {
                m_selectedShipSelector = m_shipSelectors[i];
                m_selectedShipSelector.SetSelected(true);
                break;
            }
        }

        UpdateShipActionButtons();
    }

    private void OnShipCountChanged()
    {
        PopulateShipSelectorGrid();
    }

    private void OnFleetHPUpdated()
    {
        RefreshShipHealthDisplay();
    }

    private void OnShipStatsChanged(SpaceShip ship)
    {
        for (int i = 0; i < m_shipSelectors.Length; i++)
        {
            if (m_shipSelectors[i] != null && m_shipSelectors[i].Ship == ship)
            {
                m_shipSelectors[i].RefreshStats();
                break;
            }
        }
    }

    // ── 함선 추가 ─────────────────────────────────────────────────────

    private void OnAddShipButtonClicked()
    {
        if (m_myCharacter == null) return;

        var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;
        int currentShipCount = m_myFleet.m_ships.Count;
        int requiredTechLevel = DataManager.Instance.m_dataTableResearch.GetRequiredTechLevel(currentShipCount + 1);
        var require = new RequireStruct(requiredTechLevel);

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title     = LocalizationManager.Instance.Get("fleet_add_ship_name"),
            message   = LocalizationManager.Instance.Get("popup_message_add_ship"),
            require   = require,
            cost      = new CostStruct(ECostType.ModulePoint, gameSettings.addShipCost),
            onConfirm = ExecuteAddShip
        });
    }

    private void ExecuteAddShip()
    {
        if (m_myCharacter == null) return;

        ServerErrorCode errorCode = CanAddShip();
        if (errorCode != ServerErrorCode.SUCCESS)
        {
            ShowErrorMessage($"{errorCode}");
            return;
        }

        var request = new AddShipRequest { fleetId = null };

        NetworkManager.Instance.AddShip(request, (response) =>
        {
            if (response.errorCode == 0)
            {
                m_myCharacter.UpdateModulePoint(response.data.modulePointRemain);
                
                if (response.data.updatedFleetInfo != null)
                    DataManager.Instance.SetFleetData(response.data.updatedFleetInfo);

                if (response.data.newShipInfo != null && m_myCharacter.m_ownedFleet != null)
                    ObjectManager.Instance.m_myFleet.CreateSpaceShipFromData(response.data.newShipInfo, true);

                EventManager.Trigger_FleetShipCountChanged();
            }
        });
    }

    private ServerErrorCode CanAddShip()
    {
        if (m_myCharacter == null) return ServerErrorCode.CLIENT_CanAddShip_CHARACTER_NOT_FOUND;

        var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;
        if (m_myCharacter.m_ownedFleet == null) return ServerErrorCode.FLEET_NOT_FOUND;
        int currentShipCount = m_myCharacter.m_ownedFleet.m_ships.Count;
        int maxInCsv = DataManager.Instance.m_dataTableResearch.GetMaxShipCountInCsv();
        if (currentShipCount >= maxInCsv) return ServerErrorCode.CLIENT_CanAddShip_FLEET_MAX_SHIPS_REACHED;

        int techLevel = m_myCharacter.GetTechLevel();
        int maxShipsAtTech = DataManager.Instance.m_dataTableResearch.GetShipCount(techLevel);
        if (currentShipCount >= maxShipsAtTech) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_TECH_LEVEL;
        if (m_myCharacter.m_characterInfo.modulePoint < gameSettings.addShipCost) return ServerErrorCode.ADD_SHIP_FAIL_INSUFFICIENT_MODULE_POINT;

        return ServerErrorCode.SUCCESS;
    }
}
