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

            UpdateShipFormation(m_fleetInfo.formation, false);
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

    // shipId로 함선 찾기
    public SpaceShip FindShip(long shipId)
    {
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null && ship.m_shipInfo.id == shipId)
                return ship;
        }
        return null;
    }

    // shipId, bodyIndex, moduleType, slotIndex로 특정 모듈 찾기
    public ModuleBase FindModule(long shipId, int bodyIndex, EModuleType moduleType, int slotIndex)
    {
        SpaceShip ship = FindShip(shipId);
        if (ship == null) return null;

        return ship.FindModule(bodyIndex, moduleType, slotIndex);
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
            formationType = newFormationType
        };

        NetworkManager.Instance.ChangeFormation(request, (response) =>
        {
            if (response.errorCode == 0)
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
        foreach (ModuleBody body in aliveShip.m_moduleBodys)
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

            CapabilityProfile fleetStats = GetTotalStats(true);
            float repairPerSecond = 10;//fleetStats.totalMaintenanceCapability;

            if (repairPerSecond <= 0) continue;

            float repairPerShip = repairPerSecond / m_ships.Count;

            foreach (SpaceShip ship in m_ships)
            {
                if (ship == null || ship.IsAlive() == false) continue;

                foreach (ModuleBody body in ship.m_moduleBodys)
                {
                    if (body == null || body.m_health >= body.m_healthMax) continue;

                    float repairAmount = repairPerShip / ship.m_moduleBodys.Count;
                    body.m_health = Mathf.Min(body.m_health + repairAmount, body.m_healthMax);
                }

                ship.UpdateShipStatCur();
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

    // GetTotalStats는 하위 호환성을 위해 GetCapabilityProfile을 호출
    public CapabilityProfile GetTotalStats(bool useCurrent = true)
    {
        return GetCapabilityProfile(useCurrent);
    }

    // 함대의 능력치 프로파일 계산
    public CapabilityProfile GetCapabilityProfile(bool useCurrent = true)
    {
        CapabilityProfile totalStats = new CapabilityProfile();

        foreach (SpaceShip ship in m_ships)
        {
            if (ship == null) continue;

            CapabilityProfile shipStats = useCurrent ? ship.m_spaceShipStatsCur : ship.m_spaceShipStatsOrg;

            totalStats.hp += shipStats.hp;
            totalStats.engineSpeed += shipStats.engineSpeed;
            totalStats.cargoCapacity += shipStats.cargoCapacity;
            totalStats.attackDps += shipStats.attackDps;
            totalStats.totalWeapons += shipStats.totalWeapons;
            totalStats.totalEngines += shipStats.totalEngines;
        }

        // 육각형 능력치 자동 계산
        totalStats.firepower = totalStats.attackDps;
        totalStats.survivability = totalStats.hp;
        totalStats.mobility = totalStats.engineSpeed;
        totalStats.logistics = totalStats.cargoCapacity;
        totalStats.sustainment = 0; // 향후 확장
        totalStats.detection = 0;   // 향후 확장

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