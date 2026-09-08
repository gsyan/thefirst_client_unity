// 플레이어 커맨더(캐릭터) 상태 관리
public class Commander
{
    public CommanderInfo m_commanderInfo;

    // 완료했지만 아직 안 받은 업적 존재 여부 — 서버에서 안 내려오면(로그인 직후 등) false로 시작, 존런 정산/업적 패널 응답으로 갱신됨(레드닷 표시용)
    private bool m_hasUnclaimedAchievement;

    // 오늘 수령 가능한 출석 보상이 남아있는지 — 로그인 시점 DailyBonusManager.CheckDailyBonusStatus() 응답으로 갱신됨(레드닷 표시용)
    private bool m_hasUnclaimedDailyBonus;

    public Commander(CommanderInfo commanderInfo)
    {
        m_commanderInfo = commanderInfo;
    }

    // "commander_" 로 시작하면 로컬라이즈된 이름 + commanderId 반환
    public static string GetDisplayName(string rawName, long commanderId)
    {
        if (string.IsNullOrEmpty(rawName) || rawName.StartsWith("commander_", System.StringComparison.OrdinalIgnoreCase))
            return LocalizationManager.Instance.Get("char_default_name") + commanderId;
        return rawName;
    }

    public string GetName()
    {
        if (m_commanderInfo == null) return GetDisplayName("", 0);
        return GetDisplayName(m_commanderInfo.commanderName, m_commanderInfo.commanderId);
    }

    public int GetExp()
    {
        if (m_commanderInfo == null) return 0;
        return m_commanderInfo.exp;
    }

    public void UpdateExp(int exp)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.exp = exp;
        EventManager.TriggerCommanderExpChanged(exp);
    }

    public int GetPvpPoint()
    {
        if (m_commanderInfo == null) return 0;
        return m_commanderInfo.pvpPoint;
    }

    public int GetPvpPointMaxGot()
    {
        if (m_commanderInfo == null) return 0;
        return m_commanderInfo.pvpPointMaxGot;
    }

    public void UpdatePvpPoint(int pvpPoint)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.pvpPoint = pvpPoint;
        EventManager.TriggerPvpPointChanged(pvpPoint);
    }

    public void UpdatePvpPointMaxGot(int pvpPointMaxGot)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.pvpPointMaxGot = pvpPointMaxGot;
    }

    public int GetExplorationPoint()
    {
        if (m_commanderInfo == null) return 0;
        return m_commanderInfo.explorationPoint;
    }

    public void UpdateExplorationPoint(int explorationPoint)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.explorationPoint = explorationPoint;
        EventManager.TriggerExplorationPointChanged(explorationPoint);
    }

    public int GetAchievementPoint()
    {
        if (m_commanderInfo == null) return 0;
        return m_commanderInfo.achievementPoint;
    }

    public void UpdateAchievementPoint(int achievementPoint)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.achievementPoint = achievementPoint;
        EventManager.TriggerAchievementPointChanged(achievementPoint);
    }

    public bool GetHasUnclaimedAchievement()
    {
        return m_hasUnclaimedAchievement;
    }

    // 값이 실제로 바뀔 때만 이벤트 발행 — 존런 정산/업적 패널 열기/받기 등 여러 갱신 지점에서 같은 상태로 중복 호출돼도 스팸 안 남
    public void UpdateHasUnclaimedAchievement(bool hasUnclaimedAchievement)
    {
        if (m_hasUnclaimedAchievement == hasUnclaimedAchievement) return;
        m_hasUnclaimedAchievement = hasUnclaimedAchievement;
        EventManager.TriggerUnclaimedAchievementChanged(hasUnclaimedAchievement);
    }

    public bool GetHasUnclaimedDailyBonus()
    {
        return m_hasUnclaimedDailyBonus;
    }

    // 값이 실제로 바뀔 때만 이벤트 발행(위 업적과 동일한 패턴)
    public void UpdateHasUnclaimedDailyBonus(bool hasUnclaimedDailyBonus)
    {
        if (m_hasUnclaimedDailyBonus == hasUnclaimedDailyBonus) return;
        m_hasUnclaimedDailyBonus = hasUnclaimedDailyBonus;
        EventManager.TriggerUnclaimedDailyBonusChanged(hasUnclaimedDailyBonus);
    }

    public bool IsHullUnlocked(string hullSubType)
    {
        if (m_commanderInfo == null || m_commanderInfo.unlockedHulls == null) return false;
        return m_commanderInfo.unlockedHulls.Contains(hullSubType);
    }

    // UnlockHull API 성공 응답의 unlockedHulls(권위값)로 갱신 — 호출부(UIHullPickerView)가 직접 화면을 다시 그림
    public void UpdateUnlockedHulls(System.Collections.Generic.List<string> unlockedHulls)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.unlockedHulls = unlockedHulls;
    }

    public int GetCommanderLevel()
    {
        if (m_commanderInfo == null) return 1;
        int level = m_commanderInfo.commanderLevel;
        return level > 0 ? level : 1;
    }

    // 서버 응답 commanderLevel로 갱신 후 이벤트 발생
    public void UpdateCommanderLevel(int newLevel)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.commanderLevel = newLevel;
        EventManager.TriggerCommanderLevelChange(newLevel);
    }

    public CommanderInfo GetInfo()
    {
        return m_commanderInfo;
    }

    public void UpdateCommanderInfo(CommanderInfo commanderInfo)
    {
        m_commanderInfo = commanderInfo;
        UpdateHasUnclaimedAchievement(commanderInfo.hasUnclaimedAchievement);
    }

    public void UpdateCommanderName(string commanderName, int nameChangeCount)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.commanderName = commanderName;
        m_commanderInfo.nameChangeCount = nameChangeCount;
    }

    public bool CheckEnoughExp(long cost)
    {
        if (m_commanderInfo == null) return false;
        return m_commanderInfo.exp >= cost;
    }

}
