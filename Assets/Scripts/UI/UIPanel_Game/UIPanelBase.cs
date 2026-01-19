using UnityEngine;

public class UIPanelBase : MonoBehaviour
{
    public string panelName;
    public bool bMainPanel = false;
    public bool bHideCurWhenActive = false; // 이 패널이 활성화될 때 현재 패널을 숨길지 여부
    public bool bCameraMove = false;

    virtual public void InitializeUIPanel()
    {
        
    }

    virtual public void OnShowUIPanel()
    {
        
    }

    virtual public void OnHideUIPanel()
    {
        
    }

}

