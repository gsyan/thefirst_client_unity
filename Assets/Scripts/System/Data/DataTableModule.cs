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
    public int statPoint; // 이 서브타입(티어)을 슬롯에 설치할 때 드는 성능포인트 비용 — 티어가 오를수록 가파르게 증가
    public int unlockCommanderLevel; // hull 전용 — 이 함체가 해금되는 지휘관 레벨. hull 외 카테고리는 0(미사용)

    [Header("Description")]
    [TextArea(2, 4)]
    public string description = "Ship Module";

    // Hull Module Slots (extracted from prefab) ---------------------------------------
    [Header("Hull Slot Info")]
    public ModuleSlotInfo[] moduleSlots;

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
    public float airAttack = 10f;             // 함재기 공격력
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
    public float interceptorDelay = 0f;      // 소모 후 보충 딜레이(초)
    public float interceptorRegenRate = 0f;  // 요격체 재고 회복 속도
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

    [Header("Export/Import")]
    [SerializeField, TextArea(5, 15)] private string exportedJson = "";

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

    // 해금 커맨더 레벨 이하인 hull(함체) 목록만 — 함대 편성 화면의 "선택 가능한 함체 목록"용
    public List<ModuleData> GetUnlockedHullModules(int commanderLevel)
    {
        return HullModules.FindAll(data => data.unlockCommanderLevel <= commanderLevel);
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
        string json = JsonConvert.SerializeObject(exportData, settings);
        exportedJson = json;

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif

        return json;
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
    private ModuleSlotInfo[] ExtractModuleSlotsFromPrefab(string subType)
    {
        string prefabPath = $"Prefabs/ShipModule/Hull/{subType}";
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

        // 컬럼 순서 (datatable_module.csv 헤더 기준 고정 인덱스) — 서브타입(티어)당 1행
        // 0:sub_type, 1:unlock_commander_level(hull 전용), 2:stat_point, 3:health, 4:repair, 5:speed, 6:turn_rate,
        // 7:attack, 8:splash_radius, 9:attack_cool, 10:silence_time,
        // 11:air_count, 12:air_maintenance_time, 13:air_health, 14:air_attack,
        // 15:air_attack_range, 16:air_attack_cool, 17:air_speed, 18:air_ammo,
        // 19:air_detect_radius, 20:air_avoid_radius, 21:air_disrupt,
        // 22:shield_gauge, 23:shield_regen_rate,
        // 24:interceptor_count, 25:interceptor_delay, 26:interceptor_regen_rate, 27:description
        // 발사체 이동속도(빔/미사일)는 별도 컬럼 없이 speed 컬럼을 재사용
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
            if (isHull == true && CommonUtility.ValidateHullTier(moduleSubType) == false)
            {
                Debug.LogError($"[DataTableModule] hull tier 불일치로 임포트 중단: {moduleSubType} (tier는 빔+미사일+격납고 합과 일치해야 함)");
                continue;
            }

            var module = new ModuleData
            {
                moduleName      = $"{moduleSubType}",
                moduleSubType   = moduleSubType,
                unlockCommanderLevel = ParseCsvInt (cols, 1),
                statPoint           = ParseCsvInt  (cols, 2),
                health              = ParseCsvFloat(cols, 3),
                repair              = ParseCsvFloat(cols, 4),
                speed               = ParseCsvFloat(cols, 5),
                turnRate            = ParseCsvFloat(cols, 6),
                attack              = ParseCsvFloat(cols, 7),
                splashRadius        = ParseCsvFloat(cols, 8),
                attackCool          = ParseCsvFloat(cols, 9),
                silenceTime         = ParseCsvFloat(cols, 10),
                airCount            = ParseCsvInt  (cols, 11),
                airMaintenanceTime  = ParseCsvFloat(cols, 12),
                airHealth           = ParseCsvFloat(cols, 13),
                airAttack           = ParseCsvFloat(cols, 14),
                airAttackRange      = ParseCsvFloat(cols, 15),
                airAttackCool       = ParseCsvFloat(cols, 16),
                airSpeed            = ParseCsvFloat(cols, 17),
                airAmmo             = ParseCsvInt  (cols, 18),
                airDetectRadius     = ParseCsvFloat(cols, 19),
                airAvoidRadius      = ParseCsvFloat(cols, 20),
                airDisrupt          = ParseCsvFloat(cols, 21),
                shieldGauge         = ParseCsvFloat(cols, 22),
                shieldRegenRate     = ParseCsvFloat(cols, 23),
                interceptorCount        = ParseCsvInt  (cols, 24),
                interceptorDelay        = ParseCsvFloat(cols, 25),
                interceptorRegenRate    = ParseCsvFloat(cols, 26),
                description         = cols.Length > 27 ? cols[27].Trim() : ""
            };

            // hull 모듈만 prefab에서 슬롯 정보 추출
            if (isHull == true)
                module.moduleSlots = ExtractModuleSlotsFromPrefab(moduleSubType);

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