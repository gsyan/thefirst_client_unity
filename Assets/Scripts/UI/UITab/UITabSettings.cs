using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UITabSettings : UITabBase
{
    [SerializeField] private Button m_logoutButton;
    [SerializeField] private Button m_testMineralButton;

    private SpaceFleet m_myFleet;

    public override void InitializeUITab()
    {
        InitializeUITabSettings();
    }

    private void InitializeUITabSettings()
    {
        if (m_myFleet == null)
            m_myFleet = DataManager.Instance.m_currentCharacter.GetOwnedFleet();

        if (m_logoutButton != null)
            m_logoutButton.onClick.AddListener(OnLogoutButtonClicked);

        if (m_testMineralButton != null)
            m_testMineralButton?.onClick.AddListener(() => DeveloperConsole.ExecuteCommandStatic("addmineral 1000000"));
    }

    public override void OnTabActivated()
    {
        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    public override void OnTabDeactivated()
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

