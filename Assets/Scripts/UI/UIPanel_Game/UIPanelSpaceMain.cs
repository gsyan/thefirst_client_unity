using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelSpaceMain : UIPanelBase
{
    //public UIManager m_UIManager;

    [Header("UI Buttons")]
    public Button m_fleetButton;
    public Button m_explorationButton;
    public Button m_settingButton;

    void Start()
    {
        SetupButtons();
    }

    private void SetupButtons()
    {
        m_fleetButton?.onClick.AddListener(() => UIManager.Instance.ShowPanel("UIPanelFleet"));
        m_explorationButton?.onClick.AddListener(() => UIManager.Instance.ShowPanel("UIPanelExploration"));
        m_settingButton?.onClick.AddListener(() => UIManager.Instance.ShowPanel("UIPanelSetting"));
    }

    public override void OnShowUIPanel()
    {        
    }
    public override void OnHideUIPanel()
    {        
    }
    
}

