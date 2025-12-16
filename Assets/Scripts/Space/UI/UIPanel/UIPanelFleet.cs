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
    

    override public void InitializeUIPanel()
    {
        InitializeUIPanelFleet();
    }

    private void InitializeUIPanelFleet()
    {
        if (m_myFleet == null)
            m_myFleet = DataManager.Instance.m_currentCharacter.GetOwnedFleet();

        // TabSystem의 각 탭에 연결된 Panel GameObject에서 UITabBase 컴포넌트 자동 매칭
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

        //CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
        // 카메라를 조정하여 함대/함선이 화면 세로 1/4 지점에 위치하도록 함
        // 0.25 = 위에서 1/4 지점 (화면을 4등분했을 때 1과 2 사이)
        CameraController.Instance.ApplyVerticalScreenOffset(0.25f);
    }

    public override void OnHideUIPanel()
    {
        m_tabSystem.ForceDeactivateTab();

        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
        // 카메라를 원래 위치로 복구 (부드럽게 이동)
        CameraController.Instance.ResetVerticalScreenOffset();
    }

}

