using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScrollViewModuleItem : MonoBehaviour
{
    [SerializeField] private Button m_selectButton;
    [SerializeField] private TMP_Text m_selectButtonText;
    [SerializeField] private Button m_developmentButton;    
    [SerializeField] private GameObject m_selectedIndicator; // 선택 표시 오브젝트 (Image, Border 등)
    
    public void InitializeScrollViewModuleItem(string text, UnityEngine.Events.UnityAction actionSelect, UnityEngine.Events.UnityAction actionDev)
    {       
        m_selectButton.gameObject.SetActive(true);
        m_selectButton.onClick.RemoveAllListeners();
        m_selectButton.onClick.AddListener(actionSelect);
        m_selectButton.onClick.AddListener(() => SetSelected_ScrollViewModuleItem(true));
        m_selectButtonText.text = text;

        m_developmentButton.onClick.AddListener(actionDev);

        // 초기 상태: 선택 상태 숨김
        SetSelected_ScrollViewModuleItem(false);
    }

    public void SetSelected_ScrollViewModuleItem(bool selected)
    {
        // 선택 표시 오브젝트 활성화/비활성화
        if (m_selectedIndicator != null)
            m_selectedIndicator.SetActive(selected);
    }

    public void SetDevelopmentButtonEnabled(bool isResearched)
    {
        m_developmentButton.gameObject.SetActive(!isResearched);
    }
}
