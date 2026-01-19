using UnityEngine;
using UnityEngine.UI;

public class UIPanelFirst : UIPanelBase
{
    public override void InitializeUIPanel()
    {
        
    }


    public override void OnShowUIPanel()
    {
        CameraController.Instance.ResetCameraViewport();
        // CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
        // // 카메라를 화면 위쪽 절반만 사용하도록 설정
        // CameraController.Instance.SetCameraViewportToUpperHalf();
    }

    public override void OnHideUIPanel()
    {
        // CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
        // // 카메라를 전체 화면 사용으로 복구
        // CameraController.Instance.ResetCameraViewport();
    }
}
