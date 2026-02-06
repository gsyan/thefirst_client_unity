using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 확인 버튼만 있는 단순 알림 팝업
public class UIPopupAlert : UIPopupBase
{
    [Header("Alert Popup UI")]
    public TMP_Text titleText;
    public TMP_Text messageText;
    public Button confirmButton;
    public TMP_Text confirmButtonText;

    private Action onConfirmCallback;

    protected override void Awake()
    {
        base.Awake();
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    public void ShowPopupAlert(string title, string message, Action onConfirm, string buttonText = "확인")
    {
        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;
        if (confirmButtonText != null) confirmButtonText.text = buttonText;

        onConfirmCallback = onConfirm;
        base.ShowPopup();
    }

    private void OnConfirmClicked()
    {
        onConfirmCallback?.Invoke();
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveAllListeners();
    }
}
