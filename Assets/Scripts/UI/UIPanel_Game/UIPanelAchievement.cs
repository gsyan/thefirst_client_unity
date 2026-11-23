// 업적 패널 — 일일퀘스트 탭(오늘 UTC 기준 3개: 쉬움/중간/어려움, 고정 행)과 업적 탭(영구 업적, InfiniteScrollView) 중 하나만 활성화
// 일일퀘스트와 영구 업적은 서버 API/데이터 테이블이 서로 달라 각자 응답이 도착하는 대로 따로 그림
// 완료된 업적은 유저가 직접 "받기"를 눌러야 업적포인트가 지급됨(자동 지급 아님)
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelAchievement : UIPanelBase
{
    [Header("탭")]
    [SerializeField] private Button m_tabDailyButton;
    [SerializeField] private Button m_tabAchievementButton;
    [SerializeField] private GameObject m_tabDailyRoot;
    [SerializeField] private GameObject m_tabAchievementRoot;
    [SerializeField] private UIDailyStreakBanner m_streakBanner; // 일일퀘스트 탭 상단 연속 달성 배너(ContinueDescription에 부착)

    [Header("일일퀘스트 탭")]
    [SerializeField] private Transform m_dailyRowsParent; // 자식 UIAchievementRow를 하이어라키 순서대로 수집 — 쉬움 → 중간 → 어려움 순으로 채워짐

    [Header("업적 탭")]
    [SerializeField] private InfiniteScrollView m_scrollView;
    [SerializeField] private UIAchievementRow m_rowPrefab;
    [SerializeField] private GameObject m_titleRedDot;
    [SerializeField] private RowLabelValue m_achievementPointRow; // 타이틀 아래 보유 업적포인트 상시 표시
    [SerializeField] private Button m_claimAllButton;
    [SerializeField] private TMP_Text m_claimAllButtonText;
    [SerializeField] private Button m_findNextUnclaimedButton;
    [SerializeField] private TMP_Text m_findNextUnclaimedButtonText;
    [SerializeField] private ToggleButton m_hideCompletedToggle; // 수령까지 완전히 끝난 업적을 목록에서 숨김(미수령 보상은 항상 노출)

    private const string k_hideCompletedPrefKey = "UIPanelAchievement_HideCompleted";
    private static readonly Color k_tabUnselectedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    private bool m_hideCompleted;

    // 영구 업적 카테고리 표시 순서 — 아키타입 업적은 이 뒤에 아키타입별 묶음으로 표시
    private static readonly EAchievementConditionType[] k_categoryOrder =
    {
        EAchievementConditionType.CellClear,
        EAchievementConditionType.EventCell,
        EAchievementConditionType.ZoneClearTotal,
        EAchievementConditionType.ZoneClearSpecific,
        EAchievementConditionType.ZoneFullClear,
        EAchievementConditionType.CommanderLevel,
        EAchievementConditionType.CommandPower,
        EAchievementConditionType.MineralTotal,
    };

    // 업적/일일퀘스트 항목 1개(또는 영구 업적 목록의 카테고리 헤더) — 헤더면 isHeader만 유효. groupKey는 항목→헤더 역참조(레드닷 재계산)에 사용
    private class AchievementListEntry
    {
        public bool isHeader;
        public string groupKey; // 조건 타입 이름 또는 함체 아키타입
        public string headerLabel;
        public bool headerHasUnclaimed; // 헤더 전용 — 이 카테고리 안에 완료+미수령 항목이 있는지
        public AchievementData data;
        public int currentValue;
        public bool isClaimed;
        public bool isVipClaimed;
    }

    private readonly List<AchievementListEntry> m_flattenedList = new(); // 영구 업적(헤더+항목)
    private readonly List<int> m_visibleIndices = new(); // 스크롤뷰 dataIndex → m_flattenedList 인덱스 매핑(완료 숨기기 필터 반영본)
    private readonly List<AchievementListEntry> m_dailyEntries = new(); // 오늘의 일일퀘스트(쉬움/중간/어려움 순)
    private readonly List<UIAchievementRow> m_dailyRows = new(); // m_dailyRowsParent의 자식 행(Awake에서 수집)
    private int m_dailyStreakDays; // 서버가 내려준 오늘 보상 적용 연속 일수
    private int m_lastFoundUnclaimedIndex = -1; // "다음 찾기" 순환 검색 시작점 — 목록을 새로 받을 때마다 리셋

    private void Awake()
    {
        if (m_scrollView != null)
            m_scrollView.onItemBind = OnItemBind;
        if (m_claimAllButton != null)
            m_claimAllButton.onClick.AddListener(OnClaimAllClicked);
        if (m_findNextUnclaimedButton != null)
            m_findNextUnclaimedButton.onClick.AddListener(OnFindNextUnclaimedClicked);
        if (m_tabDailyButton != null)
            m_tabDailyButton.onClick.AddListener(OnTabDailyClicked);
        if (m_tabAchievementButton != null)
            m_tabAchievementButton.onClick.AddListener(OnTabAchievementClicked);
        if (m_dailyRowsParent != null)
            m_dailyRowsParent.GetComponentsInChildren(true, m_dailyRows);
    }

    public override void InitializeUIPanel()
    {
        EventManager.Subscribe_AchievementPointChanged(OnAchievementPointChanged);
        RefreshAchievementPointRow();

        SetTabButtonText(m_tabDailyButton, "UIAchievement_TabDaily");
        SetTabButtonText(m_tabAchievementButton, "UIAchievement_TabAchievement");
        if (m_claimAllButtonText != null)
            CommonUtility.SetUILocText(m_claimAllButtonText, "UIAchievement_ClaimAllButton");
        if (m_findNextUnclaimedButtonText != null)
            CommonUtility.SetUILocText(m_findNextUnclaimedButtonText, "UIAchievement_FindNextButton");

        if (m_hideCompletedToggle != null)
        {
            m_hideCompletedToggle.SetTexts("UIAchievement_HideCompletedToggle", "");
            m_hideCompleted = PlayerPrefs.GetInt(k_hideCompletedPrefKey, 1) == 1; // 기본값 켜짐
            m_hideCompletedToggle.SetSelected(m_hideCompleted);
            m_hideCompletedToggle.button.onClick.AddListener(OnHideCompletedToggleClicked);
        }
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_AchievementPointChanged(OnAchievementPointChanged);
    }

    private void OnTabDailyClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        SelectTab(true);
    }

    private void OnTabAchievementClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        SelectTab(false);
    }

    // 두 탭 루트 중 하나만 활성화하고 탭 버튼 색으로 선택 상태 표시
    private void SelectTab(bool isDailyTab)
    {
        if (m_tabDailyRoot != null)
            m_tabDailyRoot.SetActive(isDailyTab);
        if (m_tabAchievementRoot != null)
            m_tabAchievementRoot.SetActive(isDailyTab == false);

        SetTabButtonSelected(m_tabDailyButton, isDailyTab == true);
        SetTabButtonSelected(m_tabAchievementButton, isDailyTab == false);

        // 비활성 상태에서 초기화된 스크롤뷰는 뷰포트 크기를 못 읽으므로 탭이 열리는 시점에 다시 초기화
        if (isDailyTab == false)
            RefreshScrollView();
    }

    private void SetTabButtonText(Button button, string locKey)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            CommonUtility.SetUILocText(label, locKey);
    }

    // 선택된 탭은 프리팹 원래 색, 선택 안 된 탭은 어둡게 — 둘 다 클릭 가능 상태 유지
    private void SetTabButtonSelected(Button button, bool isSelected)
    {
        if (button == null || button.targetGraphic == null) return;
        button.targetGraphic.color = isSelected == true ? Color.white : k_tabUnselectedColor;
    }

    private void RefreshScrollView()
    {
        if (m_scrollView == null || m_rowPrefab == null) return;
        // 업적 탭이 비활성이면 스크롤뷰 Awake 전이라 초기화 불가 — 탭이 열릴 때 SelectTab에서 다시 호출됨
        if (m_scrollView.gameObject.activeInHierarchy == false) return;
        m_scrollView.Initialize(m_visibleIndices.Count, m_rowPrefab.gameObject);
    }

    private void OnHideCompletedToggleClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);

        m_hideCompleted = m_hideCompleted == false;
        m_hideCompletedToggle.SetSelected(m_hideCompleted);
        PlayerPrefs.SetInt(k_hideCompletedPrefKey, m_hideCompleted ? 1 : 0);
        PlayerPrefs.Save();

        RebuildVisibleIndices();
        RefreshScrollView();
    }

    // 다른 화면(함체 언락 등)에서 업적포인트가 바뀌면 이 패널이 열려있지 않아도 안전하게 호출됨
    private void OnAchievementPointChanged(int achievementPoint)
    {
        RefreshAchievementPointRow();
    }

    private void RefreshAchievementPointRow()
    {
        if (m_achievementPointRow == null) return;

        Commander commander = DataManager.Instance.m_currentCommander;
        int ownedAchievementPoint = commander != null ? commander.GetAchievementPoint() : 0;
        string ownedAchievementPointText = CommonUtility.FormatNumber(ownedAchievementPoint);
        m_achievementPointRow.SetRow("UIPanelFleet_AchievementPoint", $": {ownedAchievementPointText}", rawValue: true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_achievementPointRow.transform as RectTransform);
    }

    public override void OnShowUIPanel()
    {
        base.OnShowUIPanel();

        SelectTab(true);
        RequestPermanentList();
        RequestDailyList();
    }

    private void RequestPermanentList()
    {
        NetworkManager.Instance.GetAchievementList(new GetAchievementListRequest(), OnGetAchievementListResponse);
    }

    private void RequestDailyList()
    {
        NetworkManager.Instance.GetDailyAchievementList(new GetDailyAchievementListRequest(), OnGetDailyAchievementListResponse);
    }

    private void OnGetAchievementListResponse(ApiResponse<GetAchievementListResponse> response)
    {
        if (response.errorCode != 0)
        {
            Debug.LogError($"[UIPanelAchievement] GetAchievementList 실패: {response.errorCode}");
            return;
        }

        Dictionary<string, AchievementStatus> statusById = new Dictionary<string, AchievementStatus>();
        if (response.data.achievements != null)
        {
            foreach (AchievementStatus status in response.data.achievements)
                statusById[status.achievementId] = status;
        }

        BuildFlattenedList(statusById);
        RefreshUnclaimedIndicators();
        RebuildVisibleIndices();
        m_lastFoundUnclaimedIndex = -1;
        RefreshScrollView();
    }

    private void OnGetDailyAchievementListResponse(ApiResponse<GetDailyAchievementListResponse> response)
    {
        if (response.errorCode != 0)
        {
            Debug.LogError($"[UIPanelAchievement] GetDailyAchievementList 실패: {response.errorCode}");
            return;
        }

        Dictionary<string, DailyAchievementStatus> statusById = new Dictionary<string, DailyAchievementStatus>();
        if (response.data.achievements != null)
        {
            foreach (DailyAchievementStatus status in response.data.achievements)
                statusById[status.achievementId] = status;
        }

        m_dailyStreakDays = response.data.streakDays;

        BuildDailyEntries(statusById);
        RefreshDailyRows();
        RefreshUnclaimedIndicators();
    }

    private int GetDifficultyRank(string difficulty)
    {
        if (difficulty == "easy") return 0;
        if (difficulty == "normal") return 1;
        if (difficulty == "hard") return 2;
        return 3;
    }

    // 서버가 내려준 오늘의 일일퀘스트(id 목록)만 골라 난이도 순으로 정렬
    private void BuildDailyEntries(Dictionary<string, DailyAchievementStatus> statusById)
    {
        m_dailyEntries.Clear();

        List<AchievementData> dailyAchievements = DataManager.Instance.m_dataTableDailyAchievement.GetDailyAchievementDataList();
        foreach (AchievementData data in dailyAchievements)
        {
            if (statusById.TryGetValue(data.achievementId, out DailyAchievementStatus status) == false) continue;

            m_dailyEntries.Add(new AchievementListEntry
            {
                isHeader = false,
                data = data,
                currentValue = status.currentValue,
                isClaimed = status.isClaimed,
                isVipClaimed = status.isVipClaimed,
            });
        }

        m_dailyEntries.Sort((a, b) => GetDifficultyRank(a.data.difficulty).CompareTo(GetDifficultyRank(b.data.difficulty)));
    }

    private void RefreshDailyRows()
    {
        bool isVipActive = IAPManager.Instance.IsVipActive();
        int bonusPercent = UIDailyStreakBanner.GetBonusPercent(m_dailyStreakDays);
        for (int i = 0; i < m_dailyRows.Count; i++)
        {
            UIAchievementRow row = m_dailyRows[i];
            if (row == null) continue;

            if (i >= m_dailyEntries.Count)
            {
                row.gameObject.SetActive(false);
                continue;
            }

            AchievementListEntry entry = m_dailyEntries[i];
            row.SetupItem(entry.data, entry.currentValue, entry.isClaimed, entry.isVipClaimed, isVipActive, OnClaimClicked, bonusPercent);
            row.SetDifficultyLabel(entry.data.difficulty);
        }

        RefreshStreakBanner();
    }

    // 서버의 연속 일수 갱신 조건과 동일 — 오늘의 일일퀘스트 전부 일반 보상 수령(VIP 수령은 무관)
    private bool IsTodayDailyAllClaimed()
    {
        if (m_dailyEntries.Count == 0) return false;
        for (int i = 0; i < m_dailyEntries.Count; i++)
        {
            if (m_dailyEntries[i].isClaimed == false) return false;
        }
        return true;
    }

    private void RefreshStreakBanner()
    {
        if (m_streakBanner == null) return;
        bool isTodayCompleted = IsTodayDailyAllClaimed();
        m_streakBanner.Refresh(m_dailyStreakDays, isTodayCompleted);
    }

    // m_hideCompleted가 켜져 있으면 "완료 + 전부 수령됨"(IsEntryUnclaimed == false)인 항목을 걸러냄 —
    // 미수령 보상(VIP 전용 미수령 포함)은 절대 숨기지 않음. 헤더는 그 카테고리에 보일 항목이 하나도 없으면 같이 숨김
    private void RebuildVisibleIndices()
    {
        m_visibleIndices.Clear();

        int headerIndex = -1;
        bool headerHasVisibleItem = false;

        for (int i = 0; i < m_flattenedList.Count; i++)
        {
            AchievementListEntry entry = m_flattenedList[i];
            if (entry.isHeader == true)
            {
                if (headerIndex >= 0 && headerHasVisibleItem == true)
                    m_visibleIndices.Add(headerIndex);

                headerIndex = i;
                headerHasVisibleItem = false;
                continue;
            }

            bool isCompleted = entry.currentValue >= entry.data.threshold
                && IsEntryUnclaimed(entry.currentValue, entry.data.threshold, entry.isClaimed, entry.isVipClaimed) == false;
            if (m_hideCompleted == true && isCompleted == true) continue;

            headerHasVisibleItem = true;
            m_visibleIndices.Add(i);
        }

        if (headerIndex >= 0 && headerHasVisibleItem == true)
            m_visibleIndices.Add(headerIndex);

        m_visibleIndices.Sort();
    }

    private void BuildFlattenedList(Dictionary<string, AchievementStatus> statusById)
    {
        m_flattenedList.Clear();

        List<AchievementData> permanentAchievements = DataManager.Instance.m_dataTableAchievement.GetAchievementDataList();
        foreach (EAchievementConditionType category in k_categoryOrder)
        {
            List<AchievementData> categoryAchievements = permanentAchievements.FindAll(a => a.conditionType == category);
            AddGroup(category.ToString(), GetCategoryLocKey(category), categoryAchievements, statusById);
        }

        // 아키타입 업적 — 테이블에 처음 나오는 순서대로 아키타입별 헤더로 묶음
        List<AchievementData> archetypeAchievements = permanentAchievements.FindAll(a => CommonUtility.IsHullArchetypeAchievement(a.conditionType));
        List<string> hullArchetypes = new List<string>();
        foreach (AchievementData data in archetypeAchievements)
        {
            if (hullArchetypes.Contains(data.conditionParam) == false)
                hullArchetypes.Add(data.conditionParam);
        }
        foreach (string hullArchetype in hullArchetypes)
        {
            List<AchievementData> groupAchievements = archetypeAchievements.FindAll(a => a.conditionParam == hullArchetype);
            AddGroup(hullArchetype, $"HullArchetype_{hullArchetype}", groupAchievements, statusById);
        }
    }

    // 헤더 1개와 그 아래 항목들을 m_flattenedList에 추가 — 항목이 없으면 헤더도 추가하지 않음
    private void AddGroup(string groupKey, string headerLocKey, List<AchievementData> achievements, Dictionary<string, AchievementStatus> statusById)
    {
        if (achievements.Count == 0) return;

        AchievementListEntry headerEntry = new AchievementListEntry
        {
            isHeader = true,
            groupKey = groupKey,
            headerLabel = LocalizationManager.Instance.Get(headerLocKey),
        };
        m_flattenedList.Add(headerEntry);

        foreach (AchievementData data in achievements)
        {
            statusById.TryGetValue(data.achievementId, out AchievementStatus status);
            int currentValue = status != null ? status.currentValue : 0;
            bool isClaimed = status != null && status.isClaimed;
            bool isVipClaimed = status != null && status.isVipClaimed;

            m_flattenedList.Add(new AchievementListEntry
            {
                isHeader = false,
                groupKey = groupKey,
                data = data,
                currentValue = currentValue,
                isClaimed = isClaimed,
                isVipClaimed = isVipClaimed,
            });

            if (IsEntryUnclaimed(currentValue, data.threshold, isClaimed, isVipClaimed) == true)
                headerEntry.headerHasUnclaimed = true;
        }
    }

    // 업적 탭(타이틀 레드닷/전체 받기/다음 찾기)은 영구 업적 기준, Commander 캐시(다른 화면 버튼 레드닷)는 일일퀘스트까지 합쳐서 갱신
    private void RefreshUnclaimedIndicators()
    {
        bool permanentUnclaimed = false;
        foreach (AchievementListEntry entry in m_flattenedList)
        {
            if (entry.isHeader == true && entry.headerHasUnclaimed == true)
            {
                permanentUnclaimed = true;
                break;
            }
        }

        bool dailyUnclaimed = false;
        foreach (AchievementListEntry entry in m_dailyEntries)
        {
            if (IsEntryUnclaimed(entry.currentValue, entry.data.threshold, entry.isClaimed, entry.isVipClaimed) == true)
            {
                dailyUnclaimed = true;
                break;
            }
        }

        if (m_titleRedDot != null)
            m_titleRedDot.SetActive(permanentUnclaimed);
        if (m_claimAllButton != null)
            m_claimAllButton.interactable = permanentUnclaimed;
        if (m_findNextUnclaimedButton != null)
            m_findNextUnclaimedButton.interactable = permanentUnclaimed;

        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander != null)
            commander.UpdateHasUnclaimedAchievement(permanentUnclaimed || dailyUnclaimed);
    }

    private void OnItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_visibleIndices.Count) return;

        UIAchievementRow row = rowObject.GetComponent<UIAchievementRow>();
        if (row == null) return;

        AchievementListEntry entry = m_flattenedList[m_visibleIndices[dataIndex]];
        if (entry.isHeader == true)
            row.SetupHeader(entry.headerLabel, entry.headerHasUnclaimed);
        else
            row.SetupItem(entry.data, entry.currentValue, entry.isClaimed, entry.isVipClaimed, IAPManager.Instance.IsVipActive(), OnClaimClicked);
    }

    private void OnClaimClicked(string achievementId, bool claimVip)
    {
        AchievementListEntry dailyEntry = m_dailyEntries.Find(e => e.data.achievementId == achievementId);
        if (dailyEntry != null)
        {
            ClaimDailyAchievementRequest request = new ClaimDailyAchievementRequest { achievementId = achievementId, claimVip = claimVip };
            NetworkManager.Instance.ClaimDailyAchievement(request, response =>
            {
                if (response.errorCode != 0)
                {
                    Debug.LogError($"[UIPanelAchievement] ClaimDailyAchievement 실패: {response.errorCode}");
                    return;
                }
                ApplyDailyClaimResult(achievementId, claimVip, response.data.achievementPointRemain);
            });
            return;
        }

        AchievementListEntry permanentEntry = m_flattenedList.Find(e => e.isHeader == false && e.data.achievementId == achievementId);
        if (permanentEntry == null) return;

        ClaimAchievementRequest permanentRequest = new ClaimAchievementRequest { achievementId = achievementId, claimVip = claimVip };
        NetworkManager.Instance.ClaimAchievement(permanentRequest, response =>
        {
            if (response.errorCode != 0)
            {
                Debug.LogError($"[UIPanelAchievement] ClaimAchievement 실패: {response.errorCode}");
                return;
            }
            ApplyPermanentClaimResult(achievementId, claimVip, response.data.achievementPointRemain);
        });
    }

    // achievementId로 엔트리를 다시 찾는 이유는 클로저로 넘긴 entry가 그 사이 목록 갱신으로 바뀌었을 가능성을 배제하기 위함
    private void ApplyDailyClaimResult(string achievementId, bool claimVip, int achievementPointRemain)
    {
        AchievementListEntry entry = m_dailyEntries.Find(e => e.data.achievementId == achievementId);
        if (entry != null)
        {
            if (claimVip == true)
                entry.isVipClaimed = true;
            else
                entry.isClaimed = true;
        }

        RefreshDailyRows();
        RefreshUnclaimedIndicators();
        UpdateCommanderAchievementPoint(achievementPointRemain);
    }

    private void ApplyPermanentClaimResult(string achievementId, bool claimVip, int achievementPointRemain)
    {
        AchievementListEntry entry = m_flattenedList.Find(e => e.isHeader == false && e.data.achievementId == achievementId);
        if (entry != null)
        {
            if (claimVip == true)
                entry.isVipClaimed = true;
            else
                entry.isClaimed = true;
        }

        RecomputeGroupHeaderUnclaimed(entry);
        RefreshUnclaimedIndicators();
        UpdateCommanderAchievementPoint(achievementPointRemain);

        // 방금 수령으로 완료 처리된 항목이 숨김 대상이 될 수 있어(개수가 줄 수 있음) 개수를 갱신하되,
        // Initialize(스크롤 최상단 리셋)가 아니라 UpdateTotalCount로 수령 버튼을 누른 그 스크롤 위치를 유지
        RebuildVisibleIndices();
        if (m_scrollView != null)
            m_scrollView.UpdateTotalCount(m_visibleIndices.Count);
    }

    private void UpdateCommanderAchievementPoint(int achievementPointRemain)
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander != null)
            commander.UpdateAchievementPoint(achievementPointRemain);
    }

    // claimedEntry가 속한 카테고리의 헤더 엔트리를 찾아 headerHasUnclaimed를 다시 계산 — 수령으로 그 카테고리의 마지막 미수령 항목이 없어졌을 수 있어서 단순 false 대입이 아니라 재순회 필요
    private void RecomputeGroupHeaderUnclaimed(AchievementListEntry claimedEntry)
    {
        if (claimedEntry == null) return;

        AchievementListEntry headerEntry = m_flattenedList.Find(e => e.isHeader == true && e.groupKey == claimedEntry.groupKey);
        if (headerEntry == null) return;

        headerEntry.headerHasUnclaimed = ComputeGroupHasUnclaimed(claimedEntry.groupKey);
    }

    private bool ComputeGroupHasUnclaimed(string groupKey)
    {
        foreach (AchievementListEntry entry in m_flattenedList)
        {
            if (entry.isHeader == true || entry.groupKey != groupKey) continue;
            if (IsEntryUnclaimed(entry.currentValue, entry.data.threshold, entry.isClaimed, entry.isVipClaimed) == true)
                return true;
        }
        return false;
    }

    // 완료 + (일반 미수령 또는 VIP 활성 상태에서 VIP 미수령) — VIP 보상은 VIP 활성 유저에게만 "미수령"으로 취급(비VIP는 애초에 대상 아님)
    private bool IsEntryUnclaimed(int currentValue, int threshold, bool isClaimed, bool isVipClaimed)
    {
        if (currentValue < threshold) return false;

        bool normalUnclaimed = isClaimed == false;
        bool vipUnclaimed = IAPManager.Instance.IsVipActive() == true && isVipClaimed == false;
        return normalUnclaimed || vipUnclaimed;
    }

    // 업적 탭의 전체 받기 — 영구 업적만 수령(일일퀘스트는 각 행의 받기 버튼).
    // VIP 유저는 일반+VIP 보상이 서버에서 한 번에 같이 스윕되는데 응답엔 VIP 수령 id 목록이 없어 로컬 패치 대신 목록을 통째로 재조회해서 정확한 상태로 맞춤
    private void OnClaimAllClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);

        NetworkManager.Instance.ClaimAllAchievements(new ClaimAllAchievementsRequest(), response =>
        {
            if (response.errorCode != 0)
            {
                Debug.LogError($"[UIPanelAchievement] ClaimAllAchievements 실패: {response.errorCode}");
                return;
            }

            int totalGranted = response.data.totalAchievementPointGranted;
            UpdateCommanderAchievementPoint(response.data.achievementPointRemain);
            if (totalGranted <= 0) return;

            RequestPermanentList();

            UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
            {
                message = LocalizationManager.Instance.Get("DailyBonus_DescAchievement", totalGranted),
                autoCloseSec = 3f,
            });
        });
    }

    // m_lastFoundUnclaimedIndex 다음 지점부터 순환 탐색 — 완료+미수령 항목을 찾으면 그 위치로 스크롤 이동하고 다음 검색 시작점으로 기억
    private void OnFindNextUnclaimedClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);

        if (m_flattenedList.Count == 0 || m_scrollView == null) return;

        int startIndex = (m_lastFoundUnclaimedIndex + 1) % m_flattenedList.Count;
        for (int i = 0; i < m_flattenedList.Count; i++)
        {
            int index = (startIndex + i) % m_flattenedList.Count;
            AchievementListEntry entry = m_flattenedList[index];
            if (entry.isHeader == true) continue;
            if (IsEntryUnclaimed(entry.currentValue, entry.data.threshold, entry.isClaimed, entry.isVipClaimed) == false) continue;

            m_lastFoundUnclaimedIndex = index;

            // 미수령 항목은 필터로 숨겨지지 않으므로 항상 m_visibleIndices 안에 존재 — 스크롤뷰 좌표계로 변환해서 이동
            int visiblePos = m_visibleIndices.IndexOf(index);
            if (visiblePos >= 0)
                m_scrollView.JumpToIndex(visiblePos);
            return;
        }
    }

    private string GetCategoryLocKey(EAchievementConditionType category)
    {
        return $"UIAchievement_Category_{category}";
    }
}
