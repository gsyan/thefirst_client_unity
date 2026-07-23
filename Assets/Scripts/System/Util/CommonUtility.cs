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
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void DebugLog(string message) => Debug.Log($"[DEV] {message}");


    #region Exploration Grid World Mapping begin -------------------------------------------------------------------
    // 탐사 그리드 시드 — 존/커맨더별 고정 시드가 같으면 항상 같은 결과(그리드 UI를 실제로 연 적이 없어도 계산 가능)
    // ObjectManager(로그인 시 초기 함대 배치)와 UITabExplorationGrid(그리드 UI) 양쪽에서 동일하게 사용
    public static int ComputeExplorationZoneSeed(int zoneNumber, int explorationSeedBase)
    {
        return explorationSeedBase ^ (zoneNumber * 486187739);
    }

    // 화면 좌표(Screen Space Overlay 캔버스 rectTransform.position과 동일)에서 카메라로 광선을 쏴 Y=groundY 평면과의 교차점(3D 월드 좌표)을 구함 —
    // 그리드 셀 버튼의 실제 화면 위치를 그대로 3D 좌표로 역산할 때 사용(카메라 줌/각도와 무관하게 항상 화면에 보이는 그대로의 위치)
    public static Vector3 RaycastScreenPointToGroundPlane(Camera cam, Vector3 screenPoint, float groundY)
    {
        if (cam == null) return Vector3.zero;
        Ray ray = cam.ScreenPointToRay(screenPoint);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
        return groundPlane.Raycast(ray, out float dist) ? ray.GetPoint(dist) : Vector3.zero;
    }
    #endregion Exploration Grid World Mapping end -------------------------------------------------------------------

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
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(moduleInfo.moduleSubType);
        if (moduleData == null) return stats;

        // 모듈 타입에 따라 능력치 설정
        if (moduleInfo.moduleType == EModuleType.beam || moduleInfo.moduleType == EModuleType.missile)
        {
            stats.attack = moduleData.attack;
            stats.totalWeapons = 1;
        }
        else if (moduleInfo.moduleType == EModuleType.hanger)
        {
            stats.airAttack = moduleData.airAttack * moduleData.airCount; // 함재기 1기당 공격력 × 함재기 수 = 이 격납고의 총 화력
            stats.airCount = moduleData.airCount;              // 함재기 수
            stats.totalWeapons = 1;
        }
        
        return stats;
    }

    // ModuleBodyInfo로부터 Body 고유의 능력치만 계산
    public static CapabilityProfile GetBodyCapabilityProfile(ModuleBodyInfo bodyInfo)
    {
        CapabilityProfile stats = new CapabilityProfile();
        if (bodyInfo == null) return stats;

        ModuleData bodyData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(bodyInfo.moduleSubType);
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
            // Body 고유 능력치 (Zone 적 배율 반영)
            CapabilityProfile bodyStats = GetBodyCapabilityProfile(bodyInfo);
            stats.health += bodyStats.health * shipInfo.bodyMultiplier;
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
                    stats.totalWeapons += moduleStats.totalWeapons;
                }
            }
        }

        return stats;
    }

    
    public static string GetShipDisplayName(SpaceShip ship)
    {
        string key = $"ShipName_{ship.m_shipInfo.positionIndex}";
        string localized = LocalizationManager.Instance.Get(key);
        if (localized != null && localized != key)
            return localized;
        return ship.m_shipInfo.shipName;
    }

    #endregion Fleet Utility end -----------------------------------------------------------------------------------

    #region Module Type begin -----------------------------------------------------------------------------------
    // 기본 SubType: t1(01), m1(01)
    public static EModuleSubType GetDefaultSubType(EModuleType moduleType)
    {
        EModuleSubType defaultSubType = (EModuleSubType)((int)moduleType * 10000 + 101);
        return defaultSubType;
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

    // 세 자리마다 콤마로 구분된 전체 숫자 문자열 (축약 없음) — 자원 UI 등 전체 값을 그대로 보여줘야 할 때 사용
    public static string FormatNumber(long value)
    {
        return value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
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

    #region ColorPalette begin -----------------------------------------------------------------------------------
    public static Color PaletteColor(string key)
    {
        var palette = DataManager.Instance.m_colorPalette;
        if (palette == null) return Color.white;
        return palette.GetColor(key);
    }
    #endregion ColorPalette end -----------------------------------------------------------------------------------
}