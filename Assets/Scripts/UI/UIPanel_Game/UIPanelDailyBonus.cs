using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 출석 보상 달력 패널
// claimedDaysMask: 비트마스크 (bit0=1일, bit5=6일)
// todayDay: 출석일수 — 접속한 서로 다른 날짜 수(1~6), 이 값 이하의 미수령 칸은 전부 클레임 가능
public class UIPanelDailyBonus : UIPanelBase
{
    private const int CALENDAR_DAYS = 6;

    [Header("Daily Bonus Panel")]
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private TMP_Text m_rewardDescText;       // "미네랄 +N 지급!" 텍스트
    [SerializeField] private GameObject m_dailyCountdownRoot; // 수령할 칸이 없을 때만 표시되는 카운트다운 그룹
    [SerializeField] private TMP_Text m_dailyNameText;        // "다음 보상까지" 라벨
    [SerializeField] private TMP_Text m_dailyValueText;       // "04:22:09" (코루틴 업데이트)
    [SerializeField] private InfiniteScrollView m_scrollView;
    [SerializeField] private UIDailyBonusDayRow m_rowPrefab;

    private int m_claimedDaysMask;
    private int m_vipClaimedDaysMask;
    private int m_todayDay;
    private Coroutine m_countdownCoroutine;

    private void Awake()
    {
        if (m_scrollView != null)
            m_scrollView.onItemBind = OnItemBind;
    }

    // 지급은 자동으로 일어나지 않고, 오늘 행의 Claim 버튼을 눌러야 실제 지급됨
    public override void OnShowUIPanel()
    {
        base.OnShowUIPanel();

        var loc = LocalizationManager.Instance;

        if (m_titleText != null)
            m_titleText.text = loc.Get("DailyBonus_Title");

        if (m_dailyNameText != null)
            m_dailyNameText.text = loc.Get("DailyBonus_NextReward");

        if (m_rewardDescText != null)
            m_rewardDescText.text = string.Empty;

        var mgr = DailyBonusManager.Instance;
        RefreshCalendar(mgr.GetClaimedDaysMask(), mgr.GetVipClaimedDaysMask(), mgr.GetTodayDay());
        StartCountdown();
    }

    public override void OnHideUIPanel()
    {
        base.OnHideUIPanel();
        StopCountdown();
    }

    private void RefreshCalendar(int claimedDaysMask, int vipClaimedDaysMask, int todayDay)
    {
        m_claimedDaysMask    = claimedDaysMask;
        m_vipClaimedDaysMask = vipClaimedDaysMask;
        m_todayDay           = todayDay;

        if (m_scrollView != null && m_rowPrefab != null)
            m_scrollView.Initialize(CALENDAR_DAYS, m_rowPrefab.gameObject);
    }

    private void OnItemBind(int dataIndex, GameObject rowObject)
    {
        UIDailyBonusDayRow row = rowObject.GetComponent<UIDailyBonusDayRow>();
        if (row == null) return;

        var table = DataManager.Instance.m_dataTableDailyBonus;
        int day = dataIndex + 1;

        bool claimed = (m_claimedDaysMask & (1 << (day - 1))) != 0;
        bool vipClaimed = (m_vipClaimedDaysMask & (1 << (day - 1))) != 0;
        bool bClaimable = day <= m_todayDay; // 출석일수(m_todayDay) 이하 칸은 전부 클레임 가능
        bool isVipActive = IAPManager.Instance.IsVipActive();
        DailyBonusRewardEntry[] normalRewards = (table != null) ? table.GetRewards(day, EDailyBonusTier.Normal) : null;
        DailyBonusRewardEntry[] vipRewards = (table != null) ? table.GetRewards(day, EDailyBonusTier.VIP) : null;

        row.SetupDailyBonusDayCell(day, claimed, vipClaimed, bClaimable, isVipActive, normalRewards, vipRewards,
            claimVip => OnDayClaimClicked(day, claimVip));
    }

    // 열려있는 칸의 Claim 버튼 클릭 — 실제 지급 API 호출은 여기서만 발생
    private void OnDayClaimClicked(int day, bool claimVip)
    {
        DailyBonusManager.Instance.ClaimDailyBonus(day, claimVip, OnClaimResponse);
    }

    private void OnClaimResponse(DailyClaimResponse response)
    {
        if (response == null || response.available == false) return;

        if (m_rewardDescText != null)
            m_rewardDescText.text = BuildGrantedDescription(response);

        m_claimedDaysMask    = response.claimedDaysMask;
        m_vipClaimedDaysMask = response.vipClaimedDaysMask;
        m_todayDay           = response.todayDay;

        if (m_scrollView != null)
            m_scrollView.RefreshVisible();

        UpdateCountdown();
    }

    private string BuildGrantedDescription(DailyClaimResponse response)
    {
        var loc = LocalizationManager.Instance;
        string desc = string.Empty;

        if (response.grantedExplorationPoint > 0)
            desc = loc.Get("DailyBonus_Desc", response.grantedExplorationPoint);

        if (response.grantedAchievementPoint > 0)
        {
            string achievementDesc = loc.Get("DailyBonus_DescAchievement", response.grantedAchievementPoint);
            desc = string.IsNullOrEmpty(desc) ? achievementDesc : $"{desc}\n{achievementDesc}";
        }

        return desc;
    }

    private void StartCountdown()
    {
        StopCountdown();
        m_countdownCoroutine = StartCoroutine(CountdownCoroutine());
    }

    private void StopCountdown()
    {
        if (m_countdownCoroutine != null)
        {
            StopCoroutine(m_countdownCoroutine);
            m_countdownCoroutine = null;
        }
    }

    private IEnumerator CountdownCoroutine()
    {
        while (true)
        {
            UpdateCountdown();
            yield return new WaitForSeconds(1f);
        }
    }

    // 수령할 칸이 남아있으면 그룹을 숨기고, 모두 수령했으면 다음 보상까지 남은 시간을 표시
    private void UpdateCountdown()
    {
        if (m_dailyCountdownRoot == null) return;

        DailyBonusManager mgr = DailyBonusManager.Instance;
        bool hasClaimable = mgr.HasClaimableDay();
        bool wasActive = m_dailyCountdownRoot.activeSelf;
        m_dailyCountdownRoot.SetActive(hasClaimable == false);
        if (hasClaimable == true) return;

        TimeSpan remaining = mgr.GetNextRewardRemaining();
        if (m_dailyValueText != null)
            m_dailyValueText.text = string.Format("{0:D2}:{1:D2}:{2:D2}", (int)remaining.TotalHours, remaining.Minutes, remaining.Seconds);

        if (wasActive == false)
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_dailyCountdownRoot.transform as RectTransform);
    }
}
