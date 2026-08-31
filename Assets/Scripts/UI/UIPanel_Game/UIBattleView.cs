// 카메라 포커스 전환 버튼 UI — UIPanelBattle의 자식 컴포넌트. 카메라 포커스 순환 및 속도 토글 버튼 포함
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIBattleView : MonoBehaviour
{
    [SerializeField] private Button m_retreatButton;
    [SerializeField] private ButtonGroupSystem buttonGroup;

    [Header("Game Speed")]
    [SerializeField] private Button m_speedButton;
    [SerializeField] private TextMeshProUGUI m_speedLabel;

    [Header("존 진행 정보")]
    [SerializeField] private TextMeshProUGUI m_zoneNameText;

    [Header("함대 전술 토글")]
    [SerializeField] private Transform m_tacticsButtonContainer;
    [SerializeField] private Image m_tacticPowerGauge; // 전술 토글 3종이 공유하는 소모 게이지(Filled/Horizontal) — 소모 계산은 UIPanelBattle이 전담, 여기선 표시만
    private Button[] m_tacticsButtons;
    private GameObject[] m_tacticsUsingImages; // 버튼 자식의 "사용중" 표시 아이콘 — on/off를 색상 대신 이 오브젝트 활성화로 표현

    private EUnitState m_fleetState = EUnitState.Idle;

    void Awake()
    {
        // start 에서 이벤트 등록 하면, 인스턴스 활성화 될때까지 이벤트 받지 못함. 그래서 여기로 이동
        EventManager.Subscribe_GameSpeedChanged(OnGameSpeedChanged);
        EventManager.Subscribe_ZoneEntered(OnZoneEntered);
        EventManager.Subscribe_MyFleetStateChanged(OnFleetStateChanged);
        EventManager.Subscribe_TacticOptionsChanged(OnTacticOptionsChanged);
        EventManager.Subscribe_TacticPowerChanged(OnTacticPowerChanged);

        SetupTacticsButtons();
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
            m_speedButton.onClick.AddListener(OnSpeedButtonClicked);

        RefreshSpeedLabel(GameSpeedController.CurrentSpeed);
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe_CameraFocusTargetChanged(OnCameraFocusTargetChanged);
        EventManager.Unsubscribe_GameSpeedChanged(OnGameSpeedChanged);
        EventManager.Unsubscribe_ZoneEntered(OnZoneEntered);
        EventManager.Unsubscribe_MyFleetStateChanged(OnFleetStateChanged);
        EventManager.Unsubscribe_TacticOptionsChanged(OnTacticOptionsChanged);
        EventManager.Unsubscribe_TacticPowerChanged(OnTacticPowerChanged);
    }

    private void OnTacticPowerChanged(float current, int max)
    {
        if (m_tacticPowerGauge == null) return;
        m_tacticPowerGauge.fillAmount = max > 0 ? Mathf.Clamp01(current / max) : 0f;
    }

    private void SetupTacticsButtons()
    {
        if (m_tacticsButtonContainer == null) return;

        m_tacticsButtons = m_tacticsButtonContainer.GetComponentsInChildren<Button>();
        m_tacticsUsingImages = new GameObject[m_tacticsButtons.Length];
        for (int i = 0; i < m_tacticsButtons.Length; i++)
        {
            int idx = i;
            m_tacticsButtons[idx].onClick.AddListener(() => { SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true); EventManager.Trigger_TacticToggleRequested(idx); });

            Transform usingImage = m_tacticsButtons[idx].transform.Find("UsingImage");
            m_tacticsUsingImages[idx] = usingImage != null ? usingImage.gameObject : null;
        }
    }

    private void OnTacticOptionsChanged(int options)
    {
        if (m_tacticsUsingImages == null) return;

        for (int i = 0; i < m_tacticsUsingImages.Length; i++)
        {
            if (m_tacticsUsingImages[i] == null) continue;
            m_tacticsUsingImages[i].SetActive((options & (1 << i)) != 0);
        }
    }

    // UIPanelBattle.OnShowUIPanel()이 호출 — OnTacticOptionsChanged는 옵션이 실제로 바뀔 때만 발행되는 이벤트라,
    // 패널이 처음 뜰 때는 아무도 쏘지 않아 UsingImage가 프리팹 저장값(전부 활성) 그대로 보이는 문제가 있었음 —
    // 패널이 뜰 때마다 현재 함대 상태로 직접 동기화
    public void RefreshTacticsDisplay()
    {
        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet != null)
            OnTacticOptionsChanged(myFleet.m_fleetInfo.tacticOptions);
    }

    private void OnFleetStateChanged(EUnitState state)
    {
        bool wasBattle = m_fleetState.IsBattleState();
        m_fleetState = state;

        if (wasBattle == true && state.IsBattleState() == false)
            CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_my_fleet);

        // 전투 진입 순간엔 함선 클릭이 막혀(CameraController.HandleModuleSelection) 카메라가 더 이상 특정 함선을 따라갈 수 없으므로,
        // 직전에 보고 있던 대상에 그대로 고정되지 않도록 기함(0번 함선)으로 강제 전환
        if (wasBattle == false && state.IsBattleState() == true)
        {
            SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
            SpaceShip flagship = myFleet != null ? myFleet.GetFlagship() : null;
            if (flagship != null)
                CameraController.Instance.SetTargetOfCameraController(flagship.transform);
        }

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
            m_speedButton.interactable = isVip;
        m_speedLabel.text = isVip == true
            ? (speed == (int)speed ? $"x{(int)speed}" : $"x{CommonUtility.FloorToDecimals(speed, 1):F1}")
            : LocalizationManager.Instance.Get("UICOMMON_AdmiralFeature");
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
}
