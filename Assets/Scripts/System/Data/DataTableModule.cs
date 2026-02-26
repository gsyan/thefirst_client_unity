// -------------------------------------------------------------------------------------
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
    public EModuleType moduleType = EModuleType.none;
    public EModuleSubType moduleSubType = EModuleSubType.none;
    public int moduleLevel = 1;

    // Body Module Slots (extracted from prefab) ---------------------------------------
    [Header("Body Slot Info")]
    public ModuleSlotInfo[] moduleSlots;

    // common ---------------------------------------------------------------------------
    [Header("Upgrade Costs")]
    public CostStruct upgradeCost = new CostStruct();

    [Header("Description")]
    [TextArea(2, 4)]
    public string description = "Ship Module";

    // Body ---------------------------------------------------------------------------
    [Header("Body Stats")]
    [Range(0, 1000)]
    public float health = 0f;
    [Range(0, 1000)] 
    public float repairPower = 0f;

    // Engine ---------------------------------------------------------------------------
    [Header("Engine Stats")]
    [Range(0, 20)]
    public float speed = 0f;
    
    // Weapon ---------------------------------------------------------------------------
    [Header("Weapon Stats")]
    [Range(0, 100)]
    public int attackFireCount = 0;   // 발사당 빔, 미사일, 함재기 수
    [Range(0, 100)]
    public float attackPower = 0f;
    [Range(0.1f, 10f)]
    public float attackCoolTime = 0f; // 발사 쿨타임 빔, 미사일, 함재기
    [Header("Weapon Projectile Stats")]
    [Range(0.01f, 5f)]
    public float projectileWidth = 0f;
    [Range(1f, 5000f)]
    public float projectileSpeed = 0f;
    
    // Hanger ------------------------------------------------------------------------------------------------
    [Header("Hanger Stats")]    
    [Range(0, 1000)]
    public int airCount = 5;             // 총 함재기 수
    [Range(0, 1000)]
    public float maintenanceTime = 10f;  // 돌아온 함재기 재출격 까지 정비 시간, 함재기당 재출격에 걸리는 시간
    [Header("Aircraft Stats")]
    [Range(1, 1000)]
    public float aircraftLaunchStraightDistance = 100f;    // 함재기 출격시 직진 거리
    [Range(1, 1000)]
    public float aircraftHealth = 50f;    // 함재기 체력
    [Range(1, 1000)]
    public float aircraftAttackPower = 10f;   // 함재기 공격력
    [Range(1, 1000)]
    public float aircraftAttackRange = 100f;   // 함재기 공격 거리
    [Range(1, 1000)]
    public float aircraftAttackCooldown = 10f;   // 함재기 공격 쿨다운
    [Range(1, 1000)]
    public float aircraftSpeed = 200f;   // 함재기 이동력
    [Range(1, 100)]
    public int aircraftAmmo = 10; // 함재기 탄약
    [Range(1, 1000)]
    public float aircraftDetectionRadius = 200f;   // 함재기 적 함재기 감지거리
    [Range(1, 1000)]
    public float aircraftAvoidanceRadius = 200f;   // 함재기 적 회피 거리


    
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

    [Header("Engine Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> engineGroups = new();

    [Header("Beam Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> beamGroups = new();

    [Header("Missile Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> missileGroups = new();

    [Header("Hanger Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> hangerGroups = new();

    [Header("Export/Import")]
    [SerializeField, TextArea(5, 15)] private string exportedJson = "";

    public List<ModuleSubTypeGroup> BodyGroups => bodyGroups;
    public List<ModuleSubTypeGroup> EngineGroups => engineGroups;
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

    public ModuleDataList EngineModules
    {
        get
        {
            var list = new ModuleDataList();
            foreach (var group in engineGroups)
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
        ModuleSubTypeGroup group = null;
        if( data.moduleType == EModuleType.body)
            group = bodyGroups.Find(g => g.subType == data.moduleSubType);
        else if( data.moduleType == EModuleType.engine)
            group = engineGroups.Find(g => g.subType == data.moduleSubType);
        else if( data.moduleType == EModuleType.beam)
            group = beamGroups.Find(g => g.subType == data.moduleSubType);
        else if( data.moduleType == EModuleType.missile)
            group = missileGroups.Find(g => g.subType == data.moduleSubType);
        else if( data.moduleType == EModuleType.hanger)
            group = hangerGroups.Find(g => g.subType == data.moduleSubType);

        if (group == null)
        {
            group = new ModuleSubTypeGroup { subType = data.moduleSubType };
            bodyGroups.Add(group);
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
        EModuleType moduleType = CommonUtility.GetModuleTypeFromSubType(subType);
        if (moduleType == EModuleType.body) return bodyGroups.Find(g => g.subType == subType);
        if (moduleType == EModuleType.engine) return engineGroups.Find(g => g.subType == subType);
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
            EModuleType moduleType = CommonUtility.GetModuleTypeFromSubType(subType);
            if (moduleType == EModuleType.body)
                bodyGroups.Add(new ModuleSubTypeGroup { subType = subType });
            else if (moduleType == EModuleType.engine)
                engineGroups.Add(new ModuleSubTypeGroup { subType = subType });
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
            { (int)EModuleType.engine, EngineModules.modules.Cast<object>().ToList() },
            { (int)EModuleType.beam, BeamModules.modules.Cast<object>().ToList() },
            { (int)EModuleType.missile, MissileModules.modules.Cast<object>().ToList() },
            { (int)EModuleType.hanger, HangerModules.modules.Cast<object>().ToList() }
        };

        var exportData = new { modules = modulesDict };

        string json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
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
                engineGroups.Clear();
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

                int engineKey = (int)EModuleType.engine;
                if (modulesObj[engineKey.ToString()] != null)
                {
                    var engineList = modulesObj[engineKey.ToString()].ToObject<List<ModuleData>>();
                    foreach (var module in engineList)
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
    private ModuleSlotInfo[] ExtractModuleSlotsFromPrefab(EModuleSubType subType, int level)
    {
        string prefabPath = $"Prefabs/ShipModule/Body/{subType}_{level}";
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

    public bool ValidateData()
    {
        bool isValid = true;

        if (BodyModules.Count == 0)
        {
            Debug.LogWarning("No Body modules defined!");
            isValid = false;
        }

        if (BeamModules.Count == 0)
        {
            Debug.LogWarning("No Weapon modules defined!");
            isValid = false;
        }

        if (MissileModules.Count == 0)
        {
            Debug.LogWarning("No Weapon modules defined!");
            isValid = false;
        }

        if (EngineModules.Count == 0)
        {
            Debug.LogWarning("No Engine modules defined!");
            isValid = false;
        }

        return isValid;
    }

#if UNITY_EDITOR
    public void GenerateLevel1to10Data()
    {
        bodyGroups.Clear();
        engineGroups.Clear();
        beamGroups.Clear();
        missileGroups.Clear();        
        hangerGroups.Clear();
        InitializeSubTypeGroups();

        foreach (EModuleSubType subType in System.Enum.GetValues(typeof(EModuleSubType)))
        {
            if (subType == EModuleSubType.none) continue;
            EModuleType moduleType = CommonUtility.GetModuleTypeFromSubType(subType);
            if( moduleType == EModuleType.body)
            {
                for (int i = 1; i <= 10; i++)
                {
                    // Extract slot info from Body prefab for each level
                    ModuleSlotInfo[] slotInfos = ExtractModuleSlotsFromPrefab(subType, 1);// 프리팹 레벨1만

                    var module = new ModuleData
                    {
                        moduleName = $"{subType} Lv.{i}",
                        moduleType = moduleType,
                        moduleSubType = subType,
                        moduleLevel = i,
                        moduleSlots = slotInfos,
                        health = 100f + ((i - 1) * 50f),
                        repairPower = 5f + ((i - 1) * 1f),
                        //upgradeCost = new CostStruct(i, 100 << (i - 1), 0, 0, 0),
                        upgradeCost = new CostStruct(1, (subType == EModuleSubType.body_battle ? 100 : 1000) << (i - 1), 0, 0, 0),
                        description = $"{subType}-class hull module level {i}"
                    };
                    AddModuleDataToTable(module);
                }
            }
            else if( moduleType == EModuleType.engine)
            {
                for (int i = 1; i <= 10; i++)
                {
                    var module = new ModuleData
                    {
                        moduleName = $"{subType} Lv.{i}",
                        moduleType = moduleType,
                        moduleSubType = subType,
                        moduleLevel = i,
                        health = 0,
                        speed = 50f + (i * 5f),
                        //upgradeCost = new CostStruct(i, 100 << (i - 1), 0, 0, 0),
                        upgradeCost = new CostStruct(1, (subType == EModuleSubType.engine_standard ? 100 : 1000) << (i - 1), 0, 0, 0),
                        description = $"{subType} LV.{i}"
                    };
                    AddModuleDataToTable(module);
                }
            }
            else if( moduleType == EModuleType.beam)
            {
                for (int i = 1; i <= 10; i++)
                {
                    var module = new ModuleData
                    {
                        moduleName = $"{subType} Lv.{i}",
                        moduleType = moduleType,
                        moduleSubType = subType,
                        moduleLevel = i,
                        health = 0,
                        attackFireCount = (subType == EModuleSubType.beam_standard) ? 1 : 2,
                        attackPower = 10f + (i * 5f),                        
                        attackCoolTime = 3.2f - (i * 0.05f),
                        projectileWidth = 5f/* + (i * 0.5f)*/,
                        projectileSpeed = 2000f/* + (i * 50.0f)*/,
                        //upgradeCost = new CostStruct(i,100 << (i - 1), 0, 0, 0),
                        upgradeCost = new CostStruct(1, (subType == EModuleSubType.beam_standard ? 100 : 1000) << (i - 1), 0, 0, 0),
                        description = $"{subType} Lv.{i}"
                    };
                    AddModuleDataToTable(module);
                }
            }
            else if( moduleType == EModuleType.missile)
            {
                for (int i = 1; i <= 10; i++)
                {
                    var module = new ModuleData
                    {
                        moduleName = $"{subType} Lv.{i}",
                        moduleType = moduleType,
                        moduleSubType = subType,
                        moduleLevel = i,
                        health = 0,
                        attackFireCount = (subType == EModuleSubType.missile_standard) ? 1 : 2,
                        attackPower = 10f + (i * 5f),
                        attackCoolTime = 3.2f - (i * 0.05f),
                        projectileWidth = 5f/* + (i * 0.5f)*/,
                        projectileSpeed = 300f/* + (i * 5.0f)*/,
                        //upgradeCost = new CostStruct(i, 100 << (i - 1), 0, 0, 0),
                        upgradeCost = new CostStruct(1, (subType == EModuleSubType.missile_standard ? 100 : 1000) << (i - 1), 0, 0, 0),
                        description = $"{subType} Lv.{i}"
                    };
                    AddModuleDataToTable(module);
                }
            }
            else if( moduleType == EModuleType.hanger)
            {
                for (int i = 1; i <= 10; i++)
                {
                    var module = new ModuleData
                    {
                        moduleName = $"{subType} Lv.{i}",
                        moduleType = moduleType,
                        moduleSubType = subType,
                        moduleLevel = i,
                        health = 0,
                        airCount = 2 + (i * 3),
                        attackCoolTime = 3.0f - (i * 0.15f),
                        attackFireCount = 1 + (i / 4),
                        maintenanceTime = 15.0f - (i * 0.5f),
                        aircraftLaunchStraightDistance = 100f + (i * 5f),
                        aircraftHealth = 30f + (i * 10f),
                        aircraftAttackPower = (subType == EModuleSubType.hanger_standard ? 5 : 50) + (i * 3f),
                        aircraftAttackRange = 100f + (i * 5f),
                        aircraftAttackCooldown = (subType == EModuleSubType.hanger_standard ? 4.1f : 2.1f) - (i * 0.1f),
                        //aircraftAttackCooldown = 1f,
                        aircraftSpeed = 300f + (i * 5f),
                        aircraftAmmo = 10 + (i * 2),
                        aircraftDetectionRadius = 200f + (i * 10f),
                        aircraftAvoidanceRadius = 200f + (i * 5f),
                        //upgradeCost = new CostStruct(i, 100 << (i - 1), 0, 0, 0),
                        upgradeCost = new CostStruct(1, (subType == EModuleSubType.hanger_standard ? 100 : 1000) << (i - 1), 0, 0, 0),
                        description = $"{subType} hanger bay level {i}"
                    };
                    AddModuleDataToTable(module);
                }
            }
        }

        EditorUtility.SetDirty(this);
    }
#endif

    #endregion


    private void OnValidate()
    {

    }
}