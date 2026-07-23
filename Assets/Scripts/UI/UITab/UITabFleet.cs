#if false
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UITabFleet : UITabBase
{
    [SerializeField] private TMP_Text m_fleetSynergyStepText;

    [Header("함선 선택 UI 부모")]
    [SerializeField] private RectTransform m_shipSelectorContainer;
    private ShipSelector[] m_shipSelectors;

    [SerializeField] private Button m_addShipButton;
    [SerializeField] private TMP_Text m_currentShipCountStatText;

    [Header("함선 액션 버튼 (선택 시 활성)")]
    [SerializeField] private Button m_btnShipRepair;    // 집중 수리 (추후 구현)

    [SerializeField] private Button m_fleetManageButton;

    private SpaceFleet m_playerFleet;
    private ShipSelector m_selectedShipSelector;
    private bool m_needsLayoutRebuild = false;

    public override void InitializeUITab()
    {
        InitializeUITabFleet();
    }

    private void InitializeUITabFleet()
    {
        if (m_shipSelectorContainer != null)
            m_shipSelectors = m_shipSelectorContainer.GetComponentsInChildren<ShipSelector>(true);

        m_fleetManageButton.onClick.AddListener(OnFleetTacticsButtonClicked);

        if (m_addShipButton != null) m_addShipButton.onClick.AddListener(OnAddShipButtonClicked);
        if (m_btnShipRepair != null) m_btnShipRepair.onClick.AddListener(OnShipRepairClicked);

        // 탭 초기화 시점에 함대가 아직 스폰되지 않았을 수 있음(튜토리얼 등) — 스폰/교체 시점에 뒤늦게 바인딩
        EventManager.Subscribe_MyFleetSet(OnMyFleetSet);
        EventManager.Subscribe_FleetShipCountChanged(OnShipCountChanged);
        EventManager.Subscribe_FleetUpdateHP(OnFleetHPUpdated);
        EventManager.Subscribe_SpaceShipSelected(OnSpaceShipSelected);
        EventManager.Subscribe_CommanderLevelChanged(OnCommanderLevelChanged);
        EventManager.Subscribe_ShipStatsChanged(OnShipStatsChanged);

        // 이미 함대가 존재하면 즉시 바인딩
        if (DataManager.Instance.m_currentCommander != null && ObjectManager.Instance.GetMyFleet() != null)
            BindPlayerFleet();
    }

    // 함대 스폰/교체(튜토리얼→실제 함대 전환 포함) 시 호출 — 매번 탭 열 때 체크하지 않아도 되도록 이벤트로 처리
    private void OnMyFleetSet()
    {
        BindPlayerFleet();
    }

    private void BindPlayerFleet()
    {
        m_playerFleet = ObjectManager.Instance.GetMyFleet();
        if (m_playerFleet == null) return;

        PopulateShipSelectorGrid();
        UpdateShipActionButtons();
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        HideTabButtons();
        UpdateCommanderLevelDisplay();

        if (m_needsLayoutRebuild == true)
        {
            m_needsLayoutRebuild = false;
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_shipSelectorContainer);
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        }

        RefreshShipHealthDisplay();

        // 선택된 함선이 없으면 살아있는 첫 함선 자동 선택
        if (m_selectedShipSelector == null && m_playerFleet != null && m_playerFleet.m_ships.Count > 0)
        {
            SpaceShip firstShip = m_playerFleet.GetFirstAliveShip();
            if (firstShip != null)
                OnShipSelectorClicked(firstShip);
            return;
        }

        // Fleet 탭 진입 시 선택된 함선 아웃라인 활성화
        if (m_selectedShipSelector != null && m_selectedShipSelector.Ship != null)
            m_selectedShipSelector.Ship.m_shipOutline.enabled = true;
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
        RefreshTabButtons();

        // Fleet 탭 벗어날 때 모든 함선 아웃라인 비활성화
        if (m_playerFleet == null) return;
        for (int i = 0; i < m_playerFleet.m_ships.Count; i++)
        {
            if (m_playerFleet.m_ships[i] == null) continue;
            m_playerFleet.m_ships[i].m_shipOutline.enabled = false;
        }
    }

    // ── Commander Level ────────────────────────────────────────────────────

    private void UpdateCommanderLevelDisplay()
    {
        var commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        int currentLevel = commander.GetCommanderLevel();

        int maxShips = DataManager.Instance.m_dataTableCommander.GetShipCount(currentLevel);
    }

    private void OnCommanderLevelChanged(int commanderLevel)
    {
        UpdateCommanderLevelDisplay();
    }

    // ── Fleet Stats ────────────────────────────────────────────────────

    // ── Formation ──────────────────────────────────────────────────────

    private void OnFleetTacticsButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_tabSystemParent == null) return;
        m_tabSystemParent.SwitchToTabByName("tab_fleettactics");
    }

    // ── ShipSelector 그리드 ────────────────────────────────────────────

    private void PopulateShipSelectorGrid()
    {
        if (m_shipSelectors == null || m_playerFleet == null) return;

        SpaceShip prevShip = m_selectedShipSelector != null ? m_selectedShipSelector.Ship : null;

        // 이전 선택 아웃라인 정리
        if (prevShip != null)
            prevShip.m_shipOutline.enabled = false;

        m_selectedShipSelector = null;

        int shipCount  = m_playerFleet.m_ships.Count;
        int maxInCsv   = DataManager.Instance.m_dataTableCommander.GetMaxShipCount();
        bool canAdd    = shipCount < maxInCsv;

        for (int i = 0; i < m_shipSelectors.Length; i++)
        {
            if (m_shipSelectors[i] == null) continue;

            if (i < shipCount)
            {
                m_shipSelectors[i].gameObject.SetActive(true);
                SpaceShip captured = m_playerFleet.m_ships[i];
                m_shipSelectors[i].InitializeShipSelector(captured, () => OnShipSelectorClicked(captured), OnShipManageClicked);

                // 이전 선택 함선 복원
                if (captured == prevShip)
                {
                    m_selectedShipSelector = m_shipSelectors[i];
                    m_selectedShipSelector.SetSelected(true);
                    if (gameObject.activeInHierarchy == true)
                        prevShip.m_shipOutline.enabled = true;
                }
            }
            else
            {
                m_shipSelectors[i].gameObject.SetActive(false);
            }
        }

        if (m_addShipButton != null) m_addShipButton.gameObject.SetActive(canAdd);
        UpdateShipCountDisplay();

        if (gameObject.activeInHierarchy == true)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_shipSelectorContainer);
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        }
        else
        {
            m_needsLayoutRebuild = true;
        }
    }

    private void UpdateShipCountDisplay()
    {
        if (m_currentShipCountStatText == null || m_playerFleet == null) return;
        int current = m_playerFleet.m_ships.Count;
        int max = DataManager.Instance.m_dataTableCommander.GetMaxShipCount();
        m_currentShipCountStatText.text = $"{current} / {max}";

        UpdateFleetSynergyDisplay();
    }

    private void UpdateFleetSynergyDisplay()
    {
        if (m_fleetSynergyStepText == null || m_playerFleet == null) return;

        float multiplier = m_playerFleet.GetShipCountAttackMultiplier();
        string multiplierStr = multiplier.ToString("F2");
        m_fleetSynergyStepText.text = LocalizationManager.Instance.Get("UITabFleet_FleetSynergyMultiply", (object)multiplierStr);

        RectTransform parent = m_fleetSynergyStepText.transform.parent as RectTransform;
        if (parent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
    }

    private void RefreshShipHealthDisplay()
    {
        if (m_shipSelectors == null) return;

        for (int i = 0; i < m_shipSelectors.Length; i++)
        {
            if (m_shipSelectors[i] == null || m_shipSelectors[i].gameObject.activeSelf == false) continue;

            if (m_shipSelectors[i].Ship == null)
            {
                // 전투 중 파괴된 함선 — 파괴 상태 표시
                m_shipSelectors[i].SetDestroyedState();
                if (m_selectedShipSelector == m_shipSelectors[i])
                    SelectAliveShipFallback();
            }
            else
            {
                m_shipSelectors[i].RefreshHealth();
            }
        }
    }

    // 살아있는 함선으로 선택 변경 (기함 우선, 인덱스 순)
    private void SelectAliveShipFallback()
    {
        if (m_selectedShipSelector != null)
        {
            m_selectedShipSelector.SetSelected(false);
            m_selectedShipSelector = null;
        }

        for (int i = 0; i < m_shipSelectors.Length; i++)
        {
            if (m_shipSelectors[i] == null || m_shipSelectors[i].gameObject.activeSelf == false) continue;
            SpaceShip ship = m_shipSelectors[i].Ship;
            if (ship != null && ship.IsAlive() == true)
            {
                OnShipSelectorClicked(ship);
                return;
            }
        }

        UpdateShipActionButtons();
    }

    private void UpdateShipActionButtons()
    {
        bool hasSelection = m_selectedShipSelector != null;
        if (m_btnShipRepair != null) m_btnShipRepair.interactable = hasSelection;
    }

    private void OnShipManageClicked()
    {
        if (m_selectedShipSelector == null || m_selectedShipSelector.Ship == null) return;
        m_tabSystemParent.SwitchToTabByName("tab_ship");
    }

    private void OnShipRepairClicked()
    {
        if (m_playerFleet == null) return;
        if (m_playerFleet.GetMissingHealth() <= 0f) return;

        var loc          = LocalizationManager.Instance;
        var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;
        int cost         = gameSettings.repairBoostMineralPerSec * gameSettings.instantRepairBaseSecs;

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title        = loc.Get("fleet_instant_repair_title"),
            message      = loc.Get("fleet_instant_repair_message"),
            cost         = new CostStruct(ECostType.Mineral, cost),
            confirmText1 = loc.Get("fleet_instant_repair_confirm"),
            onConfirm    = ExecuteFleetInstantRepair,
            onCancel     = () => { }
        });
    }

    private void ExecuteFleetInstantRepair()
    {
        NetworkManager.Instance.FleetInstantRepair(response =>
        {
            if (response.errorCode == 0)
            {
                DataManager.Instance.m_currentCommander.UpdateMineral(response.data.mineralRemain);
                m_playerFleet.FullRepair();
            }
            else
            {
                ShowErrorMessage(ErrorCodeMapping.GetMessage(response.errorCode));
            }
        });
    }

    private void OnShipSelectorClicked(SpaceShip ship)
    {
        if (m_selectedShipSelector != null)
        {
            m_selectedShipSelector.SetSelected(false);
            if (m_selectedShipSelector.Ship != null)
                m_selectedShipSelector.Ship.m_shipOutline.enabled = false;
        }

        for (int i = 0; i < m_shipSelectors.Length; i++)
        {
            if (m_shipSelectors[i] != null && m_shipSelectors[i].Ship == ship)
            {
                m_selectedShipSelector = m_shipSelectors[i];
                m_selectedShipSelector.SetSelected(true);
                if (m_selectedShipSelector.Ship != null)
                    m_selectedShipSelector.Ship.m_shipOutline.enabled = true;
                break;
            }
        }

        UpdateShipActionButtons();

        // 카메라 타겟 지정 (파괴된 함선은 이벤트 전달 생략)
        if (ship != null)
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

    private void OnShipCountChanged()
    {
        PopulateShipSelectorGrid();
    }

    private void OnFleetHPUpdated()
    {
        RefreshShipHealthDisplay();
    }

    private void OnShipStatsChanged(SpaceShip ship)
    {
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
        bool isTutorial = TutorialActionGate.IsTutorial("Tutorial_FirstPlay_ManageShip");
        if (isTutorial == false && DataManager.Instance.m_currentCommander == null) return;

        var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;

        // 튜토리얼 중(지크프리트 함대)에도 요구사항 UI는 그대로 보여줌 — GrantTutorialCommanderLevel이 임시로 레벨을 충족시켜둠
        int currentShipCount = m_playerFleet.m_ships.Count;
        int requiredCommanderLevel = DataManager.Instance.m_dataTableCommander.GetRequiredCommanderLevel(currentShipCount + 1);
        RequireStruct require = new RequireStruct(requiredCommanderLevel);

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title     = LocalizationManager.Instance.Get("UIPopupMessage_AddShipTitle"),
            message   = LocalizationManager.Instance.Get("UIPopupMessage_AddShipMessage"),
            require   = require,
            cost      = new CostStruct(ECostType.ModulePoint, gameSettings.addShipCost),
            onConfirm = ExecuteAddShip,
            onCancel  = () => { }
        });
    }

    // 튜토리얼 전용 — 서버 호출 없이 클라이언트에서만 T1 함선을 추가 (지크프리트 함대는 서버 기록 대상 아님, 비용은 ExecuteAddShip에서 TutorialActionGate로 차감)
    private void ExecuteAddShipTutorialOnly()
    {
        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet == null) return;

        int newPositionIndex = myFleet.m_ships.Count;
        ShipInfo newShipInfo = TutorialCinematicController.BuildCinematicShipInfo(1, newPositionIndex);

        SoundManager.Instance.PlayFX(EFx.Add_Ship, retrigger: true);
        SpaceShip newShip = myFleet.CreateSpaceShipByInfo(newShipInfo, bWarp: true);
        EventManager.Trigger_FleetShipCountChanged();

        // 대형 자리에 도착할 때까지 대기하는 튜토리얼 스텝(ShipArrivedAtFormation)이 참조할 수 있도록 등록
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.SetPendingNewShip(newShip);
    }

    private void ExecuteAddShip()
    {
        // 튜토리얼 진행 중(지크프리트 함대)에는 서버 요청 없이 튜토리얼용 로컬 모듈포인트에서만 차감
        if (TutorialActionGate.IsTutorial("Tutorial_FirstPlay_ManageShip"))
        {
            int addShipCost = DataManager.Instance.m_dataTableConfig.gameSettings.addShipCost;
            if (TutorialActionGate.TryConsumeModulePoint(addShipCost) == false) return;

            ExecuteAddShipTutorialOnly();
            return;
        }

        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        ServerErrorCode errorCode = CanAddShip();
        if (errorCode != ServerErrorCode.SUCCESS)
        {
            ShowErrorMessage($"{errorCode}");
            return;
        }

        var request = new AddShipRequest { fleetId = null };

        NetworkManager.Instance.AddShip(request, (response) =>
        {
            if (response.errorCode == 0)
            {
                SoundManager.Instance.PlayFX(EFx.Add_Ship, retrigger: true);
                commander.UpdateModulePoint(response.data.modulePointRemain);

                if (response.data.newShipInfo != null)
                {
                    DataManager.Instance.AddFleetShip(response.data.newShipInfo);
                    SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
                    if (myFleet != null)
                        myFleet.CreateSpaceShipById(response.data.newShipInfo.id, bWarp: true);
                }

                EventManager.Trigger_FleetShipCountChanged();
            }
        });
    }

    private ServerErrorCode CanAddShip()
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return ServerErrorCode.CLIENT_CanAddShip_COMMANDER_NOT_FOUND;

        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet == null) return ServerErrorCode.FLEET_NOT_FOUND;

        var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;
        int currentShipCount = myFleet.m_ships.Count;
        int maxInCsv = DataManager.Instance.m_dataTableCommander.GetMaxShipCount();
        if (currentShipCount >= maxInCsv) return ServerErrorCode.CLIENT_CanAddShip_FLEET_MAX_SHIPS_REACHED;

        int commanderLevel = commander.GetCommanderLevel();
        int maxShipsAtTech = DataManager.Instance.m_dataTableCommander.GetShipCount(commanderLevel);
        if (currentShipCount >= maxShipsAtTech) return ServerErrorCode.CLIENT_CanAddShip_INSUFFICIENT_COMMANDER_LEVEL;
        if (commander.m_commanderInfo.modulePoint < gameSettings.addShipCost) return ServerErrorCode.ADD_SHIP_FAIL_INSUFFICIENT_MODULE_POINT;

        return ServerErrorCode.SUCCESS;
    }
}
#endif

