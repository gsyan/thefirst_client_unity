// 확인/취소 팝업: bodyText에 message + detailText를 표시, 요구/비용은 UISection으로 표시
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
    [SerializeField] private UISection m_sectionResult;
    [SerializeField] private UISection m_sectionRequire;
    [SerializeField] private UISection m_sectionCost;
    [SerializeField] private UISection m_sectionRefund;

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

    public void ShowPopupConfirm(string title, string message, string detailText, RequireStruct require, CostStruct cost, int refundAmount, Action onConfirm, Action onCancel = null, List<(string icon, string value)> resultRows = null)
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

        BuildRefundSection(refundAmount);
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
            if (m_sectionRequire != null) m_sectionRequire.SetVisible(false);
            return true;
        }

        if (m_sectionRequire != null) m_sectionRequire.SetVisible(true);

        var ch = DataManager.Instance.m_currentCharacter;
        int currentTechLevel = ch != null ? ch.GetTechLevel() : 0;
        bool requireMet = currentTechLevel >= require.techLevel;

        string text = LocalizationManager.Instance.Get("require_level_compare", require.techLevel, currentTechLevel);
        if (m_sectionRequire != null)
            m_sectionRequire.SetRowText(0, requireMet ? text : $"<color=red>{text}</color>");

        return requireMet;
    }

    private bool BuildCostText(CostStruct cost)
    {
        if (cost == null || cost.amount <= 0)
        {
            if (m_sectionCost != null) m_sectionCost.SetVisible(false);
            return true;
        }

        if (m_sectionCost != null)
        {
            m_sectionCost.SetVisible(true);
            m_sectionCost.HideAllRows();
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
        if (m_sectionCost != null)
        {
            string val = CommonUtility.FormatBigNumber(cost.amount);
            m_sectionCost.SetRowText(rowIndex, canAfford ? val : $"<color=red>{val}</color>");
        }

        return canAfford;
    }

    private void BuildResultRows(List<(string icon, string value)> rows)
    {
        if (m_sectionResult == null) return;
        bool hasRows = rows != null && rows.Count > 0;
        m_sectionResult.SetVisible(hasRows);
        if (hasRows == false) return;
        m_sectionResult.SetRows(rows);
    }

    private void BuildRefundSection(int refundAmount)
    {
        if (m_sectionRefund == null) return;
        if (refundAmount <= 0)
        {
            m_sectionRefund.SetVisible(false);
            return;
        }

        m_sectionRefund.SetVisible(true);
        m_sectionRefund.HideAllRows();
        m_sectionRefund.SetRowText(0, CommonUtility.FormatBigNumber(refundAmount));
    }

    private void RebuildLayout()
    {
        if (m_sectionResult != null) m_sectionResult.RebuildLayout();
        if (m_sectionRefund != null) m_sectionRefund.RebuildLayout();
        if (m_sectionRequire != null) m_sectionRequire.RebuildLayout();
        if (m_sectionCost != null) m_sectionCost.RebuildLayout();
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
