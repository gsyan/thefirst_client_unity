//------------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class Character
{
    public CharacterInfo m_characterInfo;
    public SpaceFleet m_ownedFleet;
    private List<int[]> m_researchedModules;  // [moduleType, moduleSubType] 쌍의 리스트

    public Character(CharacterInfo characterInfo)
    {
        m_characterInfo = characterInfo;
        m_researchedModules = new List<int[]>();
    }

    public string GetName()
    {
        return m_characterInfo?.characterName ?? "";
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

    public int GetTechLevel()
    {
        return m_characterInfo?.techLevel ?? 1;
    }

    public CharacterInfo GetInfo()
    {
        return m_characterInfo;
    }

    public void UpdateCharacterInfo(CharacterInfo characterInfo)
    {
        m_characterInfo = characterInfo;
        // EventManager.TriggerTechLevelChange(m_characterInfo.techLevel);
        // EventManager.TriggerMineralChange(m_characterInfo.mineral);
        // EventManager.TriggerMineralRareChange(m_characterInfo.mineralRare);
        // EventManager.TriggerMineralExoticChange(m_characterInfo.mineralExotic);
        // EventManager.TriggerMineralDarkChange(m_characterInfo.mineralDark);
    }

    public void UpdateTechLevel(int techLevel)
    {
        if (m_characterInfo == null) return;
        m_characterInfo.techLevel = techLevel;
        EventManager.TriggerTechLevelChange(techLevel);
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
        if (m_characterInfo.techLevel < cost.techLevel) return false;
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

}