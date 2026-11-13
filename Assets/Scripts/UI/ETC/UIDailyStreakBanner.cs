// 일일퀘스트 연속 달성 배너 — 3칸 슬라이딩 창으로 연속 일수/가산 퍼센트를 표시
// 제목/퍼센트/설명/Line1/Line2는 자식 이름으로 찾고, 칸 3개는 m_slotsParent의 자식 순서대로 읽음(칸 내부는 TextMax/Image/TextStep/TextPercent 이름)
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIDailyStreakBanner : MonoBehaviour
{
    private const int k_slotCount = 3;

    private static readonly Color k_reachedColor = new Color(0.4f, 0.85f, 0.5f, 1f); // 달성 완료 칸(녹색 계열)
    private static readonly Color k_notReachedColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    private static readonly Color k_todayColor = new Color(1f, 0.85f, 0.3f, 1f); // 오늘 달성 목표 칸 강조

    // 하루당 가산 %, 최대 연속 일수는 서버와 공용인 DataTableConfig.dailyQuest에서 읽음
    private static DailyQuestSettings GetSettings()
    {
        return DataManager.Instance.m_dataTableConfig.gameSettings.dailyQuest;
    }

    // 연속 일수에 대응하는 보상 가산 %(최대 일수로 보정)
    public static int GetBonusPercent(int streakDays)
    {
        DailyQuestSettings settings = GetSettings();
        int appliedDays = Mathf.Clamp(streakDays, 0, settings.streakMaxDays);
        return appliedDays * settings.streakPercentPerDay;
    }

    private class Slot
    {
        public GameObject textMax;
        public Image circle;
        public TMP_Text textStep;
        public TMP_Text textPercent;
    }

    [SerializeField] private Transform m_slotsParent; // 칸 3개의 부모 — 자식 순서대로 1~3번 칸(자식 이름은 무관)

    private TMP_Text m_titleText;
    private TMP_Text m_currentPercentText;
    private TMP_Text m_descText;
    private Image m_line1;
    private Image m_line2;
    private readonly Slot[] m_slots = new Slot[k_slotCount];
    private bool m_isBound;

    private void Bind()
    {
        if (m_isBound == true) return;
        m_isBound = true;

        m_titleText = FindChildComponent<TMP_Text>("DailyArchievementName");
        m_currentPercentText = FindChildComponent<TMP_Text>("CurrentPercent");
        m_descText = FindChildComponent<TMP_Text>("DailyArchievementDescription");
        m_line1 = FindChildComponent<Image>("Line1");
        m_line2 = FindChildComponent<Image>("Line2");

        for (int i = 0; i < k_slotCount; i++)
        {
            if (m_slotsParent == null || i >= m_slotsParent.childCount) continue;
            Transform container = m_slotsParent.GetChild(i);

            Slot slot = new Slot();
            Transform textMax = container.Find("TextMax");
            if (textMax != null) slot.textMax = textMax.gameObject;
            Transform circle = container.Find("Image");
            if (circle != null) slot.circle = circle.GetComponent<Image>();
            Transform textStep = container.Find("TextStep");
            if (textStep != null) slot.textStep = textStep.GetComponent<TMP_Text>();
            Transform textPercent = container.Find("TextPercent");
            if (textPercent != null) slot.textPercent = textPercent.GetComponent<TMP_Text>();
            m_slots[i] = slot;
        }
    }

    private Transform FindChild(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
                return children[i];
        }
        return null;
    }

    private T FindChildComponent<T>(string childName) where T : Component
    {
        Transform child = FindChild(childName);
        if (child == null) return null;
        return child.GetComponent<T>();
    }

    // streakDays: 오늘 보상에 적용되는 연속 달성 일수(0 이상, 최대치 초과분은 최대치로 취급)
    // isTodayCompleted: 오늘의 일일퀘스트를 모두 수령했는지 — true면 오늘 칸을 달성(녹색)으로 표시(적용 일수/보너스 %는 그대로)
    public void Refresh(int streakDays, bool isTodayCompleted)
    {
        Bind();

        DailyQuestSettings settings = GetSettings();
        int percentPerDay = settings.streakPercentPerDay;
        int maxStreakDays = settings.streakMaxDays;
        int appliedDays = Mathf.Clamp(streakDays, 0, maxStreakDays);
        int appliedPercent = appliedDays * percentPerDay;
        int maxPercent = maxStreakDays * percentPerDay;

        if (m_titleText != null)
            CommonUtility.SetUILocText(m_titleText, "UIDailyStreak_Title");
        if (m_currentPercentText != null)
            m_currentPercentText.text = $"+{appliedPercent}%";
        if (m_descText != null)
        {
            string descFormat = LocalizationManager.Instance.Get("UIDailyStreak_Desc");
            m_descText.text = string.Format(descFormat, percentPerDay, maxPercent);
        }

        // 3칸 창의 시작 일수 — 오늘 칸이 가운데에 오도록 하되 양 끝에서는 고정(오늘 칸 = 적용 일수 + 1)
        int windowStartDay = Mathf.Clamp(appliedDays, 1, maxStreakDays - k_slotCount + 1);
        string maxLabel = LocalizationManager.Instance.Get("UIDailyStreak_Max");

        // 오늘 칸 = 오늘 완료 시 달성되는 일수(적용 일수 + 1) — 최대 일수 도달 후에는 최대 칸이 오늘 칸
        int todayDay = Mathf.Min(appliedDays + 1, maxStreakDays);

        // 색 표시용 달성 일수 — 오늘 완료했으면 오늘 칸까지 달성으로 칠함
        int completedDays = isTodayCompleted == true ? todayDay : appliedDays;

        for (int i = 0; i < k_slotCount; i++)
        {
            Slot slot = m_slots[i];
            if (slot == null) continue;

            int day = windowStartDay + i;
            bool isReached = day <= completedDays;
            bool isTodaySlot = day == todayDay;
            bool isMaxSlot = day == maxStreakDays;
            Color stateColor = k_notReachedColor;
            if (isReached == true) stateColor = k_reachedColor;
            else if (isTodaySlot == true) stateColor = k_todayColor;

            if (slot.textMax != null)
            {
                slot.textMax.SetActive(isMaxSlot);
                TMP_Text maxText = slot.textMax.GetComponent<TMP_Text>();
                if (maxText != null) maxText.text = maxLabel;
            }
            if (slot.circle != null) slot.circle.color = stateColor;
            if (slot.textStep != null)
            {
                slot.textStep.text = day.ToString();
                slot.textStep.color = stateColor;
            }
            if (slot.textPercent != null)
            {
                slot.textPercent.text = $"+{day * percentPerDay}%";
                slot.textPercent.color = stateColor;
            }
        }

        // 연결선 — 오른쪽 칸까지 도달했으면 밝게
        if (m_line1 != null)
            m_line1.color = windowStartDay + 1 <= completedDays ? k_reachedColor : k_notReachedColor;
        if (m_line2 != null)
            m_line2.color = windowStartDay + 2 <= completedDays ? k_reachedColor : k_notReachedColor;
    }
}
