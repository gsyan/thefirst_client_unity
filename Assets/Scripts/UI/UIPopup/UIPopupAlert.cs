using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 확인 버튼만 있는 단순 알림 팝업
public class UIPopupAlert : UIPopupBase
{
    [Header("Alert Popup UI")]
    [SerializeField] private  TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private  TMP_Text confirmButtonText;
    [SerializeField] private Button confirmButtonBackground;

    private Action onConfirmCallback;

    protected override void Awake()
    {
        base.Awake();
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
        if (confirmButtonBackground != null)
            confirmButtonBackground.onClick.AddListener(OnConfirmClicked);
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
}
