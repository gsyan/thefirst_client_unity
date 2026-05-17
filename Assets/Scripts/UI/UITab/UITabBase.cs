using UnityEngine;
using UnityEngine.UI;

public class UITabBase : MonoBehaviour
{
    // 부모 탭 시스템 참조
    [HideInInspector] public TabSystem m_tabSystemParent;

    [SerializeField] private Button m_closeButton;

    // UIPanelSpace 초기화 시 m_tabSystemParent 설정 후 호출
    public void InitializeCloseButton()
    {
        if (m_closeButton != null)
            m_closeButton.onClick.AddListener(() => m_tabSystemParent?.SwitchToTab(-1));
    }

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

    // 에러/실패 메시지 — UIPopupAlert로 표시 (확인 버튼 필요)
    protected void ShowErrorMessage(string message)
    {
        UIManager.Instance.ShowPopupAlert(new AlertPopupConfig { title = LocalizationManager.Instance.Get("error_message_title"), message = message });
    }
}
