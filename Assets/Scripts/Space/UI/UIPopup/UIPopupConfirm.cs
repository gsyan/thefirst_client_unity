using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupConfirm : UIPopupBase
{
    [Header("Confirm Popup UI")]
    public TMP_Text titleText;
    public TMP_Text messageText;
    public TMP_Text techText;
    public TMP_Text mineralText;
    public TMP_Text mineralRareText;
    public TMP_Text mineralExoticText;
    public TMP_Text mineralDarkText;
    public Button confirmButton;
    public Button cancelButton;

    private Action onCancelCallback;
    private Action onConfirmCallback;
    
    protected override void Awake()
    {
        base.Awake();
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    public void ShowPopupConfirm(string title, string message, CostStruct cost, Action onConfirm, Action onCancel = null)
    {
        // Set text
        if (titleText != null) titleText.text = title;

        if (messageText != null) messageText.text = message;

        // Display mineral costs
        if (cost.techLevel > 0)
        {
            techText.gameObject.SetActive(true);
            techText.text = $"Tech: {cost.techLevel:N0}";
        }
        else
            techText.gameObject.SetActive(false);
    

        if (cost.mineral > 0)
        {
            mineralText.gameObject.SetActive(true);
            mineralText.text = $"Mineral: {cost.mineral:N0}";
        }
        else
            mineralText.gameObject.SetActive(false);

        if (cost.mineralRare > 0)
        {
            mineralRareText.gameObject.SetActive(true);
            mineralRareText.text = $"Rare: {cost.mineralRare:N0}";
        }
        else
            mineralRareText.gameObject.SetActive(false);

        if (cost.mineralExotic > 0)
        {
            mineralExoticText.gameObject.SetActive(true);
            mineralExoticText.text = $"Exotic: {cost.mineralExotic:N0}";
        }
        else
            mineralExoticText.gameObject.SetActive(false);

        if (cost.mineralDark > 0)
        {
            mineralDarkText.gameObject.SetActive(true);
            mineralDarkText.text = $"Dark: {cost.mineralDark:N0}";
        }
        else
            mineralDarkText.gameObject.SetActive(false);

        // Set callbacks
        onCancelCallback = onCancel;
        onConfirmCallback = onConfirm;

        base.ShowPopup();
    }

    private void OnConfirmClicked()
    {
        onConfirmCallback?.Invoke();
    }

    private void OnCancelClicked()
    {
        onCancelCallback?.Invoke();
    }

    private void OnDestroy()
    {
        if (cancelButton != null) cancelButton.onClick.RemoveAllListeners();
        if (confirmButton != null) confirmButton.onClick.RemoveAllListeners();
    }
}
