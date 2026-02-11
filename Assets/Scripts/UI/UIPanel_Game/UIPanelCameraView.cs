using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelCameraView : UIPanelBase
{
    [SerializeField] private TMP_Text m_textCameraView;
    public Button m_cameraViewCycleButton;
    public Button m_cameraViewEnemyButton;
    public Button m_cameraViewCenterButton;
    public Button m_cameraViewMyFleetButton;
    
    void Start()
    {
        if( m_cameraViewCycleButton != null)
            m_cameraViewCycleButton.onClick.AddListener(() => OnCameraViewCycleClicked());
        if( m_cameraViewEnemyButton != null)
            m_cameraViewEnemyButton.onClick.AddListener(() => OnCameraViewClicked(ECameraFocusTarget.camera_focus_enemy_fleet));
        if( m_cameraViewCenterButton != null)
            m_cameraViewCenterButton.onClick.AddListener(() => OnCameraViewClicked(ECameraFocusTarget.camera_focus_center));
        if( m_cameraViewMyFleetButton != null)
            m_cameraViewMyFleetButton.onClick.AddListener(() => OnCameraViewClicked(ECameraFocusTarget.camera_focus_my_fleet));

        LocalizationManager.Instance.OnLanguageChanged += UpdateCurrentCameraViewText;
        UpdateCurrentCameraViewText();

        EventManager.Subscribe_CameraFocusTargetChanged(OnCameraFocusTargetChanged);
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe_CameraFocusTargetChanged(OnCameraFocusTargetChanged);
    }

    private void OnCameraFocusTargetChanged(ECameraFocusTarget target)
    {
        UpdateCurrentCameraViewText();
    }

    private void OnCameraViewCycleClicked()
    {
        CameraController.Instance.CycleCameraFocusTarget();
        UpdateCurrentCameraViewText();
    }

    private void OnCameraViewClicked(ECameraFocusTarget cameraFocusTarget)
    {
        CameraController.Instance.SetCameraFocusTarget(cameraFocusTarget);
        // SetCameraFocusTarget 내부에서 trigger 발동으로 OnCameraFocusTargetChanged이 호출되게 됨, 그래서 안불러도 됨
        //UpdateCurrentCameraViewText();
    }

    private void UpdateCurrentCameraViewText()
    {
        ECameraFocusTarget current = CameraController.Instance.FocusTarget;
        m_textCameraView.text = LocalizationManager.Instance.Get(current.ToString());
    }
}
