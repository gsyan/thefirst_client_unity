using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScrollViewShipItem : MonoBehaviour
{
    public Button m_selectButton;
    public TMP_Text m_selectButtonText;
    public Button m_manageButton;
    
    public void InitializeScrollViewShipItem(string text, UnityEngine.Events.UnityAction actionSelect, UnityEngine.Events.UnityAction actionManage)
    {       
        m_selectButton.gameObject.SetActive(true);
        m_selectButton.onClick.RemoveAllListeners();
        m_selectButton.onClick.AddListener(actionSelect);
        m_selectButton.onClick.AddListener(() => OnSelectButtonClicked());
        m_selectButtonText.text = text;

        m_manageButton.onClick.RemoveAllListeners();
        m_manageButton.onClick.AddListener(actionManage);

        // 초기 상태: 관리 버튼 숨김
        SetSelected_ScrollViewShipItem(false);
    }

    private void OnSelectButtonClicked()
    {
        SetSelected_ScrollViewShipItem(true);
    }

    public void SetSelected_ScrollViewShipItem(bool selected)
    {
        m_manageButton.gameObject.SetActive(selected);
    }
}
