using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Data;

public class UIPanelFleet_TabFormation : UITabBase
{
    public TextMeshProUGUI m_formationNameText;
    
    private SpaceFleet m_myFleet;
    private EFormationType m_currentFormationType;
    private Button m_prevButton;
    private Button m_nextButton;

    public override void InitializeUITab()
    {
        InitializeUIPanelFleetFormation();
    }
    private void InitializeUIPanelFleetFormation()
    {
        if (m_myFleet == null)
        {
            UIPanelFleet panelFleet = GetComponentInParent<UIPanelFleet>();
            if (panelFleet != null)
                m_myFleet = panelFleet.m_myFleet;
        }

        if (m_prevButton != null)
            m_prevButton.onClick.AddListener(() => ChangeFormation(-1));
        if (m_nextButton != null)
            m_nextButton.onClick.AddListener(() => ChangeFormation(1));
    }

    public override void OnTabActivated()
    {
        InitializeUI();
        
        EventManager.Subscribe_FleetChange(OnFleetChanged);
    }

    public override void OnTabDeactivated()
    {
        InitializeUI();
        
        EventManager.Unsubscribe_FleetChange(OnFleetChanged);
    }

    private void InitializeUI()
    {
        var fleet = DataManager.Instance.m_currentCharacter?.GetOwnedFleet();
        if (fleet != null)
            m_currentFormationType = fleet.m_currentFormationType;
        else
            m_currentFormationType = EFormationType.LinearHorizontal;

        UpdateFormationDisplay();
    }



    private void ChangeFormation(int direction)
    {
        var fleet = DataManager.Instance.m_currentCharacter?.GetOwnedFleet();
        if (fleet == null) return;

        int formationCount = System.Enum.GetValues(typeof(EFormationType)).Length;
        int currentIndex = (int)m_currentFormationType;
        currentIndex = (currentIndex + direction + formationCount) % formationCount;
        EFormationType newFormationType = (EFormationType)currentIndex;

        var request = new ChangeFormationRequest
        {
            fleetId = fleet.m_fleetInfo.id,
            formationType = newFormationType
        };

        NetworkManager.Instance.ChangeFormation(request, OnChangeFormationResponse);
    }

    private void OnChangeFormationResponse(ApiResponse<ChangeFormationResponse> response)
    {
        if (response.errorCode == 0 && response.data.success)
        {
            DataManager.Instance.m_currentFleetInfo = response.data.updatedFleetInfo;

            // if (System.Enum.TryParse<EFormationType>(response.data.updatedFleetInfo.formation, true, out var newFormationType))
            // {
                
            // }
            m_currentFormationType = response.data.updatedFleetInfo.formation;
            m_myFleet.UpdateShipFormation(m_currentFormationType, true);
            UpdateFormationDisplay();
        }
    }

    private void UpdateFormationDisplay()
    {
        if (m_formationNameText != null)
            m_formationNameText.text = m_currentFormationType.ToString();
    }

    private void OnFleetChanged()
    {
        UpdateFormationDisplay();
    }
}