// 함선 로드아웃 편집 화면(UIShipLoadoutEditorView)의 슬롯 1개에 대한 모듈 레벨업 팝업
// 업적포인트 기반 아키타입단위 영구 모듈강화(M6) — 레벨은 함선 인스턴스가 아니라 (내 커맨더, hullArchetype, moduleType, slotIndex) 단위로 공유됨.
// beam/missile/hangar만 대상(슬롯 개념이 있는 카테고리) — hull/shield/interceptor는 슬롯 없이 이 화면 밖에서 다룸
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
    [SerializeField] private Button m_tierDownButton; // 미사용(숨김)
    [SerializeField] private TMP_Text m_tierText;
    [SerializeField] private Button m_tierUpButton; // 레벨업 실행
    [SerializeField] private TMP_Text m_tierCostText; // 다음 레벨 업적포인트 비용 — 부족하면 빨간색

    private string m_hullArchetype;
    private EModuleType m_moduleType;
    private int m_slotIndex;
    private System.Action m_onClose;

    protected override void Awake()
    {
        base.Awake();
        if (m_confirmButton != null) m_confirmButton.gameObject.SetActive(false);
        if (m_tierDownButton != null) m_tierDownButton.gameObject.SetActive(false);
        if (m_cancelButton != null) m_cancelButton.onClick.AddListener(OnCloseClicked);
        if (m_tierUpButton != null) m_tierUpButton.onClick.AddListener(OnLevelUpClicked);

        CommonUtility.SetUILocText(m_titleText, "UI_ModuleLevelUp");
        CommonUtility.SetUILocText(m_cancelButtonText, "UI_Cancel");
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

    private void RefreshDisplay()
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        int purchaseCount = commander != null ? commander.GetArchetypeUpgradeLevel(m_hullArchetype, m_moduleType, m_slotIndex) : 0;
        int currentLevel = purchaseCount + 1;
        int nextLevel = currentLevel + 1;
        int? nextCost = GetNextLevelCost(nextLevel);
        int achievementPoint = commander != null ? commander.GetAchievementPoint() : 0;

        if (m_tierText != null)
            m_tierText.text = LocalizationManager.Instance.Get("UI_TierLabel", currentLevel);

        bool isMaxLevel = nextCost.HasValue == false;
        bool canAfford = isMaxLevel == false && achievementPoint >= nextCost.Value;

        if (m_tierCostText != null)
        {
            string achievementPointLabel = LocalizationManager.Instance.Get("UIPanelFleet_AchievementPoint");
            m_tierCostText.text = isMaxLevel == true ? "" : $"{nextCost.Value} {achievementPointLabel}";
            m_tierCostText.color = CommonUtility.PaletteColor(canAfford == false ? "Text.Warning" : "Text.Dark1");
        }

        if (m_tierUpButton != null)
            m_tierUpButton.interactable = canAfford == true;

        if (m_commandPowerRow != null)
        {
            string achievementPointLabel = LocalizationManager.Instance.Get("UIPanelFleet_AchievementPoint");
            m_commandPowerRow.SetRow("UI_CommandPower", $"{achievementPointLabel} : {CommonUtility.FormatNumber(achievementPoint)}", rawValue: true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_commandPowerRow.transform as RectTransform);
        }
    }

    private void OnLevelUpClicked()
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
