// 함선/모듈 관리 UI — 모듈 레벨업, 서브타입 교체(팝업), 슬롯 해금 처리
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITabShip : UITabBase
{
    [SerializeField] private TMP_Text  m_textShipStatus;
    [SerializeField] private RectTransform m_shipStatsContainer;
    [SerializeField] private GameObject m_rowLabelValuePrefab;                // RowLabelValue 프리팹
    
    [SerializeField] private TMP_Text  m_textModuleSelect;
    [SerializeField] private RectTransform m_moduleBeamSelectButtonContainer;
    [SerializeField] private RectTransform m_moduleMissileSelectButtonContainer1;
    [SerializeField] private RectTransform m_moduleMissileSelectButtonContainer2;
    [SerializeField] private RectTransform m_moduleHangerSelectButtonContainer1;
    [SerializeField] private RectTransform m_moduleHangerSelectButtonContainer2;
    [SerializeField] private RectTransform m_moduleBodySelectButtonContainer;
    [SerializeField] private RectTransform m_moduleEngineSelectButtonContainer;

    [SerializeField] private GameObject m_moduleSelectButtonPrefab;
    

    [SerializeField] private TMP_Text  m_textModuleStatus;
    [SerializeField] private RectTransform m_moduleStatsContainer;

    [SerializeField] private Button m_unlockModuleButton;
    [SerializeField] private Button m_levelUpModuleButton;
    [SerializeField] private TMP_Text m_levelUpModuleButtonText;
    [SerializeField] private Button m_subTypeManageButton;

    private bool bShow = false;

    private Character m_myCharacter;
    private SpaceFleet m_myFleet;
    
    private SpaceShip m_selectedShip;
    private ModuleBase m_selectedModule;

    private readonly Dictionary<string, RowLabelValue> m_shipStatRows = new();

    private readonly List<RowLabelValue> m_moduleStatRows = new();

    // 모듈 선택 버튼 풀 (단일 풀, 컨테이너 무관)
    private readonly List<ModuleSelector> m_moduleSelectorPool = new();
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

        m_shipStatRows.Clear();

        // m_moduleStatsContainer 내부의 자식들을 하이어라키 순서대로 캐싱
        if (m_moduleStatsContainer != null)
        {
            m_moduleStatRows.Clear();
            for (int i = 0; i < m_moduleStatsContainer.childCount; i++)
            {
                Transform child = m_moduleStatsContainer.GetChild(i);
                var row = child.GetComponent<RowLabelValue>();
                if (row != null)
                    m_moduleStatRows.Add(row);
            }
        }

        var poolHolderGO = new GameObject("_ModuleSelectorPool");
        poolHolderGO.transform.SetParent(transform, false);
        m_selectorPoolHolder = poolHolderGO.transform;

        m_unlockModuleButton.onClick.AddListener(OnUnlockModuleClicked);
        m_levelUpModuleButton.onClick.AddListener(OnLevelUpModuleClicked);
        m_subTypeManageButton.onClick.AddListener(OnSubTypeManageClicked);
        
        EventManager.Subscribe_SpaceShipSelected(OnSpaceShipSelected);
        EventManager.Subscribe_ShipUpdateHP(UpdateShipStatsDisplay);
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
        UpdateShipStatsDisplay();
        UpdateModuleStatsDisplay();
        PopulateModuleSelectButtons();
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
        
        bShow = false;
        
        if (m_myFleet != null)
            m_myFleet.ClearAllSelectedModule();

        //CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    // 함선 선택 처리 (3D 클릭 + UI 버튼 양쪽에서 호출됨)
    private void OnSpaceShipSelected(SpaceShip ship)
    {
        if (m_selectedShip == ship) return;

        // 이전 함선 아웃라인 해제
        if (m_selectedShip != null)
            m_selectedShip.m_shipOutline.enabled = false;

        m_selectedShip = ship;
        m_selectedShip.m_shipOutline.enabled = true;
        CameraController.Instance.SetTargetOfCameraController(m_selectedShip.transform);

        if (m_myFleet != null)
            m_myFleet.ClearAllSelectedModule();

        // 기본 모듈 선택
        m_selectedModule = null;
        if (ship.m_moduleBodys[0].m_beams.Count > 0)
            m_selectedModule = ship.m_moduleBodys[0].m_beams[0];
        else if (ship.m_moduleBodys[0].m_missiles.Count > 0)
            m_selectedModule = ship.m_moduleBodys[0].m_missiles[0];
        else
            m_selectedModule = ship.m_moduleBodys[0];


        // 탭이 활성화 상태일 때만 UI 갱신
        if (bShow)
        {
            UpdateShipStatsDisplay();
            UpdateModuleStatsDisplay();
            PopulateModuleSelectButtons();
        }
    }
    private void OnSpaceShipModuleSelected(SpaceShip ship, ModuleBase module)
    {
        if (module == null) return;
        if (m_myFleet == null) return;
        // CameraController가 ship 이벤트를 먼저 발생시키므로 이미 전환됨
        if (m_selectedShip != ship) return;

        m_selectedModule = module;
        m_selectedShip.SetSelectedModule(ship, module);

        if (bShow)
        {
            UpdateShipStatsDisplay();
            UpdateModuleStatsDisplay();
            UpdateModuleSelectButtonSelection();
        }
    }

    private void UpdateShipStatsDisplay()
    {
        if (m_selectedShip == null)
            return;

        CapabilityProfile statsOrg = m_selectedShip.m_spaceShipStatsOrg;
        CapabilityProfile statsCur = m_selectedShip.m_spaceShipStatsCur;

        SetOrCreateShipStatRow("ship_module_weapon_count", $"{statsCur.totalWeapons}");
        SetOrCreateShipStatRow("ship_module_engine_count", $"{statsCur.totalEngines}");
        SetOrCreateShipStatRow("attack_power", $"{statsCur.attack_power:F0}/{statsOrg.attack_power:F0}");
        SetOrCreateShipStatRow("health_power", $"{statsCur.health_power:F0}/{statsOrg.health_power:F0}");
        SetOrCreateShipStatRow("speed_power", $"{statsCur.speed_power:F0}/{statsOrg.speed_power:F0}");
        SetOrCreateShipStatRow("repair_power", $"{statsCur.repair_power:F0}/{statsOrg.repair_power:F0}");
        SetOrCreateShipStatRow("aircraft_attack_power", $"{statsCur.aircraft_attack_power:F0}/{statsOrg.aircraft_attack_power:F0}");
        SetOrCreateShipStatRow("aircraft_count", $"{statsCur.aircraft_count:F0}/{statsOrg.aircraft_count:F0}");
        SetOrCreateShipStatRow("aircraft_launch_count", $"{statsCur.aircraft_launch_count:F0}/{statsOrg.aircraft_launch_count:F0}");
    }

    private void SetOrCreateShipStatRow(string label, string value)
    {
        if (m_shipStatsContainer == null || m_rowLabelValuePrefab == null)
            return;

        if (m_shipStatRows.TryGetValue(label, out RowLabelValue existingRow) == true)
        {
            existingRow.SetValues(value);
            return;
        }

        GameObject rowObj = Instantiate(m_rowLabelValuePrefab, m_shipStatsContainer);
        rowObj.name = $"ShipRow_{label}";

        RowLabelValue row = rowObj.GetComponent<RowLabelValue>();
        if (row != null)
        {
            row.SetRow(label, value);
            m_shipStatRows.Add(label, row);
        }
    }
    
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

        if ((m_selectedModule is ModulePlaceholder) == false )
        {
            ShowResultMessage("Selected module is not a placeholder", 3f);
            return;
        }

        // 확인 팝업 표시
        int unlockPrice = DataManager.Instance.m_dataTableConfig.gameSettings.moduleUnlockPrice;
        CostStruct cost = new CostStruct { mineral = unlockPrice };
        string slotTypeName = LocalizationManager.Instance.Get($"module_type_{m_selectedModule.GetModuleType().ToLocKey()}");
        m_selectedModule.SetModuleStatRows(out List<string> leftLabels, out List<string> leftValues, showNext: false);

        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("ship_module_unlock"),
            LocalizationManager.Instance.Get("popup_message_module_unlock", new object[] { slotTypeName }),
            leftLabels, leftValues, cost,
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

        // 모듈 해금 요청 생성
        var unlockRequest = new ModuleUnlockRequest
        {
            shipId = m_selectedShip.m_shipInfo.id,
            bodyIndex = m_selectedModule.GetModuleBodyIndex(),
            moduleType = m_selectedModule.m_moduleSlot.m_moduleSlotInfo.moduleType,
            slotIndex = m_selectedModule.m_moduleSlot.m_moduleSlotInfo.slotIndex
        };

        // 서버에 모듈 해금 요청 전송
        NetworkManager.Instance.UnlockModule(unlockRequest, OnUnlockModuleResponse);
    }

    private void OnUnlockModuleResponse(ApiResponse<ModuleUnlockResponse> response)
    {
        if (response.errorCode == 0)
        {
            UpdateModuleAfterUnlock(response.data);
        }
        else
        {
            // 실패 메시지 표시
            string errorMessage = ErrorCodeMapping.GetMessage(response.errorCode);
            ShowResultMessage($"Module unlock failed: {errorMessage}", 3f);
        }
    }

    private void UpdateModuleAfterUnlock(ModuleUnlockResponse unlockData)
    {
        if (unlockData == null) return;

        Character character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        // 자원 업데이트
        if (unlockData.costRemainInfo != null)
        {
            character.UpdateMineral(unlockData.costRemainInfo.remainMineral);
            character.UpdateMineralRare(unlockData.costRemainInfo.remainMineralRare);
            character.UpdateMineralExotic(unlockData.costRemainInfo.remainMineralExotic);
            character.UpdateMineralDark(unlockData.costRemainInfo.remainMineralDark);
            DataManager.Instance.SaveCharacterInfoToPlayerPrefs();
        }

        // 함선 찾기
        SpaceFleet fleet = character.GetOwnedFleet();
        if (fleet == null) return;
        SpaceShip targetShip = fleet.FindShip(unlockData.shipId);
        if (targetShip == null) return;

        // 모듈 해금 처리
        targetShip.Apply_UnlockModule(unlockData.bodyIndex, unlockData.moduleType, unlockData.moduleSubType, unlockData.slotIndex);

        // 성공 메시지 표시
        ShowResultMessage("Module unlock successful!", 3f);

        // 현재 선택된 함선 모듈이 업데이트된 함선 모듈과 같다면 모듈 재선택
        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == unlockData.shipId)
        {
            PopulateModuleSelectButtons();
            ReselectReplacedModule(targetShip, unlockData.bodyIndex, unlockData.moduleType, unlockData.moduleSubType, unlockData.slotIndex);
        }
    }

    private void OnLevelUpModuleClicked()
    {
        if (m_selectedShip == null || m_selectedModule == null) return;
        if (m_selectedModule is ModulePlaceholder == true) return;

        // 업그레이드 비용 가져오기
        if (!DataManager.Instance.GetModuleUpgradeCost(m_selectedModule.GetModuleSubType(), m_selectedModule.GetModuleLevel(), out CostStruct cost))
        {
            ShowResultMessage("Failed to get upgrade cost", 3f);
            return;
        }

        string moduleSubTypeName = LocalizationManager.Instance.Get($"{m_selectedModule.GetModuleSubType().ToLocKey()}");
        int currentLevel = m_selectedModule.GetModuleLevel();
        int targetLevel = currentLevel + 1;
        m_selectedModule.SetModuleStatRows(out List<string> leftLabels, out List<string> leftValues, showNext: true);

        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("ship_module_levelup"),
            LocalizationManager.Instance.Get("popup_message_module_upgrade", new object[] { moduleSubTypeName, currentLevel, targetLevel }),
            leftLabels, leftValues, cost,
            () => ExecuteUpgradeModule()
        );
    }

    private void ExecuteUpgradeModule()
    {
        // Validate resources and upgrade availability
        if (!CanUpgrade(out string validationMessage))
        {
            ShowResultMessage($"Upgrade failed: {validationMessage}", 3f);
            return;
        }

        string partsInfo = GetPartsUpgradeInfo(m_selectedModule);
        Debug.Log($"Requesting upgrade for {partsInfo} on ship {m_selectedShip.name}");

        var upgradeRequest = new ModuleUpgradeRequest
        {
            shipId = m_selectedShip.m_shipInfo.id
            ,bodyIndex = m_selectedModule.GetModuleBodyIndex()
            ,moduleType = m_selectedModule.GetModuleType()
            ,moduleSubType = m_selectedModule.GetModuleSubType()
            ,slotIndex = m_selectedModule.GetSlotIndex()
            ,currentLevel = m_selectedModule.GetModuleLevel()
            ,targetLevel = m_selectedModule.GetModuleLevel() + 1
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

        ModuleData upgradeStats = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_selectedModule.GetModuleSubType(), m_selectedModule.GetModuleLevel() + 1);
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

        CostStruct cost;
        if (DataManager.Instance.GetModuleUpgradeCost(m_selectedModule.GetModuleSubType(), m_selectedModule.GetModuleLevel(), out cost) == false)
        {
            validationMessage = "Failed to get upgrade cost";
            return false;
        }

        int playerTechLevel = character.GetTechLevel();
        long playerMineral = character.GetMineral();
        long playerMineralRare = character.GetMineralRare();
        long playerMineralExotic = character.GetMineralExotic();
        long playerMineralDark = character.GetMineralDark();
    
        if (playerTechLevel < cost.techLevel)
        {
            validationMessage = $"Insufficient tech level (need {cost.techLevel} tech level, current {playerTechLevel})";
            return false;
        }
        if (playerMineral < cost.mineral)
        {
            validationMessage = $"Insufficient mineral (need {CommonUtility.FormatBigNumber(cost.mineral)}, have {CommonUtility.FormatBigNumber(playerMineral)})";
            return false;
        }
        if (playerMineralRare < cost.mineralRare)
        {
            validationMessage = $"Insufficient mineralRare (need {CommonUtility.FormatBigNumber(cost.mineralRare)}, have {playerMineralRare})";
            return false;
        }
        if (playerMineralExotic < cost.mineralExotic)
        {
            validationMessage = $"Insufficient mineralExotic (need {CommonUtility.FormatBigNumber(cost.mineralExotic)}, have {CommonUtility.FormatBigNumber(playerMineralExotic)})";
            return false;
        }
        if (playerMineralDark < cost.mineralDark)
        {
            validationMessage = $"Insufficient mineralDark (need {CommonUtility.FormatBigNumber(cost.mineralDark)}, have {CommonUtility.FormatBigNumber(playerMineralDark)})";
            return false;
        }

        return true;
    }

    private string GetPartsUpgradeInfo(ModuleBase moduleBase)
    {
        if (moduleBase is ModuleBody body)
            return $"ModuleBody[{body.m_moduleBodyInfo.bodyIndex}]";
        else if (moduleBase is ModuleBeam beam)
            return $"ModuleBeam[{beam.m_classId}]";
        else if (moduleBase is ModuleMissile missile)
            return $"ModuleMissile[{missile.m_classId}]";
        else if (moduleBase is ModuleEngine engine)
            return $"ModuleEngine[{engine.m_classId}]";
        else if (moduleBase is ModuleHanger hanger)
            return $"ModuleHanger[{hanger.m_classId}]";
        else
            return $"{moduleBase.GetType().Name}[{moduleBase.m_classId}]";
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

            // Update local data - shipId, bodyIndex, moduleTypePacked, slotIndex로 특정 모듈 찾아서 업데이트
            UpdateModuleAfterUpgrade(response.data);
            
            // Refresh UI
            UpdateModuleStatsDisplay();

            // Show success message
            ShowResultMessage("Upgrade successful!", 3f);
        }
        else
        {
            string errorMessage = ErrorCodeMapping.GetMessage(response.errorCode);
            Debug.LogError($"Upgrade failed: {errorMessage}");

            // Show error message
            ShowResultMessage($"Upgrade failed: {errorMessage}", 3f);
        }
    }

    private void UpdateModuleAfterUpgrade(ModuleUpgradeResponse upgradeData)
    {
        if (upgradeData == null) return;
        if (m_myFleet == null) return;

        // SpaceShip 찾기
        SpaceShip ship = m_myFleet.FindShip(upgradeData.shipId);
        if (ship == null) return;
        
        ship.Apply_ChangeModule(upgradeData.bodyIndex, upgradeData.moduleType, upgradeData.moduleSubType, upgradeData.slotIndex, upgradeData.newLevel);

        ShowResultMessage("Module Upgrade successful!", 3f);

        // 새로 생성된 모듈 재선택 (버튼 람다가 파괴된 오브젝트를 캡처하므로 반드시 버튼 먼저 갱신)
        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == upgradeData.shipId)
        {
            PopulateModuleSelectButtons();
            ReselectReplacedModule(ship, upgradeData.bodyIndex, upgradeData.moduleType, upgradeData.moduleSubType, upgradeData.slotIndex);
        }
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

            m_moduleStatRows[0].SetRow(localizationKeyModuleType + "_placeholder", "");
            m_selectedModule.SetModuleStatRows(m_moduleStatRows);
        }
        else
        {
            m_unlockModuleButton.gameObject.SetActive(false);
            m_levelUpModuleButton.gameObject.SetActive(true);
            m_subTypeManageButton.gameObject.SetActive(true);

            EModuleSubType subType = m_selectedModule.GetModuleSubType();
            int nextLevel = m_selectedModule.GetModuleLevel() + 1;
            ModuleData moduleDataNext = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, nextLevel);
            bool isMaxLevel = moduleDataNext == null;

            m_levelUpModuleButton.interactable = !isMaxLevel;
            if (m_levelUpModuleButtonText != null)
                CommonUtility.SetUILocText(m_levelUpModuleButtonText, isMaxLevel ? "max_level" : "ship_module_levelup");

            m_subTypeManageButton.interactable = true;

            m_moduleStatRows[0].SetRow(localizationKeyModuleType, "");
            m_selectedModule.SetModuleStatRows(m_moduleStatRows);
        }
    }


    private void OnSubTypeManageClicked()
    {
        if (m_selectedShip == null || m_selectedModule == null) return;
        if (m_selectedModule is ModulePlaceholder) return;

        UIManager.Instance.ShowModuleSubTypeManagePopup(m_selectedModule, OnModuleSubTypeSelected);
    }

    // 팝업에서 서브타입 선택 후 콜백 — 서버에 교체 요청 전송
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
            shipId = m_selectedShip.m_shipInfo.id,
            bodyIndex = m_selectedModule.GetModuleBodyIndex(),
            slotIndex = slotIndex,
            moduleType = m_selectedModule.GetModuleType(),
            moduleSubTypeCurrent = m_selectedModule.GetModuleSubType(),
            moduleSubTypeNew = newSubType
        };

        NetworkManager.Instance.ChangeModule(changeRequest, OnChangeModuleResponse);
    }

    private void OnChangeModuleResponse(ApiResponse<ModuleChangeResponse> response)
    {
        if (response.errorCode == 0)
        {
            UpdateModuleAfterChange(response.data);
        }
        else
        {
            string errorMessage = ErrorCodeMapping.GetMessage(response.errorCode);
            Debug.Log($"Module change failed: {errorMessage}");
            ShowResultMessage($"Module change failed: {errorMessage}", 3f);
        }
    }

    private void UpdateModuleAfterChange(ModuleChangeResponse changeData)
    {
        if (changeData == null) return;
        if (m_myFleet == null) return;

        SpaceShip ship = m_myFleet.FindShip(changeData.shipId);
        if (ship == null) return;

        ship.Apply_ChangeModule(changeData.bodyIndex, changeData.moduleTypeNew, changeData.moduleSubTypeNew, changeData.slotIndex, changeData.moduleNewLevel, changeData.newUnlockedSubTypes);

        // 재화 잔액 갱신
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

    // 선택된 함선의 모듈 목록을 컨테이너에 버튼으로 생성
    private void PopulateModuleSelectButtons()
    {
        if (m_selectedShip == null) return;
        if (m_selectedShip.m_moduleBodys.Count == 0) return;

        // 전체 반환 — pool holder로 이동하여 컨테이너 sibling 순서 오염 방지
        for (int i = 0; i < m_moduleSelectorActive.Count; i++)
        {
            m_moduleSelectorActive[i].gameObject.SetActive(false);
            m_moduleSelectorActive[i].transform.SetParent(m_selectorPoolHolder, false);
        }
        m_moduleSelectorPool.AddRange(m_moduleSelectorActive);
        m_moduleSelectorActive.Clear();

        ModuleBody body = m_selectedShip.m_moduleBodys[0];

        // 바디 (단일)
        CreateModuleSelectButton(body, m_moduleBodySelectButtonContainer);

        // 슬롯 순회 — placeholder 포함 모든 모듈 생성
        for (int i = 0; i < body.m_moduleSlots.Count; i++)
        {
            ModuleSlot slot = body.m_moduleSlots[i];
            if (slot == null || slot.transform.childCount == 0) continue;

            ModuleBase module = slot.GetComponentInChildren<ModuleBase>();
            if (module == null) continue;

            RectTransform container = GetContainerForSlot(slot.m_moduleSlotInfo.moduleType, slot.m_moduleSlotInfo.slotIndex);
            if (container == null) continue;

            CreateModuleSelectButton(module, container);
        }

        UpdateModuleSelectButtonSelection();
    }

    private RectTransform GetContainerForSlot(EModuleType moduleType, int slotIndex)
    {
        switch (moduleType)
        {
            case EModuleType.beam:
                return m_moduleBeamSelectButtonContainer;
            case EModuleType.engine:
                return m_moduleEngineSelectButtonContainer;
            case EModuleType.missile:
                return slotIndex < 2 ? m_moduleMissileSelectButtonContainer1 : m_moduleMissileSelectButtonContainer2;
            case EModuleType.hanger:
                return slotIndex < 2 ? m_moduleHangerSelectButtonContainer1 : m_moduleHangerSelectButtonContainer2;
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
        // 풀 재사용 시 이전 컨테이너 크기가 남으므로 프리팹 기본값으로 리셋
        selector.GetComponent<RectTransform>().sizeDelta = m_moduleSelectButtonPrefab.GetComponent<RectTransform>().sizeDelta;
        ModuleBase captured = module;
        selector.Initialize(module, () => OnModuleSelectorClicked(captured));
        m_moduleSelectorActive.Add(selector);
    }

    // 현재 선택된 모듈과 매칭되는 버튼만 테두리 활성화
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

    // 모듈 교체/해금 후 새로 생성된 모듈을 다시 선택하여 selectedModuleVisual 적용
    private void ReselectReplacedModule(SpaceShip targetShip, int bodyIndex, EModuleType moduleType, EModuleSubType moduleSubType, int slotIndex)
    {
        if (targetShip == null) return;

        ModuleBody body = targetShip.FindModuleBodyByIndex(bodyIndex);
        if (body == null) return;

        // Body 자체가 교체된 경우
        if (moduleType == EModuleType.body || slotIndex < 0)
        {
            m_selectedModule = body;
            EventManager.TriggerSpaceShipModuleSelected(targetShip, m_selectedModule);
            return;
        }

        // 일반 모듈 (Weapon, Engine, Hanger 등)이 교체된 경우
        ModuleSlot slot = body.FindModuleSlot(moduleType, slotIndex);
        if (slot != null && slot.transform.childCount > 0)
        {
            ModuleBase newModule = slot.GetComponentInChildren<ModuleBase>();
            if (newModule != null)
            {
                m_selectedModule = newModule;
                // 새로 생성된 모듈을 선택 상태로 설정 (selectedModuleVisual 적용)
                EventManager.TriggerSpaceShipModuleSelected(targetShip, m_selectedModule);
            }
        }
    }

}
