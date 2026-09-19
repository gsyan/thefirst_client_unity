using System;
using UnityEngine;

public class DailyBonusManager : MonoSingleton<DailyBonusManager>
{
    private const int CALENDAR_DAYS = 6;

    private int m_claimedDaysMask;
    private int m_vipClaimedDaysMask;
    private int m_todayDay;

    public int GetClaimedDaysMask()    { return m_claimedDaysMask; }
    public int GetVipClaimedDaysMask() { return m_vipClaimedDaysMask; }
    public int GetTodayDay()           { return m_todayDay; }

    // 열린 칸(1~todayDay) 중 일반 미수령이 있거나, VIP면 VIP 미수령도 있는지 — 서버 hasClaimableDay와 동일 규칙
    public bool HasClaimableDay()
    {
        int unlockedMask = (1 << Mathf.Min(m_todayDay, CALENDAR_DAYS)) - 1;
        bool normalClaimable = (unlockedMask & ~m_claimedDaysMask) != 0;
        bool vipClaimable = IAPManager.Instance.IsVipActive() == true && (unlockedMask & ~m_vipClaimedDaysMask) != 0;
        return normalClaimable || vipClaimable;
    }

    // 수령할 칸이 없을 때 다음 보상이 열리기까지 남은 시간 — 6칸이 모두 열렸으면 다음 주 월요일 UTC 0시, 아니면 다음 UTC 자정(다음 출석)
    public TimeSpan GetNextRewardRemaining()
    {
        DateTime nowUtc = DateTime.UtcNow;
        DateTime nextRewardAt = nowUtc.Date.AddDays(1);
        if (m_todayDay >= CALENDAR_DAYS)
        {
            int daysToMonday = ((int)DayOfWeek.Monday - (int)nowUtc.DayOfWeek + 7) % 7;
            if (daysToMonday == 0) daysToMonday = 7;
            nextRewardAt = nowUtc.Date.AddDays(daysToMonday);
        }
        return nextRewardAt - nowUtc;
    }

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

    // 달력 팝업에서 열려있는(출석일수 이하) 미수령 칸의 Claim 버튼 클릭 시 호출 — 실제 지급이 여기서 발생.
    // claimVip로 일반/VIP 보상을 완전히 별개로 수령(claimedDaysMask/vipClaimedDaysMask 각자 독립 관리)
    public void ClaimDailyBonus(int day, bool claimVip, Action<DailyClaimResponse> onResult)
    {
        NetworkManager.Instance.ClaimVipDailyReward(day, claimVip, response =>
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
                commander.UpdateHasUnclaimedDailyBonus(HasClaimableDay());
            }

            if (onResult != null) onResult(response.data);
        });
    }

    private void ApplyStatus(DailyBonusStatusResponse result)
    {
        m_claimedDaysMask      = result.claimedDaysMask;
        m_vipClaimedDaysMask   = result.vipClaimedDaysMask;
        m_todayDay             = result.todayDay;
    }

    private void ApplyResponse(DailyClaimResponse result)
    {
        m_claimedDaysMask      = result.claimedDaysMask;
        m_vipClaimedDaysMask   = result.vipClaimedDaysMask;
        m_todayDay             = result.todayDay;
    }
}
