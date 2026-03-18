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

    private const string SEPARATOR = "\n\n<color=#666666>─────────────</color>\n\n";

    protected override void Awake()
    {
        base.Awake();
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    public void ShowPopupConfirm(string title, string message, string detailText, CostStruct cost, Action onConfirm, Action onCancel = null)
    {
        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = BuildBodyText(message, detailText, cost);

        onCancelCallback = onCancel;
        onConfirmCallback = onConfirm;

        base.ShowPopup();
    }

    private string BuildBodyText(string message, string detailText, CostStruct cost)
    {
        var sb = new StringBuilder();
        sb.Append(message);

        if (string.IsNullOrEmpty(detailText) == false)
        {
            sb.Append(SEPARATOR);
            sb.Append(detailText);
        }

        string costText = BuildCostText(cost);
        if (string.IsNullOrEmpty(costText) == false)
        {
            sb.Append(SEPARATOR);
            sb.Append(costText);
        }

        return sb.ToString();
    }

    private string BuildCostText(CostStruct cost)
    {
        if (cost == null) return null;
        bool hasCost = cost.techLevel > 0 || cost.mineral > 0 || cost.mineralRare > 0 || cost.mineralExotic > 0 || cost.mineralDark > 0;
        if (hasCost == false) return null;

        var ch = DataManager.Instance.m_currentCharacter;
        var techLevel = ch.GetTechLevel();
        var info = ch?.m_characterInfo;
        var sb = new StringBuilder();

        string C(bool insufficient, string val) => insufficient ? $"<color=red>{val}</color>" : val;

        if (cost.techLevel > 0)
        {
            bool ins = info != null && techLevel < cost.techLevel;
            sb.Append($"<sprite name=\"IconTech\"> {C(ins, CommonUtility.FormatBigNumber(cost.techLevel))}\n\n");
        }
        if (cost.mineral > 0)
        {
            bool ins = info != null && info.mineral < cost.mineral;
            sb.Append($"<sprite name=\"IconMineral\"> {C(ins, CommonUtility.FormatBigNumber(cost.mineral))}\n\n");
        }
        if (cost.mineralRare > 0)
        {
            bool ins = info != null && info.mineralRare < cost.mineralRare;
            sb.Append($"<sprite name=\"IconMineralRare\"> {C(ins, CommonUtility.FormatBigNumber(cost.mineralRare))}\n\n");
        }
        if (cost.mineralExotic > 0)
        {
            bool ins = info != null && info.mineralExotic < cost.mineralExotic;
            sb.Append($"<sprite name=\"IconMineralExotic\"> {C(ins, CommonUtility.FormatBigNumber(cost.mineralExotic))}\n\n");
        }
        if (cost.mineralDark > 0)
        {
            bool ins = info != null && info.mineralDark < cost.mineralDark;
            sb.Append($"<sprite name=\"IconMineralDark\"> {C(ins, CommonUtility.FormatBigNumber(cost.mineralDark))}\n\n");
        }

        return sb.ToString().TrimEnd();
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
