using UnityEngine;

public class UIPanelFleetAdmiral : MonoBehaviour
{
    private SpaceFleet m_myFleet;

    void Awake()
    {
        
    }

    public void InitializeUIPanelFleetAdmiral()
    {
        if (m_myFleet == null)
        {
            UIPanelFleet panelFleet = GetComponentInParent<UIPanelFleet>();
            if (panelFleet != null)
                m_myFleet = panelFleet.m_myFleet;
        }
    }

    public void OnTabActivated()
    {
        InitializeUI();

        //CameraController.Instance.SwitchCameraMode(CameraControllerMode.DisplayFleet);
    }

    public void OnTabDeactivated()
    {
    }

    private void InitializeUI()
    {
    }

    
}
