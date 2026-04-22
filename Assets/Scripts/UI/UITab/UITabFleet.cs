using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UITabFleet : UITabBase
{
    [Header("함선 선택 UI 부모")]
    [SerializeField] private GameObject m_shipSelectorsObj;
    private ShipSelector[] m_shipSelectors;
    
    [Header("함선 액션 버튼 (선택 시 활성)")]
    [SerializeField] private Button m_btnShipRepair;    // 집중 수리 (추후 구현)

    [Header("Formation 하단 바")]
    [SerializeField] private TMP_Text m_textCurrentFormation;   // 현재 진형명
    [SerializeField] private Button m_btnFormationChange;       // [교체 ▶] 버튼

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

        m_btnFormationChange.onClick.AddListener(OnFormationChangeClicked);

        if (m_btnShipRepair != null) m_btnShipRepair.onClick.AddListener(OnShipRepairClicked);
        PopulateShipSelectorGrid();
        UpdateShipActionButtons();

        EventManager.Subscribe_AddShip(OnShipAdded);
        EventManager.Subscribe_FleetUpdateHP(OnFleetHPUpdated);
        EventManager.Subscribe_SpaceShipSelected(OnSpaceShipSelected);
        EventManager.Subscribe_TechLevelChanged(OnTechLevelChanged);
        EventManager.Subscribe_ShipStatsChanged(OnShipStatsChanged);
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        UpdateTechLevelDisplay();
        UpdateCurrentFormationText();
        RefreshShipHealthDisplay();

        // Fleet 탭 진입 시 선택된 함선 아웃라인 활성화
        if (m_selectedShipSelector != null && m_selectedShipSelector.Ship != null)
            m_selectedShipSelector.Ship.m_shipOutline.enabled = true;
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();

        // Fleet 탭 벗어날 때 모든 함선 아웃라인 비활성화
        if (m_myFleet == null) return;
        for (int i = 0; i < m_myFleet.m_ships.Count; i++)
            m_myFleet.m_ships[i].m_shipOutline.enabled = false;
    }

    // ── Tech Level ────────────────────────────────────────────────────

    private void UpdateTechLevelDisplay()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        int currentLevel = character.GetTechLevel();
        int storageCap = (int)DataManager.Instance.m_dataTableResearch.GetStackTime(currentLevel);
        int maxShips = DataManager.Instance.m_dataTableResearch.GetShipCount(currentLevel);
    }

    private TechLevelResearchData GetNextTechLevelNode(Character character)
    {
        var techList = DataManager.Instance.m_dataTableResearch.TechLevelDataList;
        for (int i = 0; i < techList.Count; i++)
        {
            if (character.IsResearchCompleted(techList[i].researchId) == false)
                return techList[i];
        }
        return null;
    }

    private void OnTechLevelButtonClicked()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        if (GetNextTechLevelNode(character) == null) return;

        int currentLevel = character.GetTechLevel();
        UIManager.Instance.ShowTechLevelupPopup(currentLevel, targetLevel =>
        {
            ResearchTechLevelsSequentially(currentLevel + 1, targetLevel);
        });
    }

    // currentLevel+1 ~ toLevel 까지 순차적으로 API 호출
    private void ResearchTechLevelsSequentially(int fromLevel, int toLevel)
    {
        var techList = DataManager.Instance.m_dataTableResearch.TechLevelDataList;
        var node = techList.Find(n => n.targetTechLevel == fromLevel);
        if (node == null) return;

        var character = DataManager.Instance.m_currentCharacter;
        if (character.CheckEnoughCostStruct(node.researchCost) == false)
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

        if (response.data.costRemainInfo != null)
            DataManager.Instance.m_currentCharacter.UpdateAllMinerals(response.data.costRemainInfo);
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

    private void PopulateShipSelectorGrid()
    {
        if (m_shipSelectors == null || m_myFleet == null) return;

        m_selectedShipSelector = null;

        int shipCount = m_myFleet.m_ships.Count;
        int maxShips  = DataManager.Instance.m_dataTableConfig.gameSettings.maxShipsPerFleet;
        bool canAdd   = shipCount < maxShips;

        for (int i = 0; i < m_shipSelectors.Length; i++)
        {
            if (m_shipSelectors[i] == null) continue;

            if (i < shipCount)
            {
                // 보유 함선 슬롯
                m_shipSelectors[i].gameObject.SetActive(true);
                SpaceShip captured = m_myFleet.m_ships[i];
                m_shipSelectors[i].InitializeShipSelector(captured, () => OnShipSelectorClicked(captured), () => OnShipManageClicked(captured));
            }
            else if (i == shipCount && canAdd)
            {
                // 다음 추가 가능 슬롯 1개만 표시
                m_shipSelectors[i].gameObject.SetActive(true);
                m_shipSelectors[i].InitializeShipSelectorLocked(OnAddShipButtonClicked);
            }
            else
            {
                // 나머지 슬롯은 숨김
                m_shipSelectors[i].gameObject.SetActive(false);
            }
        }
    }

    private void RefreshShipHealthDisplay()
    {
        if (m_shipSelectors == null || m_myFleet == null) return;

        int shipCount = m_myFleet.m_ships.Count;

        // 각 슬롯의 Ship 참조가 현재 함대와 다르면 전체 재구성
        for (int i = 0; i < m_shipSelectors.Length; i++)
        {
            if (m_shipSelectors[i] == null) continue;
            SpaceShip expected = i < shipCount ? m_myFleet.m_ships[i] : null;
            if (m_shipSelectors[i].Ship != expected)
            {
                PopulateShipSelectorGrid();
                return;
            }
        }

        for (int i = 0; i < shipCount && i < m_shipSelectors.Length; i++)
            if (m_shipSelectors[i] != null)
                m_shipSelectors[i].RefreshHealth();
    }

    private void UpdateShipActionButtons()
    {
        bool hasSelection = m_selectedShipSelector != null;
        if (m_btnShipRepair != null) m_btnShipRepair.interactable = hasSelection;
    }

    private void OnShipManageClicked(SpaceShip ship)
    {
        OnShipSelectorClicked(ship);
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
                m_selectedShipSelector.Ship.m_shipOutline.enabled = true;
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

    private void OnShipAdded()
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
        CostStruct cost = gameSettings.GetAddShipCost(currentShipCount);
        int requiredTechLevel = DataManager.Instance.m_dataTableResearch.GetRequiredTechLevel(currentShipCount + 1);
        var require = new RequireStruct(requiredTechLevel);

        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("fleet_add_ship_name"),
            LocalizationManager.Instance.Get("popup_message_add_ship"),
            null, require, cost,
            ExecuteAddShip
        );
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
                m_myCharacter.UpdateMineral(response.data.costRemainInfo.remainMineral);
                m_myCharacter.UpdateMineralRare(response.data.costRemainInfo.remainMineralRare);
                m_myCharacter.UpdateMineralExotic(response.data.costRemainInfo.remainMineralExotic);
                m_myCharacter.UpdateMineralDark(response.data.costRemainInfo.remainMineralDark);
                
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
        int techLevel = m_myCharacter.GetTechLevel();
        int maxShipsAtTech = DataManager.Instance.m_dataTableResearch.GetShipCount(techLevel);
        if (currentShipCount >= maxShipsAtTech) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_TECH_LEVEL;
        if (m_myCharacter.m_characterInfo.mineral < cost.mineral) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL;
        if (m_myCharacter.m_characterInfo.mineralRare < cost.mineralRare) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL_RARE;
        if (m_myCharacter.m_characterInfo.mineralExotic < cost.mineralExotic) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL_EXOTIC;
        if (m_myCharacter.m_characterInfo.mineralDark < cost.mineralDark) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_MINERAL_DARK;

        return ServerErrorCode.SUCCESS;
    }
}
