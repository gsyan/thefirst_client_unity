using UnityEngine;

public class UIPanelBase : MonoBehaviour
{
    public string panelName;
    public bool bMainPanel = false; // 패널 스택의 base(항상 상주, 절대 pop되지 않는 배경 패널)인지 여부 — UIManager 전체에 딱 하나만 존재해야 함
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

