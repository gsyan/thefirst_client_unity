using System;
using System.Collections;
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
    [SerializeField] private Button okButtonBackground;

    private Action onConfirmCallback;
    private Coroutine m_autoCloseCoroutine;
    
    protected override void Awake()
    {
        base.Awake();
        if (okButton != null)
            okButton.onClick.AddListener(OnConfirmClicked);
        if (okButtonBackground != null)
            okButtonBackground.onClick.AddListener(OnConfirmClicked);
    }

    public void ShowPopupAlert(string title, string message, Action onConfirm, float autoCloseSec = 0f)
    {
        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;

        onConfirmCallback = onConfirm;

        if (m_autoCloseCoroutine != null)
            StopCoroutine(m_autoCloseCoroutine);
        m_autoCloseCoroutine = null;

        base.ShowPopup();

        if (autoCloseSec > 0f)
            m_autoCloseCoroutine = StartCoroutine(AutoCloseRoutine(autoCloseSec));
    }

    private IEnumerator AutoCloseRoutine(float seconds)
    {
        int remaining = Mathf.CeilToInt(seconds);
        while (remaining > 0)
        {
            yield return new WaitForSecondsRealtime(1f);
            remaining--;
        }
        OnConfirmClicked();
    }

    private void OnConfirmClicked()
    {
        if (m_autoCloseCoroutine != null)
        {
            StopCoroutine(m_autoCloseCoroutine);
            m_autoCloseCoroutine = null;
        }
        onConfirmCallback?.Invoke();
    }
}
