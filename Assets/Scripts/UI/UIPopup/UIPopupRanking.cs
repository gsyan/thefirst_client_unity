// PvP 전체 랭킹 팝업 - 시즌명/기간 헤더 + InfiniteScrollView 랭킹 리스트
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupRanking : UIPopupBase
{
    [Header("Ranking Popup UI")]
    [SerializeField] private TMP_Text m_periodText;
    [SerializeField] private ScrollViewRankingItem m_myRankInfo;
    [SerializeField] private InfiniteScrollView m_rankingScrollView;
    [SerializeField] private GameObject m_rankingItemPrefab;
    [SerializeField] private Button m_closeButton;

    private const int PAGE_SIZE = 50;
    private readonly Dictionary<int, PvpRankingEntry> m_cache = new Dictionary<int, PvpRankingEntry>();
    private readonly HashSet<int> m_requestingPages = new HashSet<int>();
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
    }

    public void ShowPopupRanking(Action onClose)
    {
        m_onClose = onClose;
        base.ShowPopup();

        // 내 랭크는 매번 최신으로 갱신
        NetworkManager.Instance.PvpMyRank(new PvpMyRankRequest(), OnMyRankResponse);

        m_cache.Clear();
        m_requestingPages.Clear();

        var request = new PvpRankingRequest { offset = 0, limit = PAGE_SIZE };
        NetworkManager.Instance.PvpRanking(request, OnInitResponse);
    }

    private void OnCloseClicked()
    {
        m_onClose?.Invoke();
    }

    private void OnMyRankResponse(ApiResponse<PvpMyRankResponse> response)
    {
        if (response == null || response.errorCode != 0 || response.data?.myRankInfo == null) return;
        if (m_myRankInfo == null) return;

        long myCharId = DataManager.Instance.m_currentFleetInfo?.characterId ?? 0L;
        string myName = DataManager.Instance.m_currentCharacter?.GetName() ?? "";
        var entry = new PvpRankingEntry
        {
            rank = response.data.myRankInfo.pvpRank,
            characterId = myCharId,
            characterName = myName,
            pvpScore = response.data.myRankInfo.pvpScore
        };
        m_myRankInfo.SetData(entry, true);
    }

    private void OnInitResponse(ApiResponse<PvpRankingResponse> response)
    {
        if (response == null || response.errorCode != 0 || response.data == null)
        {
            Debug.LogWarning("[UIPopupPvpRanking] 랭킹 로드 실패");
            return;
        }

        if (response.data.items != null)
        {
            for (int i = 0; i < response.data.items.Count; i++)
                m_cache[i] = response.data.items[i];
        }

        UpdateHeader(response.data);
        m_rankingScrollView.Initialize(response.data.totalCount, m_rankingItemPrefab);
    }

    // 상단 시즌명/기간 텍스트 업데이트
    private void UpdateHeader(PvpRankingResponse data)
    {
        if (m_periodText != null)
        {
            bool hasStart = string.IsNullOrEmpty(data.seasonStartTime) == false;
            bool hasEnd = string.IsNullOrEmpty(data.seasonEndTime) == false;
            m_periodText.gameObject.SetActive(hasStart && hasEnd);
            if (hasStart && hasEnd)
                m_periodText.text = $"{data.seasonStartTime} ~ {data.seasonEndTime}";
        }
    }

    // InfiniteScrollView 콜백 - 아이템 데이터 적용
    private void OnItemBind(int dataIndex, GameObject itemObj)
    {
        if (itemObj.TryGetComponent<ScrollViewRankingItem>(out var item) == false) return;

        if (m_cache.TryGetValue(dataIndex, out PvpRankingEntry entry) == true)
        {
            long myCharId = DataManager.Instance.m_currentFleetInfo?.characterId ?? 0L;
            item.SetData(entry, entry.characterId == myCharId);
        }
        else
        {
            item.SetLoading();
        }
    }

    // InfiniteScrollView 콜백 - 미캐시 페이지 서버 요청
    private void OnNeedData(int startIndex, int count)
    {
        int firstPage = startIndex / PAGE_SIZE;
        int lastPage = (startIndex + count - 1) / PAGE_SIZE;

        for (int page = firstPage; page <= lastPage; page++)
        {
            int pageOffset = page * PAGE_SIZE;
            if (m_cache.ContainsKey(pageOffset) == true) continue;
            if (m_requestingPages.Contains(page) == true) continue;

            m_requestingPages.Add(page);
            int captured = page;
            var request = new PvpRankingRequest { offset = pageOffset, limit = PAGE_SIZE };
            NetworkManager.Instance.PvpRanking(request, res => OnPageResponse(res, captured));
        }
    }

    private void OnPageResponse(ApiResponse<PvpRankingResponse> response, int page)
    {
        m_requestingPages.Remove(page);
        if (response == null || response.errorCode != 0 || response.data?.items == null) return;

        int baseIndex = page * PAGE_SIZE;
        for (int i = 0; i < response.data.items.Count; i++)
            m_cache[baseIndex + i] = response.data.items[i];

        m_rankingScrollView.RefreshVisible();
    }

    private void OnDestroy()
    {
        if (m_closeButton != null)
            m_closeButton.onClick.RemoveAllListeners();
    }
}
