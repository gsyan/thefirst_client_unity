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
    }

    private void OnCameraViewCycleClicked()
    {
        CameraController.Instance.CycleCameraFocusTarget();
        UpdateCurrentCameraViewText();
    }

    private void OnCameraViewClicked(ECameraFocusTarget cameraFocusTarget)
    {
        CameraController.Instance.SetCameraFocusTarget(cameraFocusTarget);
        UpdateCurrentCameraViewText();
    }

    private void UpdateCurrentCameraViewText()
    {
        ECameraFocusTarget current = CameraController.Instance.FocusTarget;
        m_textCameraView.text = LocalizationManager.Instance.Get(current.ToString());
    }
}
