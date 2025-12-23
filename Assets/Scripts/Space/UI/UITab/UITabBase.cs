using UnityEngine;

public class UITabBase : MonoBehaviour
{
    // 부모 탭 시스템 참조
    [HideInInspector] public TabSystem m_tabSystemParent;

    virtual public void InitializeUITab()
    {
        
    }

    virtual public void OnTabActivated()
    {
        
    }

    virtual public void OnTabDeactivated()
    {
        
    }

}

