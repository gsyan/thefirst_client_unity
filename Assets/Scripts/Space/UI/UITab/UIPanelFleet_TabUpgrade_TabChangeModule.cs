using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelFleet_TabUpgrade_TabChangeModule : UITabBase
{
    private SpaceFleet m_myFleet;
    private SpaceShip m_selectedShip;
    private ModuleBase m_selectedModule;

    [SerializeField] private TextMeshProUGUI m_textTop;
    [SerializeField] private TextMeshProUGUI m_moduleChangeStatText;
    [SerializeField] private TextMeshProUGUI m_moduleChangeResultText;
    private Coroutine m_textResultCoroutine;

    [SerializeField] private RectTransform m_scrollViewModuleContent;
    [SerializeField] private GameObject m_scrollViewModuleItem;       // 프리팹

    [SerializeField] private Button m_backButton;
    [SerializeField] private Button m_changeModuleButton;

    // 생성된 모든 ScrollViewModuleItem 추적
    private List<ScrollViewModuleItem> m_moduleItems = new List<ScrollViewModuleItem>();

    private bool bShowed = false;

    public override void InitializeUITab()
    {
        if (m_textTop != null)
            m_textTop.text = "Module Change";

        var character = DataManager.Instance.m_currentCharacter;
        if (character == null || character.GetOwnedFleet() == null)
        {
            m_moduleChangeStatText.text = "No character or No Fleet";
            return;
        }
        m_myFleet = character.GetOwnedFleet();

        
        
        m_backButton.onClick.AddListener(() => m_tabSystemParent.SwitchToTab(1));
        m_changeModuleButton.onClick.AddListener(ChangeModule);

        EventManager.Subscribe_SpaceShipSelected_TabUpgrade(OnSpaceShipSelected);
        EventManager.Subscribe_SpaceShipModuleSelected_TabUpgrade(OnSpaceShipModuleSelected);
    }
    private void OnSpaceShipSelected(SpaceShip ship)
    {
        m_selectedShip = ship;
        m_selectedModule = ship.m_moduleBodys[0].m_weapons[0];
    }
    private void OnSpaceShipModuleSelected(SpaceShip ship, ModuleBase module)
    {
        if( m_selectedShip != ship) return;
        if (module == null) return;
        if (m_myFleet == null) return;
        
        m_selectedModule = module;
        
        UpdateModuleStatsDisplay();
        UpdateScrollView();
    }


    public override void OnTabActivated()
    {
        EventManager.Subscribe_ShipChange(OnShipChanged);
        
        // 함선 관리 모드로 전환
        CameraController.Instance.m_currentMode = ECameraControllerMode.Manage_Ship;

        m_selectedShip.m_shipOutline.enabled = true;
        CameraController.Instance.SetTargetOfCameraController(m_selectedShip.transform);
     
        InitializeUI();

        bShowed = true;
        UpdateScrollView();
    }

    public override void OnTabDeactivated()
    {
        EventManager.Unsubscribe_ShipChange(OnShipChanged);

        // 평소 카메라 모드로 전환
        CameraController.Instance.m_currentMode = ECameraControllerMode.Normal;
        
        m_selectedShip.m_shipOutline.enabled = false;
        
        InitializeUI();

        bShowed = false;
    }

    private void InitializeUI()
    {
        // if( m_selectedShip == null)
        //     m_selectedShip = m_myFleet.m_ships[0];
        
        // if (m_myFleet != null)
        //     m_myFleet.ClearAllSelectedModule();
        // m_selectedModule = null;

        UpdateModuleStatsDisplay();
    }


    private void OnShipChanged()
    {
        UpdateModuleStatsDisplay();
    }
    
    private void UpdateModuleStatsDisplay()
    {
        if (m_moduleChangeStatText == null) return;

        // SpaceShipStats statsOrg = m_selectedShip.m_spaceShipStatsOrg;
        // SpaceShipStats statsCur = m_selectedShip.m_spaceShipStatsCur;
        
        // string shipStatsText = $"=== SHIP STATS ===\n" +
        //                     $"Health: {statsCur.totalHealth:F0} / {statsOrg.totalHealth:F0}\n" +
        //                     $"Attack: {statsCur.totalAttackPower:F1} / {statsOrg.totalAttackPower:F1}\n" +
        //                     $"Speed: {statsCur.totalMovementSpeed:F1} / {statsOrg.totalMovementSpeed:F1}\n" +
        //                     $"Rotation: {statsCur.totalRotationSpeed:F1} / {statsOrg.totalRotationSpeed:F1}\n" +
        //                     $"Cargo: {statsCur.totalCargoCapacity:F0} / {statsOrg.totalCargoCapacity:F0}\n" +
        //                     $"Weapons: {statsCur.totalWeapons}\n" +
        //                     $"Engines: {statsCur.totalEngines}";
        
        // m_moduleChangeStatsText.text = shipStatsText;

        // if (m_selectedModule == null) return;
        // string moduleStatsCompareText = m_selectedModule.GetUpgradeComparisonText();
        // m_moduleChangeStatsText.text += "\n\n" + moduleStatsCompareText;
    }

    private void UpdateScrollView()
    {
        if( bShowed == false) return;

        if(m_scrollViewModuleContent == null || m_scrollViewModuleItem == null)
            return;

        if(m_selectedModule == null)
            return;

        // 기존 아이템 모두 제거
        m_moduleItems.Clear();
        foreach(Transform child in m_scrollViewModuleContent)
            Destroy(child.gameObject);

        // 선택된 모듈의 타입에 맞는 스크롤 뷰 목록 구성
        int currentModuleTypePacked = m_selectedModule.GetModuleTypePacked();
        if(currentModuleTypePacked == 0)
        {
            // ModulePlaceHolder 의 경우
        }
        else
        {
            EModuleType moduleType = m_selectedModule.GetModuleType();

            Character character = DataManager.Instance.m_currentCharacter;
            if (character == null) return;

            switch (moduleType)
            {
                case EModuleType.Body:
                    CreateBodyModuleItems(currentModuleTypePacked, character);
                    break;
                case EModuleType.Weapon:
                    CreateWeaponModuleItems(currentModuleTypePacked, character);
                    break;
                case EModuleType.Engine:
                    CreateEngineModuleItems(currentModuleTypePacked, character);
                    break;
                case EModuleType.Hanger:
                    CreateHangerModuleItems(currentModuleTypePacked, character);
                    break;
                default:
                    break;
            }
        }        
    }

    private void CreateBodyModuleItems(int currentModuleTypePacked, Character character)
    {
        foreach(EModuleBodySubType subType in System.Enum.GetValues(typeof(EModuleBodySubType)))
        {
            if (subType == EModuleBodySubType.None) continue;
            int moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Body, (int)subType, EModuleStyle.None);
            string moduleName = $"Body - {subType}";
            bool isResearched = character.IsModuleResearched(moduleTypePacked);
            bool isCurrentModule = moduleTypePacked == currentModuleTypePacked;

            CreateModuleItem(moduleName, moduleTypePacked, isResearched, isCurrentModule);
        }
    }

    private void CreateWeaponModuleItems(int currentModuleTypePacked, Character character)
    {
        foreach(EModuleWeaponSubType subType in System.Enum.GetValues(typeof(EModuleWeaponSubType)))
        {
            if (subType == EModuleWeaponSubType.None) continue;

            int moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Weapon, (int)subType, EModuleStyle.None);
            string moduleName = $"Weapon - {subType}";
            bool isResearched = character.IsModuleResearched(moduleTypePacked);
            bool isCurrentModule = moduleTypePacked == currentModuleTypePacked;

            CreateModuleItem(moduleName, moduleTypePacked, isResearched, isCurrentModule);
        }
    }

    private void CreateEngineModuleItems(int currentModuleTypePacked, Character character)
    {
        foreach(EModuleEngineSubType subType in System.Enum.GetValues(typeof(EModuleEngineSubType)))
        {
            if (subType == EModuleEngineSubType.None) continue;

            int moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Engine, (int)subType, EModuleStyle.None);
            string moduleName = $"Engine - {subType}";
            bool isResearched = character.IsModuleResearched(moduleTypePacked);
            bool isCurrentModule = moduleTypePacked == currentModuleTypePacked;

            CreateModuleItem(moduleName, moduleTypePacked, isResearched, isCurrentModule);
        }
    }

    private void CreateHangerModuleItems(int currentModuleTypePacked, Character character)
    {
        foreach(EModuleHangerSubType subType in System.Enum.GetValues(typeof(EModuleHangerSubType)))
        {
            if (subType == EModuleHangerSubType.None) continue;

            int moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Hanger, (int)subType, EModuleStyle.None);
            string moduleName = $"Hanger - {subType}";
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

        int slotIndex = m_selectedModule.m_moduleSlot.m_slotIndex;
        int newModuleType = m_selectedModule.GetModuleTypePacked();
        int newModuleLevel = m_selectedModule.GetModuleLevel();
        if( newModuleLevel < 1) return;

        // 모듈 교체 요청 생성
        var changeRequest = new ModuleChangeRequest
        {
            shipId = m_selectedShip.m_shipInfo.id
            , bodyIndex = m_selectedModule.GetModuleBodyIndex()
            , slotIndex = slotIndex
            , currentModuleType = m_selectedModule.GetModuleType().ToString()
            , newModuleType = CommonUtility.GetModuleType(newModuleType).ToString()
            , newModuleLevel = newModuleLevel
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

    private void ChangeModule()
    {
        if (m_selectedShip == null || m_selectedModule == null)
        {
            ShowResultMessage("No ship or module selected", 3f);
            return;
        }

        if (m_selectedModule is ModulePlaceholder)
        {
            ShowResultMessage("Cannot change placeholder module", 3f);
            return;
        }

        // TODO: 사용자가 교체할 새 모듈을 선택하도록 UI 구현 필요
        // 현재는 임시로 같은 타입의 레벨 1 모듈로 교체하는 예시
        int newModuleType = m_selectedModule.GetModuleTypePacked();
        int newModuleLevel = 1;

        // 슬롯 인덱스 가져오기
        int slotIndex = -1;
        if (m_selectedModule is ModuleWeapon weapon)
            slotIndex = weapon.m_moduleSlot?.m_slotIndex ?? -1;
        else if (m_selectedModule is ModuleEngine engine)
            slotIndex = engine.m_moduleSlot?.m_slotIndex ?? -1;
        else if (m_selectedModule is ModuleHanger hanger)
            slotIndex = hanger.m_moduleSlot?.m_slotIndex ?? -1;

        if (slotIndex < 0)
        {
            ShowResultMessage("Invalid slot index", 3f);
            return;
        }

        // 모듈 교체 요청 생성
        var changeRequest = new ModuleChangeRequest
        {
            shipId = m_selectedShip.m_shipInfo.id,
            bodyIndex = m_selectedModule.GetModuleBodyIndex(),
            slotIndex = slotIndex,
            currentModuleType = m_selectedModule.GetModuleType().ToString(),
            newModuleType = CommonUtility.GetModuleType(newModuleType).ToString(),
            newModuleLevel = newModuleLevel
        };

        Debug.Log($"Requesting module change: Ship {m_selectedShip.name}, Body {changeRequest.bodyIndex}, Slot {slotIndex}");

        // 서버에 모듈 교체 요청 전송
        NetworkManager.Instance.ChangeModule(changeRequest, OnChangeModuleResponse);
    }

    private void OnChangeModuleResponse(ApiResponse<ModuleChangeResponse> response)
    {
        Character character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        if (response.errorCode == 0 && response.data.success)
        {
            // 자원 업데이트
            if (response.data.costRemainInfo != null)
            {
                character.UpdateMineral(response.data.costRemainInfo.remainMineral);
                character.UpdateMineralRare(response.data.costRemainInfo.remainMineralRare);
                character.UpdateMineralExotic(response.data.costRemainInfo.remainMineralExotic);
                character.UpdateMineralDark(response.data.costRemainInfo.remainMineralDark);

                var characterInfo = character.GetInfo();
                DataManager.Instance.SetCharacterData(characterInfo);
            }

            // 함선 정보 업데이트
            if (response.data.updatedShipInfo != null && m_selectedShip != null)
            {
                m_selectedShip.UpdateShipFromServerResponse(response.data.updatedShipInfo);
                Debug.Log("Ship module changed successfully");
            }

            // UI 갱신
            UpdateModuleStatsDisplay();

            // 성공 메시지 표시
            ShowResultMessage($"Module change successful! {response.data.message}", 3f);
        }
        else
        {
            string errorMessage = response.data?.message ?? response.errorMessage ?? "Module change failed";
            Debug.LogError($"Module change failed: {errorMessage}");

            // 실패 메시지 표시
            ShowResultMessage($"Module change failed: {errorMessage}", 3f);
        }
    }



    // m_textResult에 텍스트를 표시하고 n초 후 자동으로 사라지게 합니다.
    public void ShowResultMessage(string message, float displayDuration = 3f)
    {
        if (m_moduleChangeResultText == null) return;

        // 이전 코루틴이 실행 중이면 중지
        if (m_textResultCoroutine != null)
            StopCoroutine(m_textResultCoroutine);

        // 메시지 표시 및 자동 사라지기 코루틴 시작
        m_textResultCoroutine = StartCoroutine(ShowResultMessageCoroutine(message, displayDuration));
    }

    private IEnumerator ShowResultMessageCoroutine(string message, float displayDuration)
    {
        // 메시지 표시
        m_moduleChangeResultText.text = message;

        // 지정된 시간만큼 대기
        yield return new WaitForSeconds(displayDuration);

        // 메시지 제거
        m_moduleChangeResultText.text = "";
        m_textResultCoroutine = null;
    }

}
