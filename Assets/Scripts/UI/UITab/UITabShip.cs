// 함선/모듈 관리 UI — 헤더(함선 네비게이터+스탯2행), 모듈 맵, 모듈 디테일 카드
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITabShip : UITabBase
{
    [Header("상단 헤더 — 함선 네비게이터 + 스탯 2행")]
    [SerializeField] private Button    m_btnPrevShip;
    [SerializeField] private Button    m_btnNextShip;
    [SerializeField] private TMP_Text  m_textShipName;
    // 1행: ATK / HP / SPD / REP
    [SerializeField] private TMP_Text  m_textShipStats1;
    // 2행: 함재기 능력 — aircraft_count == 0 이면 숨김
    [SerializeField] private TMP_Text  m_textShipStats2;

    [Header("모듈 맵 — 슬롯 컨테이너")]
    [SerializeField] private RectTransform m_moduleBodySelectButtonContainer;
    [SerializeField] private RectTransform m_moduleBeamSelectButtonContainer;
    [SerializeField] private RectTransform m_moduleMissileSelectButtonContainer1;
    [SerializeField] private RectTransform m_moduleMissileSelectButtonContainer2;
    [SerializeField] private RectTransform m_moduleHangerSelectButtonContainer1;
    [SerializeField] private RectTransform m_moduleHangerSelectButtonContainer2;
    [SerializeField] private RectTransform m_moduleEngineSelectButtonContainer;
    [SerializeField] private GameObject    m_moduleSelectButtonPrefab;

    [Header("함선 스탯 디테일")]
    [SerializeField] private Button m_btnShipStatsDetail;

    [Header("모듈 디테일 카드")]
    [SerializeField] private RectTransform m_moduleStatsContainer;

    [SerializeField] private Button    m_unlockModuleButton;
    [SerializeField] private Button    m_levelUpModuleButton;
    [SerializeField] private TMP_Text  m_levelUpModuleButtonText;
    [SerializeField] private Button    m_subTypeManageButton;

    private bool bShow = false;

    private Character  m_myCharacter;
    private SpaceFleet m_myFleet;

    private SpaceShip  m_selectedShip;
    private ModuleBase m_selectedModule;

    private readonly List<RowLabelValue> m_moduleStatRows = new();

    // 모듈 선택 버튼 풀 (단일 풀, 컨테이너 무관)
    private readonly List<ModuleSelector> m_moduleSelectorPool   = new();
    private readonly List<ModuleSelector> m_moduleSelectorActive = new();
    private Transform m_selectorPoolHolder;


    public override void InitializeUITab()
    {
        InitializeUITabShip();
    }

    private void InitializeUITabShip()
    {
        m_myCharacter = DataManager.Instance.m_currentCharacter;
        if (m_myCharacter == null || m_myCharacter.GetOwnedFleet() == null) return;
        m_myFleet = m_myCharacter.GetOwnedFleet();
        if (m_myFleet == null) return;

        // m_moduleStatsContainer 내부 자식 캐싱
        if (m_moduleStatsContainer != null)
        {
            m_moduleStatRows.Clear();
            for (int i = 0; i < m_moduleStatsContainer.childCount; i++)
            {
                var row = m_moduleStatsContainer.GetChild(i).GetComponent<RowLabelValue>();
                if (row != null) m_moduleStatRows.Add(row);
            }
        }

        var poolHolderGO = new GameObject("_ModuleSelectorPool");
        poolHolderGO.transform.SetParent(transform, false);
        m_selectorPoolHolder = poolHolderGO.transform;

        if (m_btnPrevShip != null) m_btnPrevShip.onClick.AddListener(OnPrevShipClicked);
        if (m_btnNextShip != null) m_btnNextShip.onClick.AddListener(OnNextShipClicked);

        m_unlockModuleButton.onClick.AddListener(OnUnlockModuleClicked);
        m_levelUpModuleButton.onClick.AddListener(OnLevelUpModuleClicked);
        m_subTypeManageButton.onClick.AddListener(OnSubTypeManageClicked);
        if (m_btnShipStatsDetail != null) m_btnShipStatsDetail.onClick.AddListener(OnShipStatsDetailClicked);

        EventManager.Subscribe_SpaceShipSelected(OnSpaceShipSelected);
        EventManager.Subscribe_ShipUpdateHP(UpdateShipHeader);
        EventManager.Subscribe_SpaceShipModuleSelected(OnSpaceShipModuleSelected);
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();

        if (m_selectedShip == null)
            m_selectedShip = m_myFleet.m_ships[0];
        if (m_selectedModule == null)
            m_selectedModule = m_selectedShip.m_moduleBodys[0];

        m_selectedShip.m_shipOutline.enabled = true;
        CameraController.Instance.SetTargetOfCameraController(m_selectedShip.transform);
        EventManager.TriggerSpaceShipModuleSelected(m_selectedShip, m_selectedModule);

        bShow = true;
        UpdateShipHeader();
        UpdateModuleStatsDisplay();
        PopulateModuleSelectButtons();
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();

        bShow = false;

        if (m_myFleet != null)
            m_myFleet.ClearAllSelectedModule();
    }

    // ─────────────────────────────────────────────
    // 함선 네비게이터 (< / >)
    // ─────────────────────────────────────────────

    private void OnPrevShipClicked()
    {
        if (m_myFleet == null || m_myFleet.m_ships.Count == 0) return;
        int idx = m_myFleet.m_ships.IndexOf(m_selectedShip);
        int next = (idx - 1 + m_myFleet.m_ships.Count) % m_myFleet.m_ships.Count;
        SelectShip(m_myFleet.m_ships[next]);
    }

    private void OnNextShipClicked()
    {
        if (m_myFleet == null || m_myFleet.m_ships.Count == 0) return;
        int idx = m_myFleet.m_ships.IndexOf(m_selectedShip);
        int next = (idx + 1) % m_myFleet.m_ships.Count;
        SelectShip(m_myFleet.m_ships[next]);
    }

    private void SelectShip(SpaceShip ship)
    {
        if (ship == null || ship == m_selectedShip) return;

        if (m_selectedShip != null)
            m_selectedShip.m_shipOutline.enabled = false;

        m_selectedShip = ship;
        m_selectedShip.m_shipOutline.enabled = true;
        CameraController.Instance.SetTargetOfCameraController(m_selectedShip.transform);

        if (m_myFleet != null) m_myFleet.ClearAllSelectedModule();

        // 기본 모듈 선택
        m_selectedModule = null;
        if (ship.m_moduleBodys[0].m_beams.Count > 0)
            m_selectedModule = ship.m_moduleBodys[0].m_beams[0];
        else if (ship.m_moduleBodys[0].m_missiles.Count > 0)
            m_selectedModule = ship.m_moduleBodys[0].m_missiles[0];
        else
            m_selectedModule = ship.m_moduleBodys[0];

        if (bShow)
        {
            UpdateShipHeader();
            UpdateModuleStatsDisplay();
            PopulateModuleSelectButtons();
        }

        EventManager.TriggerSpaceShipModuleSelected(m_selectedShip, m_selectedModule);
    }

    // ─────────────────────────────────────────────
    // 함선 선택 이벤트 (3D 클릭 또는 Fleet 탭에서 전환)
    // ─────────────────────────────────────────────

    private void OnSpaceShipSelected(SpaceShip ship)
    {
        if (m_selectedShip == ship) return;

        if (m_selectedShip != null)
            m_selectedShip.m_shipOutline.enabled = false;

        m_selectedShip = ship;
        m_selectedShip.m_shipOutline.enabled = true;
        CameraController.Instance.SetTargetOfCameraController(m_selectedShip.transform);

        if (m_myFleet != null) m_myFleet.ClearAllSelectedModule();

        m_selectedModule = null;
        if (ship.m_moduleBodys[0].m_beams.Count > 0)
            m_selectedModule = ship.m_moduleBodys[0].m_beams[0];
        else if (ship.m_moduleBodys[0].m_missiles.Count > 0)
            m_selectedModule = ship.m_moduleBodys[0].m_missiles[0];
        else
            m_selectedModule = ship.m_moduleBodys[0];

        if (bShow)
        {
            UpdateShipHeader();
            UpdateModuleStatsDisplay();
            PopulateModuleSelectButtons();
        }
    }

    private void OnSpaceShipModuleSelected(SpaceShip ship, ModuleBase module)
    {
        if (module == null) return;
        if (m_myFleet == null) return;
        if (m_selectedShip != ship) return;

        m_selectedModule = module;
        m_selectedShip.SetSelectedModule(ship, module);

        if (bShow)
        {
            UpdateShipHeader();
            UpdateModuleStatsDisplay();
            UpdateModuleSelectButtonSelection();
        }
    }

    // ─────────────────────────────────────────────
    // 상단 헤더 갱신 (함선 이름 + 스탯 2행)
    // ─────────────────────────────────────────────

    private void UpdateShipHeader()
    {
        if (m_selectedShip == null) return;

        if (m_textShipName != null)
            m_textShipName.text = m_selectedShip.m_shipInfo.shipName;

        CapabilityProfile statsOrg = m_selectedShip.m_spaceShipStatsOrg;
        CapabilityProfile statsCur = m_selectedShip.m_spaceShipStatsCur;

        if (m_textShipStats1 != null)
        {
            m_textShipStats1.text =
                $"<sprite name=\"IconAttack\"> {statsCur.attack_power:F0}  " +
                $"<sprite name=\"IconHp\"> {statsCur.health_power:F0}/{statsOrg.health_power:F0}  " +
                $"<sprite name=\"IconSpeed\"> {statsCur.speed_power:F0}  " +
                $"<sprite name=\"IconRepair\"> {statsCur.repair_power:F0}";
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_textShipStats1.transform.parent as RectTransform);
        }

        if (m_textShipStats2 != null)
        {
            bool hasAircraft = statsOrg.aircraft_count > 0;
            m_textShipStats2.gameObject.SetActive(hasAircraft);
            if (hasAircraft)
            {
                m_textShipStats2.text =
                    $"<sprite name=\"IconAircraftAttack\"> {statsCur.aircraft_attack_power:F0}  " +
                    $"<sprite name=\"IconAircraft\"> {statsCur.aircraft_count}/{statsOrg.aircraft_count}  " +
                    $"<sprite name=\"IconLaunch\"> {statsCur.aircraft_launch_count}";
                LayoutRebuilder.ForceRebuildLayoutImmediate(m_textShipStats2.transform.parent as RectTransform);
            }
        }
    }

    private void OnShipStatsDetailClicked()
    {
        if (m_selectedShip == null) return;

        CapabilityProfile org = m_selectedShip.m_spaceShipStatsOrg;
        CapabilityProfile cur = m_selectedShip.m_spaceShipStatsCur;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<sprite name=\"IconAttack\"> (Attack)  {cur.attack_power:F0}");
        sb.AppendLine();
        sb.AppendLine($"<sprite name=\"IconHp\"> (HP)  {cur.health_power:F0} / {org.health_power:F0}");
        sb.AppendLine();
        sb.AppendLine($"<sprite name=\"IconSpeed\"> (Speed)  {cur.speed_power:F0}");
        sb.AppendLine();
        sb.Append    ($"<sprite name=\"IconRepair\"> (Repair)  {cur.repair_power:F0}");

        if (org.aircraft_count > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine($"<sprite name=\"IconAircraftAttack\"> (Aircraft Attack)  {cur.aircraft_attack_power:F0}");
            sb.AppendLine();
            sb.AppendLine($"<sprite name=\"IconAircraft\"> (Aircraft)  {cur.aircraft_count:F0} / {org.aircraft_count:F0}");
            sb.AppendLine();
            sb.Append    ($"<sprite name=\"IconLaunch\"> (Aircraft Launch)  {cur.aircraft_launch_count:F0}");
        }

        UIManager.Instance.ShowAlertPopup(m_selectedShip.m_shipInfo.shipName, sb.ToString(), null);
    }

    // ─────────────────────────────────────────────
    // 모듈 해금
    // ─────────────────────────────────────────────

    private void OnUnlockModuleClicked()
    {
        if (m_myCharacter == null)
        {
            ShowResultMessage("Character data not available", 3f);
            return;
        }

        if (m_selectedShip == null || m_selectedModule == null)
        {
            ShowResultMessage("No ship or module selected", 3f);
            return;
        }

        if ((m_selectedModule is ModulePlaceholder) == false)
        {
            ShowResultMessage("Selected module is not a placeholder", 3f);
            return;
        }

        int unlockPrice = DataManager.Instance.m_dataTableConfig.gameSettings.moduleUnlockPrice;
        CostStruct cost = new CostStruct { mineral = unlockPrice };
        string slotTypeName = LocalizationManager.Instance.Get($"module_type_{m_selectedModule.GetModuleType().ToLocKey()}");
        string detailText = m_selectedModule.GetDetailText(1, 1);

        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("ship_module_unlock"),
            LocalizationManager.Instance.Get("popup_message_module_unlock", new object[] { slotTypeName }),
            detailText, cost,
            () => ExecuteUnlockModule()
        );
    }

    private void ExecuteUnlockModule()
    {
        int unlockPrice = DataManager.Instance.m_dataTableConfig.gameSettings.moduleUnlockPrice;
        long playerMineral = m_myCharacter.GetMineral();
        if (playerMineral < unlockPrice)
        {
            ShowResultMessage($"Insufficient mineral (need {CommonUtility.FormatBigNumber(unlockPrice)}, have {CommonUtility.FormatBigNumber(playerMineral)})", 3f);
            return;
        }

        var unlockRequest = new ModuleUnlockRequest
        {
            shipId     = m_selectedShip.m_shipInfo.id,
            bodyIndex  = m_selectedModule.GetModuleBodyIndex(),
            moduleType = m_selectedModule.m_moduleSlot.m_moduleSlotInfo.moduleType,
            slotIndex  = m_selectedModule.m_moduleSlot.m_moduleSlotInfo.slotIndex
        };

        NetworkManager.Instance.UnlockModule(unlockRequest, OnUnlockModuleResponse);
    }

    private void OnUnlockModuleResponse(ApiResponse<ModuleUnlockResponse> response)
    {
        if (response.errorCode == 0)
            UpdateModuleAfterUnlock(response.data);
        else
            ShowResultMessage($"Module unlock failed: {ErrorCodeMapping.GetMessage(response.errorCode)}", 3f);
    }

    private void UpdateModuleAfterUnlock(ModuleUnlockResponse unlockData)
    {
        if (unlockData == null) return;

        Character character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        if (unlockData.costRemainInfo != null)
        {
            character.UpdateMineral(unlockData.costRemainInfo.remainMineral);
            character.UpdateMineralRare(unlockData.costRemainInfo.remainMineralRare);
            character.UpdateMineralExotic(unlockData.costRemainInfo.remainMineralExotic);
            character.UpdateMineralDark(unlockData.costRemainInfo.remainMineralDark);
            DataManager.Instance.SaveCharacterInfoToPlayerPrefs();
        }

        SpaceFleet fleet = character.GetOwnedFleet();
        if (fleet == null) return;
        SpaceShip targetShip = fleet.FindShip(unlockData.shipId);
        if (targetShip == null) return;

        targetShip.Apply_UnlockModule(unlockData.bodyIndex, unlockData.moduleType, unlockData.moduleSubType, unlockData.slotIndex);
        ShowResultMessage("Module unlock successful!", 3f);

        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == unlockData.shipId)
        {
            PopulateModuleSelectButtons();
            ReselectReplacedModule(targetShip, unlockData.bodyIndex, unlockData.moduleType, unlockData.moduleSubType, unlockData.slotIndex);
        }
    }

    // ─────────────────────────────────────────────
    // 모듈 레벨업
    // ─────────────────────────────────────────────

    private void OnLevelUpModuleClicked()
    {
        if (m_selectedShip == null || m_selectedModule == null) return;
        if (m_selectedModule is ModulePlaceholder == true) return;

        if (DataManager.Instance.GetModuleUpgradeCost(m_selectedModule.GetModuleSubType(), m_selectedModule.GetModuleLevel(), out CostStruct cost) == false)
        {
            ShowResultMessage("Failed to get upgrade cost", 3f);
            return;
        }

        string moduleSubTypeName = LocalizationManager.Instance.Get($"{m_selectedModule.GetModuleSubType().ToLocKey()}");
        int currentLevel = m_selectedModule.GetModuleLevel();
        int targetLevel  = currentLevel + 1;
        string detailText = m_selectedModule.GetDetailText(currentLevel, targetLevel);

        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("ship_module_levelup"),
            LocalizationManager.Instance.Get("popup_message_module_upgrade", new object[] { moduleSubTypeName, currentLevel, targetLevel }),
            detailText, cost,
            () => ExecuteUpgradeModule()
        );
    }

    private void ExecuteUpgradeModule()
    {
        if (CanUpgrade(out string validationMessage) == false)
        {
            ShowResultMessage($"Upgrade failed: {validationMessage}", 3f);
            return;
        }

        var upgradeRequest = new ModuleUpgradeRequest
        {
            shipId        = m_selectedShip.m_shipInfo.id,
            bodyIndex     = m_selectedModule.GetModuleBodyIndex(),
            moduleType    = m_selectedModule.GetModuleType(),
            moduleSubType = m_selectedModule.GetModuleSubType(),
            slotIndex     = m_selectedModule.GetSlotIndex(),
            currentLevel  = m_selectedModule.GetModuleLevel(),
            targetLevel   = m_selectedModule.GetModuleLevel() + 1
        };

        NetworkManager.Instance.UpgradeModule(upgradeRequest, OnUpgradeResponse);
    }

    private bool CanUpgrade(out string validationMessage)
    {
        validationMessage = "";

        if (m_selectedModule == null)
        {
            validationMessage = "No module selected";
            return false;
        }

        ModuleData upgradeStats = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(
            m_selectedModule.GetModuleSubType(), m_selectedModule.GetModuleLevel() + 1);
        if (upgradeStats == null)
        {
            validationMessage = "Max level reached";
            return false;
        }

        var character = DataManager.Instance.m_currentCharacter;
        if (character == null)
        {
            validationMessage = "Character data not available";
            return false;
        }

        if (DataManager.Instance.GetModuleUpgradeCost(m_selectedModule.GetModuleSubType(), m_selectedModule.GetModuleLevel(), out CostStruct cost) == false)
        {
            validationMessage = "Failed to get upgrade cost";
            return false;
        }

        int requiredTechTier = m_selectedModule.GetModuleSubType().GetTechTier();
        if (character.GetTechLevel() < requiredTechTier)
        {
            validationMessage = $"Insufficient tech level (need {requiredTechTier}, current {character.GetTechLevel()})";
            return false;
        }
        if (character.GetMineral() < cost.mineral)
        {
            validationMessage = $"Insufficient mineral (need {CommonUtility.FormatBigNumber(cost.mineral)}, have {CommonUtility.FormatBigNumber(character.GetMineral())})";
            return false;
        }
        if (character.GetMineralRare() < cost.mineralRare)
        {
            validationMessage = $"Insufficient mineralRare (need {CommonUtility.FormatBigNumber(cost.mineralRare)}, have {character.GetMineralRare()})";
            return false;
        }
        if (character.GetMineralExotic() < cost.mineralExotic)
        {
            validationMessage = $"Insufficient mineralExotic (need {CommonUtility.FormatBigNumber(cost.mineralExotic)}, have {CommonUtility.FormatBigNumber(character.GetMineralExotic())})";
            return false;
        }
        if (character.GetMineralDark() < cost.mineralDark)
        {
            validationMessage = $"Insufficient mineralDark (need {CommonUtility.FormatBigNumber(cost.mineralDark)}, have {CommonUtility.FormatBigNumber(character.GetMineralDark())})";
            return false;
        }

        return true;
    }

    private void OnUpgradeResponse(ApiResponse<ModuleUpgradeResponse> response)
    {
        Character character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        if (response.errorCode == 0)
        {
            if (response.data.costRemainInfo != null)
            {
                character.UpdateMineral(response.data.costRemainInfo.remainMineral);
                character.UpdateMineralRare(response.data.costRemainInfo.remainMineralRare);
                character.UpdateMineralExotic(response.data.costRemainInfo.remainMineralExotic);
                character.UpdateMineralDark(response.data.costRemainInfo.remainMineralDark);
                DataManager.Instance.SaveCharacterInfoToPlayerPrefs();
            }

            UpdateModuleAfterUpgrade(response.data);
            UpdateModuleStatsDisplay();
            ShowResultMessage("Upgrade successful!", 3f);
        }
        else
        {
            string errorMessage = ErrorCodeMapping.GetMessage(response.errorCode);
            Debug.LogError($"Upgrade failed: {errorMessage}");
            ShowResultMessage($"Upgrade failed: {errorMessage}", 3f);
        }
    }

    private void UpdateModuleAfterUpgrade(ModuleUpgradeResponse upgradeData)
    {
        if (upgradeData == null) return;
        if (m_myFleet == null) return;

        SpaceShip ship = m_myFleet.FindShip(upgradeData.shipId);
        if (ship == null) return;

        ship.Apply_ChangeModule(upgradeData.bodyIndex, upgradeData.moduleType, upgradeData.moduleSubType, upgradeData.slotIndex, upgradeData.newLevel);

        ShowResultMessage("Module Upgrade successful!", 3f);

        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == upgradeData.shipId)
        {
            PopulateModuleSelectButtons();
            ReselectReplacedModule(ship, upgradeData.bodyIndex, upgradeData.moduleType, upgradeData.moduleSubType, upgradeData.slotIndex);
        }
    }

    // ─────────────────────────────────────────────
    // 모듈 디테일 카드 갱신
    // ─────────────────────────────────────────────

    private void HideAllModuleStatRows()
    {
        for (int i = 0; i < m_moduleStatRows.Count; i++)
            m_moduleStatRows[i].Hide();
    }

    private void UpdateModuleStatsDisplay()
    {
        if (bShow != true) return;
        if (m_selectedShip == null) return;

        string localizationKeyModuleType = $"module_type_{m_selectedModule.GetModuleType()}";

        if (m_selectedModule is ModulePlaceholder)
        {
            m_unlockModuleButton.gameObject.SetActive(true);
            m_levelUpModuleButton.gameObject.SetActive(false);
            m_subTypeManageButton.gameObject.SetActive(false);

            HideAllModuleStatRows();
            m_moduleStatRows[0].SetRow(localizationKeyModuleType + "_placeholder", "");
            m_selectedModule.SetModuleStatRows(m_moduleStatRows);
        }
        else
        {
            m_unlockModuleButton.gameObject.SetActive(false);
            m_levelUpModuleButton.gameObject.SetActive(true);
            m_subTypeManageButton.gameObject.SetActive(true);

            EModuleSubType subType    = m_selectedModule.GetModuleSubType();
            int nextLevel             = m_selectedModule.GetModuleLevel() + 1;
            ModuleData moduleDataNext = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, nextLevel);
            bool isMaxLevel           = moduleDataNext == null;

            m_levelUpModuleButton.interactable = !isMaxLevel;

            if (m_levelUpModuleButtonText != null)
            {
                if (isMaxLevel)
                {
                    CommonUtility.SetUILocText(m_levelUpModuleButtonText, "max_level");
                }
                else if (DataManager.Instance.GetModuleUpgradeCost(subType, m_selectedModule.GetModuleLevel(), out CostStruct cost))
                {
                    string costStr = $"{CommonUtility.FormatBigNumber(cost.mineral)} M";
                    if (cost.mineralRare > 0) costStr += $" / {CommonUtility.FormatBigNumber(cost.mineralRare)} MR";
                    m_levelUpModuleButtonText.text = $"{LocalizationManager.Instance.Get("ship_module_levelup")} ({costStr})";
                }
                else
                {
                    CommonUtility.SetUILocText(m_levelUpModuleButtonText, "ship_module_levelup");
                }
            }

            m_subTypeManageButton.interactable = true;

            HideAllModuleStatRows();
            m_moduleStatRows[0].SetRow(localizationKeyModuleType, "");
            m_selectedModule.SetModuleStatRows(m_moduleStatRows);
        }
    }

    // ─────────────────────────────────────────────
    // 서브타입 관리
    // ─────────────────────────────────────────────

    private void OnSubTypeManageClicked()
    {
        if (m_selectedShip == null || m_selectedModule == null) return;
        if (m_selectedModule is ModulePlaceholder) return;

        UIManager.Instance.ShowModuleSubTypeManagePopup(m_selectedModule, OnModuleSubTypeSelected);
    }

    private void OnModuleSubTypeSelected(EModuleSubType newSubType)
    {
        if (m_selectedShip == null || m_selectedModule == null) return;
        if (newSubType == EModuleSubType.none) return;
        if (newSubType == m_selectedModule.GetModuleSubType()) return;

        int slotIndex = 0;
        if (m_selectedModule.GetModuleType() != EModuleType.body)
            slotIndex = m_selectedModule.m_moduleSlot.m_moduleSlotInfo.slotIndex;

        var changeRequest = new ModuleChangeRequest
        {
            shipId               = m_selectedShip.m_shipInfo.id,
            bodyIndex            = m_selectedModule.GetModuleBodyIndex(),
            slotIndex            = slotIndex,
            moduleType           = m_selectedModule.GetModuleType(),
            moduleSubTypeCurrent = m_selectedModule.GetModuleSubType(),
            moduleSubTypeNew     = newSubType
        };

        NetworkManager.Instance.ChangeModule(changeRequest, OnChangeModuleResponse);
    }

    private void OnChangeModuleResponse(ApiResponse<ModuleChangeResponse> response)
    {
        if (response.errorCode == 0)
            UpdateModuleAfterChange(response.data);
        else
            ShowResultMessage($"Module change failed: {ErrorCodeMapping.GetMessage(response.errorCode)}", 3f);
    }

    private void UpdateModuleAfterChange(ModuleChangeResponse changeData)
    {
        if (changeData == null) return;
        if (m_myFleet == null) return;

        SpaceShip ship = m_myFleet.FindShip(changeData.shipId);
        if (ship == null) return;

        ship.Apply_ChangeModule(changeData.bodyIndex, changeData.moduleTypeNew, changeData.moduleSubTypeNew, changeData.slotIndex, changeData.moduleNewLevel, changeData.newUnlockedSubTypes);

        if (changeData.costRemainInfo != null)
        {
            var character = DataManager.Instance.m_currentCharacter;
            if (character != null)
            {
                character.UpdateMineral(changeData.costRemainInfo.remainMineral);
                character.UpdateMineralRare(changeData.costRemainInfo.remainMineralRare);
                character.UpdateMineralExotic(changeData.costRemainInfo.remainMineralExotic);
                character.UpdateMineralDark(changeData.costRemainInfo.remainMineralDark);
            }
        }

        ShowResultMessage("Module change successful!", 3f);

        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == changeData.shipId)
        {
            PopulateModuleSelectButtons();
            ReselectReplacedModule(ship, changeData.bodyIndex, changeData.moduleTypeNew, changeData.moduleSubTypeNew, changeData.slotIndex);
        }
    }

    // ─────────────────────────────────────────────
    // 모듈 선택 버튼 생성 / 갱신
    // ─────────────────────────────────────────────

    private void PopulateModuleSelectButtons()
    {
        if (m_selectedShip == null) return;
        if (m_selectedShip.m_moduleBodys.Count == 0) return;

        for (int i = 0; i < m_moduleSelectorActive.Count; i++)
        {
            m_moduleSelectorActive[i].gameObject.SetActive(false);
            m_moduleSelectorActive[i].transform.SetParent(m_selectorPoolHolder, false);
        }
        m_moduleSelectorPool.AddRange(m_moduleSelectorActive);
        m_moduleSelectorActive.Clear();

        if (m_moduleMissileSelectButtonContainer2 != null) m_moduleMissileSelectButtonContainer2.gameObject.SetActive(false);
        if (m_moduleHangerSelectButtonContainer2  != null) m_moduleHangerSelectButtonContainer2.gameObject.SetActive(false);

        ModuleBody body = m_selectedShip.m_moduleBodys[0];

        CreateModuleSelectButton(body, m_moduleBodySelectButtonContainer);

        for (int i = 0; i < body.m_moduleSlots.Count; i++)
        {
            ModuleSlot slot = body.m_moduleSlots[i];
            if (slot == null || slot.transform.childCount == 0) continue;

            ModuleBase module = slot.GetComponentInChildren<ModuleBase>();
            if (module == null) continue;

            RectTransform container = GetContainerForSlot(slot.m_moduleSlotInfo.moduleType, slot.m_moduleSlotInfo.slotIndex);
            if (container == null) continue;

            container.gameObject.SetActive(true);
            CreateModuleSelectButton(module, container);
        }

        UpdateModuleSelectButtonSelection();
    }

    private RectTransform GetContainerForSlot(EModuleType moduleType, int slotIndex)
    {
        switch (moduleType)
        {
            case EModuleType.beam:    return m_moduleBeamSelectButtonContainer;
            case EModuleType.engine:  return m_moduleEngineSelectButtonContainer;
            case EModuleType.missile: return slotIndex < 2 ? m_moduleMissileSelectButtonContainer1 : m_moduleMissileSelectButtonContainer2;
            case EModuleType.hanger:  return slotIndex < 2 ? m_moduleHangerSelectButtonContainer1  : m_moduleHangerSelectButtonContainer2;
            default: return null;
        }
    }

    private void CreateModuleSelectButton(ModuleBase module, RectTransform container)
    {
        ModuleSelector selector;
        if (m_moduleSelectorPool.Count > 0)
        {
            selector = m_moduleSelectorPool[^1];
            m_moduleSelectorPool.RemoveAt(m_moduleSelectorPool.Count - 1);
            selector.gameObject.SetActive(true);
        }
        else
        {
            var go = Instantiate(m_moduleSelectButtonPrefab, container);
            selector = go.GetComponent<ModuleSelector>();
        }

        selector.transform.SetParent(container, false);
        selector.GetComponent<RectTransform>().sizeDelta = m_moduleSelectButtonPrefab.GetComponent<RectTransform>().sizeDelta;
        ModuleBase captured = module;
        selector.Initialize(module, () => OnModuleSelectorClicked(captured));
        m_moduleSelectorActive.Add(selector);
    }

    private void UpdateModuleSelectButtonSelection()
    {
        for (int i = 0; i < m_moduleSelectorActive.Count; i++)
            m_moduleSelectorActive[i].SetSelected(m_moduleSelectorActive[i].Module == m_selectedModule);
    }

    private void OnModuleSelectorClicked(ModuleBase module)
    {
        if (m_selectedShip == null || module == null) return;
        CameraController.Instance.FocusOnModuleIfHidden(module.m_moduleSlot);
        EventManager.TriggerSpaceShipModuleSelected(m_selectedShip, module);
    }

    // 모듈 교체/해금 후 새로 생성된 모듈을 다시 선택
    private void ReselectReplacedModule(SpaceShip targetShip, int bodyIndex, EModuleType moduleType, EModuleSubType moduleSubType, int slotIndex)
    {
        if (targetShip == null) return;

        ModuleBody body = targetShip.FindModuleBodyByIndex(bodyIndex);
        if (body == null) return;

        if (moduleType == EModuleType.body || slotIndex < 0)
        {
            m_selectedModule = body;
            EventManager.TriggerSpaceShipModuleSelected(targetShip, m_selectedModule);
            return;
        }

        ModuleSlot slot = body.FindModuleSlot(moduleType, slotIndex);
        if (slot != null && slot.transform.childCount > 0)
        {
            ModuleBase newModule = slot.GetComponentInChildren<ModuleBase>();
            if (newModule != null)
            {
                m_selectedModule = newModule;
                EventManager.TriggerSpaceShipModuleSelected(targetShip, m_selectedModule);
            }
        }
    }
}
