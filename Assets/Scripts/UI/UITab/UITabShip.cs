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


    [Header("모듈 디테일 카드")]
    [SerializeField] private TMP_Text   m_moduleSubTypeText;
    [SerializeField] private GameObject m_moduleLevelName;
    [SerializeField] private TMP_Text   m_moduleLevelText;
    [SerializeField] private Transform  m_moduleStatsContainer;
    [SerializeField] private RectTransform  m_investedTotal;
    [SerializeField] private RectTransform  m_investedModulePoint;
    [SerializeField] private TMP_Text   m_investedModulePointText;
    [SerializeField] private RectTransform  m_investedMineral;
    [SerializeField] private TMP_Text   m_investedMineralText;
    
    [SerializeField] private GameObject m_unlockModuleContainer;
    [SerializeField] private UIButtonHasChildren    m_unlockModuleButton;
    [SerializeField] private TMP_Text   m_unlockModuleButtonText;
    [SerializeField] private TMP_Text   m_unlockModuleSubTypeText;
    [SerializeField] private Transform  m_moduleStatusContainer;

    [SerializeField] private UIButtonHasChildren    m_gradeDownModuleButton;
    [SerializeField] private TMP_Text               m_gradeDownModuleButtonText1;
    [SerializeField] private RowImageText           m_gradeDownModuleButtonText2;
    [SerializeField] private UIButtonHasChildren    m_gradeUpModuleButton;
    [SerializeField] private TMP_Text               m_gradeUpModuleButtonText1;
    [SerializeField] private RowImageText           m_gradeUpModuleButtonText2;

    [SerializeField] private UIButtonHasChildren    m_levelDownModuleButton;
    [SerializeField] private TMP_Text               m_levelDownModuleButtonText1;
    [SerializeField] private RowImageText           m_levelDownModuleButtonText2;
    [SerializeField] private UIButtonHasChildren    m_levelUpModuleButton;
    [SerializeField] private TMP_Text               m_levelUpModuleButtonText1;
    [SerializeField] private RowImageText           m_levelUpModuleButtonText2;

    [Header("미네랄/모듈포인트 모드 전환")]
    [SerializeField] private UIButtonHasChildren    m_btnModeToggle;
    [SerializeField] private TMP_Text               m_textModeToggle;

    //[SerializeField] private Button    m_subTypeManageButton;
    //[SerializeField] private Button    m_btnResetModule;

    private bool bShow = false;

    private Commander  m_myCommander;
    private SpaceFleet m_playerFleet;

    private SpaceShip  m_selectedShip;
    private ModuleBase m_selectedModule;
    private bool       m_bModuleChanging = false;
    private bool       m_isUnlockPending = false;

    // 행별 셀렉터 캐시 (prefab에 미리 배치된 버튼들)
    private ModuleSelector[] m_selectorsBody;
    private ModuleSelector[] m_selectorsBeam;
    private ModuleSelector[] m_selectorsMissile;
    private ModuleSelector[] m_selectorsHanger;

    private List<RowImageText> m_statsRows   = new List<RowImageText>();

    public override void InitializeUITab()
    {
        InitializeUITabShip();
    }

    private void InitializeUITabShip()
    {
        m_myCommander = DataManager.Instance.m_currentCommander;
        if (m_myCommander == null)
        { Debug.LogError("UITabShip / InitializeUITabShip m_myCommander == null"); return; }

        m_selectorsBody    = m_moduleBodySelectButtonContainer.GetComponentsInChildren<ModuleSelector>(true);
        m_selectorsBeam    = m_moduleBeamSelectButtonContainer.GetComponentsInChildren<ModuleSelector>(true);
        m_selectorsMissile = m_moduleMissileSelectButtonContainer.GetComponentsInChildren<ModuleSelector>(true);
        m_selectorsHanger  = m_moduleHangerSelectButtonContainer.GetComponentsInChildren<ModuleSelector>(true);

        m_shipStatRows     = m_shipRitContainer.GetComponentsInChildren<RowImageText>(true);
        m_aircraftStatRows = m_aircraftRitContainer.GetComponentsInChildren<RowImageText>(true);

        m_statsRows.AddRange(m_moduleStatsContainer.GetComponentsInChildren<RowImageText>(true));
        
        if (m_btnPrevShip != null) m_btnPrevShip.onClick.AddListener(OnPrevShipClicked);
        if (m_btnNextShip != null) m_btnNextShip.onClick.AddListener(OnNextShipClicked);

        m_unlockModuleButton.GetButton().onClick.AddListener(OnModuleUnlockClicked);
        if (m_unlockModuleButtonText != null)
        {
            int unlockPrice = DataManager.Instance.m_dataTableConfig.gameSettings.moduleUnlockPrice;
            m_unlockModuleButtonText.SetText($"-{unlockPrice}");
        }
        m_gradeDownModuleButton.GetButton().onClick.AddListener(OnModuleGradeDownClicked);
        m_gradeUpModuleButton.GetButton().onClick.AddListener(OnModuleGradeUpClicked);
        m_levelDownModuleButton.GetButton().onClick.AddListener(OnModuleLevelDownClicked);
        RegisterLevelUpPointerEvents();
        if (m_btnModeToggle != null) m_btnModeToggle.GetButton().onClick.AddListener(OnModeToggleClicked);
        //if (m_btnResetModule != null) m_btnResetModule.onClick.AddListener(OnResetModuleClicked);

        EventManager.Subscribe_SpaceShipSelected(OnSpaceShipSelected);
        EventManager.Subscribe_ShipUpdateHP(UpdateShipHeader);
        EventManager.Subscribe_SpaceShipModuleSelected(OnSpaceShipModuleSelected);
        EventManager.Subscribe_ModulePointChanged(OnModulePointChanged);
        EventManager.Subscribe_ShipStatsChanged(OnShipStatsChangedRefreshModules);

        // 탭 초기화 시점에 함대가 아직 스폰되지 않았을 수 있음(튜토리얼 등) — 스폰/교체 시점에 뒤늦게 바인딩
        EventManager.Subscribe_MyFleetSet(OnMyFleetSet);

        // 이미 함대가 존재하면 즉시 바인딩
        if (ObjectManager.Instance.GetMyFleet() != null)
            BindPlayerFleet();

        // 함선 관리 튜토리얼(모듈 강화 단계 포함)이 끝나면 이 탭을 자동으로 닫음
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnTutorialCompleted += OnTutorialCompleted;
    }

    // 함선 관리 튜토리얼 완료 시 호출 — 탭을 열어둔 채로 다음 스텝(전투 등)이 진행되지 않도록 닫음
    private void OnTutorialCompleted(string tutorialId)
    {
        if (tutorialId != "Tutorial_FirstPlay_ManageShip") return;

        TutorialManager.Instance.OnTutorialCompleted -= OnTutorialCompleted; // 1회성 처리라 사용 즉시 구독 해제
        if (m_tabSystemParent != null)
            m_tabSystemParent.SwitchToTab(-1);
    }

    // 함대 스폰/교체(튜토리얼→실제 함대 전환 포함) 시 호출
    private void OnMyFleetSet()
    {
        BindPlayerFleet();
    }

    private void BindPlayerFleet()
    {
        m_playerFleet = ObjectManager.Instance.GetMyFleet();
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_ModulePointChanged(OnModulePointChanged);
        EventManager.Unsubscribe_ShipStatsChanged(OnShipStatsChangedRefreshModules);
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnTutorialCompleted -= OnTutorialCompleted;
    }

    // 함대 전체 리셋 등 외부에서 함선 모듈이 갱신된 경우 — 현재 선택 중인 함선이면 모듈 UI 재구성
    // 기존 선택 모듈 오브젝트가 리셋으로 파괴/교체됐을 수 있어 유효성 확인 후 필요 시에만 기본 모듈로 재선택
    private void OnShipStatsChangedRefreshModules(SpaceShip ship)
    {
        if (bShow == false) return;
        if (ship == null || ship != m_selectedShip) return;
        if (ship.m_moduleBodys.Count == 0) return;

        if (m_selectedModule == null)
        {
            if (ship.m_moduleBodys[0].m_beams.Count > 0)
                m_selectedModule = ship.m_moduleBodys[0].m_beams[0];
            else if (ship.m_moduleBodys[0].m_missiles.Count > 0)
                m_selectedModule = ship.m_moduleBodys[0].m_missiles[0];
            else
                m_selectedModule = ship.m_moduleBodys[0];

            EventManager.TriggerSpaceShipModuleSelected(m_selectedShip, m_selectedModule);
        }

        UpdateShipHeader();
        UpdateModuleStatsDisplay();
        PopulateModuleSelectButtons();
    }

    // ─────────────────────────────────────────────
    // 미네랄 ↔ 모듈포인트 모드 전환
    // ─────────────────────────────────────────────

    private void OnModeToggleClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_selectedModule == null) return;
        // 언락 전에는 버튼을 숨기지만, 방어적으로 한 번 더 체크 (언락 튜토리얼 진행 중에는 예외)
        bool isUnlockedOrTutorial = TutorialManager.Instance.IsMineralModeUnlocked()
            || TutorialActionGate.IsTutorial(TutorialManager.MINERAL_MODE_UNLOCK_TUTORIAL_ID);
        if (isUnlockedOrTutorial == false) return;
        // 미네랄 투자 이력이 있으면 모듈포인트 모드로 복귀 불가
        if (m_selectedModule.m_isMineralMode == true && m_selectedModule.m_investedMineral > 0) return;
        m_selectedModule.m_isMineralMode = !m_selectedModule.m_isMineralMode;
        if (m_selectedModule is ModulePlaceholder)
            RefreshUnlockButton();
        else
            RefreshModuleActionButtons();
    }

    private void RefreshModeToggleButton(int investedMineral)
    {
        if (m_btnModeToggle == null) return;

        // 미네랄 강화 모드는 특정 스테이지 이후 전멸/후퇴로 언락되기 전까지 버튼 자체를 숨김
        // 단, 언락 튜토리얼 진행 중에는 완료 전이라도 버튼을 보여줘야 함(이 튜토리얼이 곧 언락 완료 조건이므로)
        bool isUnlocked = TutorialManager.Instance.IsMineralModeUnlocked()
            || TutorialActionGate.IsTutorial(TutorialManager.MINERAL_MODE_UNLOCK_TUTORIAL_ID);
        m_btnModeToggle.gameObject.SetActive(isUnlocked);
        if (isUnlocked == false) return;

        // 미네랄 투자 이력이 있으면 모드 고정 → 토글 비활성화
        string colorKey = m_selectedModule.m_isMineralMode == true ? "Mineral" : "ModulePoint";
        m_btnModeToggle.SetActiveColorKey(colorKey);
        m_btnModeToggle.SetInteractable(investedMineral == 0);
        if (m_textModeToggle != null)
            m_textModeToggle.text = m_selectedModule.m_isMineralMode == true ? "미네랄" : "모듈포인트";
    }

    private void ApplyButtonColorKey(string colorKey)
    {
        m_gradeUpModuleButton.SetActiveColorKey(colorKey);
        m_gradeDownModuleButton.SetActiveColorKey(colorKey);
        m_levelUpModuleButton.SetActiveColorKey(colorKey);
        m_levelDownModuleButton.SetActiveColorKey(colorKey);
    }

    private void OnModulePointChanged(int modulePoint)
    {
        if (bShow == false) return;
        if (m_selectedModule == null) return;
        if (m_selectedModule is ModulePlaceholder)
            RefreshUnlockButton();
        else
            RefreshModuleActionButtons();
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();

        if (m_playerFleet == null) { Debug.LogError("UITabShip / OnTabActivated / m_playerFleet == null"); return;} 

        if (m_selectedShip == null)
            m_selectedShip = m_playerFleet.m_ships[0];
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

        if (m_playerFleet != null)
            m_playerFleet.ClearAllSelectedModule();
    }

    // ─────────────────────────────────────────────
    // 함선 네비게이터 (< / >)
    // ─────────────────────────────────────────────

    private void OnPrevShipClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_playerFleet == null || m_playerFleet.m_ships.Count == 0) return;
        int idx = m_playerFleet.m_ships.IndexOf(m_selectedShip);
        int next = (idx - 1 + m_playerFleet.m_ships.Count) % m_playerFleet.m_ships.Count;
        SelectShip(m_playerFleet.m_ships[next]);
    }

    private void OnNextShipClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_playerFleet == null || m_playerFleet.m_ships.Count == 0) return;
        int idx = m_playerFleet.m_ships.IndexOf(m_selectedShip);
        int next = (idx + 1) % m_playerFleet.m_ships.Count;
        SelectShip(m_playerFleet.m_ships[next]);
    }

    private void SelectShip(SpaceShip ship)
    {
        if (ship == null || ship == m_selectedShip) return;

        m_selectedShip = ship;
        CameraController.Instance.SetTargetOfCameraController(m_selectedShip.transform);

        if (m_playerFleet != null) m_playerFleet.ClearAllSelectedModule();

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
        if (ship == null) return;
        if (m_selectedShip == ship) return;

        m_selectedShip = ship;
        CameraController.Instance.SetTargetOfCameraController(m_selectedShip.transform);

        if (m_playerFleet != null) m_playerFleet.ClearAllSelectedModule();

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
        if (m_playerFleet == null) return;
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
            m_textShipName.text = CommonUtility.GetShipDisplayName(m_selectedShip);

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

    
#region 모듈 해금 begin -------------------------------------------------------------
    private void OnModuleUnlockClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_isUnlockPending == true) return;
        if (m_myCommander == null) return;
        if (m_selectedShip == null || m_selectedModule == null) return;
        if ((m_selectedModule is ModulePlaceholder) == false) return;

        if (m_selectedModule.m_isMineralMode == false)
            ExecuteModuleUnlock();
        else
            ExecuteModuleUnlockMineral();
    }

    private void ExecuteModuleUnlock()
    {
        // 튜토리얼 중(지크프리트 함대)에는 서버 미등록 함선이라 서버 호출이 항상 실패하므로 로컬에서만 처리
        if (TutorialActionGate.IsTutorial("Tutorial_FirstPlay_ManageShip"))
        {
            ExecuteModuleUnlockTutorialOnly();
            return;
        }

        var unlockRequest = new ModuleUnlockRequest
        {
            shipId     = m_selectedShip.m_shipInfo.id,
            bodyIndex  = m_selectedModule.GetModuleBodyIndex(),
            moduleType = m_selectedModule.m_moduleSlot.m_moduleSlotInfo.moduleType,
            slotIndex  = m_selectedModule.m_moduleSlot.m_moduleSlotInfo.slotIndex
        };

        m_isUnlockPending = true;
        m_unlockModuleButton.SetInteractable(false);
        NetworkManager.Instance.UnlockModule(unlockRequest, OnModuleUnlockResponse);
    }

    // 튜토리얼 전용 — 서버 호출 없이 클라이언트에서만 모듈 언락 (지크프리트 함대는 서버 기록 대상 아님)
    private void ExecuteModuleUnlockTutorialOnly()
    {
        int unlockPrice = DataManager.Instance.m_dataTableConfig.gameSettings.moduleUnlockPrice;
        if (TutorialActionGate.TryConsumeModulePoint(unlockPrice) == false) return;

        EModuleType moduleType = m_selectedModule.m_moduleSlot.m_moduleSlotInfo.moduleType;
        EModuleSubType firstSubType = DataManager.Instance.GetFirstSubType(moduleType);

        m_selectedShip.Apply_UnlockModule(m_selectedModule.GetModuleBodyIndex(), moduleType, firstSubType,
            m_selectedModule.m_moduleSlot.m_moduleSlotInfo.slotIndex, unlockPrice, 0);
        EventManager.Trigger_ShipStatsChanged(m_selectedShip);

        PopulateModuleSelectButtons();
        ReselectReplacedModule(m_selectedShip, m_selectedModule.GetModuleBodyIndex(), moduleType, firstSubType,
            m_selectedModule.m_moduleSlot.m_moduleSlotInfo.slotIndex);
    }
    private void OnModuleUnlockResponse(ApiResponse<ModuleUnlockResponse> response)
    {
        m_isUnlockPending = false;
        if (response.errorCode == 0)
            Apply_ModuleUnlock(response.data);
        else
        {
            ShowErrorMessage($"Module unlock failed: {ErrorCodeMapping.GetMessage(response.errorCode)}");
            RefreshUnlockButton();
        }
    }
    private void Apply_ModuleUnlock(ModuleUnlockResponse unlockData)
    {
        if (unlockData == null) return;

        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        commander.UpdateModulePoint(unlockData.pointRemain);

        SpaceFleet fleet = ObjectManager.Instance.GetMyFleet();
        if (fleet == null) return;
        SpaceShip targetShip = fleet.FindShip(unlockData.shipId);
        if (targetShip == null) return;

        targetShip.Apply_UnlockModule(unlockData.bodyIndex, unlockData.moduleType, unlockData.moduleSubType, unlockData.slotIndex,
            unlockData.investedPoint, 0);
        EventManager.Trigger_ShipStatsChanged(targetShip);

        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == unlockData.shipId)
        {
            PopulateModuleSelectButtons();
            ReselectReplacedModule(targetShip, unlockData.bodyIndex, unlockData.moduleType, unlockData.moduleSubType, unlockData.slotIndex);
        }
    }

    private void ExecuteModuleUnlockMineral()
    {
        int  unlockPrice   = DataManager.Instance.m_dataTableConfig.gameSettings.moduleUnlockPrice;
        long playerMineral = m_myCommander.GetMineral();
        if (playerMineral < unlockPrice)
        {
            ShowErrorMessage($"Insufficient mineral (need {CommonUtility.FormatBigNumber(unlockPrice)}, have {CommonUtility.FormatBigNumber(playerMineral)})");
            return;
        }

        var req = new ModuleUnlockRequest
        {
            shipId     = m_selectedShip.m_shipInfo.id,
            bodyIndex  = m_selectedModule.GetModuleBodyIndex(),
            moduleType = m_selectedModule.m_moduleSlot.m_moduleSlotInfo.moduleType,
            slotIndex  = m_selectedModule.m_moduleSlot.m_moduleSlotInfo.slotIndex
        };
        m_isUnlockPending = true;
        m_unlockModuleButton.SetInteractable(false);
        NetworkManager.Instance.ModuleUnlockMineral(req, OnModuleUnlockMineralResponse);
    }
    private void OnModuleUnlockMineralResponse(ApiResponse<ModuleUnlockResponse> response)
    {
        m_isUnlockPending = false;
        if (response.errorCode == 0)
            Apply_ModuleUnlockMineral(response.data);
        else
        {
            ShowErrorMessage($"Mineral unlock failed: {ErrorCodeMapping.GetMessage(response.errorCode)}");
            RefreshUnlockButton();
        }
    }
    private void Apply_ModuleUnlockMineral(ModuleUnlockResponse unlockData)
    {
        if (unlockData == null) return;
        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        commander.UpdateMineral(unlockData.pointRemain);

        SpaceFleet fleet = ObjectManager.Instance.GetMyFleet();
        if (fleet == null) return;
        SpaceShip targetShip = fleet.FindShip(unlockData.shipId);
        if (targetShip == null) return;

        targetShip.Apply_UnlockModule(unlockData.bodyIndex, unlockData.moduleType, unlockData.moduleSubType, unlockData.slotIndex, 0, unlockData.investedPoint);
        EventManager.Trigger_ShipStatsChanged(targetShip);

        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == unlockData.shipId)
        {
            PopulateModuleSelectButtons();
            ReselectReplacedModule(targetShip, unlockData.bodyIndex, unlockData.moduleType, unlockData.moduleSubType, unlockData.slotIndex);
        }
    }
#endregion 모듈 해금 end -------------------------------------------------------------

#region 모듈 레벨 업/다운 begin -------------------------------------------------------------
    private void RegisterLevelUpPointerEvents()
    {
        m_levelUpModuleButton.GetButton().onClick.AddListener(OnLevelUpButtonClicked);
    }

    private void OnLevelUpButtonClicked()
    {
        if (m_levelUpModuleButton.GetButton().interactable == false) return;
        if (m_bModuleChanging == true) return;
        ExecuteModuleLevelUp();
    }

    private void ExecuteModuleLevelUp()
    {
        if (m_selectedShip == null || m_selectedModule == null) return;
        if (m_selectedModule is ModulePlaceholder == true) return;

        // 튜토리얼 중(지크프리트 함대)에는 서버 미등록 함선이라 서버 호출이 항상 실패하므로 로컬에서만 처리
        if (TutorialActionGate.IsTutorial("Tutorial_FirstPlay_ManageShip"))
        {
            ExecuteModuleLevelUpTutorialOnly();
            return;
        }

        if (m_selectedModule.m_isMineralMode == true)
        {
            ExecuteModuleLevelUpMineral();
            return;
        }

        int currentLevel = m_selectedModule.GetModuleLevel();
        int targetLevel  = currentLevel + 1;

        if (DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_selectedModule.GetModuleSubType(), targetLevel) == null)
        {
            ShowErrorMessage(LocalizationManager.Instance.Get("LevelupButtonTextMax"));
            return;
        }

        var commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;
        if (DataManager.Instance.GetModuleLevelUpCost(m_selectedModule.GetModuleSubType(), currentLevel, out int cost) == false) return;
        if (commander.CheckEnoughModulePoint(cost) == false)
        {
            ShowErrorMessage(LocalizationManager.Instance.Get("insufficient_module_point"));
            return;
        }

        var req = new ModuleLevelChangeRequest
        {
            shipId        = m_selectedShip.m_shipInfo.id,
            bodyIndex     = m_selectedModule.GetModuleBodyIndex(),
            moduleType    = m_selectedModule.GetModuleType(),
            moduleSubType = m_selectedModule.GetModuleSubType(),
            slotIndex     = m_selectedModule.GetSlotIndex(),
            currentLevel  = currentLevel,
            targetLevel   = targetLevel
        };
        m_bModuleChanging = true;
        NetworkManager.Instance.LevelUpModule(req, OnModuleLevelUpResponse);
    }

    // 튜토리얼 전용 — 서버 호출 없이 클라이언트에서만 레벨업 (지크프리트 함대는 서버 기록 대상 아님)
    private void ExecuteModuleLevelUpTutorialOnly()
    {
        int currentLevel = m_selectedModule.GetModuleLevel();
        int targetLevel  = currentLevel + 1;

        if (DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_selectedModule.GetModuleSubType(), targetLevel) == null)
        {
            ShowErrorMessage(LocalizationManager.Instance.Get("LevelupButtonTextMax"));
            return;
        }

        if (DataManager.Instance.GetModuleLevelUpCost(m_selectedModule.GetModuleSubType(), currentLevel, out int cost) == false) return;
        if (TutorialActionGate.TryConsumeModulePoint(cost) == false)
        {
            ShowErrorMessage(LocalizationManager.Instance.Get("insufficient_module_point"));
            return;
        }

        SoundManager.Instance.PlayFX(EFx.Level_Up, retrigger: true);
        Apply_ModuleLevelChange(m_selectedShip.m_shipInfo.id, m_selectedModule.GetModuleBodyIndex(), m_selectedModule.GetModuleType(),
            m_selectedModule.GetModuleSubType(), m_selectedModule.GetSlotIndex(), targetLevel, isLevelUp: true);
    }
    private void OnModuleLevelUpResponse(ApiResponse<ModuleLevelChangeResponse> response)
    {
        m_bModuleChanging = false;
        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        if (response.errorCode == 0)
        {
            SoundManager.Instance.PlayFX(EFx.Level_Up, retrigger: true);
            commander.UpdateModulePoint(response.data.pointRemain);
            Apply_ModuleLevelChange(response.data.shipId, response.data.bodyIndex, response.data.moduleType,
                response.data.moduleSubType, response.data.slotIndex, response.data.newLevel, isLevelUp: true);
        }
        else
        {
            string msg = ErrorCodeMapping.GetMessage(response.errorCode);
            Debug.LogError($"LevelUp failed: {msg}");
            ShowErrorMessage($"LevelUp failed: {msg}");
        }
    }
    private void Apply_ModuleLevelChange(long shipId, int bodyIndex, EModuleType moduleType,
        EModuleSubType moduleSubType, int slotIndex, int newLevel, bool isLevelUp)
    {
        if (m_playerFleet == null) return;
        SpaceShip ship = m_playerFleet.FindShip(shipId);
        if (ship == null) return;

        ModuleBase prevModule = ship.FindModule(bodyIndex, moduleType, slotIndex);
        int prevLevel         = 0;
        int prevInvestedPoint = 0;
        if (prevModule != null)
        {
            prevLevel         = prevModule.GetModuleLevel();
            prevInvestedPoint = prevModule.m_investedModulePoint;
        }

        int pointDelta = 0;
        int fromLv     = Mathf.Min(prevLevel, newLevel);
        int toLv       = Mathf.Max(prevLevel, newLevel);
        for (int lv = fromLv; lv < toLv; lv++)
        {
            if (DataManager.Instance.GetModuleLevelUpCost(moduleSubType, lv, out int cost) == true)
                pointDelta += cost;
        }
        int newInvestedPoint = isLevelUp == true
            ? prevInvestedPoint + pointDelta
            : prevInvestedPoint - pointDelta;
        ship.ApplyModuleChange(bodyIndex, moduleType, moduleSubType, slotIndex, newLevel, 0, newInvestedPoint);

        EventManager.Trigger_ShipStatsChanged(ship);

        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == shipId)
        {
            UpdateShipHeader();
            PopulateModuleSelectButtons();
            ReselectReplacedModule(ship, bodyIndex, moduleType, moduleSubType, slotIndex);
        }
    }

    private void ExecuteModuleLevelUpMineral()
    {
        int currentLevel = m_selectedModule.GetModuleLevel();
        int targetLevel  = currentLevel + 1;

        ModuleData nextData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_selectedModule.GetModuleSubType(), targetLevel);
        if (nextData == null)
        {
            ShowErrorMessage(LocalizationManager.Instance.Get("LevelupButtonTextMax"));
            return;
        }

        int  mineralCost  = nextData.mineralCost;
        var  commander    = DataManager.Instance.m_currentCommander;
        if (commander == null) return;
        if (commander.GetMineral() < mineralCost)
        {
            ShowErrorMessage(LocalizationManager.Instance.Get("insufficient_mineral"));
            return;
        }

        var req = new ModuleLevelChangeRequest
        {
            shipId        = m_selectedShip.m_shipInfo.id,
            bodyIndex     = m_selectedModule.GetModuleBodyIndex(),
            moduleType    = m_selectedModule.GetModuleType(),
            moduleSubType = m_selectedModule.GetModuleSubType(),
            slotIndex     = m_selectedModule.GetSlotIndex(),
            currentLevel  = currentLevel,
            targetLevel   = targetLevel
        };
        m_bModuleChanging = true;
        NetworkManager.Instance.ModuleLevelUpMineral(req, OnModuleLevelUpMineralResponse);
    }
    private void OnModuleLevelUpMineralResponse(ApiResponse<ModuleLevelChangeResponse> response)
    {
        m_bModuleChanging = false;
        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        if (response.errorCode == 0)
        {
            SoundManager.Instance.PlayFX(EFx.Level_Up, retrigger: true);
            commander.UpdateMineral(response.data.pointRemain);
            Apply_ModuleLevelChangeMineral(response.data);
        }
        else
        {
            string msg = ErrorCodeMapping.GetMessage(response.errorCode);
            Debug.LogError($"Mineral LevelUp failed: {msg}");
            ShowErrorMessage($"Mineral LevelUp failed: {msg}");
        }
    }
    private void Apply_ModuleLevelChangeMineral(ModuleLevelChangeResponse data)
    {
        if (m_playerFleet == null) return;
        SpaceShip ship = m_playerFleet.FindShip(data.shipId);
        if (ship == null) return;

        // ApplyModuleChange가 모듈을 재생성하므로 기존 모듈포인트 투자값을 먼저 보존
        ModuleBase prevModule = ship.FindModule(data.bodyIndex, data.moduleType, data.slotIndex);
        int savedInvestedModulePoint = prevModule != null ? prevModule.m_investedModulePoint : 0;

        ship.ApplyModuleChange(data.bodyIndex, data.moduleType, data.moduleSubType, data.slotIndex, data.newLevel, data.investedPoint, savedInvestedModulePoint);

        EventManager.Trigger_ShipStatsChanged(ship);

        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == data.shipId)
        {
            UpdateShipHeader();
            PopulateModuleSelectButtons();
            ReselectReplacedModule(ship, data.bodyIndex, data.moduleType, data.moduleSubType, data.slotIndex);
        }
    }
    
    private void OnModuleLevelDownClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_bModuleChanging == true) return;
        if (m_selectedShip == null || m_selectedModule == null) return;
        if (m_selectedModule is ModulePlaceholder == true) return;

        bool isMineralMode = m_selectedModule.m_isMineralMode;
        if (isMineralMode == false)
            ExecuteModuleLevelDown();
        else
            ExecuteModuleLevelDownMineral();
    }
    private void ExecuteModuleLevelDown()
    {
        int currentLevel = m_selectedModule.GetModuleLevel();

        if (currentLevel == 1)
        {
            // T1 Lv.1: 더 내릴 단계 없으면 플레이스홀더 복귀
            EModuleSubType prevSubType = GetPrevSubType(m_selectedModule.GetModuleSubType());
            if (prevSubType == EModuleSubType.none)
            {
                ExecuteResetModule();
                return;
            }
            // T2 이상 Lv.1: targetLevel=0 으로 서버에 이전 단계 맥스레벨 처리 위임
        }

        var req = new ModuleLevelChangeRequest
        {
            shipId        = m_selectedShip.m_shipInfo.id,
            bodyIndex     = m_selectedModule.GetModuleBodyIndex(),
            moduleType    = m_selectedModule.GetModuleType(),
            moduleSubType = m_selectedModule.GetModuleSubType(),
            slotIndex     = m_selectedModule.GetSlotIndex(),
            currentLevel  = currentLevel,
            targetLevel   = currentLevel - 1  // Lv.1이면 0 → 서버에서 이전 단계 맥스레벨 처리
        };
        m_bModuleChanging = true;
        NetworkManager.Instance.LevelDownModule(req, OnModuleLevelDownResponse);
    }
    private void OnModuleLevelDownResponse(ApiResponse<ModuleLevelChangeResponse> response)
    {
        m_bModuleChanging = false;
        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        if (response.errorCode == 0)
        {
            SoundManager.Instance.PlayFX(EFx.Level_Down, retrigger: true);
            commander.UpdateModulePoint(response.data.pointRemain);

            if (m_playerFleet == null) return;
            SpaceShip ship = m_playerFleet.FindShip(response.data.shipId);
            if (ship == null) return;

            ship.ApplyModuleChange(response.data.bodyIndex, response.data.moduleType,
                response.data.moduleSubType, response.data.slotIndex, response.data.newLevel, 0, response.data.investedPoint);

            EventManager.Trigger_ShipStatsChanged(ship);

            if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == response.data.shipId)
            {
                UpdateShipHeader();
                PopulateModuleSelectButtons();
                ReselectReplacedModule(ship, response.data.bodyIndex, response.data.moduleType,
                    response.data.moduleSubType, response.data.slotIndex);
            }
        }
        else
        {
            string msg = ErrorCodeMapping.GetMessage(response.errorCode);
            Debug.LogError($"LevelDown failed: {msg}");
            ShowErrorMessage($"LevelDown failed: {msg}");
        }
    }
    private void ExecuteModuleLevelDownMineral()
    {
        int currentLevel = m_selectedModule.GetModuleLevel();

        // Lv.1은 해당 그레이드의 최저 레벨이라 내릴 레벨이 없음 → 그레이드다운과 동일하게 처리
        if (currentLevel == 1)
        {
            ExecuteModuleGradeDownMineral();
            return;
        }

        bool hasBaseline = DataManager.Instance.CalcModulePointBaseline(
            m_selectedModule.GetModuleType(), m_selectedModule.m_investedModulePoint,
            out EModuleSubType baselineSubType, out int baselineLevel);
        bool isSameSubType  = hasBaseline == true && baselineSubType == m_selectedModule.GetModuleSubType();
        bool atOrBelowBaseline = isSameSubType == true && currentLevel <= baselineLevel;

        if (atOrBelowBaseline == true)
        {
            ShowErrorMessage("미네랄 기준점 아래로 내릴 수 없습니다");
            return;
        }

        var req = new ModuleLevelChangeRequest
        {
            shipId        = m_selectedShip.m_shipInfo.id,
            bodyIndex     = m_selectedModule.GetModuleBodyIndex(),
            moduleType    = m_selectedModule.GetModuleType(),
            moduleSubType = m_selectedModule.GetModuleSubType(),
            slotIndex     = m_selectedModule.GetSlotIndex(),
            currentLevel  = currentLevel,
            targetLevel   = currentLevel - 1
        };
        m_bModuleChanging = true;
        NetworkManager.Instance.ModuleLevelDownMineral(req, OnModuleLevelDownMineralResponse);
    }
    private void OnModuleLevelDownMineralResponse(ApiResponse<ModuleLevelChangeResponse> response)
    {
        m_bModuleChanging = false;
        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        if (response.errorCode == 0)
        {
            SoundManager.Instance.PlayFX(EFx.Level_Down, retrigger: true);
            commander.UpdateMineral(response.data.pointRemain);
            Apply_ModuleLevelChangeMineral(response.data);
        }
        else
        {
            string msg = ErrorCodeMapping.GetMessage(response.errorCode);
            Debug.LogError($"Mineral LevelDown failed: {msg}");
            ShowErrorMessage($"Mineral LevelDown failed: {msg}");
        }
    }
  

    

#endregion 모듈 레벨 업/다운 end -------------------------------------------------------------

#region 모듈 그래이드 업/다운 begin -------------------------------------------------------------
    private void OnModuleGradeUpClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_bModuleChanging == true) return;
        if (m_selectedShip == null || m_selectedModule == null) return;
        if (m_selectedModule is ModulePlaceholder == true) return;
        EModuleSubType nextSubType = GetNextSubType(m_selectedModule.GetModuleSubType());
        if (nextSubType == EModuleSubType.none) return;

        if (m_selectedModule.m_isMineralMode == false)
            ExecuteModuleGradeUp();
        else
            ExecuteModuleGradeUpMineral(nextSubType);
    }
    private void ExecuteModuleGradeUp()
    {
        int slotIndex = 0;
        if (m_selectedModule.GetModuleType() != EModuleType.body)
            slotIndex = m_selectedModule.m_moduleSlot.m_moduleSlotInfo.slotIndex;

        // 튜토리얼 중(지크프리트 함대)에는 서버 미등록 함선이라 서버 호출이 항상 실패하므로 로컬에서만 처리
        if (TutorialActionGate.IsTutorial("Tutorial_FirstPlay_ManageShip"))
        {
            ExecuteModuleGradeUpTutorialOnly(slotIndex);
            return;
        }

        var req = new ModuleGradeChangeRequest
        {
            shipId               = m_selectedShip.m_shipInfo.id,
            bodyIndex            = m_selectedModule.GetModuleBodyIndex(),
            slotIndex            = slotIndex,
            moduleType           = m_selectedModule.GetModuleType(),
            moduleSubTypeCurrent = m_selectedModule.GetModuleSubType()
        };
        m_bModuleChanging = true;
        NetworkManager.Instance.ModuleGradeUp(req, OnModuleGradeUpResponse);
    }

    // 튜토리얼 전용 — 서버 호출 없이 클라이언트에서만 그레이드업 (지크프리트 함대는 서버 기록 대상 아님)
    // 비용은 레벨업과 동일하게 DataTableUpgradeCost(등급별 모듈포인트 비용 테이블)에서 조회
    private void ExecuteModuleGradeUpTutorialOnly(int slotIndex)
    {
        EModuleSubType nextSubType = GetNextSubType(m_selectedModule.GetModuleSubType());
        if (nextSubType == EModuleSubType.none) return;

        int cost = DataManager.Instance.GetModuleResearchCost(nextSubType);
        if (TutorialActionGate.TryConsumeModulePoint(cost) == false) return;

        int bodyIndex = m_selectedModule.GetModuleBodyIndex();
        EModuleType moduleType = m_selectedModule.GetModuleType();

        m_selectedShip.ApplyModuleChange(bodyIndex, moduleType, nextSubType, slotIndex, 1, 0, cost);

        PopulateModuleSelectButtons();
        ReselectReplacedModule(m_selectedShip, bodyIndex, moduleType, nextSubType, slotIndex);
    }
    private void ExecuteModuleGradeUpMineral(EModuleSubType targetSubType)
    {
        long levelUpToMaxCost = CalcMineralLevelUpToMaxCost(m_selectedModule.GetModuleSubType(), m_selectedModule.GetModuleLevel());
        long gradeUpCost      = DataManager.Instance.m_dataTableUpgradeCost.GetCost(targetSubType);
        long totalCost        = levelUpToMaxCost + gradeUpCost;

        var commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;
        if (commander.GetMineral() < totalCost)
        {
            ShowErrorMessage(LocalizationManager.Instance.Get("insufficient_mineral"));
            return;
        }

        int slotIndex = m_selectedModule.GetModuleType() == EModuleType.body
            ? 0
            : m_selectedModule.m_moduleSlot.m_moduleSlotInfo.slotIndex;

        var req = new ModuleGradeChangeRequest
        {
            shipId               = m_selectedShip.m_shipInfo.id,
            bodyIndex            = m_selectedModule.GetModuleBodyIndex(),
            moduleType           = m_selectedModule.GetModuleType(),
            moduleSubTypeCurrent = m_selectedModule.GetModuleSubType(),
            slotIndex            = slotIndex
        };
        m_bModuleChanging = true;
        NetworkManager.Instance.ModuleGradeUpMineral(req, OnModuleGradeUpMineralResponse);
    }

    private void OnModuleGradeDownClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_bModuleChanging == true) return;
        if (m_selectedShip == null || m_selectedModule == null) return;
        if (m_selectedModule is ModulePlaceholder == true) return;

        if (m_selectedModule.m_isMineralMode == false)
            ExecuteModuleGradeDown();
        else
            ExecuteModuleGradeDownMineral();
    }
    private void ExecuteModuleGradeDown()
    {
        int slotIndex = 0;
        if (m_selectedModule.GetModuleType() != EModuleType.body)
            slotIndex = m_selectedModule.m_moduleSlot.m_moduleSlotInfo.slotIndex;

        var req = new ModuleGradeChangeRequest
        {
            shipId               = m_selectedShip.m_shipInfo.id,
            bodyIndex            = m_selectedModule.GetModuleBodyIndex(),
            slotIndex            = slotIndex,
            moduleType           = m_selectedModule.GetModuleType(),
            moduleSubTypeCurrent = m_selectedModule.GetModuleSubType()
        };
        m_bModuleChanging = true;
        NetworkManager.Instance.ModuleGradeDown(req, OnModuleGradeDownResponse);
    }
    private void ExecuteModuleGradeDownMineral()
    {
        int slotIndex = m_selectedModule.GetModuleType() == EModuleType.body
            ? 0
            : m_selectedModule.m_moduleSlot.m_moduleSlotInfo.slotIndex;

        var req = new ModuleGradeChangeRequest
        {
            shipId               = m_selectedShip.m_shipInfo.id,
            bodyIndex            = m_selectedModule.GetModuleBodyIndex(),
            moduleType           = m_selectedModule.GetModuleType(),
            moduleSubTypeCurrent = m_selectedModule.GetModuleSubType(),
            slotIndex            = slotIndex
        };
        m_bModuleChanging = true;
        NetworkManager.Instance.ModuleGradeDownMineral(req, OnModuleGradeDownMineralResponse);
    }

    private void OnModuleGradeUpResponse(ApiResponse<ModuleGradeChangeResponse> response)
    {
        m_bModuleChanging = false;
        if (response.errorCode == 0)
        {
            SoundManager.Instance.PlayFX(EFx.Grade_Up, retrigger: true);
            Apply_ModuleGradeChange(response.data);
        }
        else
            ShowErrorMessage($"Grade change failed: {ErrorCodeMapping.GetMessage(response.errorCode)}");
    }
    private void OnModuleGradeDownResponse(ApiResponse<ModuleGradeChangeResponse> response)
    {
        m_bModuleChanging = false;
        if (response.errorCode == 0)
        {
            SoundManager.Instance.PlayFX(EFx.Grade_Down, retrigger: true);
            Apply_ModuleGradeChange(response.data);
        }
        else
            ShowErrorMessage($"Grade change failed: {ErrorCodeMapping.GetMessage(response.errorCode)}");
    }
    private void Apply_ModuleGradeChange(ModuleGradeChangeResponse changeData)
    {
        if (changeData == null) return;
        if (m_playerFleet == null) return;

        var commander = DataManager.Instance.m_currentCommander;
        if (commander != null)
            commander.UpdateModulePoint(changeData.pointRemain);

        if (changeData.isShipRemoved == true)
        {
            RemoveShipFromFleet(changeData.removedShipId);
            return;
        }

        SpaceShip ship = m_playerFleet.FindShip(changeData.shipId);
        if (ship == null) return;

        if (changeData.isModuleRemoved == true)
        {
            ship.Apply_ResetModuleToPlaceholder(changeData.bodyIndex, changeData.moduleTypeCurrent, changeData.slotIndex);
            if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == changeData.shipId)
            {
                PopulateModuleSelectButtons();
                ModuleBase resetModule = ship.FindModule(changeData.bodyIndex, changeData.moduleTypeCurrent, changeData.slotIndex);
                EventManager.TriggerSpaceShipModuleSelected(m_selectedShip, resetModule);
            }
            return;
        }

        ship.ApplyModuleChange(changeData.bodyIndex, changeData.moduleTypeNew, changeData.moduleSubTypeNew,
            changeData.slotIndex, changeData.moduleNewLevel, 0, changeData.investedPoint);

        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == changeData.shipId)
        {
            PopulateModuleSelectButtons();
            ReselectReplacedModule(ship, changeData.bodyIndex, changeData.moduleTypeNew,
                changeData.moduleSubTypeNew, changeData.slotIndex);
        }
    }

    private void OnModuleGradeUpMineralResponse(ApiResponse<ModuleGradeChangeResponse> response)
    {
        m_bModuleChanging = false;
        if (response.errorCode == 0)
        {
            SoundManager.Instance.PlayFX(EFx.Grade_Up, retrigger: true);
            Apply_ModuleGradeChangeMineral(response.data);
        }
        else
            ShowErrorMessage($"Mineral grade change failed: {ErrorCodeMapping.GetMessage(response.errorCode)}");
    }
    private void OnModuleGradeDownMineralResponse(ApiResponse<ModuleGradeChangeResponse> response)
    {
        m_bModuleChanging = false;
        if (response.errorCode == 0)
        {
            SoundManager.Instance.PlayFX(EFx.Grade_Down, retrigger: true);
            Apply_ModuleGradeChangeMineral(response.data);
        }
        else
            ShowErrorMessage($"Mineral grade change failed: {ErrorCodeMapping.GetMessage(response.errorCode)}");
    }
    private void Apply_ModuleGradeChangeMineral(ModuleGradeChangeResponse data)
    {
        if (data == null) return;
        if (m_playerFleet == null) return;

        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander != null)
            commander.UpdateMineral(data.pointRemain);

        if (data.isShipRemoved == true)
        {
            RemoveShipFromFleet(data.removedShipId);
            return;
        }

        SpaceShip ship = m_playerFleet.FindShip(data.shipId);
        if (ship == null) return;

        if (data.isModuleRemoved == true)
        {
            ship.Apply_ResetModuleToPlaceholder(data.bodyIndex, data.moduleTypeCurrent, data.slotIndex);
        }
        else
        {
            ModuleBase prevModule = ship.FindModule(data.bodyIndex, data.moduleTypeNew, data.slotIndex);
            int savedInvestedModulePoint = prevModule != null ? prevModule.m_investedModulePoint : 0;

            ship.ApplyModuleChange(data.bodyIndex, data.moduleTypeNew, data.moduleSubTypeNew, data.slotIndex, data.moduleNewLevel, data.investedPoint, savedInvestedModulePoint);
        }

        EventManager.Trigger_ShipStatsChanged(ship);

        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == data.shipId)
        {
            PopulateModuleSelectButtons();
            ReselectReplacedModule(ship, data.bodyIndex, data.moduleTypeNew, data.moduleSubTypeNew, data.slotIndex);
        }
    }
#endregion 모듈 그래이드 업/다운 end -------------------------------------------------------------



    // ─────────────────────────────────────────────
    // 모듈 디테일 카드 갱신
    // ─────────────────────────────────────────────

    private void UpdateModuleStatsDisplay()
    {
        if (bShow != true) return;
        if (m_selectedShip == null) return;

        m_moduleLevelName.SetActive(false);
        m_moduleLevelText.gameObject.SetActive(false);
        foreach (var row in m_statsRows)   row.Hide();

        // 미네랄 투자 이력 있으면 미네랄 모드 고정, 없으면 모듈포인트 모드로 초기화
        int investedMineral = m_selectedModule.m_investedMineral;
        if (investedMineral > 0)
            m_selectedModule.m_isMineralMode = true;
        else
            m_selectedModule.m_isMineralMode = false;

        if (m_selectedModule is ModulePlaceholder)
        {
            m_unlockModuleContainer.SetActive(true);
            m_moduleStatusContainer.gameObject.SetActive(false);
            //if (m_btnResetModule != null) m_btnResetModule.gameObject.SetActive(false);
            string placeholderText = LocalizationManager.Instance.Get($"module_type_{m_selectedModule.GetModuleType()}_placeholder");
            if (m_unlockModuleSubTypeText != null) m_unlockModuleSubTypeText.text = placeholderText;
            RefreshUnlockButton();
        }
        else
        {
            m_unlockModuleContainer.SetActive(false);
            m_moduleStatusContainer.gameObject.SetActive(true);

            EModuleSubType subType = m_selectedModule.GetModuleSubType();
            int level              = m_selectedModule.GetModuleLevel();

            // if (m_btnResetModule != null)
            // {
            //     m_btnResetModule.gameObject.SetActive(true);
            //     bool isFlagshipBody = m_selectedShip.m_shipInfo.positionIndex == 0
            //                        && m_selectedModule.GetModuleType() == EModuleType.body;
            //     bool isDefaultBody  = m_selectedModule.GetModuleSubType() == EModuleSubType.body_t1_m1
            //                        && m_selectedModule.GetModuleLevel() == 1;
            //     m_btnResetModule.interactable = !(isFlagshipBody && isDefaultBody);
            // }

            m_moduleSubTypeText.text = subType.GetLocalizedName();

            ModuleData cur = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, level);
            if (cur != null && m_statsRows.Count >= 2)
            {
                m_moduleLevelText.SetText("{0}", level);
                m_moduleLevelName.SetActive(true);
                m_moduleLevelText.gameObject.SetActive(true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(m_moduleLevelText.transform.parent as RectTransform);

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

            m_investedModulePointText.SetText("{0}", m_selectedModule.m_investedModulePoint);
            m_investedMineralText.SetText("{0}", investedMineral);
            m_investedMineral.gameObject.SetActive(investedMineral > 0);
            RefreshModuleActionButtons();

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_moduleStatsContainer as RectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_investedModulePoint);
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_investedMineral);
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_investedTotal);
        }
    }

    // ─────────────────────────────────────────────
    // 버튼 상태 갱신
    // ─────────────────────────────────────────────

    private void RefreshUnlockButton()
    {
        if (m_unlockModuleButton == null) return;

        // ModulePlaceholder는 investedMineral 없으므로 0으로 처리
        RefreshModeToggleButton(0);

        var commander   = DataManager.Instance.m_currentCommander;
        int unlockPrice = DataManager.Instance.m_dataTableConfig.gameSettings.moduleUnlockPrice;

        if (m_selectedModule.m_isMineralMode == true)
        {
            long playerMineral = commander != null ? commander.GetMineral() : 0;
            m_unlockModuleButton.SetActiveColorKey("Mineral");
            m_unlockModuleButton.SetInteractable(playerMineral >= unlockPrice);
        }
        else
        {
            long playerPoint = commander != null ? commander.GetModulePoint() : 0;
            m_unlockModuleButton.SetActiveColorKey("ModulePoint");
            m_unlockModuleButton.SetInteractable(playerPoint >= unlockPrice);
        }
    }

    private void RefreshModuleActionButtons()
    {
        if (m_selectedModule == null) return;

        EModuleSubType subType        = m_selectedModule.GetModuleSubType();
        int            level          = m_selectedModule.GetModuleLevel();
        int            investedMineral = m_selectedModule.m_investedMineral;
        bool           isMaxLevel     = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, level + 1) == null;
        EModuleSubType nextSubType    = GetNextSubType(subType);
        EModuleSubType prevSubType    = GetPrevSubType(subType);

        RefreshModeToggleButton(investedMineral);

        if (m_selectedModule.m_isMineralMode == true)
        {
            ApplyButtonColorKey("Mineral");
            var  commander     = DataManager.Instance.m_currentCommander;
            long playerMineral = commander != null ? commander.GetMineral() : 0;

            RefreshMineralGradeUpButton(subType, level, nextSubType, playerMineral);
            RefreshMineralGradeDownButton(subType, level, prevSubType, investedMineral);
            RefreshMineralLevelUpButton(subType, level, isMaxLevel, playerMineral);
            RefreshMineralLevelDownButton(subType, level, prevSubType, investedMineral);
        }
        else
        {
            ApplyButtonColorKey("ModulePoint");
            var  commander   = DataManager.Instance.m_currentCommander;
            int  commanderLevel = commander != null ? commander.GetCommanderLevel() : 0;
            int  playerSubtypeLevel  = DataManager.Instance.m_dataTableCommanderLevel.GetSubtypeLevel(commanderLevel);
            long playerModulePoint = commander != null ? commander.GetModulePoint() : 0;

            RefreshGradeUpButton(subType, level, nextSubType, playerSubtypeLevel, playerModulePoint);
            RefreshGradeDownButton(subType, level, prevSubType);
            RefreshLevelUpButton(subType, level, isMaxLevel, playerSubtypeLevel, playerModulePoint);
            RefreshLevelDownButton(subType, level, prevSubType);
        }
    }

    private void RefreshGradeUpButton(EModuleSubType subType, int level, EModuleSubType nextSubType, int playerSubtypeLevel, long playerModulePoint)
    {
        if (nextSubType == EModuleSubType.none)
        {
            m_gradeUpModuleButton.SetInteractable(false);
            m_gradeUpModuleButtonText1.text = LocalizationManager.Instance.Get("UITabShip_GradeUp");
            m_gradeUpModuleButtonText2.Hide();
            return;
        }

        long levelUpToMaxCost = CalcLevelUpToMaxCost(subType, level);
        long gradeUpCost      = DataManager.Instance.m_dataTableUpgradeCost.GetCost(nextSubType);
        long totalCost        = levelUpToMaxCost + gradeUpCost;
        int  reqTier          = nextSubType.GetTechTier();
        bool hasTech          = playerSubtypeLevel >= reqTier;
        bool hasPoint         = playerModulePoint >= totalCost;
        bool canUpgrade       = hasTech == true && hasPoint == true;

        m_gradeUpModuleButton.SetInteractable(canUpgrade);

        // 우선순위: 1)기술레벨 부족 → 2)포인트 부족 → 3)업그레이드 가능
        if (canUpgrade == true)
        {
            m_gradeUpModuleButtonText1.text = LocalizationManager.Instance.Get("UITabShip_GradeUp");
            m_gradeUpModuleButtonText2.SetRow("mineral_basic", $"-{totalCost}");
            m_gradeUpModuleButtonText2.SetImageColor(CommonUtility.PaletteColor("ModulePoint"));
        }
        else if (hasTech == false)
        {
            m_gradeUpModuleButtonText1.text = LocalizationManager.Instance.Get("UITabShip_Require");
            SetDisabledReason(m_gradeUpModuleButtonText2, false, reqTier, totalCost);
        }
        else
        {
            m_gradeUpModuleButtonText1.text = LocalizationManager.Instance.Get("UITabShip_Require");
            SetDisabledReason(m_gradeUpModuleButtonText2, true, reqTier, totalCost);
        }
    }

    private void RefreshGradeDownButton(EModuleSubType currentSubType, int currentLevel, EModuleSubType prevSubType)
    {
        if (m_gradeDownModuleButtonText1 != null)
            m_gradeDownModuleButtonText1.text = LocalizationManager.Instance.Get("UITabShip_GradeDown");

        bool bFlagShip = m_selectedShip.IsFlagship();
        bool isFlagshipBodyMin = m_selectedShip != null
            && bFlagShip == true
            && m_selectedModule != null
            && m_selectedModule.GetModuleType() == EModuleType.body
            && prevSubType == EModuleSubType.none
            && currentLevel == 1;

        if (isFlagshipBodyMin == true)
        {
            m_gradeDownModuleButton.SetInteractable(false);
            if (m_gradeDownModuleButtonText2 != null) m_gradeDownModuleButtonText2.Hide();
            return;
        }

        m_gradeDownModuleButton.SetInteractable(true);

        if (m_gradeDownModuleButtonText2 == null) return;

        long refund;
        if (prevSubType != EModuleSubType.none)
        {
            // 그레이드 업 비용: 현재 서브타입 리서치 비용
            long currentResearchCost = DataManager.Instance.m_dataTableUpgradeCost.GetCost(currentSubType);

            // 현재 서브타입 Lv.1~currentLevel-1 레벨업 합계 (moduleLevel=n 비용은 Lv.n→n+1 비용)
            long currentLevelUpTotal = 0;
            for (int lv = 1; lv < currentLevel; lv++)
            {
                DataManager.Instance.GetModuleLevelUpCost(currentSubType, lv, out int lvCost);
                currentLevelUpTotal += lvCost;
            }

            // 이전 서브타입 전체 레벨업 합계 (서버 calcLevelRefund와 동일: lv < maxLevel)
            int  prevMaxLevel    = DataManager.Instance.GetMaxModuleLevel(prevSubType);
            long prevLevelUpTotal = 0;
            for (int lv = 1; lv < prevMaxLevel; lv++)
            {
                DataManager.Instance.GetModuleLevelUpCost(prevSubType, lv, out int lvCost);
                prevLevelUpTotal += lvCost;
            }

            refund = currentResearchCost + currentLevelUpTotal + prevLevelUpTotal;
            if (refund < 0) refund = 0;
        }
        else
        {
            refund = m_selectedModule.m_investedModulePoint;
        }
        m_gradeDownModuleButtonText2.SetRow("mineral_basic", refund > 0 ? $"+{CommonUtility.FormatBigNumber(refund)}" : "");
        m_gradeDownModuleButtonText2.SetImageColor(CommonUtility.PaletteColor("ModulePoint"));
    }

    private void RefreshLevelUpButton(EModuleSubType subType, int level, bool isMaxLevel, int playerSubtypeLevel, long playerPoint)
    {
        if (isMaxLevel == true)
        {
            m_levelUpModuleButton.SetInteractable(false);
            if (m_levelUpModuleButtonText1 != null)
                m_levelUpModuleButtonText1.text = LocalizationManager.Instance.Get("LevelupButtonTextMax");
            if (m_levelUpModuleButtonText2 != null)
                m_levelUpModuleButtonText2.SetRow("", "");
            return;
        }

        DataManager.Instance.GetModuleLevelUpCost(subType, level, out int levelUpCost);
        int  reqTier  = subType.GetTechTier();
        bool hasTech  = playerSubtypeLevel >= reqTier;
        bool hasPoint = playerPoint >= levelUpCost;
        bool canLevel = hasTech == true && hasPoint == true;

        m_levelUpModuleButton.SetInteractable(canLevel);

        if (canLevel == true)
        {
            if (m_levelUpModuleButtonText1 != null)
                m_levelUpModuleButtonText1.text = LocalizationManager.Instance.Get("UITabShip_LevelUp");
            if (m_levelUpModuleButtonText2 != null)
            {
                m_levelUpModuleButtonText2.SetRow("mineral_basic", $"-{CommonUtility.FormatBigNumber(levelUpCost)}");
                m_levelUpModuleButtonText2.SetImageColor(CommonUtility.PaletteColor("ModulePoint"));
            }
        }
        else
        {
            if (m_levelUpModuleButtonText1 != null)
                m_levelUpModuleButtonText1.text = LocalizationManager.Instance.Get("UITabShip_Require");
            if (m_levelUpModuleButtonText2 != null)
                SetDisabledReason(m_levelUpModuleButtonText2, hasTech, reqTier, levelUpCost);
        }
    }

    private void RefreshLevelDownButton(EModuleSubType subType, int level, EModuleSubType prevSubType)
    {
        if (m_levelDownModuleButtonText1 != null)
            m_levelDownModuleButtonText1.text = LocalizationManager.Instance.Get("UITabShip_LevelDown");

        bool bFlagShip = m_selectedShip.IsFlagship();
        bool isFlagshipBodyMin = m_selectedShip != null
            && bFlagShip == true
            && m_selectedModule != null
            && m_selectedModule.GetModuleType() == EModuleType.body
            && prevSubType == EModuleSubType.none
            && level == 1;

        if (isFlagshipBodyMin == true)
        {
            m_levelDownModuleButton.SetInteractable(false);
            if (m_levelDownModuleButtonText2 != null) m_levelDownModuleButtonText2.Hide();
            return;
        }

        m_levelDownModuleButton.SetInteractable(true);

        if (m_levelDownModuleButtonText2 == null) return;

        if (level > 1)
        {
            DataManager.Instance.GetModuleLevelUpCost(subType, level - 1, out int levelDownRefund);
            m_levelDownModuleButtonText2.SetRow("mineral_basic", $"+{CommonUtility.FormatBigNumber(levelDownRefund)}");
            m_levelDownModuleButtonText2.SetImageColor(CommonUtility.PaletteColor("ModulePoint"));
        }
        else if (prevSubType != EModuleSubType.none)
        {
            // Lv.1 레벨다운 = 서브타입 다운과 동일하지만 환급은 T→currentSubType 업그레이드 비용만
            long gradeDownRefund = DataManager.Instance.m_dataTableUpgradeCost.GetCost(subType);
            m_levelDownModuleButtonText2.SetRow("mineral_basic", $"+{CommonUtility.FormatBigNumber(gradeDownRefund)}");
            m_levelDownModuleButtonText2.SetImageColor(CommonUtility.PaletteColor("ModulePoint"));
        }
        else
        {
            long fullRefund = m_selectedModule.m_investedModulePoint;
            m_levelDownModuleButtonText2.SetRow("mineral_basic", fullRefund > 0 ? $"+{CommonUtility.FormatBigNumber(fullRefund)}" : "");
            m_levelDownModuleButtonText2.SetImageColor(CommonUtility.PaletteColor("ModulePoint"));
        }
    }

    private long CalcLevelUpToMaxCost(EModuleSubType subType, int currentLevel)
    {
        long total = 0;
        int  lv    = currentLevel;
        while (DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, lv + 1) != null)
        {
            if (DataManager.Instance.GetModuleLevelUpCost(subType, lv, out int cost))
                total += cost;
            lv++;
        }
        return total;
    }

    // 현재 레벨 → 현재 서브타입 맥스 레벨까지 mineralCost 합산
    private long CalcMineralLevelUpToMaxCost(EModuleSubType subType, int currentLevel)
    {
        long total = 0;
        int  lv    = currentLevel;
        while (true)
        {
            ModuleData nextData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, lv + 1);
            if (nextData == null) break;
            total += nextData.mineralCost;
            lv++;
        }
        return total;
    }

    private void RefreshMineralLevelUpButton(EModuleSubType subType, int level, bool isMaxLevel, long playerMineral)
    {
        if (isMaxLevel == true)
        {
            m_levelUpModuleButton.SetInteractable(false);
            if (m_levelUpModuleButtonText1 != null)
                m_levelUpModuleButtonText1.text = LocalizationManager.Instance.Get("LevelupButtonTextMax");
            if (m_levelUpModuleButtonText2 != null)
                m_levelUpModuleButtonText2.Hide();
            return;
        }

        ModuleData nextData   = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, level + 1);
        int        mineralCost = nextData != null ? nextData.mineralCost : 0;
        bool       canLevel    = playerMineral >= mineralCost;

        m_levelUpModuleButton.SetInteractable(canLevel);

        if (m_levelUpModuleButtonText1 != null)
            m_levelUpModuleButtonText1.text = LocalizationManager.Instance.Get("UITabShip_LevelUp");
        if (m_levelUpModuleButtonText2 != null)
        {
            m_levelUpModuleButtonText2.SetRow("mineral_basic", $"-{CommonUtility.FormatBigNumber(mineralCost)}");
            m_levelUpModuleButtonText2.SetImageColor(CommonUtility.PaletteColor("Mineral"));
            m_levelUpModuleButtonText2.SetTextColor(canLevel == true ? Color.white : Color.red);
        }
    }

    private void RefreshMineralLevelDownButton(EModuleSubType subType, int currentLevel, EModuleSubType prevSubType, int investedMineral)
    {
        if (m_levelDownModuleButtonText1 != null)
            m_levelDownModuleButtonText1.text = LocalizationManager.Instance.Get("UITabShip_LevelDown");

        // 미네랄 투자 없으면 내릴 것 없음. modulePointLevel 기본값 0 오염 방지
        if (investedMineral <= 0)
        {
            m_levelDownModuleButton.SetInteractable(false);
            if (m_levelDownModuleButtonText2 != null) m_levelDownModuleButtonText2.Hide();
            return;
        }

        // Lv.1은 해당 그레이드의 최저 레벨이라 내릴 레벨이 없음 → 그레이드다운과 동일하게 처리
        if (currentLevel == 1)
        {
            m_levelDownModuleButton.SetInteractable(true);
            if (m_levelDownModuleButtonText2 != null)
            {
                long refund = CalcMineralGradeDownRefund(subType, currentLevel, prevSubType, investedMineral);
                m_levelDownModuleButtonText2.SetRow("mineral_basic", refund > 0 ? $"+{CommonUtility.FormatBigNumber(refund)}" : "");
                m_levelDownModuleButtonText2.SetImageColor(CommonUtility.PaletteColor("Mineral"));
                m_levelDownModuleButtonText2.SetTextColor(Color.white);
            }
            return;
        }

        bool hasBaseline    = DataManager.Instance.CalcModulePointBaseline(
            m_selectedModule.GetModuleType(), m_selectedModule.m_investedModulePoint,
            out EModuleSubType baselineSubType, out int baselineLevel);
        bool isSameSubType  = hasBaseline == true && baselineSubType == subType;
        int  modulePointLevel = isSameSubType == true ? baselineLevel : 0;
        bool canDown          = currentLevel > modulePointLevel;

        m_levelDownModuleButton.SetInteractable(canDown);
        if (m_levelDownModuleButtonText2 != null)
        {
            if (canDown == true)
            {
                ModuleData curData     = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, currentLevel);
                int refundMineral      = curData != null ? curData.mineralCost : 0;
                m_levelDownModuleButtonText2.SetRow("mineral_basic", refundMineral > 0 ? $"+{CommonUtility.FormatBigNumber(refundMineral)}" : "");
                m_levelDownModuleButtonText2.SetImageColor(CommonUtility.PaletteColor("Mineral"));
                m_levelDownModuleButtonText2.SetTextColor(Color.white);
            }
            else
                m_levelDownModuleButtonText2.Hide();
        }
    }

    private void RefreshMineralGradeUpButton(EModuleSubType subType, int level, EModuleSubType nextSubType, long playerMineral)
    {
        if (nextSubType == EModuleSubType.none)
        {
            m_gradeUpModuleButton.SetInteractable(false);
            m_gradeUpModuleButtonText1.text = LocalizationManager.Instance.Get("UITabShip_GradeUp");
            m_gradeUpModuleButtonText2.Hide();
            return;
        }

        long levelUpToMaxCost = CalcMineralLevelUpToMaxCost(subType, level);
        long gradeUpCost      = DataManager.Instance.m_dataTableUpgradeCost.GetCost(nextSubType);
        long totalCost        = levelUpToMaxCost + gradeUpCost;

        var  commander       = DataManager.Instance.m_currentCommander;
        int  commanderLevel  = commander != null ? commander.GetCommanderLevel() : 0;
        int  playerSubtypeLevel      = DataManager.Instance.m_dataTableCommanderLevel.GetSubtypeLevel(commanderLevel);
        int  reqTier      = nextSubType.GetTechTier();
        bool hasTech      = playerSubtypeLevel >= reqTier;
        bool canUpgrade   = hasTech == true && playerMineral >= totalCost;

        m_gradeUpModuleButton.SetInteractable(canUpgrade);

        if (canUpgrade == true)
        {
            m_gradeUpModuleButtonText1.text = LocalizationManager.Instance.Get("UITabShip_GradeUp");
            m_gradeUpModuleButtonText2.SetRow("mineral_basic", $"-{CommonUtility.FormatBigNumber(totalCost)}");
            m_gradeUpModuleButtonText2.SetImageColor(CommonUtility.PaletteColor("Mineral"));
            m_gradeUpModuleButtonText2.SetTextColor(Color.white);
        }
        else if (hasTech == false)
        {
            m_gradeUpModuleButtonText1.text = LocalizationManager.Instance.Get("UITabShip_Require");
            m_gradeUpModuleButtonText2.SetRow("icon_tech", $"{reqTier}");
            m_gradeUpModuleButtonText2.SetImageColor(CommonUtility.PaletteColor("GeneralDark1"));
            m_gradeUpModuleButtonText2.SetTextColor(Color.red);
        }
        else
        {
            m_gradeUpModuleButtonText1.text = LocalizationManager.Instance.Get("UITabShip_Require");
            m_gradeUpModuleButtonText2.SetRow("mineral_basic", $"-{CommonUtility.FormatBigNumber(totalCost)}");
            m_gradeUpModuleButtonText2.SetImageColor(CommonUtility.PaletteColor("Mineral"));
            m_gradeUpModuleButtonText2.SetTextColor(Color.red);
        }
    }

    private void RefreshMineralGradeDownButton(EModuleSubType currentSubType, int currentLevel, EModuleSubType prevSubType, int investedMineral)
    {
        if (m_gradeDownModuleButtonText1 != null)
            m_gradeDownModuleButtonText1.text = LocalizationManager.Instance.Get("UITabShip_GradeDown");

        if (investedMineral <= 0)
        {
            m_gradeDownModuleButton.SetInteractable(false);
            if (m_gradeDownModuleButtonText2 != null) m_gradeDownModuleButtonText2.Hide();
            return;
        }

        m_gradeDownModuleButton.SetInteractable(true);

        if (m_gradeDownModuleButtonText2 == null) return;

        long refund = CalcMineralGradeDownRefund(currentSubType, currentLevel, prevSubType, investedMineral);

        m_gradeDownModuleButtonText2.SetRow("mineral_basic", refund > 0 ? $"+{CommonUtility.FormatBigNumber(refund)}" : "");
        m_gradeDownModuleButtonText2.SetImageColor(CommonUtility.PaletteColor("Mineral"));
        m_gradeDownModuleButtonText2.SetTextColor(Color.white);
    }

    // 그레이드다운(미네랄) 환급액 계산 — 그레이드다운 버튼, Lv1에서의 레벨다운 버튼이 공용으로 사용
    private long CalcMineralGradeDownRefund(EModuleSubType currentSubType, int currentLevel, EModuleSubType prevSubType, int investedMineral)
    {
        bool hasBaseline     = DataManager.Instance.CalcModulePointBaseline(
            m_selectedModule.GetModuleType(), m_selectedModule.m_investedModulePoint,
            out EModuleSubType baselineSubType, out int baselineLevel);
        int  currentVal      = (int)currentSubType;
        int  baselineVal     = hasBaseline == true ? (int)baselineSubType : 0;
        bool isAtBaseline    = currentVal <= baselineVal;

        if (prevSubType == EModuleSubType.none || isAtBaseline == true)
        {
            // 리셋 경로: 투자된 전체 미네랄 환급
            return investedMineral;
        }

        // 다운그레이드 경로: 현재 등급업 비용 + 현재 레벨업 비용 + 이전 등급 전체 레벨업 비용
        long gradeUpCost = DataManager.Instance.m_dataTableUpgradeCost.GetCost(currentSubType);

        long currentLevelUpTotal = 0;
        for (int lv = 1; lv < currentLevel; lv++)
        {
            var nextData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(currentSubType, lv + 1);
            if (nextData != null) currentLevelUpTotal += nextData.mineralCost;
        }

        int  prevMaxLevel     = DataManager.Instance.GetMaxModuleLevel(prevSubType);
        long prevLevelUpTotal = 0;
        for (int lv = 1; lv < prevMaxLevel; lv++)
        {
            var nextData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(prevSubType, lv + 1);
            if (nextData != null) prevLevelUpTotal += nextData.mineralCost;
        }

        // prevSubType이 baseline(모듈포인트로 도달한 등급)과 같다면, baseline 레벨까지는 미네랄 투입분이 아니므로 환급 대상에서 제외
        if (hasBaseline == true && baselineSubType == prevSubType)
        {
            long baselineLevelUpTotal = 0;
            for (int lv = 1; lv < baselineLevel; lv++)
            {
                var nextData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(prevSubType, lv + 1);
                if (nextData != null) baselineLevelUpTotal += nextData.mineralCost;
            }
            prevLevelUpTotal -= baselineLevelUpTotal;
        }

        long refund = gradeUpCost + currentLevelUpTotal + prevLevelUpTotal;
        if (refund < 0) refund = 0;
        return refund;
    }

    private void SetDisabledReason(RowImageText row, bool hasTech, int reqTier, long cost)
    {
        if (hasTech == false)
        {
            row.SetRow("icon_tech", $"{reqTier}");
            row.SetImageColor(CommonUtility.PaletteColor("GeneralDark1"));
        }
        else
        {
            row.SetRow("mineral_basic", $"-{CommonUtility.FormatBigNumber(cost)}");
            row.SetImageColor(CommonUtility.PaletteColor("ModulePoint"));
        }
        row.SetTextColor(Color.red);
    }

    







    



    

    

    

    

    private void ExecuteMineralReset()
    {
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
        m_bModuleChanging = true;
        NetworkManager.Instance.ModuleResetMineral(req, OnMineralResetResponse);
    }

    private void OnMineralResetResponse(ApiResponse<ModuleResetResponse> response)
    {
        m_bModuleChanging = false;
        if (response.errorCode == 0)
            Apply_MineralReset(response.data);
        else
            ShowErrorMessage($"Mineral reset failed: {ErrorCodeMapping.GetMessage(response.errorCode)}");
    }

    private void Apply_MineralReset(ModuleResetResponse data)
    {
        if (data == null) return;
        if (m_playerFleet == null) return;

        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander != null)
            commander.UpdateMineral(data.pointRemain);

        if (data.isShipRemoved == true)
        {
            RemoveShipFromFleet(data.removedShipId);
            return;
        }

        SpaceShip ship = m_playerFleet.FindShip(data.shipId);
        if (ship == null) return;

        if (data.isModuleRemoved == true)
        {
            ship.Apply_ResetModuleToPlaceholder(data.bodyIndex, data.moduleType, data.slotIndex);
        }
        else
        {
            // ApplyModuleChange가 모듈을 재생성하므로 기존 모듈포인트 투자값을 먼저 보존
            ModuleBase prevModule = ship.FindModule(data.bodyIndex, data.moduleType, data.slotIndex);
            int savedInvestedModulePoint = prevModule != null ? prevModule.m_investedModulePoint : 0;

            ship.ApplyModuleChange(data.bodyIndex, data.moduleType, data.moduleSubType, data.slotIndex, data.moduleNewLevel, data.investedPoint, savedInvestedModulePoint);
        }

        EventManager.Trigger_ShipStatsChanged(ship);

        if (m_selectedShip != null && m_selectedShip.m_shipInfo.id == data.shipId)
        {
            PopulateModuleSelectButtons();
            ReselectReplacedModule(ship, data.bodyIndex, data.moduleType, data.moduleSubType, data.slotIndex);
        }
    }

    

    

    private EModuleSubType GetNextSubType(EModuleSubType subType)
    {
        int nextVal = (int)subType + 100;
        return System.Enum.IsDefined(typeof(EModuleSubType), nextVal)
            ? (EModuleSubType)nextVal
            : EModuleSubType.none;
    }

    private EModuleSubType GetPrevSubType(EModuleSubType subType)
    {
        int prevVal = (int)subType - 100;
        if (prevVal <= 0) return EModuleSubType.none;
        return System.Enum.IsDefined(typeof(EModuleSubType), prevVal)
            ? (EModuleSubType)prevVal
            : EModuleSubType.none;
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
        if (m_playerFleet == null || m_playerFleet.m_fleetState.IsBattleState() == false)
            CameraController.Instance.FocusOnModuleIfHidden(module.m_moduleSlot);
        EventManager.TriggerSpaceShipModuleSelected(m_selectedShip, module);
    }

    // ─────────────────────────────────────────────
    // 모듈 리셋
    // ─────────────────────────────────────────────

    private void ExecuteResetModule()
    {
        if (m_selectedShip == null || m_selectedModule == null) return;

        // 기함이 아닌 함선의 바디 리셋 = 자원 환급 + 함선 삭제
        if (m_selectedModule.GetModuleType() == EModuleType.body && m_selectedShip.m_shipInfo.positionIndex != 0)
        {
            var removeReq = new ShipResetRemoveRequest { shipId = m_selectedShip.m_shipInfo.id };
            m_bModuleChanging = true;
            NetworkManager.Instance.ResetAndRemoveShip(removeReq, OnResetAndRemoveShipResponse);
            return;
        }

        int slotIndex = m_selectedModule.GetModuleType() == EModuleType.body
            ? 0
            : m_selectedModule.m_moduleSlot.m_moduleSlotInfo.slotIndex;

        var req = new ModuleResetRequest
        {
            shipId     = m_selectedShip.m_shipInfo.id,
            bodyIndex  = m_selectedModule.GetModuleBodyIndex(),
            moduleType = m_selectedModule.GetModuleType(),
            slotIndex  = slotIndex
        };
        m_bModuleChanging = true;
        NetworkManager.Instance.ResetModule(req, OnResetModuleResponse);
    }

    private void RemoveShipFromFleet(long removedShipId)
    {
        SpaceShip removedShip = m_playerFleet.FindShip(removedShipId);
        if (removedShip != null)
            m_playerFleet.RemoveShip(removedShip, refreshFormation: true);

        DataManager.Instance.RemoveFleetShip(removedShipId);
        EventManager.Trigger_FleetShipCountChanged();

        // 기함으로 선택 전환
        m_selectedShip   = m_playerFleet.m_ships.Count > 0 ? m_playerFleet.m_ships[0] : null;
        m_selectedModule = m_selectedShip != null ? m_selectedShip.m_moduleBodys[0] : null;

        if (m_selectedShip != null)
        {
            CameraController.Instance.SetTargetOfCameraController(m_selectedShip.transform);
            EventManager.TriggerSpaceShipModuleSelected(m_selectedShip, m_selectedModule);
        }

        UpdateShipHeader();
        PopulateModuleSelectButtons();
    }

    private void OnResetAndRemoveShipResponse(ApiResponse<ShipResetRemoveResponse> response)
    {
        m_bModuleChanging = false;
        if (response.errorCode != 0)
        {
            ShowErrorMessage($"Reset failed: {ErrorCodeMapping.GetMessage(response.errorCode)}");
            return;
        }

        var data = response.data;
        var commander = DataManager.Instance.m_currentCommander;
        if (commander != null)
            commander.UpdateModulePoint(data.modulePointRemain);

        RemoveShipFromFleet(data.removedShipId);

    }

    private void OnResetModuleResponse(ApiResponse<ModuleResetResponse> response)
    {
        m_bModuleChanging = false;
        if (response.errorCode != 0)
        {
            ShowErrorMessage($"Reset failed: {ErrorCodeMapping.GetMessage(response.errorCode)}");
            return;
        }

        var data = response.data;
        var commander = DataManager.Instance.m_currentCommander;
        if (commander != null)
            commander.UpdateModulePoint(data.pointRemain);

        SpaceFleet fleet = ObjectManager.Instance.GetMyFleet();
        if (fleet == null) return;
        SpaceShip targetShip = fleet.FindShip(data.shipId);
        if (targetShip == null) return;

        if (data.moduleType == EModuleType.body)
        {
            // T1에서 지원되지 않는 슬롯을 먼저 placeholder로 초기화
            ModuleBody body = targetShip.FindModuleBodyByIndex(data.bodyIndex);
            ModuleData t1Data = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(EModuleSubType.body_t1_m1, 1);
            if (body != null && t1Data != null)
            {
                var beamsCopy    = new List<ModuleBeam>(body.m_beams);
                var missilesCopy = new List<ModuleMissile>(body.m_missiles);
                var hangersCopy  = new List<ModuleHanger>(body.m_hangers);
                foreach (var beam in beamsCopy)
                    if (!IsSlotSupportedByT1(t1Data, EModuleType.beam, beam.GetSlotIndex()))
                        targetShip.Apply_ResetModuleToPlaceholder(data.bodyIndex, EModuleType.beam, beam.GetSlotIndex());
                foreach (var missile in missilesCopy)
                    if (!IsSlotSupportedByT1(t1Data, EModuleType.missile, missile.GetSlotIndex()))
                        targetShip.Apply_ResetModuleToPlaceholder(data.bodyIndex, EModuleType.missile, missile.GetSlotIndex());
                foreach (var hanger in hangersCopy)
                    if (!IsSlotSupportedByT1(t1Data, EModuleType.hanger, hanger.GetSlotIndex()))
                        targetShip.Apply_ResetModuleToPlaceholder(data.bodyIndex, EModuleType.hanger, hanger.GetSlotIndex());
            }
            // 기함 body 리셋 — T1 레벨1로 복귀
            targetShip.ApplyModuleChange(data.bodyIndex, EModuleType.body, EModuleSubType.body_t1_m1, 0, 1, 0, 0);
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

    private bool IsSlotSupportedByT1(ModuleData t1Data, EModuleType moduleType, int slotIndex)
    {
        if (t1Data == null || t1Data.moduleSlots == null) return false;
        foreach (var slot in t1Data.moduleSlots)
            if (slot.moduleType == moduleType && slot.slotIndex == slotIndex) return true;
        return false;
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

