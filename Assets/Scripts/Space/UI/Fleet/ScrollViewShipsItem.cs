using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScrollViewShipsItem : MonoBehaviour
{
    public Button m_selectButton;
    public TMP_Text m_selectButtonText;
    public Button m_AddButton;
    public TMP_Text m_AddButtonText;
   
    public void InitializeScrollViewShipsItem_SelectButton(string text, UnityEngine.Events.UnityAction action)
    {
        m_AddButton.gameObject.SetActive(false);
        m_AddButton.onClick.RemoveAllListeners();

        m_selectButton.gameObject.SetActive(true);
        m_selectButton.onClick.AddListener(action);
        m_selectButtonText.text = text;
    }

    public void InitializeScrollViewShipsItem_AddButton(string text, UnityEngine.Events.UnityAction action)
    {
        m_selectButton.gameObject.SetActive(false);
        m_selectButton.onClick.RemoveAllListeners();
        
        m_AddButton.gameObject.SetActive(true);
        m_AddButton.onClick.AddListener(action);
        m_AddButtonText.text = text;
    }
    
}
