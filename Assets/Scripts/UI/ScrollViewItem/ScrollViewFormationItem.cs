using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Components;
using UnityEngine.Localization;

public class ScrollViewFormationItem : MonoBehaviour
{
    [SerializeField] private Button m_selectButton;     // 입장
    [SerializeField] private TMP_Text m_text;
    
    public void InitializeScrollViewFormationItem(UnityEngine.Events.UnityAction actionSelect, string formationName)
    {
        m_selectButton.onClick.RemoveAllListeners();
        m_selectButton.onClick.AddListener(actionSelect);

        CommonUtility.SetUILabelText(m_text, formationName);
    }
}
