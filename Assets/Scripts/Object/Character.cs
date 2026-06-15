// 플레이어 캐릭터 상태 관리
using System.Collections.Generic;

public class Character
{
    public CharacterInfo m_characterInfo;
    private HashSet<string> m_completedResearchIds; // 모듈 연구 등 문자열 기반 완료 연구

    public Character(CharacterInfo characterInfo)
    {
        m_characterInfo = characterInfo;
        m_completedResearchIds = new HashSet<string>();
    }

    // "commander_" 로 시작하면 로컬라이즈된 이름 + characterId 반환
    public static string GetDisplayName(string rawName, long characterId)
    {
        if (string.IsNullOrEmpty(rawName) || rawName.StartsWith("commander_", System.StringComparison.OrdinalIgnoreCase))
            return LocalizationManager.Instance.Get("char_default_name") + characterId;
        return rawName;
    }

    public string GetName()
    {
        if (m_characterInfo == null) return GetDisplayName("", 0);
        return GetDisplayName(m_characterInfo.characterName, m_characterInfo.characterId);
    }

    public int GetMineral()
    {
        if (m_characterInfo == null) return 0;
        return m_characterInfo.mineral;
    }

    public int GetTechPoint()
    {
        if (m_characterInfo == null) return 0;
        return m_characterInfo.techPoint;
    }

    public void UpdateTechPoint(int techPoint)
    {
        if (m_characterInfo == null) return;
        m_characterInfo.techPoint = techPoint;
        EventManager.TriggerTechPointChanged(techPoint);
    }

    public int GetModulePoint()
    {
        if (m_characterInfo == null) return 0;
        return m_characterInfo.modulePoint;
    }

    public int GetModulePointMaxGot()
    {
        if (m_characterInfo == null) return 0;
        return m_characterInfo.modulePointMaxGot;
    }

    public void UpdateModulePoint(int modulePoint)
    {
        if (m_characterInfo == null) return;
        m_characterInfo.modulePoint = modulePoint;
        EventManager.TriggerModulePointChanged(modulePoint);
    }

    public void UpdateModulePointMaxGot(int modulePointMaxGot)
    {
        if (m_characterInfo == null) return;
        m_characterInfo.modulePointMaxGot = modulePointMaxGot;
    }

    public int GetPvpPoint()
    {
        if (m_characterInfo == null) return 0;
        return m_characterInfo.pvpPoint;
    }

    public int GetPvpPointMaxGot()
    {
        if (m_characterInfo == null) return 0;
        return m_characterInfo.pvpPointMaxGot;
    }

    public void UpdatePvpPoint(int pvpPoint)
    {
        if (m_characterInfo == null) return;
        m_characterInfo.pvpPoint = pvpPoint;
        EventManager.TriggerPvpPointChanged(pvpPoint);
    }

    public void UpdatePvpPointMaxGot(int pvpPointMaxGot)
    {
        if (m_characterInfo == null) return;
        m_characterInfo.pvpPointMaxGot = pvpPointMaxGot;
    }

    public int GetTechLevel()
    {
        if (m_characterInfo == null) return 1;
        int level = m_characterInfo.techLevel;
        return level > 0 ? level : 1;
    }

    // 서버 응답 techLevel로 갱신 후 이벤트 발생
    public void UpdateTechLevel(int newLevel)
    {
        if (m_characterInfo == null) return;
        m_characterInfo.techLevel = newLevel;
        EventManager.TriggerTechLevelChange(newLevel);
    }

    public CharacterInfo GetInfo()
    {
        return m_characterInfo;
    }

    public void UpdateCharacterInfo(CharacterInfo characterInfo)
    {
        m_characterInfo = characterInfo;
    }

    public void UpdateCharacterName(string characterName, int nameChangeCount)
    {
        if (m_characterInfo == null) return;
        m_characterInfo.characterName = characterName;
        m_characterInfo.nameChangeCount = nameChangeCount;
    }

    public void UpdateMineral(int mineral)
    {
        if (m_characterInfo == null) return;
        m_characterInfo.mineral = mineral;
        EventManager.TriggerMineralChange(mineral);
    }



    public bool CheckEnoughMineral(long cost)
    {
        if (m_characterInfo == null) return false;
        return m_characterInfo.mineral >= cost;
    }

    public bool TryConsumeMineral(int amount)
    {
        if (m_characterInfo == null) return false;
        if (m_characterInfo.mineral < amount) return false;
        m_characterInfo.mineral -= amount;
        EventManager.TriggerMineralChange(m_characterInfo.mineral);
        return true;
    }

    public bool CheckEnoughTechPoint(long cost)
    {
        if (m_characterInfo == null) return false;
        return m_characterInfo.techPoint >= cost;
    }

    public bool CheckEnoughModulePoint(long cost)
    {
        if (m_characterInfo == null) return false;
        return m_characterInfo.modulePoint >= cost;
    }




    // 문자열 기반 완료 연구 ID 목록 세팅 (모듈 연구용, techLevel과 무관)
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