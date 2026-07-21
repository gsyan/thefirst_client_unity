// PVP/Zone 랭킹 탭 - 탭 버튼 전환 + InfiniteScrollView 랭킹 리스트
// 서버가 내려준 nextUpdatedAt 기준으로 캐시 재활용, 만료 시 재요청
// 함선 시스템 대격변으로 전체 주석처리 — UITabPvp(부모 탭)와 함께 비활성화(삭제 아님)
#if false
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;


public class UITabPvp_Rank : UITabBase
{
    private enum RankingType { Pvp, Zone }

    [Header("Ranking UI")]
    [SerializeField] private TMP_Text m_periodText;
    [SerializeField] private ToggleButton m_pvpToggle;
    [SerializeField] private ToggleButton m_zoneToggle;
    [SerializeField] private ScrollViewRankingItem m_myRankInfo;
    [SerializeField] private InfiniteScrollView m_rankingScrollView;
    [SerializeField] private GameObject m_rankingItemPrefab;

    private const int PAGE_SIZE = 50;

    private readonly Dictionary<int, RankingEntry> m_pvpCache = new Dictionary<int, RankingEntry>();
    private readonly Dictionary<int, RankingEntry> m_zoneCache = new Dictionary<int, RankingEntry>();
    private readonly HashSet<int> m_requestingPages = new HashSet<int>();

    private RankingEntry m_pvpMyInfo;
    private RankingEntry m_zoneMyInfo;

    private DateTime? m_pvpNextUpdatedAt;
    private DateTime? m_zoneNextUpdatedAt;

    private RankingType m_currentType = RankingType.Pvp;

    public override void InitializeUITab()
    {
        if (m_rankingScrollView != null)
        {
            m_rankingScrollView.onItemBind = OnItemBind;
            m_rankingScrollView.onNeedData = OnNeedData;
        }
        if (m_pvpToggle != null)
            m_pvpToggle.button.onClick.AddListener(() => OnRankingTabClicked(RankingType.Pvp));
        if (m_zoneToggle != null)
            m_zoneToggle.button.onClick.AddListener(() => OnRankingTabClicked(RankingType.Zone));
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        LoadTab(RankingType.Pvp);
        UpdateToggleState();
    }

    private void UpdateToggleState()
    {
        if (m_pvpToggle != null)  m_pvpToggle.SetSelected(m_currentType == RankingType.Pvp);
        if (m_zoneToggle != null) m_zoneToggle.SetSelected(m_currentType == RankingType.Zone);
    }

    private void OnRankingTabClicked(RankingType type)
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_currentType == type) return;
        LoadTab(type);
    }

    private void LoadTab(RankingType type)
    {
        m_currentType = type;
        UpdateToggleState();
        m_requestingPages.Clear();

        if (type == RankingType.Pvp)
        {
            if (m_pvpCache.Count > 0 && IsExpired(m_pvpNextUpdatedAt) == false)
            {
                RefreshMyInfoName(m_pvpMyInfo);
                ApplyMyInfo(m_pvpMyInfo);
                m_rankingScrollView.RefreshVisible();
                return;
            }
            m_pvpCache.Clear();
            var req = new PvpRankingRequest { offset = 0, limit = PAGE_SIZE };
            NetworkManager.Instance.PvpRanking(req, OnPvpInitResponse);
        }
        else if (type == RankingType.Zone)
        {
            if (m_zoneCache.Count > 0 && IsExpired(m_zoneNextUpdatedAt) == false)
            {
                RefreshMyInfoName(m_zoneMyInfo);
                ApplyMyInfo(m_zoneMyInfo);
                if (m_periodText != null) m_periodText.gameObject.SetActive(false);
                m_rankingScrollView.RefreshVisible();
                return;
            }
            m_zoneCache.Clear();
            if (m_periodText != null) m_periodText.gameObject.SetActive(false);
            var req = new ZoneRankingRequest { offset = 0, limit = PAGE_SIZE };
            NetworkManager.Instance.ZoneRanking(req, OnZoneInitResponse);
        }
    }

    private void OnPvpInitResponse(ApiResponse<PvpRankingResponse> response)
    {
        if (m_currentType != RankingType.Pvp) return;
        if (response == null || response.errorCode != 0 || response.data == null)
        {
            Debug.LogWarning("[UITabPvp_Rank] PVP 랭킹 로드 실패");
            return;
        }

        if (response.data.items != null)
        {
            for (int i = 0; i < response.data.items.Count; i++)
                m_pvpCache[i] = response.data.items[i];
        }

        m_pvpMyInfo = response.data.myInfo;
        m_pvpNextUpdatedAt = ParseUpdatedAt(response.data.lastUpdatedAt);

        ApplyMyInfo(m_pvpMyInfo);
        UpdatePvpHeader(response.data);
        m_rankingScrollView.Initialize(response.data.totalCount, m_rankingItemPrefab);
    }

    private void UpdatePvpHeader(PvpRankingResponse data)
    {
        if (m_periodText == null) return;
        bool hasStart = string.IsNullOrEmpty(data.seasonStartTime) == false;
        bool hasEnd = string.IsNullOrEmpty(data.seasonEndTime) == false;
        m_periodText.gameObject.SetActive(hasStart && hasEnd);
        if (hasStart && hasEnd)
            m_periodText.text = $"{data.seasonStartTime} ~ {data.seasonEndTime}";
    }

    private void OnZoneInitResponse(ApiResponse<ZoneRankingResponse> response)
    {
        if (m_currentType != RankingType.Zone) return;
        if (response == null || response.errorCode != 0 || response.data == null)
        {
            Debug.LogWarning("[UITabPvp_Rank] Zone 랭킹 로드 실패");
            return;
        }

        if (response.data.items != null)
        {
            for (int i = 0; i < response.data.items.Count; i++)
                m_zoneCache[i] = response.data.items[i];
        }

        m_zoneMyInfo = response.data.myInfo;
        m_zoneNextUpdatedAt = ParseUpdatedAt(response.data.lastUpdatedAt);

        ApplyMyInfo(m_zoneMyInfo);
        m_rankingScrollView.Initialize(response.data.totalCount, m_rankingItemPrefab);
    }

    private void OnItemBind(int dataIndex, GameObject itemObj)
    {
        if (itemObj.TryGetComponent<ScrollViewRankingItem>(out var item) == false) return;

        Commander currentCommander = DataManager.Instance.m_currentCommander;
        long myCommanderId = (currentCommander != null && currentCommander.m_commanderInfo != null) ? currentCommander.m_commanderInfo.commanderId : 0;
        Dictionary<int, RankingEntry> cache = GetCurrentCache();

        if (cache.TryGetValue(dataIndex, out RankingEntry entry) == true)
            item.SetData(entry, entry.commanderId == myCommanderId);
        else
            item.SetLoading();
    }

    private void OnNeedData(int startIndex, int count)
    {
        int firstPage = startIndex / PAGE_SIZE;
        int lastPage = (startIndex + count - 1) / PAGE_SIZE;
        Dictionary<int, RankingEntry> cache = GetCurrentCache();
        RankingType capturedType = m_currentType;

        for (int page = firstPage; page <= lastPage; page++)
        {
            int pageOffset = page * PAGE_SIZE;
            if (cache.ContainsKey(pageOffset) == true) continue;
            if (m_requestingPages.Contains(page) == true) continue;

            m_requestingPages.Add(page);
            int captured = page;

            if (capturedType == RankingType.Pvp)
            {
                var req = new PvpRankingRequest { offset = pageOffset, limit = PAGE_SIZE };
                NetworkManager.Instance.PvpRanking(req, res => OnPvpPageResponse(res, captured));
            }
            else
            {
                var req = new ZoneRankingRequest { offset = pageOffset, limit = PAGE_SIZE };
                NetworkManager.Instance.ZoneRanking(req, res => OnZonePageResponse(res, captured));
            }
        }
    }

    private Dictionary<int, RankingEntry> GetCurrentCache()
    {
        if (m_currentType == RankingType.Pvp) return m_pvpCache;
        return m_zoneCache;
    }

    private void OnPvpPageResponse(ApiResponse<PvpRankingResponse> response, int page)
    {
        m_requestingPages.Remove(page);
        if (response == null || response.errorCode != 0) return;
        if (response.data == null || response.data.items == null) return;
        int baseIndex = page * PAGE_SIZE;
        for (int i = 0; i < response.data.items.Count; i++)
            m_pvpCache[baseIndex + i] = response.data.items[i];
        if (m_currentType == RankingType.Pvp)
            m_rankingScrollView.RefreshVisible();
    }

    private void OnZonePageResponse(ApiResponse<ZoneRankingResponse> response, int page)
    {
        m_requestingPages.Remove(page);
        if (response == null || response.errorCode != 0) return;
        if (response.data == null || response.data.items == null) return;
        int baseIndex = page * PAGE_SIZE;
        for (int i = 0; i < response.data.items.Count; i++)
            m_zoneCache[baseIndex + i] = response.data.items[i];
        if (m_currentType == RankingType.Zone)
            m_rankingScrollView.RefreshVisible();
    }

    // 캐시 히트 시 내 이름만 로컬 DataManager 기준으로 덮어씀 (이름 변경 즉시 반영)
    private void RefreshMyInfoName(RankingEntry info)
    {
        if (info == null) return;
        Commander myChar = DataManager.Instance.m_currentCommander;
        if (myChar != null)
            info.commanderName = myChar.GetName();
    }

    private void ApplyMyInfo(RankingEntry info)
    {
        if (m_myRankInfo == null || info == null) return;
        m_myRankInfo.SetData(info, true);
    }

    private bool IsExpired(DateTime? nextUpdatedAt)
    {
        if (nextUpdatedAt == null) return true;
        return DateTime.UtcNow >= nextUpdatedAt.Value;
    }

    private DateTime? ParseUpdatedAt(string isoTime)
    {
        if (string.IsNullOrEmpty(isoTime)) return null;
        if (DateTime.TryParse(isoTime, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dt))
            return dt;
        return null;
    }
}
#endif
