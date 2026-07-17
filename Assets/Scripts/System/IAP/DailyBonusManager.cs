using System;
using System.Globalization;
using UnityEngine;

public class DailyBonusManager : MonoSingleton<DailyBonusManager>
{
    private int m_claimedDaysMask;
    private int m_vipClaimedDaysMask;
    private int m_todayDay;
    private int m_loginRewardMonth;      // yyyyMM
    private DateTime m_nextAvailableAt;  // UTC, MinValue = 데이터 없음

    public int GetClaimedDaysMask()    { return m_claimedDaysMask; }
    public int GetVipClaimedDaysMask() { return m_vipClaimedDaysMask; }
    public int GetTodayDay()           { return m_todayDay; }
    public int GetLoginRewardMonth()   { return m_loginRewardMonth; }
    public DateTime GetNextAvailableAt() { return m_nextAvailableAt; }

    public void TryClaimDailyBonus(Action<DailyClaimResponse> onResult)
    {
        NetworkManager.Instance.ClaimVipDailyReward(response =>
        {
            if (response != null && response.errorCode == (int)ServerErrorCode.SUCCESS)
            {
                if (response.data != null)
                    ApplyResponse(response.data);
                onResult?.Invoke(response.data);
            }
            else
                onResult?.Invoke(null);
        });
    }

    // VIP 일일 미네랄 팝업 — 튜토리얼 진행 중에는 호출하면 안 됨(ObjectManager.StartNormalPlay에서만 호출)
    // onClosed: 팝업이 실제로 떴다가 닫힌 시점(또는 애초에 뜨지 않은 경우 즉시) 호출 — 이 팝업이 화면을 막고 있는 동안
    // 다른 튜토리얼(Tutorial_Exploration 등)이 UI를 가리키지 않도록 시작 시점을 늦추는 용도
    public void CheckAndShowDailyRewardPopup(System.Action onClosed = null)
    {
        TryClaimDailyBonus(result =>
        {
            if (result == null || result.available == false)
            {
                onClosed?.Invoke();
                return;
            }

            var commander = DataManager.Instance.m_currentCommander;
            if (commander != null)
                commander.UpdateMineral(result.mineralRemain);

            UIManager.Instance.ShowDailyBonusPopup(result.grantedMineral, onClosed);
        });
    }

    public void ApplyResponse(DailyClaimResponse result)
    {
        m_claimedDaysMask    = result.claimedDaysMask;
        m_vipClaimedDaysMask = result.vipClaimedDaysMask;
        m_todayDay           = result.todayDay;
        m_loginRewardMonth   = result.loginRewardMonth;

        if (DateTime.TryParse(result.nextAvailableAt, null,
            DateTimeStyles.RoundtripKind, out DateTime dt))
            m_nextAvailableAt = dt.ToUniversalTime();
        else
            m_nextAvailableAt = DateTime.MinValue;
    }

    // 달력 리셋(다음 달 1일 UTC 00:00)까지 남은 시간
    public TimeSpan GetMonthRemaining()
    {
        if (m_loginRewardMonth == 0) return TimeSpan.Zero;
        int year  = m_loginRewardMonth / 100;
        int month = m_loginRewardMonth % 100;
        int nextYear  = month == 12 ? year + 1 : year;
        int nextMonth = month == 12 ? 1 : month + 1;
        DateTime monthEnd = new DateTime(nextYear, nextMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        TimeSpan remaining = monthEnd - DateTime.UtcNow;
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
