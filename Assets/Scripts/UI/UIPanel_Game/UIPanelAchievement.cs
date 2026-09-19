// 업적 패널 — 일일 업적(오늘 UTC 기준, 자정 리셋) 카테고리를 상단에, 영구 업적 카테고리를 그 아래에 이어 붙여
// 하나의 InfiniteScrollView에 순서대로 펼쳐서 표시. 두 목록은 서버 API/데이터 테이블이 서로 다르지만
// 화면에서는 카테고리 헤더로만 시각적으로 구분됨(일일/영구 조건타입 값이 겹칠 수 있어 isDaily로 구분)
// 완료된 업적은 유저가 직접 "받기"를 눌러야 업적포인트가 지급됨(자동 지급 아님)
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelAchievement : UIPanelBase
{
    [SerializeField] private InfiniteScrollView m_scrollView;
    [SerializeField] private UIAchievementRow m_rowPrefab;
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private GameObject m_titleRedDot;
    [SerializeField] private RowLabelValue m_achievementPointRow; // 타이틀 아래 보유 업적포인트 상시 표시
    [SerializeField] private Button m_claimAllButton;
    [SerializeField] private TMP_Text m_claimAllButtonText;
    [SerializeField] private Button m_findNextUnclaimedButton;
    [SerializeField] private TMP_Text m_findNextUnclaimedButtonText;

    // 일일 업적 카테고리 표시 순서 — 상단에 먼저 표시됨. 일일 업적은 CellClear/EventCell/ZoneClearTotal만 지원(DailyAchievementService 참고)
    private static readonly EAchievementConditionType[] k_dailyCategoryOrder =
    {
        EAchievementConditionType.CellClear,
        EAchievementConditionType.EventCell,
        EAchievementConditionType.ZoneClearTotal,
    };

    // 영구 업적 카테고리 표시 순서 — 일일 목록 다음에 이어서 표시됨
    private static readonly EAchievementConditionType[] k_categoryOrder =
    {
        EAchievementConditionType.CellClear,
        EAchievementConditionType.EventCell,
        EAchievementConditionType.ZoneClearTotal,
        EAchievementConditionType.ZoneClearSpecific,
        EAchievementConditionType.ZoneFullClear,
        EAchievementConditionType.CommanderLevel,
        EAchievementConditionType.CommandPower,
        EAchievementConditionType.TacticPower,
        EAchievementConditionType.ExplorationPointTotal,
        EAchievementConditionType.HullTierCount,
        EAchievementConditionType.ModuleTierCount,
        EAchievementConditionType.HullUnlocked,
    };

    // 평탄화 리스트의 행 1개 — 헤더 또는 업적 항목 중 하나만 유효. category는 헤더/항목 둘 다 채워서 항목→헤더 역참조(레드닷 재계산)에 사용
    // isDaily는 일일/영구 조건타입 값이 겹칠 수 있어(둘 다 CellClear 등 사용) 카테고리 매칭 및 수령 API 라우팅에 반드시 필요
    private class AchievementListEntry
    {
        public bool isHeader;
        public bool isDaily;
        public EAchievementConditionType category;
        public string headerLabel;
        public bool headerHasUnclaimed; // 헤더 전용 — 이 카테고리 안에 완료+미수령 항목이 있는지
        public AchievementData data;
        public int currentValue;
        public bool isClaimed;
        public bool isVipClaimed;
    }

    private readonly List<AchievementListEntry> m_flattenedList = new();
    private int m_lastFoundUnclaimedIndex = -1; // "다음 찾기" 순환 검색 시작점 — 목록을 새로 받을 때마다 리셋

    private Dictionary<string, AchievementStatus> m_lastPermanentStatusById;
    private Dictionary<string, DailyAchievementStatus> m_lastDailyStatusById;
    private bool m_permanentListReceived;
    private bool m_dailyListReceived;

    private void Awake()
    {
        if (m_scrollView != null)
            m_scrollView.onItemBind = OnItemBind;
        if (m_claimAllButton != null)
            m_claimAllButton.onClick.AddListener(OnClaimAllClicked);
        if (m_findNextUnclaimedButton != null)
            m_findNextUnclaimedButton.onClick.AddListener(OnFindNextUnclaimedClicked);
    }

    public override void InitializeUIPanel()
    {
        EventManager.Subscribe_AchievementPointChanged(OnAchievementPointChanged);
        RefreshAchievementPointRow();

        if (m_titleText != null)
            CommonUtility.SetUILocText(m_titleText, "UI_Achievement");
        if (m_claimAllButtonText != null)
            CommonUtility.SetUILocText(m_claimAllButtonText, "UIAchievement_ClaimAllButton");
        if (m_findNextUnclaimedButtonText != null)
            CommonUtility.SetUILocText(m_findNextUnclaimedButtonText, "UIAchievement_FindNextButton");
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_AchievementPointChanged(OnAchievementPointChanged);
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
        m_achievementPointRow.SetRow("UIPanelFleet_AchievementPoint", $": {ownedAchievementPoint}", rawValue: true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_achievementPointRow.transform as RectTransform);
    }

    // 업적 보상 수령으로 업적포인트가 티어4 함체 언락 비용에 도달했으면, 패널을 닫는 순간 함체 언락 안내 튜토리얼 시작(최초 1회, 판정은 TutorialManager)
    public override void OnHideUIPanel()
    {
        base.OnHideUIPanel();

        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        int ownedAchievementPoint = commander.GetAchievementPoint();
        TutorialManager.Instance.TryStartHullUnlockTutorial(ownedAchievementPoint);
    }

    public override void OnShowUIPanel()
    {
        base.OnShowUIPanel();

        m_permanentListReceived = false;
        m_dailyListReceived = false;
        NetworkManager.Instance.GetAchievementList(new GetAchievementListRequest(), OnGetAchievementListResponse);
        NetworkManager.Instance.GetDailyAchievementList(new GetDailyAchievementListRequest(), OnGetDailyAchievementListResponse);
    }

    private void OnGetAchievementListResponse(ApiResponse<GetAchievementListResponse> response)
    {
        if (response.errorCode != 0)
        {
            Debug.LogError($"[UIPanelAchievement] GetAchievementList 실패: {response.errorCode}");
            return;
        }

        m_lastPermanentStatusById = new Dictionary<string, AchievementStatus>();
        if (response.data.achievements != null)
        {
            foreach (AchievementStatus status in response.data.achievements)
                m_lastPermanentStatusById[status.achievementId] = status;
        }
        m_permanentListReceived = true;
        TryBuildFlattenedListWhenReady();
    }

    private void OnGetDailyAchievementListResponse(ApiResponse<GetDailyAchievementListResponse> response)
    {
        if (response.errorCode != 0)
        {
            Debug.LogError($"[UIPanelAchievement] GetDailyAchievementList 실패: {response.errorCode}");
            return;
        }

        m_lastDailyStatusById = new Dictionary<string, DailyAchievementStatus>();
        if (response.data.achievements != null)
        {
            foreach (DailyAchievementStatus status in response.data.achievements)
                m_lastDailyStatusById[status.achievementId] = status;
        }
        m_dailyListReceived = true;
        TryBuildFlattenedListWhenReady();
    }

    // 일일/영구 두 목록이 모두 도착한 뒤에만 하나의 리스트로 합쳐서 그림 — 한쪽만 먼저 와서 목록이 절반만 보이는 걸 방지
    private void TryBuildFlattenedListWhenReady()
    {
        if (m_permanentListReceived == false || m_dailyListReceived == false) return;

        BuildFlattenedList(m_lastPermanentStatusById, m_lastDailyStatusById);
        RefreshUnclaimedIndicators();
        m_lastFoundUnclaimedIndex = -1;

        if (m_scrollView != null && m_rowPrefab != null)
            m_scrollView.Initialize(m_flattenedList.Count, m_rowPrefab.gameObject);
    }

    private void BuildFlattenedList(Dictionary<string, AchievementStatus> permanentStatusById, Dictionary<string, DailyAchievementStatus> dailyStatusById)
    {
        m_flattenedList.Clear();

        List<AchievementData> dailyAchievements = DataManager.Instance.m_dataTableDailyAchievement.GetDailyAchievementDataList();
        foreach (EAchievementConditionType category in k_dailyCategoryOrder)
        {
            List<AchievementData> categoryAchievements = dailyAchievements.FindAll(a => a.conditionType == category);
            if (categoryAchievements.Count == 0) continue;

            AchievementListEntry headerEntry = new AchievementListEntry
            {
                isHeader = true,
                isDaily = true,
                category = category,
                headerLabel = LocalizationManager.Instance.Get(GetCategoryLocKey(true, category)),
            };
            m_flattenedList.Add(headerEntry);

            foreach (AchievementData data in categoryAchievements)
            {
                dailyStatusById.TryGetValue(data.achievementId, out DailyAchievementStatus status);
                int currentValue = status != null ? status.currentValue : 0;
                bool isClaimed = status != null && status.isClaimed;
                bool isVipClaimed = status != null && status.isVipClaimed;

                m_flattenedList.Add(new AchievementListEntry
                {
                    isHeader = false,
                    isDaily = true,
                    category = category,
                    data = data,
                    currentValue = currentValue,
                    isClaimed = isClaimed,
                    isVipClaimed = isVipClaimed,
                });

                if (IsEntryUnclaimed(currentValue, data.threshold, isClaimed, isVipClaimed) == true)
                    headerEntry.headerHasUnclaimed = true;
            }
        }

        List<AchievementData> permanentAchievements = DataManager.Instance.m_dataTableAchievement.GetAchievementDataList();
        foreach (EAchievementConditionType category in k_categoryOrder)
        {
            List<AchievementData> categoryAchievements = permanentAchievements.FindAll(a => a.conditionType == category);
            if (categoryAchievements.Count == 0) continue;

            AchievementListEntry headerEntry = new AchievementListEntry
            {
                isHeader = true,
                isDaily = false,
                category = category,
                headerLabel = LocalizationManager.Instance.Get(GetCategoryLocKey(false, category)),
            };
            m_flattenedList.Add(headerEntry);

            foreach (AchievementData data in categoryAchievements)
            {
                permanentStatusById.TryGetValue(data.achievementId, out AchievementStatus status);
                int currentValue = status != null ? status.currentValue : 0;
                bool isClaimed = status != null && status.isClaimed;
                bool isVipClaimed = status != null && status.isVipClaimed;

                m_flattenedList.Add(new AchievementListEntry
                {
                    isHeader = false,
                    isDaily = false,
                    category = category,
                    data = data,
                    currentValue = currentValue,
                    isClaimed = isClaimed,
                    isVipClaimed = isVipClaimed,
                });

                if (IsEntryUnclaimed(currentValue, data.threshold, isClaimed, isVipClaimed) == true)
                    headerEntry.headerHasUnclaimed = true;
            }
        }
    }

    // 카테고리 헤더 레드닷(headerHasUnclaimed 이미 계산됨) 종합해서 타이틀 레드닷 + Commander 캐시(다른 화면 버튼 레드닷)까지 갱신
    private void RefreshUnclaimedIndicators()
    {
        bool anyUnclaimed = false;
        foreach (AchievementListEntry entry in m_flattenedList)
        {
            if (entry.isHeader == true && entry.headerHasUnclaimed == true)
            {
                anyUnclaimed = true;
                break;
            }
        }

        if (m_titleRedDot != null)
            m_titleRedDot.SetActive(anyUnclaimed);
        if (m_claimAllButton != null)
            m_claimAllButton.interactable = anyUnclaimed;
        if (m_findNextUnclaimedButton != null)
            m_findNextUnclaimedButton.interactable = anyUnclaimed;

        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander != null)
            commander.UpdateHasUnclaimedAchievement(anyUnclaimed);
    }

    private void OnItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_flattenedList.Count) return;

        UIAchievementRow row = rowObject.GetComponent<UIAchievementRow>();
        if (row == null) return;

        AchievementListEntry entry = m_flattenedList[dataIndex];
        if (entry.isHeader == true)
            row.SetupHeader(entry.headerLabel, entry.headerHasUnclaimed);
        else
            row.SetupItem(entry.data, entry.currentValue, entry.isClaimed, entry.isVipClaimed, IAPManager.Instance.IsVipActive(), OnClaimClicked);
    }

    private void OnClaimClicked(string achievementId, bool claimVip)
    {
        AchievementListEntry entry = m_flattenedList.Find(e => e.isHeader == false && e.data.achievementId == achievementId);
        if (entry == null) return;

        if (entry.isDaily == true)
        {
            ClaimDailyAchievementRequest request = new ClaimDailyAchievementRequest { achievementId = achievementId, claimVip = claimVip };
            NetworkManager.Instance.ClaimDailyAchievement(request, response =>
            {
                if (response.errorCode != 0)
                {
                    Debug.LogError($"[UIPanelAchievement] ClaimDailyAchievement 실패: {response.errorCode}");
                    return;
                }
                ApplyClaimResult(achievementId, claimVip, response.data.achievementPointRemain);
            });
        }
        else
        {
            ClaimAchievementRequest request = new ClaimAchievementRequest { achievementId = achievementId, claimVip = claimVip };
            NetworkManager.Instance.ClaimAchievement(request, response =>
            {
                if (response.errorCode != 0)
                {
                    Debug.LogError($"[UIPanelAchievement] ClaimAchievement 실패: {response.errorCode}");
                    return;
                }
                ApplyClaimResult(achievementId, claimVip, response.data.achievementPointRemain);
            });
        }
    }

    // 일일/영구 수령 응답 처리가 완전히 동일해서 공용화 — achievementId로 엔트리를 다시 찾는 이유는 클로저로 넘긴 entry가 그 사이 리스트 갱신으로 바뀌었을 가능성을 배제하기 위함
    private void ApplyClaimResult(string achievementId, bool claimVip, int achievementPointRemain)
    {
        AchievementListEntry entry = m_flattenedList.Find(e => e.isHeader == false && e.data.achievementId == achievementId);
        if (entry != null)
        {
            if (claimVip == true)
                entry.isVipClaimed = true;
            else
                entry.isClaimed = true;
        }

        RecomputeCategoryHeaderUnclaimed(entry);
        RefreshUnclaimedIndicators();

        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander != null)
            commander.UpdateAchievementPoint(achievementPointRemain);

        if (m_scrollView != null)
            m_scrollView.RefreshVisible();
    }

    // claimedEntry가 속한 카테고리의 헤더 엔트리를 찾아 headerHasUnclaimed를 다시 계산 — 수령으로 그 카테고리의 마지막 미수령 항목이 없어졌을 수 있어서 단순 false 대입이 아니라 재순회 필요
    private void RecomputeCategoryHeaderUnclaimed(AchievementListEntry claimedEntry)
    {
        if (claimedEntry == null) return;

        AchievementListEntry headerEntry = m_flattenedList.Find(e => e.isHeader == true && e.isDaily == claimedEntry.isDaily && e.category == claimedEntry.category);
        if (headerEntry == null) return;

        headerEntry.headerHasUnclaimed = ComputeCategoryHasUnclaimed(claimedEntry.isDaily, claimedEntry.category);
    }

    private bool ComputeCategoryHasUnclaimed(bool isDaily, EAchievementConditionType category)
    {
        foreach (AchievementListEntry entry in m_flattenedList)
        {
            if (entry.isHeader == true || entry.isDaily != isDaily || entry.category != category) continue;
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

    // 일일 → 영구 순서로 이어서 호출 — 두 API가 같은 commander.achievementPoint를 각자 트랜잭션으로 갱신하므로
    // 동시 호출 시 나중에 도착한 응답의 remain 값이 먼저 처리된 쪽의 지급분을 반영 못 할 수 있어 순차 처리로 확정값 보장.
    // VIP 유저는 일반+VIP 보상이 서버에서 한 번에 같이 스윕되는데 응답엔 VIP 수령 id 목록이 없어 로컬 패치 대신 목록을 통째로 재조회해서 정확한 상태로 맞춤
    private void OnClaimAllClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);

        NetworkManager.Instance.ClaimAllDailyAchievements(new ClaimAllDailyAchievementsRequest(), dailyResponse =>
        {
            int dailyGranted = 0;
            if (dailyResponse.errorCode == 0)
                dailyGranted = dailyResponse.data.totalAchievementPointGranted;
            else
                Debug.LogError($"[UIPanelAchievement] ClaimAllDailyAchievements 실패: {dailyResponse.errorCode}");

            NetworkManager.Instance.ClaimAllAchievements(new ClaimAllAchievementsRequest(), permanentResponse =>
            {
                int permanentGranted = 0;
                if (permanentResponse.errorCode == 0)
                {
                    permanentGranted = permanentResponse.data.totalAchievementPointGranted;
                    Commander commander = DataManager.Instance.m_currentCommander;
                    if (commander != null)
                        commander.UpdateAchievementPoint(permanentResponse.data.achievementPointRemain);
                }
                else
                {
                    Debug.LogError($"[UIPanelAchievement] ClaimAllAchievements 실패: {permanentResponse.errorCode}");
                }

                int totalGranted = dailyGranted + permanentGranted;
                if (totalGranted <= 0) return;

                m_permanentListReceived = false;
                m_dailyListReceived = false;
                NetworkManager.Instance.GetAchievementList(new GetAchievementListRequest(), OnGetAchievementListResponse);
                NetworkManager.Instance.GetDailyAchievementList(new GetDailyAchievementListRequest(), OnGetDailyAchievementListResponse);

                UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
                {
                    message = LocalizationManager.Instance.Get("DailyBonus_DescAchievement", totalGranted),
                    autoCloseSec = 3f,
                });
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
            m_scrollView.JumpToIndex(index);
            return;
        }
    }

    private string GetCategoryLocKey(bool isDaily, EAchievementConditionType category)
    {
        string prefix = isDaily == true ? "UIDailyAchievement_Category_" : "UIAchievement_Category_";
        return $"{prefix}{category}";
    }
}
