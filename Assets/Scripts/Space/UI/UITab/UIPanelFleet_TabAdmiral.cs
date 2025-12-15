using UnityEngine;

public class UIPanelFleet_TabAdmiral : UITabBase
{
    private SpaceFleet m_myFleet;
    
    public override void InitializeUITab()
    {
        InitializeUIPanelFleetAdmiral();
    }
    private void InitializeUIPanelFleetAdmiral()
    {
        if (m_myFleet == null)
        {
            UIPanelFleet panelFleet = GetComponentInParent<UIPanelFleet>();
            if (panelFleet != null)
                m_myFleet = panelFleet.m_myFleet;
        }
    }

    public override void OnTabActivated()
    {
        //base.OnShowUIPanel();
        InitializeUI();
        
    }

    public override void OnTabDeactivated()
    {
        //base.OnHideUIPanel();
        InitializeUI();

        
    }

    private void InitializeUI()
    {
    }

    
}
