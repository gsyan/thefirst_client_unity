// 확인/취소 팝업: bodyText에 message + detailText를 표시, 요구/비용은 Row 컨테이너로 표시
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupConfirm : UIPopupBase
{
    [Header("Confirm Popup UI")]
    public TMP_Text titleText;
    [SerializeField] private TMP_Text m_bodyText;

    [SerializeField] private RectTransform m_layoutRoot;
    [SerializeField] private Transform m_resultContainer;
    [SerializeField] private GameObject m_result1;
    [SerializeField] private GameObject m_result2;
    private RowImageText[] m_resultRows;
    [SerializeField] private Transform m_requireContainer;
    private RowImageText[] m_requireRows;
    [SerializeField] private Transform m_costContainer;
    private RowImageText[] m_costRows;
    

    public Button confirmButton;
    public Button cancelButton;

    private Action onCancelCallback;
    private Action onConfirmCallback;

    protected override void Awake()
    {
        base.Awake();
        if (m_resultContainer != null)
            m_resultRows = m_resultContainer.GetComponentsInChildren<RowImageText>(true);
        if (m_requireContainer != null)
            m_requireRows = m_requireContainer.GetComponentsInChildren<RowImageText>(true);
        if (m_costContainer != null)
            m_costRows = m_costContainer.GetComponentsInChildren<RowImageText>(true);        
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    public void ShowPopupConfirm(string title, string message, string detailText, RequireStruct require, CostStruct cost, Action onConfirm, Action onCancel = null, List<(string icon, string value)> resultRows = null)
    {
        base.ShowPopup();
        if (titleText != null)
            titleText.text = title;

        bool canConfirm = true;
        if (m_bodyText != null)
        {
            string bodyStr = BuildBodyText(message, detailText);
            m_bodyText.text = bodyStr;
            m_bodyText.gameObject.SetActive(string.IsNullOrEmpty(bodyStr) == false);
        }

        bool requireMet = BuildRequireText(require);
        if (requireMet == false) canConfirm = false;

        bool canAfford = BuildCostText(cost);
        if (canAfford == false) canConfirm = false;

        BuildResultRows(resultRows);

        if (confirmButton != null) confirmButton.interactable = canConfirm;

        onCancelCallback = onCancel;
        onConfirmCallback = onConfirm;

        RebuildLayout();
    }

    private string BuildBodyText(string message, string detailText)
    {
        var sb = new StringBuilder();
        sb.Append(message);
        if (string.IsNullOrEmpty(detailText) == false)
            sb.Append(detailText);
        return sb.ToString();
    }

    private bool BuildRequireText(RequireStruct require)
    {
        if (require == null || require.techLevel <= 0)
        {
            if (m_requireContainer != null) m_requireContainer.gameObject.SetActive(false);
            return true;
        }

        if (m_requireContainer != null) m_requireContainer.gameObject.SetActive(true);

        var ch = DataManager.Instance.m_currentCharacter;
        int currentTechLevel = ch != null ? ch.GetTechLevel() : 0;
        bool requireMet = currentTechLevel >= require.techLevel;

        if (m_requireRows != null && m_requireRows.Length > 0)
        {
            string text = LocalizationManager.Instance.Get("require_level_compare", require.techLevel, currentTechLevel);
            m_requireRows[0].SetTextRowImageText(requireMet ? text : $"<color=red>{text}</color>");
        }

        return requireMet;
    }

    private bool BuildCostText(CostStruct cost)
    {
        if (cost == null || cost.amount <= 0)
        {
            if (m_costContainer != null) m_costContainer.gameObject.SetActive(false);
            return true;
        }

        if (m_costContainer != null) m_costContainer.gameObject.SetActive(true);

        if (m_costRows != null)
        {
            for (int i = 0; i < m_costRows.Length; i++)
                m_costRows[i].Hide();
        }

        var ch = DataManager.Instance.m_currentCharacter;
        long current = 0;
        if (cost.costType == ECostType.Mineral)
            current = ch != null ? ch.GetMineral() : 0;
        else if (cost.costType == ECostType.TechPoint)
            current = ch != null ? ch.GetTechPoint() : 0;
        else if (cost.costType == ECostType.ModulePoint)
            current = ch != null ? ch.GetModulePoint() : 0;
        else if (cost.costType == ECostType.PvpPoint)
            current = ch != null ? ch.GetPvpPoint() : 0;

        bool canAfford = current >= cost.amount;
        int rowIndex = (int)cost.costType;
        if (m_costRows != null && rowIndex < m_costRows.Length)
        {
            string val = CommonUtility.FormatBigNumber(cost.amount);
            m_costRows[rowIndex].SetTextRowImageText(canAfford ? val : $"<color=red>{val}</color>");
        }

        return canAfford;
    }

    private void BuildResultRows(List<(string icon, string value)> rows)
    {
        if (m_resultContainer == null) return;
        bool hasRows = rows != null && rows.Count > 0;
        m_resultContainer.gameObject.SetActive(hasRows);
        if (hasRows == false) return;
        for (int i = 0; i < m_resultRows.Length; i++)
        {
            if (i < rows.Count)
                m_resultRows[i].SetRow(rows[i].icon, rows[i].value);
            else
                m_resultRows[i].Hide();
        }
    }

    private void RebuildLayout()
    {
        if (m_result1 != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_result1.GetComponent<RectTransform>());
        if (m_result2 != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_result2.GetComponent<RectTransform>());
        if (m_resultContainer != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_resultContainer as RectTransform);
        if (m_requireContainer != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_requireContainer as RectTransform);
        if (m_costContainer != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_costContainer as RectTransform);
        if (m_layoutRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_layoutRoot);
    }

    private void OnConfirmClicked()
    {
        onConfirmCallback?.Invoke();
    }

    private void OnCancelClicked()
    {
        onCancelCallback?.Invoke();
    }
}
