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
    public Button confirmButton;
    public Button cancelButton;

    private Action onCancelCallback;
    private Action onConfirmCallback;

    private const string SEPARATOR = "\n<color=#666666>─────────────</color>\n";

    protected override void Awake()
    {
        base.Awake();
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    public void ShowPopupConfirm(string title, string message, string detailText, RequireStruct require, long mineralCost, Action onConfirm, Action onCancel = null)
    {
        if (titleText != null) titleText.text = title;

        bool canConfirm = true;
        if (bodyText != null) bodyText.text = BuildBodyText(message, detailText, require, mineralCost, out canConfirm);

        if (confirmButton != null) confirmButton.interactable = canConfirm;

        onCancelCallback = onCancel;
        onConfirmCallback = onConfirm;

        base.ShowPopup();
    }

    private string BuildBodyText(string message, string detailText, RequireStruct require, long mineralCost, out bool canConfirm)
    {
        var sb = new StringBuilder();
        sb.Append(message);

        if (string.IsNullOrEmpty(detailText) == false)
        {
            sb.Append(SEPARATOR);
            sb.Append(detailText);
        }

        canConfirm = true;

        string requireText = BuildRequireText(require, out bool requireMet);
        if (string.IsNullOrEmpty(requireText) == false)
        {
            sb.Append(SEPARATOR);
            sb.Append(requireText);
            if (requireMet == false) canConfirm = false;
        }

        string costText = BuildCostText(mineralCost, out bool canAfford);
        if (string.IsNullOrEmpty(costText) == false)
        {
            sb.Append(SEPARATOR);
            sb.Append(costText);
            if (canAfford == false) canConfirm = false;
        }

        return sb.ToString();
    }

    private string BuildRequireText(RequireStruct require, out bool requireMet)
    {
        requireMet = true;
        if (require == null || require.techLevel <= 0) return null;

        var ch = DataManager.Instance.m_currentCharacter;
        int currentTechLevel = ch != null ? ch.GetTechLevel() : 0;
        requireMet = currentTechLevel >= require.techLevel;

        string text = LocalizationManager.Instance.Get("require_level_compare", CommonUtility.Sprite("gears"), require.techLevel, currentTechLevel);
        return requireMet ? text : $"<color=red>{text}</color>";
    }

    private string BuildCostText(long mineralCost, out bool canAfford)
    {
        canAfford = true;
        if (mineralCost <= 0) return null;

        var info = DataManager.Instance.m_currentCharacter?.m_characterInfo;
        bool ins = info != null && info.mineral < mineralCost;
        if (ins == true) canAfford = false;

        string C(bool insufficient, string val) => insufficient ? $"<color=red>{val}</color>" : val;
        return $"{CommonUtility.Sprite("crystal-growth")} {C(ins, CommonUtility.FormatBigNumber(mineralCost))}";
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
