using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization;

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
            EModuleType.beam => maxBeamDps,
            EModuleType.missile => maxMissileDps,
            EModuleType.hanger => maxHangerDps,
            _ => Mathf.Max(maxBeamDps, maxMissileDps, maxHangerDps)
        };
    }

    // 모듈 타입에 따른 최대 HP 반환 (합산 시 Body HP 기준)
    public readonly float GetMaxHp(EModuleType moduleType)
    {
        return moduleType switch
        {
            EModuleType.body => maxBodyHp,
            EModuleType.engine => maxEngineHp,
            EModuleType.beam => maxBeamHp,
            EModuleType.missile => maxMissileHp,
            EModuleType.hanger => maxHangerHp,
            _ => maxBodyHp
        };
    }

    // 모듈 타입에 따른 최대 Speed 반환 (Engine만 해당)
    public readonly float GetMaxSpeed(EModuleType moduleType)
    {
        return moduleType == EModuleType.engine ? maxEngineSpeed : 0f;
    }

    // 모듈 타입에 따른 최대 Cargo 반환 (Body만 해당)
    public readonly float GetMaxCargo(EModuleType moduleType)
    {
        return moduleType == EModuleType.body ? maxBodyCargo : 0f;
    }
}

public static class EnumExtensions
{
    public static string ToLocKey<T>(this T value) where T : Enum
        => value.ToString();
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

    // 렌더러들의 bounds 계산 (파티클/트레일 제외 옵션)
    public static Bounds CalculateRendererBounds(Transform target, bool excludeParticles = true, bool excludeTrails = true, bool excludeDisabled = true)
    {
        if (target == null)
            return new Bounds(Vector3.zero, Vector3.zero);

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        return CalculateRendererBoundsInternal(renderers, target.position, excludeParticles, excludeTrails, excludeDisabled);
    }

    // Renderer 배열로 bounds 계산
    public static Bounds CalculateRendererBounds(Renderer[] renderers, Vector3 fallbackCenter, bool excludeParticles = true, bool excludeTrails = true, bool excludeDisabled = true)
    {
        return CalculateRendererBoundsInternal(renderers, fallbackCenter, excludeParticles, excludeTrails, excludeDisabled);
    }

    private static Bounds CalculateRendererBoundsInternal(Renderer[] renderers, Vector3 fallbackCenter, bool excludeParticles, bool excludeTrails, bool excludeDisabled)
    {
        if (renderers == null || renderers.Length == 0)
            return new Bounds(fallbackCenter, Vector3.zero);

        Bounds bounds = new Bounds();
        bool initialized = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            // 비활성화 체크
            if (excludeDisabled && (!r.enabled || !r.gameObject.activeInHierarchy))
                continue;

            // 파티클 제외
            if (excludeParticles && r is ParticleSystemRenderer)
                continue;

            // 트레일 제외
            if (excludeTrails && r is TrailRenderer)
                continue;

            // bounds 유효성 검사 (NaN, Infinity 체크)
            Bounds b = r.bounds;
            if (float.IsNaN(b.center.x) || float.IsInfinity(b.size.x))
                continue;

            if (!initialized)
            {
                bounds = b;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(b);
            }
        }

        if (!initialized)
            return new Bounds(fallbackCenter, Vector3.zero);

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
        if (moduleInfo.moduleType == EModuleType.beam || moduleInfo.moduleType == EModuleType.missile)
        {
            stats.attack_power = moduleData.m_attackPower * moduleData.m_attackFireCount;// 공격력 × 발사 개수
            stats.health_power = moduleData.m_health;// 체력 수치
            stats.totalWeapons = 1;
        }
        else if (moduleInfo.moduleType == EModuleType.hanger)
        {
            stats.attack_power = moduleData.m_hangarCapability * moduleData.m_aircraftAttackPower;// 함재기 수용량 × 함재기 공격력
            stats.health_power = moduleData.m_health;// 체력 수치
            stats.totalWeapons = 1;
        }
        else if (moduleInfo.moduleType == EModuleType.engine)
        {
            stats.speed_power = moduleData.m_movementSpeed;
            stats.health_power = moduleData.m_health;
            stats.totalEngines = 1;
        }

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
            stats.health_power = bodyData.m_health;
            stats.cargo_capacity = bodyData.m_cargoCapacity;
            stats.repair_power = bodyData.m_repairPower;
        }

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
            stats.attack_power += shipStats.attack_power;
            stats.health_power += shipStats.health_power;
            stats.speed_power += shipStats.speed_power;
            stats.cargo_capacity += shipStats.cargo_capacity;
            stats.repair_power += shipStats.repair_power;
            stats.totalWeapons += shipStats.totalWeapons;
            stats.totalEngines += shipStats.totalEngines;
        }
        stats.speed_power = stats.speed_power / fleetInfo.ships.Count;

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
            stats.health_power += bodyStats.health_power;
            stats.cargo_capacity += bodyStats.cargo_capacity;
            stats.repair_power += bodyStats.repair_power;

            // Engine 모듈들 합산
            if (bodyInfo.engines != null)
            {
                foreach (ModuleInfo moduleInfo in bodyInfo.engines)
                {
                    CapabilityProfile moduleStats = GetModuleCapabilityProfile(moduleInfo);
                    stats.speed_power += moduleStats.speed_power;
                    stats.totalEngines += moduleStats.totalEngines;
                    stats.health_power += moduleStats.health_power;
                }
            }

            // Beam 모듈들 합산
            if (bodyInfo.beams != null)
            {
                foreach (ModuleInfo moduleInfo in bodyInfo.beams)
                {
                    CapabilityProfile moduleStats = GetModuleCapabilityProfile(moduleInfo);
                    stats.attack_power += moduleStats.attack_power;
                    stats.totalWeapons += moduleStats.totalWeapons;
                    stats.health_power += moduleStats.health_power;
                }
            }

            // Missile 모듈들 합산
            if (bodyInfo.missiles != null)
            {
                foreach (ModuleInfo moduleInfo in bodyInfo.missiles)
                {
                    CapabilityProfile moduleStats = GetModuleCapabilityProfile(moduleInfo);
                    stats.attack_power += moduleStats.attack_power;
                    stats.totalWeapons += moduleStats.totalWeapons;
                    stats.health_power += moduleStats.health_power;
                }
            }

            // Hanger 모듈들 합산
            if (bodyInfo.hangers != null)
            {
                foreach (ModuleInfo moduleInfo in bodyInfo.hangers)
                {
                    CapabilityProfile moduleStats = GetModuleCapabilityProfile(moduleInfo);
                    stats.attack_power += moduleStats.attack_power;
                    stats.totalWeapons += moduleStats.totalWeapons;
                    stats.health_power += moduleStats.health_power;
                }
            }
        }

        return stats;
    }

    
    #endregion Fleet Utility end -----------------------------------------------------------------------------------

    #region Module Type begin -----------------------------------------------------------------------------------
    public static EModuleType GetModuleTypeFromSubType(EModuleSubType subType)
    {
        if (subType == EModuleSubType.none) return EModuleType.none;        
        int typeValue = (int)subType / 1000;
        return (EModuleType)typeValue;
    }

    #endregion Module Type end -----------------------------------------------------------------------------------




    #region  begin -----------------------------------------------------------------------------------



    #endregion  end -----------------------------------------------------------------------------------

    #region UI begin -----------------------------------------------------------------------------------
    // 숫자를 K, M, B, T 단위로 포맷팅
    public static string FormatBigNumber(float value)
    {
        float absValue = Mathf.Abs(value);

        if (absValue >= 1_000_000_000_000f)
            return $"{value / 1_000_000_000_000f:0.#}T";
        if (absValue >= 1_000_000_000f)
            return $"{value / 1_000_000_000f:0.#}B";
        if (absValue >= 1_000_000f)
            return $"{value / 1_000_000f:0.#}M";
        if (absValue >= 1_000f)
            return $"{value / 1_000f:0.#}K";

        return $"{(int)value}";
    }
    public static string FormatBigNumber(long value)
    {
        return FormatBigNumber((float)value);
    }

    // label 에 localization
    public static void SetUILocText(TMP_Text textComp, string text)
    {
        // Label (Localized)
        var labelLocalize = textComp.GetComponent<LocalizeStringEvent>();
        if (labelLocalize != null)
        {
            const string TABLE = "UI";
            labelLocalize.StringReference = new LocalizedString(TABLE, text);
            labelLocalize.RefreshString();
        }
        else
        {
            // LocalizeStringEvent가 없으면 그냥 raw 텍스트로라도 표시
            textComp.text = text;
        }
    }

    #endregion UI end -----------------------------------------------------------------------------------
}