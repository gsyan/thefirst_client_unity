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
    public int m_moduleTypePacked;

    [Header("Basic Info")]
    public string m_moduleName = "Module";
    public EModuleType m_moduleType = EModuleType.None;
    public EModuleSubType m_moduleSubType = EModuleSubType.None;
    public EModuleStyle m_moduleStyle = EModuleStyle.None;
    public int m_moduleLevel = 1;

#if UNITY_EDITOR
    private void OnValidate()
    {
        m_moduleTypePacked = CommonUtility.CreateModuleTypePacked(m_moduleType, m_moduleSubType, m_moduleStyle);
    }
#endif

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
    public float m_aircraftHealth = 50f;    // 함재기 체력
    [Range(1, 1000)]
    public float m_aircraftAttackPower = 10f;   // 함재기 공격력
    [Range(1, 100)]
    public int m_aircraftAmmo = 10; // 함재기 탄약

}

// [System.Serializable]
// public class ModuleBodyData
// {
//     public int m_moduleTypePacked;

//     [Header("Basic Info")]
//     public string m_name = "New Body";
//     public EModuleSubType m_subType = EModuleSubType.Body_Battle;
//     public EModuleStyle m_style = EModuleStyle.None;
//     public int m_level = 1;

// #if UNITY_EDITOR
//     private void OnValidate()
//     {
//         m_moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Body, m_subType, m_style);
//     }
// #endif

//     [Header("Body Stats")]
//     [Range(1, 1000)]
//     public float m_health = 200f;
//     [Range(0, 1000)] 
//     public float m_cargoCapacity = 100f;    
    
//     [Header("Upgrade Costs")]
//     public CostStruct m_upgradeCost = new CostStruct();



//     [Header("Description")]
//     [TextArea(2, 4)]
//     public string m_description = "Ship body module";
// }

// [System.Serializable]
// public class ModuleEngineData
// {
//     public int m_moduleTypePacked;

//     [Header("Basic Info")]
//     public string m_name = "New Engine";
//     public EModuleSubType m_subType = EModuleSubType.Engine_Standard;
//     public EModuleStyle m_style = EModuleStyle.None;
//     public int m_level = 1;

// #if UNITY_EDITOR
//     private void OnValidate()
//     {
//         m_moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Engine, m_subType, m_style);
//     }
// #endif

//     [Header("Engine Stats")]
//     [Range(1, 1000)]
//     public float m_health = 50f;
//     [Range(0, 20)]
//     public float m_movementSpeed = 5f;
//     [Range(0, 10)]
//     public float m_rotationSpeed = 3f;

//     [Header("Upgrade Costs")]
//     public CostStruct m_upgradeCost = new CostStruct();

//     [Header("Description")]
//     [TextArea(2, 4)]
//     public string m_description = "Engine module";
// }

// [System.Serializable]
// public class ModuleWeaponData
// {
//     public int m_moduleTypePacked;

//     [Header("Basic Info")]
//     public string m_name = "New Weapon";
//     public EModuleSubType m_subType = EModuleSubType.Weapon_Beam;
//     public EModuleStyle m_style = EModuleStyle.None;
//     public int m_level = 1;

// #if UNITY_EDITOR
//     private void OnValidate()
//     {
//         m_moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Weapon, m_subType, m_style);
//     }
// #endif

//     [Header("Weapon Stats")]
//     [Range(1, 1000)]
//     public float m_health = 50f;
//     [Range(0, 100)]
//     public int m_attackFireCount = 1;
//     [Range(0, 100)]
//     public float m_attackPower = 25f;
//     [Range(0.1f, 10f)]
//     public float m_attackCoolTime = 2f;

//     [Header("Projectile Stats")]
//     [Range(1f, 100f)]
//     public float m_projectileLength = 5f;
//     [Range(0.01f, 5f)]
//     public float m_projectileWidth = 0.1f;
//     [Range(1f, 500f)]
//     public float m_projectileSpeed = 20f;

//     [Header("Upgrade Costs")]
//     public CostStruct m_upgradeCost = new CostStruct();

//     [Header("Description")]
//     [TextArea(2, 4)]
//     public string m_description = "Weapon module";
// }



// [System.Serializable]
// public class ModuleHangerData
// {
//     public int m_moduleTypePacked;

//     [Header("Basic Info")]
//     public string m_name = "New Hanger";
//     public EModuleSubType m_subType = EModuleSubType.Hanger_Standard;
//     public EModuleStyle m_style = EModuleStyle.None;
//     public int m_level = 1;

// #if UNITY_EDITOR
//     private void OnValidate()
//     {
//         m_moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Hanger, m_subType, m_style);
//     }
// #endif

//     [Header("Hanger Stats")]
//     [Range(1, 1000)]
//     public float m_health = 50f;

//     [Range(0, 1000)]
//     public int m_hangarCapability = 5;
//     [Range(0, 1000)]
//     public int m_scoutCapability = 5; 
//     [Range(0, 10)]
//     public float m_launchCool = 1f; // 함재기 발사 쿨타임
//     [Range(0, 10)]
//     public int m_launchCount = 1;   // 함재기 회당 발사 댓수
//     [Range(0, 1000)]
//     public float m_maintenanceTime = 10f;   // 돌아온 함재기 재출격 까지 정비 시간, 함재기당 재출격에 걸리는 시간

//     [Header("Aircraft Stats")]
//     [Range(1, 1000)]
//     public float m_aircraftHealth = 50f;    // 함재기 체력
//     [Range(1, 1000)]
//     public float m_aircraftAttackPower = 10f;   // 함재기 공격력
//     [Range(1, 100)]
//     public int m_aircraftAmmo = 10; // 함재기 탄약

//     [Header("Upgrade Costs")]
//     public CostStruct m_upgradeCost = new CostStruct();

//     [Header("Description")]
//     [TextArea(2, 4)]
//     public string m_description = "Hanger module";
// }

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

    [Header("Weapon Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> weaponGroups = new();

    [Header("Hanger Modules by SubType")]
    [SerializeField] private List<ModuleSubTypeGroup> hangerGroups = new();

    [Header("Export/Import")]
    [SerializeField, TextArea(5, 15)] private string exportedJson = "";

    public List<ModuleSubTypeGroup> BodyGroups => bodyGroups;
    public List<ModuleSubTypeGroup> EngineGroups => engineGroups;
    public List<ModuleSubTypeGroup> WeaponGroups => weaponGroups;
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

    public ModuleDataList WeaponModules
    {
        get
        {
            var list = new ModuleDataList();
            foreach (var group in weaponGroups)
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
        if( data.m_moduleType == EModuleType.Body)
            group = bodyGroups.Find(g => g.subType == data.m_moduleSubType);
        else if( data.m_moduleType == EModuleType.Engine)
            group = engineGroups.Find(g => g.subType == data.m_moduleSubType);
        else if( data.m_moduleType == EModuleType.Weapon)
            group = weaponGroups.Find(g => g.subType == data.m_moduleSubType);
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
        else if( moduleType == EModuleType.Weapon)
            group = weaponGroups.Find(g => g.subType == subType);
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
            else if (moduleType == EModuleType.Weapon)
                weaponGroups.Add(new ModuleSubTypeGroup { subType = subType });
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
            { (int)EModuleType.Weapon, WeaponModules.modules.Cast<object>().ToList() },
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
                weaponGroups.Clear();
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

                int weaponKey = (int)EModuleType.Weapon;
                if (modulesObj[weaponKey.ToString()] != null)
                {
                    var weaponList = modulesObj[weaponKey.ToString()].ToObject<List<ModuleData>>();
                    foreach (var module in weaponList)
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

    public bool ValidateData()
    {
        bool isValid = true;

        if (BodyModules.Count == 0)
        {
            Debug.LogWarning("No Body modules defined!");
            isValid = false;
        }

        if (WeaponModules.Count == 0)
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

    public void GenerateLevel1to10Data()
    {
        bodyGroups.Clear();
        weaponGroups.Clear();
        engineGroups.Clear();
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
                    var module = new ModuleData
                    {
                        m_moduleName = $"{subType} Lv.{i}",
                        m_moduleType = moduleType,
                        m_moduleSubType = subType,
                        m_moduleStyle = EModuleStyle.None,
                        m_moduleLevel = i,
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
                        m_moduleStyle = EModuleStyle.None,
                        m_moduleLevel = i,
                        m_health = 30f + (i * 10f),
                        m_movementSpeed = 3f + (i * 0.5f),
                        //m_upgradeCost = new CostStruct(i, 50 * i, 0, 0, 0),
                        m_upgradeCost = new CostStruct(1, 50 * i, 0, 0, 0),
                        m_description = $"{subType} LV.{i}"
                    };
                    AddModuleDataToTable(module);
                }
            }
            else if( moduleType == EModuleType.Weapon)
            {
                for (int i = 1; i <= 10; i++)
                {
                    var module = new ModuleData
                    {
                        m_moduleName = $"{subType} Lv.{i}",
                        m_moduleType = moduleType,
                        m_moduleSubType = subType,
                        m_moduleStyle = EModuleStyle.None,
                        m_moduleLevel = i,
                        m_health = 30f + (i * 10f),
                        m_attackFireCount = 1 + (i / 5),
                        m_attackPower = 10f + (i * 5f),                        
                        m_attackCoolTime = 2.0f - (i * 0.05f),
                        m_projectileLength = 5f/* + (i * 0.5f)*/,
                        m_projectileWidth = 0.1f/* + (i * 0.05f)*/,
                        m_projectileSpeed = 20f/* + (i * 0.5f)*/,
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
                        m_moduleStyle = EModuleStyle.None,
                        m_moduleLevel = i,
                        m_health = 40f + (i * 15f),
                        m_hangarCapability = 2 + (i * 3),
                        m_scoutCapability = 1 + (i * 2),
                        m_launchCool = 3.0f - (i * 0.15f),
                        m_launchCount = 1 + (i / 4),
                        m_maintenanceTime = 15.0f - (i * 0.5f),
                        m_aircraftHealth = 30f + (i * 10f),
                        m_aircraftAttackPower = 5f + (i * 3f),
                        m_aircraftAmmo = 10 + (i * 2),
                        //m_upgradeCost = new CostStruct(i, 50 * i, 0, 0, 0),
                        m_upgradeCost = new CostStruct(1, 50 * i, 0, 0, 0),
                        m_description = $"{subType} hanger bay level {i}"
                    };
                    AddModuleDataToTable(module);
                }
            }
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    #endregion


    private void OnValidate()
    {

    }
}