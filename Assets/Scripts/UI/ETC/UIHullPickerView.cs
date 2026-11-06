// 함체 선택 팝업 — UIPanelFleet 프리팹에 내장된 오버레이(별도 UIManager 팝업 스택 대상 아님). 리스트에서 함체를
// 고르고 확인/취소로 결정만 알려줄 뿐, 실제로 어느 슬롯에 어떻게 적용할지는 모른다(호출부가 콜백에서 처리) — 재사용성을
// 위해 이 컴포넌트는 "선택기" 역할만 담당한다. 선택된 함체의 스탯을 현재 장착 함체와 비교해서 함께 보여준다.
// 확인 시 팝업은 바로 닫히지 않고, 호출부가 처리 결과를 알려줄 때(성공) 닫힘 — 실패하면 선택을 유지한 채 다시 시도할 수 있다
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIHullPickerView : MonoBehaviour
{
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private TMP_Text m_statsTitleText;
    [SerializeField] private TMP_Text m_confirmButtonText; // 상태에 따라 "배치"/"배치해제"/"언락"으로 바뀌는 동적 라벨
    [SerializeField] private TMP_Text m_confirmButtonCostText; // 언락 모드에서만 활성화되는 비용(AP n) 표시
    [SerializeField] private TMP_Text m_cancelButtonText;
    [SerializeField] private InfiniteScrollView m_scrollView;
    [SerializeField] private UIAvailableHullRow m_rowPrefab;
    [SerializeField] private Button m_confirmButton; // 배치/배치해제/언락 3가지 역할을 겸하는 패널 공용 버튼 1개 — 선택된 함체 상태에 따라 RefreshUnlockState가 라벨/동작을 바꿔줌
    [SerializeField] private Button m_cancelButton;

    [SerializeField] private TMP_Text m_shipDescribeText; // 스탯 목록 위 고정 영역 — 선택된 함체의 대략적인 설명(ModuleData.descriptionKey 로컬라이즈)
    [SerializeField] private UIStatRow m_statsRowPrefab; // 선택된 함체의 스탯 — Column_Stats와 동일한 구조/프리팹 재사용
    [SerializeField] private InfiniteScrollView m_statsScrollView;

    [Header("성능 탭과 별개 탭 — 탭 전환 자체는 TabSystem(UIPanelRank 참고)이 프리팹에서 담당, 여기선 두 리스트 데이터만 갱신")]
    [SerializeField] private UIShipPassiveRow m_passiveRowPrefab;
    [SerializeField] private InfiniteScrollView m_passiveScrollView;
    [SerializeField] private TMP_Text m_passiveTabText; // 탭 버튼 라벨("패시브") — m_statsTitleText와 동일하게 Awake에서 1회만 세팅

    [SerializeField] private RowLabelValue m_commandPowerRow; // 선택 후보를 적용했을 때의 지휘력 미리보기 — UIPanelFleet 성능 컬럼의 지휘력 행과 동일한 구성
    [SerializeField] private RowLabelValue m_achievementPointRow; // 현재 보유 업적포인트 — 언락 비용(AP)을 감당할 수 있는지 바로 비교해보라고 상시 표시

    [SerializeField] private RawImage m_previewImage; // 선택된 함체의 3D 바디 미리보기 — ShipPreviewManager가 렌더링한 텍스처

    [Header("화면 상단 진척도 게이지 — 선택된 함체의 미해금 패시브 중 counter_type별 다음 목표. 프리팹에 최대 개수(2)만큼 미리 배치, 런타임 Instantiate 금지")]
    [SerializeField] private GameObject m_progressGaugeContainer; // 보여줄 미션이 하나도 없으면 컨테이너 자체를 비활성화
    [SerializeField] private UIProgressGaugeBar[] m_progressGaugeSlots;

    [Header("모듈 슬롯 개수 표시 — 순수 정보용(Button 비활성)")]
    [SerializeField] private TMP_Text m_beamSlotCountText;
    [SerializeField] private TMP_Text m_missileSlotCountText;
    [SerializeField] private TMP_Text m_hangarSlotCountText;
    [SerializeField] private TMP_Text m_shieldSlotCountText;
    [SerializeField] private TMP_Text m_interceptorSlotCountText;

    private readonly List<ModuleData> m_hullsCache = new();
    private List<ShipStatRowEntry> m_statEntries = new();
    private List<PassiveRowEntry> m_passiveEntries = new();
    private Dictionary<string, ShipStatRowEntry> m_currentEntriesByLabel; // 비교 기준(현재 장착 함체) — 라벨로 조회

    private string m_selectedHullSubType;
    private int m_currentSlotCommandCost; // 이 슬롯이 지금 실제로 쓰고 있는 지휘력(보관만, 표시는 projectedUsedCommandPower/max 비율로 함)
    private int m_baseUsedCommandPower; // 이 슬롯이 점유 중이던 지휘력을 미리 뺀 값 — 후보 함체 비용만 더하면 미리보기 완성
    private int m_maxCommandPower;
    private System.Action<string, System.Action<bool>> m_onConfirm; // 확인 시 (선택된 hullSubType, 호출부가 처리를 마치고 부르는 콜백(true=팝업 닫기)) 전달
    private System.Action<System.Action<bool>> m_onRemove; // 배치 해제 시 (호출부가 처리를 마치고 부르는 콜백(true=팝업 닫기)) 전달
    private bool m_hasExistingShip; // Open() 시점에 이 슬롯에 이미 함선이 배치돼 있었는지 — 배치해제 모드 판정 조건(선택 후보가 바뀌어도 유지)
    private string m_originalHullSubType; // Open() 시점에 이 슬롯에 배치돼 있던 함체 — 선택이 이 값과 같으면 "배치해제" 모드로 전환
    private HashSet<string> m_archetypesPlacedElsewhere = new(); // M7 — 다른 슬롯이 이미 쓰고 있는 아키타입(이 슬롯 자신의 현재 함체는 제외) — 행에 "배치됨" 표시용
    private System.Action m_onCancel;
    private System.Func<RectTransform> m_lowestLockedHullTargetResolver; // 등록/해제 시 같은 델리게이트 인스턴스를 써야 하므로 보관
    private System.Func<bool> m_overCommandPowerEvaluator;

    private enum EPickerButtonMode { Place, Unplace, Unlock }
    private EPickerButtonMode m_buttonMode = EPickerButtonMode.Place; // 패널 공용 버튼(m_confirmButton) 1개가 지금 어떤 동작을 하는지 — RefreshUnlockState가 갱신, 클릭 핸들러가 참조

    private struct PassiveRowEntry
    {
        public int stage;
        public string description;
        public bool isUnlocked;
    }

    private void Awake()
    {
        if (m_confirmButton != null)
            m_confirmButton.onClick.AddListener(OnActionButtonClicked);
        if (m_cancelButton != null)
            m_cancelButton.onClick.AddListener(OnCancelClicked);
        if (m_scrollView != null)
            m_scrollView.onItemBind = OnItemBind;
        if (m_statsScrollView != null)
            m_statsScrollView.onItemBind = OnStatsItemBind;
        if (m_passiveScrollView != null)
            m_passiveScrollView.onItemBind = OnPassiveItemBind;

        // 한 번 세팅되면 바뀌지 않는 정적 라벨 — Open()마다 반복 세팅하지 않고 Awake에서 1회만 처리
        if (m_statsTitleText != null)
            CommonUtility.SetUILocText(m_statsTitleText, "UIFleet_StatsTitle");
        if (m_passiveTabText != null)
            CommonUtility.SetUILocText(m_passiveTabText, "UIFleet_PassiveTabTitle");
        if (m_cancelButtonText != null)
            CommonUtility.SetUILocText(m_cancelButtonText, "UI_Cancel");
        // m_confirmButtonText는 상태에 따라 바뀌는 동적 라벨이라 여기서 고정 세팅하지 않음 — RefreshUnlockState가 매번 갱신

        // 튜토리얼이 "언락 안 된 최저 티어 함체의 언락 버튼"을 강조할 수 있도록 동적 타겟 리졸버 등록
        m_lowestLockedHullTargetResolver = ResolveLowestLockedHullUnlockButton;
        TutorialManager.Instance.RegisterDynamicTarget(TutorialManager.DYNAMIC_TARGET_LOWEST_LOCKED_HULL_UNLOCK_BUTTON, m_lowestLockedHullTargetResolver);

        // 튜토리얼 Branch 스텝이 "고른 함체가 지휘력을 초과하는지"를 물어볼 수 있도록 조건 평가자 등록
        m_overCommandPowerEvaluator = IsSelectedHullOverCommandPower;
        TutorialManager.Instance.RegisterBranchCondition(ETutorialConditionType.CommandPowerInsufficientForPickedHull, m_overCommandPowerEvaluator);

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        TutorialManager tutorialManager = TutorialManager.Instance; // 종료 중이면 null
        if (tutorialManager != null)
        {
            tutorialManager.UnregisterDynamicTarget(TutorialManager.DYNAMIC_TARGET_LOWEST_LOCKED_HULL_UNLOCK_BUTTON, m_lowestLockedHullTargetResolver);
            tutorialManager.UnregisterBranchCondition(ETutorialConditionType.CommandPowerInsufficientForPickedHull, m_overCommandPowerEvaluator);
        }
    }

    // 언락 안 된 함체 중 티어가 가장 낮은(같으면 목록 앞쪽) 함체를 찾아 선택 상태로 만든 뒤,
    // 패널 공용 언락 버튼의 rect를 돌려줌 — 버튼이 행별이 아니라 선택된 함체 기준 공용이라 스크롤 위치와 무관하게 항상 가리킬 수 있음
    private RectTransform ResolveLowestLockedHullUnlockButton()
    {
        int targetDataIndex = -1;
        int lowestTier = int.MaxValue;
        for (int i = 0; i < m_hullsCache.Count; i++)
        {
            ModuleData hull = m_hullsCache[i];
            if (IsHullLocked(hull) == false) continue;

            int tier = CommonUtility.ParseTier(hull.moduleSubType);
            if (tier >= lowestTier) continue;

            lowestTier = tier;
            targetDataIndex = i;
        }
        if (targetDataIndex < 0) return null;

        string targetHullSubType = m_hullsCache[targetDataIndex].moduleSubType;
        if (targetHullSubType != m_selectedHullSubType)
            OnHullClicked(m_hullsCache[targetDataIndex]);

        if (m_confirmButton == null || m_buttonMode != EPickerButtonMode.Unlock) return null;
        return m_confirmButton.transform as RectTransform;
    }

    // availableHulls: 고를 수 있는 함체 목록, currentHull: 비교 기준이 되는 현재 장착 함체(빈 슬롯이면 null → 비교 없이 수치만 표시)
    // currentModules: 그 슬롯에 실제로 장착된 모듈 구성(로드아웃) — null이면 currentHull의 기본 장착 구성으로 비교
    // currentSlotCommandCost: 이 슬롯이 지금 실제로 쓰고 있는 지휘력 — 리스트 각 행의 증감(+/-) 표시 기준
    // baseUsedCommandPower: 이 슬롯이 점유 중이던 몫을 이미 뺀 사용 지휘력(호출부가 계산해서 넘김)
    public void Open(List<ModuleData> availableHulls, ModuleData currentHull, ModuleHullInfo currentModules, int currentSlotCommandCost, int baseUsedCommandPower, int maxCommandPower, HashSet<string> archetypesPlacedElsewhere, System.Action<string, System.Action<bool>> onConfirm, System.Action<System.Action<bool>> onRemove = null, System.Action onCancel = null)
    {
        m_hullsCache.Clear();
        m_hullsCache.AddRange(availableHulls);
        m_archetypesPlacedElsewhere = archetypesPlacedElsewhere != null ? archetypesPlacedElsewhere : new HashSet<string>();

        // 원래 비어있던 슬롯(currentHull==null)은 고를 함체가 아직 없으므로, 목록 첫 번째 함체를 기본 선택해 미리보기가 빈 화면으로 뜨지 않게 함
        string defaultHullSubType = m_hullsCache.Count > 0 ? m_hullsCache[0].moduleSubType : null;
        m_selectedHullSubType = currentHull != null ? currentHull.moduleSubType : defaultHullSubType;
        m_hasExistingShip = currentHull != null;
        m_originalHullSubType = currentHull != null ? currentHull.moduleSubType : null;
        m_currentSlotCommandCost = currentSlotCommandCost;
        m_baseUsedCommandPower = baseUsedCommandPower;
        m_maxCommandPower = maxCommandPower;
        m_onConfirm = onConfirm;
        m_onRemove = onRemove;
        m_onCancel = onCancel;

        if (m_titleText != null)
            CommonUtility.SetUILocText(m_titleText, "UIFleet_HullPicker_Title");

        m_currentEntriesByLabel = null;
        if (currentHull != null)
        {
            m_currentEntriesByLabel = new Dictionary<string, ShipStatRowEntry>();
            List<ShipStatRowEntry> currentEntries = ShipStatGaugeBuilder.Build(currentHull, currentModules);
            for (int i = 0; i < currentEntries.Count; i++)
                m_currentEntriesByLabel[currentEntries[i].label] = currentEntries[i];
        }

        // Initialize는 viewport 크기를 읽으므로 반드시 활성화 다음에 호출 — 비활성 상태로 호출하면 NRE 위험
        gameObject.SetActive(true);
        m_scrollView.Initialize(m_hullsCache.Count, m_rowPrefab.gameObject);
        RefreshStatsDisplay();
        RefreshPassiveDisplay();
        RefreshProgressGauges();
        RefreshUnlockState(); // m_buttonMode를 먼저 확정해야 바로 아래 RefreshCommandPowerPreview의 interactable 판정이 최신 모드를 씀
        RefreshCommandPowerPreview();
        RefreshPreview();
        RefreshModuleSlotCounts();
    }

    public void Close()
    {
        gameObject.SetActive(false);
        m_onConfirm = null;
        m_onRemove = null;
        m_onCancel = null;
        ShipPreviewManager.Instance.Clear();
    }

    private void RefreshPreview()
    {
        if (m_previewImage == null) return;

        Rect previewRect = m_previewImage.rectTransform.rect;
        float aspect = previewRect.height > 0f ? previewRect.width / previewRect.height : 1f;

        m_previewImage.texture = ShipPreviewManager.Instance.GetPreviewTexture(aspect);
        ModuleData selectedHull = m_hullsCache.Find(p => p.moduleSubType == m_selectedHullSubType);
        ModuleHullInfo previewModules = selectedHull != null ? FleetComposition.BuildDefaultLevel1Modules(m_selectedHullSubType) : null;
        ShipPreviewManager.Instance.ShowHull(selectedHull, previewModules);
    }

    // 선택된 함체 후보의 카테고리별 슬롯 개수를 텍스트로 표시 — [beam, missile, hangar, shield, interceptor]
    private void RefreshModuleSlotCounts()
    {
        ModuleData selectedHull = m_hullsCache.Find(p => p.moduleSubType == m_selectedHullSubType);
        int[] slots = selectedHull != null ? CommonUtility.ParseHullSlotComposition(selectedHull.moduleSubType) : new int[5];

        if (m_beamSlotCountText != null) m_beamSlotCountText.text = $"{slots[0]}";
        if (m_missileSlotCountText != null) m_missileSlotCountText.text = $"{slots[1]}";
        if (m_hangarSlotCountText != null) m_hangarSlotCountText.text = $"{slots[2]}";
        if (m_shieldSlotCountText != null) m_shieldSlotCountText.text = $"{slots[3]}";
        if (m_interceptorSlotCountText != null) m_interceptorSlotCountText.text = $"{slots[4]}";
    }

    private void OnItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_hullsCache.Count) return;

        UIAvailableHullRow row = rowObject.GetComponent<UIAvailableHullRow>();
        if (row == null) return;

        ModuleData hull = m_hullsCache[dataIndex];
        string hullSubType = hull.moduleSubType;
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        ModuleHullInfo defaultModulesForRow = FleetComposition.BuildDefaultLevel1Modules(hullSubType);
        int projectedCost = composition != null ? composition.ComputeProjectedSlotCommandCost(hullSubType, defaultModulesForRow) : hull.statPoint;
        bool isOverCommandPowerIfSelected = m_baseUsedCommandPower + projectedCost > m_maxCommandPower;

        bool isLocked = IsHullLocked(hull);
        bool isPlacedElsewhere = string.IsNullOrEmpty(hull.hullArchetype) == false && m_archetypesPlacedElsewhere.Contains(hull.hullArchetype);
        row.Setup(hull, projectedCost, isOverCommandPowerIfSelected, isLocked, isPlacedElsewhere, OnHullClicked);
        row.SetSelectedAvailableHullRow(hullSubType == m_selectedHullSubType);

        LayoutRebuilder.ForceRebuildLayoutImmediate(rowObject.transform as RectTransform);
    }

    private void OnHullClicked(ModuleData hull)
    {
        m_selectedHullSubType = hull.moduleSubType;
        m_scrollView.RefreshVisible(); // 재바인드되며 OnItemBind가 다시 불려 하이라이트가 새 선택으로 갱신됨
        RefreshStatsDisplay();
        RefreshPassiveDisplay();
        RefreshProgressGauges();
        RefreshUnlockState(); // m_buttonMode를 먼저 확정해야 바로 아래 RefreshCommandPowerPreview의 interactable 판정이 최신 모드를 씀
        RefreshCommandPowerPreview();
        RefreshPreview();
        RefreshModuleSlotCounts();
    }

    // 선택된 함체 상태에 따라 패널 공용 버튼(m_confirmButton) 1개의 모드를 결정 — 잠김: 언락, 이미 이 슬롯에 배치된 함체를 다시 선택: 배치해제, 그 외: 배치
    private void RefreshUnlockState()
    {
        ModuleData selectedHull = m_hullsCache.Find(p => p.moduleSubType == m_selectedHullSubType);
        bool isLocked = IsHullLocked(selectedHull);
        bool isSameAsCurrent = m_hasExistingShip == true && m_selectedHullSubType == m_originalHullSubType;

        RefreshAchievementPointRow();

        if (isLocked == true)
            m_buttonMode = EPickerButtonMode.Unlock;
        else if (isSameAsCurrent == true)
            m_buttonMode = EPickerButtonMode.Unplace;
        else
            m_buttonMode = EPickerButtonMode.Place;

        if (m_confirmButtonText != null)
        {
            string labelKey;
            if (m_buttonMode == EPickerButtonMode.Unlock)
                labelKey = "UIHullPicker_UnlockButtonLabel";
            else if (m_buttonMode == EPickerButtonMode.Unplace)
                labelKey = "UI_Remove";
            else
                labelKey = "UI_Place";
            CommonUtility.SetUILocText(m_confirmButtonText, labelKey);
        }

        if (m_confirmButtonCostText != null)
        {
            bool showCost = m_buttonMode == EPickerButtonMode.Unlock;
            m_confirmButtonCostText.gameObject.SetActive(showCost);
            if (showCost == true)
            {
                string apLabel = LocalizationManager.Instance.Get("UI_AchievementPoint");
                m_confirmButtonCostText.text = $"{apLabel} {CommonUtility.FormatNumber(selectedHull.unlockAchievementPointCost)}";
            }
        }
    }

    // unlockAchievementPointCost가 0보다 크면 티어4+ 언락 대상 — 아직 언락 안 됐으면 잠김
    // 티어1은 기본 지급이라 항상 사용 가능, 티어2+는 비용(ap_cost)과 무관하게 언락 기록(CommanderInfo.unlockedHulls) 존재 여부로만 판정 — 서버 FleetService.placeFleetShip과 동일 규칙
    private bool IsHullLocked(ModuleData hull)
    {
        if (hull == null || CommonUtility.ParseTier(hull.moduleSubType) <= 1) return false;
        Commander commander = DataManager.Instance.m_currentCommander;
        return commander == null || commander.IsHullUnlocked(hull.moduleSubType) == false;
    }

    // 패널 공용 언락 버튼 클릭 — 현재 선택된 함체 기준. 함체명/비용 확인 팝업 후 확정 시 서버에 업적포인트 소모 요청
    // cost를 CostStruct로 넘기면 UIPopupConfirm이 부족 시 빨간색 표기 + 확인 버튼 비활성을 알아서 처리함
    private void OnUnlockButtonClicked()
    {
        ModuleData hull = m_hullsCache.Find(p => p.moduleSubType == m_selectedHullSubType);
        if (hull == null) return;

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message = string.Format(LocalizationManager.Instance.Get("UIHullPicker_UnlockConfirmMessage"), CommonUtility.BuildHullDisplayName(hull.moduleSubType)),
            cost = new CostStruct(ECostType.AchievementPoint, hull.unlockAchievementPointCost),
            onConfirm = () => RequestUnlockHull(hull.moduleSubType),
            onCancel = () => { },
        });
    }

    // 배치 해제 버튼 — 호출부(UIPanelFleet)가 실제 서버 요청/로컬 편성 반영을 처리, 여기선 확인 후 위임만 함
    private void OnRemoveButtonClicked()
    {
        if (m_onRemove == null) return;

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message = LocalizationManager.Instance.Get("UIHullPicker_RemoveConfirmMessage"),
            onConfirm = () =>
            {
                if (m_confirmButton != null)
                    m_confirmButton.interactable = false;
                m_onRemove(OnConfirmProcessed);
            },
            onCancel = () => { },
        });
    }

    private void RequestUnlockHull(string hullSubType)
    {
        UnlockHullRequest request = new UnlockHullRequest { hullSubType = hullSubType };
        NetworkManager.Instance.UnlockHull(request, response =>
        {
            if (response.errorCode != 0)
            {
                Debug.LogError($"[UIHullPickerView] UnlockHull 실패: {response.errorCode}");
                return;
            }

            Commander commander = DataManager.Instance.m_currentCommander;
            if (commander != null)
            {
                commander.UpdateUnlockedHulls(response.data.unlockedHulls);
                commander.UpdateAchievementPoint(response.data.achievementPointRemain);
            }

            m_scrollView.RefreshVisible();
            RefreshUnlockState(); // 언락 성공으로 모드가 Place로 바뀌므로 interactable 판정(RefreshCommandPowerPreview) 전에 먼저 갱신
            RefreshCommandPowerPreview();
        });
    }

    // 선택된 함체를 이 슬롯에 적용했다고 가정했을 때의 총 지휘력 사용량
    private int GetProjectedUsedCommandPower()
    {
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        ModuleHullInfo defaultModules = FleetComposition.BuildDefaultLevel1Modules(m_selectedHullSubType);
        int selectedCost = composition != null ? composition.ComputeProjectedSlotCommandCost(m_selectedHullSubType, defaultModules) : 0;
        int projectedUsedCommandPower = m_baseUsedCommandPower + selectedCost;
        return projectedUsedCommandPower;
    }

    // 튜토리얼 분기 조건(CommandPowerInsufficientForPickedHull) — 팝업이 열려 있고 선택된 함체가 지휘력 최대치를 넘기면 true
    private bool IsSelectedHullOverCommandPower()
    {
        if (gameObject.activeInHierarchy == false) return false;

        int projectedUsedCommandPower = GetProjectedUsedCommandPower();
        bool isOverCommandPower = projectedUsedCommandPower > m_maxCommandPower;
        return isOverCommandPower;
    }

    // 선택 후보를 실제로 적용했다고 가정했을 때의 지휘력 사용량을 미리 계산해서 보여줌 — 최대치 초과 시 경고색 + 확인 버튼 비활성화
    // 캐파는 바디(hull) 자신의 statPoint만 반영(M13) — 모듈 구성은 비용에 영향 없음
    private void RefreshCommandPowerPreview()
    {
        if (m_commandPowerRow == null) return;

        int projectedUsedCommandPower = GetProjectedUsedCommandPower();
        bool isOverCommandPower = projectedUsedCommandPower > m_maxCommandPower;

        string projectedUsedText = CommonUtility.FormatNumber(projectedUsedCommandPower);
        string maxCommandPowerText = CommonUtility.FormatNumber(m_maxCommandPower);
        m_commandPowerRow.SetRow("UI_CommandPower", $"{projectedUsedText} / {maxCommandPowerText}", rawValue: true);
        m_commandPowerRow.SetValueColor(CommonUtility.PaletteColor(isOverCommandPower == true ? "Text.Warning" : "Text.Dark1"));
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_commandPowerRow.transform as RectTransform);

        // 언락/배치해제 모드는 지휘력과 무관하게 항상 눌림(언락은 AP 소모, 배치해제는 비용 없음) — 배치 모드에서만 지휘력 초과 여부로 잠금
        if (m_confirmButton != null)
            m_confirmButton.interactable = m_buttonMode != EPickerButtonMode.Place || isOverCommandPower == false;
    }

    // 현재 보유 업적포인트 상시 표시 — 언락 비용(AP)을 감당할 수 있는지 바로 비교해보라는 용도, 선택 함체와 무관하게 항상 같은 값
    private void RefreshAchievementPointRow()
    {
        if (m_achievementPointRow == null) return;

        Commander commander = DataManager.Instance.m_currentCommander;
        int achievementPoint = commander != null ? commander.GetAchievementPoint() : 0;

        string apLabel = LocalizationManager.Instance.Get("UI_AchievementPoint");
        m_achievementPointRow.SetRow(apLabel, CommonUtility.FormatNumber(achievementPoint), rawValue: true, rawLabel: true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_achievementPointRow.transform as RectTransform);
    }

    // 선택된 함체의 스탯을 현재 장착 함체와 비교해서 표시 — InfiniteScrollView가 화면에 보이는 행만 OnStatsItemBind로 바인딩하므로
    // 여기서는 m_statEntries만 갱신하고 Initialize로 스크롤뷰에 개수만 알려줌
    private void RefreshStatsDisplay()
    {
        ModuleData selectedHull = m_hullsCache.Find(p => p.moduleSubType == m_selectedHullSubType);
        if (selectedHull == null)
        {
            m_statEntries.Clear();
            if (m_statsScrollView != null && m_statsRowPrefab != null)
                m_statsScrollView.Initialize(0, m_statsRowPrefab.gameObject);
            if (m_shipDescribeText != null) m_shipDescribeText.text = string.Empty;
            return;
        }

        // 함체는 항상 기본 로드아웃(전 슬롯 레벨1 장착) 기준으로 미리보기 — 언락 여부와 무관하게 동일
        ModuleHullInfo previewModules = FleetComposition.BuildDefaultLevel1Modules(m_selectedHullSubType);
        List<ShipStatRowEntry> entries = ShipStatGaugeBuilder.Build(selectedHull, previewModules);
        AppendRemovedStatEntries(entries);
        m_statEntries = entries;

        if (m_statsScrollView != null && m_statsRowPrefab != null)
            m_statsScrollView.Initialize(m_statEntries.Count, m_statsRowPrefab.gameObject);
        if (m_shipDescribeText != null) m_shipDescribeText.text = LocalizationManager.Instance.Get(selectedHull.descriptionKey);
    }

    // InfiniteScrollView가 dataIndex번 스탯 행을 화면에 배치할 때마다 호출 — 캐시된 m_statEntries로 바인딩
    private void OnStatsItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_statEntries.Count) return;

        UIStatRow row = rowObject.GetComponent<UIStatRow>();
        if (row == null) return;

        ShipStatRowEntry entry = m_statEntries[dataIndex];
        string diffText = BuildDiffText(entry);

        if (entry.isNumericValue == true)
            row.SetStatRow(entry.label, entry.value, diffText);
        else
            row.SetValueOnly(entry.label, entry.rawValueText, diffText);
    }

    // 아키타입 패시브 4단계 — 성능 탭과 별개 탭(패시브 탭)에서 자체 스크롤뷰로 표시. 아직 카운터를 실제로 증가시키는 로직이
    // 전혀 연결돼 있지 않아서(M9~M11에서 아키타입별로 작업 예정) 지금은 전부 0/threshold로 보임. 잠김/해금 판정만 threshold 비교로 정확하게 표시
    private void RefreshPassiveDisplay()
    {
        ModuleData selectedHull = m_hullsCache.Find(p => p.moduleSubType == m_selectedHullSubType);
        m_passiveEntries.Clear();

        if (selectedHull != null && string.IsNullOrEmpty(selectedHull.hullArchetype) == false)
        {
            List<HullArchetypePassiveData> passives = DataManager.Instance.m_dataTableHullArchetypePassive.GetPassivesForArchetype(selectedHull.hullArchetype);
            Commander commander = DataManager.Instance.m_currentCommander;
            bool isHullLocked = IsHullLocked(selectedHull); // 함체 자체를 아직 언락 못 한 상태면 패시브 진행도가 의미 없으니 전부 해금된 것처럼 미리보기

            for (int i = 0; i < passives.Count; i++)
            {
                HullArchetypePassiveData passive = passives[i];
                long currentValue = commander != null ? commander.GetHullArchetypePassiveCounterValue(selectedHull.hullArchetype, passive.counterType) : 0;
                bool isUnlocked = isHullLocked == true || currentValue >= passive.threshold;

                string description = LocalizationManager.Instance.Get(passive.descriptionKey);
                string displayText = isUnlocked == true
                    ? description
                    : $"{description} ({CommonUtility.FormatNumber((int)currentValue)} / {CommonUtility.FormatNumber(passive.threshold)})";

                m_passiveEntries.Add(new PassiveRowEntry { stage = passive.stage, description = displayText, isUnlocked = isUnlocked });
            }
        }

        if (m_passiveScrollView != null && m_passiveRowPrefab != null)
            m_passiveScrollView.Initialize(m_passiveEntries.Count, m_passiveRowPrefab.gameObject);
    }

    // 화면 상단 진척도 게이지 — 패시브 단계는 1→2→3→4 순차 해금이므로, 선택된 함체의 "다음 미달성 단계" 딱 하나만 표시
    // (counter_type이 단계마다 달라도 병렬 트랙이 아니라 순서상 다음 단계 하나). 슬롯이 남으면 숨김
    private void RefreshProgressGauges()
    {
        ModuleData selectedHull = m_hullsCache.Find(p => p.moduleSubType == m_selectedHullSubType);
        List<(string counterType, long currentValue, int threshold, string description)> nextGoals = new();

        if (selectedHull != null && string.IsNullOrEmpty(selectedHull.hullArchetype) == false && IsHullLocked(selectedHull) == false)
        {
            List<HullArchetypePassiveData> passives = DataManager.Instance.m_dataTableHullArchetypePassive.GetPassivesForArchetype(selectedHull.hullArchetype);
            Commander commander = DataManager.Instance.m_currentCommander;

            for (int i = 0; i < passives.Count; i++)
            {
                HullArchetypePassiveData passive = passives[i];
                long currentValue = commander != null ? commander.GetHullArchetypePassiveCounterValue(selectedHull.hullArchetype, passive.counterType) : 0;
                if (currentValue >= passive.threshold) continue; // 이 단계는 이미 달성 — 다음 단계 확인

                string missionLabel = LocalizationManager.Instance.Get($"HullArchetypePassiveMission_{passive.counterType}"); // 효과 설명이 아니라 "무엇을 해야 하는지" 조건 라벨
                nextGoals.Add((passive.counterType, currentValue, passive.threshold, missionLabel));
                break; // 순차 해금 — 가장 빠른 미달성 단계 하나만
            }
        }

        if (m_progressGaugeContainer != null)
            m_progressGaugeContainer.SetActive(nextGoals.Count > 0);

        if (m_progressGaugeSlots == null) return;
        for (int i = 0; i < m_progressGaugeSlots.Length; i++)
        {
            if (m_progressGaugeSlots[i] == null) continue;

            if (i >= nextGoals.Count)
            {
                m_progressGaugeSlots[i].Hide();
                continue;
            }

            var goal = nextGoals[i];
            float ratio = goal.threshold > 0 ? (float)goal.currentValue / goal.threshold : 0f;
            string valueText = $"{CommonUtility.FormatNumber((int)goal.currentValue)} / {CommonUtility.FormatNumber(goal.threshold)}";
            m_progressGaugeSlots[i].SetGauge(ratio, valueText, goal.description);
        }
    }

    // InfiniteScrollView가 dataIndex번 패시브 행을 화면에 배치할 때마다 호출 — 캐시된 m_passiveEntries로 바인딩
    private void OnPassiveItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_passiveEntries.Count) return;

        UIShipPassiveRow row = rowObject.GetComponent<UIShipPassiveRow>();
        if (row == null) return;

        PassiveRowEntry entry = m_passiveEntries[dataIndex];
        row.SetPassiveRow(entry.stage, entry.description, entry.isUnlocked);
    }

    // 현재 장착 함체엔 있었지만 선택한 함체에는 없어진 스탯(예: 미사일 미장착으로 변경)도 0값 항목으로 추가해
    // 감소(-) diff를 보여준다 — Normal 모드(게이지형 DPS 등)만 대상. None/Reverse는 0으로 대체할 의미있는 표시값이 없어 제외
    private void AppendRemovedStatEntries(List<ShipStatRowEntry> entries)
    {
        if (m_currentEntriesByLabel == null) return;

        HashSet<string> selectedLabels = new();
        for (int i = 0; i < entries.Count; i++)
            selectedLabels.Add(entries[i].label);

        foreach (KeyValuePair<string, ShipStatRowEntry> pair in m_currentEntriesByLabel)
        {
            if (selectedLabels.Contains(pair.Key) == true) continue;
            if (pair.Value.isNumericValue == false) continue;

            ShipStatRowEntry removedEntry = pair.Value;
            removedEntry.value = 0f;
            removedEntry.rawValueText = "0.0";
            removedEntry.compareValue = 0f;
            entries.Add(removedEntry);
        }
    }

    // 라벨로 현재 장착 함체의 동일 스탯을 찾아 증감을 리치텍스트로 포맷.
    // 현재 함체에 아예 없던 스탯(예: 미사일 미장착 → 장착)은 기준값 0으로 취급해 신규 획득으로 표시
    private string BuildDiffText(ShipStatRowEntry entry)
    {
        return ShipStatGaugeBuilder.BuildDiffText(entry, m_currentEntriesByLabel);
    }

    // 패널 공용 버튼(m_confirmButton) 클릭 — 지금 모드(RefreshUnlockState가 갱신)에 따라 배치/배치해제/언락 중 하나로 위임
    private void OnActionButtonClicked()
    {
        if (m_buttonMode == EPickerButtonMode.Unlock)
        {
            OnUnlockButtonClicked();
            return;
        }
        if (m_buttonMode == EPickerButtonMode.Unplace)
        {
            OnRemoveButtonClicked();
            return;
        }
        OnPlaceClicked();
    }

    private void OnPlaceClicked()
    {
        if (m_onConfirm == null) return;

        // 서버 응답이 올 때까지 팝업을 열어둠 — 중복 확정을 막기 위해 확인 버튼만 잠근다
        if (m_confirmButton != null)
            m_confirmButton.interactable = false;
        m_onConfirm(m_selectedHullSubType, OnConfirmProcessed);
    }

    // 호출부가 요청 처리를 마치고 알림 — closePicker=true면 닫고, false(재시도 가능한 실패)면 선택/미리보기를 유지한 채 버튼 상태를 복원
    private void OnConfirmProcessed(bool closePicker)
    {
        if (closePicker == true)
        {
            Close();
            return;
        }

        RefreshCommandPowerPreview();
    }

    private void OnCancelClicked()
    {
        System.Action onCancel = m_onCancel;
        Close();
        if (onCancel != null) onCancel();
    }
}
