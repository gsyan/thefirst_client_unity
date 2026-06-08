using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UITabPvp_MyInfo : UITabBase
{
    [SerializeField] private TMP_Text m_myInfoScoreText;
    [SerializeField] private TMP_Text m_myInfoRankText;
    [SerializeField] private TMP_Text m_myInfoSeasonText;
    [SerializeField] private Button m_refreshButton;
    [SerializeField] private TMP_Text m_refreshButtonText;
    [SerializeField] private Transform m_opponentCardParent;
    private PvpSelectCard[] m_opponentCards;

    private int m_myScore;
    private int m_myRank;
    private int m_refreshRemain;
    private int m_seasonNumber;
    private string m_seasonEndTime;

    public Action<PvpOpponentInfo> onAttackClicked;

    public override void InitializeUITab()
    {
        m_refreshButton.onClick.AddListener(OnRefreshClicked);
        if (m_opponentCardParent != null)
            m_opponentCards = m_opponentCardParent.GetComponentsInChildren<PvpSelectCard>(true);
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        RequestPvpList();
    }

    public void RequestPvpList()
    {
        var request = new PvpListRequest();
        NetworkManager.Instance.PvpList(request, OnPvpListResponse);
    }

    private void OnPvpListResponse(ApiResponse<PvpListResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            ShowErrorMessage("상대 목록을 불러올 수 없습니다.");
            return;
        }
        SetOpponentList(response.data.opponents);
    }

    public int GetCurrentScore()
    {
        return m_myScore;
    }

    public int GetCurrentRank()
    {
        return m_myRank;
    }

    public void SetMyRankInfo(PvpRankInfo rankInfo)
    {
        m_myScore = rankInfo.pvpScore;
        m_myRank = rankInfo.pvpRank;
        m_refreshRemain = rankInfo.pvpListRefreshRemain;
        m_seasonNumber = rankInfo.seasonNumber;
        m_seasonEndTime = rankInfo.seasonEndTime;
        UpdateMyInfo();
    }

    public void UpdateScore(int score, int rank)
    {
        m_myScore = score;
        m_myRank = rank;
        UpdateMyInfo();
    }

    public void SetOpponentList(List<PvpOpponentInfo> opponents)
    {
        if (m_opponentCards == null) return;
        for (int i = 0; i < m_opponentCards.Length; i++)
        {
            if (m_opponentCards[i] == null) continue;
            if (opponents != null && i < opponents.Count)
            {
                PvpOpponentInfo opponent = opponents[i];
                m_opponentCards[i].InitializePvpSelectCard(opponent, () => onAttackClicked?.Invoke(opponent));
            }
            else
            {
                m_opponentCards[i].SetEmpty();
            }
        }
    }

    private void UpdateMyInfo()
    {
        string rankStr = m_myRank > 0 ? $"#{m_myRank}" : "#-";
        m_myInfoScoreText.text = $"{m_myScore}";
        m_myInfoRankText.text = rankStr;

        if (m_myInfoSeasonText != null)
        {
            bool hasSeason = m_seasonNumber > 0;
            m_myInfoSeasonText.gameObject.SetActive(hasSeason);
            if (hasSeason == true)
            {
                string dStr = "";
                if (string.IsNullOrEmpty(m_seasonEndTime) == false &&
                    System.DateTime.TryParse(m_seasonEndTime, null, System.Globalization.DateTimeStyles.RoundtripKind, out System.DateTime endTime))
                {
                    int daysRemain = (int)(endTime - System.DateTime.UtcNow).TotalDays;
                    dStr = daysRemain > 0 ? $" (D-{daysRemain})" : " (D-0)";
                }
                m_myInfoSeasonText.text = LocalizationManager.Instance.Get("UITabRank_SeasonNumber", m_seasonNumber) + dStr;
            }
        }

        if (m_refreshButtonText != null)
            m_refreshButtonText.text = LocalizationManager.Instance.Get("UITabRank_refresh", new object[] { m_refreshRemain, 5 });
    }

    private void OnRefreshClicked()
    {
        if (m_refreshRemain <= 0)
        {
            ShowErrorMessage("오늘 갱신 횟수를 모두 사용했습니다.");
            return;
        }
        string title = LocalizationManager.Instance.Get("pvp_opponent_list");
        string message = LocalizationManager.Instance.Get("pvp_refresh_confirm", m_refreshRemain);
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title     = title,
            message   = message,
            onConfirm = ExecuteRefresh
        });
    }

    private void ExecuteRefresh()
    {
        var request = new PvpRefreshRequest();
        NetworkManager.Instance.PvpRefresh(request, OnPvpRefreshResponse);
    }

    private void OnPvpRefreshResponse(ApiResponse<PvpRefreshResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            ShowErrorMessage("갱신에 실패했습니다.");
            return;
        }
        m_refreshRemain = response.data.refreshRemain;
        UpdateMyInfo();
        SetOpponentList(response.data.opponents);
    }
}
