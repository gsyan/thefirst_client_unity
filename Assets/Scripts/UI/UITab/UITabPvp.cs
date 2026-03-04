// PvP 탭 - 상대 목록, 전투 시작/결과, 랭킹 보드(InfiniteScrollView) 관리
using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UITabPvp : UITabBase
{
    [Header("PvP UI Components")]
    [SerializeField] private TMP_Text m_myScoreText;
    [SerializeField] private TMP_Text m_myRankText;
    [SerializeField] private Button m_refreshButton;
    [SerializeField] private TMP_Text m_refreshButtonText;
    [SerializeField] private RectTransform m_scrollViewContent;
    [SerializeField] private GameObject m_pvpItemPrefab;

    [Header("PvP Ranking Board")]
    [SerializeField] private RectTransform m_scrollViewContentRankingBoard;
    [SerializeField] private GameObject m_scrollViewRankingItemPrefab;
    [SerializeField] private InfiniteScrollView m_rankingScrollView; // ranking ScrollRect에 부착된 컴포넌트
    [SerializeField] private Button m_pvpRankListButton;


    [Header("PvP Warp")]
    [SerializeField] private Material m_pvpBattleSkybox;
    [SerializeField] private DataTableZone m_datatableZone;

    private SpaceFleet m_myFleet;
    private Character m_myCharacter;
    private readonly List<ScrollViewPvpItem> m_pvpItemPool = new List<ScrollViewPvpItem>();
    private readonly List<ScrollViewPvpItem> m_pvpItemActive = new List<ScrollViewPvpItem>();

    private int m_myScore;
    private int m_myRank;
    private int m_refreshRemain;
    private string m_currentBattleToken;

    // 랭킹 보드 - 서버에서 받아온 데이터 캐시 및 요청 중인 페이지 관리
    private const int RANKING_PAGE_SIZE = 50;
    private readonly Dictionary<int, RankingEntry> m_rankingCache = new Dictionary<int, RankingEntry>();
    private readonly HashSet<int> m_requestingPages = new HashSet<int>(); // 요청 중인 페이지 번호
    private bool m_rankingInitialized;

    public override void InitializeUITab()
    {
        m_myCharacter = DataManager.Instance.m_currentCharacter;
        if (m_myCharacter == null || m_myCharacter.GetOwnedFleet() == null) return;
        m_myFleet = m_myCharacter.GetOwnedFleet();

        m_refreshButton.onClick.AddListener(OnRefreshClicked);
        if (m_pvpRankListButton != null)
            m_pvpRankListButton.onClick.AddListener(OnRankListButtonClicked);

        if (m_rankingScrollView != null)
        {
            m_rankingScrollView.onItemBind = OnRankingItemBind;
            m_rankingScrollView.onNeedData = OnNeedRankingData;
        }

        EventManager.Subscribe_MyFleetDestroyed(OnMyFleetWiped);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_MyFleetDestroyed(OnMyFleetWiped);
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        //CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
        RequestPvpList();
        RequestPvpMyRank();
    }

    public override void OnTabDeactivated()
    {
        //CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    private void OnRankListButtonClicked()
    {
        UIManager.Instance.ShowRankingPopup();
    }

    // 상대 목록 요청
    private void RequestPvpList()
    {
        var request = new PvpListRequest();
        NetworkManager.Instance.PvpList(request, OnPvpListResponse);
    }

    // 내 랭크 정보 요청
    private void RequestPvpMyRank()
    {
        var request = new PvpMyRankRequest();
        NetworkManager.Instance.PvpMyRank(request, OnPvpMyRankResponse);
    }

    private void OnPvpListResponse(ApiResponse<PvpListResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            ShowResultMessage("상대 목록을 불러올 수 없습니다.");
            return;
        }

        PopulateOpponentList(response.data.opponents);
    }

    private void OnPvpMyRankResponse(ApiResponse<PvpMyRankResponse> response)
    {
        if (response == null || response.errorCode != 0 || response.data?.myRankInfo == null) return;

        m_myScore = response.data.myRankInfo.pvpScore;
        m_myRank = response.data.myRankInfo.pvpRank;
        m_refreshRemain = response.data.myRankInfo.pvpListRefreshRemain;
        UpdateMyInfo();
    }

    private void UpdateMyInfo()
    {
        if (m_myScoreText != null) m_myScoreText.text = $"{m_myScore}";
        if (m_myRankText != null) m_myRankText.text = m_myRank > 0 ? $"{m_myRank}" : "-";
        if (m_refreshButtonText != null) m_refreshButtonText.text = LocalizationManager.Instance.Get("pvp_opponent_list_refresh", new object[] {m_refreshRemain, 5});
    }

    private void PopulateOpponentList(List<PvpOpponentInfo> opponents)
    {
        // 활성 아이템 회수
        for (int i = 0; i < m_pvpItemActive.Count; i++)
        {
            m_pvpItemActive[i].gameObject.SetActive(false);
            m_pvpItemPool.Add(m_pvpItemActive[i]);
        }
        m_pvpItemActive.Clear();

        if (opponents == null) return;

        for (int i = 0; i < opponents.Count; i++)
        {
            ScrollViewPvpItem item = GetOrCreatePvpItem();
            PvpOpponentInfo opponent = opponents[i];
            item.InitializeScrollViewPvpItem(opponent, () => OnAttackClicked(opponent));
            item.gameObject.SetActive(true);
            m_pvpItemActive.Add(item);
        }
    }

    private ScrollViewPvpItem GetOrCreatePvpItem()
    {
        if (m_pvpItemPool.Count > 0)
        {
            int lastIndex = m_pvpItemPool.Count - 1;
            ScrollViewPvpItem pooled = m_pvpItemPool[lastIndex];
            m_pvpItemPool.RemoveAt(lastIndex);
            return pooled;
        }

        GameObject obj = Instantiate(m_pvpItemPrefab, m_scrollViewContent);
        return obj.GetComponent<ScrollViewPvpItem>();
    }

    // 갱신 버튼
    private void OnRefreshClicked()
    {
        if (m_refreshRemain <= 0)
        {
            ShowResultMessage("오늘 갱신 횟수를 모두 사용했습니다.");
            return;
        }

        var request = new PvpRefreshRequest();
        NetworkManager.Instance.PvpRefresh(request, OnPvpRefreshResponse);
    }

    private void OnPvpRefreshResponse(ApiResponse<PvpRefreshResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            ShowResultMessage("갱신에 실패했습니다.");
            return;
        }

        m_refreshRemain = response.data.refreshRemain;
        UpdateMyInfo();
        PopulateOpponentList(response.data.opponents);
    }

    // 공격 버튼 - 상대 함대 정보 팝업 표시
    private void OnAttackClicked(PvpOpponentInfo opponent)
    {
        CapabilityProfile stats = CommonUtility.GetFleetCapabilityProfile(opponent.fleetInfo);
        int shipCount = (opponent.fleetInfo != null && opponent.fleetInfo.ships != null) ? opponent.fleetInfo.ships.Count : 0;

        string title = opponent.characterName;
        string message = LocalizationManager.Instance.Get("pvp_opponent_info", new object[] { opponent.pvpScore, opponent.rank });

        List<string> labels = new List<string>
        {
            "fleet_ship_count",
            "health_power",
            "attack_power",
            "aircraft_count"
        };
        List<string> values = new List<string>
        {
            shipCount.ToString(),
            CommonUtility.FormatBigNumber(stats.health_power),
            CommonUtility.FormatBigNumber(stats.attack_power),
            stats.aircraft_count.ToString()
        };

        UIManager.Instance.ShowConfirmPopup(title, message, labels, values, null,
            () => RequestPvpBattleStart(opponent));
    }

    // 서버에 전투 시작 요청
    private void RequestPvpBattleStart(PvpOpponentInfo opponent)
    {
        var request = new PvpBattleStartRequest { opponentCharacterId = opponent.characterId };
        NetworkManager.Instance.PvpBattleStart(request, OnBattleStartResponse);
    }

    // 서버 응답 후 워프 연출 → 전투 시작
    private void OnBattleStartResponse(ApiResponse<PvpBattleStartResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            ShowResultMessage("전투를 시작할 수 없습니다.");
            return;
        }

        m_currentBattleToken = response.data.battleToken;
        FleetInfo opponentFleetInfo = response.data.opponentFleetInfo;

        var pp = WarpPostProcessing.Instance;
        if (pp != null)
            pp.SetSkyboxBlendTarget(m_pvpBattleSkybox);

        m_myFleet.StartFleetWarp(m_pvpBattleSkybox, () =>
        {
            UIManager.Instance.ShowPanel("UIPanelCameraView");

            ObjectManager.Instance.StartPvpBattle(opponentFleetInfo, (isVictory) =>
            {
                ReportBattleResult(isVictory);
            });
        });
    }

    // 플레이어 함대 전멸
    private void OnMyFleetWiped()
    {
        // ObjectManager.ForceEndBattle(false)가 콜백을 호출함
    }

    private void ReportBattleResult(bool isVictory)
    {
        var request = new PvpBattleResultRequest
        {
            battleToken = m_currentBattleToken,
            isVictory = isVictory
        };

        NetworkManager.Instance.PvpBattleResult(request, OnBattleResultResponse);
    }

    private void OnBattleResultResponse(ApiResponse<PvpBattleResultResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            ShowResultMessage("전투 결과 처리 실패");
            ReturnFromBattle();
            return;
        }

        int scoreChange = response.data.scoreChange;
        int oldRank = m_myRank;
        m_myScore = response.data.newScore;
        m_myRank = response.data.newRank;
        bool isVictory = scoreChange >= 0;

        UpdateMyInfo();
        EventManager.TriggerPvpBattleResult(isVictory, scoreChange, m_myScore, m_myRank);

        string titleKey = isVictory ? "pvp_battle_result_win" : "pvp_battle_result_lose";
        string scoreStr = isVictory ? $"+{scoreChange}" : $"{scoreChange}";
        string title = LocalizationManager.Instance.Get(titleKey);
        string scoreLine = LocalizationManager.Instance.Get("pvp_battle_result_score", scoreStr, m_myScore);
        string rankLine = LocalizationManager.Instance.Get("pvp_battle_result_rank", oldRank, m_myRank);
        UIManager.Instance.ShowAlertPopup(title, $"{scoreLine}\n{rankLine}", ReturnFromBattle);
    }

    // ─── 랭킹 보드 ─────────────────────────────────────────────────────────────

    // 랭킹 보드 첫 진입 - 1페이지 요청으로 totalCount 확보 후 초기화
    public void RequestRankingBoardOpen()
    {
        if (m_rankingScrollView == null) return;
        if (m_rankingInitialized == true)
        {
            m_rankingScrollView.JumpToIndex(0);
            return;
        }

        var request = new PvpRankingRequest { offset = 0, limit = RANKING_PAGE_SIZE };
        NetworkManager.Instance.PvpRanking(request, OnRankingInitResponse);
    }

    // 특정 순위가 화면 상단에 오도록 이동 (1-based rank)
    public void ShowRank(int rank)
    {
        if (m_rankingScrollView == null || m_rankingInitialized == false) return;
        m_rankingScrollView.JumpToIndex(rank - 1);
    }

    private void OnRankingInitResponse(ApiResponse<PvpRankingResponse> response)
    {
        if (response == null || response.errorCode != 0 || response.data == null)
        {
            ShowResultMessage("랭킹을 불러올 수 없습니다.");
            return;
        }

        // 첫 페이지 데이터 캐시 저장
        if (response.data.items != null)
        {
            for (int i = 0; i < response.data.items.Count; i++)
                m_rankingCache[i] = response.data.items[i];
        }

        m_rankingInitialized = true;
        m_rankingScrollView.Initialize(response.data.totalCount, m_scrollViewRankingItemPrefab);
    }

    // InfiniteScrollView 콜백 - 아이템에 데이터 적용
    private void OnRankingItemBind(int dataIndex, GameObject itemObj)
    {
        if (itemObj.TryGetComponent<ScrollViewRankingItem>(out var item) == false) return;

        if (m_rankingCache.TryGetValue(dataIndex, out RankingEntry entry) == true)
        {
            long myCharId = DataManager.Instance.m_currentCharacter.m_characterInfo.characterId;
            item.SetData(entry, entry.characterId == myCharId);
        }
        else
        {
            item.SetLoading();
        }
    }

    // InfiniteScrollView 콜백 - 해당 범위의 데이터 서버 요청
    private void OnNeedRankingData(int startIndex, int count)
    {
        int firstPage = startIndex / RANKING_PAGE_SIZE;
        int lastPage = (startIndex + count - 1) / RANKING_PAGE_SIZE;

        for (int page = firstPage; page <= lastPage; page++)
        {
            // 이미 캐시됐거나 요청 중인 페이지는 스킵
            int pageOffset = page * RANKING_PAGE_SIZE;
            if (m_rankingCache.ContainsKey(pageOffset) == true) continue;
            if (m_requestingPages.Contains(page) == true) continue;

            m_requestingPages.Add(page);
            int capturedPage = page;
            var request = new PvpRankingRequest { offset = pageOffset, limit = RANKING_PAGE_SIZE };
            NetworkManager.Instance.PvpRanking(request, res => OnRankingPageResponse(res, capturedPage));
        }
    }

    private void OnRankingPageResponse(ApiResponse<PvpRankingResponse> response, int page)
    {
        m_requestingPages.Remove(page);

        if (response == null || response.errorCode != 0 || response.data?.items == null) return;

        int baseIndex = page * RANKING_PAGE_SIZE;
        for (int i = 0; i < response.data.items.Count; i++)
            m_rankingCache[baseIndex + i] = response.data.items[i];

        m_rankingScrollView.RefreshVisible();
    }

    // ────────────────────────────────────────────────────────────────────────────

    // 전투 종료 후 워프 복귀
    private void ReturnFromBattle()
    {
        m_currentBattleToken = null;

        UIManager.Instance.HidePanel("UIPanelCameraView");
        CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_my_fleet);

        ZoneConfig zoneConfig = m_datatableZone.GetZone(0);
        if (zoneConfig == null) return;

        var pp = WarpPostProcessing.Instance;
        if (pp != null)
            pp.SetSkyboxBlendTarget(zoneConfig.skyboxMaterial);

        m_myFleet.StartFleetWarp(zoneConfig.skyboxMaterial, () =>
        {
            if (m_myFleet.IsFleetAlive() == false)
                m_myFleet.RebuildFleet(0.1f);
            else
                m_myFleet.RestoreDestroyedShips(0.1f);

            RequestPvpList();
            RequestPvpMyRank();
        });
    }
}
