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

    // includeSelf=true 이면 자신의 탭 버튼도 포함해 숨김/표시
    protected void SetOtherTabsVisible(bool visible, bool includeSelf = false)
    {
        if (m_tabSystemParent == null) return;
        for (int i = 0; i < m_tabSystemParent.tabs.Count; i++)
        {
            var tab = m_tabSystemParent.tabs[i];
            if (tab.tabButton == null) continue;
            if (includeSelf == false && tab.tabPanel == gameObject) continue;
            tab.tabButton.gameObject.SetActive(visible);
        }
    }

    protected void ShowErrorMessage(string message)
    {
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig { title = LocalizationManager.Instance.Get("error_message_title"), message = message, autoCloseSec = 5f });
    }
}
