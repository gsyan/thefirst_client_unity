using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 출석 보상 달력 패널
// claimedDaysMask: 비트마스크 (bit0=1일, bit5=6일)
// todayDay: 서버 기준 오늘 날짜 (1~6)
public class UIPanelDailyBonus : UIPanelBase
{
    private const int CALENDAR_DAYS = 6;

    [Header("Daily Bonus Panel")]
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private TMP_Text m_rewardDescText;       // "미네랄 +N 지급!" 텍스트
    [SerializeField] private TMP_Text m_monthValueText;       // "18일 07:32:11" (코루틴 업데이트)
    [SerializeField] private TMP_Text m_dailyValueText;       // "04:22:09" (코루틴 업데이트)
    [SerializeField] private InfiniteScrollView m_scrollView;
    [SerializeField] private UIDailyBonusDayRow m_rowPrefab;

    private int m_claimedDaysMask;
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

        if (m_rewardDescText != null)
            m_rewardDescText.gameObject.SetActive(false);

        var mgr = DailyBonusManager.Instance;
        RefreshCalendar(mgr.GetClaimedDaysMask(), mgr.GetTodayDay());
        StartCountdown();
    }

    public override void OnHideUIPanel()
    {
        base.OnHideUIPanel();
        StopCountdown();
    }

    private void RefreshCalendar(int claimedDaysMask, int todayDay)
    {
        m_claimedDaysMask = claimedDaysMask;
        m_todayDay        = todayDay;

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
        bool bToday  = day == m_todayDay;
        DailyBonusRewardEntry[] rewards = (table != null) ? table.GetRewards(day) : null;

        row.SetupDailyBonusDayCell(day, claimed, bToday, rewards, OnDayClaimClicked);
    }

    // 오늘 행 Claim 버튼 클릭 — 실제 지급 API 호출은 여기서만 발생
    private void OnDayClaimClicked()
    {
        DailyBonusManager.Instance.ClaimDailyBonus(OnClaimResponse);
    }

    private void OnClaimResponse(DailyClaimResponse response)
    {
        if (response == null || response.available == false) return;

        if (m_rewardDescText != null)
        {
            m_rewardDescText.gameObject.SetActive(true);
            m_rewardDescText.text = BuildGrantedDescription(response);
        }

        m_claimedDaysMask = response.claimedDaysMask;
        m_todayDay        = response.todayDay;

        if (m_scrollView != null)
            m_scrollView.RefreshVisible();
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
        bool firstRun = true;
        while (true)
        {
            var mgr = DailyBonusManager.Instance;
            var loc = LocalizationManager.Instance;

            // 이번 주 남은 시간 (값만 업데이트)
            if (m_monthValueText != null)
            {
                TimeSpan week = mgr.GetWeekRemaining();
                int days = (int)week.TotalDays;
                m_monthValueText.text = days > 0
                    ? loc.Get("DailyBonus_DaysTime", days, week.Hours, week.Minutes, week.Seconds)
                    : string.Format("{0:D2}:{1:D2}:{2:D2}", (int)week.TotalHours, week.Minutes, week.Seconds);
            }

            // 다음 일일보상까지 (값만 업데이트)
            if (m_dailyValueText != null)
            {
                TimeSpan daily = mgr.GetDailyRemaining();
                m_dailyValueText.text = daily <= TimeSpan.Zero
                    ? loc.Get("DailyBonus_Available")
                    : string.Format("{0:D2}:{1:D2}:{2:D2}", (int)daily.TotalHours, daily.Minutes, daily.Seconds);
            }

            // 첫 프레임 텍스트 세팅 후 부모 레이아웃 1회 강제 갱신
            if (firstRun == true)
            {
                firstRun = false;
                if (m_monthValueText != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(m_monthValueText.transform.parent as RectTransform);
                if (m_dailyValueText != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(m_dailyValueText.transform.parent as RectTransform);
            }

            yield return new WaitForSeconds(1f);
        }
    }
}
