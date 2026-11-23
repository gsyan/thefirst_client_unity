// 일일퀘스트(파랑)/아키타입 패시브 미션(황금) 진행도가 오르면 화면 상단에 항목마다 한 줄씩 표시 — 일일퀘 줄은 항상 위, 패시브 줄은 항상 아래
// 전투 중 값은 클라 집계(BattleStatTracker) 기준(퇴각 시 서버 미반영), 셀 클리어/존 탈출 확정 후엔 서버값으로 오른 일일퀘스트를 추가 표시. 패널 위·팝업 아래 컨테이너에 배치
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIBattleProgressNotice : MonoBehaviour
{
    [SerializeField] private TMP_Text m_rowTemplate; // 줄 템플릿 — Awake에서 k_maxRows개 복제해 같은 부모(VerticalLayoutGroup) 아래에 풀로 둠, 템플릿 자신은 비활성

    private static readonly Color k_dailyColor = new Color(0.4f, 0.75f, 1f);
    private static readonly Color k_passiveColor = new Color(0.91764706f, 0.69411767f, 0.12156863f); // 제독 승급 버튼 라벨 색과 동일
    private const int k_maxRows = 6;
    private const float k_pollIntervalSec = 0.1f;
    private const float k_holdSec = 3f;   // 마지막 변화 후 유지 시간
    private const float k_fadeSec = 0.4f;

    private class NoticeRow
    {
        public TMP_Text text;
        public string key;          // 같은 항목이면 이 줄을 그대로 갱신
        public bool isDaily;
        public float changedTime = -1f; // -1 = 비어 있음
    }

    private class DailyTrack
    {
        public AchievementData data;
        public long baseValue;  // 전투 시작 시점 서버 진행도
        public long lastShown;
    }

    private class PassiveTrack
    {
        public string key;      // "아키타입:카운터" — 줄 식별용(폴링 중 문자열 생성을 피하려고 미리 만들어 둠)
        public string hullArchetype;
        public string counterType;
        public string label;    // "켄투리온함 패시브2 퀘스트 : 적 함선 파괴"
        public int threshold;
        public long baseValue;  // 전투 시작 시점 서버 진행도(이 단계 안에서의 값)
        public long lastShown;
    }

    private readonly List<NoticeRow> m_rows = new List<NoticeRow>();
    private readonly List<NoticeRow> m_reorderBuffer = new List<NoticeRow>();
    private readonly List<DailyTrack> m_dailyTracks = new List<DailyTrack>();
    private readonly List<PassiveTrack> m_passiveTracks = new List<PassiveTrack>();
    private readonly Dictionary<string, long> m_knownDailyValues = new Dictionary<string, long>(); // 퀘스트별 마지막으로 알던(표시한) 값 — 서버 확정 후 오른 분량 판정 기준
    private WaitForSecondsRealtime m_pollWait;
    private Coroutine m_pollCoroutine;

    private void Awake()
    {
        m_pollWait = new WaitForSecondsRealtime(k_pollIntervalSec);
        BuildRowPool();
        EventManager.Subscribe_BattleStatCollectingChanged(OnBattleStatCollectingChanged);
        EventManager.Subscribe_DailyProgressConfirmed(OnDailyProgressConfirmed);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_BattleStatCollectingChanged(OnBattleStatCollectingChanged);
        EventManager.Unsubscribe_DailyProgressConfirmed(OnDailyProgressConfirmed);
    }

    private void BuildRowPool()
    {
        if (m_rowTemplate == null) return;

        m_rowTemplate.gameObject.SetActive(false);
        Transform rowParent = m_rowTemplate.transform.parent;
        for (int i = 0; i < k_maxRows; i++)
        {
            TMP_Text rowText = Instantiate(m_rowTemplate, rowParent);
            rowText.raycastTarget = false; // 아래 패널 터치를 가로채지 않도록
            rowText.gameObject.SetActive(false);
            m_rows.Add(new NoticeRow { text = rowText });
        }
    }

    private void OnBattleStatCollectingChanged(bool isCollecting)
    {
        if (isCollecting == false) return; // 종료 후엔 표시 중인 줄만 마저 사라지게 둠(폴링은 IsCollecting으로 자동 중단)

        m_dailyTracks.Clear();
        BuildPassiveTracks();
        // 오늘 일일퀘스트 진행도는 서버에만 있으므로 전투 시작마다 조용히(스피너 없이) 받아옴
        NetworkManager.Instance.GetDailyAchievementList(new GetDailyAchievementListRequest(), OnGetDailyAchievementListResponse, ERequestPolicy.Silent);
        EnsurePolling();
    }

    private void OnGetDailyAchievementListResponse(ApiResponse<GetDailyAchievementListResponse> response)
    {
        if (response == null || response.errorCode != 0 || response.data == null || response.data.achievements == null) return;
        if (BattleStatTracker.IsCollecting() == false) return; // 응답 전에 전투가 이미 끝남

        m_dailyTracks.Clear();
        DataTableDailyAchievement table = DataManager.Instance.m_dataTableDailyAchievement;
        foreach (DailyAchievementStatus status in response.data.achievements)
        {
            AchievementData data = table.GetDailyAchievement(status.achievementId);
            if (data == null) continue;
            m_knownDailyValues[status.achievementId] = status.currentValue;
            if (status.isClaimed == true || status.currentValue >= data.threshold) continue;
            if (BattleStatTracker.TryGetLiveDailyCounter(data.conditionType, out long liveValue) == false) continue; // 클리어로만 정해지는 조건

            long shownValue = status.currentValue + liveValue;
            m_dailyTracks.Add(new DailyTrack { data = data, baseValue = status.currentValue, lastShown = shownValue });
        }
    }

    // 셀 클리어/존 탈출 확정 — 서버에서만 오르는 진행도(셀 클리어, 존 클리어, 클리어 광물 등)를 다시 받아 오른 항목을 표시
    private void OnDailyProgressConfirmed()
    {
        NetworkManager.Instance.GetDailyAchievementList(new GetDailyAchievementListRequest(), OnConfirmedDailyListResponse, ERequestPolicy.Silent);
    }

    private void OnConfirmedDailyListResponse(ApiResponse<GetDailyAchievementListResponse> response)
    {
        if (response == null || response.errorCode != 0 || response.data == null || response.data.achievements == null) return;

        DataTableDailyAchievement table = DataManager.Instance.m_dataTableDailyAchievement;
        foreach (DailyAchievementStatus status in response.data.achievements)
        {
            AchievementData data = table.GetDailyAchievement(status.achievementId);
            if (data == null) continue;

            bool hasKnownValue = m_knownDailyValues.TryGetValue(status.achievementId, out long knownValue);
            m_knownDailyValues[status.achievementId] = status.currentValue;
            if (hasKnownValue == false) continue; // 비교 기준이 없으면(이번 실행 중 처음 받은 값) 표시하지 않음
            if (status.isClaimed == true || status.currentValue <= knownValue) continue;
            if (knownValue >= data.threshold) continue; // 이미 완료돼 있던 퀘스트

            ShowNotice(data.achievementId, true, BuildDailyMessage(data, status.currentValue));
        }
        EnsurePolling();
    }

    // 내 함대 각 아키타입의 "다음 미해금 단계" 하나씩 — 단계는 순차 해금이라 가장 앞의 미완료 단계만 진행됨
    private void BuildPassiveTracks()
    {
        m_passiveTracks.Clear();
        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        Commander commander = DataManager.Instance.m_currentCommander;
        if (myFleet == null || commander == null) return;

        for (int i = 0; i < myFleet.m_ships.Count; i++)
        {
            SpaceShip ship = myFleet.m_ships[i];
            if (ship == null || ship.m_moduleHulls.Count == 0) continue;

            string hullArchetype = ship.m_moduleHulls[0].GetHullArchetype();
            if (string.IsNullOrEmpty(hullArchetype) == true) continue;

            List<HullArchetypePassiveData> passives = DataManager.Instance.m_dataTableHullArchetypePassive.GetPassivesForArchetype(hullArchetype);
            for (int j = 0; j < passives.Count; j++)
            {
                HullArchetypePassiveData passive = passives[j];
                commander.GetHullArchetypePassiveStageProgress(hullArchetype, passive.stage, passive.threshold, out long currentValue, out bool isStageCompleted);
                if (isStageCompleted == true) continue;

                string archetypeName = LocalizationManager.Instance.Get($"HullArchetype_{hullArchetype}");
                string missionLabel = LocalizationManager.Instance.Get($"HullArchetypePassiveMission_{passive.counterType}");
                string passivePrefix = LocalizationManager.Instance.Get("UIBattleProgressNotice_PassivePrefix", archetypeName, passive.stage);
                long liveValue = BattleStatTracker.GetArchetypeCounterValue(hullArchetype, passive.counterType);
                m_passiveTracks.Add(new PassiveTrack
                {
                    key = $"{hullArchetype}:{passive.counterType}",
                    hullArchetype = hullArchetype,
                    counterType = passive.counterType,
                    label = $"{passivePrefix} : {missionLabel}",
                    threshold = passive.threshold,
                    baseValue = currentValue,
                    lastShown = currentValue + liveValue,
                });
                break;
            }
        }
    }

    private void EnsurePolling()
    {
        if (m_pollCoroutine == null)
            m_pollCoroutine = StartCoroutine(PollRoutine());
    }

    private IEnumerator PollRoutine()
    {
        while (BattleStatTracker.IsCollecting() == true || HasVisibleRow() == true)
        {
            if (BattleStatTracker.IsCollecting() == true)
            {
                PollDaily();
                PollPassive();
            }

            UpdateRowFades();
            yield return m_pollWait;
        }
        m_pollCoroutine = null;
    }

    private void PollDaily()
    {
        for (int i = 0; i < m_dailyTracks.Count; i++)
        {
            DailyTrack track = m_dailyTracks[i];
            BattleStatTracker.TryGetLiveDailyCounter(track.data.conditionType, out long liveValue);
            long totalValue = track.baseValue + liveValue;
            long shownValue = System.Math.Min(totalValue, track.data.threshold);
            if (shownValue == track.lastShown) continue;

            track.lastShown = shownValue;
            m_knownDailyValues[track.data.achievementId] = shownValue; // 클리어 확정 후 같은 증가분을 다시 띄우지 않도록
            ShowNotice(track.data.achievementId, true, BuildDailyMessage(track.data, shownValue));
        }
    }

    private void PollPassive()
    {
        for (int i = 0; i < m_passiveTracks.Count; i++)
        {
            PassiveTrack track = m_passiveTracks[i];
            long liveValue = BattleStatTracker.GetArchetypeCounterValue(track.hullArchetype, track.counterType);
            long totalValue = track.baseValue + liveValue;
            long shownValue = System.Math.Min(totalValue, track.threshold);
            if (shownValue == track.lastShown) continue;

            track.lastShown = shownValue;
            ShowNotice(track.key, false, $"{track.label} {CommonUtility.FormatNumber(shownValue)}/{CommonUtility.FormatNumber(track.threshold)}");
        }
    }

    // UIAchievementRow와 같은 포맷 인자({0}=조건 파라미터, {1}=목표치, {2}=보상) + 진행도, 앞에 업적 패널 순서 번호(쉬움1/중간2/어려움3)
    private string BuildDailyMessage(AchievementData data, long value)
    {
        long shownValue = System.Math.Min(value, data.threshold);
        int questNumber = GetDailyQuestNumber(data.difficulty);
        string dailyPrefix = LocalizationManager.Instance.Get("UIBattleProgressNotice_DailyPrefix", questNumber);
        string questName = BuildDailyNoticeName(data);
        return $"{dailyPrefix} : {questName} {CommonUtility.FormatNumber(shownValue)}/{CommonUtility.FormatNumber(data.threshold)}";
    }

    // 목표치는 진행도에 이미 나오므로 숫자 없는 알림용 이름(UIDailyAchievement_Notice_*)을 우선 사용, 키가 없으면 원래 이름
    private string BuildDailyNoticeName(AchievementData data)
    {
        string noticeKey = data.nameKey.Replace("UIDailyAchievement_Name_", "UIDailyAchievement_Notice_");
        string noticeFormat = LocalizationManager.Instance.Get(noticeKey);
        bool hasNoticeName = noticeFormat != noticeKey;
        string format = hasNoticeName == true ? noticeFormat : LocalizationManager.Instance.Get(data.nameKey);
        return string.Format(format, data.conditionParam, data.threshold, data.achievementPointReward);
    }

    // UIPanelAchievement.GetDifficultyRank와 같은 순서
    private int GetDailyQuestNumber(string difficulty)
    {
        if (difficulty == "easy") return 1;
        if (difficulty == "normal") return 2;
        if (difficulty == "hard") return 3;
        return 0;
    }

    // 같은 항목이 떠 있으면 그 줄만 갱신, 없으면 빈 줄(없으면 가장 오래 갱신 안 된 줄)을 새 항목으로 사용
    private void ShowNotice(string key, bool isDaily, string message)
    {
        NoticeRow row = FindVisibleRow(key, isDaily);
        bool isNewRow = row == null;
        if (isNewRow == true)
            row = AcquireRow();
        if (row == null) return;

        row.key = key;
        row.isDaily = isDaily;
        row.changedTime = Time.realtimeSinceStartup;
        row.text.text = message;
        row.text.color = isDaily == true ? k_dailyColor : k_passiveColor;
        row.text.alpha = 1f;
        if (row.text.gameObject.activeSelf == false)
            row.text.gameObject.SetActive(true);

        if (isNewRow == true)
            PlaceNewRow(row);
        EnsurePolling();
    }

    private NoticeRow FindVisibleRow(string key, bool isDaily)
    {
        for (int i = 0; i < m_rows.Count; i++)
        {
            NoticeRow row = m_rows[i];
            if (row.changedTime >= 0f && row.isDaily == isDaily && row.key == key) return row;
        }
        return null;
    }

    private NoticeRow AcquireRow()
    {
        NoticeRow oldestRow = null;
        for (int i = 0; i < m_rows.Count; i++)
        {
            NoticeRow row = m_rows[i];
            if (row.changedTime < 0f) return row;
            if (oldestRow == null || row.changedTime < oldestRow.changedTime) oldestRow = row;
        }
        return oldestRow;
    }

    // 새 줄은 맨 아래로 — 일일퀘 줄이면 떠 있는 패시브 줄들을 기존 순서대로 다시 그 아래로 내려 "일일퀘 위/패시브 아래"를 유지
    private void PlaceNewRow(NoticeRow newRow)
    {
        newRow.text.transform.SetAsLastSibling();
        if (newRow.isDaily == false) return;

        m_reorderBuffer.Clear();
        Transform rowParent = newRow.text.transform.parent;
        for (int i = 0; i < rowParent.childCount; i++)
        {
            Transform child = rowParent.GetChild(i);
            for (int j = 0; j < m_rows.Count; j++)
            {
                NoticeRow row = m_rows[j];
                if (row.text.transform == child && row.changedTime >= 0f && row.isDaily == false)
                    m_reorderBuffer.Add(row);
            }
        }
        for (int i = 0; i < m_reorderBuffer.Count; i++)
            m_reorderBuffer[i].text.transform.SetAsLastSibling();
        m_reorderBuffer.Clear();
    }

    private bool HasVisibleRow()
    {
        for (int i = 0; i < m_rows.Count; i++)
        {
            if (m_rows[i].changedTime >= 0f) return true;
        }
        return false;
    }

    // 마지막 변화 후 k_holdSec 유지 → k_fadeSec 동안 흐려지며 사라짐
    private void UpdateRowFades()
    {
        float now = Time.realtimeSinceStartup;
        for (int i = 0; i < m_rows.Count; i++)
        {
            NoticeRow row = m_rows[i];
            if (row.changedTime < 0f) continue;

            float elapsed = now - row.changedTime;
            if (elapsed <= k_holdSec) continue;

            float fadeProgress = (elapsed - k_holdSec) / k_fadeSec;
            if (fadeProgress >= 1f)
            {
                row.changedTime = -1f;
                row.key = null;
                row.text.gameObject.SetActive(false);
                continue;
            }
            row.text.alpha = 1f - fadeProgress;
        }
    }
}
