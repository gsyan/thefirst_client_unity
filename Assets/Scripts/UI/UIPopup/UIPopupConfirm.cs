using System;
using System.Collections;
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
    public List<(string icon, string value, Color color)> resultRows;
    public bool resultRowsVertical; // true면 컨테이너당 1개씩 세로 배치
    public List<(string icon, string value)> pvpOpponentRows; // STATUS 섹션 (GeneralBright1 색)
    public RequireStruct require;
    public CostStruct cost;
    public int refundAmount;
    public List<int> rewardAmounts;
    public int mineralVipMultiplier; // 0이면 기본 표시, 양수이면 미네랄 행에 "× N(VIP)" 접미사
    public Action onConfirm;
    public Action onCancel;
    public float autoCloseSec;

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
    [SerializeField] private UISection m_sectionPrefab;
    [SerializeField] private RectTransform m_sectionsRoot;

    [SerializeField] private Button cancelButton;
    [SerializeField] private Image m_cancelImage;
    [SerializeField] private TMP_Text m_cancelText1;
    [SerializeField] private TMP_Text m_cancelText2;

    [SerializeField] private UIButtonHasChildren confirmButton;
    [SerializeField] private Image m_confirmImage;
    [SerializeField] private TMP_Text m_confirmText1;
    [SerializeField] private TMP_Text m_confirmText2;

    private Action onCancelCallback;
    private Action onConfirmCallback;
    private Coroutine m_autoCloseCoroutine;
    private static readonly WaitForSecondsRealtime s_wait1Sec = new WaitForSecondsRealtime(1f);

    private Sprite m_defaultCancelImage;
    private Sprite m_defaultConfirmImage;

    private List<UISection> m_sectionCache = new List<UISection>();

    protected override void Awake()
    {
        base.Awake();
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (confirmButton != null) confirmButton.GetButton().onClick.AddListener(OnConfirmClicked);

        if (m_cancelImage != null) m_defaultCancelImage = m_cancelImage.sprite;
        if (m_confirmImage != null) m_defaultConfirmImage = m_confirmImage.sprite;
    }

    private void OnCancelClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        StopAutoClose();
        onCancelCallback?.Invoke();
    }

    private void OnConfirmClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        StopAutoClose();
        onConfirmCallback?.Invoke();
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

        int sectionIdx = 0;
        BuildResultRows(config.resultRows, config.resultRowsVertical, ref sectionIdx);
        BuildPvpOpponentSection(config.pvpOpponentRows, ref sectionIdx);
        bool requireMet = BuildRequireSection(config.require, ref sectionIdx);
        bool canAfford  = BuildCostSection(config.cost, ref sectionIdx);
        BuildRefundSection(config.refundAmount, ref sectionIdx);
        BuildRewardSection(config.rewardAmounts, config.mineralVipMultiplier, ref sectionIdx);
        HideUnusedSections(sectionIdx);

        if (requireMet == false) canConfirm = false;
        if (canAfford == false) canConfirm = false;

        BuildButtonSection(config);
        if (confirmButton != null) confirmButton.SetInteractable(canConfirm);

        onCancelCallback = config.onCancel;
        onConfirmCallback = config.onConfirm;

        if (m_autoCloseCoroutine != null) StopCoroutine(m_autoCloseCoroutine);
        m_autoCloseCoroutine = null;
        if (config.autoCloseSec > 0f)
            m_autoCloseCoroutine = StartCoroutine(AutoCloseRoutine(config.autoCloseSec));

        RebuildLayout();
    }

    private UISection GetOrCreateSection(ref int idx)
    {
        if (idx < m_sectionCache.Count)
        {
            m_sectionCache[idx].SetVisible(true);
            return m_sectionCache[idx++];
        }
        UISection sec = Instantiate(m_sectionPrefab, m_sectionsRoot);
        m_sectionCache.Add(sec);
        idx++;
        return sec;
    }

    private void HideUnusedSections(int usedCount)
    {
        for (int i = usedCount; i < m_sectionCache.Count; i++)
            m_sectionCache[i].SetVisible(false);
    }

    private string BuildBodyText(string message, string detailText)
    {
        var sb = new StringBuilder();
        sb.Append(message);
        if (string.IsNullOrEmpty(detailText) == false)
            sb.Append(detailText);
        return sb.ToString();
    }

    private bool BuildRequireSection(RequireStruct require, ref int sectionIdx)
    {
        if (require == null || require.commanderLevel <= 0)
            return true;

        UISection sec = GetOrCreateSection(ref sectionIdx);
        sec.gameObject.name = "UISection_Require";
        sec.SetTitle("REQUIRE");
        sec.HideAllRows();

        var ch = DataManager.Instance.m_currentCommander;
        int currentCommanderLevel = ch != null ? ch.GetCommanderLevel() : 0;
        bool requireMet = currentCommanderLevel >= require.commanderLevel;

        string icon = require.commanderLevel > 0 ? "icon_tech" : string.Empty;
        string text = LocalizationManager.Instance.Get("require_level_compare", require.commanderLevel, currentCommanderLevel);
        sec.SetRow(0, icon, CommonUtility.PaletteColor("GeneralBright1"), requireMet ? text : $"<color=red>{text}</color>");

        return requireMet;
    }

    private bool BuildCostSection(CostStruct cost, ref int sectionIdx)
    {
        if (cost == null || cost.amount <= 0)
            return true;

        UISection sec = GetOrCreateSection(ref sectionIdx);
        sec.gameObject.name = "UISection_Cost";
        sec.SetTitle("COST");
        sec.HideAllRows();

        var ch = DataManager.Instance.m_currentCommander;
        long current = 0;
        if (cost.costType == ECostType.Mineral)
            current = ch != null ? ch.GetMineral() : 0;
        else if (cost.costType == ECostType.ModulePoint)
            current = ch != null ? ch.GetModulePoint() : 0;
        else if (cost.costType == ECostType.PvpPoint)
            current = ch != null ? ch.GetPvpPoint() : 0;

        bool canAfford = current >= cost.amount;
        string val = CommonUtility.FormatBigNumber(cost.amount);
        Color iconColor = GetCostColor(cost.costType);
        sec.SetRow(0, "mineral_basic", iconColor, canAfford ? val : $"<color=red>{val}</color>");

        return canAfford;
    }

    private void BuildResultRows(List<(string icon, string value, Color color)> rows, bool vertical, ref int sectionIdx)
    {
        if (rows == null || rows.Count <= 0)
            return;

        UISection sec = GetOrCreateSection(ref sectionIdx);
        sec.gameObject.name = "UISection_Result";
        sec.SetTitle("RESULT");
        if (vertical)
            sec.SetRowsVertical(rows);
        else
            sec.SetRows(rows);
    }

    private void BuildPvpOpponentSection(List<(string icon, string value)> rows, ref int sectionIdx)
    {
        if (rows == null || rows.Count <= 0)
            return;

        UISection sec = GetOrCreateSection(ref sectionIdx);
        sec.gameObject.name = "UISection_Status";
        sec.SetTitle("STATUS");
        sec.SetRows(rows, CommonUtility.PaletteColor("GeneralBright1"));
    }

    private void BuildRefundSection(int refundAmount, ref int sectionIdx)
    {
        if (refundAmount <= 0)
            return;

        UISection sec = GetOrCreateSection(ref sectionIdx);
        sec.gameObject.name = "UISection_Refund";
        sec.SetTitle("REFUND");
        sec.HideAllRows();
        sec.SetRowText(0, CommonUtility.FormatBigNumber(refundAmount));
    }

    private void BuildRewardSection(List<int> amounts, int mineralVipMultiplier, ref int sectionIdx)
    {
        bool hasAny = false;
        if (amounts != null)
        {
            for (int i = 0; i < amounts.Count; i++)
            {
                if (amounts[i] > 0) { hasAny = true; break; }
            }
        }
        if (hasAny == false)
            return;

        UISection sec = GetOrCreateSection(ref sectionIdx);
        sec.gameObject.name = "UISection_Reward";
        sec.SetTitle("REWARD");
        sec.HideAllRows();
        for (int i = 0; i < amounts.Count; i++)
        {
            if (amounts[i] > 0)
            {
                string text = CommonUtility.FormatBigNumber(amounts[i]);
                if (i == 0 && mineralVipMultiplier > 0)
                    text = $"{text} × {mineralVipMultiplier}(VIP)";
                sec.SetRow(i, "mineral_basic", GetRewardColor(i), text);
            }
        }
    }

    private static Color GetRewardColor(int rewardIndex)
    {
        if (rewardIndex == 0) return CommonUtility.PaletteColor("Mineral");
        if (rewardIndex == 1) return CommonUtility.PaletteColor("Commander");
        if (rewardIndex == 2) return CommonUtility.PaletteColor("ModulePoint");
        return CommonUtility.PaletteColor("GeneralBright1");
    }

    private void BuildButtonSection(ConfirmPopupConfig config)
    {
        var loc = LocalizationManager.Instance;

        bool showCancel = config.onCancel != null;
        if (cancelButton != null) cancelButton.gameObject.SetActive(showCancel);
        if (showCancel)
        {
            if (m_cancelImage != null) m_cancelImage.sprite = config.cancelImage != null ? config.cancelImage : m_defaultCancelImage;
            if (m_cancelText1 != null) m_cancelText1.text = config.cancelText1 ?? loc.Get("Simple_Cancel");
            if (m_cancelText2 != null)
            {
                bool has = string.IsNullOrEmpty(config.cancelText2) == false;
                m_cancelText2.gameObject.SetActive(has);
                if (has) m_cancelText2.text = config.cancelText2;
            }
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
        for (int i = 0; i < m_sectionCache.Count; i++)
        {
            if (m_sectionCache[i].gameObject.activeSelf == true)
                m_sectionCache[i].RebuildLayout();
        }
        if (m_sectionsRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_sectionsRoot);
        if (cancelButton != null) LayoutRebuilder.ForceRebuildLayoutImmediate(cancelButton.GetComponent<RectTransform>());
        if (confirmButton != null) LayoutRebuilder.ForceRebuildLayoutImmediate(confirmButton.GetComponent<RectTransform>());
        if (m_layoutRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_layoutRoot);
    }

    private IEnumerator AutoCloseRoutine(float seconds)
    {
        int remaining = Mathf.CeilToInt(seconds);
        while (remaining > 0)
        {
            yield return s_wait1Sec;
            remaining--;
        }
        // cancel 버튼이 있으면 취소로, 없으면(단순 알림 용도) 확인으로 처리
        if (onCancelCallback != null)
            OnCancelClicked();
        else
            OnConfirmClicked();
    }

    private void StopAutoClose()
    {
        if (m_autoCloseCoroutine != null)
        {
            StopCoroutine(m_autoCloseCoroutine);
            m_autoCloseCoroutine = null;
        }
    }

    private static Color GetCostColor(ECostType costType)
    {
        if (costType == ECostType.Mineral)     return CommonUtility.PaletteColor("Mineral");
        if (costType == ECostType.ModulePoint) return CommonUtility.PaletteColor("ModulePoint");
        if (costType == ECostType.PvpPoint)    return CommonUtility.PaletteColor("PvpPoint");
        return Color.white;
    }
}
