using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelSpace : UIPanelBase
{
    [Header("Tab System")]
    public TabSystem m_tabSystem;

    [Header("Manual Tab Setup (Alternative)")]
    public Button closeButton;

    [HideInInspector] public SpaceFleet m_myFleet;

    // UITabShip 탭 인덱스 (자동 전환용)
    private int m_shipTabIndex = -1;

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

                // UITabShip 탭 인덱스 저장
                if (tabBase is UITabShip)
                    m_shipTabIndex = i;
            }
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(() => UIManager.Instance.ShowMainPanel());
    }

    public override void OnShowUIPanel()
    {
        CameraController.Instance.SetShipSelectionEnabled(true);
        EventManager.Subscribe_SpaceShipSelected(OnShipSelectedAutoTabSwitch);
        m_tabSystem.ForceActivateTab();
    }

    public override void OnHideUIPanel()
    {
        CameraController.Instance.SetShipSelectionEnabled(false);
        EventManager.Unsubscribe_SpaceShipSelected(OnShipSelectedAutoTabSwitch);
        m_tabSystem.ForceDeactivateTab();

        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    // 다른 탭에서 함선 클릭 시 UITabShip으로 자동 전환
    private void OnShipSelectedAutoTabSwitch(SpaceShip ship)
    {
        if (m_shipTabIndex < 0) return;
        if (m_tabSystem.GetCurrentActiveTab() == m_shipTabIndex) return;
        m_tabSystem.SwitchToTab(m_shipTabIndex);
        
    }

}

