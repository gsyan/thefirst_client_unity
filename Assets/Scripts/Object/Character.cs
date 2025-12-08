//------------------------------------------------------------------------------
using UnityEngine;

public class Character
{
    private CharacterInfo m_characterInfo;
    private SpaceFleet m_ownedFleet;

    public Character(CharacterInfo characterInfo)
    {
        m_characterInfo = characterInfo;
    }

    public string GetName()
    {
        return m_characterInfo?.characterName ?? "";
    }

    public long GetMoney()
    {
        return m_characterInfo?.money ?? 0;
    }

    public long GetMineral()
    {
        return m_characterInfo?.mineral ?? 0;
    }

    public int GetTechnologyLevel()
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
        EventManager.TriggerMoneyChange(m_characterInfo.money);
        EventManager.TriggerMineralChange(m_characterInfo.mineral);
        EventManager.TriggerTechLevelChange(m_characterInfo.techLevel);
    }

    public void UpdateMoney(long money)
    {
        if (m_characterInfo == null) return;
        m_characterInfo.money = money;
        EventManager.TriggerMoneyChange(money);
    }

    public void UpdateMineral(long mineral)
    {
        if (m_characterInfo == null) return;
        m_characterInfo.mineral = mineral;
        EventManager.TriggerMineralChange(mineral);
    }

    public void UpdateTechLevel(int techLevel)
    {
        if (m_characterInfo == null) return;
        m_characterInfo.techLevel = techLevel;
        EventManager.TriggerTechLevelChange(techLevel);
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

    public bool CanAddShip()
    {
        var gameSettings = DataManager.Instance.m_dataTableConfig.gameSettings;

        if (m_ownedFleet == null) return false;
        if (m_ownedFleet.m_ships.Count >= gameSettings.maxShipsPerFleet) return false;
        if (GetMoney() < gameSettings.shipAddMoneyCost) return false;
        if (GetMineral() < gameSettings.shipAddMineralCost) return false;

        return true;
    }

    public void AddNewShip(System.Action<bool> onComplete)
    {
        if (!CanAddShip())
        {
            onComplete?.Invoke(false);
            return;
        }

        // Request ship addition to server
        var request = new AddShipRequest
        {
            fleetId = null // Add to current active fleet
        };

        NetworkManager.Instance.AddShip(request, (response) =>
        {
            if (response.errorCode == 0 && response.data.success)
            {
                UpdateMoney(response.data.remainMoney);
                UpdateMineral(response.data.remainMineral);

                if (response.data.updatedFleetInfo != null)
                    DataManager.Instance.SetFleetData(response.data.updatedFleetInfo);

                if (response.data.newShipInfo != null && m_ownedFleet != null)
                {
                    ObjectManager.Instance.m_myFleet.CreateSpaceShipFromData(response.data.newShipInfo);
                    ObjectManager.Instance.m_myFleet.UpdateShipFormation(ObjectManager.Instance.m_myFleet.m_currentFormationType, false);
                }

                EventManager.TriggerFleetChange();

                onComplete?.Invoke(true);
            }
            else
            {
                onComplete?.Invoke(false);
            }
        });
    }

}