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

    [Header("모듈 맵 — 행 컨테이너 (레이블 + 셀렉터 포함)")]
    [SerializeField] private RectTransform m_moduleBodySelectButtonContainer;
    [SerializeField] private RectTransform m_moduleBeamSelectButtonContainer;
    [SerializeField] private RectTransform m_moduleMissileSelectButtonContainer;
    [SerializeField] private RectTransform m_moduleHangerSelectButtonContainer;
    

    [SerializeField] private RectTransform m_moduleSelectRoot; // CSF 루트 (Select 또는 ModuleSelect)

    [Header("모듈 맵 — 행 레이블 (sprite icon)")]
    [SerializeField] private TMP_Text m_labelBody;
    [SerializeField] private TMP_Text m_labelBeam;
    [SerializeField] private TMP_Text m_labelMissile;
    [SerializeField] private TMP_Text m_labelHanger;
    

    [Header("함선 스탯 디테일")]
    [SerializeField] private Button m_btnShipStatsDetail;

    [Header("모듈 디테일 카드")]
    [SerializeField] private TMP_Text  m_moduleStatsText;

    [SerializeField] private Button    m_unlockModuleButton;
    [SerializeField] private Button    m_levelUpModuleButton;
    [SerializeField] private TMP_Text  m_levelUpModuleButtonText;
    [SerializeField] private Button    m_subTypeManageButton;

    private bool bShow = false;

    private Character  m_myCharacter;
    private SpaceFleet m_myFleet;

    private SpaceShip  m_selectedShip;
    private ModuleBase m_selectedModule;


    // 행별 셀렉터 캐시 (prefab에 미리 배치된 버튼들)
    private ModuleSelector[] m_selectorsBody;
    private ModuleSelector[] m_selectorsBeam;
    private ModuleSelector[] m_selectorsMissile;
    private ModuleSelector[] m_selectorsHanger;
    


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

        m_selectorsBody    = m_moduleBodySelectButtonContainer.GetComponentsInChildren<ModuleSelector>(true);
        m_selectorsBeam    = m_moduleBeamSelectButtonContainer.GetComponentsInChildren<ModuleSelector>(true);
        m_selectorsMissile = m_moduleMissileSelectButtonContainer.GetComponentsInChildren<ModuleSelector>(true);
        m_selectorsHanger  = m_moduleHangerSelectButtonContainer.GetComponentsInChildren<ModuleSelector>(true);
        
        if (m_labelBody    != null) m_labelBody.text    = "<sprite name=\"IconSpaceShip\">";
        if (m_labelBeam    != null) m_labelBeam.text    = "<sprite name=\"IconBeam\">";
        if (m_labelMissile != null) m_labelMissile.text = "<sprite name=\"IconMissile\">";
        if (m_labelHanger  != null) m_labelHanger.text  = "<sprite name=\"IconAircraft\">";

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

        CameraController.Instance.SetTargetOfCameraController(m_selectedShip.transform);
        EventManager.TriggerSpaceShipModuleSelected(m_selectedShip, m_selectedModule);

        bShow = true;
        UpdateShipHeader();
        UpdateModuleStatsDisplay();
        PopulateModuleSelectButtons();

        if (m_moduleSelectRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_moduleSelectRoot);
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

        m_selectedShip = ship;
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

        m_selectedShip = ship;
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
                $"<sprite name=\"IconAttack\"> {statsCur.attack:F0}  " +
                $"<sprite name=\"IconHp\"> {statsOrg.health:F0}  " +
                $"<sprite name=\"IconSpeed\"> {statsCur.speed:F0}  " +
                $"<sprite name=\"IconRepair\"> {statsCur.repair:F0}";
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_textShipStats1.transform.parent as RectTransform);
        }

        if (m_textShipStats2 != null)
        {
            bool hasAircraft = statsOrg.airCount > 0;
            m_textShipStats2.gameObject.SetActive(hasAircraft);
            if (hasAircraft)
            {
                m_textShipStats2.text =
                    $"<sprite name=\"IconAircraftAttack\"> {statsCur.airAttack:F0}  " +
                    $"<sprite name=\"IconAircraft\"> {statsOrg.airCount}  " +
                    $"<sprite name=\"IconLaunch\"> {statsCur.airLaunchCount}";
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

        int currentLevel = m_selectedModule.GetModuleLevel();

        // 다음 레벨 데이터 없으면 이미 최대 레벨
        if (DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_selectedModule.GetModuleSubType(), currentLevel + 1) == null)
        {
            ShowResultMessage(LocalizationManager.Instance.Get("max_level"), 2f);
            return;
        }

        UIManager.Instance.ShowModuleLevelupPopup(
            m_selectedModule.GetModuleSubType(),
            m_selectedModule.GetModuleType(),
            currentLevel,
            OnModuleUpgradeConfirmed
        );
    }

    private void OnModuleUpgradeConfirmed(int targetLevel)
    {
        if (CanUpgradeToLevel(targetLevel, out string validationMessage) == false)
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
            targetLevel   = targetLevel
        };

        NetworkManager.Instance.UpgradeModule(upgradeRequest, OnUpgradeResponse);
    }

    private bool CanUpgradeToLevel(int targetLevel, out string validationMessage)
    {
        validationMessage = "";

        if (m_selectedModule == null)
        {
            validationMessage = "No module selected";
            return false;
        }

        // targetLevel 데이터 존재 여부 확인
        ModuleData targetData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(
            m_selectedModule.GetModuleSubType(), targetLevel);
        if (targetData == null)
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

        int requiredTechTier = m_selectedModule.GetModuleSubType().GetTechTier();
        if (character.GetTechLevel() < requiredTechTier)
        {
            validationMessage = $"Insufficient tech level (need {requiredTechTier}, current {character.GetTechLevel()})";
            return false;
        }

        // currentLevel → targetLevel 누적 비용 합산 후 검증
        int fromLevel = m_selectedModule.GetModuleLevel();
        long totalMineral = 0, totalMineralRare = 0, totalMineralExotic = 0, totalMineralDark = 0;
        for (int lv = fromLevel; lv < targetLevel; lv++)
        {
            if (DataManager.Instance.GetModuleUpgradeCost(m_selectedModule.GetModuleSubType(), lv, out CostStruct cost) == false)
            {
                validationMessage = "Failed to get upgrade cost";
                return false;
            }
            totalMineral       += cost.mineral;
            totalMineralRare   += cost.mineralRare;
            totalMineralExotic += cost.mineralExotic;
            totalMineralDark   += cost.mineralDark;
        }

        if (character.GetMineral() < totalMineral)
        {
            validationMessage = $"Insufficient mineral";
            return false;
        }
        if (character.GetMineralRare() < totalMineralRare)
        {
            validationMessage = $"Insufficient mineralRare";
            return false;
        }
        if (character.GetMineralExotic() < totalMineralExotic)
        {
            validationMessage = $"Insufficient mineralExotic";
            return false;
        }
        if (character.GetMineralDark() < totalMineralDark)
        {
            validationMessage = $"Insufficient mineralDark";
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
        EventManager.Trigger_ShipStatsChanged(ship);

        ShowResultMessage("Module Upgrade successful!", 3f);

        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == upgradeData.shipId)
        {
            UpdateShipHeader();
            PopulateModuleSelectButtons();
            ReselectReplacedModule(ship, upgradeData.bodyIndex, upgradeData.moduleType, upgradeData.moduleSubType, upgradeData.slotIndex);
        }
    }

    // ─────────────────────────────────────────────
    // 모듈 디테일 카드 갱신
    // ─────────────────────────────────────────────

    private void UpdateModuleStatsDisplay()
    {
        if (bShow != true) return;
        if (m_selectedShip == null) return;

        if (m_selectedModule is ModulePlaceholder)
        {
            m_unlockModuleButton.gameObject.SetActive(true);
            m_levelUpModuleButton.gameObject.SetActive(false);
            m_subTypeManageButton.gameObject.SetActive(false);

            if (m_moduleStatsText != null)
            {
                m_moduleStatsText.text = LocalizationManager.Instance.Get($"module_type_{m_selectedModule.GetModuleType()}_placeholder");
                LayoutRebuilder.ForceRebuildLayoutImmediate(m_moduleStatsText.transform.parent as RectTransform);
            }
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

            if (m_moduleStatsText != null)
            {
                int level = m_selectedModule.GetModuleLevel();
                string typeName = LocalizationManager.Instance.Get(m_selectedModule.GetModuleSubType().ToLocKey());
                m_moduleStatsText.text = typeName + "\n\n" + m_selectedModule.GetDetailText(level, level);
                LayoutRebuilder.ForceRebuildLayoutImmediate(m_moduleStatsText.transform.parent as RectTransform);
            }
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

        ModuleBody body = m_selectedShip.m_moduleBodys[0];

        // Body 행: 슬롯 1개 고정
        RefreshRow(EModuleType.body, body, m_selectorsBody, m_moduleBodySelectButtonContainer);

        // 나머지 타입 행
        RefreshRow(EModuleType.beam,    body, m_selectorsBeam,    m_moduleBeamSelectButtonContainer);
        RefreshRow(EModuleType.missile, body, m_selectorsMissile, m_moduleMissileSelectButtonContainer);
        RefreshRow(EModuleType.hanger,  body, m_selectorsHanger,  m_moduleHangerSelectButtonContainer);

        UpdateModuleSelectButtonSelection();

        if (m_moduleSelectRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_moduleSelectRoot);
    }

    private void RefreshRow(EModuleType type, ModuleBody body, ModuleSelector[] selectors, RectTransform container)
    {
        if (selectors == null) return;

        // 타입별 슬롯 수집
        int slotCount;
        ModuleBase[] modules;

        if (type == EModuleType.body)
        {
            slotCount = 1;
            modules = new ModuleBase[] { body };
        }
        else
        {
            var slots = body.m_moduleSlots.FindAll(s => s.m_moduleSlotInfo.moduleType == type);
            slotCount = slots.Count;
            modules = new ModuleBase[slotCount];
            for (int i = 0; i < slotCount; i++)
                modules[i] = slots[i].transform.childCount > 0
                    ? slots[i].GetComponentInChildren<ModuleBase>()
                    : null;
        }

        // 슬롯이 없으면 행 전체 숨김
        container.gameObject.SetActive(slotCount > 0);

        for (int i = 0; i < selectors.Length; i++)
        {
            if (i >= slotCount || modules[i] == null)
            {
                selectors[i].gameObject.SetActive(false);
                continue;
            }

            selectors[i].gameObject.SetActive(true);
            ModuleBase captured = modules[i];
            selectors[i].Initialize(captured, () => OnModuleSelectorClicked(captured));
        }
    }

    private void UpdateModuleSelectButtonSelection()
    {
        UpdateRowSelection(m_selectorsBody);
        UpdateRowSelection(m_selectorsBeam);
        UpdateRowSelection(m_selectorsMissile);
        UpdateRowSelection(m_selectorsHanger);
    }

    private void UpdateRowSelection(ModuleSelector[] selectors)
    {
        if (selectors == null) return;
        for (int i = 0; i < selectors.Length; i++)
        {
            if (selectors[i].gameObject.activeSelf)
                selectors[i].SetSelected(selectors[i].Module == m_selectedModule);
        }
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
