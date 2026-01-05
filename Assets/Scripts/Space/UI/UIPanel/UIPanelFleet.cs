using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelFleet : UIPanelBase
{
    [Header("Tab System")]
    public TabSystem m_tabSystem;

    [Header("Manual Tab Setup (Alternative)")]
    public Button closeButton;

    [HideInInspector] public SpaceFleet m_myFleet;
    

    public override void InitializeUIPanel()
    {
        InitializeUIPanelFleet();
    }

    private void InitializeUIPanelFleet()
    {
        if (m_myFleet == null)
            m_myFleet = DataManager.Instance.m_currentCharacter.GetOwnedFleet();

        // TabSystem의 각 탭
        for (int i = 0; i < m_tabSystem.tabs.Count; i++)
        {
            var tabData = m_tabSystem.tabs[i];
            if (tabData.tabPanel != null)
            {
                UITabBase tabBase = tabData.tabPanel.GetComponent<UITabBase>();
                if (tabBase == null) continue;
                tabBase.InitializeUITab();
                tabData.onActivate = tabBase.OnTabActivated;
                tabData.onDeactivate = tabBase.OnTabDeactivated;
            }
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(() => UIManager.Instance.ShowMainPanel());
    }

    public override void OnShowUIPanel()
    {
        m_tabSystem.ForceActivateTab();

        // 카메라를 화면 위쪽 절반만 사용하도록 설정
        CameraController.Instance.SetCameraViewportToUpperHalf();
    }

    public override void OnHideUIPanel()
    {
        m_tabSystem.ForceDeactivateTab();

        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
        // 카메라를 전체 화면 사용으로 복구
        CameraController.Instance.ResetCameraViewport();
    }

}

