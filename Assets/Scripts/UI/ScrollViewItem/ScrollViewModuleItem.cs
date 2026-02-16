using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScrollViewModuleItem : MonoBehaviour
{
    [SerializeField] private Button m_selectButton;
    [SerializeField] private TMP_Text m_selectButtonText;
    [SerializeField] private GameObject m_selectedIndicator; // 선택 표시 오브젝트 (Image, Border 등)

    public void InitializeScrollViewModuleItem(string text, UnityEngine.Events.UnityAction actionSelect)
    {
        m_selectButton.gameObject.SetActive(true);
        m_selectButton.onClick.RemoveAllListeners();
        m_selectButton.onClick.AddListener(actionSelect);
        CommonUtility.SetUILocText(m_selectButtonText, text);

        // 초기 상태: 선택 상태 숨김
        SetSelected_ScrollViewModuleItem(false);
    }

    public void SetSelected_ScrollViewModuleItem(bool selected)
    {
        // 선택 표시 오브젝트 활성화/비활성화
        if (m_selectedIndicator != null)
            m_selectedIndicator.SetActive(selected);
    }
}
