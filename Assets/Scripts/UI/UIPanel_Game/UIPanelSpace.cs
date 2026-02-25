// 우주 공간 UI 패널 — 탭 시스템 초기화 및 모듈 선택 시 UITabShip 자동 전환
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
    private int m_moduleTabIndex = -1;

    public override void InitializeUIPanel()
    {
        InitializeUIPanelSpace();
    }

    private void InitializeUIPanelSpace()
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
                    m_moduleTabIndex = i;
            }
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(() => UIManager.Instance.ShowMainPanel());
    }

    public override void OnShowUIPanel()
    {
        CameraController.Instance.SetShipSelectionEnabled(true);
        EventManager.Subscribe_SpaceShipModuleSelected(OnModuleSelectedAutoTabSwitch);
        m_tabSystem.ForceActivateTab();
    }

    public override void OnHideUIPanel()
    {
        CameraController.Instance.SetShipSelectionEnabled(false);
        EventManager.Unsubscribe_SpaceShipModuleSelected(OnModuleSelectedAutoTabSwitch);
        m_tabSystem.ForceDeactivateTab();

        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    private void OnDestroy()
    {

    }

    // 모듈이 선택될 때만 UITabShip 로 자동 전환 (함선 클릭만으로는 전환 안 함)
    private void OnModuleSelectedAutoTabSwitch(SpaceShip ship, ModuleBase module)
    {
        if (m_moduleTabIndex < 0) return;
        if (m_tabSystem.GetCurrentActiveTab() == m_moduleTabIndex) return;
        m_tabSystem.SwitchToTab(m_moduleTabIndex);
    }

}

