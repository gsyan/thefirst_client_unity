using System;
using System.Collections;
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
        if (moduleInfo.moduleType == EModuleType.beam)
        {
            stats.beamAttack = moduleData.attack;
            stats.totalWeapons = 1;
        }
        else if (moduleInfo.moduleType == EModuleType.missile)
        {
            stats.missileAttack = moduleData.attack;
            stats.totalWeapons = 1;
        }
        else if (moduleInfo.moduleType == EModuleType.hangar)
        {
            stats.airAttack = moduleData.airAttack * moduleData.airCount; // 함재기 1기당 공격력 × 함재기 수 = 이 격납고의 총 화력
            stats.airCount = moduleData.airCount;              // 함재기 수
            stats.totalWeapons = 1;
        }
        
        return stats;
    }

    // ModuleHullInfo로부터 Body 고유의 능력치만 계산
    public static CapabilityProfile GetBodyCapabilityProfile(ModuleHullInfo hullInfo)
    {
        CapabilityProfile stats = new CapabilityProfile();
        if (hullInfo == null) return stats;

        ModuleData bodyData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(hullInfo.moduleSubType);
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
            stats.beamAttack += shipStats.beamAttack;
            stats.missileAttack += shipStats.missileAttack;
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

        if (shipInfo == null || shipInfo.hulls == null) return stats;

        foreach (ModuleHullInfo bodyInfo in shipInfo.hulls)
        {
            // Body 고유 능력치 (Zone 적 배율 반영)
            CapabilityProfile bodyStats = GetBodyCapabilityProfile(bodyInfo);
            stats.health += bodyStats.health * shipInfo.healthMultiplier;
            stats.repair += bodyStats.repair;
            stats.speed  += bodyStats.speed;

            // Beam 모듈들 합산
            if (bodyInfo.beams != null)
            {
                foreach (ModuleInfo moduleInfo in bodyInfo.beams)
                {
                    CapabilityProfile moduleStats = GetModuleCapabilityProfile(moduleInfo);
                    stats.beamAttack += moduleStats.beamAttack;

                    stats.totalWeapons += moduleStats.totalWeapons;
                }
            }

            // Missile 모듈들 합산
            if (bodyInfo.missiles != null)
            {
                foreach (ModuleInfo moduleInfo in bodyInfo.missiles)
                {
                    CapabilityProfile moduleStats = GetModuleCapabilityProfile(moduleInfo);
                    stats.missileAttack += moduleStats.missileAttack;

                    stats.totalWeapons += moduleStats.totalWeapons;
                }
            }

            // Hangar 모듈들 합산
            if (bodyInfo.hangars != null)
            {
                foreach (ModuleInfo moduleInfo in bodyInfo.hangars)
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
    // moduleSubType 문자열의 첫 토큰으로 EModuleType 파싱 (예: "hull_3_1_11100" -> hull, "beam_1_1" -> beam)
    public static EModuleType ParseModuleType(string subType)
    {
        if (string.IsNullOrEmpty(subType) == true) return EModuleType.none;

        string typeName = subType.Split('_')[0];
        bool parsed = System.Enum.TryParse(typeName, out EModuleType moduleType);
        if (parsed == false) return EModuleType.none;

        return moduleType;
    }

    // moduleSubType 문자열의 tier 파싱 (예: "hull_3_1_11100" -> 3)
    public static int ParseTier(string subType)
    {
        if (string.IsNullOrEmpty(subType) == true) return 0;

        string[] parts = subType.Split('_');
        if (parts.Length < 2) return 0;

        int.TryParse(parts[1], out int tier);
        return tier;
    }

    // moduleSubType 문자열의 gen 파싱 (예: "hull_3_1_11100" -> 1)
    public static int ParseGen(string subType)
    {
        if (string.IsNullOrEmpty(subType) == true) return 0;

        string[] parts = subType.Split('_');
        if (parts.Length < 3) return 0;

        int.TryParse(parts[2], out int gen);
        return gen;
    }

    // hull 전용: 5자리 구성을 [빔,미사일,격납고,실드,요격체] 개수 배열로 파싱 (예: "hull_3_1_11100" -> [1,1,1,0,0])
    public static int[] ParseHullSlotComposition(string hullSubType)
    {
        int[] result = new int[5];
        if (string.IsNullOrEmpty(hullSubType) == true) return result;

        string[] parts = hullSubType.Split('_');
        if (parts.Length < 4 || parts[3].Length != 5) return result;

        for (int i = 0; i < 5; i++)
        {
            bool isDigit = char.IsDigit(parts[3][i]);
            if (isDigit == false) return new int[5];
            result[i] = parts[3][i] - '0';
        }

        return result;
    }

    // hull tier 강제 규칙 검증: tier == 빔+미사일+격납고 합 (실드/요격체 제외)
    public static bool ValidateHullTier(string hullSubType)
    {
        int tier = ParseTier(hullSubType);
        int[] composition = ParseHullSlotComposition(hullSubType);
        int compositionTier = composition[0] + composition[1] + composition[2];
        return tier == compositionTier;
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

    // UI에 표시되는 소수점 값은 항상 버림 — 반올림으로 실제 효과보다 부풀려 보이는 것을 방지, 모든 화면에서 동일 규칙 사용
    public static float FloorToDecimals(float value, int decimals)
    {
        float scale = Mathf.Pow(10f, decimals);
        return Mathf.Floor(value * scale + 0.0001f) / scale; // epsilon은 부동소수점 표현 오차로 인한 오버림 방지
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

    // from → to 카운팅 롤링 애니메이션(변화량 * 0.03초, 최대 0.5초) — 재화/포인트 텍스트 갱신 공용
    // onComplete는 애니메이션이 실제로 끝난 프레임(조기 리턴 경로 포함)에 호출됨 — 호출부가 "롤링이 진짜 끝난 시점"을 정확히 알아야 할 때 사용
    public static IEnumerator AnimateCounterText(TMP_Text textUI, long from, long to, System.Action onComplete = null)
    {
        if (from < 0 || from == to)
        {
            textUI.text = FormatNumber(to);
            onComplete?.Invoke();
            yield break;
        }

        float duration = Mathf.Min(Mathf.Abs(to - from) * 0.03f, 0.5f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t       = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            long  current = from + (long)((to - from) * t);
            textUI.text = FormatNumber(current);
            yield return null;
        }

        textUI.text = FormatNumber(to);
        onComplete?.Invoke();
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