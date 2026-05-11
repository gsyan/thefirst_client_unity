// 확인/취소 팝업: bodyText에 message + detailText + cost 를 조합해 표시
// detailText/cost null 또는 전부 0이면 해당 섹션 생략
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupConfirm : UIPopupBase
{
    [Header("Confirm Popup UI")]
    public TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;

    [SerializeField] private RowImageText m_techLevel;
    [SerializeField] private RowImageText m_mineral;
    [SerializeField] private RowImageText m_techPoint;
    [SerializeField] private RowImageText m_modulePoint;
    [SerializeField] private RowImageText m_pvpPoint;

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

    public void ShowPopupConfirm(string title, string message, string detailText, RequireStruct require, CostStruct cost, Action onConfirm, Action onCancel = null)
    {
        base.ShowPopup();
        if (titleText != null)
            titleText.text = title;

        bool canConfirm = true;
        if (bodyText != null) bodyText.text = BuildBodyText(message, detailText, require, cost, out canConfirm);

        if (confirmButton != null) confirmButton.interactable = canConfirm;

        onCancelCallback = onCancel;
        onConfirmCallback = onConfirm;

        LayoutRebuilder.ForceRebuildLayoutImmediate(titleText.transform.parent as RectTransform);
    }

    private string BuildBodyText(string message, string detailText, RequireStruct require, CostStruct cost, out bool canConfirm)
    {
        var sb = new StringBuilder();
        sb.Append(message);

        if (string.IsNullOrEmpty(detailText) == false)
            sb.Append(detailText);

        canConfirm = true;

        bool requireMet = BuildRequireText(require);
        if (requireMet == false) canConfirm = false;

        bool canAfford = BuildCostText(cost);
        if (canAfford == false) canConfirm = false;

        return sb.ToString();
    }

    private bool BuildRequireText(RequireStruct require)
    {
        if (require == null || require.techLevel <= 0)
        {
            m_techLevel.Hide();
            return true;
        }

        var ch = DataManager.Instance.m_currentCharacter;
        int currentTechLevel = ch != null ? ch.GetTechLevel() : 0;
        bool requireMet = currentTechLevel >= require.techLevel;

        string text = LocalizationManager.Instance.Get("require_level_compare", require.techLevel, currentTechLevel);
        m_techLevel.SetText( requireMet ? text : $"<color=red>{text}</color>");
        return requireMet;
    }

    private bool BuildCostText(CostStruct cost)
    {
        m_mineral.Hide();
        m_techPoint.Hide();
        m_modulePoint.Hide();
        m_pvpPoint.Hide();

        if (cost == null || cost.amount <= 0) return true;

        var ch = DataManager.Instance.m_currentCharacter;
        long current = 0;
        RowImageText row = null;
        
        if (cost.costType == ECostType.Mineral)
        {
            row = m_mineral;
            current = ch != null ? ch.GetMineral() : 0;
        }
        else if (cost.costType == ECostType.TechPoint)
        {
            row = m_techPoint;
            current = ch != null ? ch.GetTechPoint() : 0;
        }
        else if (cost.costType == ECostType.ModulePoint)
        {
            row = m_modulePoint;
            current = ch != null ? ch.GetModulePoint() : 0;
        }
        else if (cost.costType == ECostType.PvpPoint)
        {
            row = m_pvpPoint;
            current = ch != null ? ch.GetPvpPoint() : 0;
        }

        bool canAfford = current >= cost.amount;
        if (row != null)
        {
            string val = CommonUtility.FormatBigNumber(cost.amount);
            row.gameObject.SetActive(true);
            row.SetText(canAfford ? val : $"<color=red>{val}</color>");
        }
        return canAfford;
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
