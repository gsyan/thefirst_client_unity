using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelSpaceMain : UIPanelBase
{
    //public UIManager m_UIManager;

    [Header("UI Buttons")]
    public Button fleetButton;
    public Button explorationButton;

    void Start()
    {
        SetupButtons();
    }

    private void SetupButtons()
    {
        fleetButton?.onClick.AddListener(() => UIManager.Instance.ShowPanel("UIPanelFleet"));
        explorationButton?.onClick.AddListener(() => UIManager.Instance.ShowPanel("UIPanelExploration"));
    }

    public override void OnShowUIPanel()
    {        
    }
    public override void OnHideUIPanel()
    {        
    }
    
}

