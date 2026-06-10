using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 일일 출석 보너스 달력 팝업
// claimedDaysMask: 비트마스크 (bit0=1일, bit27=28일)
// todayDay: 오늘 수령한 날짜 (1~28, 0이면 오늘 수령 없음)
public class UIPopupDailyBonus : UIPopupBase
{
    private const int CALENDAR_DAYS = 28;

    [Header("Daily Bonus Popup")]
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private TMP_Text m_rewardDescText;   // "미네랄 +N 지급!" 텍스트
    [SerializeField] private UIPopupDailyBonusDayCell[] m_dayCells; // 인스펙터에서 28개 연결
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private TMP_Text m_confirmButtonText;

    private Action m_onConfirm;

    protected override void Awake()
    {
        base.Awake();
        if (m_confirmButton != null)
            m_confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    // grantedMineral: 오늘 지급된 미네랄 (0이면 지급 메세지 숨김)
    public void ShowPopupDailyBonus(int claimedDaysMask, int todayDay, int grantedMineral, Action onConfirm)
    {
        base.ShowPopup();
        m_onConfirm = onConfirm;

        var loc = LocalizationManager.Instance;

        if (m_titleText != null)
            m_titleText.text = loc.Get("DailyBonus_Title");

        if (m_rewardDescText != null)
        {
            bool hasReward = grantedMineral > 0;
            m_rewardDescText.gameObject.SetActive(hasReward);
            if (hasReward == true)
                m_rewardDescText.text = loc.Get("DailyBonus_Desc", grantedMineral);
        }

        if (m_confirmButtonText != null)
            m_confirmButtonText.text = loc.Get("Simple_Confirm");

        int todayInMonth = DateTime.UtcNow.Day;
        RefreshCalendar(claimedDaysMask, todayDay, todayInMonth);
    }

    // 달력 재갱신 (수령 없이 열람 용도)
    public void ShowCalendarOnly(int claimedDaysMask, Action onConfirm)
    {
        base.ShowPopup();
        m_onConfirm = onConfirm;

        var loc = LocalizationManager.Instance;

        if (m_titleText != null)
            m_titleText.text = loc.Get("DailyBonus_Title");

        if (m_rewardDescText != null)
            m_rewardDescText.gameObject.SetActive(false);

        if (m_confirmButtonText != null)
            m_confirmButtonText.text = loc.Get("Simple_Confirm");

        int todayInMonth = DateTime.UtcNow.Day;
        RefreshCalendar(claimedDaysMask, 0, todayInMonth);
    }

    private void RefreshCalendar(int claimedDaysMask, int todayDay, int todayInMonth)
    {
        for (int i = 0; i < m_dayCells.Length; i++)
        {
            if (m_dayCells[i] == null) continue;
            int day = i + 1; // 1~28
            if (day > CALENDAR_DAYS) break;

            UIPopupDailyBonusDayCell.EDailyBonusCellState state = ResolveCellState(claimedDaysMask, day, todayDay, todayInMonth);
            m_dayCells[i].Setup(day, state);
        }
    }

    private UIPopupDailyBonusDayCell.EDailyBonusCellState ResolveCellState(int mask, int day, int todayDay, int todayInMonth)
    {
        bool claimed = (mask & (1 << (day - 1))) != 0;
        if (claimed == true)
        {
            if (day == todayDay) return UIPopupDailyBonusDayCell.EDailyBonusCellState.ClaimedToday;
            return UIPopupDailyBonusDayCell.EDailyBonusCellState.Claimed;
        }
        // 지나간 날이지만 못 받음 (일반 유저 미수령)
        if (day < todayInMonth) return UIPopupDailyBonusDayCell.EDailyBonusCellState.MissedNoReward;
        return UIPopupDailyBonusDayCell.EDailyBonusCellState.Future;
    }

    private void OnConfirmClicked()
    {
        Action cb = m_onConfirm;
        m_onConfirm = null;
        cb?.Invoke();
    }
}
