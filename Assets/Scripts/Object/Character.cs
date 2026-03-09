// 플레이어 캐릭터 상태 관리 - 모듈 연구 목록(int쌍), 문자열 연구 ID(tech_level_N 등) 포함
using System.Collections.Generic;
using UnityEngine;

public class Character
{
    public CharacterInfo m_characterInfo;
    public SpaceFleet m_ownedFleet;
    private List<int[]> m_researchedModules;       // [moduleType, moduleSubType] 쌍의 리스트
    private HashSet<string> m_completedResearchIds; // tech_level_N 등 문자열 기반 완료 연구

    public Character(CharacterInfo characterInfo)
    {
        m_characterInfo = characterInfo;
        m_researchedModules    = new List<int[]>();
        m_completedResearchIds = new HashSet<string>();
    }

    // "empty_" 로 시작하는 이름이면 로컬라이즈된 이름 + characterId로 반환 (예: "지휘관42")
    public static string GetDisplayName(string rawName, long characterId)
    {
        if (string.IsNullOrEmpty(rawName) || rawName.StartsWith("empty_"))
            return LocalizationManager.Instance.Get("char_default_name") + characterId;
        return rawName;
    }

    public string GetName()
    {
        return GetDisplayName(m_characterInfo?.characterName ?? "", m_characterInfo?.characterId ?? 0);
    }

    public long GetMineral()
    {
        return m_characterInfo?.mineral ?? 0;
    }

    public long GetMineralRare()
    {
        return m_characterInfo?.mineralRare ?? 0;
    }

    public long GetMineralExotic()
    {
        return m_characterInfo?.mineralExotic ?? 0;
    }

    public long GetMineralDark()
    {
        return m_characterInfo?.mineralDark ?? 0;
    }

    // 완료된 tech_level_N ID 중 최댓값을 기술레벨로 반환 (기본값 1)
    public int GetTechLevel()
    {
        int max = 1;
        foreach (string id in m_completedResearchIds)
        {
            if (id.StartsWith("tech_level_") && int.TryParse(id["tech_level_".Length..], out int lv))
                max = Mathf.Max(max, lv);
        }
        return max;
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

    public void UpdateMineral(long mineral)
    {
        if (m_characterInfo == null) return;
        m_characterInfo.mineral = mineral;
        EventManager.TriggerMineralChange(mineral);
    }

    public void UpdateMineralRare(long mineralRare)
    {
        if (m_characterInfo == null) return;
        m_characterInfo.mineralRare = mineralRare;
        EventManager.TriggerMineralRareChange(mineralRare);
    }

    public void UpdateMineralExotic(long mineralExotic)
    {
        if (m_characterInfo == null) return;
        m_characterInfo.mineralExotic = mineralExotic;
        EventManager.TriggerMineralExoticChange(mineralExotic);
    }

    public void UpdateMineralDark(long mineralDark)
    {
        if (m_characterInfo == null) return;
        m_characterInfo.mineralDark = mineralDark;
        EventManager.TriggerMineralDarkChange(mineralDark);
    }

    public void UpdateAllMinerals(CostRemainInfo costRemainInfo)
    {
        if (m_characterInfo == null || costRemainInfo == null) return;

        UpdateMineral(costRemainInfo.remainMineral);
        UpdateMineralRare(costRemainInfo.remainMineralRare);
        UpdateMineralExotic(costRemainInfo.remainMineralExotic);
        UpdateMineralDark(costRemainInfo.remainMineralDark);
    }

    public bool CheckEnoughCostStruct(CostStruct cost)
    {
        if (cost == null) return true;
        if (GetTechLevel() < cost.techLevel) return false;
        if (m_characterInfo.mineral < cost.mineral) return false;
        if (m_characterInfo.mineralRare < cost.mineralRare) return false;
        if (m_characterInfo.mineralExotic < cost.mineralExotic) return false;
        if (m_characterInfo.mineralDark < cost.mineralDark) return false;
        return true;
    }




    public void SetOwnedFleet(SpaceFleet fleet)
    {
        m_ownedFleet = fleet;
    }

    public SpaceFleet GetOwnedFleet()
    {
        return m_ownedFleet;
    }

    public bool HasFleet()
    {
        return m_ownedFleet != null;
    }

    public SpaceShip GetRandomAliveShip()
    {
        if (m_ownedFleet == null) return null;
        return m_ownedFleet.GetRandomAliveShip();
    }

    public bool IsFleetAlive()
    {
        if (m_ownedFleet == null) return false;
        return m_ownedFleet.IsFleetAlive();
    }

    // 개발된 모듈 목록 설정
    public void SetResearchedModules(int[][] researchedModules)
    {
        if (researchedModules == null)
        {
            m_researchedModules.Clear();
            return;
        }

        m_researchedModules = new List<int[]>(researchedModules);
    }

    // 특정 모듈이 개발되었는지 확인
    public bool IsModuleResearched(EModuleType moduleType, EModuleSubType moduleSubType)
    {
        if (m_researchedModules == null) return false;

        foreach (var pair in m_researchedModules)
        {
            if (pair[0] == (int)moduleType && pair[1] == (int)moduleSubType)
                return true;
        }
        return false;
    }

    // 모듈 개발 추가
    public void AddResearchedModule(EModuleType moduleType, EModuleSubType moduleSubType)
    {
        if (m_researchedModules == null)
            m_researchedModules = new List<int[]>();

        if (!IsModuleResearched(moduleType, moduleSubType))
        {
            m_researchedModules.Add(new int[] { (int)moduleType, (int)moduleSubType });
        }
    }

    // 개발된 모듈 목록 업데이트
    public void UpdateResearchedModules(int[][] researchedModules)
    {
        if (researchedModules == null) return;
        SetResearchedModules(researchedModules);
    }

    // 문자열 기반 완료 연구 ID 목록 세팅
    public void SetCompletedResearchIds(string[] ids)
    {
        m_completedResearchIds.Clear();
        if (ids == null) return;
        foreach (string id in ids)
            m_completedResearchIds.Add(id);
        EventManager.TriggerTechLevelChange(GetTechLevel());
    }

    // 연구 완료 후 단건 추가
    public void AddCompletedResearchId(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        m_completedResearchIds.Add(id);
        EventManager.TriggerTechLevelChange(GetTechLevel());
    }

    public bool IsResearchCompleted(string researchId)
    {
        return m_completedResearchIds.Contains(researchId);
    }

}