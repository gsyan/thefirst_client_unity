using System.Reflection;
using UnityEngine;

// 모듈 최대 능력치 (육각형 차트 백분율 계산용)
[System.Serializable]
public struct ModuleMaxStats
{
    // Body
    public float maxBodyHp;
    public float maxBodyCargo;

    // Engine
    public float maxEngineHp;
    public float maxEngineSpeed;

    // Beam
    public float maxBeamHp;
    public float maxBeamDps;

    // Missile
    public float maxMissileHp;
    public float maxMissileDps;

    // Hanger
    public float maxHangerHp;
    public float maxHangerDps;

    // 모듈 타입에 따른 최대 DPS 반환 (합산 시 무기 중 최대값)
    public readonly float GetMaxDps(EModuleType moduleType)
    {
        return moduleType switch
        {
            EModuleType.Beam => maxBeamDps,
            EModuleType.Missile => maxMissileDps,
            EModuleType.Hanger => maxHangerDps,
            _ => Mathf.Max(maxBeamDps, maxMissileDps, maxHangerDps)
        };
    }

    // 모듈 타입에 따른 최대 HP 반환 (합산 시 Body HP 기준)
    public readonly float GetMaxHp(EModuleType moduleType)
    {
        return moduleType switch
        {
            EModuleType.Body => maxBodyHp,
            EModuleType.Engine => maxEngineHp,
            EModuleType.Beam => maxBeamHp,
            EModuleType.Missile => maxMissileHp,
            EModuleType.Hanger => maxHangerHp,
            _ => maxBodyHp
        };
    }

    // 모듈 타입에 따른 최대 Speed 반환 (Engine만 해당)
    public readonly float GetMaxSpeed(EModuleType moduleType)
    {
        return moduleType == EModuleType.Engine ? maxEngineSpeed : 0f;
    }

    // 모듈 타입에 따른 최대 Cargo 반환 (Body만 해당)
    public readonly float GetMaxCargo(EModuleType moduleType)
    {
        return moduleType == EModuleType.Body ? maxBodyCargo : 0f;
    }
}

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
    public static CapabilityProfile GetModuleCapabilityProfile(ModuleInfo moduleInfo)
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
            // 체력 수치
            stats.hp = moduleData.m_health;
            stats.totalWeapons = 1;
        }
        else if (moduleInfo.moduleType == EModuleType.Hanger)
        {
            // DPS 계산: 함재기 수용량 × 함재기 공격력 / 함재기 공격 쿨다운
            if (moduleData.m_aircraftAttackCooldown > 0)
                stats.attackDps = moduleData.m_hangarCapability * moduleData.m_aircraftAttackPower / moduleData.m_aircraftAttackCooldown;
            // 체력 수치
            stats.hp = moduleData.m_health;
            stats.totalWeapons = 1;
        }
        else if (moduleInfo.moduleType == EModuleType.Engine)
        {
            stats.engineSpeed = moduleData.m_movementSpeed;
            stats.hp = moduleData.m_health;
            stats.totalEngines = 1;
        }



        // 육각형 능력치 자동 계산 (최대값 대비 백분율) - 모듈 타입별 max 사용
        CalculatePersentStats(ref stats, moduleInfo.moduleType);

        return stats;
    }

    // ModuleBodyInfo로부터 Body 고유의 능력치만 계산
    public static CapabilityProfile GetBodyCapabilityProfile(ModuleBodyInfo bodyInfo)
    {
        CapabilityProfile stats = new CapabilityProfile();
        if (bodyInfo == null) return stats;

        ModuleData bodyData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(bodyInfo.moduleSubType, bodyInfo.moduleLevel);
        if (bodyData != null)
        {
            stats.hp = bodyData.m_health;
            stats.cargoCapacity = bodyData.m_cargoCapacity;
        }

        CalculatePersentStats(ref stats, EModuleType.Body);
        return stats;
    }

    // ModuleBodyInfo로부터 능력치 계산 (구버전 - 모든 모듈 합산)
    public static CapabilityProfile GetBodyCapabilityProfile_old(ModuleBodyInfo bodyInfo)
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
                CapabilityProfile engineStats = GetModuleCapabilityProfile(engineInfo);
                stats.engineSpeed += engineStats.engineSpeed;
                stats.totalEngines += engineStats.totalEngines;
            }
        }

        // Beam 모듈들 합산
        if (bodyInfo.beams != null)
        {
            foreach (ModuleInfo moduleInfo in bodyInfo.beams)
            {
                CapabilityProfile moduleStats = GetModuleCapabilityProfile(moduleInfo);
                stats.attackDps += moduleStats.attackDps;
                stats.totalWeapons += moduleStats.totalWeapons;
            }
        }

        // Missile 모듈들 합산
        if (bodyInfo.missiles != null)
        {
            foreach (ModuleInfo moduleInfo in bodyInfo.missiles)
            {
                CapabilityProfile moduleStats = GetModuleCapabilityProfile(moduleInfo);
                stats.attackDps += moduleStats.attackDps;
                stats.totalWeapons += moduleStats.totalWeapons;
            }
        }

        // 육각형 능력치 자동 계산 (최대값 대비 백분율)
        CalculatePersentStats(ref stats);

        return stats;
    }

    // FleetInfo로부터 능력치 계산
    public static CapabilityProfile GetFleetCapabilityProfile(FleetInfo fleetInfo)
    {
        CapabilityProfile stats = new CapabilityProfile();

        if (fleetInfo == null || fleetInfo.ships == null) return stats;

        // 모든 함선의 능력치 합산
        foreach (ShipInfo shipInfo in fleetInfo.ships)
        {
            CapabilityProfile shipStats = GetShipCapabilityProfile(shipInfo);
            stats.attackDps += shipStats.attackDps;
            stats.hp += shipStats.hp;
            stats.engineSpeed += shipStats.engineSpeed;
            stats.cargoCapacity += shipStats.cargoCapacity;
            stats.totalWeapons += shipStats.totalWeapons;
            stats.totalEngines += shipStats.totalEngines;
        }
        stats.engineSpeed = stats.engineSpeed / fleetInfo.ships.Count;

        // 육각형 능력치 자동 계산 (최대값 대비 백분율)
        CalculatePersentStats(ref stats);

        return stats;
    }

    // ShipInfo로부터 능력치 계산 (모든 바디 + 모든 모듈 합산)
    public static CapabilityProfile GetShipCapabilityProfile(ShipInfo shipInfo)
    {
        CapabilityProfile stats = new CapabilityProfile();

        if (shipInfo == null || shipInfo.bodies == null) return stats;

        foreach (ModuleBodyInfo bodyInfo in shipInfo.bodies)
        {
            // Body 고유 능력치
            CapabilityProfile bodyStats = GetBodyCapabilityProfile(bodyInfo);
            stats.hp += bodyStats.hp;
            stats.cargoCapacity += bodyStats.cargoCapacity;

            // Engine 모듈들 합산
            if (bodyInfo.engines != null)
            {
                foreach (ModuleInfo moduleInfo in bodyInfo.engines)
                {
                    CapabilityProfile moduleStats = GetModuleCapabilityProfile(moduleInfo);
                    stats.engineSpeed += moduleStats.engineSpeed;
                    stats.totalEngines += moduleStats.totalEngines;
                    stats.hp += moduleStats.hp;
                }
            }

            // Beam 모듈들 합산
            if (bodyInfo.beams != null)
            {
                foreach (ModuleInfo moduleInfo in bodyInfo.beams)
                {
                    CapabilityProfile moduleStats = GetModuleCapabilityProfile(moduleInfo);
                    stats.attackDps += moduleStats.attackDps;
                    stats.totalWeapons += moduleStats.totalWeapons;
                    stats.hp += moduleStats.hp;
                }
            }

            // Missile 모듈들 합산
            if (bodyInfo.missiles != null)
            {
                foreach (ModuleInfo moduleInfo in bodyInfo.missiles)
                {
                    CapabilityProfile moduleStats = GetModuleCapabilityProfile(moduleInfo);
                    stats.attackDps += moduleStats.attackDps;
                    stats.totalWeapons += moduleStats.totalWeapons;
                    stats.hp += moduleStats.hp;
                }
            }

            // Hanger 모듈들 합산
            if (bodyInfo.hangers != null)
            {
                foreach (ModuleInfo moduleInfo in bodyInfo.hangers)
                {
                    CapabilityProfile moduleStats = GetModuleCapabilityProfile(moduleInfo);
                    stats.attackDps += moduleStats.attackDps;
                    stats.totalWeapons += moduleStats.totalWeapons;
                    stats.hp += moduleStats.hp;
                }
            }
        }

        // 육각형 능력치 자동 계산 (최대값 대비 백분율)
        CalculatePersentStats(ref stats);

        return stats;
    }

    // 육각형 능력치 계산 (장착 모듈 수 × 최대값 대비 백분율, 0~100)
    // moduleType: 개별 모듈일 경우 해당 타입, 합산일 경우 None
    private static void CalculatePersentStats(ref CapabilityProfile stats, EModuleType moduleType = EModuleType.None)
    {
        ModuleMaxStats maxStats = DataManager.Instance.m_dataTableModule.MaxStats;

        // firepower: 장착된 무기 수 × 타입별 최대 DPS 대비
        float maxDps = maxStats.GetMaxDps(moduleType);
        float maxTotalDps = maxDps * stats.totalWeapons;
        stats.firepower = maxTotalDps > 0 ? stats.attackDps / maxTotalDps * 100f : 0f;

        // survivability: 타입별 최대 HP 대비
        float maxHp = maxStats.GetMaxHp(moduleType);
        stats.survivability = maxHp > 0 ? stats.hp / maxHp * 100f : 0f;

        // mobility: 장착된 엔진 수 × 최대 Speed 대비
        float maxSpeed = maxStats.GetMaxSpeed(EModuleType.Engine);
        float maxTotalSpeed = maxSpeed * stats.totalEngines;
        stats.mobility = maxTotalSpeed > 0 ? stats.engineSpeed / maxTotalSpeed * 100f : 0f;

        // logistics: Body 최대 Cargo 대비
        float maxCargo = maxStats.GetMaxCargo(EModuleType.Body);
        stats.logistics = maxCargo > 0 ? stats.cargoCapacity / maxCargo * 100f : 0f;

        stats.sustainment = 0f; // 향후 확장
        stats.detection = 0f;   // 향후 확장
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