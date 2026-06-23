using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScrollViewShipItemAdd : MonoBehaviour
{
    public Button m_AddButton;
    public TMP_Text m_AddButtonText;

    public void InitializeScrollViewShipItemAdd(string text, UnityEngine.Events.UnityAction action)
    {
        m_AddButton.gameObject.SetActive(true);
        m_AddButton.onClick.AddListener(() => { SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true); action?.Invoke(); });
        m_AddButtonText.text = text;
    }
    
}
