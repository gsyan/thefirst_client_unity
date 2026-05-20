// 카메라 포커스 전환 버튼 패널 — 존 전투 중 표시, 카메라 포커스 순환 및 속도 토글 버튼 포함
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIPanelCameraView : UIPanelBase
{
    public Button m_cameraViewCycleButton;
    [SerializeField] private ButtonGroupSystem buttonGroup;

    [Header("Game Speed")]
    [SerializeField] private Button m_speedButton;
    [SerializeField] private TextMeshProUGUI m_speedLabel;

    [Header("존 진행 정보")]
    [SerializeField] private TextMeshProUGUI m_zoneNameText;

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
    }

    void Start()
    {
        if (m_cameraViewCycleButton != null)
            m_cameraViewCycleButton.onClick.AddListener(() => OnCameraViewCycleClicked());

        // 버튼 클릭 시 카메라 포커스 변경
        if (buttonGroup != null)
        {
            buttonGroup.items[0].onSelected = () => CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_my_fleet);
            buttonGroup.items[1].onSelected = () => CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_center);
            buttonGroup.items[2].onSelected = () => CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_enemy_fleet);
        }

        buttonGroup.defaultIndex = (int)CameraController.Instance.FocusTarget;
        buttonGroup.Initialize();

        // buttonGroup 초기화 완료 후 구독 (Select 호출 안전 보장)
        EventManager.Subscribe_CameraFocusTargetChanged(OnCameraFocusTargetChanged);

        if (m_speedButton != null)
            m_speedButton.onClick.AddListener(OnSpeedButtonClicked);

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
    }

    private void OnFleetStateChanged(EUnitState state)
    {
        bool wasBattle = m_fleetState == EUnitState.Battle;
        m_fleetState = state;

        if (wasBattle == true && state != EUnitState.Battle)
            CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_my_fleet);

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
        if (m_fleetState == EUnitState.Battle && m_isExplorationOpen == false)
            UIManager.Instance.ShowPanel(panelName);
        else
            UIManager.Instance.HidePanel(panelName);
    }

    private void OnCameraViewCycleClicked()
    {
        CameraController.Instance.CycleCameraFocusTarget();
    }

    private void OnCameraFocusTargetChanged(ECameraFocusTarget target)
    {
        buttonGroup.Select((int)target);
    }

    private void OnSpeedButtonClicked()
    {
        GameSpeedController.CycleNext();
    }

    private void OnGameSpeedChanged(float speed, float pitch)
    {
        RefreshSpeedLabel(speed);
    }

    private void RefreshSpeedLabel(float speed)
    {
        if (m_speedLabel == null) return;
        // 0.5 → "x0.5", 1.0 → "x1", 1.5 → "x1.5" 형태로 표시
        m_speedLabel.text = speed == (int)speed ? $"x{(int)speed}" : $"x{speed:F1}";
    }

    public override void OnShowUIPanel()
    {
        // 패널이 뜰 때 현재 viewport 비율로 즉시 위치 동기화 (비활성 중 놓친 이벤트 보정)
        OnViewportChanged(m_lastViewportRatio);
    }

    private void OnZoneEntered(string zoneName, bool isFirstClear)
    {
        if (m_zoneNameText != null)
        {
            string label = LocalizationManager.Instance.Get("exploration_zone_list_name");
            m_zoneNameText.text = $"{label} {zoneName}";
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

        cam.RefreshCenterModeZoom();
    }
}
