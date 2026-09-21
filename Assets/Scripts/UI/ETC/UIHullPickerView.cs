// 함체 선택 팝업 — UIPanelFleet 프리팹에 내장된 오버레이(별도 UIManager 팝업 스택 대상 아님). 리스트에서 함체를
// 고르고 확인/취소로 결정만 알려줄 뿐, 실제로 어느 슬롯에 어떻게 적용할지는 모른다(호출부가 콜백에서 처리) — 재사용성을
// 위해 이 컴포넌트는 "선택기" 역할만 담당한다. 선택된 함체의 스탯을 현재 장착 함체와 비교해서 함께 보여준다
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIHullPickerView : MonoBehaviour
{
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private TMP_Text m_statsTitleText;
    [SerializeField] private TMP_Text m_confirmButtonText;
    [SerializeField] private TMP_Text m_cancelButtonText;
    [SerializeField] private InfiniteScrollView m_scrollView;
    [SerializeField] private UIAvailableHullRow m_rowPrefab;
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private Button m_cancelButton;

    [SerializeField] private UIStatRow m_statsRowPrefab; // 선택된 함체의 스탯 — Column_Stats와 동일한 구조/프리팹 재사용
    [SerializeField] private InfiniteScrollView m_statsScrollView;

    [SerializeField] private RowLabelValue m_commandPowerRow; // 선택 후보를 적용했을 때의 지휘력 미리보기 — UIPanelFleet 성능 컬럼의 지휘력 행과 동일한 구성

    [SerializeField] private RawImage m_previewImage; // 선택된 함체의 3D 바디 미리보기 — ShipPreviewManager가 렌더링한 텍스처

    [Header("모듈 슬롯 개수 표시 — 순수 정보용(Button 비활성)")]
    [SerializeField] private TMP_Text m_beamSlotCountText;
    [SerializeField] private TMP_Text m_missileSlotCountText;
    [SerializeField] private TMP_Text m_hangarSlotCountText;
    [SerializeField] private TMP_Text m_shieldSlotCountText;
    [SerializeField] private TMP_Text m_interceptorSlotCountText;

    private readonly List<ModuleData> m_hullsCache = new();
    private List<ShipStatRowEntry> m_statEntries = new();
    private Dictionary<string, ShipStatRowEntry> m_currentEntriesByLabel; // 비교 기준(현재 장착 함체) — 라벨로 조회

    private string m_selectedHullSubType;
    private ModuleHullInfo m_currentModules; // 이 슬롯에 실제로 장착돼 있던 모듈 — 함체 변경 시 슬롯 유지 계산의 기준
    private int m_currentSlotCommandCost; // 이 슬롯이 지금 실제로 쓰고 있는 지휘력 — 각 후보 함체 행의 증감(+/-) 표시 기준
    private int m_baseUsedCommandPower; // 이 슬롯이 점유 중이던 지휘력을 미리 뺀 값 — 후보 함체 비용만 더하면 미리보기 완성
    private int m_maxCommandPower;
    private System.Action<string> m_onConfirm; // 확인 시 선택된 hullSubType 전달
    private System.Action m_onCancel;
    private System.Func<RectTransform> m_lowestLockedHullTargetResolver; // 등록/해제 시 같은 델리게이트 인스턴스를 써야 하므로 보관
    private System.Func<bool> m_overCommandPowerEvaluator;

    private void Awake()
    {
        if (m_confirmButton != null)
            m_confirmButton.onClick.AddListener(OnConfirmClicked);
        if (m_cancelButton != null)
            m_cancelButton.onClick.AddListener(OnCancelClicked);
        if (m_scrollView != null)
            m_scrollView.onItemBind = OnItemBind;
        if (m_statsScrollView != null)
            m_statsScrollView.onItemBind = OnStatsItemBind;

        // 한 번 세팅되면 바뀌지 않는 정적 라벨 — Open()마다 반복 세팅하지 않고 Awake에서 1회만 처리
        if (m_statsTitleText != null)
            CommonUtility.SetUILocText(m_statsTitleText, "UIFleet_StatsTitle");
        if (m_confirmButtonText != null)
            CommonUtility.SetUILocText(m_confirmButtonText, "UI_Confirm");
        if (m_cancelButtonText != null)
            CommonUtility.SetUILocText(m_cancelButtonText, "UI_Cancel");

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

    // 언락 안 됐고 선행조건이 충족된 함체 중 티어가 가장 낮은(같으면 목록 앞쪽) 행의 언락 버튼 — 그 행이 화면에 바인딩돼 있지 않으면 null
    private RectTransform ResolveLowestLockedHullUnlockButton()
    {
        if (m_scrollView == null) return null;

        int targetDataIndex = -1;
        int lowestTier = int.MaxValue;
        for (int i = 0; i < m_hullsCache.Count; i++)
        {
            ModuleData hull = m_hullsCache[i];
            if (IsHullLocked(hull) == false) continue;
            if (IsUnlockPrerequisiteMet(hull) == false) continue;

            int tier = CommonUtility.ParseTier(hull.moduleSubType);
            if (tier >= lowestTier) continue;

            lowestTier = tier;
            targetDataIndex = i;
        }
        if (targetDataIndex < 0) return null;

        RectTransform unlockButtonRect = null;
        m_scrollView.ForEachVisibleItem((dataIndex, rowObject) =>
        {
            if (dataIndex != targetDataIndex) return;

            UIAvailableHullRow row = rowObject.GetComponent<UIAvailableHullRow>();
            if (row != null) unlockButtonRect = row.GetVisibleUnlockButtonRect();
        });
        return unlockButtonRect;
    }

    // availableHulls: 고를 수 있는 함체 목록, currentHull: 비교 기준이 되는 현재 장착 함체(빈 슬롯이면 null → 비교 없이 수치만 표시)
    // currentModules: 그 슬롯에 실제로 장착된 모듈 구성(로드아웃) — null이면 currentHull의 기본 장착 구성으로 비교
    // currentSlotCommandCost: 이 슬롯이 지금 실제로 쓰고 있는 지휘력 — 리스트 각 행의 증감(+/-) 표시 기준
    // baseUsedCommandPower: 이 슬롯이 점유 중이던 몫을 이미 뺀 사용 지휘력(호출부가 계산해서 넘김)
    public void Open(List<ModuleData> availableHulls, ModuleData currentHull, ModuleHullInfo currentModules, int currentSlotCommandCost, int baseUsedCommandPower, int maxCommandPower, System.Action<string> onConfirm, System.Action onCancel = null)
    {
        m_hullsCache.Clear();
        m_hullsCache.AddRange(availableHulls);
        m_selectedHullSubType = currentHull != null ? currentHull.moduleSubType : null;
        m_currentModules = currentModules;
        m_currentSlotCommandCost = currentSlotCommandCost;
        m_baseUsedCommandPower = baseUsedCommandPower;
        m_maxCommandPower = maxCommandPower;
        m_onConfirm = onConfirm;
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
        RefreshCommandPowerPreview();
        RefreshPreview();
        RefreshModuleSlotCounts();
    }

    public void Close()
    {
        gameObject.SetActive(false);
        m_onConfirm = null;
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
        ShipPreviewManager.Instance.ShowHull(selectedHull);
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
        ModuleHullInfo keptModulesForRow = GetKeptModules(hullSubType);
        int projectedCost = composition != null ? composition.ComputeProjectedSlotCommandCost(hullSubType, keptModulesForRow) : hull.statPoint;
        int deltaCost = projectedCost - m_currentSlotCommandCost;

        bool isLocked = IsHullLocked(hull);
        row.Setup(hull, deltaCost, isLocked, OnHullClicked, OnHullUnlockClicked);
        row.SetSelectedAvailableHullRow(hullSubType == m_selectedHullSubType);

        LayoutRebuilder.ForceRebuildLayoutImmediate(rowObject.transform as RectTransform);
    }

    private void OnHullClicked(ModuleData hull)
    {
        m_selectedHullSubType = hull.moduleSubType;
        m_scrollView.RefreshVisible(); // 재바인드되며 OnItemBind가 다시 불려 하이라이트가 새 선택으로 갱신됨
        RefreshStatsDisplay();
        RefreshCommandPowerPreview();
        RefreshPreview();
        RefreshModuleSlotCounts();
    }

    // unlockAchievementPointCost가 0보다 크면 티어4+ 언락 대상 — 아직 언락 안 됐으면 잠김
    private bool IsHullLocked(ModuleData hull)
    {
        if (hull == null || hull.unlockAchievementPointCost <= 0) return false;
        Commander commander = DataManager.Instance.m_currentCommander;
        return commander == null || commander.IsHullUnlocked(hull.moduleSubType) == false;
    }

    // 서버 FleetService.validateUnlockPrerequisite와 동일 규칙(값은 서버 ACHIEVEMENT_UNLOCK_MIN_HULL_TIER와 일치시켜야 함) —
    // gen=1(기본 제공) 함체만 선행조건 적용. 기본형은 이전 티어 기본형이, 실드/요격체 변형은 같은 티어 기본형이 선행 언락돼 있어야 함
    private const int ACHIEVEMENT_UNLOCK_MIN_HULL_TIER = 4;

    private bool IsUnlockPrerequisiteMet(ModuleData hull)
    {
        if (CommonUtility.ParseGen(hull.moduleSubType) != 1) return true;

        int tier = CommonUtility.ParseTier(hull.moduleSubType);
        int[] slots = CommonUtility.ParseHullSlotComposition(hull.moduleSubType);
        bool hasShield = slots[3] > 0;
        bool hasInterceptor = slots[4] > 0;
        bool isBaseVariant = hasShield == false && hasInterceptor == false;
        if (isBaseVariant == true && tier <= ACHIEVEMENT_UNLOCK_MIN_HULL_TIER) return true;

        int prerequisiteTier = isBaseVariant == true ? tier - 1 : tier;
        ModuleData prerequisiteHull = m_hullsCache.Find(p =>
        {
            int[] pSlots = CommonUtility.ParseHullSlotComposition(p.moduleSubType);
            return CommonUtility.ParseGen(p.moduleSubType) == 1
                && CommonUtility.ParseTier(p.moduleSubType) == prerequisiteTier
                && pSlots[3] == 0 && pSlots[4] == 0;
        });
        if (prerequisiteHull == null) return true;

        Commander commander = DataManager.Instance.m_currentCommander;
        return commander != null && commander.IsHullUnlocked(prerequisiteHull.moduleSubType) == true;
    }

    // 언락 버튼 클릭 — 선행조건 먼저 검사(서버 왕복 없이 즉시 차단), 통과 시 함체명/비용 확인 팝업 후 확정 시 서버에 업적포인트 소모 요청
    // cost를 CostStruct로 넘기면 UIPopupConfirm이 부족 시 빨간색 표기 + 확인 버튼 비활성을 알아서 처리함
    private void OnHullUnlockClicked(ModuleData hull)
    {
        if (IsUnlockPrerequisiteMet(hull) == false)
        {
            UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
            {
                message = LocalizationManager.Instance.Get("UIHullPicker_UnlockPrerequisiteFailMessage"),
                onConfirm = () => { },
            });
            return;
        }

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message = string.Format(LocalizationManager.Instance.Get("UIHullPicker_UnlockConfirmMessage"), hull.moduleSubType),
            cost = new CostStruct(ECostType.AchievementPoint, hull.unlockAchievementPointCost),
            onConfirm = () => RequestUnlockHull(hull.moduleSubType),
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
                if (response.errorCode == (int)ServerErrorCode.UNLOCK_HULL_FAIL_PREREQUISITE_NOT_UNLOCKED)
                {
                    UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
                    {
                        message = LocalizationManager.Instance.Get("UIHullPicker_UnlockPrerequisiteFailMessage"),
                        onConfirm = () => { },
                    });
                }
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
            RefreshCommandPowerPreview();
        });
    }

    // 선택된 함체를 이 슬롯에 적용했다고 가정했을 때의 총 지휘력 사용량
    private int GetProjectedUsedCommandPower()
    {
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        ModuleHullInfo keptModules = GetKeptModules(m_selectedHullSubType);
        int selectedCost = composition != null ? composition.ComputeProjectedSlotCommandCost(m_selectedHullSubType, keptModules) : 0;
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
    // 슬롯이 이미 점유 중이던 모듈(m_currentModules)은 새 함체의 슬롯 범위 안에서 그대로 유지되므로, 정적 statPoint가 아니라
    // 실제 유지될 모듈 구성(GetKeptModules) 기준으로 계산해야 정확함 — 서버 FleetService.placeFleetShip과 동일 규칙
    private void RefreshCommandPowerPreview()
    {
        if (m_commandPowerRow == null) return;

        int projectedUsedCommandPower = GetProjectedUsedCommandPower();
        bool isOverCommandPower = projectedUsedCommandPower > m_maxCommandPower;

        m_commandPowerRow.SetRow("UI_CommandPower", $"{projectedUsedCommandPower} / {m_maxCommandPower}", rawValue: true);
        m_commandPowerRow.SetValueColor(CommonUtility.PaletteColor(isOverCommandPower == true ? "Text.Warning" : "Text.Dark1"));
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_commandPowerRow.transform as RectTransform);

        ModuleData selectedHull = m_hullsCache.Find(p => p.moduleSubType == m_selectedHullSubType);
        bool isSelectedHullLocked = IsHullLocked(selectedHull);
        if (m_confirmButton != null)
            m_confirmButton.interactable = isOverCommandPower == false && isSelectedHullLocked == false;
    }

    // m_currentModules를 targetHullSubType의 슬롯 범위로 필터링한 결과 — 리스트 각 행의 비용 미리보기와 선택된 함체의 미리보기/Confirm에 공용으로 사용
    // m_currentModules가 null이면(원래 비어있던 슬롯) null을 그대로 반환해 기본 로드아웃(무기 없음) 분기를 그대로 타게 함
    private ModuleHullInfo GetKeptModules(string targetHullSubType)
    {
        return FleetComposition.FilterModulesForNewHull(m_currentModules, targetHullSubType);
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
            return;
        }

        ModuleHullInfo keptModules = GetKeptModules(m_selectedHullSubType);
        List<ShipStatRowEntry> entries = ShipStatGaugeBuilder.Build(selectedHull, keptModules);
        AppendRemovedStatEntries(entries);
        m_statEntries = entries;

        if (m_statsScrollView != null && m_statsRowPrefab != null)
            m_statsScrollView.Initialize(m_statEntries.Count, m_statsRowPrefab.gameObject);
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

    private void OnConfirmClicked()
    {
        System.Action<string> onConfirm = m_onConfirm;
        string selected = m_selectedHullSubType;
        Close();
        if (onConfirm != null) onConfirm(selected);
    }

    private void OnCancelClicked()
    {
        System.Action onCancel = m_onCancel;
        Close();
        if (onCancel != null) onCancel();
    }
}
