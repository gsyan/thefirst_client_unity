// 함선 로드아웃 편집 화면(UIShipLoadoutEditorView)의 슬롯 1개에 대한 모듈 레벨업 팝업
// 업적포인트 기반 아키타입단위 영구 모듈강화(M6) — 레벨은 함선 인스턴스가 아니라 (내 커맨더, hullArchetype, moduleType, slotIndex) 단위로 공유됨.
// beam/missile/hangar만 대상(슬롯 개념이 있는 카테고리) — hull/shield/interceptor는 슬롯 없이 이 화면 밖에서 다룸
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupModuleReinforce : UIPopupBase
{
    [SerializeField] private RowLabelValue m_commandPowerRow; // 보유 업적포인트 표시로 재활용
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private Button m_confirmButton; // 미사용(숨김) — 레벨업은 즉시 서버에 반영되는 단일 액션이라 별도 커밋 단계가 없음
    [SerializeField] private TMP_Text m_confirmButtonText;
    [SerializeField] private Button m_cancelButton; // 닫기
    [SerializeField] private TMP_Text m_cancelButtonText;

    [Header("레벨업 — 티어업/다운 버튼 자리를 레벨업 단일 액션으로 재활용(다운그레이드 없음)")]
    [SerializeField] private TMP_Text m_tierText;
    [SerializeField] private Button m_tierUpButton; // 레벨업 실행
    [SerializeField] private TMP_Text m_tierUpButtonText; // 버튼 라벨("강화") — 고정 텍스트, Awake에서 1회만 세팅
    [SerializeField] private TMP_Text m_tierCostText; // 다음 레벨 업적포인트 비용 — 부족하면 빨간색

    [Header("현재 레벨의 실제 스탯 목록(beam/missile/hangar) — Label/Value만 표시")]
    [SerializeField] private InfiniteScrollView m_statsScrollView;
    [SerializeField] private UIStatRow m_statsRowPrefab;

    private string m_hullArchetype;
    private EModuleType m_moduleType;
    private int m_slotIndex;
    private System.Action m_onClose;
    private List<(string labelKey, float value)> m_statEntries = new();

    protected override void Awake()
    {
        base.Awake();
        if (m_confirmButton != null) m_confirmButton.gameObject.SetActive(false);
        if (m_cancelButton != null) m_cancelButton.onClick.AddListener(OnCloseClicked);
        if (m_tierUpButton != null) m_tierUpButton.onClick.AddListener(OnLevelUpClicked);
        if (m_statsScrollView != null) m_statsScrollView.onItemBind = OnStatsItemBind;

        CommonUtility.SetUILocText(m_titleText, "UI_ModuleLevelUp");
        CommonUtility.SetUILocText(m_cancelButtonText, "UI_Cancel");
        CommonUtility.SetUILocText(m_tierUpButtonText, "UI_ModuleLevelUpButton");
    }

    public void ShowPopupModuleReinforce(string hullArchetype, EModuleType moduleType, int slotIndex, System.Action onClose)
    {
        m_hullArchetype = hullArchetype;
        m_moduleType = moduleType;
        m_slotIndex = slotIndex;
        m_onClose = onClose;

        RefreshDisplay();
        ShowPopup();
    }

    // 다음 레벨 행의 ap_cost — 더 올릴 레벨 데이터가 CSV에 없으면(최대 레벨) null
    private int? GetNextLevelCost(int nextLevel)
    {
        string key = $"{m_moduleType}_{nextLevel}_1";
        ModuleData data = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(key);
        return data != null ? data.unlockAchievementPointCost : (int?)null;
    }

    // 현재 레벨/다음 레벨/다음 레벨 ap_cost(최대 레벨이면 null) — RefreshDisplay와 OnLevelUpClicked 공용
    private (int currentLevel, int nextLevel, int? nextCost) ComputeLevelInfo()
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        int purchaseCount = commander != null ? commander.GetArchetypeUpgradeLevel(m_hullArchetype, m_moduleType, m_slotIndex) : 0;
        int currentLevel = purchaseCount + 1;
        int nextLevel = currentLevel + 1;
        return (currentLevel, nextLevel, GetNextLevelCost(nextLevel));
    }

    private void RefreshDisplay()
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        (int currentLevel, _, int? nextCost) = ComputeLevelInfo();
        int achievementPoint = commander != null ? commander.GetAchievementPoint() : 0;

        if (m_tierText != null)
            m_tierText.text = LocalizationManager.Instance.Get("UI_TierLabel", currentLevel);

        bool isMaxLevel = nextCost.HasValue == false;
        bool canAfford = isMaxLevel == false && achievementPoint >= nextCost.Value;

        if (m_tierCostText != null)
        {
            string achievementPointLabel = LocalizationManager.Instance.Get("UI_AchievementPoint");
            m_tierCostText.gameObject.SetActive(!isMaxLevel);
            m_tierCostText.text = isMaxLevel == true ? "" : $"{achievementPointLabel} {nextCost.Value}";
            m_tierCostText.color = CommonUtility.PaletteColor(canAfford == false ? "Text.Warning" : "Text.Dark1");
        }

        if (m_tierUpButton != null)
            m_tierUpButton.interactable = canAfford == true;

        if (m_commandPowerRow != null)
        {
            string achievementPointLabel = LocalizationManager.Instance.Get("UIPanelFleet_AchievementPoint");
            m_commandPowerRow.SetRow("UIPanelFleet_AchievementPoint", $"{CommonUtility.FormatNumber(achievementPoint)}", rawValue: true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_commandPowerRow.transform as RectTransform);
        }

        RefreshStatRows(currentLevel);
    }

    // 현재 레벨의 실제 장착 스탯(beam/missile/hangar) 목록으로 스크롤뷰 갱신
    private void RefreshStatRows(int currentLevel)
    {
        if (m_statsScrollView == null || m_statsRowPrefab == null) return;

        string key = $"{m_moduleType}_{currentLevel}_1";
        ModuleData data = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(key);
        m_statEntries = BuildStatEntries(data);
        m_statsScrollView.Initialize(m_statEntries.Count, m_statsRowPrefab.gameObject);
    }

    // moduleType별 표시 대상 스탯 — 기존 UIModuleReinforce_* 로컬라이즈 키 재사용(구 개별강화 UI에서 쓰던 라벨)
    private List<(string labelKey, float value)> BuildStatEntries(ModuleData data)
    {
        List<(string labelKey, float value)> entries = new();
        if (data == null) return entries;

        if (m_moduleType == EModuleType.beam)
        {
            entries.Add(("UIModuleReinforce_Attack", data.attack));
            entries.Add(("UIModuleReinforce_FireRate", data.attackCool));
        }
        else if (m_moduleType == EModuleType.missile)
        {
            entries.Add(("UIModuleReinforce_Attack", data.attack));
            entries.Add(("UIModuleReinforce_FireRate", data.attackCool));
            entries.Add(("UIModuleReinforce_SilenceTime", data.silenceTime));
        }
        else if (m_moduleType == EModuleType.hangar)
        {
            entries.Add(("UIModuleReinforce_AttackToShip", data.airAttackToShip));
            entries.Add(("UIModuleReinforce_AttackToFighter", data.airAttackToFighter));
            entries.Add(("UIModuleReinforce_Ammo", data.airAmmo));
            entries.Add(("UIModuleReinforce_Health", data.airHealth));
            entries.Add(("UIModuleReinforce_Disrupt", data.airDisrupt));
        }

        return entries;
    }

    // InfiniteScrollView가 dataIndex번 행을 화면에 배치할 때마다 호출 — 캐시된 m_statEntries로 바인딩
    private void OnStatsItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_statEntries.Count) return;

        UIStatRow row = rowObject.GetComponent<UIStatRow>();
        if (row == null) return;

        (string labelKey, float value) = m_statEntries[dataIndex];
        row.SetStatRow(LocalizationManager.Instance.Get(labelKey), value);
    }

    // 강화 버튼 클릭 — 바로 요청하지 않고 확인 팝업(다음 레벨/비용 + 스탯 변화 표시)을 거친 뒤 확정 시에만 RequestLevelUp 호출
    private void OnLevelUpClicked()
    {
        (int currentLevel, int nextLevel, int? nextCost) = ComputeLevelInfo();
        if (nextCost.HasValue == false) return; // 최대 레벨 — 버튼이 이미 비활성화 상태지만 방어적으로 재확인

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message = string.Format(LocalizationManager.Instance.Get("UIPopupModuleReinforce_LevelUpConfirmMessage"), nextLevel),
            cost = new CostStruct(ECostType.AchievementPoint, nextCost.Value),
            resultRows = BuildComparisonRows(currentLevel, nextLevel),
            resultSectionTitle = LocalizationManager.Instance.Get("UIPopupModuleReinforce_StatChangeTitle"), // UISection.SetTitle은 로컬라이즈하지 않고 그대로 표시하므로 여기서 직접 resolve
            onConfirm = RequestLevelUp,
            onCancel = () => { },
        });
    }

    // 현재 레벨 → 다음 레벨 스탯 비교 행("10.0 → 12.0" 형식) — BuildStatEntries와 같은 순서로 1:1 대응되므로 인덱스로 짝지음
    private List<(string label, string value, Color? color)> BuildComparisonRows(int currentLevel, int nextLevel)
    {
        DataTableModule moduleTable = DataManager.Instance.m_dataTableModule;
        ModuleData currentData = moduleTable.GetModuleDataFromTable($"{m_moduleType}_{currentLevel}_1");
        ModuleData nextData = moduleTable.GetModuleDataFromTable($"{m_moduleType}_{nextLevel}_1");

        List<(string labelKey, float value)> currentEntries = BuildStatEntries(currentData);
        List<(string labelKey, float value)> nextEntries = BuildStatEntries(nextData);

        List<(string label, string value, Color? color)> rows = new();
        for (int i = 0; i < currentEntries.Count; i++)
        {
            string currentText = $"{CommonUtility.FloorToDecimals(currentEntries[i].value, 1):F1}";
            string nextText = $"{CommonUtility.FloorToDecimals(nextEntries[i].value, 1):F1}";
            rows.Add((currentEntries[i].labelKey, $"{currentText} → {nextText}", null));
        }
        return rows;
    }

    private void RequestLevelUp()
    {
        if (m_tierUpButton != null) m_tierUpButton.interactable = false; // 서버 응답까지 중복 요청 방지

        UpgradeArchetypeModuleRequest request = new UpgradeArchetypeModuleRequest
        {
            hullArchetype = m_hullArchetype,
            moduleType = m_moduleType,
            slotIndex = m_slotIndex,
        };
        NetworkManager.Instance.UpgradeArchetypeModule(request, response =>
        {
            if (response.errorCode != 0)
            {
                Debug.LogError($"[UIPopupModuleReinforce] UpgradeArchetypeModule 실패: {response.errorCode}");
                RefreshDisplay();
                return;
            }

            Commander commander = DataManager.Instance.m_currentCommander;
            if (commander != null)
            {
                commander.UpdateArchetypeUpgradeLevel(m_hullArchetype, m_moduleType, m_slotIndex, response.data.level);
                commander.UpdateAchievementPoint(response.data.achievementPointRemain);
            }

            RefreshDisplay();
        });
    }

    private void OnCloseClicked()
    {
        if (m_onClose != null) m_onClose.Invoke();
    }
}
