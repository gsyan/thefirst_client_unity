using UnityEngine;

public static class CommonUtility
{
    #region Fleet Utility begin -----------------------------------------------------------------------------------
    public static Vector3 CalculateFleetCenter(Vector3[] shipPositions)
    {
        if (shipPositions == null || shipPositions.Length == 0)
            return Vector3.zero;
            
        Vector3 center = Vector3.zero;
        foreach (var position in shipPositions)
        {
            center += position;
        }
        
        return center / shipPositions.Length;
    }
    
    // Calculate fleet bounds
    public static Bounds CalculateFleetBounds(Vector3[] shipPositions, float shipSize = 2f)
    {
        if (shipPositions == null || shipPositions.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one);

        Bounds bounds = new Bounds(shipPositions[0], Vector3.one * shipSize);

        foreach (var position in shipPositions)
        {
            bounds.Encapsulate(new Bounds(position, Vector3.one * shipSize));
        }

        return bounds;
    }

    // ModuleInfo로부터 능력치 계산
    public static CapabilityProfile GetCapabilityProfile(ModuleInfo moduleInfo)
    {
        CapabilityProfile stats = new CapabilityProfile();
        if (moduleInfo == null) return stats;
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(moduleInfo.moduleSubType, moduleInfo.moduleLevel);
        if (moduleData == null) return stats;

        // 모듈 타입에 따라 능력치 설정
        if (moduleInfo.moduleType == EModuleType.Beam || moduleInfo.moduleType == EModuleType.Missile)
        {
            // DPS 계산: 공격력 × 발사 개수 / 쿨타임
            if (moduleData.m_attackCoolTime > 0)
                stats.attackDps = moduleData.m_attackPower * moduleData.m_attackFireCount / moduleData.m_attackCoolTime;
            stats.totalWeapons = 1;
        }
        else if (moduleInfo.moduleType == EModuleType.Engine)
        {
            stats.engineSpeed = moduleData.m_movementSpeed;
            stats.totalEngines = 1;
        }

        return stats;
    }

    // ModuleBodyInfo로부터 능력치 계산
    public static CapabilityProfile GetCapabilityProfile(ModuleBodyInfo bodyInfo)
    {
        CapabilityProfile stats = new CapabilityProfile();
        if (bodyInfo == null) return stats;
        // Body 자체의 데이터
        ModuleData bodyData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(bodyInfo.moduleSubType, bodyInfo.moduleLevel);
        if (bodyData != null)
        {
            stats.hp = bodyData.m_health;
            stats.cargoCapacity = bodyData.m_cargoCapacity;
        }

        // Engine 모듈들 합산
        if (bodyInfo.engines != null)
        {
            foreach (ModuleInfo engineInfo in bodyInfo.engines)
            {
                CapabilityProfile engineStats = GetCapabilityProfile(engineInfo);
                stats.engineSpeed += engineStats.engineSpeed;
                stats.totalEngines += engineStats.totalEngines;
            }
        }

        // Beam 모듈들 합산
        if (bodyInfo.beams != null)
        {
            foreach (ModuleInfo moduleInfo in bodyInfo.beams)
            {
                CapabilityProfile moduleStats = GetCapabilityProfile(moduleInfo);
                stats.attackDps += moduleStats.attackDps;
                stats.totalWeapons += moduleStats.totalWeapons;
            }
        }

        // Missile 모듈들 합산
        if (bodyInfo.missiles != null)
        {
            foreach (ModuleInfo moduleInfo in bodyInfo.missiles)
            {
                CapabilityProfile moduleStats = GetCapabilityProfile(moduleInfo);
                stats.attackDps += moduleStats.attackDps;
                stats.totalWeapons += moduleStats.totalWeapons;
            }
        }

        return stats;
    }

    // ShipInfo로부터 능력치 계산
    public static CapabilityProfile GetCapabilityProfile(ShipInfo shipInfo)
    {
        CapabilityProfile stats = new CapabilityProfile();

        if (shipInfo == null || shipInfo.bodies == null) return stats;

        // 모든 바디의 능력치 합산
        foreach (ModuleBodyInfo bodyInfo in shipInfo.bodies)
        {
            CapabilityProfile bodyStats = GetCapabilityProfile(bodyInfo);
            stats.attackDps += bodyStats.attackDps;
            stats.hp += bodyStats.hp;
            stats.engineSpeed += bodyStats.engineSpeed;
            stats.cargoCapacity += bodyStats.cargoCapacity;            
            stats.totalWeapons += bodyStats.totalWeapons;
            stats.totalEngines += bodyStats.totalEngines;
        }

        // 육각형 능력치 자동 계산
        stats.firepower = stats.attackDps;
        stats.survivability = stats.hp;
        stats.mobility = stats.engineSpeed;
        stats.logistics = stats.cargoCapacity;
        stats.sustainment = 0; // 향후 확장
        stats.detection = 0;   // 향후 확장

        return stats;
    }

    // FleetInfo로부터 능력치 계산
    public static CapabilityProfile GetCapabilityProfile(FleetInfo fleetInfo)
    {
        CapabilityProfile stats = new CapabilityProfile();

        if (fleetInfo == null || fleetInfo.ships == null) return stats;

        // 모든 함선의 능력치 합산
        foreach (ShipInfo shipInfo in fleetInfo.ships)
        {
            CapabilityProfile shipStats = GetCapabilityProfile(shipInfo);
            stats.attackDps += shipStats.attackDps;
            stats.hp += shipStats.hp;
            stats.engineSpeed += shipStats.engineSpeed;
            stats.cargoCapacity += shipStats.cargoCapacity;
            stats.totalWeapons += shipStats.totalWeapons;
            stats.totalEngines += shipStats.totalEngines;
        }
        stats.engineSpeed = stats.engineSpeed / fleetInfo.ships.Count;

        // 육각형 능력치 자동 계산
        stats.firepower = stats.attackDps;
        stats.survivability = stats.hp;
        stats.mobility = stats.engineSpeed;
        stats.logistics = stats.cargoCapacity;
        stats.sustainment = 0; // 향후 확장
        stats.detection = 0;   // 향후 확장

        return stats;
    }
    #endregion Fleet Utility end -----------------------------------------------------------------------------------

    #region Module Type begin -----------------------------------------------------------------------------------
    public static EModuleType GetModuleTypeFromSubType(EModuleSubType subType)
    {
        if (subType == EModuleSubType.None) return EModuleType.None;        
        int typeValue = (int)subType / 1000;
        return (EModuleType)typeValue;
    }

    #endregion Module Type end -----------------------------------------------------------------------------------




    #region  begin -----------------------------------------------------------------------------------
    
    

    #endregion  end -----------------------------------------------------------------------------------
}