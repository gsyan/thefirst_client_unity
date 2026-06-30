// 플레이어 커맨더(캐릭터) 상태 관리
using System.Collections.Generic;

public class Commander
{
    public CommanderInfo m_commanderInfo;
    private HashSet<string> m_completedResearchIds; // 모듈 연구 등 문자열 기반 완료 연구

    public Commander(CommanderInfo commanderInfo)
    {
        m_commanderInfo = commanderInfo;
        m_completedResearchIds = new HashSet<string>();
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

    public int GetMineral()
    {
        if (m_commanderInfo == null) return 0;
        return m_commanderInfo.mineral;
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

    public int GetModulePoint()
    {
        if (m_commanderInfo == null) return 0;
        return m_commanderInfo.modulePoint;
    }

    public int GetModulePointMaxGot()
    {
        if (m_commanderInfo == null) return 0;
        return m_commanderInfo.modulePointMaxGot;
    }

    public void UpdateModulePoint(int modulePoint)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.modulePoint = modulePoint;
        EventManager.TriggerModulePointChanged(modulePoint);
    }

    public void UpdateModulePointMaxGot(int modulePointMaxGot)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.modulePointMaxGot = modulePointMaxGot;
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

    public void UpdateMineral(int mineral)
    {
        if (m_commanderInfo == null) return;
        m_commanderInfo.mineral = mineral;
        EventManager.TriggerMineralChange(mineral);
    }



    public bool CheckEnoughMineral(long cost)
    {
        if (m_commanderInfo == null) return false;
        return m_commanderInfo.mineral >= cost;
    }

    public bool TryConsumeMineral(int amount)
    {
        if (m_commanderInfo == null) return false;
        if (m_commanderInfo.mineral < amount) return false;
        m_commanderInfo.mineral -= amount;
        EventManager.TriggerMineralChange(m_commanderInfo.mineral);
        return true;
    }

    public bool CheckEnoughExp(long cost)
    {
        if (m_commanderInfo == null) return false;
        return m_commanderInfo.exp >= cost;
    }

    public bool CheckEnoughModulePoint(long cost)
    {
        if (m_commanderInfo == null) return false;
        return m_commanderInfo.modulePoint >= cost;
    }




    // 문자열 기반 완료 연구 ID 목록 세팅 (모듈 연구용)
    public void SetCompletedResearchIds(string[] ids)
    {
        m_completedResearchIds.Clear();
        if (ids == null) return;
        foreach (string id in ids)
            m_completedResearchIds.Add(id);
    }

    // 연구 완료 후 단건 추가 (모듈 연구용)
    public void AddCompletedResearchId(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        m_completedResearchIds.Add(id);
    }

    public bool IsResearchCompleted(string researchId)
    {
        return m_completedResearchIds.Contains(researchId);
    }

}
