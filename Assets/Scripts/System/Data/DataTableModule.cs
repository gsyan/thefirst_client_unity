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
    public int moduleLevel = 1;

    // common ---------------------------------------------------------------------------
    public int modulePointCost;
    public int mineralCost;     // 발동 1회당 소모 Mineral (수리/미사일/격납고)

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

    // Weapon ---------------------------------------------------------------------------
    [Header("Weapon Stats")]
    public int attackFireCount = 0;     // 발사당 빔, 미사일, 함재기 수
    public float attack = 0f;
    public float splashRadius = 0f;     // 0 = 단일 타겟, >0 = 범위 공격 반경
    public float attackCool = 0f;       // 발사 쿨다운 빔, 미사일, 함재기
    [Header("Weapon Projectile Stats")]
    public float projectileSpeed = 0f;

    // Hanger ------------------------------------------------------------------------------------------------
    [Header("Hanger Stats")]
    public int airCount = 5;                  // 총 함재기 수
    public float airMaintenanceTime = 10f;    // 돌아온 함재기 재출격 까지 정비 시간, 함재기당 재출격에 걸리는 시간
    [Header("Aircraft Stats")]
    public float airLaunchDist = 100f;        // 함재기 출격시 직진 거리
    public float airHealth = 50f;             // 함재기 체력
    public float airAttack = 10f;             // 함재기 공격력
    public float airAttackRange = 100f;       // 함재기 공격 거리
    public float airAttackCool = 10f;         // 함재기 공격 쿨다운
    public float airSpeed = 200f;             // 함재기 이동력
    public int airAmmo = 10;                  // 함재기 탄약
    public float airDetectRadius = 200f;      // 함재기 적 함재기 감지거리
    public float airAvoidRadius = 200f;       // 함재기 적 회피 거리    
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

    [Header("Export/Import")]
    [SerializeField, TextArea(5, 15)] private string exportedJson = "";

    public List<ModuleSubTypeGroup> BodyGroups => bodyGroups;
    public List<ModuleSubTypeGroup> BeamGroups => beamGroups;
    public List<ModuleSubTypeGroup> MissileGroups => missileGroups;
    public List<ModuleSubTypeGroup> HangerGroups => hangerGroups;

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


    #region Public Methods

    public void AddModuleDataToTable(ModuleData data)
    {
        EModuleType moduleType = (EModuleType)data.moduleSubType.GetModuleType();
        ModuleSubTypeGroup group = null;
        if (moduleType == EModuleType.body)
            group = bodyGroups.Find(g => g.subType == data.moduleSubType);
        else if (moduleType == EModuleType.beam)
            group = beamGroups.Find(g => g.subType == data.moduleSubType);
        else if (moduleType == EModuleType.missile)
            group = missileGroups.Find(g => g.subType == data.moduleSubType);
        else if (moduleType == EModuleType.hanger)
            group = hangerGroups.Find(g => g.subType == data.moduleSubType);

        if (group == null)
        {
            group = new ModuleSubTypeGroup { subType = data.moduleSubType };
            if (moduleType == EModuleType.body)          bodyGroups.Add(group);
            else if (moduleType == EModuleType.beam)     beamGroups.Add(group);
            else if (moduleType == EModuleType.missile)  missileGroups.Add(group);
            else if (moduleType == EModuleType.hanger)   hangerGroups.Add(group);
        }
        group.modules.Add(data);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public ModuleData GetModuleDataFromTable(EModuleSubType subType, int level)
    {
        ModuleSubTypeGroup group = FindGroup(subType);
        if (group == null) return null;
        return group.modules.Find(m => m.moduleLevel == level);
    }

    // 해당 subType의 최대 레벨 반환 (데이터가 없으면 0)
    public int GetMaxLevel(EModuleSubType subType)
    {
        ModuleSubTypeGroup group = FindGroup(subType);
        if (group == null) return 0;
        return group.modules.Count;
    }

    private ModuleSubTypeGroup FindGroup(EModuleSubType subType)
    {
        EModuleType moduleType = (EModuleType)subType.GetModuleType();
        if (moduleType == EModuleType.body) return bodyGroups.Find(g => g.subType == subType);
        if (moduleType == EModuleType.beam) return beamGroups.Find(g => g.subType == subType);
        if (moduleType == EModuleType.missile) return missileGroups.Find(g => g.subType == subType);
        if (moduleType == EModuleType.hanger) return hangerGroups.Find(g => g.subType == subType);
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
            { (int)EModuleType.hanger, HangerModules.modules.Cast<object>().ToList() }
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
        InitializeSubTypeGroups();

        string[] lines = csvText.Split('\n');
        if (lines.Length < 2) return;

        // 헤더로 컬럼명 → 인덱스 맵 구성
        string[] headers = ParseCsvLine(lines[0].Trim());
        var col = new Dictionary<string, int>();
        for (int i = 0; i < headers.Length; i++)
            col[headers[i].Trim()] = i;

        ModuleSlotInfo[] moduleSlotInfo = null;
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = ParseCsvLine(line);

            if (!int.TryParse(GetCol(cols, col, "sub_type"), out int subTypeInt)) continue;
            if (!int.TryParse(GetCol(cols, col, "level"), out int level)) continue;

            EModuleSubType moduleSubType = (EModuleSubType)subTypeInt;

            var module = new ModuleData
            {
                moduleName = $"{moduleSubType} Lv.{level}",
                moduleSubType = moduleSubType,
                moduleLevel = level,
                health                          = ParseCsvFloat(GetCol(cols, col, "health")),
                repair                          = ParseCsvFloat(GetCol(cols, col, "repair")),
                speed                           = ParseCsvFloat(GetCol(cols, col, "speed")),
                attack                          = ParseCsvFloat(GetCol(cols, col, "attack")),
                splashRadius                    = ParseCsvFloat(GetCol(cols, col, "splash_radius")),
                attackFireCount                 = ParseCsvInt  (GetCol(cols, col, "attack_count")),
                attackCool                      = ParseCsvFloat(GetCol(cols, col, "attack_cool")),
                projectileSpeed                 = ParseCsvFloat(GetCol(cols, col, "projectile_speed")),
                airCount                        = ParseCsvInt  (GetCol(cols, col, "air_count")),
                airMaintenanceTime              = ParseCsvFloat(GetCol(cols, col, "air_maintenance_time")),
                airLaunchDist                   = ParseCsvFloat(GetCol(cols, col, "air_launch_dist")),
                airHealth                       = ParseCsvFloat(GetCol(cols, col, "air_health")),
                airAttack                       = ParseCsvFloat(GetCol(cols, col, "air_attack")),
                airAttackRange                  = ParseCsvFloat(GetCol(cols, col, "air_attack_range")),
                airAttackCool                   = ParseCsvFloat(GetCol(cols, col, "air_attack_cool")),
                airSpeed                        = ParseCsvFloat(GetCol(cols, col, "air_speed")),
                airAmmo                         = ParseCsvInt  (GetCol(cols, col, "air_ammo")),
                airDetectRadius                 = ParseCsvFloat(GetCol(cols, col, "air_detect_radius")),
                airAvoidRadius                  = ParseCsvFloat(GetCol(cols, col, "air_avoid_radius")),
                modulePointCost                 = ParseCsvInt(GetCol(cols, col, "cost_mp")),
                mineralCost                     = ParseCsvInt(GetCol(cols, col, "cost_mineral")),
                description                     = GetCol(cols, col, "description")
            };

            // body 모듈만 prefab에서 슬롯 정보 추출 (레벨1 프리팹 기준으로 모든 레벨 공통 적용)
            if (moduleSubType.GetModuleType() == (int)EModuleType.body && module.moduleLevel == 1)
            {
                moduleSlotInfo = null;
                moduleSlotInfo = ExtractModuleSlotsFromPrefab(moduleSubType);
            }
            module.moduleSlots = moduleSlotInfo;

            AddModuleDataToTable(module);
        }

        int total = BodyModules.Count + BeamModules.Count + MissileModules.Count + HangerModules.Count;
        Debug.Log($"[DataTableModule] CSV Import 완료: {total}개 모듈");
        EditorUtility.SetDirty(this);
    }

    private string GetCol(string[] cols, Dictionary<string, int> colMap, string name)
    {
        if (colMap.TryGetValue(name, out int idx) == false || idx >= cols.Length) return "";
        return cols[idx].Trim();
    }

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