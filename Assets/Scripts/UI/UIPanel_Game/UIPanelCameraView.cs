// 카메라 포커스 전환 버튼 패널 — 존 전투 중 표시, 카메라 포커스 순환 및 속도 토글 버튼 포함
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIPanelCameraView : UIPanelBase
{
    [SerializeField] private Button m_retreatButton;
    [SerializeField] private ButtonGroupSystem buttonGroup;

    [Header("Game Speed")]
    [SerializeField] private UIButtonHasChildren m_speedButton;
    [SerializeField] private TextMeshProUGUI m_speedLabel;

    [Header("존 진행 정보")]
    [SerializeField] private TextMeshProUGUI m_zoneNameText;

    [Header("함대 전술 토글")]
    [SerializeField] private Transform m_tacticsButtonContainer;
    private UIButtonHasChildren[] m_tacticsButtons;

    [SerializeField] private UIButtonHasChildren m_tacticsFormationButton;
    [SerializeField] private Image m_tacticsFormationImage;

    // 플로팅 미네랄 소비 텍스트 풀
    private readonly List<UIFloatingCostText> m_floatingPool = new List<UIFloatingCostText>();
    private RectTransform m_floatingContainer;
    private const int k_floatingPoolSize = 6;

    private RectTransform m_rectTransform;
    private float m_lastViewportRatio = 0f; // 패널 비활성 중 놓친 이벤트 대비

    private EUnitState m_fleetState = EUnitState.Idle;
    private bool m_isExplorationOpen = false;

    void Awake()
    {
        m_rectTransform = GetComponent<RectTransform>();
        // start 에서 이벤트 등록 하면, 인스턴스 활성화 될때까지 이벤트 받지 못함. 그래서 여기로 이동
        EventManager.Subscribe_CameraViewportChanged(OnViewportChanged);
        EventManager.Subscribe_GameSpeedChanged(OnGameSpeedChanged);
        EventManager.Subscribe_ZoneEntered(OnZoneEntered);
        EventManager.Subscribe_MyFleetStateChanged(OnFleetStateChanged);
        EventManager.Subscribe_ExplorationTabOpened(OnExplorationTabOpened);
        EventManager.Subscribe_ExplorationTabClosed(OnExplorationTabClosed);
        EventManager.Subscribe_TacticOptionsChanged(OnTacticOptionsChanged);
        EventManager.Subscribe_FleetShipCountChanged(OnFleetShipCountChanged);
        EventManager.Subscribe_FormationChanged(OnFormationChanged);
        EventManager.Subscribe_TacticMineralConsumed(OnTacticMineralConsumed);

        SetupTacticsButtons();
        SetupFormationButton();
        InitFloatingPool();
    }

    void Start()
    {
        // 버튼 클릭 시 카메라 포커스 변경
        if (buttonGroup != null)
        {
            buttonGroup.items[0].onSelected = () => CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_my_fleet);
            buttonGroup.items[1].onSelected = () => CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_center);
            // 적 함대 버튼은 이미 선택된 상태에서도 순환이 필요하므로 onSelected 대신 onClick 직접 사용
            // ButtonGroupSystem은 같은 인덱스 재클릭 시 onSelected를 호출하지 않으므로 우회
            Button enemyFleetBtn = buttonGroup.items[2].button;
            if (enemyFleetBtn != null)
                enemyFleetBtn.onClick.AddListener(() => CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_enemy_fleet));
        }

        buttonGroup.defaultIndex = (int)CameraController.Instance.FocusTarget;
        buttonGroup.Initialize();

        // buttonGroup 초기화 완료 후 구독 (Select 호출 안전 보장)
        EventManager.Subscribe_CameraFocusTargetChanged(OnCameraFocusTargetChanged);

        // 버튼 콜백은 OnFleetStateChanged에서 전투 종류에 따라 동적으로 세팅

        if (m_speedButton != null)
        {
            m_speedButton.GetButton().onClick.AddListener(OnSpeedButtonClicked);
            m_speedButton.SetActiveColorKey("Vip");
            m_speedButton.SetInactiveColorKey("VipDark");
        }

        RefreshSpeedLabel(GameSpeedController.CurrentSpeed);
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe_CameraFocusTargetChanged(OnCameraFocusTargetChanged);
        EventManager.Unsubscribe_CameraViewportChanged(OnViewportChanged);
        EventManager.Unsubscribe_GameSpeedChanged(OnGameSpeedChanged);
        EventManager.Unsubscribe_ZoneEntered(OnZoneEntered);
        EventManager.Unsubscribe_MyFleetStateChanged(OnFleetStateChanged);
        EventManager.Unsubscribe_ExplorationTabOpened(OnExplorationTabOpened);
        EventManager.Unsubscribe_ExplorationTabClosed(OnExplorationTabClosed);
        EventManager.Unsubscribe_TacticOptionsChanged(OnTacticOptionsChanged);
        EventManager.Unsubscribe_FleetShipCountChanged(OnFleetShipCountChanged);
        EventManager.Unsubscribe_FormationChanged(OnFormationChanged);
        EventManager.Unsubscribe_TacticMineralConsumed(OnTacticMineralConsumed);
    }

    private void InitFloatingPool()
    {
        GameObject containerGo = new GameObject("FloatingCostTextPool");
        m_floatingContainer = containerGo.AddComponent<RectTransform>();
        m_floatingContainer.SetParent(transform.parent, false);
        m_floatingContainer.anchorMin = Vector2.zero;
        m_floatingContainer.anchorMax = Vector2.one;
        m_floatingContainer.offsetMin = Vector2.zero;
        m_floatingContainer.offsetMax = Vector2.zero;

        for (int i = 0; i < k_floatingPoolSize; i++)
        {
            UIFloatingCostText item = CreateFloatingText();
            item.gameObject.SetActive(false);
            m_floatingPool.Add(item);
        }
    }

    private UIFloatingCostText CreateFloatingText()
    {
        GameObject go = new GameObject("FloatingCostText");
        RectTransform rt = go.AddComponent<RectTransform>();
        UIFloatingCostText floatText = go.AddComponent<UIFloatingCostText>();
        go.transform.SetParent(m_floatingContainer, false);
        rt.sizeDelta = new Vector2(80f, 30f);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        return floatText;
    }

    private UIFloatingCostText GetFromPool()
    {
        for (int i = 0; i < m_floatingPool.Count; i++)
        {
            if (m_floatingPool[i].gameObject.activeInHierarchy == false)
                return m_floatingPool[i];
        }
        UIFloatingCostText newItem = CreateFloatingText();
        m_floatingPool.Add(newItem);
        return newItem;
    }

    private void OnTacticMineralConsumed(int tacticIdx, int cost)
    {
        if (m_tacticsButtons == null || tacticIdx >= m_tacticsButtons.Length) return;

        RectTransform btnRect = m_tacticsButtons[tacticIdx].GetComponent<RectTransform>();
        if (btnRect == null) return;

        // 버튼 월드 중심 → FloatingCostTextPool 로컬 좌표 변환
        Vector3 worldCenter = btnRect.TransformPoint(btnRect.rect.center);
        Vector2 localPos    = m_floatingContainer.InverseTransformPoint(worldCenter);
        Vector2 startPos    = localPos + new Vector2(0f, btnRect.rect.height * 0.5f + 5f);

        UIFloatingCostText floatText = GetFromPool();
        floatText.Play($"-{cost}", startPos, ReturnToPool);
    }

    private void ReturnToPool(UIFloatingCostText item)
    {
        item.gameObject.SetActive(false);
    }

    private void SetupTacticsButtons()
    {
        if (m_tacticsButtonContainer == null) return;

        m_tacticsButtons = m_tacticsButtonContainer.GetComponentsInChildren<UIButtonHasChildren>();
        for (int i = 0; i < m_tacticsButtons.Length; i++)
        {
            int idx = i;
            m_tacticsButtons[idx].GetButton().onClick.AddListener(() => { SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true); EventManager.Trigger_TacticToggleRequested(idx); });
        }
    }

    private void OnTacticOptionsChanged(int options)
    {
        if (m_tacticsButtons == null) return;

        Color colorOn  = CommonUtility.PaletteColor("Mineral");
        Color colorOff = CommonUtility.PaletteColor("MineralDark");

        for (int i = 0; i < m_tacticsButtons.Length; i++)
            m_tacticsButtons[i].SetColor((options & (1 << i)) != 0 ? colorOn : colorOff);
    }

    private void OnFleetStateChanged(EUnitState state)
    {
        bool wasBattle = m_fleetState.IsBattleState();
        m_fleetState = state;

        if (wasBattle == true && state.IsBattleState() == false)
            CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_my_fleet);

        if (m_retreatButton != null)
        {
            m_retreatButton.onClick.RemoveAllListeners();

            // Tutorial_Exploration 중 첫 실제 전투는 후퇴 기능을 막고 끝까지 진행시킴
            bool isTutorialExploration = TutorialActionGate.IsTutorial("Tutorial_Exploration");
            m_retreatButton.interactable = isTutorialExploration == false;

            if (isTutorialExploration == false && state == EUnitState.BattleExploration)
                m_retreatButton.onClick.AddListener(() => { SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true); EventManager.TriggerRetreatExploration(); });
            else if (state == EUnitState.BattlePvp)
                m_retreatButton.onClick.AddListener(() => { SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true); EventManager.TriggerRetreatPvp(); });
        }

        RefreshVisibility();
    }

    private void OnExplorationTabOpened()
    {
        m_isExplorationOpen = true;
        RefreshVisibility();
    }

    private void OnExplorationTabClosed()
    {
        m_isExplorationOpen = false;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        // Tutorial_FirstPlay_Battle 연출 중에는 이 패널(존 정보/전술 토글 등)을 노출하지 않음
        bool isTutorialBattle = TutorialActionGate.IsTutorial("Tutorial_FirstPlay_Battle");

        if (m_fleetState.IsBattleState() == true && m_isExplorationOpen == false && isTutorialBattle == false)
            UIManager.Instance.ShowPanel(panelName);
        else
            UIManager.Instance.HidePanel(panelName);
    }

    private void OnCameraFocusTargetChanged(ECameraFocusTarget target)
    {
        buttonGroup.Select((int)target);
    }

    private void OnSpeedButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        GameSpeedController.CycleNext();
    }

    private void OnGameSpeedChanged(float speed, float pitch)
    {
        RefreshSpeedLabel(speed);
    }

    private void RefreshSpeedLabel(float speed)
    {
        if (m_speedLabel == null) return;

        bool isVip = IAPManager.Instance.IsVipActive();
#if UNITY_EDITOR
        //isVip = true;
#endif
        if (m_speedButton != null)
            m_speedButton.SetInteractable(isVip);
        m_speedLabel.text = isVip == true
            ? (speed == (int)speed ? $"x{(int)speed}" : $"x{speed:F1}")
            : LocalizationManager.Instance.Get("UICOMMON_AdmiralFeature");
    }

    public override void OnShowUIPanel()
    {
        // 패널이 뜰 때 현재 viewport 비율로 즉시 위치 동기화 (비활성 중 놓친 이벤트 보정) - 없어도 작동 하나 혹시 나중을 위해 남겨둠
        //OnViewportChanged(m_lastViewportRatio);

        // 이벤트를 놓쳤을 수 있으므로 현재 전술 옵션 상태로 즉시 동기화
        SpaceFleet fleet = ObjectManager.Instance.GetMyFleet();
        if (fleet != null)
            OnTacticOptionsChanged(fleet.m_fleetInfo.tacticOptions);

        RefreshFormationButton();
    }

    private void OnZoneEntered(string zoneName, bool isFirstClear)
    {
        if (m_zoneNameText != null)
        {
            string label = LocalizationManager.Instance.Get("exploration_zone_list_name");
            m_zoneNameText.text = $"{label} {zoneName}";
            m_zoneNameText.color = CommonUtility.PaletteColor("Text.Dark1");
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_zoneNameText.transform.parent as RectTransform);
        }
    }

    // 현재 카메라 viewport 너비 중앙에 버튼 배치
    private void OnViewportChanged(float ratio)
    {
        m_lastViewportRatio = ratio;
        if (m_rectTransform == null) return;

        var cam = CameraController.Instance;
        if (cam == null) return;

        float centerX = cam.GetViewportWidth() / 2f;
        Vector2 min = m_rectTransform.anchorMin;
        Vector2 max = m_rectTransform.anchorMax;
        min.x = centerX;
        max.x = centerX;
        m_rectTransform.anchorMin = min;
        m_rectTransform.anchorMax = max;
    }

    private void SetupFormationButton()
    {
        if (m_tacticsFormationButton == null) return;
        m_tacticsFormationButton.GetButton().onClick.AddListener(OnFormationButtonClicked);
        m_tacticsFormationButton.SetActiveColorKey("Action.Primary");
        RefreshFormationButton();
    }

    private void OnFormationButtonClicked()
    {
        SpaceFleet fleet = ObjectManager.Instance.GetMyFleet();
        if (fleet == null) return;
        fleet.CycleFormation();
    }

    private void OnFleetShipCountChanged()
    {
        RefreshFormationButtonInteractable();
    }

    private void OnFormationChanged(EFormationType formation)
    {
        RefreshFormationIcon(formation);
    }

    private void RefreshFormationButton()
    {
        SpaceFleet fleet = ObjectManager.Instance.GetMyFleet();
        RefreshFormationButtonInteractable();
        if (fleet != null)
            RefreshFormationIcon(fleet.m_currentFormationType);
    }

    private void RefreshFormationButtonInteractable()
    {
        if (m_tacticsFormationButton == null) return;
        SpaceFleet fleet = ObjectManager.Instance.GetMyFleet();
        int shipCount = fleet != null ? fleet.GetAliveShipCount() : 0;
        m_tacticsFormationButton.SetInteractable(shipCount >= 3);
    }

    private void RefreshFormationIcon(EFormationType formation)
    {
        if (m_tacticsFormationImage == null) return;
        FormationPreset preset = FormationPresetDB.Get(formation);
        if (preset == null || preset.formationIcon == null) return;
        m_tacticsFormationImage.sprite = preset.formationIcon;
    }
}
