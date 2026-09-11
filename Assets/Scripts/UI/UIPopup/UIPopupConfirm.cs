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
    public string message;
    public string detailText;
    public List<(string label, string value, Color? color)> resultRows;
    public bool resultRowsVertical; // 라벨+값 행은 항상 한 줄에 한 항목이라 현재는 무의미(시그니처 호환용으로만 유지)
    public string resultSectionTitle = "RESULT"; // resultRows 섹션 헤더 텍스트
    public List<(string label, string value)> pvpOpponentRows; // STATUS 섹션 (General.Bright1 색)
    public RequireStruct require;
    public CostStruct cost;
    public int refundAmount;
    public List<int> rewardAmounts;
    public int mineralVipMultiplier; // 0이면 기본 표시, 양수이면 미네랄 행에 "× N(VIP)" 접미사
    public Action onConfirm;
    public Action onCancel;
    public float autoCloseSec;

    // 버튼 커스터마이징 (null이면 프리팹 기본값 유지)
    public string cancelText1;
    public string confirmText1;

    // 3번째 버튼(선택) — extraText1/onExtra 둘 다 null이면 버튼 자체가 숨겨짐(기존 2버튼 팝업과 동일하게 동작)
    public string extraText1;
    public Action onExtra;
}

// 확인/취소 팝업: bodyText에 message + detailText를 표시, 요구/비용은 UISection으로 표시
public class UIPopupConfirm : UIPopupBase
{
    [Header("Confirm Popup UI")]
    [SerializeField] private TMP_Text m_bodyText;

    [SerializeField] private RectTransform m_layoutRoot;

    [SerializeField] private RectTransform m_sectionsRoot;
    [SerializeField] private UISection m_sectionPrefab;

    [SerializeField] private RowLabelValue m_ownedPointRow; // COST 섹션 바로 위에 현재 보유량을 한 줄로 표시(값 우측 정렬은 프리팹 TMP_Text Alignment 설정)

    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text m_cancelText1;
    
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text m_confirmText1;

    [SerializeField] private Button m_extraButton;
    [SerializeField] private TMP_Text m_extraText1;

    private Action onCancelCallback;
    private Action onConfirmCallback;
    private Action onExtraCallback;
    private Coroutine m_autoCloseCoroutine;
    private static readonly WaitForSecondsRealtime s_wait1Sec = new WaitForSecondsRealtime(1f);

    private List<UISection> m_sectionCache = new List<UISection>();

    protected override void Awake()
    {
        base.Awake();
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
        if (m_extraButton != null)
            m_extraButton.onClick.AddListener(OnExtraClicked);
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

    private void OnExtraClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        StopAutoClose();
        onExtraCallback?.Invoke();
    }

    public void ShowPopupConfirm(ConfirmPopupConfig config)
    {
        base.ShowPopup();
        
        bool canConfirm = true;
        if (m_bodyText != null)
        {
            string bodyStr = BuildBodyText(config.message, config.detailText);
            m_bodyText.text = bodyStr;
            m_bodyText.gameObject.SetActive(string.IsNullOrEmpty(bodyStr) == false);
        }

        // 섹션 빌드 시작 전 무조건 켜두고, 끝난 뒤 필요 여부에 따라 다시 정리
        if (m_sectionsRoot != null) m_sectionsRoot.gameObject.SetActive(true);

        int sectionIdx = 0;
        BuildResultRows(config.resultRows, config.resultRowsVertical, config.resultSectionTitle, ref sectionIdx);
        BuildPvpOpponentSection(config.pvpOpponentRows, ref sectionIdx);
        bool requireMet = BuildRequireSection(config.require, ref sectionIdx);
        bool canAfford  = BuildCostSection(config.cost, ref sectionIdx);
        BuildRefundSection(config.refundAmount, ref sectionIdx);
        BuildRewardSection(config.rewardAmounts, config.mineralVipMultiplier, ref sectionIdx);
        HideUnusedSections(sectionIdx);
        if (m_sectionsRoot != null) m_sectionsRoot.gameObject.SetActive(sectionIdx > 0);

        if (requireMet == false) canConfirm = false;
        if (canAfford == false) canConfirm = false;

        BuildButtonSection(config);
        if (confirmButton != null) confirmButton.interactable = canConfirm;

        onCancelCallback = config.onCancel;
        onConfirmCallback = config.onConfirm;
        onExtraCallback = config.onExtra;

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

        string text = LocalizationManager.Instance.Get("require_level_compare", require.commanderLevel, currentCommanderLevel);
        sec.SetRow(0, "UIPopupConfirm_RequireLevel", CommonUtility.PaletteColor("General.Bright1"), requireMet ? text : $"<color=red>{text}</color>");

        return requireMet;
    }

    private bool BuildCostSection(CostStruct cost, ref int sectionIdx)
    {
        if (cost == null || cost.amount <= 0)
        {
            if (m_ownedPointRow != null)
            {
                m_ownedPointRow.Hide();
                SetOwnedPointRowParentActive(false);
            }
            return true;
        }

        UISection sec = GetOrCreateSection(ref sectionIdx);
        sec.gameObject.name = "UISection_Cost";
        sec.SetTitle("COST");
        sec.HideAllRows();

        var ch = DataManager.Instance.m_currentCommander;
        long current = 0;
        if (cost.costType == ECostType.PvpPoint)
            current = ch != null ? ch.GetPvpPoint() : 0;
        else if (cost.costType == ECostType.AchievementPoint)
            current = ch != null ? ch.GetAchievementPoint() : 0;

        bool canAfford = current >= cost.amount;
        string val = CommonUtility.FormatBigNumber(cost.amount);
        Color iconColor = GetCostColor(cost.costType);
        sec.SetRow(0, GetCostLabelKey(cost.costType), iconColor, canAfford ? val : $"<color=red>{val}</color>");

        if (m_ownedPointRow != null)
        {
            m_ownedPointRow.SetRow("UIPopupConfirm_OwnedLabel", CommonUtility.FormatBigNumber(current), rawValue: true);
            m_ownedPointRow.SetValueColor(iconColor);
            SetOwnedPointRowParentActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_ownedPointRow.transform as RectTransform);
        }

        return canAfford;
    }

    // m_ownedPointRow 자신만 SetActive로는 부모 컨테이너(패딩/배경 등)가 그대로 남아 빈 여백이 생길 수 있어 부모도 같이 토글
    private void SetOwnedPointRowParentActive(bool active)
    {
        if (m_ownedPointRow == null) return;
        Transform parent = m_ownedPointRow.transform.parent;
        if (parent != null) parent.gameObject.SetActive(active);
    }

    private static string GetCostLabelKey(ECostType costType)
    {
        if (costType == ECostType.PvpPoint) return "UIPopupConfirm_PvpPointLabel";
        if (costType == ECostType.AchievementPoint) return "UIPanelFleet_AchievementPoint";
        return "UIPopupConfirm_PvpPointLabel";
    }

    private void BuildResultRows(List<(string label, string value, Color? color)> rows, bool vertical, string title, ref int sectionIdx)
    {
        if (rows == null || rows.Count <= 0)
            return;

        UISection sec = GetOrCreateSection(ref sectionIdx);
        sec.gameObject.name = "UISection_Result";
        sec.SetTitle(title);
        if (vertical)
            sec.SetRowsVertical(rows);
        else
            sec.SetRows(rows);
    }

    private void BuildPvpOpponentSection(List<(string label, string value)> rows, ref int sectionIdx)
    {
        if (rows == null || rows.Count <= 0)
            return;

        UISection sec = GetOrCreateSection(ref sectionIdx);
        sec.gameObject.name = "UISection_Status";
        sec.SetTitle("STATUS");
        sec.SetRows(rows, CommonUtility.PaletteColor("General.Bright1"));
    }

    private void BuildRefundSection(int refundAmount, ref int sectionIdx)
    {
        if (refundAmount <= 0)
            return;

        UISection sec = GetOrCreateSection(ref sectionIdx);
        sec.gameObject.name = "UISection_Refund";
        sec.SetTitle("REFUND");
        sec.HideAllRows();
        sec.SetRow(0, "mineral_amount", CommonUtility.PaletteColor("Mineral"), CommonUtility.FormatBigNumber(refundAmount));
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
        int rowIdx = 0;
        for (int i = 0; i < amounts.Count; i++)
        {
            if (amounts[i] > 0)
            {
                string text = CommonUtility.FormatBigNumber(amounts[i]);
                if (i == 0 && mineralVipMultiplier > 0)
                    text = $"{text} × {mineralVipMultiplier}(VIP)";
                sec.SetRow(rowIdx, GetRewardLabelKey(i), GetRewardColor(i), text);
                rowIdx++;
            }
        }
    }

    // rewardAmounts 규약: index0=exp, index1=explorationPoint, index2=pvpPoint(미네랄/모듈포인트는 이 팝업에서 제거됨)
    private static string GetRewardLabelKey(int rewardIndex)
    {
        if (rewardIndex == 0) return "UIPopupConfirm_ExpLabel";
        if (rewardIndex == 1) return "UIPanelExplorationGrid_OwnedPoint";
        if (rewardIndex == 2) return "UIPopupConfirm_PvpPointLabel";
        return "mineral_amount";
    }

    private static Color GetRewardColor(int rewardIndex)
    {
        if (rewardIndex == 0) return CommonUtility.PaletteColor("Commander");
        if (rewardIndex == 1) return CommonUtility.PaletteColor("General.Bright1"); // 탐험 포인트 전용 팔레트 키가 아직 없어 기본색 사용
        if (rewardIndex == 2) return CommonUtility.PaletteColor("PvpPoint");
        return CommonUtility.PaletteColor("General.Bright1");
    }

    private void BuildButtonSection(ConfirmPopupConfig config)
    {
        var loc = LocalizationManager.Instance;

        bool showCancel = config.onCancel != null;
        if (cancelButton != null) cancelButton.gameObject.SetActive(showCancel);
        if (showCancel)
            if (m_cancelText1 != null) m_cancelText1.text = config.cancelText1 ?? loc.Get("Simple_Cancel");

        if (m_confirmText1 != null) m_confirmText1.text = config.confirmText1 ?? loc.Get("Simple_Confirm");

        bool showExtra = config.onExtra != null;
        if (m_extraButton != null) m_extraButton.gameObject.SetActive(showExtra);
        if (showExtra && m_extraText1 != null) m_extraText1.text = config.extraText1 ?? "";
    }

    private void RebuildLayout()
    {
        for (int i = 0; i < m_sectionCache.Count; i++)
        {
            if (m_sectionCache[i].gameObject.activeSelf == true)
                m_sectionCache[i].RebuildLayout();
        }
        if (m_ownedPointRow != null && m_ownedPointRow.gameObject.activeSelf == true)
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_ownedPointRow.transform as RectTransform);
        if (m_sectionsRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_sectionsRoot);
        if (cancelButton != null) LayoutRebuilder.ForceRebuildLayoutImmediate(cancelButton.GetComponent<RectTransform>());
        if (confirmButton != null) LayoutRebuilder.ForceRebuildLayoutImmediate(confirmButton.GetComponent<RectTransform>());
        if (m_extraButton != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_extraButton.GetComponent<RectTransform>());
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
        if (costType == ECostType.PvpPoint) return CommonUtility.PaletteColor("PvpPoint");
        // 업적포인트 전용 팔레트 키가 아직 없어 기본색 사용
        if (costType == ECostType.AchievementPoint) return CommonUtility.PaletteColor("General.Bright1");
        return Color.white;
    }
}
