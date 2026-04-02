// 함대 탭 UI — Tech Level 행, Fleet Stats(2행 압축), 함선 선택 그리드(9칸 고정, 프리팹에 미리 배치), Formation 하단 바 + 교체 팝업 관리
// 빈 슬롯은 잠금 아이콘으로 표시, 클릭 시 함선 추가 팝업 호출
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UITabFleet : UITabBase
{
    [Header("Tech Level 행")]
    [SerializeField] private TMP_Text m_textTechLevelInfo;
    [SerializeField] private Button   m_btnTechLevelDetail;
    [SerializeField] private Button   m_btnTechLevel;
    [SerializeField] private TMP_Text m_textTechLevelButton;

    [Header("Fleet Stats (상단 2행)")]
    [SerializeField] private TMP_Text m_textFleetStats1;
    [SerializeField] private TMP_Text m_textFleetStats2;
    [SerializeField] private Button   m_btnFleetStatsDetail;

    [Header("함선 선택 그리드 (프리팹에 9개 미리 배치)")]
    [SerializeField] private ShipSelector[] m_shipSelectors;
    
    [Header("함선 액션 버튼 (선택 시 활성)")]
    [SerializeField] private Button m_btnShipManage;    // 함선 관리 탭으로 이동
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
        if (m_myFleet == null) return;

        m_btnFormationChange.onClick.AddListener(OnFormationChangeClicked);

        if (m_btnShipManage != null) m_btnShipManage.onClick.AddListener(OnShipManageClicked);
        if (m_btnShipRepair != null) m_btnShipRepair.onClick.AddListener(OnShipRepairClicked);
        if (m_btnTechLevelDetail != null) m_btnTechLevelDetail.onClick.AddListener(OnTechLevelDetailClicked);
        if (m_btnTechLevel != null) m_btnTechLevel.onClick.AddListener(OnTechLevelButtonClicked);
        if (m_btnFleetStatsDetail != null) m_btnFleetStatsDetail.onClick.AddListener(OnFleetStatsDetailClicked);

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
        UpdateFleetStatsDisplay();
        UpdateCurrentFormationText();
        RefreshShipHealthDisplay();
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
    }

    // ── Tech Level ────────────────────────────────────────────────────

    private void UpdateTechLevelDisplay()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        int currentLevel = character.GetTechLevel();
        int storageCap = 3 + (currentLevel / 2);  // 기술레벨이 높을수록 자원 보관 가능 시간 증가
        int maxShips = DataManager.Instance.m_dataTableConfig.gameSettings.GetMaxShipsAtTechLevel(currentLevel);
        TechLevelResearchData nextNode = GetNextTechLevelNode(character);

        // 기술레벨 요약: 레벨 / 자원 보관 캡 / 최대 함선 수
        if (m_textTechLevelInfo != null)
        {
            m_textTechLevelInfo.text = $"<sprite name=\"IconTech\"> {currentLevel} <sprite name=\"IconMineralCap\"> {storageCap}h <sprite name=\"IconShips\"> {maxShips}";
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_textTechLevelInfo.transform.parent as RectTransform);
        }

        if (m_btnTechLevel != null)
        {
            m_btnTechLevel.interactable = nextNode != null;
            if (m_textTechLevelButton != null)
            {
                // string.Format 을 사용하지 않고 직접 조합 — Smart String이 {1}을 소모하는 문제 우회
                m_textTechLevelButton.text = nextNode != null
                    ? $"Lv.{currentLevel} <voffset=6>→</voffset> {nextNode.targetTechLevel}"
                    : LocalizationManager.Instance.Get("max_level");
            }
        }
    }

    private void OnTechLevelDetailClicked()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        int currentLevel = character.GetTechLevel();
        int storageCap = 3 + (currentLevel / 2);
        int maxShips = DataManager.Instance.m_dataTableConfig.gameSettings.GetMaxShipsAtTechLevel(currentLevel);
        TechLevelResearchData nextNode = GetNextTechLevelNode(character);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<sprite name=\"IconMineralCap\"> (Resource Cap)  {storageCap}h");
        sb.AppendLine();
        sb.Append    ($"<sprite name=\"IconShips\"> (Max Ships)  {maxShips}");

        if (nextNode != null)
        {
            int nextCap = 3 + (nextNode.targetTechLevel / 2);
            int nextMaxShips = DataManager.Instance.m_dataTableConfig.gameSettings.GetMaxShipsAtTechLevel(nextNode.targetTechLevel);
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine(LocalizationManager.Instance.Get("tech_level_on_reach", new object[] { nextNode.targetTechLevel }));
            sb.AppendLine();
            sb.AppendLine($"<sprite name=\"IconMineralCap\"> (Resource Cap)  {nextCap}h");
            sb.AppendLine();
            sb.Append    ($"<sprite name=\"IconShips\"> (Max Ships)  {nextMaxShips}");
        }

        UIManager.Instance.ShowAlertPopup(LocalizationManager.Instance.Get("tech_level_detail_title"), sb.ToString(), null);
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
            ShowResultMessage(LocalizationManager.Instance.Get("error_insufficient_resources"), 3f);
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
            ShowResultMessage($"Research failed: {errorMessage}", 3f);
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
        else
            ShowResultMessage(LocalizationManager.Instance.Get("research_complete"), 3f);
    }

    private void OnTechLevelChanged(int techLevel)
    {
        UpdateTechLevelDisplay();
    }

    // ── Fleet Stats ────────────────────────────────────────────────────

    private void UpdateFleetStatsDisplay()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null || character.GetOwnedFleet() == null) return;

        SpaceFleet fleet = character.GetOwnedFleet();
        CapabilityProfile statsOrg = fleet.GetFleetCapabilityProfile(false);
        CapabilityProfile statsCur = fleet.GetFleetCapabilityProfile(true);

        // 1행: 함선 전투력 합산
        if (m_textFleetStats1 != null)
        {
            m_textFleetStats1.text =
                $"<sprite name=\"IconAttack\"> {statsCur.attack:F0}  " +
                $"<sprite name=\"IconHp\"> {statsOrg.health:F0}  " +
                $"<sprite name=\"IconSpeed\"> {statsCur.speed:F0}  " +
                $"<sprite name=\"IconRepair\"> {statsCur.repair:F0}";
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_textFleetStats1.transform.parent as RectTransform);
        }

        // 2행: 함재기 (보유 시만 표시)
        if (m_textFleetStats2 != null)
        {
            bool hasAircraft = statsOrg.airCount > 0;
            m_textFleetStats2.gameObject.SetActive(hasAircraft);
            if (hasAircraft)
            {
                m_textFleetStats2.text =
                    $"<sprite name=\"IconAircraftAttack\"> {statsCur.airAttack:F0}  " +
                    $"<sprite name=\"IconAircraft\"> {statsOrg.airCount:F0}  " +
                    $"<sprite name=\"IconLaunch\"> {statsCur.airLaunchCount:F0}";
                LayoutRebuilder.ForceRebuildLayoutImmediate(m_textFleetStats2.transform.parent as RectTransform);
            }
        }
    }

    private void OnFleetStatsDetailClicked()
    {
        if (m_myFleet == null) return;

        CapabilityProfile org = m_myFleet.GetFleetCapabilityProfile(false);
        CapabilityProfile cur = m_myFleet.GetFleetCapabilityProfile(true);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<sprite name=\"IconAttack\"> (Attack)  {cur.attack:F0}");
        sb.AppendLine();
        sb.AppendLine($"<sprite name=\"IconHp\"> (HP)  {org.health:F0}");
        sb.AppendLine();
        sb.AppendLine($"<sprite name=\"IconSpeed\"> (Speed)  {cur.speed:F0}");
        sb.AppendLine();
        sb.Append    ($"<sprite name=\"IconRepair\"> (Repair)  {cur.repair:F0}");

        if (org.airCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine($"<sprite name=\"IconAircraftAttack\"> (Aircraft Attack)  {cur.airAttack:F0}");
            sb.AppendLine();
            sb.AppendLine($"<sprite name=\"IconAircraft\"> (Aircraft)  {org.airCount:F0}");
            sb.AppendLine();
            sb.Append    ($"<sprite name=\"IconLaunch\"> (Aircraft Launch)  {cur.airLaunchCount:F0}");
        }

        UIManager.Instance.ShowAlertPopup("Fleet Stats", sb.ToString(), null);
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

    private void PopulateShipSelectorGrid()
    {
        if (m_shipSelectors == null || m_myFleet == null) return;

        m_selectedShipSelector = null;

        int shipCount = m_myFleet.m_ships.Count;
        int maxShips  = DataManager.Instance.m_dataTableConfig.gameSettings.maxShipsPerFleet;

        for (int i = 0; i < m_shipSelectors.Length; i++)
        {
            if (m_shipSelectors[i] == null) continue;

            if (i < shipCount)
            {
                SpaceShip captured = m_myFleet.m_ships[i];
                m_shipSelectors[i].Initialize(captured, () => OnShipSelectorClicked(captured));
            }
            else
            {
                bool canAdd = shipCount < maxShips;
                m_shipSelectors[i].InitializeLocked(canAdd ? OnAddShipButtonClicked : null);
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
        UpdateFleetStatsDisplay();
        PopulateShipSelectorGrid();
    }

    private void OnFleetHPUpdated()
    {
        UpdateFleetStatsDisplay();
        RefreshShipHealthDisplay();
    }

    private void OnShipStatsChanged(SpaceShip ship)
    {
        UpdateFleetStatsDisplay();
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
        CostStruct cost = gameSettings.GetAddShipCost(m_myFleet.m_ships.Count);

        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("fleet_add_ship_name"),
            LocalizationManager.Instance.Get("popup_message_add_ship"),
            null, cost,
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
