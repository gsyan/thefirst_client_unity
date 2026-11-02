// 모듈 데이터 테이블 ScriptableObject
// CSV Import(에디터 전용) → ScriptableObject 갱신 → JSON Export → 서버 배포 순서로 사용
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif



[System.Serializable]
public class ModuleData
{
    [Header("Basic Info")]
    public string moduleName = "Module";
    public string moduleSubType = "";
    // moduleType은 moduleSubType에서 유추: CommonUtility.ParseModuleType(moduleSubType)

    // common ---------------------------------------------------------------------------
    public int statPoint; // 이 서브타입(티어)을 슬롯에 설치할 때 드는 지휘력(CP) 비용 — 티어가 오를수록 가파르게 증가
    // 레벨 행(type_level)에 도달하는 데 드는 업적포인트 비용(ap_cost 컬럼). hull의 레벨1 행은 그 아키타입의 선체 언락 비용(M1)과 동일한 값 —
    // hull 레벨2~10/beam·missile·hangar·shield·interceptor 전 레벨은 M6 레벨업 비용(ArchetypeModuleUpgradeService가 CSV에서 직접 조회)
    public int unlockAchievementPointCost;
    public string hullArchetype = ""; // hull 전용 — 13개+ 아키타입 중 하나(예: "전투함", "약탈선"). hull 외 카테고리는 ""(미사용)
    // hull 전용 — 실제 프리팹 파일명(예: "hull_1_centurion_11100"). 프리팹은 아키타입당 1개뿐이라 moduleSubType(레벨별로 다름, 예: "hull_1_centurion_3")과
    // 파일명이 다름 — CSV Import 시점(BuildHullPrefabName)에 한 번 찾아서 캐싱, 런타임(Resources.Load)은 이 값만 사용하고 폴더 탐색을 하지 않음
    public string hullPrefabName = "";

    [Header("Description")]
    public string descriptionKey = ""; // UI.csv 로컬라이즈 키 — 원문 텍스트를 직접 담지 않음

    // Hull Module Slots (extracted from prefab) ---------------------------------------
    [Header("Hull Slot Info")]
    public ModuleSlotInfo[] moduleSlots;

    // hull 전용 — 실드/요격체는 3D ModuleSlot 마커가 없는 로직 전용 컴포넌트라 moduleSlots 파싱으로는 보유 여부를 알 수 없어 CSV에 직접 플래그로 둠
    // (실제 전투 중 작동 가능 여부는 이것과 별개로 CommanderHullArchetypeProgress 패시브 해금 진행도가 결정 — 이 플래그는 "설계상 보유하는 아키타입인가"만 의미)
    public bool hasShield = false;
    public bool hasInterceptor = false;

    // Hull ---------------------------------------------------------------------------
    [Header("Hull Stats")]
    public float health = 0f;
    public float repair = 0f;
    public float speed = 0f;
    public float turnRate = 0f; // 선회력

    // Weapon ---------------------------------------------------------------------------
    // 발사체 이동속도는 speed 필드를 재사용(빔/미사일 행은 hull 이동속도 개념이 없으므로 겸용)
    [Header("Weapon Stats")]
    public float attack = 0f;
    public float splashRadius = 0f;     // 0 = 단일 타겟, >0 = 범위 공격 반경
    public float attackCool = 0f;       // 발사 쿨다운 빔, 미사일, 함재기
    public float silenceTime = 0f;      // 미사일 적중 시 무장 침묵 시간 (초)

    // Hangar ------------------------------------------------------------------------------------------------
    [Header("Hangar Stats")]
    public int airCount = 5;                  // 총 함재기 수
    public float airMaintenanceTime = 10f;    // 돌아온 함재기 재출격 까지 정비 시간, 함재기당 재출격에 걸리는 시간
    [Header("Aircraft Stats")]
    public float airHealth = 50f;             // 함재기 체력
    public float airAttackToShip = 10f;       // 함재기 대함 공격력
    public float airAttackToFighter = 10f;    // 함재기 대전투기(도그파이트) 공격력
    public float airAttackRange = 100f;       // 함재기 공격 거리
    public float airAttackCool = 10f;         // 함재기 공격 쿨다운
    public float airSpeed = 200f;             // 함재기 이동력
    public int airAmmo = 10;                  // 함재기 탄약
    public float airDetectRadius = 200f;      // 함재기 적 함재기 감지거리
    public float airAvoidRadius = 200f;       // 함재기 적 회피 거리
    public float airDisrupt = 0f;    // 함재기가 명중 시 타겟 함선에 거는 공격 딜레이(교란) (초)

    // Shield ---------------------------------------------------------------------------
    [Header("Shield Stats")]
    public float shieldGauge = 0f;      // 기본 게이지
    public float shieldRegenRate = 0f;  // 초당 게이지 회복량

    // Interceptor ------------------------------------------------------------------------
    [Header("Interceptor Stats")]
    public int interceptorCount = 0;         // 요격체 재고 수
    public float interceptorRegenTime = 0f;  // 요격체 1기가 다시 생성되기까지 걸리는 시간(초)
}

[System.Serializable]
public class ModuleDataList
{
    [SerializeField] public List<ModuleData> modules = new List<ModuleData>();

    public ModuleData this[int index]
    {
        get => modules[index];
        set => modules[index] = value;
    }

    public int Count => modules.Count;
    public void Add(ModuleData data) => modules.Add(data);
    public bool Remove(ModuleData data) => modules.Remove(data);
    public void Clear() => modules.Clear();
    public ModuleData Find(System.Predicate<ModuleData> match) => modules.Find(match);
    public List<ModuleData> FindAll(System.Predicate<ModuleData> match) => modules.FindAll(match);
}

[System.Serializable]
public class ModuleSubTypeGroup
{
    public string subType;
    public List<ModuleData> modules = new List<ModuleData>();
}

[CreateAssetMenu(fileName = "DataTableModule", menuName = "Custom/DataTableModule")]
public class DataTableModule : ScriptableObject
{
    [Header("Hull Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> hullGroups = new();

    [Header("Beam Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> beamGroups = new();

    [Header("Missile Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> missileGroups = new();

    [Header("Hangar Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> hangarGroups = new();

    [Header("Shield Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> shieldGroups = new();

    [Header("Interceptor Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> interceptorGroups = new();

    public List<ModuleSubTypeGroup> HullGroups => hullGroups;
    public List<ModuleSubTypeGroup> BeamGroups => beamGroups;
    public List<ModuleSubTypeGroup> MissileGroups => missileGroups;
    public List<ModuleSubTypeGroup> HangarGroups => hangarGroups;
    public List<ModuleSubTypeGroup> ShieldGroups => shieldGroups;
    public List<ModuleSubTypeGroup> InterceptorGroups => interceptorGroups;

    public ModuleDataList HullModules
    {
        get
        {
            var list = new ModuleDataList();
            foreach (var group in hullGroups)
                foreach (var module in group.modules)
                    list.Add(module);
            return list;
        }
    }

    public ModuleDataList BeamModules
    {
        get
        {
            var list = new ModuleDataList();
            foreach (var group in beamGroups)
                foreach (var module in group.modules)
                    list.Add(module);
            return list;
        }
    }

    public ModuleDataList MissileModules
    {
        get
        {
            var list = new ModuleDataList();
            foreach (var group in missileGroups)
                foreach (var module in group.modules)
                    list.Add(module);
            return list;
        }
    }

    public ModuleDataList HangarModules
    {
        get
        {
            var list = new ModuleDataList();
            foreach (var group in hangarGroups)
                foreach (var module in group.modules)
                    list.Add(module);
            return list;
        }
    }

    public ModuleDataList ShieldModules
    {
        get
        {
            var list = new ModuleDataList();
            foreach (var group in shieldGroups)
                foreach (var module in group.modules)
                    list.Add(module);
            return list;
        }
    }

    public ModuleDataList InterceptorModules
    {
        get
        {
            var list = new ModuleDataList();
            foreach (var group in interceptorGroups)
                foreach (var module in group.modules)
                    list.Add(module);
            return list;
        }
    }


    #region Public Methods

    public void AddModuleDataToTable(ModuleData data)
    {
        EModuleType moduleType = CommonUtility.ParseModuleType(data.moduleSubType);
        ModuleSubTypeGroup group = null;
        if (moduleType == EModuleType.hull)               group = hullGroups.Find(g => g.subType == data.moduleSubType);
        else if (moduleType == EModuleType.beam)          group = beamGroups.Find(g => g.subType == data.moduleSubType);
        else if (moduleType == EModuleType.missile)       group = missileGroups.Find(g => g.subType == data.moduleSubType);
        else if (moduleType == EModuleType.hangar)        group = hangarGroups.Find(g => g.subType == data.moduleSubType);
        else if (moduleType == EModuleType.shield)        group = shieldGroups.Find(g => g.subType == data.moduleSubType);
        else if (moduleType == EModuleType.interceptor)   group = interceptorGroups.Find(g => g.subType == data.moduleSubType);

        if (group == null)
        {
            group = new ModuleSubTypeGroup { subType = data.moduleSubType };
            if (moduleType == EModuleType.hull)               hullGroups.Add(group);
            else if (moduleType == EModuleType.beam)          beamGroups.Add(group);
            else if (moduleType == EModuleType.missile)       missileGroups.Add(group);
            else if (moduleType == EModuleType.hangar)        hangarGroups.Add(group);
            else if (moduleType == EModuleType.shield)        shieldGroups.Add(group);
            else if (moduleType == EModuleType.interceptor)   interceptorGroups.Add(group);
        }
        group.modules.Add(data);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    // 서브타입(티어)당 데이터 1개 — 레벨 축은 삭제됨(강화는 추후 별도 정률 공식으로 처리 예정)
    public ModuleData GetModuleDataFromTable(string subType)
    {
        ModuleSubTypeGroup group = FindGroup(subType);
        if (group == null || group.modules.Count == 0) return null;
        return group.modules[0];
    }


    private ModuleSubTypeGroup FindGroup(string subType)
    {
        EModuleType moduleType = CommonUtility.ParseModuleType(subType);
        if (moduleType == EModuleType.hull) return hullGroups.Find(g => g.subType == subType);
        if (moduleType == EModuleType.beam) return beamGroups.Find(g => g.subType == subType);
        if (moduleType == EModuleType.missile) return missileGroups.Find(g => g.subType == subType);
        if (moduleType == EModuleType.hangar) return hangarGroups.Find(g => g.subType == subType);
        if (moduleType == EModuleType.shield) return shieldGroups.Find(g => g.subType == subType);
        if (moduleType == EModuleType.interceptor) return interceptorGroups.Find(g => g.subType == subType);
        return null;
    }

    #endregion


    #region JSON Export/Import

    public string ExportToJson()
    {
        var modulesDict = new Dictionary<int, List<object>>
        {
            { (int)EModuleType.hull, HullModules.modules.Cast<object>().ToList() },
            { (int)EModuleType.beam, BeamModules.modules.Cast<object>().ToList() },
            { (int)EModuleType.missile, MissileModules.modules.Cast<object>().ToList() },
            { (int)EModuleType.hangar, HangarModules.modules.Cast<object>().ToList() },
            { (int)EModuleType.shield, ShieldModules.modules.Cast<object>().ToList() },
            { (int)EModuleType.interceptor, InterceptorModules.modules.Cast<object>().ToList() }
        };

        var exportData = new { modules = modulesDict };

        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
        };
        return JsonConvert.SerializeObject(exportData, settings);
    }

    public void ImportFromJson(string json)
    {
        try
        {
            var jsonObj = Newtonsoft.Json.Linq.JObject.Parse(json);
            var modulesObj = jsonObj["modules"];

            if (modulesObj != null)
            {
                hullGroups.Clear();
                beamGroups.Clear();
                missileGroups.Clear();
                hangarGroups.Clear();
                shieldGroups.Clear();
                interceptorGroups.Clear();

                int hullKey = (int)EModuleType.hull;
                if (modulesObj[hullKey.ToString()] != null)
                {
                    var hullList = modulesObj[hullKey.ToString()].ToObject<List<ModuleData>>();
                    foreach (var module in hullList)
                        AddModuleDataToTable(module);
                }

                int beamKey = (int)EModuleType.beam;
                if (modulesObj[beamKey.ToString()] != null)
                {
                    var beamList = modulesObj[beamKey.ToString()].ToObject<List<ModuleData>>();
                    foreach (var module in beamList)
                        AddModuleDataToTable(module);
                }

                int missileKey = (int)EModuleType.missile;
                if (modulesObj[missileKey.ToString()] != null)
                {
                    var missileList = modulesObj[missileKey.ToString()].ToObject<List<ModuleData>>();
                    foreach (var module in missileList)
                        AddModuleDataToTable(module);
                }

                int hangarKey = (int)EModuleType.hangar;
                if (modulesObj[hangarKey.ToString()] != null)
                {
                    var hangarList = modulesObj[hangarKey.ToString()].ToObject<List<ModuleData>>();
                    foreach (var module in hangarList)
                        AddModuleDataToTable(module);
                }

                int shieldKey = (int)EModuleType.shield;
                if (modulesObj[shieldKey.ToString()] != null)
                {
                    var shieldList = modulesObj[shieldKey.ToString()].ToObject<List<ModuleData>>();
                    foreach (var module in shieldList)
                        AddModuleDataToTable(module);
                }

                int interceptorKey = (int)EModuleType.interceptor;
                if (modulesObj[interceptorKey.ToString()] != null)
                {
                    var interceptorList = modulesObj[interceptorKey.ToString()].ToObject<List<ModuleData>>();
                    foreach (var module in interceptorList)
                        AddModuleDataToTable(module);
                }

#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to import JSON: {e.Message}");
        }
    }

    #endregion


    #region Validation & Utility

#if UNITY_EDITOR
    // hull_{tier}_{archetypeSlug}_{level}(레벨 토큰 포함, 아키타입당 10레벨이 프리팹 1개 공유)와
    // 레거시 hull_{tier}_{gen}_{5자리슬롯코드}(gen 토큰이 숫자) 두 포맷이 과도기에 공존 — 3번째 토큰이 숫자가 아니면 신규 포맷
    // 신규 포맷 프리팹 파일명은 "hull_{tier}_{archetypeSlug}_{5자리슬롯코드}"처럼 사람이 보기 좋은 슬롯코드 접미사가 붙을 수 있어
    // CSV 행에서는 알 수 없는 값이므로 정확히 일치하는 이름 대신 "hull_{tier}_{archetypeSlug}_"로 시작하는 프리팹을 폴더에서 찾는다
    private static string BuildHullPrefabName(string hullSubType)
    {
        string[] parts = hullSubType.Split('_');
        bool isLegacyFormat = parts.Length != 4 || int.TryParse(parts[2], out _) == true;
        if (isLegacyFormat == true) return hullSubType;

        string archetypePrefix = $"{parts[0]}_{parts[1]}_{parts[2]}";
        string hullFolderPath = Application.dataPath + "/Resources/Prefabs/ShipModule/Hull";
        string[] matchingPrefabPaths = System.IO.Directory.GetFiles(hullFolderPath, $"{archetypePrefix}_*.prefab");
        if (matchingPrefabPaths.Length == 0) return archetypePrefix;

        return System.IO.Path.GetFileNameWithoutExtension(matchingPrefabPaths[0]);
    }

    private ModuleSlotInfo[] ExtractModuleSlotsFromPrefab(string subType)
    {
        string prefabName = BuildHullPrefabName(subType);
        string prefabPath = $"Prefabs/ShipModule/Hull/{prefabName}";
        GameObject prefab = Resources.Load<GameObject>(prefabPath);
        if (prefab == null) return null;

        ModuleSlot[] slots = prefab.GetComponentsInChildren<ModuleSlot>(true);
        if (slots == null || slots.Length == 0) return null;

        var slotInfos = new List<ModuleSlotInfo>();
        foreach (var slot in slots)
        {
            var info = new ModuleSlotInfo(
                slot.m_moduleSlotInfo.moduleType,
                slot.m_moduleSlotInfo.slotIndex
            );
            slotInfos.Add(info);
        }

        Debug.Log($"Extracted {slotInfos.Count} ModuleSlots from {prefabPath}");
        return slotInfos.ToArray();
    }
#endif

    

#if UNITY_EDITOR
    public void LoadFromCsv(string csvText)
    {
        hullGroups.Clear();
        beamGroups.Clear();
        missileGroups.Clear();
        hangarGroups.Clear();
        shieldGroups.Clear();
        interceptorGroups.Clear();

        // 컬럼 순서 (datatable_module.csv 헤더 기준 고정 인덱스) — type_level(타입_티어_[아키타입]_레벨 또는 티어/세대)당 1행
        // 0:type_level, 1:unlock_achievement_point_cost(hull 전용),
        // 2:hull_archetype(hull 전용), 3:stat_point, 4:health, 5:repair, 6:speed, 7:turn_rate,
        // 8:attack, 9:splash_radius, 10:attack_cool, 11:silence_time,
        // 12:air_count, 13:air_maintenance_time, 14:air_health, 15:air_attack, 16:air_attack_to_fighter,
        // 17:air_attack_range, 18:air_attack_cool, 19:air_speed, 20:air_ammo,
        // 21:air_detect_radius, 22:air_avoid_radius, 23:air_disrupt,
        // 24:shield_gauge, 25:shield_regen_rate,
        // 26:interceptor_count, 27:interceptor_regen_time, 28:description_key(로컬라이즈 키, UI.csv),
        // 29:has_shield(hull 전용, 0/1), 30:has_interceptor(hull 전용, 0/1)
        // 발사체 이동속도(빔/미사일)는 별도 컬럼 없이 speed 컬럼을 재사용
        // 성급(1~5)은 별도 컬럼 없이 type_level의 "hull_" 다음 숫자(ParseTier)를 그대로 사용 — UIAvailableHullRow가 직접 파싱
        string[] lines = csvText.Split('\n');
        if (lines.Length < 2) return;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = ParseCsvLine(line);
            if (cols.Length < 2) continue;

            string moduleSubType = cols[0].Trim();
            if (string.IsNullOrEmpty(moduleSubType) == true) continue;

            bool isHull = CommonUtility.ParseModuleType(moduleSubType) == EModuleType.hull;

            var module = new ModuleData
            {
                moduleName      = $"{moduleSubType}",
                moduleSubType   = moduleSubType,
                unlockAchievementPointCost = ParseCsvInt(cols, 1),
                hullArchetype       = cols.Length > 2 ? cols[2].Trim() : "",
                statPoint           = ParseCsvInt  (cols, 3),
                health              = ParseCsvFloat(cols, 4),
                repair              = ParseCsvFloat(cols, 5),
                speed               = ParseCsvFloat(cols, 6),
                turnRate            = ParseCsvFloat(cols, 7),
                attack              = ParseCsvFloat(cols, 8),
                splashRadius        = ParseCsvFloat(cols, 9),
                attackCool          = ParseCsvFloat(cols, 10),
                silenceTime         = ParseCsvFloat(cols, 11),
                airCount            = ParseCsvInt  (cols, 12),
                airMaintenanceTime  = ParseCsvFloat(cols, 13),
                airHealth           = ParseCsvFloat(cols, 14),
                airAttackToShip     = ParseCsvFloat(cols, 15),
                airAttackToFighter  = ParseCsvFloat(cols, 16),
                airAttackRange      = ParseCsvFloat(cols, 17),
                airAttackCool       = ParseCsvFloat(cols, 18),
                airSpeed            = ParseCsvFloat(cols, 19),
                airAmmo             = ParseCsvInt  (cols, 20),
                airDetectRadius     = ParseCsvFloat(cols, 21),
                airAvoidRadius      = ParseCsvFloat(cols, 22),
                airDisrupt          = ParseCsvFloat(cols, 23),
                shieldGauge         = ParseCsvFloat(cols, 24),
                shieldRegenRate     = ParseCsvFloat(cols, 25),
                interceptorCount        = ParseCsvInt  (cols, 26),
                interceptorRegenTime    = ParseCsvFloat(cols, 27),
                descriptionKey      = cols.Length > 28 ? cols[28].Trim() : "",
                hasShield           = ParseCsvInt(cols, 29) != 0,
                hasInterceptor      = ParseCsvInt(cols, 30) != 0
            };

            // hull 모듈만 prefab에서 슬롯 정보 추출 + 실제 프리팹 파일명 캐싱(런타임 로드용)
            if (isHull == true)
            {
                module.hullPrefabName = BuildHullPrefabName(moduleSubType);
                module.moduleSlots = ExtractModuleSlotsFromPrefab(moduleSubType);
            }

            AddModuleDataToTable(module);
        }

        int total = HullModules.Count + BeamModules.Count + MissileModules.Count + HangarModules.Count
                  + ShieldModules.Count + InterceptorModules.Count;
        Debug.Log($"[DataTableModule] CSV Import 완료: {total}개 모듈");
        EditorUtility.SetDirty(this);
    }

    private float ParseCsvFloat(string[] cols, int idx) => idx < cols.Length ? ParseCsvFloat(cols[idx]) : 0f;
    private int   ParseCsvInt  (string[] cols, int idx) => idx < cols.Length ? ParseCsvInt  (cols[idx]) : 0;

    private string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();
        foreach (char c in line)
        {
            if (c == '"')
                inQuotes = !inQuotes;
            else if (c == ',' && inQuotes == false)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
                current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    private float ParseCsvFloat(string s)
    {
        s = s.Replace(",", "").Trim();
        return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float r) ? r : 0f;
    }

    private int ParseCsvInt(string s) { s = s.Replace(",", "").Trim(); return int.TryParse(s, out int r) ? r : 0; }
    private long ParseCsvLong(string s) { s = s.Replace(",", "").Trim(); return long.TryParse(s, out long r) ? r : 0L; }
#endif

    #endregion


    private void OnValidate()
    {

    }
}