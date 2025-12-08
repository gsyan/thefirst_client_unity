using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelSpaceMain : MonoBehaviour
{
    public UIManager m_UIManager;

    [Header("UI Buttons")]
    public Button fleetButton;
    public Button explorationButton;

    void Start()
    {
        SetupButtons();
    }

    private void SetupButtons()
    {
        fleetButton?.onClick.AddListener(() => m_UIManager?.ShowPanel("PanelFleet"));
        explorationButton?.onClick.AddListener(() => m_UIManager?.ShowPanel("PanelExploration"));
    }

    private void OnEnable()
    {
        CameraController.Instance.SwitchCameraMode(CameraControllerMode.Normal, null);
    }



}

