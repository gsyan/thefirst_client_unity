using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// public enum FleetManagementTab
// {
//     Admiral,
//     Formation,
//     Upgrade
// }

public class UIPanelExploration : UIPanelBase
{
    [Header("Tab System")]
    public TabSystem tabSystem;

    [Header("Manual Tab Setup (Alternative)")]
    public Button admiralTabButton;
    public Button formationTabButton;
    public Button upgradeTabButton;
    public Button closeButton;

    [Header("Tab Panels")]
    public GameObject admiralPanel;
    public GameObject formationPanel;
    public GameObject upgradePanel;

    private void Start()
    {
        if (tabSystem != null)
            SetupTabSystem();
        else
            SetupManualTabs();

        closeButton?.onClick.AddListener(() => UIManager.Instance.ShowMainPanel());
    }

    private void SetupTabSystem()
    {
        // TabSystem이 인스펙터에서 설정되지 않았다면 자동으로 생성
        if (tabSystem.tabs.Count == 0)
        {
            // 탭 데이터 자동 설정
            var tabs = new List<TabData>();

            // Admiral Tab
            if (admiralTabButton != null && admiralPanel != null)
            {
                tabs.Add(new TabData
                {
                    tabButton = admiralTabButton,
                    tabPanel = admiralPanel,
                    tabName = "Admiral",
                    activeColor = new Color(0.2f, 0.5f, 1f, 1f),
                    inactiveColor = new Color(0.8f, 0.8f, 0.8f, 1f)
                });
            }

            // Formation Tab
            if (formationTabButton != null && formationPanel != null)
            {
                tabs.Add(new TabData
                {
                    tabButton = formationTabButton,
                    tabPanel = formationPanel,
                    tabName = "Formation",
                    activeColor = new Color(0.2f, 0.5f, 1f, 1f),
                    inactiveColor = new Color(0.8f, 0.8f, 0.8f, 1f)
                });
            }

            // Upgrade Tab
            if (upgradeTabButton != null && upgradePanel != null)
            {
                tabs.Add(new TabData
                {
                    tabButton = upgradeTabButton,
                    tabPanel = upgradePanel,
                    tabName = "Upgrade",
                    activeColor = new Color(0.2f, 0.5f, 1f, 1f),
                    inactiveColor = new Color(0.8f, 0.8f, 0.8f, 1f)
                });
            }

            tabSystem.tabs = tabs;
            tabSystem.defaultActiveTab = 2; // Upgrade 탭을 기본으로 설정
        }
    }

    private void SetupManualTabs()
    {
        // 기존 수동 방식 (TabSystem을 사용하지 않는 경우)
        admiralTabButton?.onClick.AddListener(() => SwitchTab("Admiral"));
        formationTabButton?.onClick.AddListener(() => SwitchTab("Formation"));
        upgradeTabButton?.onClick.AddListener(() => SwitchTab("Upgrade"));

        // 기본적으로 Upgrade 탭 활성화
        SwitchTab("Upgrade");
    }

    public void SwitchTab(string tabName)
    {
        if (tabSystem != null)
        {
            tabSystem.SwitchToTabByName(tabName);
        }
        else
        {
            // 수동 방식 fallback
            SwitchTabManual(tabName);
        }
    }

    private void SwitchTabManual(string tabName)
    {
        // 모든 패널 비활성화
        SetPanelActive(admiralPanel, false);
        SetPanelActive(formationPanel, false);
        SetPanelActive(upgradePanel, false);

        // 선택된 패널 활성화
        switch (tabName)
        {
            case "Admiral":
                SetPanelActive(admiralPanel, true);
                UpdateButtonVisual(admiralTabButton, true);
                UpdateButtonVisual(formationTabButton, false);
                UpdateButtonVisual(upgradeTabButton, false);
                break;
            case "Formation":
                SetPanelActive(formationPanel, true);
                UpdateButtonVisual(admiralTabButton, false);
                UpdateButtonVisual(formationTabButton, true);
                UpdateButtonVisual(upgradeTabButton, false);
                break;
            case "Upgrade":
                SetPanelActive(upgradePanel, true);
                UpdateButtonVisual(admiralTabButton, false);
                UpdateButtonVisual(formationTabButton, false);
                UpdateButtonVisual(upgradeTabButton, true);
                break;
        }

        Debug.Log($"Switched to {tabName} tab");
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }

    private void UpdateButtonVisual(Button button, bool selected)
    {
        if (button == null) return;

        var colors = button.colors;
        if (selected)
        {
            colors.normalColor = new Color(0.2f, 0.5f, 1f, 1f);
        }
        else
        {
            colors.normalColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        }
        button.colors = colors;
    }

    private void OnEnable()
    {
        // Refresh fleet data when UI opens
        RefreshFleetData();
    }

    private void RefreshFleetData()
    {
        // Update UI with current fleet information from DataManager
        var currentFleet = DataManager.Instance.m_currentFleetInfo;
        if (currentFleet != null)
        {
            Debug.Log($"Fleet UI refreshed - Fleet: {currentFleet.fleetName} with {DataManager.Instance.GetShipCount()} ships");
        }
        else
        {
            Debug.LogWarning("No current fleet data available");
        }
    }
}

