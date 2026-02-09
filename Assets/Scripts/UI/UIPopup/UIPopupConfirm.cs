using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupConfirm : UIPopupBase
{
    [Header("Confirm Popup UI")]
    public TMP_Text titleText;
    public TMP_Text messageText;
    [SerializeField] private RowLabelValue m_rowPrefab;
    [SerializeField] private Transform m_rowContainer;
    public Button confirmButton;
    public Button cancelButton;

    private Action onCancelCallback;
    private Action onConfirmCallback;
    private List<RowLabelValue> m_activeRows = new List<RowLabelValue>();
    private List<RowLabelValue> m_pooledRows = new List<RowLabelValue>();
    
    protected override void Awake()
    {
        base.Awake();
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    public void ShowPopupConfirm(string title, string message, CostStruct cost, Action onConfirm, Action onCancel = null)
    {
        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;

        // 기존 Row들 풀에 반환
        ClearRows();

        // CostStruct 필드별로 0보다 크면 Row 추가
        if (cost.techLevel > 0) AddRow("tech_level", cost.techLevel.ToString("N0"));
        if (cost.mineral > 0) AddRow("mineral_amount", CommonUtility.FormatBigNumber(cost.mineral));
        if (cost.mineralRare > 0) AddRow("mineral_rare_amount", CommonUtility.FormatBigNumber(cost.mineralRare));
        if (cost.mineralExotic > 0) AddRow("mineral_exotic_amount", CommonUtility.FormatBigNumber(cost.mineralExotic));
        if (cost.mineralDark > 0) AddRow("mineral_dark_amount", CommonUtility.FormatBigNumber(cost.mineralDark));

        onCancelCallback = onCancel;
        onConfirmCallback = onConfirm;

        base.ShowPopup();
    }

    private void AddRow(string labelKey, string value)
    {
        RowLabelValue row = GetOrCreateRow();
        row.SetRow(labelKey, value);
        row.gameObject.SetActive(true);
        m_activeRows.Add(row);
    }

    private RowLabelValue GetOrCreateRow()
    {
        if (m_pooledRows.Count > 0)
        {
            var row = m_pooledRows[m_pooledRows.Count - 1];
            m_pooledRows.RemoveAt(m_pooledRows.Count - 1);
            return row;
        }
        return Instantiate(m_rowPrefab, m_rowContainer);
    }

    private void ClearRows()
    {
        foreach (var row in m_activeRows)
        {
            row.gameObject.SetActive(false);
            m_pooledRows.Add(row);
        }
        m_activeRows.Clear();
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
