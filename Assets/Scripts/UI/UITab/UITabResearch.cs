using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UITabResearch : UITabBase
{
    private SpaceFleet m_myFleet;
    private Character m_myCharacter;
    
    
    public override void InitializeUITab()
    {
        InitializeUITabResearch();
    }

    private void InitializeUITabResearch()
    {
        m_myCharacter = DataManager.Instance.m_currentCharacter;
        if (m_myCharacter == null || m_myCharacter.GetOwnedFleet() == null) return;
        m_myFleet = m_myCharacter.GetOwnedFleet();

        
    }

    public override void OnTabActivated()
    {
        
    }

    public override void OnTabDeactivated()
    {
        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    
}

