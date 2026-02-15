using System.Collections.Generic;
using System.Data;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UITabShip : UITabBase
{
    [SerializeField] private TMP_Text  m_textShipStatus;
    [SerializeField] private RectTransform m_shipStatsContainer;
    private readonly List<string> shipStatLabels = new List<string>{"module_weapon_count", "module_engine_count", "attack_power", "health_power", "speed_power", "cargo_power", "repair_power"};
    
    [SerializeField] private TMP_Text  m_textModuleStatus;
    [SerializeField] private RectTransform m_moduleStatsContainer;
    private readonly List<string> moduleStatLabels = new List<string>{"module_type", "level", "attack_power", "health_power", "speed_power", "cargo_power", "repair_power"};

    [SerializeField] private Button m_unlockModuleButton;    
    [SerializeField] private Button m_upgradeModuleButton;
    [SerializeField] private TMP_Text m_upgradeModuleButtonText;
    
    [SerializeField] private RectTransform m_scrollViewModuleContent;
    [SerializeField] private GameObject m_scrollViewModuleItem;       // 프리팹
    

    private bool bShow = false;

    private Character m_myCharacter;
    private SpaceFleet m_myFleet;
    
    private SpaceShip m_selectedShip;
    private ModuleBase m_selectedModule;

    private readonly List<RowLabelValue> m_shipStatRows = new();
    private readonly List<RowLabelValue> m_moduleStatRows = new();
    
    private readonly List<ScrollViewModuleItem> m_moduleItemPool = new List<ScrollViewModuleItem>();
    private List<ScrollViewModuleItem> m_moduleItemActive = new List<ScrollViewModuleItem>();


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

        // m_shipStatsContainer 내부의 자식들을 하이어라키 순서대로 캐싱
        if (m_shipStatsContainer != null)
        {
            m_shipStatRows.Clear();
            for (int i = 0; i < m_shipStatsContainer.childCount; i++)
            {
                Transform child = m_shipStatsContainer.GetChild(i);
                RowLabelValue row = child.GetComponent<RowLabelValue>();
                if (row != null)
                {
                    row.SetLabel(shipStatLabels[i]);
                    m_shipStatRows.Add(row);
                }
            }
        }

        // m_moduleStatsContainer 내부의 자식들을 하이어라키 순서대로 캐싱
        if (m_moduleStatsContainer != null)
        {
            m_moduleStatRows.Clear();
            for (int i = 0; i < m_moduleStatsContainer.childCount; i++)
            {
                Transform child = m_moduleStatsContainer.GetChild(i);
                RowLabelValue row = child.GetComponent<RowLabelValue>();
                if (row != null)
                {
                    row.SetLabel(moduleStatLabels[i]);
                    m_moduleStatRows.Add(row);
                }       
            }
        }

        m_unlockModuleButton.onClick.AddListener(UnlockModule);        
        m_upgradeModuleButton.onClick.AddListener(UpgradeModule);
        
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
        //UpdateModuleRightUIFrame();
        PopulateModuleScrollView();
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
        
        if (m_myFleet != null)
            m_myFleet.ClearAllSelectedModule();

        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
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
            //UpdateModuleRightUIFrame();
            PopulateModuleScrollView();
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
            //UpdateModuleRightUIFrame();
            PopulateModuleScrollView();
        }
    }

    private void UpdateShipStatsDisplay()
    {
        if (m_selectedShip == null)
            return;

        CapabilityProfile statsOrg = m_selectedShip.m_spaceShipStatsOrg;
        CapabilityProfile statsCur = m_selectedShip.m_spaceShipStatsCur;

        m_shipStatRows[0].SetValue($"{statsCur.totalWeapons}");
        m_shipStatRows[1].SetValue($"{statsCur.totalEngines}");
        m_shipStatRows[2].SetValue($"{statsCur.attack_power:F0}/{statsOrg.attack_power:F0}");
        m_shipStatRows[3].SetValue($"{statsCur.health_power:F0}/{statsOrg.health_power:F0}");
        m_shipStatRows[4].SetValue($"{statsCur.speed_power:F0}/{statsOrg.speed_power:F0}");
        m_shipStatRows[5].SetValue($"{statsCur.cargo_capacity:F0}/{statsOrg.cargo_capacity:F0}");
        m_shipStatRows[6].SetValue($"{statsCur.repair_power:F0}/{statsOrg.repair_power:F0}");
    }
    
    private void PopulateModuleScrollView()
    {
        if (bShow != true) return;
        if (m_myCharacter == null) return;
        if (m_scrollViewModuleContent == null || m_scrollViewModuleItem == null) return;
        if (m_selectedModule == null) return;
        if (m_selectedModule is ModulePlaceholder) return;

        // 활성 아이템 비활성화
        for (int i = 0; i < m_moduleItemActive.Count; i++)
            m_moduleItemActive[i].gameObject.SetActive(false);
        m_moduleItemActive.Clear();

        // 슬롯의 원래 정보를 기준으로 목록 구성
        EModuleType targetModuleType = m_selectedModule.GetModuleType();

        int poolIndex = 0;
        // 선택된 모듈의 타입에 맞는 스크롤 뷰 목록 구성
        foreach(EModuleSubType subType in System.Enum.GetValues(typeof(EModuleSubType)))
        {
            if (subType == EModuleSubType.none) continue;
            EModuleType moduleType = CommonUtility.GetModuleTypeFromSubType(subType);
            // targetModuleType 에 속하는 서브 타입만 순회
            if (moduleType != targetModuleType) continue;

            // DataTableModule에서 해당 SubType의 SlotType 조회
            ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, 1);
            if (moduleData == null) continue;

            string moduleName = $"{subType}";

            bool isResearched = m_myCharacter.IsModuleResearched(moduleType, subType);
            bool isCurrentModule = subType == m_selectedModule.GetModuleSubType();
            CreateModuleItem(poolIndex, moduleName, moduleType, subType, isResearched, isCurrentModule);
            poolIndex++;
        }
    }

    private void CreateModuleItem(int poolIndex, string moduleName, EModuleType moduleType, EModuleSubType moduleSubType, bool isResearched, bool isCurrentModule)
    {
        ScrollViewModuleItem scrollViewItem;
        if (poolIndex < m_moduleItemPool.Count)
        {
            scrollViewItem = m_moduleItemPool[poolIndex];
            scrollViewItem.gameObject.SetActive(true);
        }
        else
        {
            var item = Instantiate(m_scrollViewModuleItem, m_scrollViewModuleContent);
            item.name = m_scrollViewModuleItem.name;
            scrollViewItem = item.GetComponent<ScrollViewModuleItem>();
            m_moduleItemPool.Add(scrollViewItem);
        }

        scrollViewItem.InitializeScrollViewModuleItem(
            moduleName,
            () => OnModuleSelectClicked(scrollViewItem, moduleType, moduleSubType),
            () => OnModuleResearchClicked(moduleType, moduleSubType)
        );
        scrollViewItem.SetDevelopmentButtonEnabled(isResearched);
        scrollViewItem.SetSelected_ScrollViewModuleItem(isCurrentModule);
        m_moduleItemActive.Add(scrollViewItem);
    }


    private void UnlockModule()
    {
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

        // 해금 비용 확인
        int unlockPrice = DataManager.Instance.m_dataTableConfig.gameSettings.m_moduleUnlockPrice;
        Character character = DataManager.Instance.m_currentCharacter;
        if (character == null)
        {
            ShowResultMessage("Character data not available", 3f);
            return;
        }

        long playerMineral = character.GetMineral();
        if (playerMineral < unlockPrice)
        {
            ShowResultMessage($"Insufficient mineral (need {unlockPrice}, have {playerMineral})", 3f);
            return;
        }

        // 확인 팝업 표시
        CostStruct cost = new CostStruct { mineral = unlockPrice };
        string slotTypeName = LocalizationManager.Instance.Get($"module_type_{m_selectedModule.GetModuleType().ToLocKey()}");

        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("module_unlock"),
            LocalizationManager.Instance.Get("popup_message_module_unlock", new object[] { slotTypeName }),
            cost,
            () => ExecuteUnlockModule()
        );
    }

    private void ExecuteUnlockModule()
    {
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
            ReselectReplacedModule(targetShip, unlockData.bodyIndex, unlockData.moduleType, unlockData.moduleSubType, unlockData.slotIndex);
    }

    private void UpgradeModule()
    {
        if (m_selectedShip == null || m_selectedModule == null) return;
        if (m_selectedModule is ModulePlaceholder == true) return;

        // Validate resources and upgrade availability
        if (!CanUpgrade(out string validationMessage))
        {
            Debug.LogWarning($"Upgrade blocked: {validationMessage}");
            ShowResultMessage($"Upgrade failed: {validationMessage}", 3f);
            return;
        }

        // 업그레이드 비용 가져오기
        if (!DataManager.Instance.GetModuleUpgradeCost(m_selectedModule.GetModuleSubType(), m_selectedModule.GetModuleLevel(), out CostStruct cost))
        {
            ShowResultMessage("Failed to get upgrade cost", 3f);
            return;
        }


        string moduleTypeName = LocalizationManager.Instance.Get($"module_type_{m_selectedModule.GetModuleType().ToLocKey()}");
        int currentLevel = m_selectedModule.GetModuleLevel();
        int targetLevel = currentLevel + 1;
        
        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("module_upgrade"),
            LocalizationManager.Instance.Get("popup_message_module_upgrade", new object[] { moduleTypeName, currentLevel, targetLevel }),
            cost,
            () => ExecuteUpgradeModule()
        );
    }

    private void ExecuteUpgradeModule()
    {
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
            validationMessage = $"Insufficient mineral (need {cost.mineral}, have {playerMineral})";
            return false;
        }
        if (playerMineralRare < cost.mineralRare)
        {
            validationMessage = $"Insufficient mineralRare (need {cost.mineralRare}, have {playerMineralRare})";
            return false;
        }
        if (playerMineralExotic < cost.mineralExotic)
        {
            validationMessage = $"Insufficient mineralExotic (need {cost.mineralExotic}, have {playerMineralExotic})";
            return false;
        }
        if (playerMineralDark < cost.mineralDark)
        {
            validationMessage = $"Insufficient mineralDark (need {cost.mineralDark}, have {playerMineralDark})";
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

        // 모든 아이템의 선택 해제
	    foreach (var item in m_moduleItemActive)
		item.SetSelected_ScrollViewModuleItem(false);

        // 새로 생성된 모듈 재선택
        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == upgradeData.shipId)
		ReselectReplacedModule(ship, upgradeData.bodyIndex, upgradeData.moduleType, upgradeData.moduleSubType, upgradeData.slotIndex);
    }

    private void UpdateModuleStatsDisplay()
    {
        if (bShow != true) return;
        if (m_selectedShip == null) return;

        // foreach (var row in m_moduleStatRows)
        //     row.gameObject.SetActive(false);

        string localizationKeyModuleType = $"module_type_{m_selectedModule.GetModuleType()}";
        
        if (m_selectedModule is ModulePlaceholder)
        {
            m_upgradeModuleButton.gameObject.SetActive(false);
            m_unlockModuleButton.gameObject.SetActive(true);

            m_moduleStatRows[0].SetValue(localizationKeyModuleType + "_placeholder");
            //m_moduleStatRows[0].gameObject.SetActive(true);

            EModuleType moduleType = m_selectedModule.GetModuleType();
            EModuleSubType subType = EnemyModuleSlotConfig.GetDefaultSubType(moduleType);
            ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, 1);
            if (moduleData != null)
            {
                m_moduleStatRows[1].SetValue("1");
                m_moduleStatRows[2].SetValue($"{moduleData.m_attackPower:F0}");                
                m_moduleStatRows[3].SetValue($"{moduleData.m_health:F0}");                
                m_moduleStatRows[4].SetValue($"{moduleData.m_movementSpeed:F0}");                
                m_moduleStatRows[5].SetValue($"{moduleData.m_cargoCapacity:F0}");                
                m_moduleStatRows[6].SetValue($"{moduleData.m_repairPower:F0}");
                
                // m_moduleStatRows[1].gameObject.SetActive(true);
                // m_moduleStatRows[2].gameObject.SetActive(true);
                // m_moduleStatRows[3].gameObject.SetActive(true);
                // m_moduleStatRows[4].gameObject.SetActive(true);
                // m_moduleStatRows[5].gameObject.SetActive(true);
                // m_moduleStatRows[6].gameObject.SetActive(true);
            }

            
            
        }
        else
        {
            m_unlockModuleButton.gameObject.SetActive(false);
            m_upgradeModuleButton.gameObject.SetActive(true);

            EModuleType moduleType = m_selectedModule.GetModuleType();
            EModuleSubType subType = m_selectedModule.GetModuleSubType();
            int currentLevel = m_selectedModule.GetModuleLevel();
            int nextLevel = currentLevel + 1;

            ModuleData moduleDataNext = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, nextLevel);
            // 업그레이드 가능한 상황
            if (moduleDataNext != null)
            {
                CommonUtility.SetUILocText(m_upgradeModuleButtonText, "module_upgrade");

                ModuleData moduleDataCurrent = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, currentLevel);
                
                m_moduleStatRows[0].SetValue(localizationKeyModuleType);
                m_moduleStatRows[1].SetValue($"<mspace=0.6em>{currentLevel,5} <voffset=6>→</voffset> {nextLevel,-5}</mspace>");
                m_moduleStatRows[2].SetValue($"<mspace=0.6em>{moduleDataCurrent.m_attackPower,5:F0} <voffset=6>→</voffset> {moduleDataNext.m_attackPower,-5:F0}</mspace>");
                m_moduleStatRows[3].SetValue($"<mspace=0.6em>{moduleDataCurrent.m_health,5:F0} <voffset=6>→</voffset> {moduleDataNext.m_health,-5:F0}</mspace>");
                m_moduleStatRows[4].SetValue($"<mspace=0.6em>{moduleDataCurrent.m_movementSpeed,5:F0} <voffset=6>→</voffset> {moduleDataNext.m_movementSpeed,-5:F0}</mspace>");
                m_moduleStatRows[5].SetValue($"<mspace=0.6em>{moduleDataCurrent.m_cargoCapacity,5:F0} <voffset=6>→</voffset> {moduleDataNext.m_cargoCapacity,-5:F0}</mspace>");
                m_moduleStatRows[6].SetValue($"<mspace=0.6em>{moduleDataCurrent.m_repairPower,5:F0} <voffset=6>→</voffset> {moduleDataNext.m_repairPower,-5:F0}</mspace>");

                // DataManager.Instance.GetModuleUpgradeCost(subType, currentLevel, out CostStruct cost)
                //m_upgradeModuleStatRows[2].SetValue1($"Mineral: {CommonUtility.FormatBigNumber(cost.mineral)}");
            }
            // 이미 맥스 레벨
            else
            {
                CommonUtility.SetUILocText(m_upgradeModuleButtonText, "max_level");

                CapabilityProfile statsOrg = m_selectedModule.GetModuleCapabilityProfile(true);

                m_moduleStatRows[0].SetValue(localizationKeyModuleType);
                m_moduleStatRows[1].SetValue($"{currentLevel}");
                m_moduleStatRows[2].SetValue($"{statsOrg.attack_power:F0}");
                m_moduleStatRows[3].SetValue($"{statsOrg.health_power:F0}");
                m_moduleStatRows[4].SetValue($"{statsOrg.speed_power:F0}");
                m_moduleStatRows[5].SetValue($"{statsOrg.cargo_capacity:F0}");
                m_moduleStatRows[6].SetValue($"{statsOrg.repair_power:F0}");

                //m_upgradeModuleStatusContainer.gameObject.SetActive(false);
                //m_upgradeModuleDescription.text = LocalizationManager.Instance.Get("max_level");
            }
        }
    }

    // 0이 아닌 비용만 표시
    private string BuildCostText(CostStruct cost)
    {
        var sb = new System.Text.StringBuilder();
        if (cost.mineral > 0)
            sb.AppendLine($"Mineral: {CommonUtility.FormatBigNumber(cost.mineral)}");
        if (cost.mineralRare > 0)
            sb.AppendLine($"Mineral.R: {CommonUtility.FormatBigNumber(cost.mineralRare)}");
        if (cost.mineralExotic > 0)
            sb.AppendLine($"Mineral.E: {CommonUtility.FormatBigNumber(cost.mineralExotic)}");
        if (cost.mineralDark > 0)
            sb.AppendLine($"Mineral.D: {CommonUtility.FormatBigNumber(cost.mineralDark)}");
        // 마지막 줄바꿈 제거
        if (sb.Length > 0 && sb[sb.Length - 1] == '\n')
            sb.Length -= 1;
        if (sb.Length > 0 && sb[sb.Length - 1] == '\r')
            sb.Length -= 1;
        return sb.Length > 0 ? sb.ToString() : "Free";
    }

    private void OnModuleSelectClicked(ScrollViewModuleItem selectedItem, EModuleType moduleType, EModuleSubType moduleSubType)
    {
        EModuleType currentModuleType = m_selectedModule.GetModuleType();
        EModuleSubType currentModuleSubType = m_selectedModule.GetModuleSubType();

        // 같은 모듈이면 바꿀 필요 없음
        if (currentModuleType == moduleType && currentModuleSubType == moduleSubType)
        {
            ShowResultMessage("Same module type selected. No change needed", 3f);
            return;
        }

        int slotIndex = 0;
        if( EModuleType.body != m_selectedModule.GetModuleType())
            slotIndex = m_selectedModule.m_moduleSlot.m_moduleSlotInfo.slotIndex;

        // 모듈 교체 요청 생성
        var changeRequest = new ModuleChangeRequest
        {
            shipId = m_selectedShip.m_shipInfo.id
            , bodyIndex = m_selectedModule.GetModuleBodyIndex()
            , slotIndex = slotIndex
            , moduleType = currentModuleType
            , moduleSubTypeCurrent = currentModuleSubType
            , moduleSubTypeNew = moduleSubType
        };

        Debug.Log($"Requesting module change: Ship {m_selectedShip.name}, Body {changeRequest.bodyIndex}, Slot {slotIndex}");

        // 서버에 모듈 교체 요청 전송
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

    private void OnModuleResearchClicked(EModuleType moduleType, EModuleSubType moduleSubType)
    {
        // 개발 버튼 클릭 시
        // Get research cost from DataManager
        CostStruct researchCost = DataManager.Instance.GetModuleResearchCost(moduleSubType);        
        // check)
        bool result = DataManager.Instance.m_currentCharacter.CheckEnoughCostStruct(researchCost);
        if( result == false)
        {
            ShowResultMessage($"Insufficient resources(cost mineral: {researchCost.mineral})", 3f);
            return;
        }

        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("module_research"),
            LocalizationManager.Instance.Get("popup_message_module_research", new object[] { moduleSubType.ToLocKey() }),
            researchCost,
            onConfirm: () =>
            {
                // Confirm button clicked - Send research request to server
                Debug.Log($"Research confirmed for: {moduleSubType}");

                var request = new ModuleResearchRequest
                {
                    moduleType = moduleType
                    , moduleSubType = moduleSubType
                };

                NetworkManager.Instance.ResearchModule(request, OnModuleResearchResponse);
            },
            onCancel: () =>
            {
                // Cancel button clicked
                Debug.Log($"Research cancelled for: {moduleSubType}");
                ShowResultMessage("Research cancelled", 2f);
            }
        );
    }

    private void OnModuleResearchResponse(ApiResponse<ModuleResearchResponse> response)
    {
        if (response.errorCode == 0)
        {
            // Research successful
            var researchResponse = response.data;

            // Update character's remaining resources
            if (researchResponse.costRemainInfo != null)
                DataManager.Instance.m_currentCharacter.UpdateAllMinerals(researchResponse.costRemainInfo);

            // Update researched modules list
            if (researchResponse.researchedModuleTypes != null)
                DataManager.Instance.m_currentCharacter.UpdateResearchedModules(researchResponse.researchedModuleTypes);

            ShowResultMessage($"Research completed: {researchResponse.moduleType}-{researchResponse.moduleSubType}", 3f);

            // Refresh UI to show newly researched module
            PopulateModuleScrollView();
        }
        else
        {
            // Research failed
            string errorMessage = ErrorCodeMapping.GetMessage(response.errorCode);
            Debug.LogError($"Research failed: {errorMessage}");
            ShowResultMessage($"Research failed: {errorMessage}", 3f);
        }
    }



    private void UpdateModuleAfterChange(ModuleChangeResponse changeData)
    {
        if (changeData == null) return;
        if (m_myFleet == null) return;

        SpaceShip ship = m_myFleet.FindShip(changeData.shipId);
        if (ship == null) return;

        ship.Apply_ChangeModule(changeData.bodyIndex, changeData.moduleTypeNew, changeData.moduleSubTypeNew, changeData.slotIndex, changeData.moduleNewLevel);

        ShowResultMessage("Module change successful!", 3f);

        // 모든 아이템의 선택 해제
        foreach (var item in m_moduleItemActive)
            item.SetSelected_ScrollViewModuleItem(false);

        // 새로 생성된 모듈 재선택
        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == changeData.shipId)
            ReselectReplacedModule(ship, changeData.bodyIndex, changeData.moduleTypeNew, changeData.moduleSubTypeNew, changeData.slotIndex);
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
