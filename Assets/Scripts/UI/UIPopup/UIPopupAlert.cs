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
    [SerializeField] private Button okButton;
    [SerializeField] private  TMP_Text confirmButtonText;
    [SerializeField] private Button okButtonBackground;

    private Action onConfirmCallback;

    protected override void Awake()
    {
        base.Awake();
        if (okButton != null)
            okButton.onClick.AddListener(OnConfirmClicked);
        if (okButtonBackground != null)
            okButtonBackground.onClick.AddListener(OnConfirmClicked);
    }

    public void ShowPopupAlert(string title, string message, Action onConfirm, string buttonText = null)
    {
        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;
        if (confirmButtonText != null) confirmButtonText.text = buttonText ?? LocalizationManager.Instance.Get("ok");

        onConfirmCallback = onConfirm;
        base.ShowPopup();
    }

    private void OnConfirmClicked()
    {
        onConfirmCallback?.Invoke();
    }
}
