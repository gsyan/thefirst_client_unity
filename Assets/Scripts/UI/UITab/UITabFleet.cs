using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UITabFleet : UITabBase
{
    [Header("함선 선택 UI 부모")]
    [SerializeField] private RectTransform m_shipSelectorContainer;
    private ShipSelector[] m_shipSelectors;

    [SerializeField] private Button m_addShipButton;
    [SerializeField] private TMP_Text m_currentShipCountStatText;

    [Header("함선 액션 버튼 (선택 시 활성)")]
    [SerializeField] private Button m_btnShipRepair;    // 집중 수리 (추후 구현)

    [SerializeField] private Button m_fleetManageButton;

    private SpaceFleet m_playerFleet;
    private ShipSelector m_selectedShipSelector;
    private bool m_needsLayoutRebuild = false;

    public override void InitializeUITab()
    {
        InitializeUITabFleet();
    }

    private void InitializeUITabFleet()
    {
        if (DataManager.Instance.m_currentCharacter == null || ObjectManager.Instance.m_myFleet == null) return;
        m_playerFleet = ObjectManager.Instance.m_myFleet;

        if (m_shipSelectorContainer != null)
            m_shipSelectors = m_shipSelectorContainer.GetComponentsInChildren<ShipSelector>(true);

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
        HideTabButtons();
        UpdateTechLevelDisplay();

        if (m_needsLayoutRebuild == true)
        {
            m_needsLayoutRebuild = false;
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_shipSelectorContainer);
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        }

        RefreshShipHealthDisplay();

        // 선택된 함선이 없으면 살아있는 첫 함선 자동 선택
        if (m_selectedShipSelector == null && m_playerFleet != null && m_playerFleet.m_ships.Count > 0)
        {
            SpaceShip firstShip = m_playerFleet.GetFirstAliveShip();
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
        RefreshTabButtons();

        // Fleet 탭 벗어날 때 모든 함선 아웃라인 비활성화
        if (m_playerFleet == null) return;
        for (int i = 0; i < m_playerFleet.m_ships.Count; i++)
        {
            if (m_playerFleet.m_ships[i] == null) continue;
            m_playerFleet.m_ships[i].m_shipOutline.enabled = false;
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
        if (m_shipSelectors == null || m_playerFleet == null) return;

        SpaceShip prevShip = m_selectedShipSelector != null ? m_selectedShipSelector.Ship : null;

        // 이전 선택 아웃라인 정리
        if (prevShip != null)
            prevShip.m_shipOutline.enabled = false;

        m_selectedShipSelector = null;

        int shipCount  = m_playerFleet.m_ships.Count;
        int maxInCsv   = DataManager.Instance.m_dataTableResearch.GetMaxShipCountInCsv();
        bool canAdd    = shipCount < maxInCsv;

        for (int i = 0; i < m_shipSelectors.Length; i++)
        {
            if (m_shipSelectors[i] == null) continue;

            if (i < shipCount)
            {
                m_shipSelectors[i].gameObject.SetActive(true);
                SpaceShip captured = m_playerFleet.m_ships[i];
                m_shipSelectors[i].InitializeShipSelector(captured, () => OnShipSelectorClicked(captured), OnShipManageClicked);

                // 이전 선택 함선 복원
                if (captured == prevShip)
                {
                    m_selectedShipSelector = m_shipSelectors[i];
                    m_selectedShipSelector.SetSelected(true);
                    if (gameObject.activeInHierarchy == true)
                        prevShip.m_shipOutline.enabled = true;
                }
            }
            else
            {
                m_shipSelectors[i].gameObject.SetActive(false);
            }
        }

        if (m_addShipButton != null) m_addShipButton.gameObject.SetActive(canAdd);
        UpdateShipCountDisplay();

        if (gameObject.activeInHierarchy == true)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_shipSelectorContainer);
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        }
        else
        {
            m_needsLayoutRebuild = true;
        }
    }

    private void UpdateShipCountDisplay()
    {
        if (m_currentShipCountStatText == null || m_playerFleet == null) return;
        int current = m_playerFleet.m_ships.Count;
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
        if (m_playerFleet == null) return;
        if (m_playerFleet.GetMissingHealth() <= 0f) return;

        var loc          = LocalizationManager.Instance;
        var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;
        int cost         = gameSettings.battleRepairMineralPerSec * gameSettings.instantRepairBaseSecs;

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title        = loc.Get("fleet_instant_repair_title"),
            message      = loc.Get("fleet_instant_repair_message"),
            cost         = new CostStruct(ECostType.Mineral, cost),
            confirmText1 = loc.Get("fleet_instant_repair_confirm"),
            onConfirm    = ExecuteFleetInstantRepair,
            onCancel     = () => { }
        });
    }

    private void ExecuteFleetInstantRepair()
    {
        NetworkManager.Instance.FleetInstantRepair(response =>
        {
            if (response.errorCode == 0)
            {
                DataManager.Instance.m_currentCharacter.UpdateMineral(response.data.mineralRemain);
                m_playerFleet.FullRepair();
            }
            else
            {
                ShowErrorMessage(ErrorCodeMapping.GetMessage(response.errorCode));
            }
        });
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
        if (DataManager.Instance.m_currentCharacter == null) return;

        var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;
        int currentShipCount = m_playerFleet.m_ships.Count;
        int requiredTechLevel = DataManager.Instance.m_dataTableResearch.GetRequiredTechLevel(currentShipCount + 1);
        var require = new RequireStruct(requiredTechLevel);

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title     = LocalizationManager.Instance.Get("fleet_add_ship_name"),
            message   = LocalizationManager.Instance.Get("popup_message_add_ship"),
            require   = require,
            cost      = new CostStruct(ECostType.ModulePoint, gameSettings.addShipCost),
            onConfirm = ExecuteAddShip,
            onCancel  = () => { }
        });
    }

    private void ExecuteAddShip()
    {
        Character character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

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
                character.UpdateModulePoint(response.data.modulePointRemain);

                if (response.data.newShipInfo != null)
                {
                    DataManager.Instance.AddFleetShip(response.data.newShipInfo);
                    if (ObjectManager.Instance.m_myFleet != null)
                        ObjectManager.Instance.m_myFleet.CreateSpaceShipById(response.data.newShipInfo.id, bWarp: true);
                }

                EventManager.Trigger_FleetShipCountChanged();
            }
        });
    }

    private ServerErrorCode CanAddShip()
    {
        Character character = DataManager.Instance.m_currentCharacter;
        if (character == null) return ServerErrorCode.CLIENT_CanAddShip_CHARACTER_NOT_FOUND;

        SpaceFleet myFleet = ObjectManager.Instance.m_myFleet;
        if (myFleet == null) return ServerErrorCode.FLEET_NOT_FOUND;

        var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;
        int currentShipCount = myFleet.m_ships.Count;
        int maxInCsv = DataManager.Instance.m_dataTableResearch.GetMaxShipCountInCsv();
        if (currentShipCount >= maxInCsv) return ServerErrorCode.CLIENT_CanAddShip_FLEET_MAX_SHIPS_REACHED;

        int techLevel = character.GetTechLevel();
        int maxShipsAtTech = DataManager.Instance.m_dataTableResearch.GetShipCount(techLevel);
        if (currentShipCount >= maxShipsAtTech) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_TECH_LEVEL;
        if (character.m_characterInfo.modulePoint < gameSettings.addShipCost) return ServerErrorCode.ADD_SHIP_FAIL_INSUFFICIENT_MODULE_POINT;

        return ServerErrorCode.SUCCESS;
    }
}

