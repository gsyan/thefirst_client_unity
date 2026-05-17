using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 알림 팝업 설정 데이터
public class AlertPopupConfig
{
    public string title;
    public string message;
    public List<int> rewardAmounts;
    public float autoCloseSec;
    public Action onConfirm;
}

// 확인 버튼만 있는 단순 알림 팝업
public class UIPopupAlert : UIPopupBase
{
    [Header("Alert Popup UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button okButton;
    [SerializeField] private Button okButtonBackground;
    [SerializeField] private UISection m_sectionReward;
    [SerializeField] private RectTransform m_layoutRoot;

    private Action onConfirmCallback;
    private Coroutine m_autoCloseCoroutine;

    protected override void Awake()
    {
        base.Awake();
        if (okButton != null)
            okButton.onClick.AddListener(OnConfirmClicked);
        if (okButtonBackground != null)
            okButtonBackground.onClick.AddListener(OnConfirmClicked);
        if (m_sectionReward != null)
            m_sectionReward.SetVisible(false);
    }

    public void ShowPopupAlert(AlertPopupConfig config)
    {
        if (titleText != null) titleText.text = config.title;
        if (messageText != null)
        {
            messageText.text = config.message;
            messageText.gameObject.SetActive(string.IsNullOrEmpty(config.message) == false);
        }

        BuildRewardRows(config.rewardAmounts);

        onConfirmCallback = config.onConfirm;

        if (m_autoCloseCoroutine != null)
            StopCoroutine(m_autoCloseCoroutine);
        m_autoCloseCoroutine = null;

        base.ShowPopup();

        if (m_sectionReward != null) m_sectionReward.RebuildLayout();
        if (m_layoutRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_layoutRoot);

        if (config.autoCloseSec > 0f)
            m_autoCloseCoroutine = StartCoroutine(AutoCloseRoutine(config.autoCloseSec));
    }

    private void BuildRewardRows(List<int> amounts)
    {
        if (m_sectionReward == null) return;

        bool hasAny = false;
        if (amounts != null)
        {
            for (int i = 0; i < amounts.Count; i++)
            {
                if (amounts[i] > 0) { hasAny = true; break; }
            }
        }

        m_sectionReward.SetVisible(hasAny);
        if (hasAny == false) return;

        m_sectionReward.HideAllRows();
        for (int i = 0; i < amounts.Count; i++)
        {
            if (amounts[i] > 0)
                m_sectionReward.SetRowText(i, CommonUtility.FormatBigNumber(amounts[i]));
        }
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
