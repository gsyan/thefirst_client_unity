// PVP/Zone 랭킹 팝업 - 탭 버튼 전환 + InfiniteScrollView 랭킹 리스트
// 서버가 내려준 nextUpdatedAt 기준으로 캐시 재활용, 만료 시 재요청
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupRanking : UIPopupBase
{
    private enum RankingType { Pvp, Zone }

    [Header("Ranking Popup UI")]
    [SerializeField] private TMP_Text m_periodText;
    [SerializeField] private Button m_pvpRankListButton;
    [SerializeField] private Button m_zoneRankListButton;

    [SerializeField] private ScrollViewRankingItem m_myRankInfo;
    [SerializeField] private InfiniteScrollView m_rankingScrollView;
    [SerializeField] private GameObject m_rankingItemPrefab;
    [SerializeField] private Button m_closeButton;

    private const int PAGE_SIZE = 50;

    private readonly Dictionary<int, RankingEntry> m_pvpCache = new Dictionary<int, RankingEntry>();
    private readonly Dictionary<int, RankingEntry> m_zoneCache = new Dictionary<int, RankingEntry>();
    private readonly HashSet<int> m_requestingPages = new HashSet<int>();

    // 내 랭킹 정보 (랭킹 첫 응답 myInfo)
    private RankingEntry m_pvpMyInfo;
    private RankingEntry m_zoneMyInfo;

    // 서버가 내려준 다음 갱신 예정 시각 (UTC)
    private DateTime? m_pvpNextUpdatedAt;
    private DateTime? m_zoneNextUpdatedAt;

    private RankingType m_currentType = RankingType.Pvp;
    private Action m_onClose;

    protected override void Awake()
    {
        base.Awake();
        if (m_closeButton != null)
            m_closeButton.onClick.AddListener(OnCloseClicked);
        if (m_rankingScrollView != null)
        {
            m_rankingScrollView.onItemBind = OnItemBind;
            m_rankingScrollView.onNeedData = OnNeedData;
        }
        if (m_pvpRankListButton != null)
            m_pvpRankListButton.onClick.AddListener(() => OnTabClicked(RankingType.Pvp));
        if (m_zoneRankListButton != null)
            m_zoneRankListButton.onClick.AddListener(() => OnTabClicked(RankingType.Zone));
    }

    public void ShowPopupRanking(Action onClose)
    {
        m_onClose = onClose;
        base.ShowPopup();
        LoadTab(RankingType.Pvp);
    }

    private void OnTabClicked(RankingType type)
    {
        if (m_currentType == type) return;
        LoadTab(type);
    }

    private void LoadTab(RankingType type)
    {
        m_currentType = type;
        m_requestingPages.Clear();

        if (type == RankingType.Pvp)
        {
            // 캐시 유효하면 서버 재요청 없이 로컬 데이터 표시 (단, 내 이름은 항상 최신으로 덮어씀)
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

    private void OnCloseClicked()
    {
        m_onClose?.Invoke();
    }

    private void OnPvpInitResponse(ApiResponse<PvpRankingResponse> response)
    {
        if (m_currentType != RankingType.Pvp) return;
        if (response == null || response.errorCode != 0 || response.data == null)
        {
            Debug.LogWarning("[UIPopupRanking] PVP 랭킹 로드 실패");
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
            Debug.LogWarning("[UIPopupRanking] Zone 랭킹 로드 실패");
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

    // InfiniteScrollView 콜백 - 아이템 데이터 적용
    private void OnItemBind(int dataIndex, GameObject itemObj)
    {
        if (itemObj.TryGetComponent<ScrollViewRankingItem>(out var item) == false) return;

        long myCharId = DataManager.Instance.m_currentCharacter.m_characterInfo.characterId;
        Dictionary<int, RankingEntry> cache = GetCurrentCache();

        if (cache.TryGetValue(dataIndex, out RankingEntry entry) == true)
            item.SetData(entry, entry.characterId == myCharId);
        else
            item.SetLoading();
    }

    // InfiniteScrollView 콜백 - 미캐시 페이지 서버 요청
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
        if (response == null || response.errorCode != 0 || response.data?.items == null) return;
        int baseIndex = page * PAGE_SIZE;
        for (int i = 0; i < response.data.items.Count; i++)
            m_pvpCache[baseIndex + i] = response.data.items[i];
        if (m_currentType == RankingType.Pvp)
            m_rankingScrollView.RefreshVisible();
    }

    private void OnZonePageResponse(ApiResponse<ZoneRankingResponse> response, int page)
    {
        m_requestingPages.Remove(page);
        if (response == null || response.errorCode != 0 || response.data?.items == null) return;
        int baseIndex = page * PAGE_SIZE;
        for (int i = 0; i < response.data.items.Count; i++)
            m_zoneCache[baseIndex + i] = response.data.items[i];
        if (m_currentType == RankingType.Zone)
            m_rankingScrollView.RefreshVisible();
    }

    // ── 유틸 ───────────────────────────────────────────────────────────────

    // 캐시 히트 시 내 이름만 로컬 DataManager 기준으로 덮어씀 (이름 변경 즉시 반영)
    private void RefreshMyInfoName(RankingEntry info)
    {
        if (info == null) return;
        info.characterName = DataManager.Instance.m_currentCharacter?.GetName() ?? info.characterName;
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
