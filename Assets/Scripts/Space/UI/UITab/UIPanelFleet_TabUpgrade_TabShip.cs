using System.Collections;
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
    [SerializeField] private Button m_upgradeModuleButton;
    [SerializeField] private Button m_changeModuleButton;
    
    public override void InitializeUITab()
    {
        if (m_textTop != null)
            m_textTop.text = "Ship Management";

        var character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;
        
        m_myFleet = character.GetOwnedFleet();
        if (m_myFleet == null) return;

        m_backButton.onClick.AddListener(() => m_tabSystemParent.SwitchToTab(0));
        m_upgradeModuleButton.onClick.AddListener(UpgradeModule);
        m_changeModuleButton.onClick.AddListener(() => m_tabSystemParent.SwitchToTab(2));
        
        EventManager.Subscribe_SpaceShipSelected_TabUpgrade(OnSpaceShipSelected);
        EventManager.Subscribe_SpaceShipModuleSelected_TabUpgrade(OnSpaceShipModuleSelected);
    }
    private void OnSpaceShipSelected(SpaceShip ship)
    {
        m_selectedShip = ship;

        if (m_myFleet != null)
            m_myFleet.ClearAllSelectedModule();
        m_selectedModule = ship.m_moduleBodys[0].m_weapons[0];
    }
    private void OnSpaceShipModuleSelected(SpaceShip ship, ModuleBase module)
    {
        if( m_selectedShip != ship) return;
        if (module == null) return;
        if (m_myFleet == null) return;
        
        m_selectedModule = module;
        m_selectedShip.SetSelectedModule(ship, module);

        UpdateShipStatsDisplay();
    }

    public override void OnTabActivated()
    {
        EventManager.Subscribe_ShipChange(OnShipChanged);

        // 함선 관리 모드로 전환
        CameraController.Instance.m_currentMode = ECameraControllerMode.Manage_Ship;

        m_selectedShip.m_shipOutline.enabled = true;
        CameraController.Instance.SetTargetOfCameraController(m_selectedShip.transform);
        EventManager.TriggerSpaceShipModuleSelected_TabUpgrade(m_selectedShip, m_selectedModule);

        InitializeUI();
    }

    public override void OnTabDeactivated()
    {
        EventManager.Unsubscribe_ShipChange(OnShipChanged);
        
        // 평소 카메라 모드로 전환
        CameraController.Instance.m_currentMode = ECameraControllerMode.Normal;

        m_selectedShip.m_shipOutline.enabled = false;

        InitializeUI();
    }

    private void InitializeUI()
    {
        // if (m_myFleet != null)
        //     m_myFleet.ClearAllSelectedModule();
        
        // if( m_selectedShip == null)
        //     m_selectedShip = m_myFleet.m_ships[0];
        
        //m_selectedModule = null;

        

        UpdateShipStatsDisplay();
        
    }

    

    

    private void OnShipChanged()
    {
        UpdateShipStatsDisplay();
    }
    
    private void UpdateShipStatsDisplay()
    {
        if (m_textStat == null) return;

        SpaceShipStats statsOrg = m_selectedShip.m_spaceShipStatsOrg;
        SpaceShipStats statsCur = m_selectedShip.m_spaceShipStatsCur;
        
        string shipStatsText = $"=== SHIP STATS ===\n" +
                            $"Health: {statsCur.totalHealth:F0} / {statsOrg.totalHealth:F0}\n" +
                            $"Attack: {statsCur.totalAttackPower:F1} / {statsOrg.totalAttackPower:F1}\n" +
                            $"Speed: {statsCur.totalMovementSpeed:F1} / {statsOrg.totalMovementSpeed:F1}\n" +
                            $"Rotation: {statsCur.totalRotationSpeed:F1} / {statsOrg.totalRotationSpeed:F1}\n" +
                            $"Cargo: {statsCur.totalCargoCapacity:F0} / {statsOrg.totalCargoCapacity:F0}\n" +
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
            ,currentLevel = m_selectedModule.GetModuleLevel()
            ,targetLevel = m_selectedModule.GetModuleLevel() + 1
            ,bodyIndex = m_selectedModule.GetModuleBodyIndex()
            ,moduleType = m_selectedModule.GetModuleType().ToString()
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

        var upgradeStats = DataManager.Instance.RestoreModuleDataByType(m_selectedModule.GetModuleTypePacked(), m_selectedModule.GetModuleLevel() + 1);
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
        
        if (response.errorCode == 0 && response.data.success)
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

            // Update local data
            if (m_selectedModule != null)
            {
                m_selectedModule.SetModuleLevel(response.data.newLevel);
                // Update stats if provided
                if (response.data.newStats != null)
                {
                    m_selectedModule.m_health = response.data.newStats.health;
                }
            }
            
            // Refresh UI
            UpdateShipStatsDisplay();
            //UpdateFleetStatsDisplay();

            // Show success message
            ShowResultMessage($"Upgrade successful! {response.data.message}", 3f);
        }
        else
        {
            string errorMessage = response.data?.message ?? response.errorMessage ?? "Upgrade failed";
            Debug.LogError($"Upgrade failed: {errorMessage}");

            // Show error message
            ShowResultMessage($"Upgrade failed: {errorMessage}", 3f);
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
