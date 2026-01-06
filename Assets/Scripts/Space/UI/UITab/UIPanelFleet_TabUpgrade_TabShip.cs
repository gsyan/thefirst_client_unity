using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelFleet_TabUpgrade_TabShip : UITabBase
{
    [HideInInspector] public SpaceFleet m_myFleet;
    private SpaceShip m_selectedShip;
    private ModuleBase m_selectedModule;

    [SerializeField] private TextMeshProUGUI m_textTop;
    [SerializeField] private TextMeshProUGUI m_textStat;
    [SerializeField] private TextMeshProUGUI m_textResult;
    private Coroutine m_textResultCoroutine;

    [SerializeField] private Button m_backButton;
    [SerializeField] private Button m_unlockModuleButton;
    [SerializeField] private TMP_Text m_unlockModuleButtonText;
    [SerializeField] private GameObject m_scrollViewModule;
    [SerializeField] private RectTransform m_scrollViewModuleContent;
    [SerializeField] private GameObject m_scrollViewModuleItem;       // 프리팹
    [SerializeField] private Button m_selectModuleButton;
    [SerializeField] private Button m_upgradeModuleButton;

    // 생성된 모든 ScrollViewModuleItem 추적
    private List<ScrollViewModuleItem> m_moduleItems = new List<ScrollViewModuleItem>();

    private bool bShow = false;
    
    public override void InitializeUITab()
    {
        if (m_textTop != null)
            m_textTop.text = "Ship Management";

        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;
        
        m_myFleet = character.GetOwnedFleet();
        if (m_myFleet == null) return;

        m_backButton.onClick.AddListener(() => m_tabSystemParent.SwitchToTab(0));
        m_unlockModuleButton.onClick.AddListener(UnlockModule);
        m_selectModuleButton.onClick.AddListener(() => m_tabSystemParent.SwitchToTab(2));
        m_upgradeModuleButton.onClick.AddListener(UpgradeModule);
        
        
        EventManager.Subscribe_SpaceShipSelected_TabUpgrade(OnSpaceShipSelected);
        EventManager.Subscribe_SpaceShipModuleSelected_TabUpgrade(OnSpaceShipModuleSelected);
    }
    private void OnSpaceShipSelected(SpaceShip ship)
    {
        m_selectedShip = ship;

        if (m_myFleet != null)
            m_myFleet.ClearAllSelectedModule();
        
        if (m_selectedModule == null)
        {
            if (ship.m_moduleBodys[0].m_weapons.Count > 0)
                m_selectedModule = ship.m_moduleBodys[0].m_weapons[0];
            else
                m_selectedModule = ship.m_moduleBodys[0];
        }
            
    }
    private void OnSpaceShipModuleSelected(SpaceShip ship, ModuleBase module)
    {
        if( m_selectedShip != ship) return;
        if (module == null) return;
        if (m_myFleet == null) return;
        
        m_selectedModule = module;
        m_selectedShip.SetSelectedModule(ship, module);

        UpdateShipStatsDisplay();
        UpdateUIFrame();
        UpdateScrollView();
    }

    public override void OnTabActivated()
    {
        EventManager.Subscribe_ShipChange(OnShipChanged);

        // 함선 관리 모드로 전환
        CameraController.Instance.m_currentMode = ECameraControllerMode.Manage_Ship;

        m_selectedShip.m_shipOutline.enabled = true;
        CameraController.Instance.SetTargetOfCameraController(m_selectedShip.transform);
        EventManager.TriggerSpaceShipModuleSelected_TabUpgrade(m_selectedShip, m_selectedModule);

        bShow = true;
        UpdateShipStatsDisplay();
        UpdateUIFrame();
        UpdateScrollView();
    }

    public override void OnTabDeactivated()
    {
        EventManager.Unsubscribe_ShipChange(OnShipChanged);
        
        // 평소 카메라 모드로 전환
        CameraController.Instance.m_currentMode = ECameraControllerMode.Normal;

        m_selectedShip.m_shipOutline.enabled = false;

        bShow = false;
    }

    private void OnShipChanged()
    {
        if (bShow != true) return;

        UpdateShipStatsDisplay();
    }


    private void UpdateUIFrame()
    {
        if (bShow != true) return;

        int moduleTypePacked = m_selectedModule.GetModuleTypePacked();
        if( moduleTypePacked == 0)
        {
            m_unlockModuleButton.gameObject.SetActive(true);

            m_scrollViewModule.gameObject.SetActive(false);
            m_selectModuleButton.gameObject.SetActive(false);
            m_upgradeModuleButton.gameObject.SetActive(false);
        }
        else
        {
            m_unlockModuleButton.gameObject.SetActive(false);
            
            m_scrollViewModule.gameObject.SetActive(true);
            m_selectModuleButton.gameObject.SetActive(true);
            m_upgradeModuleButton.gameObject.SetActive(true);
        }
    }

    private void UpdateShipStatsDisplay()
    {
        if (bShow != true) return;
        if (m_textStat == null) return;

        CapabilityProfile statsOrg = m_selectedShip.m_spaceShipStatsOrg;
        CapabilityProfile statsCur = m_selectedShip.m_spaceShipStatsCur;
        
        string shipStatsText = $"=== SHIP STATS ===\n" +
                              $"Att(dps): {statsCur.attackDps:F1} / {statsOrg.attackDps:F1}\n" +
                              $"HP: {statsCur.hp:F0} / {statsOrg.hp:F0}\n" +                              
                              $"Sp: {statsCur.engineSpeed:F1} / {statsOrg.engineSpeed:F1}\n" +
                              $"Car: {statsCur.cargoCapacity:F0} / {statsOrg.cargoCapacity:F0}\n" +
                              $"Weapons: {statsCur.totalWeapons}\n" +
                              $"Engines: {statsCur.totalEngines}";



        m_textStat.text = shipStatsText;

        if (m_selectedModule != null)
        {
            string moduleStatsCompareText = m_selectedModule.GetUpgradeComparisonText();
            m_textStat.text += "\n\n" + moduleStatsCompareText;
        }
        else
        {
            m_textStat.text += "\n\n" + "Select Module First";
        }
        
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
        int unlockPrice = DataManager.Instance.m_dataTableConfig.gameSettings.moduleReleasePrice;
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

        // 모듈 해금 요청 생성
        var unlockRequest = new ModuleUnlockRequest
        {
            shipId = m_selectedShip.m_shipInfo.id,
            bodyIndex = m_selectedModule.GetModuleBodyIndex(),
            moduleType = m_selectedModule.m_moduleSlot.m_moduleType,
            moduleSubType = m_selectedModule.m_moduleSlot.m_moduleSubType,
            slotIndex = m_selectedModule.m_moduleSlot.m_slotIndex
        };

        // 서버에 모듈 해금 요청 전송
        NetworkManager.Instance.UnlockModule(unlockRequest, OnUnlockModuleResponse);
    }

    private void OnUnlockModuleResponse(ApiResponse<ModuleUnlockResponse> response)
    {
        if (response.errorCode == 0)
        {
            Character character = DataManager.Instance.m_currentCharacter;
            if (character == null) return;

            // 자원 업데이트
            if (response.data.costRemainInfo == null) return;
            character.UpdateMineral(response.data.costRemainInfo.remainMineral);
            character.UpdateMineralRare(response.data.costRemainInfo.remainMineralRare);
            character.UpdateMineralExotic(response.data.costRemainInfo.remainMineralExotic);
            character.UpdateMineralDark(response.data.costRemainInfo.remainMineralDark);

            var characterInfo = character.GetInfo();
            DataManager.Instance.SetCharacterData(characterInfo);

            // 응답 데이터로부터 업데이트할 함선 찾기
            if (response.data.updatedShipInfo == null)
            {
                Debug.LogError("updatedShipInfo is null");
                ShowResultMessage("Module unlock failed: Invalid server response", 3f);
                return;
            }

            // Character -> SpaceFleet -> SpaceShip 경로로 업데이트할 함선 찾기
            SpaceFleet fleet = character.GetOwnedFleet();
            if (fleet == null)
            {
                Debug.LogError("Owned fleet is null");
                return;
            }

            // updatedShipInfo의 id로 함선 찾기
            SpaceShip targetShip = null;
            foreach (SpaceShip ship in fleet.m_ships)
            {
                if (ship != null && ship.m_shipInfo.id == response.data.updatedShipInfo.id)
                {
                    targetShip = ship;
                    break;
                }
            }

            if (targetShip == null)
            {
                Debug.LogError($"Ship with id {response.data.updatedShipInfo.id} not found in fleet");
                return;
            }

            // unlock 의 대상은 body 를 제외한 ship module
            int moduleTypePacked = m_selectedModule.m_moduleSlot.m_moduleTypePacked;
            int  bodyIndex = m_selectedModule.GetModuleBodyIndex();
            int slotIndex = m_selectedModule.m_moduleSlot?.m_slotIndex ?? -1;

            // 함선 정보 업데이트
            targetShip.UpdateShipFromServerResponse(response.data.updatedShipInfo);
            // 성공 메시지 표시
            ShowResultMessage("Module unlock successful!", 3f);

            // 현재 선택된 함선 모듈이 업데이트된 함선 모듈과 같다면 모듈 재선택
            if (m_selectedShip == null || m_selectedShip.m_shipInfo.id != response.data.updatedShipInfo.id) return;
            // 새로 생성된 모듈을 다시 선택
            ReselectReplacedModule(targetShip, bodyIndex, slotIndex, moduleTypePacked);
        }
        else
        {
            // 실패 메시지 표시
            string errorMessage = ErrorCodeMapping.GetMessage(response.errorCode);
            ShowResultMessage($"Module unlock failed: {errorMessage}", 3f);
        }
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

        string partsInfo = GetPartsUpgradeInfo(m_selectedModule);
        Debug.Log($"Requesting upgrade for {partsInfo} on ship {m_selectedShip.name}");

        // Create upgrade request
        var upgradeRequest = new ModuleUpgradeRequest
        {
            shipId = m_selectedShip.m_shipInfo.id
            ,bodyIndex = m_selectedModule.GetModuleBodyIndex()
            ,moduleTypePacked = m_selectedModule.GetModuleTypePacked()
            ,slotIndex = m_selectedModule.GetSlotIndex()
            ,currentLevel = m_selectedModule.GetModuleLevel()
            ,targetLevel = m_selectedModule.GetModuleLevel() + 1
        };
        

        // Send upgrade request to server
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

        ModuleData upgradeStats = DataManager.Instance.RestoreModuleData(m_selectedModule.GetModuleTypePacked(), m_selectedModule.GetModuleLevel() + 1);
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
        if (DataManager.Instance.GetModuleUpgradeCost(m_selectedModule.GetModuleTypePacked(), m_selectedModule.GetModuleLevel(), out cost) == false)
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
        else if (moduleBase is ModuleWeapon weapon)
            return $"ModuleWeapon[{weapon.m_classId}]";
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
                var characterInfo = DataManager.Instance.m_currentCharacter.GetInfo();
                character.UpdateMineral(response.data.costRemainInfo.remainMineral);
                character.UpdateMineralRare(response.data.costRemainInfo.remainMineralRare);
                character.UpdateMineralExotic(response.data.costRemainInfo.remainMineralExotic);
                character.UpdateMineralDark(response.data.costRemainInfo.remainMineralDark);
                DataManager.Instance.SetCharacterData(characterInfo);
            }

            // Update local data - shipId, bodyIndex, moduleTypePacked, slotIndex로 특정 모듈 찾아서 업데이트
            UpdateModuleAfterUpgrade(response.data);
            
            // Refresh UI
            UpdateShipStatsDisplay();

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

        // Fleet의 계층적 FindModule 사용: shipId → bodyIndex → moduleTypePacked, slotIndex
        ModuleBase targetModule = m_myFleet.FindModule(upgradeData.shipId, upgradeData.bodyIndex, upgradeData.moduleTypePacked, upgradeData.slotIndex);
        if (targetModule == null)
        {
            Debug.LogError($"Module not found: shipId={upgradeData.shipId}, bodyIndex={upgradeData.bodyIndex}, moduleTypePacked={upgradeData.moduleTypePacked}, slotIndex={upgradeData.slotIndex}");
            return;
        }

        // 모듈 레벨업 적용 (레벨 + 모든 스탯 갱신)
        targetModule.ApplyModuleLevelUp(upgradeData.newLevel);

        // SpaceShip 통계 업데이트
        SpaceShip ship = m_myFleet.FindShip(upgradeData.shipId);
        if (ship != null)
            ship.UpdateShipStats();

        Debug.Log($"Module upgraded successfully: Ship={upgradeData.shipId}, Body={upgradeData.bodyIndex}, Slot={upgradeData.slotIndex}, NewLevel={upgradeData.newLevel}");
    }

    private void UpdateScrollView()
    {
        if (bShow != true) return;
        if (m_scrollViewModuleContent == null || m_scrollViewModuleItem == null) return;
        if (m_selectedModule == null) return;
        Character character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;
        int currentModuleTypePacked = m_selectedModule.GetModuleTypePacked();
        if (currentModuleTypePacked == 0) return;

        // 기존 아이템 모두 제거
        m_moduleItems.Clear();        
        foreach(Transform child in m_scrollViewModuleContent)
            Destroy(child.gameObject);
        
        EModuleType targetModuleType = m_selectedModule.GetModuleType();
        EModuleSubType targetModuleSubType = EModuleSubType.None;
        if( targetModuleType == EModuleType.Weapon)
            targetModuleSubType = m_selectedModule.GetModuleSubType();
        
        // 선택된 모듈의 타입에 맞는 스크롤 뷰 목록 구성
        foreach(EModuleSubType subType in System.Enum.GetValues(typeof(EModuleSubType)))
        {
            if (subType == EModuleSubType.None) continue;
            EModuleType moduleType = CommonUtility.GetModuleTypeFromSubType(subType);
            // targetModuleType 에 속하는 서브 타입만 순회
            if (moduleType != targetModuleType) continue;
            // targetModuleSubType 이 EModuleSubType.None 이면 통과, 아니라면 같아야 통과
            if (targetModuleSubType != EModuleSubType.None && subType != targetModuleSubType) continue;

            int moduleTypePacked = CommonUtility.CreateModuleTypePacked(moduleType, subType, EModuleStyle.None);
            string moduleName = $"{subType}";
            bool isResearched = character.IsModuleResearched(moduleTypePacked);
            bool isCurrentModule = moduleTypePacked == currentModuleTypePacked;
            CreateModuleItem(moduleName, moduleTypePacked, isResearched, isCurrentModule);
        }
    }

    private void CreateModuleItem(string moduleName, int moduleTypePacked, bool isResearched, bool isCurrentModule)
    {
        GameObject item = Instantiate(m_scrollViewModuleItem, m_scrollViewModuleContent);
        if(item != null)
        {
            ScrollViewModuleItem scrollViewItem = item.GetComponent<ScrollViewModuleItem>();
            if(scrollViewItem != null)
            {
                scrollViewItem.InitializeScrollViewModuleItem(
                    moduleName,
                    () => OnModuleTypeSelected(scrollViewItem, moduleTypePacked),
                    () => OnDevelopModuleClicked(moduleTypePacked)
                );

                // 개발 여부에 따라 Dev 버튼 활성화/비활성화
                scrollViewItem.SetDevelopmentButtonEnabled(isResearched);

                // 현재 선택된 모듈 표시
                scrollViewItem.SetSelected_ScrollViewModuleItem(isCurrentModule);

                // 리스트에 추가
                m_moduleItems.Add(scrollViewItem);
            }
        }
    }

    private void OnModuleTypeSelected(ScrollViewModuleItem selectedItem, int moduleTypePacked)
    {
        // 사용자가 스크롤뷰에서 모듈 타입을 선택했을 때
        Debug.Log($"Module type selected: {moduleTypePacked}");

        // 다른 모든 아이템의 선택 해제
        foreach (var item in m_moduleItems)
        {
            if (item != selectedItem)
                item.SetSelected_ScrollViewModuleItem(false);
        }

        int currentModuleTypePacked = m_selectedModule.GetModuleTypePacked();
        int slotIndex = 0;
        if( EModuleType.Body != m_selectedModule.GetModuleType())
            slotIndex = m_selectedModule.m_moduleSlot.m_slotIndex;

        // 모듈 교체 요청 생성
        var changeRequest = new ModuleChangeRequest
        {
            shipId = m_selectedShip.m_shipInfo.id
            , bodyIndex = m_selectedModule.GetModuleBodyIndex()
            , slotIndex = slotIndex
            , currentModuleTypePacked = currentModuleTypePacked
            , newModuleTypePacked = moduleTypePacked
        };

        Debug.Log($"Requesting module change: Ship {m_selectedShip.name}, Body {changeRequest.bodyIndex}, Slot {slotIndex}");

        // 서버에 모듈 교체 요청 전송
        NetworkManager.Instance.ChangeModule(changeRequest, OnChangeModuleResponse);

    }

    private void OnDevelopModuleClicked(int moduleTypePacked)
    {
        // 개발 버튼 클릭 시
        EModuleType moduleType = CommonUtility.GetModuleType(moduleTypePacked);
        int subTypeValue = (moduleTypePacked >> 16) & 0xFF; // SUBTYPE_SHIFT = 16

        Debug.Log($"Development requested for: Type={moduleType}, SubType={subTypeValue}");

        // TODO: 모듈 개발 UI나 로직 연결
        ShowResultMessage($"Module development not implemented yet: {moduleType}", 3f);
    }

    // private void ChangeModule()
    // {
    //     if (m_selectedShip == null || m_selectedModule == null)
    //     {
    //         ShowResultMessage("No ship or module selected", 3f);
    //         return;
    //     }

    //     if (m_selectedModule is ModulePlaceholder)
    //     {
    //         ShowResultMessage("Cannot change placeholder module", 3f);
    //         return;
    //     }

    //     // TODO: 사용자가 교체할 새 모듈을 선택하도록 UI 구현 필요
    //     // 현재는 임시로 같은 타입의 모듈로 교체하는 예시
    //     int currentModuleTypePacked = m_selectedModule.GetModuleTypePacked();
    //     int newModuleTypePacked = m_selectedModule.GetModuleTypePacked();
    //     int newModuleLevel = m_selectedModule.GetModuleLevel();

    //     // 슬롯 인덱스 가져오기
    //     int slotIndex = -1;
    //     if (m_selectedModule is ModuleWeapon weapon)
    //         slotIndex = weapon.m_moduleSlot?.m_slotIndex ?? -1;
    //     else if (m_selectedModule is ModuleEngine engine)
    //         slotIndex = engine.m_moduleSlot?.m_slotIndex ?? -1;
    //     else if (m_selectedModule is ModuleHanger hanger)
    //         slotIndex = hanger.m_moduleSlot?.m_slotIndex ?? -1;

    //     if (slotIndex < 0)
    //     {
    //         ShowResultMessage("Invalid slot index", 3f);
    //         return;
    //     }

    //     // 모듈 교체 요청 생성
    //     var changeRequest = new ModuleChangeRequest
    //     {
    //         shipId = m_selectedShip.m_shipInfo.id,
    //         bodyIndex = m_selectedModule.GetModuleBodyIndex(),
    //         slotIndex = slotIndex,
    //         currentModuleTypePacked = currentModuleTypePacked,
    //         newModuleTypePacked = newModuleTypePacked,
    //         newModuleLevel = newModuleLevel
    //     };

    //     Debug.Log($"Requesting module change: Ship {m_selectedShip.name}, Body {changeRequest.bodyIndex}, Slot {slotIndex}");

    //     // 서버에 모듈 교체 요청 전송
    //     NetworkManager.Instance.ChangeModule(changeRequest, OnChangeModuleResponse);
    // }

    private void OnChangeModuleResponse(ApiResponse<ModuleChangeResponse> response)
    {
        if (response.errorCode == 0)
        {
            UpdateModuleAfterChange(response.data);
        }
        else
        {
            string errorMessage = ErrorCodeMapping.GetMessage(response.errorCode);
            Debug.LogError($"Module change failed: {errorMessage}");
            ShowResultMessage($"Module change failed: {errorMessage}", 3f);
        }
    }

    private void UpdateModuleAfterChange(ModuleChangeResponse changeData)
    {
        if (changeData == null) return;
        if (m_myFleet == null) return;

        // 이전 모듈 찾기
        ModuleBase oldModule = m_myFleet.FindModule(changeData.shipId, changeData.bodyIndex, changeData.oldModuleTypePacked, changeData.slotIndex);
        if (oldModule == null)
        {
            Debug.LogError($"Old module not found: shipId={changeData.shipId}, bodyIndex={changeData.bodyIndex}, oldModuleTypePacked={changeData.oldModuleTypePacked}, slotIndex={changeData.slotIndex}");
            ShowResultMessage("Module change failed: Old module not found", 3f);
            return;
        }
        SpaceShip ship = m_myFleet.FindShip(changeData.shipId);
        if (ship == null) return;

        ship.ChangeModule(changeData.bodyIndex, changeData.oldModuleTypePacked, changeData.newModuleTypePacked, changeData.slotIndex);

        // SpaceShip 통계 업데이트
        ship.UpdateShipStats();

        ShowResultMessage("Module change successful!", 3f);

        // 새로 생성된 모듈 재선택
        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == changeData.shipId)
            ReselectReplacedModule(ship, changeData.bodyIndex, changeData.slotIndex, changeData.newModuleTypePacked);
    }

    // 모듈 교체/해금 후 새로 생성된 모듈을 다시 선택하여 하이라이트 적용
    private void ReselectReplacedModule(SpaceShip targetShip, int bodyIndex, int slotIndex, int moduleTypePacked)
    {
        if (targetShip == null) return;

        ModuleBody body = targetShip.FindModuleBodyByIndex(bodyIndex);
        if (body == null) return;

        // Body 자체가 교체된 경우 (slotIndex == -1)
        EModuleType moduleType = CommonUtility.GetModuleType(moduleTypePacked);
        if (moduleType == EModuleType.Body || slotIndex < 0)
        {
            m_selectedModule = body;
            EventManager.TriggerSpaceShipModuleSelected_TabUpgrade(targetShip, m_selectedModule);
            return;
        }

        // 일반 모듈 (Weapon, Engine, Hanger 등)이 교체된 경우
        ModuleSlot slot = body.FindModuleSlot(moduleTypePacked, slotIndex);
        if (slot != null && slot.transform.childCount > 0)
        {
            ModuleBase newModule = slot.GetComponentInChildren<ModuleBase>();
            if (newModule != null)
            {
                m_selectedModule = newModule;
                // 새로 생성된 모듈을 선택 상태로 설정 (하이라이트 적용)
                EventManager.TriggerSpaceShipModuleSelected_TabUpgrade(targetShip, m_selectedModule);
            }
        }
    }



























    // m_textResult에 텍스트를 표시하고 n초 후 자동으로 사라지게 합니다.
    public void ShowResultMessage(string message, float displayDuration = 3f)
    {
        if (m_textResult == null) return;

        // 이전 코루틴이 실행 중이면 중지
        if (m_textResultCoroutine != null)
            StopCoroutine(m_textResultCoroutine);

        // 메시지 표시 및 자동 사라지기 코루틴 시작
        m_textResultCoroutine = StartCoroutine(ShowResultMessageCoroutine(message, displayDuration));
    }

    private IEnumerator ShowResultMessageCoroutine(string message, float displayDuration)
    {
        // 메시지 표시
        m_textResult.text = message;

        // 지정된 시간만큼 대기
        yield return new WaitForSeconds(displayDuration);

        // 메시지 제거
        m_textResult.text = "";
        m_textResultCoroutine = null;
    }


}
