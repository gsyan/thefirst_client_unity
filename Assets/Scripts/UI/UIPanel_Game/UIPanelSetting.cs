using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelSetting : UIPanelBase
{
    [HideInInspector] public SpaceFleet m_myFleet;
    
    [SerializeField] private Button m_closeButton;
    [SerializeField] private Button m_logoutButton;

    public override void InitializeUIPanel()
    {
        InitializeUIPanelSetting();
    }

    private void InitializeUIPanelSetting()
    {
        if (m_myFleet == null)
            m_myFleet = DataManager.Instance.m_currentCharacter.GetOwnedFleet();

        if (m_closeButton != null)
            m_closeButton.onClick.AddListener(() => UIManager.Instance.ShowMainPanel());
        if (m_logoutButton != null)
            m_logoutButton.onClick.AddListener(OnLogoutButtonClicked);
    }

    public override void OnShowUIPanel()
    {
        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
        // 카메라를 화면 위쪽 절반만 사용하도록 설정
        CameraController.Instance.SetCameraViewportToUpperHalf();
    }

    public override void OnHideUIPanel()
    {
        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
        // 카메라를 전체 화면 사용으로 복구
        CameraController.Instance.ResetCameraViewport();
    }

    private void OnLogoutButtonClicked()
    {
        UIManager.Instance.ShowConfirmPopup(
            "Logout",
            "Are you sure you want to logout?",
            new CostStruct(),
            onConfirm: () =>
            {
                NetworkManager.Instance.Logout();
                LoadingManager.LoadSceneWithLoading("MainScene");
            },
            onCancel: null
        );
    }

}

