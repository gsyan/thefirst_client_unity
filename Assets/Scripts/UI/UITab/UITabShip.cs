// 함선/모듈 관리 UI — 헤더(함선 네비게이터+스탯2행), 모듈 맵, 모듈 디테일 카드
using System.Collections;
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
    // 1행: ATK / HP / REP / SPD
    [SerializeField] private Transform  m_shipRitContainer;
    private RowImageText[] m_shipStatRows;
    // 2행: 함재기 능력 — aircraft_count == 0 이면 숨김
    [SerializeField] private Transform  m_aircraftRitContainer;
    private RowImageText[] m_aircraftStatRows;

    [Header("모듈 맵 — 행 컨테이너 (레이블 + 셀렉터 포함)")]
    [SerializeField] private RectTransform m_moduleBodySelectButtonContainer;
    [SerializeField] private RectTransform m_moduleBeamSelectButtonContainer;
    [SerializeField] private RectTransform m_moduleMissileSelectButtonContainer;
    [SerializeField] private RectTransform m_moduleHangerSelectButtonContainer;


    [Header("함선 스탯 디테일")]
    [SerializeField] private Button m_btnShipStatsDetail;

    [Header("모듈 디테일 카드")]
    [SerializeField] private TMP_Text   m_moduleSubTypeText;
    [SerializeField] private TMP_Text   m_moduleLevelText;
    [SerializeField] private Transform  m_moduleStatsContainer;
    [SerializeField] private Transform  m_moduleInvestedMineralContainer;
    
    [SerializeField] private Button    m_unlockModuleButton;
    [SerializeField] private Button    m_levelUpModuleButton;
    //[SerializeField] private TMP_Text  m_levelUpModuleButtonText;
    [SerializeField] private Button    m_subTypeManageButton;
    [SerializeField] private Button    m_btnResetModule;

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

    private List<RowImageText> m_statsRows   = new List<RowImageText>();
    private List<RowImageText> m_mineralRows = new List<RowImageText>();
    


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

        m_shipStatRows     = m_shipRitContainer.GetComponentsInChildren<RowImageText>(true);
        m_aircraftStatRows = m_aircraftRitContainer.GetComponentsInChildren<RowImageText>(true);

        m_statsRows.AddRange(m_moduleStatsContainer.GetComponentsInChildren<RowImageText>(true));
        m_mineralRows.AddRange(m_moduleInvestedMineralContainer.GetComponentsInChildren<RowImageText>(true));
        
        if (m_btnPrevShip != null) m_btnPrevShip.onClick.AddListener(OnPrevShipClicked);
        if (m_btnNextShip != null) m_btnNextShip.onClick.AddListener(OnNextShipClicked);

        m_unlockModuleButton.onClick.AddListener(OnModuleUnlockClicked);
        m_levelUpModuleButton.onClick.AddListener(OnModuleLevelUpClicked);
        m_subTypeManageButton.onClick.AddListener(OnSubTypeManageClicked);
        if (m_btnShipStatsDetail != null) m_btnShipStatsDetail.onClick.AddListener(OnShipStatsDetailClicked);
        if (m_btnResetModule != null) m_btnResetModule.onClick.AddListener(OnResetModuleClicked);

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

        foreach (var row in m_shipStatRows)    row.Hide();
        foreach (var row in m_aircraftStatRows) row.Hide();

        m_shipStatRows[0].SetRow("bubbling-beam",   $"{statsCur.attack:F0}");
        m_shipStatRows[1].SetRow("techno-heart",    $"{statsCur.health:F0}");
        m_shipStatRows[2].SetRow("auto-repair",     $"{statsCur.repair:F0}");
        m_shipStatRows[3].SetRow("rocket-thruster", $"{statsCur.speed:F0}");
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_shipRitContainer as RectTransform);

        bool hasAircraft = statsOrg.airCount > 0;
        m_aircraftRitContainer.gameObject.SetActive(hasAircraft);
        if (hasAircraft)
        {
            m_aircraftStatRows[0].SetRow("strafe",      $"{statsCur.airAttack:F0}");
            m_aircraftStatRows[1].SetRow("jet-fighter", $"{statsOrg.airCount}");
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_aircraftRitContainer as RectTransform);
        }
    }

    private void OnShipStatsDetailClicked()
    {
        if (m_selectedShip == null) return;

        CapabilityProfile org = m_selectedShip.m_spaceShipStatsOrg;
        CapabilityProfile cur = m_selectedShip.m_spaceShipStatsCur;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{CommonUtility.Sprite("bubbling-beam")} (Attack)  {cur.attack:F0}");
        sb.Append($"{CommonUtility.Sprite("techno-heart")} (HP)  {org.health:F0}" );
        sb.AppendLine($"{CommonUtility.Sprite("auto-repair")} (Repair)  {cur.repair:F0}");
        sb.AppendLine($"{CommonUtility.Sprite("rocket-thruster")} (Speed)  {cur.speed:F0}");

        if (org.airCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{CommonUtility.Sprite("strafe")} (Aircraft Attack)  {cur.airAttack:F0}");
            sb.AppendLine($"{CommonUtility.Sprite("jet-fighter")} (Aircraft Count)  {org.airCount:F0}");
        }

        UIManager.Instance.ShowPopupAlert(m_selectedShip.m_shipInfo.shipName, sb.ToString(), null);
    }

    // ─────────────────────────────────────────────
    // 모듈 해금
    // ─────────────────────────────────────────────

    private void OnModuleUnlockClicked()
    {
        if (m_myCharacter == null)
        {
            ShowErrorMessage("Character data not available");
            return;
        }

        if (m_selectedShip == null || m_selectedModule == null)
        {
            ShowErrorMessage("No ship or module selected");
            return;
        }

        if ((m_selectedModule is ModulePlaceholder) == false)
        {
            ShowErrorMessage("Selected module is not a placeholder");
            return;
        }

        int unlockPrice = DataManager.Instance.m_dataTableConfig.gameSettings.moduleUnlockPrice;
        string slotTypeName = LocalizationManager.Instance.Get($"module_type_{m_selectedModule.GetModuleType().ToLocKey()}");
        EModuleType moduleType = m_selectedModule.GetModuleType();
        var resultRows = CommonUtility.GetModuleStatRows(moduleType, CommonUtility.GetDefaultSubType(moduleType), 1, 1);

        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("ship_module_unlock"),
            LocalizationManager.Instance.Get("popup_message_module_unlock", new object[] { slotTypeName }),
            null, null, new CostStruct(ECostType.ModulePoint, unlockPrice),
            () => ExecuteModuleUnlock(),
            null, resultRows
        );
    }

    private void ExecuteModuleUnlock()
    {
        int unlockPrice = DataManager.Instance.m_dataTableConfig.gameSettings.moduleUnlockPrice;
        long playerMineral = m_myCharacter.GetMineral();
        if (playerMineral < unlockPrice)
        {
            ShowErrorMessage($"Insufficient mineral (need {CommonUtility.FormatBigNumber(unlockPrice)}, have {CommonUtility.FormatBigNumber(playerMineral)})");
            return;
        }

        var unlockRequest = new ModuleUnlockRequest
        {
            shipId     = m_selectedShip.m_shipInfo.id,
            bodyIndex  = m_selectedModule.GetModuleBodyIndex(),
            moduleType = m_selectedModule.m_moduleSlot.m_moduleSlotInfo.moduleType,
            slotIndex  = m_selectedModule.m_moduleSlot.m_moduleSlotInfo.slotIndex
        };

        NetworkManager.Instance.UnlockModule(unlockRequest, OnModuleUnlockResponse);
    }

    private void OnModuleUnlockResponse(ApiResponse<ModuleUnlockResponse> response)
    {
        if (response.errorCode == 0)
            UpdateAfterModuleUnlock(response.data);
        else
            ShowErrorMessage($"Module unlock failed: {ErrorCodeMapping.GetMessage(response.errorCode)}");
    }

    private void UpdateAfterModuleUnlock(ModuleUnlockResponse unlockData)
    {
        if (unlockData == null) return;

        Character character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        character.UpdateModulePoint(unlockData.modulePointRemain);

        SpaceFleet fleet = character.GetOwnedFleet();
        if (fleet == null) return;
        SpaceShip targetShip = fleet.FindShip(unlockData.shipId);
        if (targetShip == null) return;

        targetShip.Apply_UnlockModule(unlockData.bodyIndex, unlockData.moduleType, unlockData.moduleSubType, unlockData.slotIndex,
            unlockData.investedModulePoint);
        EventManager.Trigger_ShipStatsChanged(targetShip);

        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == unlockData.shipId)
        {
            PopulateModuleSelectButtons();
            ReselectReplacedModule(targetShip, unlockData.bodyIndex, unlockData.moduleType, unlockData.moduleSubType, unlockData.slotIndex);
        }
    }

    // ─────────────────────────────────────────────
    // 모듈 레벨업
    // ─────────────────────────────────────────────

    private void OnModuleLevelUpClicked()
    {
        if (m_selectedShip == null || m_selectedModule == null) return;
        if (m_selectedModule is ModulePlaceholder == true) return;

        int currentLevel = m_selectedModule.GetModuleLevel();

        // 다음 레벨 데이터 없으면 이미 최대 레벨
        if (DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_selectedModule.GetModuleSubType(), currentLevel + 1) == null)
        {
            ShowErrorMessage(LocalizationManager.Instance.Get("max_level"));
            return;
        }

        UIManager.Instance.ShowModuleLevelupPopup(
            m_selectedModule.GetModuleSubType(),
            m_selectedModule.GetModuleType(),
            currentLevel,
            ExecuteModuleLevelUp
        );
    }

    private void ExecuteModuleLevelUp(int targetLevel)
    {
        if (CanLevelup(targetLevel, out string validationMessage) == false)
        {
            ShowErrorMessage($"Levelup failed: {validationMessage}");
            return;
        }

        var levelUpRequest = new ModuleLevelUpRequest
        {
            shipId        = m_selectedShip.m_shipInfo.id,
            bodyIndex     = m_selectedModule.GetModuleBodyIndex(),
            moduleType    = m_selectedModule.GetModuleType(),
            moduleSubType = m_selectedModule.GetModuleSubType(),
            slotIndex     = m_selectedModule.GetSlotIndex(),
            currentLevel  = m_selectedModule.GetModuleLevel(),
            targetLevel   = targetLevel
        };

        NetworkManager.Instance.LevelUpModule(levelUpRequest, OnLevelUpResponse);
    }

    private bool CanLevelup(int targetLevel, out string validationMessage)
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
        long totalModulePoint = 0;
        for (int lv = fromLevel; lv < targetLevel; lv++)
        {
            if (DataManager.Instance.GetModuleLevelUpCost(m_selectedModule.GetModuleSubType(), lv, out long cost) == false)
            {
                validationMessage = "Failed to get upgrade cost";
                return false;
            }
            totalModulePoint += cost;
        }

        if (character.CheckEnoughModulePoint(totalModulePoint) == false)
        {
            validationMessage = $"Insufficient modulePoint";
            return false;
        }

        return true;
    }

    private void OnLevelUpResponse(ApiResponse<ModuleLevelUpResponse> response)
    {
        Character character = DataManager.Instance.m_currentCharacter;
        if (character == null) return;

        if (response.errorCode == 0)
        {
            character.UpdateModulePoint(response.data.modulePointRemain);

            ApplyModuleLevelUp(response.data);
            UpdateModuleStatsDisplay();
        }
        else
        {
            string errorMessage = ErrorCodeMapping.GetMessage(response.errorCode);
            Debug.LogError($"Upgrade failed: {errorMessage}");
            ShowErrorMessage($"Upgrade failed: {errorMessage}");
        }
    }

    private void ApplyModuleLevelUp(ModuleLevelUpResponse upgradeData)
    {
        if (upgradeData == null) return;
        if (m_myFleet == null) return;

        SpaceShip ship = m_myFleet.FindShip(upgradeData.shipId);
        if (ship == null) return;

        // 레벨업 전 모듈의 투자 광물 및 레벨 저장
        ModuleBase prevModule = ship.FindModule(upgradeData.bodyIndex, upgradeData.moduleType, upgradeData.slotIndex);
        int prevLevel         = 0;
        int prevInvModulePoint = 0;
        if (prevModule != null)
        {
            prevLevel          = prevModule.GetModuleLevel();
            prevInvModulePoint = prevModule.m_investedModulePoint;
        }

        ship.ApplyModuleChange(upgradeData.bodyIndex, upgradeData.moduleType, upgradeData.moduleSubType, upgradeData.slotIndex, upgradeData.newLevel);

        // 레벨업 비용을 계산해 투자 modulePoint에 누적
        int addedModulePoint = 0;
        for (int lv = prevLevel; lv < upgradeData.newLevel; lv++)
        {
            if (DataManager.Instance.GetModuleLevelUpCost(upgradeData.moduleSubType, lv, out long cost) == true)
                addedModulePoint += (int)cost;
        }
        ship.SetModuleInvestedModulePoint(upgradeData.bodyIndex, upgradeData.moduleType, upgradeData.slotIndex,
            prevInvModulePoint + addedModulePoint);

        EventManager.Trigger_ShipStatsChanged(ship);


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

        foreach (var row in m_statsRows)   row.Hide();
        foreach (var row in m_mineralRows) row.Hide();

        if (m_selectedModule is ModulePlaceholder)
        {
            m_unlockModuleButton.gameObject.SetActive(true);
            m_levelUpModuleButton.gameObject.SetActive(false);
            m_subTypeManageButton.gameObject.SetActive(false);
            if (m_btnResetModule != null) m_btnResetModule.gameObject.SetActive(false);

            m_moduleSubTypeText.text = LocalizationManager.Instance.Get($"module_type_{m_selectedModule.GetModuleType()}_placeholder");
        }
        else
        {
            m_unlockModuleButton.gameObject.SetActive(false);

            EModuleSubType subType    = m_selectedModule.GetModuleSubType();
            int level                 = m_selectedModule.GetModuleLevel();
            int nextLevel             = level + 1;
            ModuleData moduleDataNext = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, nextLevel);
            bool isMaxLevel           = moduleDataNext == null;

            // MAX 레벨이면 서브타입 변경 버튼, 아니면 레벨업 버튼
            m_levelUpModuleButton.gameObject.SetActive(!isMaxLevel);
            m_subTypeManageButton.gameObject.SetActive(isMaxLevel);

            if (m_btnResetModule != null)
            {
                m_btnResetModule.gameObject.SetActive(true);
                // 기함 body이고 이미 T1 레벨1이면 리셋 불필요 → 비활성화
                bool isFlagshipBody = m_selectedShip.m_shipInfo.positionIndex == 0
                                   && m_selectedModule.GetModuleType() == EModuleType.body;
                bool isDefaultBody  = m_selectedModule.GetModuleSubType() == EModuleSubType.body_t1_m1
                                   && m_selectedModule.GetModuleLevel() == 1;
                m_btnResetModule.interactable = !(isFlagshipBody && isDefaultBody);
            }

            m_moduleSubTypeText.text = m_selectedModule.GetModuleSubType().GetLocalizedName();

            // 스탯 rows
            ModuleData cur = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, level);
            if (cur != null && m_statsRows.Count >= 2)
            {
                m_moduleLevelText.SetText("{0}", level);

                EModuleType moduleType = m_selectedModule.GetModuleType();
                if (moduleType == EModuleType.body && m_statsRows.Count >= 4)
                {
                    m_statsRows[0].SetRow("techno-heart",     $"{cur.health:F0}");
                    m_statsRows[1].SetRow("auto-repair",      $"{cur.repair:F0}");
                    m_statsRows[2].SetRow("rocket-thruster",  $"{cur.speed:F0}");
                }
                else if (moduleType == EModuleType.beam || moduleType == EModuleType.missile)
                {
                    m_statsRows[0].SetRow("bubbling-beam", $"{cur.attack:F0}");
                }
                else if (moduleType == EModuleType.hanger && m_statsRows.Count >= 5)
                {
                    m_statsRows[0].SetRow("strafe",        $"{cur.airAttack:F0}");
                    m_statsRows[1].SetRow("heart-wings",   $"{cur.airHealth:F0}");
                    m_statsRows[2].SetRow("light-fighter", $"{cur.airSpeed:F0}");
                    m_statsRows[3].SetRow("jet-fighter",   $"{cur.airCount:F0}");
                }
            }
            // 투자 modulePoint rows
            if (m_mineralRows.Count >= 1)
            {
                if (m_selectedModule.m_investedModulePoint > 0)
                    m_mineralRows[0].SetRow("upgrade", $"{m_selectedModule.m_investedModulePoint}");
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_moduleStatsContainer as RectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_moduleInvestedMineralContainer as RectTransform);
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

        NetworkManager.Instance.ChangeModule(changeRequest, OnModuleChangeResponse);
    }

    private void OnModuleChangeResponse(ApiResponse<ModuleChangeResponse> response)
    {
        if (response.errorCode == 0)
            ApplyModuleChange(response.data);
        else
            ShowErrorMessage($"Module change failed: {ErrorCodeMapping.GetMessage(response.errorCode)}");
    }

    private void ApplyModuleChange(ModuleChangeResponse changeData)
    {
        if (changeData == null) return;
        if (m_myFleet == null) return;

        SpaceShip ship = m_myFleet.FindShip(changeData.shipId);
        if (ship == null) return;

        ship.ApplyModuleChange(changeData.bodyIndex, changeData.moduleTypeNew, changeData.moduleSubTypeNew, changeData.slotIndex, changeData.moduleNewLevel, changeData.newUnlockedSubTypes);

        var character = DataManager.Instance.m_currentCharacter;
        if (character != null)
            character.UpdateModulePoint(changeData.modulePointRemain);


        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == changeData.shipId)
        {
            PopulateModuleSelectButtons();
            ReselectReplacedModule(ship, changeData.bodyIndex, changeData.moduleTypeNew, changeData.moduleSubTypeNew, changeData.slotIndex);
        }
    }

    // 모듈 선택 버튼 생성 / 갱신
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

        for (int i = 0; i < selectors.Length; i++)
        {
            if (i >= slotCount || modules[i] == null)
            {
                // 슬롯이 없는 경우: 시각적으로 유지하되 기능 비활성화
                selectors[i].SetNotExist();
                continue;
            }

            ModuleBase captured = modules[i];
            selectors[i].InitializeModuleSelector(captured, () => OnModuleSelectorClicked(captured));
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
                selectors[i].SetModuleSelected(selectors[i].Module == m_selectedModule);
        }
    }

    private void OnModuleSelectorClicked(ModuleBase module)
    {
        if (m_selectedShip == null || module == null) return;
        CameraController.Instance.FocusOnModuleIfHidden(module.m_moduleSlot);
        EventManager.TriggerSpaceShipModuleSelected(m_selectedShip, module);
    }

    // ─────────────────────────────────────────────
    // 모듈 리셋
    // ─────────────────────────────────────────────

    private void OnResetModuleClicked()
    {
        if (m_selectedShip == null || m_selectedModule == null) return;
        if (m_selectedModule is ModulePlaceholder) return;

        if (m_selectedModule.GetModuleType() == EModuleType.body)
        {
            // 기함이 아닌 경우 함선 전체 리셋
            if (m_selectedShip.m_shipInfo.positionIndex != 0)
            {
                OnResetShipClicked();
                return;
            }
            // 기함 body 리셋 — T1 레벨1로 되돌리기 (투자 광물 환급)
        }

        int mp = m_selectedModule.m_investedModulePoint;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(LocalizationManager.Instance.Get("ship_module_reset_confirm"));
        sb.AppendLine();
        sb.AppendLine(BuildRefundText(mp));

        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("ship_module_reset"),
            sb.ToString(),
            null, null, null,
            ExecuteResetModule
        );
    }

    private void ExecuteResetModule()
    {
        if (m_selectedShip == null || m_selectedModule == null) return;

        int slotIndex = m_selectedModule.GetModuleType() == EModuleType.body
            ? 0
            : m_selectedModule.m_moduleSlot.m_moduleSlotInfo.slotIndex;

        var req = new ModuleResetRequest
        {
            shipId    = m_selectedShip.m_shipInfo.id,
            bodyIndex = m_selectedModule.GetModuleBodyIndex(),
            moduleType = m_selectedModule.GetModuleType(),
            slotIndex  = slotIndex
        };
        NetworkManager.Instance.ResetModule(req, OnResetModuleResponse);
    }

    private void OnResetModuleResponse(ApiResponse<ModuleResetResponse> response)
    {
        if (response.errorCode != 0)
        {
            ShowErrorMessage($"Reset failed: {ErrorCodeMapping.GetMessage(response.errorCode)}");
            return;
        }

        var data = response.data;
        var character = DataManager.Instance.m_currentCharacter;
        if (character != null)
            character.UpdateModulePoint(data.modulePointRemain);

        SpaceFleet fleet = character?.GetOwnedFleet();
        if (fleet == null) return;
        SpaceShip targetShip = fleet.FindShip(data.shipId);
        if (targetShip == null) return;

        if (data.moduleType == EModuleType.body)
        {
            // 기함 body 리셋 — T1 레벨1로 복귀
            targetShip.ApplyModuleChange(data.bodyIndex, EModuleType.body, EModuleSubType.body_t1_m1, 0, 1);
        }
        else
        {
            targetShip.Apply_ResetModuleToPlaceholder(data.bodyIndex, data.moduleType, data.slotIndex);
        }
        EventManager.Trigger_ShipStatsChanged(targetShip);

        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == data.shipId)
        {
            PopulateModuleSelectButtons();
            ReselectReplacedModule(targetShip, data.bodyIndex, data.moduleType, EModuleSubType.none, data.slotIndex);
        }
    }

    // ─────────────────────────────────────────────
    // 함선 리셋 + 삭제
    // ─────────────────────────────────────────────

    private void OnResetShipClicked()
    {
        if (m_selectedShip == null) return;
        if (m_selectedShip.m_shipInfo.positionIndex == 0)
        {
            ShowErrorMessage(LocalizationManager.Instance.Get("ship_reset_flagship_forbidden"));
            return;
        }

        int totalMp = 0;
        foreach (var body in m_selectedShip.m_moduleBodys)
        {
            totalMp += body.m_investedModulePoint;
            foreach (var slot in body.m_moduleSlots)
            {
                if (slot.transform.childCount == 0) continue;
                ModuleBase mod = slot.GetComponentInChildren<ModuleBase>();
                if (mod == null || mod is ModulePlaceholder) continue;
                totalMp += mod.m_investedModulePoint;
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(LocalizationManager.Instance.Get("ship_reset_confirm"));
        sb.AppendLine();
        sb.AppendLine(BuildRefundText(totalMp));

        UIManager.Instance.ShowConfirmPopup(
            LocalizationManager.Instance.Get("ship_reset"),
            sb.ToString(),
            null, null, null,
            ExecuteResetShip
        );
    }

    private void ExecuteResetShip()
    {
        if (m_selectedShip == null) return;
        var req = new ShipResetRemoveRequest { shipId = m_selectedShip.m_shipInfo.id };
        NetworkManager.Instance.ResetAndRemoveShip(req, OnResetShipResponse);
    }

    private void OnResetShipResponse(ApiResponse<ShipResetRemoveResponse> response)
    {
        if (response.errorCode != 0)
        {
            ShowErrorMessage($"Ship reset failed: {ErrorCodeMapping.GetMessage(response.errorCode)}");
            return;
        }

        var data = response.data;
        var character = DataManager.Instance.m_currentCharacter;
        if (character != null)
            character.UpdateModulePoint(data.modulePointRemain);

        if (data.updatedFleetInfo != null)
            DataManager.Instance.SetFleetData(data.updatedFleetInfo);

        SpaceFleet fleet = ObjectManager.Instance.m_myFleet;
        if (fleet != null)
        {
            SpaceShip removed = fleet.FindShip(data.removedShipId);
            if (removed != null)
                fleet.RemoveShip(removed, refreshFormation: true);
            m_myFleet = fleet;
        }

        EventManager.Trigger_FleetShipCountChanged();

        m_selectedShip   = null;
        m_selectedModule = null;
        if (m_myFleet != null && m_myFleet.m_ships.Count > 0)
            SelectShip(m_myFleet.m_ships[0]);
    }

    // 회수 자원 문자열 생성
    private string BuildRefundText(int modulePoint)
    {
        if (modulePoint == 0)
            return LocalizationManager.Instance.Get("ship_reset_no_refund");

        return $"{CommonUtility.Sprite("upgrade")} {CommonUtility.FormatBigNumber(modulePoint)}";
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
