using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RowLabelButton : MonoBehaviour
{
    [SerializeField] private TMP_Text m_label;
    [SerializeField] private Button m_button;
    [SerializeField] private TMP_Text m_buttonText;

    public void SetRow(string label, UnityEngine.Events.UnityAction buttonAction, string buttonText)
    {
        SetLabel(label);
        SetButton(buttonAction, buttonText);
    }

    public void SetLabel(string label)
    {
        if (m_label != null) m_label.text = label;
    }
    
    public void SetButton(UnityEngine.Events.UnityAction buttonAction, string buttonText)
    {
        if (m_button != null)
        {
            m_button.onClick.RemoveAllListeners();
            m_button.onClick.AddListener(buttonAction);  
        } 
        if (m_buttonText != null) m_buttonText.text = buttonText;
    }
}
