// 다단계 레벨업 팝업 — 모듈/기술레벨 공용. < > 버튼으로 목표 레벨 선택, 누적 비용 표시
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupLevelup : UIPopupBase
{
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
    [SerializeField] private RectTransform m_layoutRoot;

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
            m_subjectNameText.text = subType.GetLocalizedName() + "\n<color=#666666>─────────────</color>";

        ShowInternal(currentLevel, onConfirm, onCancel);
    }

    public void ShowTechLevel(int currentTechLevel, Action<int> onConfirm, Action onCancel = null)
    {
        m_mode = Mode.TechLevel;

        if (m_titleText != null)
            m_titleText.text = LocalizationManager.Instance.Get("tech_level_levelup");
        if (m_subjectNameText != null)
            m_subjectNameText.text = $"{CommonUtility.Sprite("gears")} Tech Level\n<color=#666666>─────────────</color>";

        ShowInternal(currentTechLevel, onConfirm, onCancel);
    }

    private void ShowInternal(int currentLevel, Action<int> onConfirm, Action onCancel)
    {
        m_currentLevel   = currentLevel;
        m_minTargetLevel = currentLevel + 1;
        m_targetLevel    = currentLevel + 1;
        m_maxDataLevel   = CalculateMaxDataLevel(currentLevel);
        m_onConfirm      = onConfirm;
        m_onCancel       = onCancel;

        UpdateDisplay();
        base.ShowPopup();
        if (m_layoutRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_layoutRoot);
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
        if (m_levelFromText != null) m_levelFromText.text = m_currentLevel.ToString();
        if (m_levelToText   != null) m_levelToText.text   = m_targetLevel.ToString();

        CostStruct total = CalculateCumulativeCost(m_currentLevel, m_targetLevel);
        bool canAfford   = CheckCanAfford(total);

        if (m_bodyText != null)
            m_bodyText.text = BuildBodyText(total);

        if (m_prevButton != null)
            m_prevButton.interactable = m_targetLevel > m_minTargetLevel;
        if (m_nextButton != null)
            m_nextButton.interactable = m_targetLevel < m_maxDataLevel;

        if (m_confirmButton != null)
            m_confirmButton.interactable = canAfford;

        if (m_layoutRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_layoutRoot);
    }

    private string BuildBodyText(CostStruct cost)
    {
        const string SEPARATOR = "\n\n<color=#666666>─────────────</color>\n\n";
        var sb = new StringBuilder();

        if (m_mode == Mode.Module)
        {
            // 스탯 비교 (레벨 헤더 첫 줄 제거)
            string full = CommonUtility.GetModuleDetailText(m_moduleType, m_subType, m_currentLevel, m_targetLevel);
            if (string.IsNullOrEmpty(full) == false)
            {
                int sep = full.IndexOf("\n\n", StringComparison.Ordinal);
                string statsOnly = sep >= 0 ? full.Substring(sep + 2) : full;
                sb.Append(statsOnly);
            }
        }
        else
        {
            // 기술레벨: 해금 정보 (현재 → 목표)
            int currentCap  = 3 + (m_currentLevel / 2);
            int targetCap   = 3 + (m_targetLevel  / 2);
            int currentShips = DataManager.Instance.m_dataTableConfig.gameSettings.GetMaxShipsAtTechLevel(m_currentLevel);
            int targetShips  = DataManager.Instance.m_dataTableConfig.gameSettings.GetMaxShipsAtTechLevel(m_targetLevel);

            sb.Append($"{CommonUtility.Sprite("clockwork")}  {currentCap}h → {targetCap}h\n\n");
            sb.Append($"{CommonUtility.Sprite("spiky-field")} {currentShips} → {targetShips}");
        }

        // 비용 (공통)
        if (cost != null)
        {
            var info = DataManager.Instance.m_currentCharacter?.m_characterInfo;
            string C(bool ins, string val) => ins ? $"<color=red>{val}</color>" : val;

            var costSb = new StringBuilder();
            if (cost.mineral > 0)
            {
                bool ins = info != null && info.mineral < cost.mineral;
                costSb.Append($"{CommonUtility.Sprite("crystal-growth")} {C(ins, CommonUtility.FormatBigNumber(cost.mineral))}\n\n");
            }
            if (cost.mineralRare > 0)
            {
                bool ins = info != null && info.mineralRare < cost.mineralRare;
                costSb.Append($"{CommonUtility.Sprite("minerals")} {C(ins, CommonUtility.FormatBigNumber(cost.mineralRare))}\n\n");
            }
            if (cost.mineralExotic > 0)
            {
                bool ins = info != null && info.mineralExotic < cost.mineralExotic;
                costSb.Append($"{CommonUtility.Sprite("emerald")} {C(ins, CommonUtility.FormatBigNumber(cost.mineralExotic))}\n\n");
            }
            if (cost.mineralDark > 0)
            {
                bool ins = info != null && info.mineralDark < cost.mineralDark;
                costSb.Append($"{CommonUtility.Sprite("fire-gem")} {C(ins, CommonUtility.FormatBigNumber(cost.mineralDark))}\n\n");
            }

            string costStr = costSb.ToString().TrimEnd();
            if (string.IsNullOrEmpty(costStr) == false)
            {
                if (sb.Length > 0) sb.Append(SEPARATOR);
                sb.Append(costStr);
            }
        }

        return sb.ToString();
    }

    private bool CheckCanAfford(CostStruct cost)
    {
        if (cost == null) return true;
        var info = DataManager.Instance.m_currentCharacter?.m_characterInfo;
        if (info == null) return false;

        return info.mineral       >= cost.mineral
            && info.mineralRare   >= cost.mineralRare
            && info.mineralExotic >= cost.mineralExotic
            && info.mineralDark   >= cost.mineralDark;
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

    private CostStruct CalculateCumulativeCost(int fromLevel, int toLevel)
    {
        var total = new CostStruct();
        if (m_mode == Mode.Module)
        {
            for (int lv = fromLevel; lv < toLevel; lv++)
            {
                if (DataManager.Instance.GetModuleUpgradeCost(m_subType, lv, out CostStruct c) == false) break;
                total.mineral       += c.mineral;
                total.mineralRare   += c.mineralRare;
                total.mineralExotic += c.mineralExotic;
                total.mineralDark   += c.mineralDark;
            }
        }
        else
        {
            for (int lv = fromLevel; lv < toLevel; lv++)
            {
                CostStruct c = DataManager.Instance.m_dataTableResearch.GetTechLevelUpgradeCost(lv);
                total.mineral       += c.mineral;
                total.mineralRare   += c.mineralRare;
                total.mineralExotic += c.mineralExotic;
                total.mineralDark   += c.mineralDark;
            }
        }
        return total;
    }
}
