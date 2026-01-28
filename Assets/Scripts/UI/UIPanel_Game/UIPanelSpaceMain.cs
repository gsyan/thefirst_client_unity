using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelSpaceMain : UIPanelBase
{
    //public UIManager m_UIManager;

    [Header("UI Buttons")]
    [SerializeField] private Button m_fleetButton;
    [SerializeField] private Button m_explorationButton;
    [SerializeField] private Button m_settingButton;

    [SerializeField] private Button m_testMineralButton;

    void Start()
    {
        SetupButtons();
    }

    private void SetupButtons()
    {
        m_fleetButton?.onClick.AddListener(() => UIManager.Instance.ShowPanel("UIPanelFleet"));
        m_explorationButton?.onClick.AddListener(() => UIManager.Instance.ShowPanel("UIPanelExploration"));
        m_settingButton?.onClick.AddListener(() => UIManager.Instance.ShowPanel("UIPanelSetting"));

        // TODO: 개발 완료 시 삭제 예정
        m_testMineralButton?.onClick.AddListener(() => DeveloperConsole.ExecuteCommandStatic("addmineral 1000000"));
    }

    public override void OnShowUIPanel()
    {        
    }
    public override void OnHideUIPanel()
    {        
    }
    
}

