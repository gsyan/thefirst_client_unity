// -------------------------------------------------------------------------------------
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

// 모듈 최대 능력치 (육각형 차트 백분율 계산용)
[System.Serializable]
public struct ModuleMaxStats
{
    public float maxDps;
    public float maxHp;
    public float maxSpeed;
    public float maxCargo;
}

[System.Serializable]
public class ModuleData
{
    [Header("Basic Info")]
    public string m_moduleName = "Module";
    public EModuleType m_moduleType = EModuleType.None;
    public EModuleSubType m_moduleSubType = EModuleSubType.None;
    public int m_moduleLevel = 1;

    // Body Module Slots (extracted from prefab) ---------------------------------------
    [Header("Body Slot Info")]
    public ModuleSlotInfo[] m_moduleSlots;

    // common ---------------------------------------------------------------------------
    [Header("Body Stats")]
    [Range(1, 1000)]
    public float m_health = 0f;
    [Range(0, 1000)] 
    public float m_cargoCapacity = 0f;    
    
    [Header("Upgrade Costs")]
    public CostStruct m_upgradeCost = new CostStruct();

    [Header("Description")]
    [TextArea(2, 4)]
    public string m_description = "Ship Module";

    // Engine ---------------------------------------------------------------------------
    [Header("Engine Stats")]
    [Range(0, 20)]
    public float m_movementSpeed = 0f;
    
    // Weapon ---------------------------------------------------------------------------
    [Header("Weapon Stats")]
    [Range(0, 100)]
    public int m_attackFireCount = 0;
    [Range(0, 100)]
    public float m_attackPower = 0f;
    [Range(0.1f, 10f)]
    public float m_attackCoolTime = 0f;
    [Header("Weapon Projectile Stats")]
    [Range(1f, 100f)]
    public float m_projectileLength = 0f;
    [Range(0.01f, 5f)]
    public float m_projectileWidth = 0f;
    [Range(1f, 500f)]
    public float m_projectileSpeed = 0f;
    
    // Hanger ------------------------------------------------------------------------------------------------
    [Header("Hanger Stats")]    
    [Range(0, 1000)]
    public int m_hangarCapability = 5;
    [Range(0, 1000)]
    public int m_scoutCapability = 5; 
    [Range(0, 10)]
    public float m_launchCool = 1f; // 함재기 발사 쿨타임
    [Range(0, 10)]
    public int m_launchCount = 1;   // 함재기 회당 발사 댓수
    [Range(0, 1000)]
    public float m_maintenanceTime = 10f;   // 돌아온 함재기 재출격 까지 정비 시간, 함재기당 재출격에 걸리는 시간
    [Header("Aircraft Stats")]
    [Range(1, 1000)]
    public float m_aircraftLaunchStraightDistance = 100f;    // 함재기 출격시 직진 거리
    [Range(1, 1000)]
    public float m_aircraftHealth = 50f;    // 함재기 체력
    [Range(1, 1000)]
    public float m_aircraftAttackPower = 10f;   // 함재기 공격력
    [Range(1, 1000)]
    public float m_aircraftAttackRange = 100f;   // 함재기 공격 거리
    [Range(1, 1000)]
    public float m_aircraftAttackCooldown = 10f;   // 함재기 공격 쿨다운
    [Range(1, 1000)]
    public float m_aircraftSpeed = 200f;   // 함재기 이동력
    [Range(1, 100)]
    public int m_aircraftAmmo = 10; // 함재기 탄약
    [Range(1, 1000)]
    public float m_aircraftDetectionRadius = 200f;   // 함재기 적 함재기 감지거리
    [Range(1, 1000)]
    public float m_aircraftAvoidanceRadius = 200f;   // 함재기 적 회피 거리


    
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

    // 최대 능력치 (캐싱)
    public ModuleMaxStats MaxStats { get; private set; }

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

    /// <summary>
    /// 모든 모듈 데이터에서 각 능력치의 최대값을 계산하여 캐싱
    /// DataManager 초기화 시 호출
    /// </summary>
    public void CalculateMaxStats()
    {
        float maxDps = 0f;
        float maxHp = 0f;
        float maxSpeed = 0f;
        float maxCargo = 0f;

        // Beam 모듈에서 최대 DPS
        foreach (var group in beamGroups)
        {
            foreach (var module in group.modules)
            {
                if (module.m_attackCoolTime > 0)
                {
                    float dps = module.m_attackPower * module.m_attackFireCount / module.m_attackCoolTime;
                    if (dps > maxDps) maxDps = dps;
                }
            }
        }

        // Missile 모듈에서 최대 DPS
        foreach (var group in missileGroups)
        {
            foreach (var module in group.modules)
            {
                if (module.m_attackCoolTime > 0)
                {
                    float dps = module.m_attackPower * module.m_attackFireCount / module.m_attackCoolTime;
                    if (dps > maxDps) maxDps = dps;
                }
            }
        }

        // Body 모듈에서 최대 HP, Cargo
        foreach (var group in bodyGroups)
        {
            foreach (var module in group.modules)
            {
                if (module.m_health > maxHp) maxHp = module.m_health;
                if (module.m_cargoCapacity > maxCargo) maxCargo = module.m_cargoCapacity;
            }
        }

        // Engine 모듈에서 최대 Speed
        foreach (var group in engineGroups)
        {
            foreach (var module in group.modules)
            {
                if (module.m_movementSpeed > maxSpeed) maxSpeed = module.m_movementSpeed;
            }
        }

        MaxStats = new ModuleMaxStats
        {
            maxDps = maxDps,
            maxHp = maxHp,
            maxSpeed = maxSpeed,
            maxCargo = maxCargo
        };

        Debug.Log($"[DataTableModule] MaxStats calculated - DPS:{maxDps}, HP:{maxHp}, Speed:{maxSpeed}, Cargo:{maxCargo}");
    }

    public void AddModuleDataToTable(ModuleData data)
    {
        ModuleSubTypeGroup group = null;
        if( data.m_moduleType == EModuleType.Body)
            group = bodyGroups.Find(g => g.subType == data.m_moduleSubType);
        else if( data.m_moduleType == EModuleType.Engine)
            group = engineGroups.Find(g => g.subType == data.m_moduleSubType);
        else if( data.m_moduleType == EModuleType.Beam)
            group = beamGroups.Find(g => g.subType == data.m_moduleSubType);
        else if( data.m_moduleType == EModuleType.Missile)
            group = missileGroups.Find(g => g.subType == data.m_moduleSubType);
        else if( data.m_moduleType == EModuleType.Hanger)
            group = hangerGroups.Find(g => g.subType == data.m_moduleSubType);

        if (group == null)
        {
            group = new ModuleSubTypeGroup { subType = data.m_moduleSubType };
            bodyGroups.Add(group);
        }
        group.modules.Add(data);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public ModuleData GetModuleDataFromTable(EModuleSubType subType, int level)
    {
        ModuleSubTypeGroup group = null;
        EModuleType moduleType = CommonUtility.GetModuleTypeFromSubType(subType);

        if( moduleType == EModuleType.Body)
            group = bodyGroups.Find(g => g.subType == subType);
        else if( moduleType == EModuleType.Engine)
            group = engineGroups.Find(g => g.subType == subType);
        else if( moduleType == EModuleType.Beam)
            group = beamGroups.Find(g => g.subType == subType);
        else if( moduleType == EModuleType.Missile)
            group = missileGroups.Find(g => g.subType == subType);
        else if( moduleType == EModuleType.Hanger)
            group = hangerGroups.Find(g => g.subType == subType);

        if (group == null) return null;
        return group.modules.Find(m => m.m_moduleLevel == level);
    }

    public void InitializeSubTypeGroups()
    {
        foreach (EModuleSubType subType in System.Enum.GetValues(typeof(EModuleSubType)))
        {
            if (subType == EModuleSubType.None) continue;
            EModuleType moduleType = CommonUtility.GetModuleTypeFromSubType(subType);
            if (moduleType == EModuleType.Body)
                bodyGroups.Add(new ModuleSubTypeGroup { subType = subType });
            else if (moduleType == EModuleType.Engine)
                engineGroups.Add(new ModuleSubTypeGroup { subType = subType });
            else if (moduleType == EModuleType.Beam)
                beamGroups.Add(new ModuleSubTypeGroup { subType = subType });
            else if (moduleType == EModuleType.Missile)
                missileGroups.Add(new ModuleSubTypeGroup { subType = subType });
            else if (moduleType == EModuleType.Hanger)
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
            { (int)EModuleType.Body, BodyModules.modules.Cast<object>().ToList() },
            { (int)EModuleType.Engine, EngineModules.modules.Cast<object>().ToList() },
            { (int)EModuleType.Beam, BeamModules.modules.Cast<object>().ToList() },
            { (int)EModuleType.Missile, MissileModules.modules.Cast<object>().ToList() },
            { (int)EModuleType.Hanger, HangerModules.modules.Cast<object>().ToList() }
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

                int bodyKey = (int)EModuleType.Body;
                if (modulesObj[bodyKey.ToString()] != null)
                {
                    var bodyList = modulesObj[bodyKey.ToString()].ToObject<List<ModuleData>>();
                    foreach (var module in bodyList)
                        AddModuleDataToTable(module);
                }

                int engineKey = (int)EModuleType.Engine;
                if (modulesObj[engineKey.ToString()] != null)
                {
                    var engineList = modulesObj[engineKey.ToString()].ToObject<List<ModuleData>>();
                    foreach (var module in engineList)
                        AddModuleDataToTable(module);
                }

                int beamKey = (int)EModuleType.Beam;
                if (modulesObj[beamKey.ToString()] != null)
                {
                    var beamList = modulesObj[beamKey.ToString()].ToObject<List<ModuleData>>();
                    foreach (var module in beamList)
                        AddModuleDataToTable(module);
                }

                int missileKey = (int)EModuleType.Missile;
                if (modulesObj[missileKey.ToString()] != null)
                {
                    var missileList = modulesObj[missileKey.ToString()].ToObject<List<ModuleData>>();
                    foreach (var module in missileList)
                        AddModuleDataToTable(module);
                }

                int hangerKey = (int)EModuleType.Hanger;
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
            if (subType == EModuleSubType.None) continue;
            EModuleType moduleType = CommonUtility.GetModuleTypeFromSubType(subType);
            if( moduleType == EModuleType.Body)
            {
                for (int i = 1; i <= 10; i++)
                {
                    // Extract slot info from Body prefab for each level
                    ModuleSlotInfo[] slotInfos = ExtractModuleSlotsFromPrefab(subType, i);

                    var module = new ModuleData
                    {
                        m_moduleName = $"{subType} Lv.{i}",
                        m_moduleType = moduleType,
                        m_moduleSubType = subType,
                        m_moduleLevel = i,
                        m_moduleSlots = slotInfos,
                        m_health = 100f + (i * 50f),
                        m_cargoCapacity = 50f + (i * 25f),
                        //m_upgradeCost = new CostStruct(i, 50 * i, 0, 0, 0),
                        m_upgradeCost = new CostStruct(1, 50 * i, 0, 0, 0),
                        m_description = $"{subType}-class hull module level {i}"
                    };
                    AddModuleDataToTable(module);
                }
            }
            else if( moduleType == EModuleType.Engine)
            {
                for (int i = 1; i <= 10; i++)
                {
                    var module = new ModuleData
                    {
                        m_moduleName = $"{subType} Lv.{i}",
                        m_moduleType = moduleType,
                        m_moduleSubType = subType,
                        m_moduleLevel = i,
                        m_health = 30f + (i * 10f),
                        m_movementSpeed = 50f + (i * 5f),
                        //m_upgradeCost = new CostStruct(i, 50 * i, 0, 0, 0),
                        m_upgradeCost = new CostStruct(1, 50 * i, 0, 0, 0),
                        m_description = $"{subType} LV.{i}"
                    };
                    AddModuleDataToTable(module);
                }
            }
            else if( moduleType == EModuleType.Beam)
            {
                for (int i = 1; i <= 10; i++)
                {
                    var module = new ModuleData
                    {
                        m_moduleName = $"{subType} Lv.{i}",
                        m_moduleType = moduleType,
                        m_moduleSubType = subType,
                        m_moduleLevel = i,
                        m_health = 30f + (i * 10f),
                        m_attackFireCount = 1 + (i / 5),
                        m_attackPower = 10f + (i * 5f),                        
                        m_attackCoolTime = 2.0f - (i * 0.05f),
                        m_projectileLength = 50f/* + (i * 5.0f)*/,
                        m_projectileWidth = 5f/* + (i * 0.5f)*/,
                        m_projectileSpeed = 200f/* + (i * 5.0f)*/,
                        //m_upgradeCost = new CostStruct(i, 50 * i, 0, 0, 0),
                        m_upgradeCost = new CostStruct(1, 50 * i, 0, 0, 0),
                        m_description = $"{subType} Lv.{i}"
                    };
                    AddModuleDataToTable(module);
                }
            }
            else if( moduleType == EModuleType.Missile)
            {
                for (int i = 1; i <= 10; i++)
                {
                    var module = new ModuleData
                    {
                        m_moduleName = $"{subType} Lv.{i}",
                        m_moduleType = moduleType,
                        m_moduleSubType = subType,
                        m_moduleLevel = i,
                        m_health = 30f + (i * 10f),
                        m_attackFireCount = 1 + (i / 5),
                        m_attackPower = 10f + (i * 5f),                        
                        m_attackCoolTime = 2.0f - (i * 0.05f),
                        m_projectileLength = 50f/* + (i * 5.0f)*/,
                        m_projectileWidth = 5f/* + (i * 0.5f)*/,
                        m_projectileSpeed = 200f/* + (i * 5.0f)*/,
                        //m_upgradeCost = new CostStruct(i, 50 * i, 0, 0, 0),
                        m_upgradeCost = new CostStruct(1, 50 * i, 0, 0, 0),
                        m_description = $"{subType} Lv.{i}"
                    };
                    AddModuleDataToTable(module);
                }
            }
            else if( moduleType == EModuleType.Hanger)
            {
                for (int i = 1; i <= 10; i++)
                {
                    var module = new ModuleData
                    {
                        m_moduleName = $"{subType} Lv.{i}",
                        m_moduleType = moduleType,
                        m_moduleSubType = subType,
                        m_moduleLevel = i,
                        m_health = 40f + (i * 15f),
                        m_hangarCapability = 2 + (i * 3),
                        m_scoutCapability = 1 + (i * 2),
                        m_launchCool = 3.0f - (i * 0.15f),
                        m_launchCount = 1 + (i / 4),
                        m_maintenanceTime = 15.0f - (i * 0.5f),
                        m_aircraftLaunchStraightDistance = 100f + (i * 5f),
                        m_aircraftHealth = 30f + (i * 10f),
                        m_aircraftAttackPower = 5f + (i * 3f),
                        m_aircraftAttackRange = 100f + (i * 5f),
                        //m_aircraftAttackCooldown = 10f - (i * 0.2f),
                        m_aircraftAttackCooldown = 1f,
                        m_aircraftSpeed = 300f + (i * 5f),
                        m_aircraftAmmo = 10 + (i * 2),
                        m_aircraftDetectionRadius = 200f + (i * 10f),
                        m_aircraftAvoidanceRadius = 200f + (i * 5f),
                        //m_upgradeCost = new CostStruct(i, 50 * i, 0, 0, 0),
                        m_upgradeCost = new CostStruct(1, 50 * i, 0, 0, 0),
                        m_description = $"{subType} hanger bay level {i}"
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