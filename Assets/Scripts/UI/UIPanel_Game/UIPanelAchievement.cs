// 업적 패널 — 카테고리 헤더 + 업적 행을 하나의 InfiniteScrollView에 순서대로 펼쳐서 표시.
// 완료된 업적은 유저가 직접 "받기"를 눌러야 업적포인트가 지급됨(자동 지급 아님)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelAchievement : UIPanelBase
{
    [SerializeField] private InfiniteScrollView m_scrollView;
    [SerializeField] private UIAchievementRow m_rowPrefab;
    [SerializeField] private GameObject m_titleRedDot;
    [SerializeField] private RowLabelValue m_achievementPointRow; // 타이틀 아래 보유 업적포인트 상시 표시

    // 카테고리(조건 타입) 표시 순서 — 여기 순서가 곧 패널에 보이는 헤더 순서
    private static readonly EAchievementConditionType[] k_categoryOrder =
    {
        EAchievementConditionType.CellClear,
        EAchievementConditionType.EventCell,
        EAchievementConditionType.ZoneClearTotal,
        EAchievementConditionType.ZoneClearSpecific,
        EAchievementConditionType.CommanderLevel,
        EAchievementConditionType.CommandPower,
        EAchievementConditionType.TacticPower,
        EAchievementConditionType.ExplorationPointTotal,
        EAchievementConditionType.HullTierCount,
        EAchievementConditionType.ModuleTierCount,
    };

    // 평탄화 리스트의 행 1개 — 헤더 또는 업적 항목 중 하나만 유효. category는 헤더/항목 둘 다 채워서 항목→헤더 역참조(레드닷 재계산)에 사용
    private class AchievementListEntry
    {
        public bool isHeader;
        public EAchievementConditionType category;
        public string headerLabel;
        public bool headerHasUnclaimed; // 헤더 전용 — 이 카테고리 안에 완료+미수령 항목이 있는지
        public AchievementData data;
        public int currentValue;
        public bool isClaimed;
    }

    private readonly List<AchievementListEntry> m_flattenedList = new();

    private void Awake()
    {
        if (m_scrollView != null)
            m_scrollView.onItemBind = OnItemBind;
    }

    public override void InitializeUIPanel()
    {
        EventManager.Subscribe_AchievementPointChanged(OnAchievementPointChanged);
        RefreshAchievementPointRow();
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
        m_achievementPointRow.SetRow("UIPanelFleet_AchievementPoint", ownedAchievementPoint.ToString(), rawValue: true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_achievementPointRow.transform as RectTransform);
    }

    public override void OnShowUIPanel()
    {
        base.OnShowUIPanel();
        RequestAchievementList();
    }

    private void RequestAchievementList()
    {
        NetworkManager.Instance.GetAchievementList(new GetAchievementListRequest(), OnGetAchievementListResponse);
    }

    private void OnGetAchievementListResponse(ApiResponse<GetAchievementListResponse> response)
    {
        if (response.errorCode != 0)
        {
            Debug.LogError($"[UIPanelAchievement] GetAchievementList 실패: {response.errorCode}");
            return;
        }

        Dictionary<string, AchievementStatus> statusById = new();
        if (response.data.achievements != null)
        {
            foreach (AchievementStatus status in response.data.achievements)
                statusById[status.achievementId] = status;
        }

        BuildFlattenedList(statusById);
        RefreshUnclaimedIndicators();

        if (m_scrollView != null && m_rowPrefab != null)
            m_scrollView.Initialize(m_flattenedList.Count, m_rowPrefab.gameObject);
    }

    private void BuildFlattenedList(Dictionary<string, AchievementStatus> statusById)
    {
        m_flattenedList.Clear();

        List<AchievementData> allAchievements = DataManager.Instance.m_dataTableAchievement.GetAchievementDataList();

        foreach (EAchievementConditionType category in k_categoryOrder)
        {
            List<AchievementData> categoryAchievements = allAchievements.FindAll(a => a.conditionType == category);
            if (categoryAchievements.Count == 0) continue;

            AchievementListEntry headerEntry = new AchievementListEntry
            {
                isHeader = true,
                category = category,
                headerLabel = LocalizationManager.Instance.Get(GetCategoryLocKey(category)),
            };
            m_flattenedList.Add(headerEntry);

            foreach (AchievementData data in categoryAchievements)
            {
                statusById.TryGetValue(data.achievementId, out AchievementStatus status);
                int currentValue = status != null ? status.currentValue : 0;
                bool isClaimed = status != null && status.isClaimed;

                m_flattenedList.Add(new AchievementListEntry
                {
                    isHeader = false,
                    category = category,
                    data = data,
                    currentValue = currentValue,
                    isClaimed = isClaimed,
                });

                if (isClaimed == false && currentValue >= data.threshold)
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
            row.SetupItem(entry.data, entry.currentValue, entry.isClaimed, OnClaimClicked);
    }

    private void OnClaimClicked(string achievementId)
    {
        ClaimAchievementRequest request = new ClaimAchievementRequest { achievementId = achievementId };
        NetworkManager.Instance.ClaimAchievement(request, response =>
        {
            if (response.errorCode != 0)
            {
                Debug.LogError($"[UIPanelAchievement] ClaimAchievement 실패: {response.errorCode}");
                return;
            }

            AchievementListEntry entry = m_flattenedList.Find(e => e.isHeader == false && e.data.achievementId == achievementId);
            if (entry != null)
                entry.isClaimed = true;

            RecomputeCategoryHeaderUnclaimed(entry);
            RefreshUnclaimedIndicators();

            Commander commander = DataManager.Instance.m_currentCommander;
            if (commander != null)
                commander.UpdateAchievementPoint(response.data.achievementPointRemain);

            if (m_scrollView != null)
                m_scrollView.RefreshVisible();
        });
    }

    // claimedEntry가 속한 카테고리의 헤더 엔트리를 찾아 headerHasUnclaimed를 처음부터 다시 계산 — 수령으로 그 카테고리의 마지막 미수령 항목이 없어졌을 수 있어서 단순 false 대입이 아니라 재순회 필요
    private void RecomputeCategoryHeaderUnclaimed(AchievementListEntry claimedEntry)
    {
        if (claimedEntry == null) return;

        AchievementListEntry headerEntry = m_flattenedList.Find(e => e.isHeader == true && e.category == claimedEntry.category);
        if (headerEntry == null) return;

        bool categoryHasUnclaimed = false;
        foreach (AchievementListEntry entry in m_flattenedList)
        {
            if (entry.isHeader == true || entry.category != claimedEntry.category) continue;
            if (entry.isClaimed == false && entry.currentValue >= entry.data.threshold)
            {
                categoryHasUnclaimed = true;
                break;
            }
        }
        headerEntry.headerHasUnclaimed = categoryHasUnclaimed;
    }

    private string GetCategoryLocKey(EAchievementConditionType category)
    {
        return $"UIAchievement_Category_{category}";
    }
}
