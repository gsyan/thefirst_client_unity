// 모듈 레벨업 팝업 — < > 버튼으로 목표 레벨 선택, 누적 비용 표시
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupLevelup : UIPopupBase
{
    [SerializeField] private RectTransform m_layoutRoot;

    [Header("타이틀 / 주제명")]
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private TMP_Text m_subjectNameText;

    [Header("레벨 선택 행")]
    [SerializeField] private TMP_Text m_levelFromText;
    [SerializeField] private TMP_Text m_levelToText;
    [SerializeField] private Button   m_prevButton;
    [SerializeField] private Button   m_nextButton;

    [Header("스탯 + 비용")]
    [SerializeField] private TMP_Text  m_bodyText;
    [SerializeField] private UISection m_sectionResult;
    [SerializeField] private UISection m_sectionCost;

    [Header("하단 버튼")]
    [SerializeField] private UIButtonHasChildren m_confirmButton;
    [SerializeField] private Button              m_cancelButton;

    private EModuleSubType m_subType;
    private EModuleType    m_moduleType;

    private int         m_currentLevel;
    private int         m_minTargetLevel;
    private int         m_maxDataLevel;
    private int         m_targetLevel;
    private Action<int> m_onConfirm;
    private Action      m_onCancel;

    protected override void Awake()
    {
        base.Awake();
        m_prevButton?.onClick.AddListener(OnPrevClicked);
        m_nextButton?.onClick.AddListener(OnNextClicked);
        if (m_confirmButton != null) m_confirmButton.GetButton().onClick.AddListener(OnConfirmClicked);
        m_cancelButton?.onClick.AddListener(OnCancelClicked);
    }

    // ─────────────────────────────────────────────
    // Show 진입점
    // ─────────────────────────────────────────────

    public void ShowModule(EModuleSubType subType, EModuleType moduleType, int currentLevel, Action<int> onConfirm, Action onCancel = null)
    {
        m_subType    = subType;
        m_moduleType = moduleType;

        if (m_titleText != null)
            m_titleText.text = LocalizationManager.Instance.Get("ship_module_levelup");
        if (m_subjectNameText != null)
            m_subjectNameText.text = $"<color=#009682>──────────────────────</color>\n{subType.GetLocalizedName()}\n<color=#009682>──────────────────────</color>";

        base.ShowPopup();

        m_currentLevel   = currentLevel;
        m_minTargetLevel = currentLevel + 1;
        m_targetLevel    = currentLevel + 1;
        m_maxDataLevel   = CalculateMaxDataLevel(currentLevel);
        m_onConfirm      = onConfirm;
        m_onCancel       = onCancel;

        UpdateDisplay();
    }

    // ─────────────────────────────────────────────
    // 버튼 이벤트
    // ─────────────────────────────────────────────

    private void OnPrevClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_targetLevel <= m_minTargetLevel) return;
        m_targetLevel--;
        UpdateDisplay();
    }

    private void OnNextClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_targetLevel >= m_maxDataLevel) return;
        m_targetLevel++;
        UpdateDisplay();
    }

    private void OnConfirmClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        m_onConfirm?.Invoke(m_targetLevel);
        HidePopup();
    }

    private void OnCancelClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        m_onCancel?.Invoke();
    }

    // ─────────────────────────────────────────────
    // 디스플레이 갱신
    // ─────────────────────────────────────────────

    private void UpdateDisplay()
    {
        if (m_levelFromText != null) m_levelFromText.text = $"Lv.{m_currentLevel}";
        if (m_levelToText   != null) m_levelToText.text   = $"Lv.{m_targetLevel}";
        long totalCost = CalculateCumulativeCost(m_currentLevel, m_targetLevel);

        bool canAfford = CheckCanAfford(totalCost);

        if (m_bodyText != null)
        {
            m_bodyText.text = string.Empty;
            m_bodyText.gameObject.SetActive(false);
        }

        UpdateResultRows();
        UpdateCostRows(totalCost);

        if (m_prevButton != null)
            m_prevButton.interactable = m_targetLevel > m_minTargetLevel;
        if (m_nextButton != null)
            m_nextButton.interactable = m_targetLevel < m_maxDataLevel;

        if (m_confirmButton != null)
            m_confirmButton.SetInteractable(canAfford);

        RebuildLayout();
    }

    private void RebuildLayout()
    {
        if (m_sectionResult != null) m_sectionResult.RebuildLayout();
        if (m_sectionCost != null) m_sectionCost.RebuildLayout();
        if (m_layoutRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_layoutRoot);
    }

    private bool CheckCanAfford(long pointCost)
    {
        Commander currentCommander = DataManager.Instance.m_currentCommander;
        CommanderInfo info = (currentCommander != null) ? currentCommander.m_commanderInfo : null;
        if (info == null) return false;
        return info.modulePoint >= pointCost;
    }

    private void UpdateResultRows()
    {
        if (m_sectionResult == null) return;
        m_sectionResult.HideAllRows();

        var statRows = CommonUtility.GetModuleStatRows(m_moduleType, m_subType, m_currentLevel, m_targetLevel);
        if (statRows == null) return;
        m_sectionResult.SetRows(statRows);
    }

    private void UpdateCostRows(long totalCost)
    {
        if (m_sectionCost == null) return;
        m_sectionCost.HideAllRows();
        if (totalCost <= 0) return;

        Commander currentCommander = DataManager.Instance.m_currentCommander;
        CommanderInfo commanderInfo = (currentCommander != null) ? currentCommander.m_commanderInfo : null;
        int rowIndex = (int)ECostType.ModulePoint;
        bool canAfford = commanderInfo != null && commanderInfo.modulePoint >= totalCost;
        m_sectionCost.SetRowText(rowIndex, canAfford ? $"{totalCost}" : $"<color=red>{totalCost}</color>");
    }

    // ─────────────────────────────────────────────
    // 계산 헬퍼
    // ─────────────────────────────────────────────

    private int CalculateMaxDataLevel(int fromLevel)
    {
        int level = fromLevel;
        while (DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_subType, level + 1) != null)
            level++;
        return level;
    }

    private long CalculateCumulativeCost(int fromLevel, int toLevel)
    {
        long total = 0;
        for (int lv = fromLevel; lv < toLevel; lv++)
        {
            if (DataManager.Instance.GetModuleLevelUpCost(m_subType, lv, out int c) == false) break;
            total += c;
        }
        return total;
    }
}
