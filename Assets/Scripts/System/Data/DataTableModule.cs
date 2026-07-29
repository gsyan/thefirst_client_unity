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
    public EModuleSubType moduleSubType = EModuleSubType.none;
    // moduleType은 moduleSubType에서 유추: (EModuleType)moduleSubType.GetModuleType()

    // common ---------------------------------------------------------------------------
    public int statPoint; // 이 서브타입(티어)을 슬롯에 설치할 때 드는 성능포인트 비용 — 티어가 오를수록 가파르게 증가

    [Header("Description")]
    [TextArea(2, 4)]
    public string description = "Ship Module";

    // Body Module Slots (extracted from prefab) ---------------------------------------
    [Header("Body Slot Info")]
    public ModuleSlotInfo[] moduleSlots;

    // Body ---------------------------------------------------------------------------
    [Header("Body Stats")]
    public float health = 0f;
    public float repair = 0f;
    public float speed = 0f;
    public float turnRate = 0f; // 선회력 — 함선 프리셋 Flat Stats(Turn Rate Points)의 기본 수치로 사용됨

    // Weapon ---------------------------------------------------------------------------
    // 발사체 이동속도는 speed 필드를 재사용(빔/미사일 행은 body 이동속도 개념이 없으므로 겸용)
    [Header("Weapon Stats")]
    public float attack = 0f;
    public float splashRadius = 0f;     // 0 = 단일 타겟, >0 = 범위 공격 반경
    public float attackCool = 0f;       // 발사 쿨다운 빔, 미사일, 함재기
    public float silenceTime = 0f;      // 미사일 적중 시 무장 침묵 시간 (초)

    // Hanger ------------------------------------------------------------------------------------------------
    [Header("Hanger Stats")]
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
    public float shieldDelay = 0f;      // 재가동 딜레이(초)
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
    public EModuleSubType subType;
    public List<ModuleData> modules = new List<ModuleData>();
}

[CreateAssetMenu(fileName = "DataTableModule", menuName = "Custom/DataTableModule")]
public class DataTableModule : ScriptableObject
{
    [Header("Body Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> bodyGroups = new();

    [Header("Beam Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> beamGroups = new();

    [Header("Missile Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> missileGroups = new();

    [Header("Hanger Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> hangerGroups = new();

    [Header("Shield Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> shieldGroups = new();

    [Header("Interceptor Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> interceptorGroups = new();

    [Header("Export/Import")]
    [SerializeField, TextArea(5, 15)] private string exportedJson = "";

    public List<ModuleSubTypeGroup> BodyGroups => bodyGroups;
    public List<ModuleSubTypeGroup> BeamGroups => beamGroups;
    public List<ModuleSubTypeGroup> MissileGroups => missileGroups;
    public List<ModuleSubTypeGroup> HangerGroups => hangerGroups;
    public List<ModuleSubTypeGroup> ShieldGroups => shieldGroups;
    public List<ModuleSubTypeGroup> InterceptorGroups => interceptorGroups;

    public ModuleDataList BodyModules
    {
        get
        {
            var list = new ModuleDataList();
            foreach (var group in bodyGroups)
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

    public ModuleDataList HangerModules
    {
        get
        {
            var list = new ModuleDataList();
            foreach (var group in hangerGroups)
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
        EModuleType moduleType = (EModuleType)data.moduleSubType.GetModuleType();
        ModuleSubTypeGroup group = null;
        if (moduleType == EModuleType.body)               group = bodyGroups.Find(g => g.subType == data.moduleSubType);
        else if (moduleType == EModuleType.beam)          group = beamGroups.Find(g => g.subType == data.moduleSubType);
        else if (moduleType == EModuleType.missile)       group = missileGroups.Find(g => g.subType == data.moduleSubType);
        else if (moduleType == EModuleType.hanger)        group = hangerGroups.Find(g => g.subType == data.moduleSubType);
        else if (moduleType == EModuleType.shield)        group = shieldGroups.Find(g => g.subType == data.moduleSubType);
        else if (moduleType == EModuleType.interceptor)   group = interceptorGroups.Find(g => g.subType == data.moduleSubType);

        if (group == null)
        {
            group = new ModuleSubTypeGroup { subType = data.moduleSubType };
            if (moduleType == EModuleType.body)               bodyGroups.Add(group);
            else if (moduleType == EModuleType.beam)          beamGroups.Add(group);
            else if (moduleType == EModuleType.missile)       missileGroups.Add(group);
            else if (moduleType == EModuleType.hanger)        hangerGroups.Add(group);
            else if (moduleType == EModuleType.shield)        shieldGroups.Add(group);
            else if (moduleType == EModuleType.interceptor)   interceptorGroups.Add(group);
        }
        group.modules.Add(data);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    // 서브타입(티어)당 데이터 1개 — 레벨 축은 삭제됨(강화는 추후 별도 정률 공식으로 처리 예정)
    public ModuleData GetModuleDataFromTable(EModuleSubType subType)
    {
        ModuleSubTypeGroup group = FindGroup(subType);
        if (group == null || group.modules.Count == 0) return null;
        return group.modules[0];
    }

    private ModuleSubTypeGroup FindGroup(EModuleSubType subType)
    {
        EModuleType moduleType = (EModuleType)subType.GetModuleType();
        if (moduleType == EModuleType.body) return bodyGroups.Find(g => g.subType == subType);
        if (moduleType == EModuleType.beam) return beamGroups.Find(g => g.subType == subType);
        if (moduleType == EModuleType.missile) return missileGroups.Find(g => g.subType == subType);
        if (moduleType == EModuleType.hanger) return hangerGroups.Find(g => g.subType == subType);
        if (moduleType == EModuleType.shield) return shieldGroups.Find(g => g.subType == subType);
        if (moduleType == EModuleType.interceptor) return interceptorGroups.Find(g => g.subType == subType);
        return null;
    }

    public void InitializeSubTypeGroups()
    {
        foreach (EModuleSubType subType in System.Enum.GetValues(typeof(EModuleSubType)))
        {
            if (subType == EModuleSubType.none) continue;
            EModuleType moduleType = (EModuleType)subType.GetModuleType();
            if (moduleType == EModuleType.body)
                bodyGroups.Add(new ModuleSubTypeGroup { subType = subType });
            else if (moduleType == EModuleType.beam)
                beamGroups.Add(new ModuleSubTypeGroup { subType = subType });
            else if (moduleType == EModuleType.missile)
                missileGroups.Add(new ModuleSubTypeGroup { subType = subType });
            else if (moduleType == EModuleType.hanger)
                hangerGroups.Add(new ModuleSubTypeGroup { subType = subType });
            else if (moduleType == EModuleType.shield)
                shieldGroups.Add(new ModuleSubTypeGroup { subType = subType });
            else if (moduleType == EModuleType.interceptor)
                interceptorGroups.Add(new ModuleSubTypeGroup { subType = subType });
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    #endregion


    #region JSON Export/Import

    public string ExportToJson()
    {
        var modulesDict = new Dictionary<int, List<object>>
        {
            { (int)EModuleType.body, BodyModules.modules.Cast<object>().ToList() },
            { (int)EModuleType.beam, BeamModules.modules.Cast<object>().ToList() },
            { (int)EModuleType.missile, MissileModules.modules.Cast<object>().ToList() },
            { (int)EModuleType.hanger, HangerModules.modules.Cast<object>().ToList() },
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
                bodyGroups.Clear();
                beamGroups.Clear();
                missileGroups.Clear();
                hangerGroups.Clear();
                shieldGroups.Clear();
                interceptorGroups.Clear();
                InitializeSubTypeGroups();

                int bodyKey = (int)EModuleType.body;
                if (modulesObj[bodyKey.ToString()] != null)
                {
                    var bodyList = modulesObj[bodyKey.ToString()].ToObject<List<ModuleData>>();
                    foreach (var module in bodyList)
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

                int hangerKey = (int)EModuleType.hanger;
                if (modulesObj[hangerKey.ToString()] != null)
                {
                    var hangerList = modulesObj[hangerKey.ToString()].ToObject<List<ModuleData>>();
                    foreach (var module in hangerList)
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
    private ModuleSlotInfo[] ExtractModuleSlotsFromPrefab(EModuleSubType subType)
    {
        string prefabPath = $"Prefabs/ShipModule/Body/{subType}";
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
        bodyGroups.Clear();
        beamGroups.Clear();
        missileGroups.Clear();
        hangerGroups.Clear();
        shieldGroups.Clear();
        interceptorGroups.Clear();
        InitializeSubTypeGroups();

        // 컬럼 순서 (datatable_module.csv 헤더 기준 고정 인덱스) — 레벨 축 삭제됨, 서브타입(티어)당 1행
        // 0:sub_type, 1:stat_point, 2:health, 3:repair, 4:speed, 5:turn_rate,
        // 6:attack, 7:splash_radius, 8:attack_cool, 9:silence_time,
        // 10:air_count, 11:air_maintenance_time, 12:air_health, 13:air_attack,
        // 14:air_attack_range, 15:air_attack_cool, 16:air_speed, 17:air_ammo,
        // 18:air_detect_radius, 19:air_avoid_radius, 20:air_disrupt,
        // 21:shield_gauge, 22:shield_delay, 23:shield_regen_rate,
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

            if (!int.TryParse(cols[0].Trim(), out int subTypeInt)) continue;

            EModuleSubType moduleSubType = (EModuleSubType)subTypeInt;

            var module = new ModuleData
            {
                moduleName      = $"{moduleSubType}",
                moduleSubType   = moduleSubType,
                statPoint           = ParseCsvInt  (cols, 1),
                health              = ParseCsvFloat(cols, 2),
                repair              = ParseCsvFloat(cols, 3),
                speed               = ParseCsvFloat(cols, 4),
                turnRate            = ParseCsvFloat(cols, 5),
                attack              = ParseCsvFloat(cols, 6),
                splashRadius        = ParseCsvFloat(cols, 7),
                attackCool          = ParseCsvFloat(cols, 8),
                silenceTime         = ParseCsvFloat(cols, 9),
                airCount            = ParseCsvInt  (cols, 10),
                airMaintenanceTime  = ParseCsvFloat(cols, 11),
                airHealth           = ParseCsvFloat(cols, 12),
                airAttack           = ParseCsvFloat(cols, 13),
                airAttackRange      = ParseCsvFloat(cols, 14),
                airAttackCool       = ParseCsvFloat(cols, 15),
                airSpeed            = ParseCsvFloat(cols, 16),
                airAmmo             = ParseCsvInt  (cols, 17),
                airDetectRadius     = ParseCsvFloat(cols, 18),
                airAvoidRadius      = ParseCsvFloat(cols, 19),
                airDisrupt          = ParseCsvFloat(cols, 20),
                shieldGauge         = ParseCsvFloat(cols, 21),
                shieldDelay         = ParseCsvFloat(cols, 22),
                shieldRegenRate     = ParseCsvFloat(cols, 23),
                interceptorCount        = ParseCsvInt  (cols, 24),
                interceptorDelay        = ParseCsvFloat(cols, 25),
                interceptorRegenRate    = ParseCsvFloat(cols, 26),
                description         = cols.Length > 27 ? cols[27].Trim() : ""
            };

            // body 모듈만 prefab에서 슬롯 정보 추출
            if (moduleSubType.GetModuleType() == (int)EModuleType.body)
                module.moduleSlots = ExtractModuleSlotsFromPrefab(moduleSubType);

            AddModuleDataToTable(module);
        }

        int total = BodyModules.Count + BeamModules.Count + MissileModules.Count + HangerModules.Count
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