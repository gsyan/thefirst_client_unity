//------------------------------------------------------------------------------
using UnityEngine;

public class Character
{
    public CharacterInfo m_characterInfo;
    public SpaceFleet m_ownedFleet;

    public Character(CharacterInfo characterInfo)
    {
        m_characterInfo = characterInfo;
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
        EventManager.TriggerTechLevelChange(m_characterInfo.techLevel);
        EventManager.TriggerMineralChange(m_characterInfo.mineral);
        EventManager.TriggerMineralRareChange(m_characterInfo.mineralRare);
        EventManager.TriggerMineralExoticChange(m_characterInfo.mineralExotic);
        EventManager.TriggerMineralDarkChange(m_characterInfo.mineralDark);
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

}