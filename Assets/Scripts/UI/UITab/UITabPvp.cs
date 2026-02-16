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

    private SpaceFleet m_myFleet;
    private Character m_myCharacter;
    private readonly List<ScrollViewPvpItem> m_pvpItemPool = new List<ScrollViewPvpItem>();
    private readonly List<ScrollViewPvpItem> m_pvpItemActive = new List<ScrollViewPvpItem>();

    private int m_myScore;
    private int m_myRank;
    private int m_refreshRemain;
    private string m_currentBattleToken;

    public override void InitializeUITab()
    {
        m_myCharacter = DataManager.Instance.m_currentCharacter;
        if (m_myCharacter == null || m_myCharacter.GetOwnedFleet() == null) return;
        m_myFleet = m_myCharacter.GetOwnedFleet();

        m_refreshButton.onClick.AddListener(OnRefreshClicked);

        EventManager.Subscribe_MyFleetDestroyed(OnMyFleetWiped);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_MyFleetDestroyed(OnMyFleetWiped);
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        RequestPvpList();
    }

    public override void OnTabDeactivated()
    {
        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    // 상대 목록 요청
    private void RequestPvpList()
    {
        var request = new PvpListRequest();
        NetworkManager.Instance.PvpList(request, OnPvpListResponse);
    }

    private void OnPvpListResponse(ApiResponse<PvpListResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            ShowResultMessage("상대 목록을 불러올 수 없습니다.");
            return;
        }

        m_myScore = response.data.myRankInfo.pvpScore;
        m_myRank = response.data.myRankInfo.pvpRank;
        m_refreshRemain = response.data.myRankInfo.pvpListRefreshRemain;

        UpdateMyInfo();
        PopulateOpponentList(response.data.opponents);
    }

    private void UpdateMyInfo()
    {
        if (m_myScoreText != null) m_myScoreText.text = $"{m_myScore}";
        if (m_myRankText != null) m_myRankText.text = $"{m_myRank}";
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

    // 공격 버튼
    private void OnAttackClicked(PvpOpponentInfo opponent)
    {
        var request = new PvpBattleStartRequest { opponentCharacterId = opponent.characterId };
        NetworkManager.Instance.PvpBattleStart(request, (response) => OnBattleStartResponse(response));
    }

    private void OnBattleStartResponse(ApiResponse<PvpBattleStartResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            ShowResultMessage("전투를 시작할 수 없습니다.");
            return;
        }

        m_currentBattleToken = response.data.battleToken;
        FleetInfo opponentFleetInfo = response.data.opponentFleetInfo;

        UIManager.Instance.ShowPanel("UIPanelCameraView");

        ObjectManager.Instance.StartPvpBattle(opponentFleetInfo, (isVictory) =>
        {
            ReportBattleResult(isVictory);
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
        UIManager.Instance.HidePanel("UIPanelCameraView");

        if (response == null || response.errorCode != 0)
        {
            ShowResultMessage("전투 결과 처리 실패");
            ReturnFromBattle();
            return;
        }

        int scoreChange = response.data.scoreChange;
        m_myScore = response.data.newScore;
        m_myRank = response.data.newRank;
        bool isVictory = scoreChange >= 0;

        UpdateMyInfo();

        string resultMsg = isVictory
            ? $"승리! (점수 +{scoreChange})"
            : $"패배 (점수 {scoreChange})";
        ShowResultMessage(resultMsg, 5f);

        EventManager.TriggerPvpBattleResult(isVictory, scoreChange, m_myScore, m_myRank);

        ReturnFromBattle();
    }

    private void ReturnFromBattle()
    {
        m_currentBattleToken = null;

        // 전멸 시 함대 복구
        if (m_myFleet.IsFleetAlive() == false)
            m_myFleet.RebuildFleet(0.1f);
        else
            m_myFleet.RestoreDestroyedShips(0.1f);

        // 상대 목록 재요청
        RequestPvpList();
    }
}
