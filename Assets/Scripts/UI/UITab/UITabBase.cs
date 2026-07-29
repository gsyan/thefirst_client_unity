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

    protected void HideTabButtons()
    {
        if (m_tabSystemParent == null) return;
        foreach (var tab in m_tabSystemParent.tabs)
        {
            if (tab.tabButton == null) continue;
            tab.tabButton.gameObject.SetActive(false);
        }
    }

    // fleet 상태에 따라 탭 버튼 가시성 복원
    protected void RefreshTabButtons()
    {
        if (m_tabSystemParent != null)
            m_tabSystemParent.RefreshTabButtonsByFleetState();
    }

    protected void ShowErrorMessage(string message)
    {
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig { message = message, autoCloseSec = 5f });
    }
}
