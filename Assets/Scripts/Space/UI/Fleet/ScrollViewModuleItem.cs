using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScrollViewModuleItem : MonoBehaviour
{
    [SerializeField] private Button m_selectButton;
    [SerializeField] private TMP_Text m_selectButtonText;
    [SerializeField] private Button m_developmentButton;
    
    public void InitializeScrollViewModuleItem(string text, UnityEngine.Events.UnityAction actionSelect, UnityEngine.Events.UnityAction actionManage)
    {       
        m_selectButton.gameObject.SetActive(true);
        m_selectButton.onClick.RemoveAllListeners();
        m_selectButton.onClick.AddListener(actionSelect);
        m_selectButton.onClick.AddListener(() => OnSelectButtonClicked());
        m_selectButtonText.text = text;

        m_developmentButton.onClick.AddListener(actionManage);

        // 초기 상태: 관리 버튼 숨김
        SetSelected_ScrollViewModuleItem(false);
    }

    private void OnSelectButtonClicked()
    {
        SetSelected_ScrollViewModuleItem(true);
    }

    public void SetSelected_ScrollViewModuleItem(bool selected)
    {
        if (m_selectButton == null) return;

        ColorBlock colors = m_selectButton.colors;
        if (selected)
        {
            // 선택된 상태: 노란색 계열로 표시
            colors.normalColor = new Color(1f, 0.9f, 0.5f);
            colors.highlightedColor = new Color(1f, 0.95f, 0.7f);
        }
        else
        {
            // 선택 안 된 상태: 기본 색상
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.95f, 0.95f, 0.95f);
        }
        m_selectButton.colors = colors;
    }

    public void SetDevelopmentButtonEnabled(bool isResearched)
    {
        // 개발되지 않은 모듈은 Dev 버튼 활성화
        // 개발된 모듈은 Dev 버튼 비활성화
        m_developmentButton.interactable = !isResearched;
    }
}
