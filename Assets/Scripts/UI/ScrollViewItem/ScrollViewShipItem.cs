using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScrollViewShipItem : MonoBehaviour
{
    public Button m_selectButton;
    public TMP_Text m_selectButtonText;
    
    public void InitializeScrollViewShipItem(string text, UnityEngine.Events.UnityAction actionSelect)
    {       
        m_selectButton.gameObject.SetActive(true);
        m_selectButton.onClick.RemoveAllListeners();
        m_selectButton.onClick.AddListener(actionSelect);
        m_selectButtonText.text = text;
    }
}
