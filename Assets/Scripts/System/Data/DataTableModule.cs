// -------------------------------------------------------------------------------------
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class ModuleBodyData
{
    public int m_moduleTypePacked;

    [Header("Basic Info")]
    public string m_name = "New Body";
    public EModuleBodySubType m_subType = EModuleBodySubType.Battle;
    public EModuleStyle m_style = EModuleStyle.StyleA;
    public int m_level = 1;

#if UNITY_EDITOR
    private void OnValidate()
    {
        m_moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Body, (int)m_subType, m_style);
    }
#endif

    [Header("Body Stats")]
    [Range(1, 1000)]
    public float m_health = 200f;
    [Range(0, 1000)] 
    public float m_cargoCapacity = 100f;    
    
    [Header("Upgrade Costs")]
    public CostStruct m_upgradeCost = new CostStruct();



    [Header("Description")]
    [TextArea(2, 4)]
    public string m_description = "Ship body module";
}

[System.Serializable]
public class ModuleEngineData
{
    public int m_moduleTypePacked;

    [Header("Basic Info")]
    public string m_name = "New Engine";
    public EModuleEngineSubType m_subType = EModuleEngineSubType.Standard;
    public EModuleStyle m_style = EModuleStyle.StyleA;
    public int m_level = 1;

#if UNITY_EDITOR
    private void OnValidate()
    {
        m_moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Engine, (int)m_subType, m_style);
    }
#endif

    [Header("Engine Stats")]
    [Range(1, 1000)]
    public float m_health = 50f;
    [Range(0, 20)]
    public float m_movementSpeed = 5f;
    [Range(0, 10)]
    public float m_rotationSpeed = 3f;

    [Header("Upgrade Costs")]
    public CostStruct m_upgradeCost = new CostStruct();

    [Header("Description")]
    [TextArea(2, 4)]
    public string m_description = "Engine module";
}

[System.Serializable]
public class ModuleWeaponData
{
    public int m_moduleTypePacked;

    [Header("Basic Info")]
    public string m_name = "New Weapon";
    public EModuleWeaponSubType m_subType = EModuleWeaponSubType.Beam;
    public EModuleStyle m_style = EModuleStyle.StyleA;
    public int m_level = 1;

#if UNITY_EDITOR
    private void OnValidate()
    {
        m_moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Weapon, (int)m_subType, m_style);
    }
#endif

    [Header("Weapon Stats")]
    [Range(1, 1000)]
    public float m_health = 50f;
    [Range(0, 100)]
    public int m_attackFireCount = 1;
    [Range(0, 100)]
    public float m_attackPower = 25f;
    [Range(0.1f, 10f)]
    public float m_attackCoolTime = 2f;

    [Header("Projectile Stats")]
    [Range(1f, 100f)]
    public float m_projectileLength = 5f;
    [Range(0.01f, 5f)]
    public float m_projectileWidth = 0.1f;
    [Range(1f, 500f)]
    public float m_projectileSpeed = 20f;

    [Header("Upgrade Costs")]
    public CostStruct m_upgradeCost = new CostStruct();

    [Header("Description")]
    [TextArea(2, 4)]
    public string m_description = "Weapon module";
}



[System.Serializable]
public class ModuleHangerData
{
    public int m_moduleTypePacked;

    [Header("Basic Info")]
    public string m_name = "New Hanger";
    public EModuleHangerSubType m_subType = EModuleHangerSubType.Standard;
    public EModuleStyle m_style = EModuleStyle.StyleA;
    public int m_level = 1;

#if UNITY_EDITOR
    private void OnValidate()
    {
        m_moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Hanger, (int)m_subType, m_style);
    }
#endif

    [Header("Hanger Stats")]
    [Range(1, 1000)]
    public float m_health = 50f;

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

    [Header("Upgrade Costs")]
    public CostStruct m_upgradeCost = new CostStruct();

    [Header("Description")]
    [TextArea(2, 4)]
    public string m_description = "Hanger module";
}

[System.Serializable]
public class ModuleBodyDataList
{
    [SerializeField] public List<ModuleBodyData> modules = new List<ModuleBodyData>();

    public ModuleBodyData this[int index]
    {
        get => modules[index];
        set => modules[index] = value;
    }

    public int Count => modules.Count;
    public void Add(ModuleBodyData data) => modules.Add(data);
    public bool Remove(ModuleBodyData data) => modules.Remove(data);
    public void Clear() => modules.Clear();
    public ModuleBodyData Find(System.Predicate<ModuleBodyData> match) => modules.Find(match);
    public List<ModuleBodyData> FindAll(System.Predicate<ModuleBodyData> match) => modules.FindAll(match);
}

[System.Serializable]
public class ModuleWeaponDataList
{
    [SerializeField] public List<ModuleWeaponData> modules = new List<ModuleWeaponData>();

    public ModuleWeaponData this[int index]
    {
        get => modules[index];
        set => modules[index] = value;
    }

    public int Count => modules.Count;
    public void Add(ModuleWeaponData data) => modules.Add(data);
    public bool Remove(ModuleWeaponData data) => modules.Remove(data);
    public void Clear() => modules.Clear();
    public ModuleWeaponData Find(System.Predicate<ModuleWeaponData> match) => modules.Find(match);
    public List<ModuleWeaponData> FindAll(System.Predicate<ModuleWeaponData> match) => modules.FindAll(match);
}

[System.Serializable]
public class ModuleEngineDataList
{
    [SerializeField] public List<ModuleEngineData> modules = new List<ModuleEngineData>();

    public ModuleEngineData this[int index]
    {
        get => modules[index];
        set => modules[index] = value;
    }

    public int Count => modules.Count;
    public void Add(ModuleEngineData data) => modules.Add(data);
    public bool Remove(ModuleEngineData data) => modules.Remove(data);
    public void Clear() => modules.Clear();
    public ModuleEngineData Find(System.Predicate<ModuleEngineData> match) => modules.Find(match);
    public List<ModuleEngineData> FindAll(System.Predicate<ModuleEngineData> match) => modules.FindAll(match);
}

[System.Serializable]
public class ModuleHangerDataList
{
    [SerializeField] public List<ModuleHangerData> modules = new List<ModuleHangerData>();

    public ModuleHangerData this[int index]
    {
        get => modules[index];
        set => modules[index] = value;
    }

    public int Count => modules.Count;
    public void Add(ModuleHangerData data) => modules.Add(data);
    public bool Remove(ModuleHangerData data) => modules.Remove(data);
    public void Clear() => modules.Clear();
    public ModuleHangerData Find(System.Predicate<ModuleHangerData> match) => modules.Find(match);
    public List<ModuleHangerData> FindAll(System.Predicate<ModuleHangerData> match) => modules.FindAll(match);
}

[System.Serializable]
public class ModuleBodySubTypeGroup
{
    public EModuleBodySubType subType;
    public List<ModuleBodyData> modules = new List<ModuleBodyData>();
}

[System.Serializable]
public class ModuleWeaponSubTypeGroup
{
    public EModuleWeaponSubType subType;
    public List<ModuleWeaponData> modules = new List<ModuleWeaponData>();
}

[System.Serializable]
public class ModuleEngineSubTypeGroup
{
    public EModuleEngineSubType subType;
    public List<ModuleEngineData> modules = new List<ModuleEngineData>();
}

[System.Serializable]
public class ModuleHangerSubTypeGroup
{
    public EModuleHangerSubType subType;
    public List<ModuleHangerData> modules = new List<ModuleHangerData>();
}

[CreateAssetMenu(fileName = "DataTableModule", menuName = "Custom/DataTableModule")]
public class DataTableModule : ScriptableObject
{
    [Header("Body Modules by SubType")]
    [SerializeField] private List<ModuleBodySubTypeGroup> bodyGroups = new();

    [Header("Engine Modules by SubType")]
    [SerializeField] private List<ModuleEngineSubTypeGroup> engineGroups = new();

    [Header("Weapon Modules by SubType")]
    [SerializeField] private List<ModuleWeaponSubTypeGroup> weaponGroups = new();

    [Header("Hanger Modules by SubType")]
    [SerializeField] private List<ModuleHangerSubTypeGroup> hangerGroups = new();

    [Header("Export/Import")]
    [SerializeField, TextArea(5, 15)] private string exportedJson = "";

    public List<ModuleBodySubTypeGroup> BodyGroups => bodyGroups;
    public List<ModuleEngineSubTypeGroup> EngineGroups => engineGroups;
    public List<ModuleWeaponSubTypeGroup> WeaponGroups => weaponGroups;
    public List<ModuleHangerSubTypeGroup> HangerGroups => hangerGroups;

    public ModuleBodyDataList BodyModules
    {
        get
        {
            var list = new ModuleBodyDataList();
            foreach (var group in bodyGroups)
                foreach (var module in group.modules)
                    list.Add(module);
            return list;
        }
    }

    public ModuleEngineDataList EngineModules
    {
        get
        {
            var list = new ModuleEngineDataList();
            foreach (var group in engineGroups)
                foreach (var module in group.modules)
                    list.Add(module);
            return list;
        }
    }

    public ModuleWeaponDataList WeaponModules
    {
        get
        {
            var list = new ModuleWeaponDataList();
            foreach (var group in weaponGroups)
                foreach (var module in group.modules)
                    list.Add(module);
            return list;
        }
    }

    public ModuleHangerDataList HangerModules
    {
        get
        {
            var list = new ModuleHangerDataList();
            foreach (var group in hangerGroups)
                foreach (var module in group.modules)
                    list.Add(module);
            return list;
        }
    }


    #region Public Methods

    public void AddBodyModule(ModuleBodyData data, EModuleBodySubType subType)
    {
        var group = bodyGroups.Find(g => g.subType == subType);
        if (group == null)
        {
            group = new ModuleBodySubTypeGroup { subType = subType };
            bodyGroups.Add(group);
        }
        group.modules.Add(data);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public void AddEngineModule(ModuleEngineData data, EModuleEngineSubType subType)
    {
        var group = engineGroups.Find(g => g.subType == subType);
        if (group == null)
        {
            group = new ModuleEngineSubTypeGroup { subType = subType };
            engineGroups.Add(group);
        }
        group.modules.Add(data);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public void AddWeaponModule(ModuleWeaponData data, EModuleWeaponSubType subType)
    {
        var group = weaponGroups.Find(g => g.subType == subType);
        if (group == null)
        {
            group = new ModuleWeaponSubTypeGroup { subType = subType };
            weaponGroups.Add(group);
        }
        group.modules.Add(data);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public void AddHangerModule(ModuleHangerData data, EModuleHangerSubType subType)
    {
        var group = hangerGroups.Find(g => g.subType == subType);
        if (group == null)
        {
            group = new ModuleHangerSubTypeGroup { subType = subType };
            hangerGroups.Add(group);
        }
        group.modules.Add(data);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public ModuleBodyData GetBodyModule(EModuleBodySubType subType, int level)
    {
        var group = bodyGroups.Find(g => g.subType == subType);
        if (group == null) return null;
        return group.modules.Find(m => m.m_level == level);
    }

    public ModuleEngineData GetEngineModule(EModuleEngineSubType subType, int level)
    {
        var group = engineGroups.Find(g => g.subType == subType);
        if (group == null) return null;
        return group.modules.Find(m => m.m_level == level);
    }

    public ModuleWeaponData GetWeaponModule(EModuleWeaponSubType subType, int level)
    {
        var group = weaponGroups.Find(g => g.subType == subType);
        if (group == null) return null;
        return group.modules.Find(m => m.m_level == level);
    }

    public ModuleHangerData GetHangerModule(EModuleHangerSubType subType, int level)
    {
        var group = hangerGroups.Find(g => g.subType == subType);
        if (group == null) return null;
        return group.modules.Find(m => m.m_level == level);
    }

    public void InitializeSubTypeGroups()
    {
        if (bodyGroups.Count == 0)
        {
            foreach (EModuleBodySubType subType in System.Enum.GetValues(typeof(EModuleBodySubType)))
                if (subType != EModuleBodySubType.None)
                    bodyGroups.Add(new ModuleBodySubTypeGroup { subType = subType });
        }

        if (engineGroups.Count == 0)
        {
            foreach (EModuleEngineSubType subType in System.Enum.GetValues(typeof(EModuleEngineSubType)))
                if (subType != EModuleEngineSubType.None)
                    engineGroups.Add(new ModuleEngineSubTypeGroup { subType = subType });
        }

        if (weaponGroups.Count == 0)
        {
            foreach (EModuleWeaponSubType subType in System.Enum.GetValues(typeof(EModuleWeaponSubType)))
                if (subType != EModuleWeaponSubType.None)
                    weaponGroups.Add(new ModuleWeaponSubTypeGroup { subType = subType });
        }

        if (hangerGroups.Count == 0)
        {
            foreach (EModuleHangerSubType subType in System.Enum.GetValues(typeof(EModuleHangerSubType)))
                if (subType != EModuleHangerSubType.None)
                    hangerGroups.Add(new ModuleHangerSubTypeGroup { subType = subType });
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
                    var bodyList = modulesObj[bodyKey.ToString()].ToObject<List<ModuleBodyData>>();
                    foreach (var module in bodyList)
                        AddBodyModule(module, module.m_subType);
                }

                int engineKey = (int)EModuleType.Engine;
                if (modulesObj[engineKey.ToString()] != null)
                {
                    var engineList = modulesObj[engineKey.ToString()].ToObject<List<ModuleEngineData>>();
                    foreach (var module in engineList)
                        AddEngineModule(module, module.m_subType);
                }

                int weaponKey = (int)EModuleType.Weapon;
                if (modulesObj[weaponKey.ToString()] != null)
                {
                    var weaponList = modulesObj[weaponKey.ToString()].ToObject<List<ModuleWeaponData>>();
                    foreach (var module in weaponList)
                        AddWeaponModule(module, module.m_subType);
                }

                int hangerKey = (int)EModuleType.Hanger;
                if (modulesObj[hangerKey.ToString()] != null)
                {
                    var hangerList = modulesObj[hangerKey.ToString()].ToObject<List<ModuleHangerData>>();
                    foreach (var module in hangerList)
                        AddHangerModule(module, module.m_subType);
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
        InitializeSubTypeGroups();

        bodyGroups.Clear();
        weaponGroups.Clear();
        engineGroups.Clear();
        hangerGroups.Clear();
        InitializeSubTypeGroups();

        foreach (EModuleBodySubType subType in System.Enum.GetValues(typeof(EModuleBodySubType)))
        {
            if (subType == EModuleBodySubType.None) continue;

            for (int i = 1; i <= 10; i++)
            {
                var bodyModule = new ModuleBodyData
                {
                    m_name = $"{subType} Hull Lv.{i}",
                    m_subType = subType,
                    m_style = EModuleStyle.StyleA,
                    m_level = i,
                    m_health = 100f + (i * 50f),
                    m_cargoCapacity = 50f + (i * 25f),
                    m_upgradeCost = new CostStruct(i, 50 * i, 0, 0, 0),
                    m_description = $"{subType}-class hull module level {i}"
                };
                AddBodyModule(bodyModule, bodyModule.m_subType);
            }
        }

        foreach (EModuleWeaponSubType subType in System.Enum.GetValues(typeof(EModuleWeaponSubType)))
        {
            if (subType == EModuleWeaponSubType.None) continue;

            for (int i = 1; i <= 10; i++)
            {
                var weaponModule = new ModuleWeaponData
                {
                    m_name = $"{subType} Weapon Lv.{i}",
                    m_subType = subType,
                    m_style = EModuleStyle.StyleA,
                    m_level = i,
                    m_health = 30f + (i * 10f),
                    m_attackPower = 10f + (i * 5f),
                    m_attackFireCount = 1 + (i / 5),
                    m_attackCoolTime = 2.0f - (i * 0.05f),
                    //m_upgradeCost = new CostStruct(i, 50 * i, 0, 0, 0),
                    m_upgradeCost = new CostStruct(1, 50 * i, 0, 0, 0),
                    m_description = $"{subType} weapon system level {i}"
                };
                AddWeaponModule(weaponModule, weaponModule.m_subType);
            }
        }

        foreach (EModuleEngineSubType subType in System.Enum.GetValues(typeof(EModuleEngineSubType)))
        {
            if (subType == EModuleEngineSubType.None) continue;

            for (int i = 1; i <= 10; i++)
            {
                var engineModule = new ModuleEngineData
                {
                    m_name = $"{subType} Engine Lv.{i}",
                    m_subType = subType,
                    m_style = EModuleStyle.StyleA,
                    m_level = i,
                    m_health = 30f + (i * 10f),
                    m_movementSpeed = 3f + (i * 0.5f),
                    m_rotationSpeed = 2f + (i * 0.3f),
                    m_upgradeCost = new CostStruct(i, 50 * i, 0, 0, 0),
                    m_description = $"{subType} propulsion system level {i}"
                };
                AddEngineModule(engineModule, engineModule.m_subType);
            }
        }

        foreach (EModuleHangerSubType subType in System.Enum.GetValues(typeof(EModuleHangerSubType)))
        {
            if (subType == EModuleHangerSubType.None) continue;

            for (int i = 1; i <= 10; i++)
            {
                var hangerModule = new ModuleHangerData
                {
                    m_name = $"{subType} Hanger Lv.{i}",
                    m_subType = subType,
                    m_style = EModuleStyle.StyleA,
                    m_level = i,
                    m_health = 40f + (i * 15f),
                    m_hangarCapability = 2 + (i * 3),
                    m_scoutCapability = 1 + (i * 2),
                    m_launchCool = 3.0f - (i * 0.15f),
                    m_launchCount = 1 + (i / 4),
                    m_maintenanceTime = 15.0f - (i * 0.5f),
                    m_aircraftHealth = 30f + (i * 10f),
                    m_aircraftAttackPower = 5f + (i * 3f),
                    m_aircraftAmmo = 10 + (i * 2),
                    m_upgradeCost = new CostStruct(i, 50 * i, 0, 0, 0),
                    m_description = $"{subType} hanger bay level {i}"
                };
                AddHangerModule(hangerModule, hangerModule.m_subType);
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