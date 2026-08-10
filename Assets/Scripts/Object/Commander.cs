// 플레이어 커맨더(캐릭터) 상태 관리
public class Commander
{
    public CommanderInfo m_commanderInfo;

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
