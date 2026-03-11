// 카메라 포커스 전환 버튼 패널 — 존 전투 중 표시, 카메라 포커스 순환 및 버튼그룹 동기화
using UnityEngine;
using UnityEngine.UI;

public class UIPanelCameraView : UIPanelBase
{
    public Button m_cameraViewCycleButton;
    [SerializeField] private ButtonGroupSystem buttonGroup;

    [Header("Viewport 연동 앵커 (0~1 범위)")]
    [SerializeField] private float m_anchorXA = 0.5f;    // UI 닫힘 앵커 중심 X
    [SerializeField] private float m_anchorXB = 0.25f;   // UI 열림 앵커 중심 X

    private RectTransform m_rectTransform;
    private float m_lastViewportRatio = 0f; // 패널 비활성 중 놓친 이벤트 대비

    void Awake()
    {
        m_rectTransform = GetComponent<RectTransform>();
        // start 에서 이벤트 등록 하면, 인스턴스 활성화 될때까지 이벤트 받지 못함. 그래서 여기로 이동
        EventManager.Subscribe_CameraViewportChanged(OnViewportChanged);
    }

    void Start()
    {
        if (m_cameraViewCycleButton != null)
            m_cameraViewCycleButton.onClick.AddListener(() => OnCameraViewCycleClicked());

        // 버튼 클릭 시 카메라 포커스 변경
        if (buttonGroup != null)
            buttonGroup.items[0].onSelected = () => CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_my_fleet);
            buttonGroup.items[1].onSelected = () => CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_center);
            buttonGroup.items[2].onSelected = () => CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_enemy_fleet);

        buttonGroup.defaultIndex = (int)CameraController.Instance.FocusTarget;
        buttonGroup.Initialize();

        // buttonGroup 초기화 완료 후 구독 (Select 호출 안전 보장)
        EventManager.Subscribe_CameraFocusTargetChanged(OnCameraFocusTargetChanged);
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe_CameraFocusTargetChanged(OnCameraFocusTargetChanged);
        EventManager.Unsubscribe_CameraViewportChanged(OnViewportChanged);
    }

    private void OnCameraViewCycleClicked()
    {
        CameraController.Instance.CycleCameraFocusTarget();
    }

    private void OnCameraFocusTargetChanged(ECameraFocusTarget target)
    {
        buttonGroup.Select((int)target);
    }

    public override void OnShowUIPanel()
    {
        // 패널이 뜰 때 현재 viewport 비율로 즉시 위치 동기화 (비활성 중 놓친 이벤트 보정)
        OnViewportChanged(m_lastViewportRatio);
    }

    // viewport ratio (0=전체화면, 1=UI열림) — 앵커 중심 X만 이동, 크기 유지
    private void OnViewportChanged(float ratio)
    {
        m_lastViewportRatio = ratio;
        if (m_rectTransform == null) return;
        float centerX = Mathf.Lerp(m_anchorXA, m_anchorXB, ratio);
        Vector2 min = m_rectTransform.anchorMin;
        Vector2 max = m_rectTransform.anchorMax;
        min.x = centerX;
        max.x = centerX;
        m_rectTransform.anchorMin = min;
        m_rectTransform.anchorMax = max;
    }
}
