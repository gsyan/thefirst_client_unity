using System;
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
    [SerializeField] private TMP_Text m_rewardDescText;   // "미네랄 +N 지급!" 텍스트
    [SerializeField] private Transform m_gridLayoutGroup;
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private TMP_Text m_confirmButtonText;

    private UIPopupDailyBonusDayCell[] m_dayCells;
    private Action m_onConfirm;

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

        var cellPrefab = Resources.Load<UIPopupDailyBonusDayCell>(CELL_PREFAB_PATH);
        if (cellPrefab == null)
        {
            Debug.LogError("[UIPopupDailyBonus] 셀 프리팹 로드 실패: " + CELL_PREFAB_PATH);
            return;
        }

        m_dayCells = new UIPopupDailyBonusDayCell[CALENDAR_DAYS];
        for (int i = 0; i < CALENDAR_DAYS; i++)
            m_dayCells[i] = Instantiate(cellPrefab, m_gridLayoutGroup);
    }

    // grantedMineral: 오늘 지급된 미네랄 (0이면 지급 메세지 숨김)
    public void ShowPopupDailyBonus(int claimedDaysMask, int vipClaimedDaysMask, int todayDay, int grantedMineral, Action onConfirm)
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

        RefreshCalendar(claimedDaysMask, vipClaimedDaysMask, todayDay);
    }

    // 달력 재갱신 (수령 없이 열람 용도)
    public void ShowCalendarOnly(int claimedDaysMask, int vipClaimedDaysMask, Action onConfirm)
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

        var character = DataManager.Instance.m_currentCharacter;
        int todayDay = (character != null) ? character.GetTodayDay() : 0;
        RefreshCalendar(claimedDaysMask, vipClaimedDaysMask, todayDay);
    }

    private void RefreshCalendar(int claimedDaysMask, int vipClaimedDaysMask, int todayDay)
    {
        var table = DataManager.Instance.m_dataTableDailyBonus;
        for (int i = 0; i < m_dayCells.Length; i++)
        {
            if (m_dayCells[i] == null) continue;
            int day = i + 1; // 1~28
            if (day > CALENDAR_DAYS) break;

            UIPopupDailyBonusDayCell.EDailyBonusCellState state = ResolveCellState(claimedDaysMask, day, todayDay);
            bool vipClaimed = (vipClaimedDaysMask & (1 << (day - 1))) != 0;
            DailyBonusRewardEntry[] rewards = (table != null) ? table.GetRewards(day) : null;
            m_dayCells[i].SetupDailyBonusDayCell(day, state, vipClaimed, rewards);
        }
    }

    private UIPopupDailyBonusDayCell.EDailyBonusCellState ResolveCellState(int mask, int day, int todayDay)
    {
        bool claimed = (mask & (1 << (day - 1))) != 0;
        if (claimed == true)
        {
            if (day == todayDay) return UIPopupDailyBonusDayCell.EDailyBonusCellState.ClaimedToday;
            return UIPopupDailyBonusDayCell.EDailyBonusCellState.Claimed;
        }
        if (day < todayDay) return UIPopupDailyBonusDayCell.EDailyBonusCellState.MissedNoReward;
        return UIPopupDailyBonusDayCell.EDailyBonusCellState.Future;
    }

    private void OnConfirmClicked()
    {
        Action cb = m_onConfirm;
        m_onConfirm = null;
        cb?.Invoke();
    }
}
