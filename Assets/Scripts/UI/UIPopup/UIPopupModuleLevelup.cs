// 모듈 레벨업 전용 팝업 — < > 버튼으로 목표 레벨 선택, 누적 비용 표시
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupModuleLevelup : UIPopupBase
{
    [Header("타이틀 / 모듈명")]
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private TMP_Text m_moduleNameText;

    [Header("레벨 선택 행")]
    [SerializeField] private TMP_Text m_levelFromText;
    [SerializeField] private TMP_Text m_levelToText;
    [SerializeField] private Button   m_prevButton;
    [SerializeField] private Button   m_nextButton;

    [Header("스탯 + 비용 (단일 텍스트)")]
    [SerializeField] private TMP_Text m_bodyText;

    [Header("하단 버튼")]
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private Button m_cancelButton;

    // 팝업 내부 상태
    private EModuleSubType  m_subType;
    private EModuleType     m_moduleType;
    private int             m_currentLevel;
    private int             m_minTargetLevel;
    private int             m_maxDataLevel;
    private int             m_targetLevel;
    private Action<int>     m_onConfirm;
    private Action          m_onCancel;

    protected override void Awake()
    {
        base.Awake();
        m_prevButton?.onClick.AddListener(OnPrevClicked);
        m_nextButton?.onClick.AddListener(OnNextClicked);
        m_confirmButton?.onClick.AddListener(OnConfirmClicked);
        m_cancelButton?.onClick.AddListener(OnCancelClicked);
    }

    public void Show(EModuleSubType subType, EModuleType moduleType, int currentLevel, Action<int> onConfirm, Action onCancel = null)
    {
        m_subType      = subType;
        m_moduleType   = moduleType;
        m_currentLevel = currentLevel;
        m_onConfirm    = onConfirm;
        m_onCancel     = onCancel;

        m_minTargetLevel     = currentLevel + 1;
        m_targetLevel        = currentLevel + 1;
        m_maxDataLevel       = CalculateMaxDataLevel(subType, currentLevel);

        if (m_titleText != null)
            m_titleText.text = LocalizationManager.Instance.Get("ship_module_levelup");

        if (m_moduleNameText != null)
            m_moduleNameText.text = LocalizationManager.Instance.Get($"{subType.ToLocKey()}") + "\n<color=#666666>─────────────</color>";
            

        UpdateDisplay();
        base.ShowPopup();
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

        // < > 버튼 상태 — 데이터 범위 기준, 자원 부족은 Confirm만 막음
        if (m_prevButton != null)
            m_prevButton.interactable = m_targetLevel > m_minTargetLevel;
        if (m_nextButton != null)
            m_nextButton.interactable = m_targetLevel < m_maxDataLevel;

        // Confirm 버튼 상태
        if (m_confirmButton != null)
            m_confirmButton.interactable = canAfford;
    }

    // 스탯 비교 + 구분선 + 누적 비용을 하나의 텍스트로 조합
    private string BuildBodyText(CostStruct cost)
    {
        const string SEPARATOR = "\n\n<color=#666666>─────────────</color>\n\n";

        var sb = new StringBuilder();

        // 스탯 비교 (CommonUtility 반환값의 첫 줄(레벨 헤더)은 레벨 행 UI와 중복되므로 제거)
        string full = CommonUtility.GetModuleDetailText(m_moduleType, m_subType, m_currentLevel, m_targetLevel);
        if (string.IsNullOrEmpty(full) == false)
        {
            int sep = full.IndexOf("\n\n", StringComparison.Ordinal);
            string statsOnly = sep >= 0 ? full.Substring(sep + 2) : full;
            sb.Append(statsOnly);
        }

        // 비용
        if (cost != null)
        {
            var info = DataManager.Instance.m_currentCharacter?.m_characterInfo;
            string C(bool ins, string val) => ins ? $"<color=red>{val}</color>" : val;

            var costSb = new StringBuilder();
            if (cost.mineral > 0)
            {
                bool ins = info != null && info.mineral < cost.mineral;
                costSb.Append($"<sprite name=\"IconMineral\"> {C(ins, CommonUtility.FormatBigNumber(cost.mineral))}\n\n");
            }
            if (cost.mineralRare > 0)
            {
                bool ins = info != null && info.mineralRare < cost.mineralRare;
                costSb.Append($"<sprite name=\"IconMineralR\"> {C(ins, CommonUtility.FormatBigNumber(cost.mineralRare))}\n\n");
            }
            if (cost.mineralExotic > 0)
            {
                bool ins = info != null && info.mineralExotic < cost.mineralExotic;
                costSb.Append($"<sprite name=\"IconMineralE\"> {C(ins, CommonUtility.FormatBigNumber(cost.mineralExotic))}\n\n");
            }
            if (cost.mineralDark > 0)
            {
                bool ins = info != null && info.mineralDark < cost.mineralDark;
                costSb.Append($"<sprite name=\"IconMineralD\"> {C(ins, CommonUtility.FormatBigNumber(cost.mineralDark))}\n\n");
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

    // DataTable 상 올라갈 수 있는 최대 레벨 반환
    private int CalculateMaxDataLevel(EModuleSubType subType, int fromLevel)
    {
        int level = fromLevel;
        while (DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, level + 1) != null)
            level++;
        return level;
    }

    // 현재 자원으로 올라갈 수 있는 최대 레벨 반환 (기술레벨 포함)
    // currentLevel → targetLevel 누적 비용 합산
    private CostStruct CalculateCumulativeCost(int fromLevel, int toLevel)
    {
        var total = new CostStruct();
        for (int lv = fromLevel; lv < toLevel; lv++)
        {
            if (DataManager.Instance.GetModuleUpgradeCost(m_subType, lv, out CostStruct c) == false) break;
            total.mineral       += c.mineral;
            total.mineralRare   += c.mineralRare;
            total.mineralExotic += c.mineralExotic;
            total.mineralDark   += c.mineralDark;
        }
        return total;
    }
}
