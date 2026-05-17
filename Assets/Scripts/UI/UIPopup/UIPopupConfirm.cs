using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 확인/취소 팝업 설정 데이터
public class ConfirmPopupConfig
{
    public string title;
    public string message;
    public string detailText;
    public List<(string icon, string value)> resultRows;
    public RequireStruct require;
    public CostStruct cost;
    public int refundAmount;
    public List<int> rewardAmounts;
    public Action onConfirm;
    public Action onCancel;

    // 버튼 커스터마이징 (null이면 프리팹 기본값 유지)
    public Sprite cancelImage;
    public string cancelText1;
    public string cancelText2;
    public Sprite confirmImage;
    public string confirmText1;
    public string confirmText2;
}

// 확인/취소 팝업: bodyText에 message + detailText를 표시, 요구/비용은 UISection으로 표시
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
    [SerializeField] private UISection m_sectionReward;

    [SerializeField] private Button cancelButton;
    [SerializeField] private Image m_cancelImage;
    [SerializeField] private TMP_Text m_cancelText1;
    [SerializeField] private TMP_Text m_cancelText2;

    [SerializeField] private Button confirmButton;
    [SerializeField] private Image m_confirmImage;
    [SerializeField] private TMP_Text m_confirmText1;
    [SerializeField] private TMP_Text m_confirmText2;

    private Action onCancelCallback;
    private Action onConfirmCallback;

    private Sprite m_defaultCancelImage;
    private Sprite m_defaultConfirmImage;

    protected override void Awake()
    {
        base.Awake();
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);

        if (m_cancelImage != null) m_defaultCancelImage = m_cancelImage.sprite;
        if (m_confirmImage != null) m_defaultConfirmImage = m_confirmImage.sprite;
    }

    public void ShowPopupConfirm(ConfirmPopupConfig config)
    {
        base.ShowPopup();
        if (titleText != null)
            titleText.text = config.title;

        bool canConfirm = true;
        if (m_bodyText != null)
        {
            string bodyStr = BuildBodyText(config.message, config.detailText);
            m_bodyText.text = bodyStr;
            m_bodyText.gameObject.SetActive(string.IsNullOrEmpty(bodyStr) == false);
        }

        bool requireMet = BuildRequireSection(config.require);
        if (requireMet == false) canConfirm = false;

        bool canAfford = BuildCostSection(config.cost);
        if (canAfford == false) canConfirm = false;

        BuildRefundSection(config.refundAmount);
        BuildResultRows(config.resultRows);
        BuildRewardSection(config.rewardAmounts);
        BuildButtonSection(config);

        if (confirmButton != null) confirmButton.interactable = canConfirm;

        onCancelCallback = config.onCancel;
        onConfirmCallback = config.onConfirm;

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

    private bool BuildRequireSection(RequireStruct require)
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

    private bool BuildCostSection(CostStruct cost)
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

    private void BuildRewardSection(List<int> amounts)
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

    private void BuildButtonSection(ConfirmPopupConfig config)
    {
        var loc = LocalizationManager.Instance;

        if (m_cancelImage != null) m_cancelImage.sprite = config.cancelImage != null ? config.cancelImage : m_defaultCancelImage;
        if (m_cancelText1 != null) m_cancelText1.text = config.cancelText1 ?? loc.Get("Simple_Cancel");
        if (m_cancelText2 != null)
        {
            bool has = string.IsNullOrEmpty(config.cancelText2) == false;
            m_cancelText2.gameObject.SetActive(has);
            if (has) m_cancelText2.text = config.cancelText2;
        }

        if (m_confirmImage != null) m_confirmImage.sprite = config.confirmImage != null ? config.confirmImage : m_defaultConfirmImage;
        if (m_confirmText1 != null) m_confirmText1.text = config.confirmText1 ?? loc.Get("Simple_Confirm");
        if (m_confirmText2 != null)
        {
            bool has = string.IsNullOrEmpty(config.confirmText2) == false;
            m_confirmText2.gameObject.SetActive(has);
            if (has) m_confirmText2.text = config.confirmText2;
        }
    }

    private void RebuildLayout()
    {
        if (m_sectionResult != null) m_sectionResult.RebuildLayout();
        if (m_sectionRefund != null) m_sectionRefund.RebuildLayout();
        if (m_sectionRequire != null) m_sectionRequire.RebuildLayout();
        if (m_sectionCost != null) m_sectionCost.RebuildLayout();
        if (m_sectionReward != null) m_sectionReward.RebuildLayout();
        if (cancelButton != null) LayoutRebuilder.ForceRebuildLayoutImmediate(cancelButton.GetComponent<RectTransform>());
        if (confirmButton != null) LayoutRebuilder.ForceRebuildLayoutImmediate(confirmButton.GetComponent<RectTransform>());
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
