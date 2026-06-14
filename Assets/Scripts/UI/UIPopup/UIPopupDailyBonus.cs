using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 일일 출석 보너스 달력 팝업
// claimedDaysMask: 비트마스크 (bit0=1일, bit27=28일)
// todayDay: 서버 기준 오늘 날짜 (1~28)
public class UIPopupDailyBonus : UIPopupBase
{
    private const int CALENDAR_DAYS = 28;
    private const string CELL_PREFAB_PATH = "Prefabs/UI/ETC/UIPopupDailyBonusDayCell";

    [Header("Daily Bonus Popup")]
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private TMP_Text m_rewardDescText;       // "미네랄 +N 지급!" 텍스트    
    [SerializeField] private TMP_Text m_monthValueText;       // "18일 07:32:11" (코루틴 업데이트)    
    [SerializeField] private TMP_Text m_dailyValueText;       // "04:22:09" (코루틴 업데이트)
    [SerializeField] private Transform m_gridLayoutGroup;
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private TMP_Text m_confirmButtonText;

    private UIPopupDailyBonusDayCell[] m_dayCells;
    private Action m_onConfirm;
    private Coroutine m_countdownCoroutine;

    protected override void Awake()
    {
        base.Awake();
        if (m_confirmButton != null)
            m_confirmButton.onClick.AddListener(OnConfirmClicked);
        CreateDayCells();
    }

    private void CreateDayCells()
    {
        if (m_gridLayoutGroup == null) return;

        var cellPrefab = ResourceManager.Instance.Load<UIPopupDailyBonusDayCell>(CELL_PREFAB_PATH);
        if (cellPrefab == null)
        {
            Debug.LogError("[UIPopupDailyBonus] 셀 프리팹 로드 실패: " + CELL_PREFAB_PATH);
            return;
        }

        m_dayCells = new UIPopupDailyBonusDayCell[CALENDAR_DAYS];
        for (int i = 0; i < CALENDAR_DAYS; i++)
            m_dayCells[i] = Instantiate(cellPrefab, m_gridLayoutGroup);
    }

    // 수령 직후 호출 — grantedMineral > 0이면 지급 메시지 표시
    public void ShowPopupDailyBonus(int grantedMineral, Action onConfirm)
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

        var mgr = DailyBonusManager.Instance;
        RefreshCalendar(mgr.GetClaimedDaysMask(), mgr.GetVipClaimedDaysMask(), mgr.GetTodayDay());
        StartCountdown();
    }

    // 수령 없이 열람 용도
    public void ShowCalendarOnly(Action onConfirm)
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

        var mgr = DailyBonusManager.Instance;
        RefreshCalendar(mgr.GetClaimedDaysMask(), mgr.GetVipClaimedDaysMask(), mgr.GetTodayDay());
        StartCountdown();
    }

    public override void HidePopup()
    {
        StopCountdown();
        base.HidePopup();
    }

    private void RefreshCalendar(int claimedDaysMask, int vipClaimedDaysMask, int todayDay)
    {
        var table = DataManager.Instance.m_dataTableDailyBonus;
        for (int i = 0; i < m_dayCells.Length; i++)
        {
            if (m_dayCells[i] == null) continue;
            int day = i + 1;
            if (day > CALENDAR_DAYS) break;

            bool claimed    = (claimedDaysMask    & (1 << (day - 1))) != 0;
            bool bToday     = claimed && (day == todayDay);
            bool passed     = claimed || (day < todayDay);
            bool vipClaimed = (vipClaimedDaysMask & (1 << (day - 1))) != 0;
            DailyBonusRewardEntry[] rewards = (table != null) ? table.GetRewards(day) : null;
            m_dayCells[i].SetupDailyBonusDayCell(day, claimed, vipClaimed, bToday, passed, rewards);
        }
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

            // 이번 달 남은 시간 (값만 업데이트)
            if (m_monthValueText != null)
            {
                TimeSpan month = mgr.GetMonthRemaining();
                int days = (int)month.TotalDays;
                m_monthValueText.text = days > 0
                    ? loc.Get("DailyBonus_DaysTime", days, month.Hours, month.Minutes, month.Seconds)
                    : string.Format("{0:D2}:{1:D2}:{2:D2}", (int)month.TotalHours, month.Minutes, month.Seconds);
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

    private void OnConfirmClicked()
    {
        Action cb = m_onConfirm;
        m_onConfirm = null;
        cb?.Invoke();
    }
}
