using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelCameraView : UIPanelBase
{
    public Button m_cameraViewCycleButton;
    [SerializeField] private ButtonGroupSystem buttonGroup;

    void Start()
    {
        if( m_cameraViewCycleButton != null)
            m_cameraViewCycleButton.onClick.AddListener(() => OnCameraViewCycleClicked());

        // 버튼 클릭 시 카메라 포커스 변경
        if (buttonGroup != null)
            buttonGroup.items[0].onSelected = () => CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_my_fleet);
            buttonGroup.items[1].onSelected = () => CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_center);
            buttonGroup.items[2].onSelected = () => CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_enemy_fleet);

        buttonGroup.defaultIndex = (int)CameraController.Instance.FocusTarget;
        buttonGroup.Initialize();
        
        EventManager.Subscribe_CameraFocusTargetChanged(OnCameraFocusTargetChanged);
    }

    void OnDestroy()
    {
        EventManager.Unsubscribe_CameraFocusTargetChanged(OnCameraFocusTargetChanged);
    }

    private void OnCameraViewCycleClicked()
    {
        CameraController.Instance.CycleCameraFocusTarget();
    }

    // 외부에서 카메라 포커스가 변경되었을 때 버튼 상태 동기화
    private void OnCameraFocusTargetChanged(ECameraFocusTarget target)
    {
        buttonGroup.Select((int)target);
    }
}
