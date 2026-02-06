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
    }

    public override void OnHideUIPanel()
    {
        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
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

