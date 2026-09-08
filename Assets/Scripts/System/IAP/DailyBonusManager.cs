using System;
using System.Globalization;
using UnityEngine;

public class DailyBonusManager : MonoSingleton<DailyBonusManager>
{
    private int m_claimedDaysMask;
    private int m_vipClaimedDaysMask;
    private int m_todayDay;
    private DateTime m_loginRewardWeekStart; // UTC, 이번 주 월요일 00:00, MinValue = 데이터 없음
    private DateTime m_nextAvailableAt;      // UTC, MinValue = 데이터 없음

    public int GetClaimedDaysMask()    { return m_claimedDaysMask; }
    public int GetVipClaimedDaysMask() { return m_vipClaimedDaysMask; }
    public int GetTodayDay()           { return m_todayDay; }
    public DateTime GetLoginRewardWeekStart() { return m_loginRewardWeekStart; }
    public DateTime GetNextAvailableAt() { return m_nextAvailableAt; }

    // 로그인(접속) 시점 1회 호출 — 지급 없이 상태만 조회해 레드닷 갱신(업적이 존런 종료 시점에 1회 확인하는 것과 동일한 패턴)
    public void CheckDailyBonusStatus()
    {
        NetworkManager.Instance.GetDailyBonusStatus(response =>
        {
            if (response == null || response.errorCode != (int)ServerErrorCode.SUCCESS || response.data == null) return;

            ApplyStatus(response.data);

            Commander commander = DataManager.Instance.m_currentCommander;
            if (commander != null)
                commander.UpdateHasUnclaimedDailyBonus(response.data.available);
        });
    }

    // 달력 팝업의 오늘 칸 Claim 버튼 클릭 시에만 호출 — 실제 지급이 여기서 발생
    public void ClaimDailyBonus(Action<DailyClaimResponse> onResult)
    {
        NetworkManager.Instance.ClaimVipDailyReward(response =>
        {
            if (response == null || response.errorCode != (int)ServerErrorCode.SUCCESS || response.data == null)
            {
                if (onResult != null) onResult(null);
                return;
            }

            ApplyResponse(response.data);

            Commander commander = DataManager.Instance.m_currentCommander;
            if (commander != null)
            {
                if (response.data.grantedExplorationPoint > 0)
                    commander.UpdateExplorationPoint(response.data.explorationPointRemain);
                if (response.data.grantedAchievementPoint > 0)
                    commander.UpdateAchievementPoint(response.data.achievementPointRemain);
                commander.UpdateHasUnclaimedDailyBonus(false);
            }

            if (onResult != null) onResult(response.data);
        });
    }

    private void ApplyStatus(DailyBonusStatusResponse result)
    {
        m_claimedDaysMask      = result.claimedDaysMask;
        m_vipClaimedDaysMask   = result.vipClaimedDaysMask;
        m_todayDay             = result.todayDay;
        m_loginRewardWeekStart = ParseUtcDate(result.loginRewardWeekStart);
        m_nextAvailableAt      = ParseUtcDateTime(result.nextAvailableAt);
    }

    private void ApplyResponse(DailyClaimResponse result)
    {
        m_claimedDaysMask      = result.claimedDaysMask;
        m_vipClaimedDaysMask   = result.vipClaimedDaysMask;
        m_todayDay             = result.todayDay;
        m_loginRewardWeekStart = ParseUtcDate(result.loginRewardWeekStart);
        m_nextAvailableAt      = ParseUtcDateTime(result.nextAvailableAt);
    }

    private static DateTime ParseUtcDate(string isoDate)
    {
        DateTime dt;
        bool parsed = DateTime.TryParse(isoDate, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt);
        if (parsed == true) return dt;
        return DateTime.MinValue;
    }

    private static DateTime ParseUtcDateTime(string isoDateTime)
    {
        DateTime dt;
        bool parsed = DateTime.TryParse(isoDateTime, null, DateTimeStyles.RoundtripKind, out dt);
        if (parsed == true) return dt.ToUniversalTime();
        return DateTime.MinValue;
    }

    // 달력 리셋(다음 주 월요일 UTC 00:00)까지 남은 시간
    public TimeSpan GetWeekRemaining()
    {
        if (m_loginRewardWeekStart == DateTime.MinValue) return TimeSpan.Zero;
        DateTime weekEnd = m_loginRewardWeekStart.AddDays(7);
        TimeSpan remaining = weekEnd - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    // 다음 일일보상 수령 가능까지 남은 시간
    public TimeSpan GetDailyRemaining()
    {
        if (m_nextAvailableAt == DateTime.MinValue) return TimeSpan.Zero;
        TimeSpan remaining = m_nextAvailableAt - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
