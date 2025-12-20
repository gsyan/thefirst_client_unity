//------------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpaceFleet : MonoBehaviour
{
    public FleetInfo m_fleetInfo;
    [SerializeField] public bool m_isEnemyFleet = false;
    public EFleetState m_fleetState = EFleetState.None;
    public EFormationType m_currentFormationType = EFormationType.LinearHorizontal;
    [SerializeField] public List<SpaceShip> m_ships = new List<SpaceShip>();
    [HideInInspector] public UIPanelFleet_TabUpgrade m_panelFleet_TabUpgrade;


    private void Start()
    {
        //UpdateShipFormation(m_currentFormationType, false);
        if (m_isEnemyFleet == false)
        {
            StartCoroutine(AutoRepair());
        }
    }

    public void InitializeSpaceFleet(FleetInfo fleetInfo, bool isEnemy = false, EFleetState fleetState = EFleetState.None)
    {
        m_fleetInfo = fleetInfo;
        m_isEnemyFleet = isEnemy;
        m_fleetState = fleetState;

        if (m_fleetInfo.ships != null && m_fleetInfo.ships.Length > 0)
        {
            for (int i = 0; i < m_fleetInfo.ships.Length; i++)
                CreateSpaceShipFromData(fleetInfo.ships[i]);

            if (string.IsNullOrEmpty(m_fleetInfo.formation) == false)
            {
                if (System.Enum.TryParse<EFormationType>(m_fleetInfo.formation, out var formationType))
                    UpdateShipFormation(formationType, false);
            }
        }


    }
    public void CreateSpaceShipFromData(ShipInfo shipInfo)
    {
        GameObject shipGo = new GameObject($"{shipInfo.shipName}");
        SpaceShip spaceShip = shipGo.AddComponent<SpaceShip>();
        spaceShip.InitializeSpaceShip(this, shipInfo);
        AddShip(spaceShip);
    }
    public void AddShip(SpaceShip ship)
    {
        if (ship == null) return;
        m_ships.Add(ship);
        ship.transform.SetParent(transform);
        ship.transform.localRotation = Quaternion.identity;
    }

    
    

    public void UpdateShipFormation(EFormationType formationType = EFormationType.LinearHorizontal, bool smooth = true)
    {
        m_currentFormationType = formationType;

        if (smooth)
        {
            List<SpaceShip> sortedShips = new List<SpaceShip>(m_ships);
            sortedShips.Sort((a, b) => a.m_shipInfo.positionIndex.CompareTo(b.m_shipInfo.positionIndex));

            for (int i = 0; i < sortedShips.Count; i++)
            {
                SpaceShip ship = sortedShips[i];
                if (ship == null) continue;
                float delay = i * 0.1f;
                StartCoroutine(DelayedFormationMove(ship, formationType, delay));
            }
        }
        else
        {
            foreach (SpaceShip ship in m_ships)
            {
                if (ship != null)
                    ship.transform.localPosition = ship.CalculateShipPosition(formationType);
            }
        }
    }

    private IEnumerator DelayedFormationMove(SpaceShip ship, EFormationType formationType, float delay)
    {
        if (delay > 0)
            yield return new WaitForSeconds(delay);
        ship.MoveToFormationPosition(formationType);
    }


    public void ChangeFormation(EFormationType newFormationType)
    {
        if (m_isEnemyFleet) return;

        var request = new ChangeFormationRequest
        {
            fleetId = m_fleetInfo.id,
            formationType = newFormationType.ToString()
        };

        NetworkManager.Instance.ChangeFormation(request, (response) =>
        {
            if (response.errorCode == 0 && response.data.success)
            {
                UpdateShipFormation(newFormationType);
                if (response.data.updatedFleetInfo != null)
                    DataManager.Instance.SetFleetData(response.data.updatedFleetInfo);
            }
        });
    }

    public void RemoveShip(SpaceShip ship)
    {
        if (ship == null) return;
        m_ships.Remove(ship);

        if (IsFleetAlive() == false)
        {
            if (m_isEnemyFleet == true)
                ObjectManager.Instance.RemoveEnemyFleet(this);
        }
    }

    public bool IsFleetAlive()
    {
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null && ship.IsAlive() == true)
                return true;
        }
        return false;
    }

    public SpaceShip GetRandomAliveShip()
    {
        List<SpaceShip> aliveShips = new List<SpaceShip>();
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null && ship.IsAlive() == true)
                aliveShips.Add(ship);
        }

        if (aliveShips.Count > 0)
        {
            int randomIndex = Random.Range(0, aliveShips.Count);
            return aliveShips[randomIndex];
        }

        return null;
    }

    public ModuleBody GetRandomAliveBodyPart()
    {
        SpaceShip aliveShip = GetRandomAliveShip();
        if (aliveShip == null) return null;

        List<ModuleBody> aliveBodies = new List<ModuleBody>();
        foreach (ModuleBody body in aliveShip.m_moduleBodyList)
        {
            if (body != null && body.m_health > 0)
                aliveBodies.Add(body);
        }

        if (aliveBodies.Count > 0)
        {
            int randomIndex = Random.Range(0, aliveBodies.Count);
            return aliveBodies[randomIndex];
        }

        return null;
    }

    public void RemoveDeadShips()
    {
        for (int i = m_ships.Count - 1; i >= 0; i--)
        {
            if (m_ships[i] == null || m_ships[i].IsAlive() == false)
            {
                if (m_ships[i] != null)
                {
                    Destroy(m_ships[i].gameObject);
                }
                m_ships.RemoveAt(i);
            }
        }
    }

    public void SetFleetState(EFleetState fleetState)
    {
        m_fleetState = fleetState;
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null && ship.IsAlive())
                ship.ApplyFleetStateToShip();
        }
    }

    public System.Collections.IEnumerator AutoRepair()
    {
        while (IsFleetAlive() == true)
        {
            yield return new WaitForSeconds(1.0f);

            SpaceShipStats fleetStats = GetTotalStats(true);
            float repairPerSecond = 10;//fleetStats.totalMaintenanceCapability;

            if (repairPerSecond <= 0) continue;

            float repairPerShip = repairPerSecond / m_ships.Count;

            foreach (SpaceShip ship in m_ships)
            {
                if (ship == null || ship.IsAlive() == false) continue;

                foreach (ModuleBody body in ship.m_moduleBodyList)
                {
                    if (body == null || body.m_health >= body.m_healthMax) continue;

                    float repairAmount = repairPerShip / ship.m_moduleBodyList.Count;
                    body.m_health = Mathf.Min(body.m_health + repairAmount, body.m_healthMax);
                }

                ship.RecalculateHealth();
            }
        }
    }

    virtual protected void OnFleetDestroyed()
    {
        StopAllCoroutines();

        if (m_isEnemyFleet == true)
        {
            ObjectManager.Instance.RemoveEnemyFleet(this);
        }

        // gameObject.SetActive(false);
    }

    public SpaceShipStats GetTotalStats(bool useCurrent = true)
    {
        SpaceShipStats totalStats = new SpaceShipStats();

        foreach (SpaceShip ship in m_ships)
        {
            if (ship == null) continue;

            SpaceShipStats shipStats = useCurrent ? ship.m_spaceShipStatsCur : ship.m_spaceShipStatsOrg;

            totalStats.totalHealth += shipStats.totalHealth;
            totalStats.totalMovementSpeed += shipStats.totalMovementSpeed;
            totalStats.totalRotationSpeed += shipStats.totalRotationSpeed;
            totalStats.totalCargoCapacity += shipStats.totalCargoCapacity;
            totalStats.totalAttackPower += shipStats.totalAttackPower;
            totalStats.totalWeapons += shipStats.totalWeapons;
            totalStats.totalEngines += shipStats.totalEngines;
        }

        return totalStats;
    }

    public int GetAverageShipLevel()
    {
        if (m_ships.Count == 0) return 0;

        int totalLevel = 0;
        foreach (SpaceShip ship in m_ships)
        {
            if (ship == null) continue;
            totalLevel += ship.GetAverageModuleLevel();
        }
        return totalLevel / m_ships.Count;
    }

    public void ClearAllSelectedModule()
    {
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null)
                ship.ClearSelectedModule();
        }
    }

    

}