// 함선 로드아웃 편집 화면(UIShipLoadoutEditorView)의 슬롯 1개에 대한 강화 포인트 편집 팝업
// Confirm 전까지는 이 팝업 내부의 로컬 복사본(m_local)만 바뀌고,
// 호출부(UIShipLoadoutEditorView)의 pending 상태는 Confirm을 눌러야 반영됨(Cancel이면 로컬 버퍼 폐기, pending 변경 없음)
// UI는 카테고리의 모든 강화 가능 축(빔: 공격/연사, 미사일: +침묵, 격납고: 대함/대전투기/탄약/체력, 실드: 게이지/회복, 요격체: 회복)을 나열
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupModuleReinforce : UIPopupBase
{
    [SerializeField] private UIReinforceStatRow m_rowPrefab;
    [SerializeField] private InfiniteScrollView m_scrollView;
    [SerializeField] private RowLabelValue m_commandPowerRow; // 잔여 지휘력 표시 — UIShipLoadoutEditorView.RefreshCommandPowerPreview와 동일한 표시 방식
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private TMP_Text m_confirmButtonText;
    [SerializeField] private Button m_cancelButton;
    [SerializeField] private TMP_Text m_cancelButtonText;

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
        public string label; // 항목 식별자 — GetPoints/SetPoints/CalculateActualStatValue의 분기 키
        public bool isReadOnly; // 강화 불가(티어별 고정 수치) — Up/Down 비활성, 투입 포인트 칸 비움
    }

    private EModuleType m_moduleType;
    private ModuleInfo m_local;               // 편집 로컬 복사본 — 서브타입(티어업/다운으로 변경)과 모든 강화 포인트, Confirm 전까지 외부에 반영 안 됨
    private int m_maxCommandPower;
    private int m_usedByOtherSlots;           // 이 슬롯을 제외한 다른 슬롯들의 지휘력 사용량 — 잔여 지휘력 계산용
    private int m_otherFieldsCostInThisSlot;  // 이 슬롯의 설치비(현재 서브타입의 statPoint) — 티어업/다운 시 새 티어의 statPoint로 갱신됨
    private int m_hullTier;                   // 무기 티어 상한 — 함체 티어를 넘는 티어업은 불가(함체를 낮은 티어로 바꾸면 초과 모듈이 자동 다운그레이드되므로 상한도 항상 이 값과 일치해야 함)
    private System.Action m_onClose;
    private System.Action<ModuleInfo> m_onConfirm;

    private readonly List<ReinforceEntry> m_entries = new();

    protected override void Awake()
    {
        base.Awake();
        if (m_confirmButton != null) m_confirmButton.onClick.AddListener(OnConfirmClicked);
        if (m_cancelButton != null) m_cancelButton.onClick.AddListener(OnCancelClicked);
        if (m_tierUpButton != null) m_tierUpButton.onClick.AddListener(() => OnTierClicked(+1));
        if (m_tierDownButton != null) m_tierDownButton.onClick.AddListener(() => OnTierClicked(-1));
        if (m_scrollView != null) m_scrollView.onItemBind = OnItemBind;

        CommonUtility.SetUILocText(m_titleText, "UI_ModuleReinforce");
        CommonUtility.SetUILocText(m_confirmButtonText, "UI_Confirm");
        CommonUtility.SetUILocText(m_cancelButtonText, "UI_Cancel");
        CommonUtility.SetUILocText(m_colStatusText, "UIFleet_ModuleReinforce_ColStatus");
        CommonUtility.SetUILocText(m_colValueText, "UIFleet_ModuleReinforce_ColValue");
        CommonUtility.SetUILocText(m_colInvestedText, "UIFleet_ModuleReinforce_ColInvested");

        // 모듈 티어업 튜토리얼이 티어업 버튼을 가리킬 수 있도록 리졸버 등록
        m_tierUpButtonResolver = ResolveTierUpButton;
        TutorialManager.Instance.RegisterDynamicTarget(TutorialManager.DYNAMIC_TARGET_MODULE_TIER_UP_BUTTON, m_tierUpButtonResolver);
    }

    private System.Func<RectTransform> m_tierUpButtonResolver;

    private void OnDestroy()
    {
        TutorialManager tutorialManager = TutorialManager.Instance; // 종료 중이면 null
        if (tutorialManager != null)
            tutorialManager.UnregisterDynamicTarget(TutorialManager.DYNAMIC_TARGET_MODULE_TIER_UP_BUTTON, m_tierUpButtonResolver);
    }

    private RectTransform ResolveTierUpButton()
    {
        if (m_tierUpButton == null || m_tierUpButton.gameObject.activeInHierarchy == false) return null;
        return m_tierUpButton.GetComponent<RectTransform>();
    }

    // initialInfo: 편집 시작 상태(서브타입 + 강화 포인트) — 팝업은 복사본만 수정하고 Confirm 시 그 복사본을 onConfirm으로 돌려줌
    // usedByOtherSlots: 이 슬롯을 제외한 다른 슬롯들의 지휘력 사용량(설치비+강화포인트 포함, 호출부가 FleetComposition 기준으로 계산해 넘김)
    // installCost: 이 슬롯의 설치비(바디 비용 제외, initialInfo 서브타입의 statPoint) — 강화 포인트와 별개로 잔여 지휘력 계산에 필요
    public void ShowPopupModuleReinforce(EModuleType moduleType, ModuleInfo initialInfo,
        int maxCommandPower, int usedByOtherSlots, int installCost, int hullTier, System.Action onClose, System.Action<ModuleInfo> onConfirm)
    {
        m_moduleType = moduleType;
        m_local = FleetComposition.CloneModuleInfo(initialInfo);
        m_maxCommandPower = maxCommandPower;
        m_usedByOtherSlots = usedByOtherSlots;
        m_otherFieldsCostInThisSlot = installCost;
        m_hullTier = hullTier;
        m_onClose = onClose;
        m_onConfirm = onConfirm;

        RefreshTierText();

        RefreshEntries();
        RefreshTierButtons();
        ShowPopup();
    }

    // 카테고리 접두어({moduleType}) + (현재 티어 + tierDelta) + "_1"로 후보 서브타입을 조립해 데이터 존재 여부 확인 — 없으면 null
    // 티어업(tierDelta>0)은 함체 티어를 넘을 수 없음 — 함체를 낮은 티어로 바꾸면 초과 모듈이 자동 다운그레이드되는 규칙과 짝을 이룸
    private ModuleData FindAdjacentTierModuleData(int tierDelta)
    {
        int currentTier = CommonUtility.ParseTier(m_local.moduleSubType);
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

        int remainingExcludingTierCost = m_maxCommandPower - (m_usedByOtherSlots + FleetComposition.GetReinforceCpCostPerPoint() * FleetComposition.SumReinforcePoints(m_local));
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

        m_local.moduleSubType = candidate.moduleSubType;
        m_otherFieldsCostInThisSlot = candidate.statPoint;
        RefreshTierText();

        RefreshCommandPowerPreview();
        RefreshTierButtons();
        if (m_scrollView != null) m_scrollView.RefreshVisible(); // 티어가 바뀌면 현재 서브타입 기준 실제 수치(StatValue)도 다시 그려야 함
    }

    // 원문 서브타입 코드(예: "hangar_14_1")를 그대로 노출하지 않고 "Tier N"으로 로컬라이즈해 표시
    private void RefreshTierText()
    {
        if (m_tierText == null) return;
        m_tierText.text = LocalizationManager.Instance.Get("UI_TierLabel", CommonUtility.ParseTier(m_local.moduleSubType));
    }

    private void RefreshEntries()
    {
        m_entries.Clear();
        if (m_moduleType == EModuleType.beam)
        {
            m_entries.Add(new ReinforceEntry { label = "Attack" });
            m_entries.Add(new ReinforceEntry { label = "Fire Rate" });
        }
        else if (m_moduleType == EModuleType.missile)
        {
            m_entries.Add(new ReinforceEntry { label = "Attack" });
            m_entries.Add(new ReinforceEntry { label = "Fire Rate" });
            m_entries.Add(new ReinforceEntry { label = "Silence Time" });
        }
        else if (m_moduleType == EModuleType.hangar)
        {
            m_entries.Add(new ReinforceEntry { label = "Attack To Ship" });
            m_entries.Add(new ReinforceEntry { label = "Attack To Fighter" });
            m_entries.Add(new ReinforceEntry { label = "Ammo" });
            m_entries.Add(new ReinforceEntry { label = "Health" });
            m_entries.Add(new ReinforceEntry { label = "Disrupt" });
        }
        else if (m_moduleType == EModuleType.shield)
        {
            m_entries.Add(new ReinforceEntry { label = "Shield Gauge" });
            m_entries.Add(new ReinforceEntry { label = "Shield Regen Rate" });
        }
        else if (m_moduleType == EModuleType.interceptor)
        {
            m_entries.Add(new ReinforceEntry { label = "Interceptor Count", isReadOnly = true });
            m_entries.Add(new ReinforceEntry { label = "Interceptor Regen Time" });
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
        int currentPoints = GetPoints(entry.label);
        int maxPerSlot = DataManager.Instance.m_dataTableConfig.gameSettings.shipStatFormula.maxAttackReinforcePointsPerSlot;
        bool isAtSlotCap = currentPoints >= maxPerSlot;
        bool hasRemainingCommandPower = GetRemainingCommandPower() >= FleetComposition.GetReinforceCpCostPerPoint();
        bool canIncrease = entry.isReadOnly == false && isAtSlotCap == false && hasRemainingCommandPower == true;
        bool canDecrease = entry.isReadOnly == false && currentPoints > 0;

        float actualValue = CalculateActualStatValue(entry.label, currentPoints);
        int cpSpent = currentPoints * FleetComposition.GetReinforceCpCostPerPoint();
        string displayLabel = LocalizationManager.Instance.Get(GetLabelLocKey(entry.label));
        row.Setup(dataIndex, displayLabel, actualValue, cpSpent, entry.isReadOnly == false, canIncrease, canDecrease, OnRowPointsChanged,
            GetValueDecimals(entry.label), showInvested: entry.isReadOnly == false);
    }

    // 교란은 값이 작아(0.05~0.7) 소수 3자리, 쿨다운/침묵시간은 소수 2자리, 탄약은 정수, 그 외 소수 1자리
    private int GetValueDecimals(string label)
    {
        if (label == "Disrupt") return 3;
        if (label == "Fire Rate" || label == "Silence Time" || label == "Interceptor Regen Time") return 2;
        if (label == "Ammo" || label == "Interceptor Count") return 0;
        return 1;
    }

    // 항목 → ModuleInfo 포인트 필드 매핑. 실드는 attackPoints=게이지/attackToFighterPoints=회복속도, 요격체는 attackPoints=회복시간(에디터가 경계에서 ModuleHullInfo로 변환)
    private int GetPoints(string label)
    {
        if (label == "Attack To Fighter" || label == "Shield Regen Rate") return m_local.attackToFighterPoints;
        if (label == "Fire Rate") return m_local.fireRatePoints;
        if (label == "Silence Time") return m_local.silencePoints;
        if (label == "Ammo") return m_local.ammoPoints;
        if (label == "Health") return m_local.healthPoints;
        if (label == "Disrupt") return m_local.disruptPoints;
        return m_local.attackPoints;
    }

    private void SetPoints(string label, int points)
    {
        if (label == "Attack To Fighter" || label == "Shield Regen Rate") m_local.attackToFighterPoints = points;
        else if (label == "Fire Rate") m_local.fireRatePoints = points;
        else if (label == "Silence Time") m_local.silencePoints = points;
        else if (label == "Ammo") m_local.ammoPoints = points;
        else if (label == "Health") m_local.healthPoints = points;
        else if (label == "Disrupt") m_local.disruptPoints = points;
        else m_local.attackPoints = points;
    }

    // entry.label은 CalculateActualStatValue의 분기 비교에도 쓰이는 내부 식별자라 그대로 두고, 표시용 로컬라이즈 키만 매핑
    private string GetLabelLocKey(string label)
    {
        if (label == "Attack") return "UIModuleReinforce_Attack";
        if (label == "Fire Rate") return "UIModuleReinforce_FireRate";
        if (label == "Silence Time") return "UIModuleReinforce_SilenceTime";
        if (label == "Attack To Ship") return "UIModuleReinforce_AttackToShip";
        if (label == "Attack To Fighter") return "UIModuleReinforce_AttackToFighter";
        if (label == "Ammo") return "UIModuleReinforce_Ammo";
        if (label == "Health") return "UIModuleReinforce_Health";
        if (label == "Disrupt") return "UIModuleReinforce_Disrupt";
        if (label == "Shield Gauge") return "UIModuleReinforce_ShieldGauge";
        if (label == "Shield Regen Rate") return "UIModuleReinforce_ShieldRegenRate";
        if (label == "Interceptor Count") return "UIModuleReinforce_InterceptorCount";
        if (label == "Interceptor Regen Time") return "UIModuleReinforce_InterceptorRegenTime";
        return "";
    }

    // 강화 포인트(points)가 실제로 만들어내는 스탯 수치 — ShipStatCalculator와 동일한 공식 사용
    private float CalculateActualStatValue(string label, int points)
    {
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_local.moduleSubType);
        ShipStatFormulaSettings formula = DataManager.Instance.m_dataTableConfig.gameSettings.shipStatFormula;

        if (m_moduleType == EModuleType.beam || m_moduleType == EModuleType.missile)
        {
            float baseAttack = moduleData != null ? moduleData.attack : 0f;
            float baseAttackCool = moduleData != null ? moduleData.attackCool : 0f;
            float attackPerPoint = m_moduleType == EModuleType.beam ? formula.beam.attackPerPoint : formula.missile.attackPerPoint;
            float maxCoolReductionRatio = m_moduleType == EModuleType.beam ? formula.beam.maxCoolReductionRatio : formula.missile.maxCoolReductionRatio;
            float attackCoolFloor = m_moduleType == EModuleType.beam ? formula.beam.attackCoolFloor : formula.missile.attackCoolFloor;

            if (label == "Attack") return ShipStatCalculator.ComputeReinforcedFlat(baseAttack, points, attackPerPoint);
            if (label == "Fire Rate") return ShipStatCalculator.ComputeReducedCooldown(baseAttackCool, points, maxCoolReductionRatio, attackCoolFloor, formula);
            if (label == "Silence Time" && m_moduleType == EModuleType.missile)
            {
                float baseSilenceTime = moduleData != null ? moduleData.silenceTime : 0f;
                return ShipStatCalculator.ComputeReinforcedFlat(baseSilenceTime, points, formula.missile.silenceTimePerPoint);
            }
        }
        else if (m_moduleType == EModuleType.hangar)
        {
            float baseShipAttack = moduleData != null ? moduleData.airAttackToShip : 0f;
            float baseFighterAttack = moduleData != null ? moduleData.airAttackToFighter : 0f;
            float baseAmmo = moduleData != null ? moduleData.airAmmo : 0f;
            float baseHealth = moduleData != null ? moduleData.airHealth : 0f;
            float baseDisrupt = moduleData != null ? moduleData.airDisrupt : 0f;

            if (label == "Attack To Ship") return ShipStatCalculator.ComputeReinforcedFlat(baseShipAttack, points, formula.hangar.attackPerPoint);
            if (label == "Attack To Fighter") return ShipStatCalculator.ComputeReinforcedFlat(baseFighterAttack, points, formula.hangar.attackPerPoint);
            if (label == "Ammo") return ShipStatCalculator.ComputeReinforcedAmmo(baseAmmo, points, formula);
            if (label == "Health") return ShipStatCalculator.ComputeBoostedValue(baseHealth, points, formula.hangar.maxHealthBonusRatio, formula);
            if (label == "Disrupt") return ShipStatCalculator.ComputeReinforcedFlat(baseDisrupt, points, formula.hangar.disruptPerPoint);
        }
        else if (m_moduleType == EModuleType.shield)
        {
            // 성능 표시/전투 모듈과 같은 공식(ShipStatCalculator) 사용
            float baseShieldGauge = moduleData != null ? moduleData.shieldGauge : 0f;
            float baseShieldRegenRate = moduleData != null ? moduleData.shieldRegenRate : 0f;

            if (label == "Shield Gauge") return ShipStatCalculator.ComputeShieldGauge(baseShieldGauge, points, formula);
            if (label == "Shield Regen Rate") return ShipStatCalculator.ComputeShieldRegenRate(baseShieldRegenRate, points, formula);
        }
        else if (m_moduleType == EModuleType.interceptor)
        {
            float baseInterceptorRegenTime = moduleData != null ? moduleData.interceptorRegenTime : 0f;

            if (label == "Interceptor Count") return moduleData != null ? moduleData.interceptorCount : 0f;
            if (label == "Interceptor Regen Time") return ShipStatCalculator.ComputeInterceptorRegenTime(baseInterceptorRegenTime, points, formula);
        }

        return 0f;
    }

    private void OnRowPointsChanged(int dataIndex, int delta)
    {
        if (dataIndex < 0 || dataIndex >= m_entries.Count) return;

        int maxPerSlot = DataManager.Instance.m_dataTableConfig.gameSettings.shipStatFormula.maxAttackReinforcePointsPerSlot;
        ReinforceEntry entry = m_entries[dataIndex];
        int rawValue = GetPoints(entry.label) + delta;
        int clampedValue = rawValue < 0 ? 0 : rawValue;
        if (clampedValue > maxPerSlot) clampedValue = maxPerSlot;

        SetPoints(entry.label, clampedValue);

        RefreshCommandPowerPreview();
        RefreshTierButtons(); // 강화 포인트가 바뀌면 잔여 지휘력도 바뀌어 티어업 가능 여부가 달라짐
        if (m_scrollView != null) m_scrollView.RefreshVisible();
    }

    // 잔여 지휘력 = 최대 지휘력 - (다른 슬롯 사용량 + 이 슬롯의 설치비 + 이 슬롯의 강화 포인트 합)
    private int GetRemainingCommandPower()
    {
        int thisSlotReinforceCost = FleetComposition.GetReinforceCpCostPerPoint() * FleetComposition.SumReinforcePoints(m_local);
        int thisSlotCost = m_otherFieldsCostInThisSlot + thisSlotReinforceCost;
        return m_maxCommandPower - (m_usedByOtherSlots + thisSlotCost);
    }

    private void RefreshCommandPowerPreview()
    {
        if (m_commandPowerRow == null) return;

        int remaining = GetRemainingCommandPower();
        bool isOverCommandPower = remaining < 0;

        m_commandPowerRow.SetRow("UI_CommandPower", $"{m_maxCommandPower - remaining} / {m_maxCommandPower}", rawValue: true);
        m_commandPowerRow.SetValueColor(CommonUtility.PaletteColor(isOverCommandPower == true ? "Text.Warning" : "Text.Dark1"));
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_commandPowerRow.transform as RectTransform);
    }

    private void OnConfirmClicked()
    {
        if (m_onConfirm != null) m_onConfirm(m_local);
        if (m_onClose != null) m_onClose.Invoke();
    }

    private void OnCancelClicked()
    {
        if (m_onClose != null) m_onClose.Invoke();
    }
}
