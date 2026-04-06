using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization;

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
            stats.attack = moduleData.attack * moduleData.attackFireCount;// 공격력 × 발사 개수
            stats.totalWeapons = 1;
        }
        else if (moduleInfo.moduleType == EModuleType.hanger)
        {
            stats.airAttack = (int)moduleData.airAttack;       // 함재기 공격력
            stats.airCount = moduleData.airCount;              // 함재기 수
            stats.airLaunchCount = moduleData.attackFireCount; // 함재기 발진 수
            stats.totalWeapons = 1;
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
            stats.health = bodyData.health;
            stats.repair = bodyData.repair;
            stats.speed  = bodyData.speed;
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
            stats.totalWeapons += shipStats.totalWeapons;
            stats.attack += shipStats.attack;
            stats.health += shipStats.health;
            stats.speed += shipStats.speed;
            stats.repair += shipStats.repair;
            stats.airAttack += shipStats.airAttack;
            stats.airCount += shipStats.airCount;
            stats.airLaunchCount+= shipStats.airLaunchCount;
        }
        stats.speed = stats.speed / fleetInfo.ships.Count;

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
            stats.health += bodyStats.health;
            stats.repair += bodyStats.repair;
            stats.speed  += bodyStats.speed;

            // Beam 모듈들 합산
            if (bodyInfo.beams != null)
            {
                foreach (ModuleInfo moduleInfo in bodyInfo.beams)
                {
                    CapabilityProfile moduleStats = GetModuleCapabilityProfile(moduleInfo);
                    stats.attack += moduleStats.attack;
                    
                    stats.totalWeapons += moduleStats.totalWeapons;
                }
            }

            // Missile 모듈들 합산
            if (bodyInfo.missiles != null)
            {
                foreach (ModuleInfo moduleInfo in bodyInfo.missiles)
                {
                    CapabilityProfile moduleStats = GetModuleCapabilityProfile(moduleInfo);
                    stats.attack += moduleStats.attack;

                    stats.totalWeapons += moduleStats.totalWeapons;
                }
            }

            // Hanger 모듈들 합산
            if (bodyInfo.hangers != null)
            {
                foreach (ModuleInfo moduleInfo in bodyInfo.hangers)
                {
                    CapabilityProfile moduleStats = GetModuleCapabilityProfile(moduleInfo);
                    stats.airAttack += moduleStats.airAttack;
                    stats.airCount += moduleStats.airCount;
                    stats.airLaunchCount+= moduleStats.airLaunchCount;
                    stats.totalWeapons += moduleStats.totalWeapons;
                }
            }
        }

        return stats;
    }

    
    #endregion Fleet Utility end -----------------------------------------------------------------------------------

    #region Module Type begin -----------------------------------------------------------------------------------
    // 기본 SubType: t1(01), m1(01)
    public static EModuleSubType GetDefaultSubType(EModuleType moduleType)
    {
        EModuleSubType defaultSubType = (EModuleSubType)((int)moduleType * 10000 + 101);
        return defaultSubType;
    }

    // 모듈 스탯을 아이콘+수치 문자열로 반환 (팝업 detailText용)
    public static string GetModuleDetailText(EModuleType moduleType, EModuleSubType subType, int fromLevel, int toLevel)
    {
        bool showRange = fromLevel != toLevel;
        ModuleData cur = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, fromLevel);
        ModuleData nxt = showRange ? DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(subType, toLevel) : null;
        if (cur == null) return string.Empty;
        if (showRange && nxt == null) return string.Empty;

        string V(float c, float n) => showRange ? $"{c:F0} → {n:F0}" : $"{c:F0}";
        string levelStr = showRange ? $"Lv. {fromLevel} → {toLevel}" : $"Lv. {fromLevel}";

        var lines = new List<string> { levelStr };

        if (moduleType == EModuleType.body)
        {
            lines.Add($"<sprite name=\"IconHp\"> {V(cur.health, nxt?.health ?? 0f)}");
            lines.Add($"<sprite name=\"IconRepair\"> {V(cur.repair, nxt?.repair ?? 0f)}");
            lines.Add($"<sprite name=\"IconSpeed\"> {V(cur.speed, nxt?.speed ?? 0f)}");
        }
        else if (moduleType == EModuleType.beam || moduleType == EModuleType.missile)
        {
            lines.Add($"<sprite name=\"IconAttack\"> {V(cur.attack, nxt?.attack ?? 0f)}");
        }
        else if (moduleType == EModuleType.hanger)
        {
            lines.Add($"<sprite name=\"IconAircraftAttack\"> {V(cur.airAttack, nxt?.airAttack ?? 0f)}");
            lines.Add($"<sprite name=\"IconAircraftHp\"> {V(cur.airHealth, nxt?.airHealth ?? 0f)}");
            lines.Add($"<sprite name=\"IconAircraftSpeed\"> {V(cur.airSpeed, nxt?.airSpeed ?? 0f)}");
            lines.Add($"<sprite name=\"IconAircraft\"> {V(cur.airCount, nxt?.airCount ?? 0f)}");
            lines.Add($"<sprite name=\"IconLaunch\"> {V(cur.attackFireCount, nxt?.attackFireCount ?? 0f)}");
        }

        return string.Join("\n\n", lines);
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