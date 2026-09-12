// 함선 로드아웃 편집 화면(UIShipLoadoutEditorView)의 슬롯 1개에 대한 강화 포인트 편집 팝업
// Confirm 전까지는 이 팝업 내부의 로컬 버퍼(m_localAttackPoints/m_localAttackToFighterPoints)만 바뀌고,
// 호출부(UIShipLoadoutEditorView)의 pending 상태는 Confirm을 눌러야 반영됨(Cancel이면 로컬 버퍼 폐기, pending 변경 없음)
// UI는 카테고리의 모든 강화 가능 축을 다 나열하되, 실제로 값이 반영되어 작동하는 항목은 공격력 계열(isEditable=true)뿐
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupModuleReinforce : UIPopupBase
{
    [SerializeField] private UIReinforceStatRow m_rowPrefab;
    [SerializeField] private InfiniteScrollView m_scrollView;
    [SerializeField] private RowLabelValue m_commandPowerRow; // 잔여 지휘력 표시 — UIShipLoadoutEditorView.RefreshCommandPowerPreview와 동일한 표시 방식
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private Button m_cancelButton;

    [Header("컬럼 헤더 — 고정 텍스트라 Awake에서 한 번만 로컬라이즈")]
    [SerializeField] private TMP_Text m_colStatusText;
    [SerializeField] private TMP_Text m_colValueText;
    [SerializeField] private TMP_Text m_colInvestedText;

    [Header("티어업/다운 — 무기 모듈 서브타입 자체를 한 티어 위/아래로 교체(강화 포인트와는 별개 축)")]
    [SerializeField] private Button m_tierDownButton;
    [SerializeField] private TMP_Text m_tierText;
    [SerializeField] private Button m_tierUpButton;
    [SerializeField] private TMP_Text m_tierCostText; // 티어업 시 추가로 드는 지휘력(+N CP) — 부족하면 빨간색

    private struct ReinforceEntry
    {
        public string label;
        public int currentValue;
        public bool isEditable; // 공격력 계열만 true — 나머지는 Up/Down 항상 비활성화
    }

    private EModuleType m_moduleType;
    private string m_localModuleSubType;      // 편집 로컬 버퍼 — 티어업/다운으로 바뀌는 이 슬롯의 서브타입(예: beam_1_1 → beam_2_1)
    private int m_localAttackPoints;          // 편집 로컬 버퍼 — Confirm 전까지 외부에 반영 안 됨
    private int m_localAttackToFighterPoints;
    private int m_maxCommandPower;
    private int m_usedByOtherSlots;           // 이 슬롯을 제외한 다른 슬롯들의 지휘력 사용량 — 잔여 지휘력 계산용
    private int m_otherFieldsCostInThisSlot;  // 이 슬롯의 설치비(현재 서브타입의 statPoint) — 티어업/다운 시 새 티어의 statPoint로 갱신됨
    private int m_hullTier;                   // 무기 티어 상한 — 함체 티어를 넘는 티어업은 불가(함체를 낮은 티어로 바꾸면 초과 모듈이 자동 다운그레이드되므로 상한도 항상 이 값과 일치해야 함)
    private System.Action m_onClose;
    private System.Action<string, int, int> m_onConfirm; // (moduleSubType, attackPoints, attackToFighterPoints)

    private readonly List<ReinforceEntry> m_entries = new();

    protected override void Awake()
    {
        base.Awake();
        if (m_confirmButton != null) m_confirmButton.onClick.AddListener(OnConfirmClicked);
        if (m_cancelButton != null) m_cancelButton.onClick.AddListener(OnCancelClicked);
        if (m_tierUpButton != null) m_tierUpButton.onClick.AddListener(() => OnTierClicked(+1));
        if (m_tierDownButton != null) m_tierDownButton.onClick.AddListener(() => OnTierClicked(-1));
        if (m_scrollView != null) m_scrollView.onItemBind = OnItemBind;

        CommonUtility.SetUILocText(m_colStatusText, "UIFleet_ModuleReinforce_ColStatus");
        CommonUtility.SetUILocText(m_colValueText, "UIFleet_ModuleReinforce_ColValue");
        CommonUtility.SetUILocText(m_colInvestedText, "UIFleet_ModuleReinforce_ColInvested");
    }

    // usedByOtherSlots: 이 슬롯을 제외한 다른 슬롯들의 지휘력 사용량(설치비+강화포인트 포함, 호출부가 FleetComposition 기준으로 계산해 넘김)
    // installCost: 이 슬롯의 설치비(바디 비용 제외, initialModuleSubType의 statPoint) — 강화 포인트와 별개로 잔여 지휘력 계산에 필요
    public void ShowPopupModuleReinforce(EModuleType moduleType, string initialModuleSubType, int initialAttackPoints, int initialAttackToFighterPoints,
        int maxCommandPower, int usedByOtherSlots, int installCost, int hullTier, System.Action onClose, System.Action<string, int, int> onConfirm)
    {
        m_moduleType = moduleType;
        m_localModuleSubType = initialModuleSubType;
        m_localAttackPoints = initialAttackPoints;
        m_localAttackToFighterPoints = initialAttackToFighterPoints;
        m_maxCommandPower = maxCommandPower;
        m_usedByOtherSlots = usedByOtherSlots;
        m_otherFieldsCostInThisSlot = installCost;
        m_hullTier = hullTier;
        m_onClose = onClose;
        m_onConfirm = onConfirm;

        m_tierText.text = m_localModuleSubType;

        RefreshEntries();
        RefreshTierButtons();
        ShowPopup();
    }

    // 카테고리 접두어({moduleType}) + (현재 티어 + tierDelta) + "_1"로 후보 서브타입을 조립해 데이터 존재 여부 확인 — 없으면 null
    // 티어업(tierDelta>0)은 함체 티어를 넘을 수 없음 — 함체를 낮은 티어로 바꾸면 초과 모듈이 자동 다운그레이드되는 규칙과 짝을 이룸
    private ModuleData FindAdjacentTierModuleData(int tierDelta)
    {
        int currentTier = CommonUtility.ParseTier(m_localModuleSubType);
        int candidateTier = currentTier + tierDelta;
        if (candidateTier < 1) return null;
        if (tierDelta > 0 && candidateTier > m_hullTier) return null;

        string candidateSubType = $"{m_moduleType}_{candidateTier}_1";
        return DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(candidateSubType);
    }

    // 티어다운은 statPoint가 항상 낮거나 같아(티어가 오를수록 가파르게 증가하는 설계) 잔여 지휘력 걱정 없이 항상 가능 —
    // 티어업만 잔여 지휘력(강화 포인트 포함)으로 감당 가능한지 확인
    private void RefreshTierButtons()
    {
        ModuleData downData = FindAdjacentTierModuleData(-1);
        ModuleData upData = FindAdjacentTierModuleData(+1);

        int remainingExcludingTierCost = m_maxCommandPower - (m_usedByOtherSlots + FleetComposition.k_reinforceCpCostPerPoint * (m_localAttackPoints + m_localAttackToFighterPoints));
        bool canAffordUp = upData != null && remainingExcludingTierCost - upData.statPoint >= 0;

        if (m_tierDownButton != null) m_tierDownButton.interactable = downData != null;
        if (m_tierUpButton != null) m_tierUpButton.interactable = canAffordUp;

        if (m_tierCostText != null)
        {
            if (upData == null)
            {
                m_tierCostText.text = "";
            }
            else
            {
                int upCostDelta = upData.statPoint - m_otherFieldsCostInThisSlot;
                m_tierCostText.text = $"+{upCostDelta} CP";
                m_tierCostText.color = CommonUtility.PaletteColor(canAffordUp == false ? "Text.Warning" : "Text.Dark1");
            }
        }
    }

    private void OnTierClicked(int tierDelta)
    {
        ModuleData candidate = FindAdjacentTierModuleData(tierDelta);
        if (candidate == null) return;

        m_localModuleSubType = candidate.moduleSubType;
        m_otherFieldsCostInThisSlot = candidate.statPoint;
        m_tierText.text = candidate.moduleSubType;

        RefreshCommandPowerPreview();
        RefreshTierButtons();
        if (m_scrollView != null) m_scrollView.RefreshVisible(); // 티어가 바뀌면 m_localModuleSubType 기준 실제 수치(StatValue)도 다시 그려야 함
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

        float actualValue = CalculateActualStatValue(entry);
        int cpSpent = entry.currentValue * FleetComposition.k_reinforceCpCostPerPoint;
        row.Setup(dataIndex, entry.label, actualValue, cpSpent, entry.isEditable, canIncrease, canDecrease, OnRowPointsChanged);
    }

    // 강화 포인트(entry.currentValue)가 실제로 만들어내는 스탯 수치 — ShipStatCalculator.CalculateWeaponSlots/CalculateHangarSlots와 동일한 공식 사용
    private float CalculateActualStatValue(ReinforceEntry entry)
    {
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_localModuleSubType);
        ShipStatFormulaSettings formula = DataManager.Instance.m_dataTableConfig.gameSettings.shipStatFormula;

        if (m_moduleType == EModuleType.beam || m_moduleType == EModuleType.missile)
        {
            float baseAttack = moduleData != null ? moduleData.attack : 0f;
            float baseAttackCool = moduleData != null ? moduleData.attackCool : 0f;
            float baseProjectileSpeed = moduleData != null ? moduleData.speed : 0f;
            float attackPerPoint = m_moduleType == EModuleType.beam ? formula.beam.attackPerPoint : formula.missile.attackPerPoint;
            float attackCoolReductionPerPoint = m_moduleType == EModuleType.beam ? formula.beam.attackCoolReductionPerPoint : formula.missile.attackCoolReductionPerPoint;
            float attackCoolFloor = m_moduleType == EModuleType.beam ? formula.beam.attackCoolFloor : formula.missile.attackCoolFloor;
            float projectileSpeedPerPoint = m_moduleType == EModuleType.beam ? formula.beam.projectileSpeedPerPoint : formula.missile.projectileSpeedPerPoint;

            if (entry.label == "Attack") return baseAttack + entry.currentValue * attackPerPoint;
            if (entry.label == "Fire Rate") return Mathf.Max(attackCoolFloor, baseAttackCool - entry.currentValue * attackCoolReductionPerPoint);
            if (entry.label == "Projectile Speed") return baseProjectileSpeed + entry.currentValue * projectileSpeedPerPoint;
            if (entry.label == "Silence Time" && m_moduleType == EModuleType.missile)
            {
                float baseSilenceTime = moduleData != null ? moduleData.silenceTime : 0f;
                return baseSilenceTime + entry.currentValue * formula.missile.silenceTimePerPoint;
            }
        }
        else if (m_moduleType == EModuleType.hangar)
        {
            if (entry.label == "Attack To Ship") return formula.hangar.baseShipAttack + entry.currentValue * formula.hangar.reinforcePerPoint;
            if (entry.label == "Attack To Fighter") return formula.hangar.baseFighterAttack + entry.currentValue * formula.hangar.reinforcePerPoint;
            if (entry.label == "Ammo") return formula.hangar.baseAmmo + entry.currentValue * formula.hangar.reinforcePerPoint;
            if (entry.label == "Health") return formula.hangar.baseHealth + entry.currentValue * formula.hangar.reinforcePerPoint;
        }

        return 0f;
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
        RefreshTierButtons(); // 강화 포인트가 바뀌면 잔여 지휘력도 바뀌어 티어업 가능 여부가 달라짐
        if (m_scrollView != null) m_scrollView.RefreshVisible();
    }

    // 잔여 지휘력 = 최대 지휘력 - (다른 슬롯 사용량 + 이 슬롯의 설치비 + 이 슬롯의 강화 포인트 합)
    private int GetRemainingCommandPower()
    {
        int thisSlotReinforceCost = FleetComposition.k_reinforceCpCostPerPoint * (m_localAttackPoints + m_localAttackToFighterPoints);
        int thisSlotCost = m_otherFieldsCostInThisSlot + thisSlotReinforceCost;
        return m_maxCommandPower - (m_usedByOtherSlots + thisSlotCost);
    }

    private void RefreshCommandPowerPreview()
    {
        if (m_commandPowerRow == null) return;

        int remaining = GetRemainingCommandPower();
        bool isOverCommandPower = remaining < 0;

        m_commandPowerRow.SetRow("CommandPower", $"{m_maxCommandPower - remaining} / {m_maxCommandPower}", rawValue: true);
        m_commandPowerRow.SetValueColor(CommonUtility.PaletteColor(isOverCommandPower == true ? "Text.Warning" : "Text.Dark1"));
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_commandPowerRow.transform as RectTransform);
    }

    private void OnConfirmClicked()
    {
        if (m_onConfirm != null) m_onConfirm(m_localModuleSubType, m_localAttackPoints, m_localAttackToFighterPoints);
        if (m_onClose != null) m_onClose.Invoke();
    }

    private void OnCancelClicked()
    {
        if (m_onClose != null) m_onClose.Invoke();
    }
}
