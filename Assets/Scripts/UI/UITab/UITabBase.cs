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

    // 에러/실패 메시지 — UIPopupAlert로 표시 (확인 버튼 필요)
    protected void ShowErrorMessage(string message)
    {
        UIManager.Instance.ShowAlertPopup(LocalizationManager.Instance.Get("error"), message, null);
    }
}
