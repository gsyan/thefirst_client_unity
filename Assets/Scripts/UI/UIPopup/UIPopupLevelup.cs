// 다단계 레벨업 팝업 — 모듈/기술레벨 공용. < > 버튼으로 목표 레벨 선택, 누적 비용 표시
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
    [SerializeField] private TMP_Text m_bodyText;
    [SerializeField] private RectTransform m_resultContainer;
    [SerializeField] private RectTransform m_results;
    [SerializeField] private GameObject m_result1;
    [SerializeField] private GameObject m_result2;
    private RowImageText[] m_result1Rows;
    private RowImageText[] m_result2Rows;
    [SerializeField] private Transform m_costContainer;
    private RowImageText[] m_costRows;


    [Header("하단 버튼")]
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private Button m_cancelButton;

    private enum Mode { Module, TechLevel }
    private Mode m_mode;

    // 모듈 전용 상태
    private EModuleSubType m_subType;
    private EModuleType    m_moduleType;

    // 공통 상태
    private int         m_currentLevel;
    private int         m_minTargetLevel;
    private int         m_maxDataLevel;
    private int         m_targetLevel;
    private Action<int> m_onConfirm;
    private Action      m_onCancel;

    protected override void Awake()
    {
        base.Awake();
        if (m_result1 != null)
            m_result1Rows = m_result1.GetComponentsInChildren<RowImageText>(true);
        if (m_result2 != null)
            m_result2Rows = m_result2.GetComponentsInChildren<RowImageText>(true);
        if (m_costContainer != null)
            m_costRows = m_costContainer.GetComponentsInChildren<RowImageText>(true);
        m_prevButton?.onClick.AddListener(OnPrevClicked);
        m_nextButton?.onClick.AddListener(OnNextClicked);
        m_confirmButton?.onClick.AddListener(OnConfirmClicked);
        m_cancelButton?.onClick.AddListener(OnCancelClicked);
    }

    // ─────────────────────────────────────────────
    // Show 진입점
    // ─────────────────────────────────────────────

    public void ShowModule(EModuleSubType subType, EModuleType moduleType, int currentLevel, Action<int> onConfirm, Action onCancel = null)
    {
        m_mode       = Mode.Module;
        m_subType    = subType;
        m_moduleType = moduleType;

        if (m_titleText != null)
            m_titleText.text = LocalizationManager.Instance.Get("ship_module_levelup");
        if (m_subjectNameText != null)
            m_subjectNameText.text = $"<color=#009682>──────────────────────</color>\n{subType.GetLocalizedName()}\n<color=#009682>──────────────────────</color>";

        ShowInternal(currentLevel, onConfirm, onCancel);
    }

    public void ShowTechLevel(int currentTechLevel, Action<int> onConfirm, Action onCancel = null)
    {
        m_mode = Mode.TechLevel;

        if (m_titleText != null)
            m_titleText.text = LocalizationManager.Instance.Get("tech_level_levelup");
        if (m_subjectNameText != null)
            m_subjectNameText.text = $"<color=#009682>───────────────────────────</color>";

        ShowInternal(currentTechLevel, onConfirm, onCancel);
    }

    private void ShowInternal(int currentLevel, Action<int> onConfirm, Action onCancel)
    {
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
        if (m_targetLevel <= m_minTargetLevel) return;
        m_targetLevel--;
        UpdateDisplay();
    }

    private void OnNextClicked()
    {
        if (m_targetLevel >= m_maxDataLevel) return;
        m_targetLevel++;
        UpdateDisplay();
    }

    private void OnConfirmClicked()
    {
        m_onConfirm?.Invoke(m_targetLevel);
        HidePopup();
    }

    private void OnCancelClicked()
    {
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
            string bodyStr = BuildBodyText();
            m_bodyText.text = bodyStr;
            m_bodyText.gameObject.SetActive(string.IsNullOrEmpty(bodyStr) == false);
        }

        UpdateResultRows();
        UpdateCostRows(totalCost);

        if (m_prevButton != null)
            m_prevButton.interactable = m_targetLevel > m_minTargetLevel;
        if (m_nextButton != null)
            m_nextButton.interactable = m_targetLevel < m_maxDataLevel;

        if (m_confirmButton != null)
            m_confirmButton.interactable = canAfford;

        RebuildLayout();
    }

    // ContentSizeFitter 중첩 구조이므로 안쪽(Result1/2) → 바깥쪽(Panel) 순서로 재빌드
    private void RebuildLayout()
    {
        if (m_result1 != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_result1.GetComponent<RectTransform>());
        if (m_result2 != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_result2.GetComponent<RectTransform>());
        if (m_results != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_results);
        if (m_resultContainer != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_resultContainer);
        if (m_layoutRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(m_layoutRoot);
    }

    private string BuildBodyText()
    {
        return string.Empty;
    }

    private bool CheckCanAfford(long pointCost)
    {
        var info = DataManager.Instance.m_currentCharacter?.m_characterInfo;
        if (info == null) return false;
        if (m_mode == Mode.Module)
            return info.modulePoint >= pointCost;
        return info.techPoint >= pointCost;
    }

    private void UpdateResultRows()
    {
        int r1Count = m_result1Rows != null ? m_result1Rows.Length : 0;
        int r2Count = m_result2Rows != null ? m_result2Rows.Length : 0;

        for (int i = 0; i < r1Count; i++) m_result1Rows[i].Hide();
        for (int i = 0; i < r2Count; i++) m_result2Rows[i].Hide();

        if (m_mode == Mode.TechLevel)
        {
            if (r1Count > 0)
            {
                int currentShips = DataManager.Instance.m_dataTableResearch.GetShipCount(m_currentLevel);
                int targetShips  = DataManager.Instance.m_dataTableResearch.GetShipCount(m_targetLevel);
                m_result1Rows[0].SetRow("icon_ship", $"{currentShips} <voffset=6>→</voffset> {targetShips}");
            }
            if (m_result2 != null) m_result2.SetActive(false);
        }
        else if (m_mode == Mode.Module)
        {
            var statRows = CommonUtility.GetModuleStatRows(m_moduleType, m_subType, m_currentLevel, m_targetLevel);
            if (statRows == null) return;

            for (int i = 0; i < statRows.Count; i++)
            {
                if (i < r1Count)
                    m_result1Rows[i].SetRow(statRows[i].icon, statRows[i].value);
                else if (i - r1Count < r2Count)
                    m_result2Rows[i - r1Count].SetRow(statRows[i].icon, statRows[i].value);
            }

            bool needResult2 = statRows.Count > r1Count;
            if (m_result2 != null) m_result2.SetActive(needResult2);
        }
    }

    private void UpdateCostRows(long totalCost)
    {
        if (m_costRows == null) return;
        for (int i = 0; i < m_costRows.Length; i++)
            m_costRows[i].Hide();

        if (totalCost <= 0) return;

        var characterInfo = DataManager.Instance.m_currentCharacter?.m_characterInfo;
        int rowIndex;
        bool canAfford;

        if (m_mode == Mode.TechLevel)
        {
            rowIndex  = (int)ECostType.TechPoint;
            canAfford = characterInfo != null && characterInfo.techPoint >= totalCost;
        }
        else if (m_mode == Mode.Module)
        {
            rowIndex  = (int)ECostType.ModulePoint;
            canAfford = characterInfo != null && characterInfo.modulePoint >= totalCost;
        }
        else return;

        if (rowIndex < m_costRows.Length)
            m_costRows[rowIndex].SetTextRowImageText(canAfford ? $"{totalCost}" : $"<color=red>{totalCost}</color>");
    }

    // ─────────────────────────────────────────────
    // 계산 헬퍼
    // ─────────────────────────────────────────────

    private int CalculateMaxDataLevel(int fromLevel)
    {
        if (m_mode == Mode.Module)
        {
            int level = fromLevel;
            while (DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_subType, level + 1) != null)
                level++;
            return level;
        }
        else
        {
            int max = fromLevel;
            var list = DataManager.Instance.m_dataTableResearch.TechLevelDataList;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].targetTechLevel > max)
                    max = list[i].targetTechLevel;
            }
            return max;
        }
    }

    private long CalculateCumulativeCost(int fromLevel, int toLevel)
    {
        long total = 0;
        if (m_mode == Mode.Module)
        {
            for (int lv = fromLevel; lv < toLevel; lv++)
            {
                if (DataManager.Instance.GetModuleLevelUpCost(m_subType, lv, out long c) == false) break;
                total += c;
            }
        }
        else
        {
            for (int lv = fromLevel; lv < toLevel; lv++)
                total += DataManager.Instance.m_dataTableResearch.GetTechLevelUpgradeCost(lv);
        }
        return total;
    }
}
