// 함선 로드아웃 편집 화면(UIShipLoadoutEditorView)의 슬롯 1개에 대한 강화 포인트 편집 팝업
// Confirm 전까지는 이 팝업 내부의 로컬 버퍼(m_localAttackPoints/m_localAttackToFighterPoints)만 바뀌고,
// 호출부(UIShipLoadoutEditorView)의 pending 상태는 Confirm을 눌러야 반영됨(Cancel이면 로컬 버퍼 폐기, pending 변경 없음)
// UI는 카테고리의 모든 강화 가능 축을 다 나열하되, 실제로 값이 반영되어 작동하는 항목은 공격력 계열(isEditable=true)뿐
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupModuleReinforce : UIPopupBase
{
    [SerializeField] private UIReinforceStatRow m_rowPrefab;
    [SerializeField] private InfiniteScrollView m_scrollView;
    [SerializeField] private RowLabelValue m_commandPowerRow; // 잔여 지휘력 표시 — UIShipLoadoutEditorView.RefreshCommandPowerPreview와 동일한 표시 방식
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private Button m_cancelButton;

    private struct ReinforceEntry
    {
        public string label;
        public int currentValue;
        public bool isEditable; // 공격력 계열만 true — 나머지는 Up/Down 항상 비활성화
    }

    private EModuleType m_moduleType;
    private int m_localAttackPoints;          // 편집 로컬 버퍼 — Confirm 전까지 외부에 반영 안 됨
    private int m_localAttackToFighterPoints;
    private int m_maxCommandPower;
    private int m_usedByOtherSlots;           // 이 슬롯을 제외한 다른 슬롯들의 지휘력 사용량 — 잔여 지휘력 계산용
    private int m_otherFieldsCostInThisSlot;  // 이 슬롯의 설치비 + 강화 대상 외 필드 비용(현재는 항상 0, 향후 확장 대비)
    private System.Action m_onClose;
    private System.Action<int, int> m_onConfirm; // (attackPoints, attackToFighterPoints)

    private readonly List<ReinforceEntry> m_entries = new();

    protected override void Awake()
    {
        base.Awake();
        if (m_confirmButton != null) m_confirmButton.onClick.AddListener(OnConfirmClicked);
        if (m_cancelButton != null) m_cancelButton.onClick.AddListener(OnCancelClicked);
        if (m_scrollView != null) m_scrollView.onItemBind = OnItemBind;
    }

    // usedByOtherSlots: 이 슬롯을 제외한 다른 슬롯들의 지휘력 사용량(설치비+강화포인트 포함, 호출부가 FleetComposition 기준으로 계산해 넘김)
    // installCost: 이 슬롯의 설치비(바디 비용 제외, 이 모듈 자체의 statPoint) — 강화 포인트와 별개로 잔여 지휘력 계산에 필요
    public void ShowPopupModuleReinforce(EModuleType moduleType, int initialAttackPoints, int initialAttackToFighterPoints,
        int maxCommandPower, int usedByOtherSlots, int installCost, System.Action onClose, System.Action<int, int> onConfirm)
    {
        m_moduleType = moduleType;
        m_localAttackPoints = initialAttackPoints;
        m_localAttackToFighterPoints = initialAttackToFighterPoints;
        m_maxCommandPower = maxCommandPower;
        m_usedByOtherSlots = usedByOtherSlots;
        m_otherFieldsCostInThisSlot = installCost;
        m_onClose = onClose;
        m_onConfirm = onConfirm;

        RefreshEntries();
        ShowPopup();
    }

    private void RefreshEntries()
    {
        m_entries.Clear();
        if (m_moduleType == EModuleType.beam)
        {
            m_entries.Add(new ReinforceEntry { label = "Attack", currentValue = m_localAttackPoints, isEditable = true });
            m_entries.Add(new ReinforceEntry { label = "Fire Rate", currentValue = 0, isEditable = false });
            m_entries.Add(new ReinforceEntry { label = "Projectile Speed", currentValue = 0, isEditable = false });
        }
        else if (m_moduleType == EModuleType.missile)
        {
            m_entries.Add(new ReinforceEntry { label = "Attack", currentValue = m_localAttackPoints, isEditable = true });
            m_entries.Add(new ReinforceEntry { label = "Fire Rate", currentValue = 0, isEditable = false });
            m_entries.Add(new ReinforceEntry { label = "Projectile Speed", currentValue = 0, isEditable = false });
            m_entries.Add(new ReinforceEntry { label = "Silence Time", currentValue = 0, isEditable = false });
        }
        else if (m_moduleType == EModuleType.hangar)
        {
            m_entries.Add(new ReinforceEntry { label = "Attack To Ship", currentValue = m_localAttackPoints, isEditable = true });
            m_entries.Add(new ReinforceEntry { label = "Attack To Fighter", currentValue = m_localAttackToFighterPoints, isEditable = true });
            m_entries.Add(new ReinforceEntry { label = "Ammo", currentValue = 0, isEditable = false });
            m_entries.Add(new ReinforceEntry { label = "Health", currentValue = 0, isEditable = false });
        }

        RefreshCommandPowerPreview();
        if (m_scrollView != null && m_rowPrefab != null)
            m_scrollView.Initialize(m_entries.Count, m_rowPrefab.gameObject);
    }

    private void OnItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_entries.Count) return;
        UIReinforceStatRow row = rowObject.GetComponent<UIReinforceStatRow>();
        if (row == null) return;

        ReinforceEntry entry = m_entries[dataIndex];
        int maxPerSlot = DataManager.Instance.m_dataTableConfig.gameSettings.shipStatFormula.maxAttackReinforcePointsPerSlot;
        bool isAtSlotCap = entry.currentValue >= maxPerSlot;
        bool hasRemainingCommandPower = GetRemainingCommandPower() > 0;
        bool canIncrease = isAtSlotCap == false && hasRemainingCommandPower == true;
        bool canDecrease = entry.currentValue > 0;

        row.Setup(dataIndex, entry.label, entry.currentValue, entry.isEditable, canIncrease, canDecrease, OnRowPointsChanged);
    }

    private void OnRowPointsChanged(int dataIndex, int delta)
    {
        if (dataIndex < 0 || dataIndex >= m_entries.Count) return;

        int maxPerSlot = DataManager.Instance.m_dataTableConfig.gameSettings.shipStatFormula.maxAttackReinforcePointsPerSlot;
        ReinforceEntry entry = m_entries[dataIndex];
        int rawValue = entry.currentValue + delta;
        int clampedValue = rawValue < 0 ? 0 : rawValue;
        if (clampedValue > maxPerSlot) clampedValue = maxPerSlot;

        entry.currentValue = clampedValue;
        m_entries[dataIndex] = entry;

        bool isFighterSlot = m_moduleType == EModuleType.hangar && entry.label == "Attack To Fighter";
        if (isFighterSlot == true) m_localAttackToFighterPoints = clampedValue;
        else m_localAttackPoints = clampedValue;

        RefreshCommandPowerPreview();
        if (m_scrollView != null) m_scrollView.RefreshVisible();
    }

    // 잔여 지휘력 = 최대 지휘력 - (다른 슬롯 사용량 + 이 슬롯의 설치비 + 이 슬롯의 강화 포인트 합)
    private int GetRemainingCommandPower()
    {
        int thisSlotReinforceCost = m_localAttackPoints + m_localAttackToFighterPoints;
        int thisSlotCost = m_otherFieldsCostInThisSlot + thisSlotReinforceCost;
        return m_maxCommandPower - (m_usedByOtherSlots + thisSlotCost);
    }

    private void RefreshCommandPowerPreview()
    {
        if (m_commandPowerRow == null) return;

        int remaining = GetRemainingCommandPower();
        bool isOverCommandPower = remaining < 0;

        m_commandPowerRow.SetRow("UITabCommander_CommandPower", $"{m_maxCommandPower - remaining} / {m_maxCommandPower}", rawValue: true);
        m_commandPowerRow.SetValueColor(CommonUtility.PaletteColor(isOverCommandPower == true ? "Text.Warning" : "Text.Dark1"));
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_commandPowerRow.transform as RectTransform);
    }

    private void OnConfirmClicked()
    {
        if (m_onConfirm != null) m_onConfirm(m_localAttackPoints, m_localAttackToFighterPoints);
        if (m_onClose != null) m_onClose.Invoke();
    }

    private void OnCancelClicked()
    {
        if (m_onClose != null) m_onClose.Invoke();
    }
}
