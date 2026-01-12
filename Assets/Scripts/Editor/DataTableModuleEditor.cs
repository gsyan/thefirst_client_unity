
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(DataTableModule))]
public class DataTableModuleEditor : Editor
{
    private DataTableModule dataTableModule;
    private Vector2 scrollPosition;

    private Dictionary<EModuleSubType, bool> bodySubTypeFoldouts = new Dictionary<EModuleSubType, bool>();
    private Dictionary<EModuleSubType, bool> engineSubTypeFoldouts = new Dictionary<EModuleSubType, bool>();
    private Dictionary<EModuleSubType, bool> weaponSubTypeFoldouts = new Dictionary<EModuleSubType, bool>();
    private Dictionary<EModuleSubType, bool> hangerSubTypeFoldouts = new Dictionary<EModuleSubType, bool>();

    private bool showBodyModules = false;
    private bool showEngineModules = false;
    private bool showWeaponModules = false;
    private bool showHangerModules = false;
    private bool showUtilityTools = true;
    private bool showJsonTools = true;

    private readonly Color bodyColor = new Color(0.7f, 0.9f, 0.7f);
    private readonly Color engineColor = new Color(0.7f, 0.7f, 0.9f);
    private readonly Color weaponColor = new Color(0.9f, 0.7f, 0.7f);
    private readonly Color hangerColor = new Color(0.9f, 0.9f, 0.7f);

    private void OnEnable()
    {
        dataTableModule = (DataTableModule)target;
        // InitializeSubTypeGroups()를 여기서 호출하면 안됨!
        // OnEnable은 Inspector에서 asset 선택할 때마다 호출되어
        // 빈 그룹이 계속 추가됨
        // Generate 버튼에서만 호출해야 함
    }

    public override void OnInspectorGUI()
    {
        if (dataTableModule == null) return;

        serializedObject.Update();

        EditorGUILayout.Space(5);
        DataTableModuleEditorDrawHeader();
        EditorGUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawBodyModuleSection();
        DrawEngineModuleSection();
        DrawWeaponModuleSection();
        DrawHangerModuleSection();

        EditorGUILayout.Space(20);
        DrawUtilityTools();
        EditorGUILayout.Space(10);
        DrawJsonTools();

        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(dataTableModule);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DataTableModuleEditorDrawHeader()
    {
        EditorGUILayout.BeginHorizontal("box");
        GUILayout.Label("Data Table Module", EditorStyles.largeLabel);
        GUILayout.FlexibleSpace();

        int totalModules = dataTableModule.BodyModules.Count + dataTableModule.EngineModules.Count + dataTableModule.WeaponModules.Count + dataTableModule.HangerModules.Count;
        GUILayout.Label($"Total: {totalModules}", EditorStyles.miniLabel);

        EditorGUILayout.EndHorizontal();
    }

    #region Body Modules
    private void DrawBodyModuleSection()
    {
        EditorGUILayout.BeginVertical("box");

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = bodyColor;
        showBodyModules = EditorGUILayout.Foldout(showBodyModules, $"Body Modules ({dataTableModule.BodyModules.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (showBodyModules)
        {
            foreach (var group in dataTableModule.BodyGroups)
            {
                DrawBodySubTypeGroup(group);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBodySubTypeGroup(ModuleSubTypeGroup group)
    {
        if (!bodySubTypeFoldouts.ContainsKey(group.subType))
            bodySubTypeFoldouts[group.subType] = false;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();

        bodySubTypeFoldouts[group.subType] = EditorGUILayout.Foldout(bodySubTypeFoldouts[group.subType], $"{group.subType} ({group.modules.Count})", true);

        if (GUILayout.Button("Add", GUILayout.Width(50)))
        {
            EModuleType moduleType = CommonUtility.GetModuleTypeFromSubType(group.subType);
            var moduleData = new ModuleData
            {
                m_moduleName = $"{group.subType} LV.{group.modules.Count + 1}",
                m_moduleType = moduleType,
                m_moduleSubType = group.subType,
                m_moduleStyle = EModuleStyle.None,
                m_moduleLevel = group.modules.Count + 1,
                m_health = 200f,
                m_cargoCapacity = 100f,
                m_description = $"{group.subType} LV.{group.modules.Count + 1}"
            };
            group.modules.Add(moduleData);
            EditorUtility.SetDirty(dataTableModule);
        }

        EditorGUILayout.EndHorizontal();

        if (bodySubTypeFoldouts[group.subType])
        {
            for (int i = 0; i < group.modules.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");
                DrawBodyModuleDetails(group.modules[i], group, i);
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBodyModuleDetails(ModuleData module, ModuleSubTypeGroup group, int index)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Level {module.m_moduleLevel}", EditorStyles.boldLabel, GUILayout.Width(80));

        if (GUILayout.Button("Remove", GUILayout.Width(70)))
        {
            group.modules.RemoveAt(index);
            EditorUtility.SetDirty(dataTableModule);
            return;
        }
        EditorGUILayout.EndHorizontal();

        module.m_moduleName = EditorGUILayout.TextField("Name", module.m_moduleName);
        module.m_moduleStyle = (EModuleStyle)EditorGUILayout.EnumPopup("Style", module.m_moduleStyle);
        module.m_moduleLevel = EditorGUILayout.IntSlider("Level", module.m_moduleLevel, 1, 10);

        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.m_health = EditorGUILayout.Slider("Health", module.m_health, 1f, 1000f);
        module.m_cargoCapacity = EditorGUILayout.Slider("Cargo", module.m_cargoCapacity, 0f, 1000f);

        EditorGUILayout.LabelField("Upgrade Cost", EditorStyles.boldLabel);
        module.m_upgradeCost.techLevel = EditorGUILayout.IntField("TechLevel", module.m_upgradeCost.techLevel);
        module.m_upgradeCost.mineral = EditorGUILayout.LongField("Mineral", module.m_upgradeCost.mineral);
        module.m_upgradeCost.mineralRare = EditorGUILayout.LongField("MineralRare", module.m_upgradeCost.mineralRare);
        module.m_upgradeCost.mineralExotic = EditorGUILayout.LongField("MineralExotic", module.m_upgradeCost.mineralExotic);
        module.m_upgradeCost.mineralDark = EditorGUILayout.LongField("MineralDark", module.m_upgradeCost.mineralDark);

        module.m_description = EditorGUILayout.TextField("Description", module.m_description);
    }
    #endregion

    #region Weapon Modules
    private void DrawWeaponModuleSection()
    {
        EditorGUILayout.BeginVertical("box");

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = weaponColor;
        showWeaponModules = EditorGUILayout.Foldout(showWeaponModules, $"Weapon Modules ({dataTableModule.WeaponModules.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (showWeaponModules)
        {
            foreach (var group in dataTableModule.WeaponGroups)
            {
                DrawWeaponSubTypeGroup(group);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawWeaponSubTypeGroup(ModuleSubTypeGroup group)
    {
        if (!weaponSubTypeFoldouts.ContainsKey(group.subType))
            weaponSubTypeFoldouts[group.subType] = false;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();

        weaponSubTypeFoldouts[group.subType] = EditorGUILayout.Foldout(weaponSubTypeFoldouts[group.subType], $"{group.subType} ({group.modules.Count})", true);

        if (GUILayout.Button("Add", GUILayout.Width(50)))
        {
            EModuleType moduleType = CommonUtility.GetModuleTypeFromSubType(group.subType);
            var module = new ModuleData
            {
                m_moduleName = $"{group.subType} LV{group.modules.Count + 1}",
                m_moduleType = moduleType,
                m_moduleSubType = group.subType,
                m_moduleStyle = EModuleStyle.None,
                m_moduleLevel = group.modules.Count + 1,
                m_health = 50f,
                m_attackPower = 25f,
                m_attackFireCount = 1,
                m_attackCoolTime = 2f,
                m_description = $"{group.subType} LV{group.modules.Count + 1}"
            };
            group.modules.Add(module);
            EditorUtility.SetDirty(dataTableModule);
        }

        EditorGUILayout.EndHorizontal();

        if (weaponSubTypeFoldouts[group.subType])
        {
            for (int i = 0; i < group.modules.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");
                DrawWeaponModuleDetails(group.modules[i], group, i);
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawWeaponModuleDetails(ModuleData module, ModuleSubTypeGroup group, int index)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Level {module.m_moduleLevel}", EditorStyles.boldLabel, GUILayout.Width(80));

        if (GUILayout.Button("Remove", GUILayout.Width(70)))
        {
            group.modules.RemoveAt(index);
            EditorUtility.SetDirty(dataTableModule);
            return;
        }
        EditorGUILayout.EndHorizontal();

        module.m_moduleName = EditorGUILayout.TextField("Name", module.m_moduleName);
        module.m_moduleStyle = (EModuleStyle)EditorGUILayout.EnumPopup("Style", module.m_moduleStyle);
        module.m_moduleLevel = EditorGUILayout.IntSlider("Level", module.m_moduleLevel, 1, 10);

        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.m_health = EditorGUILayout.Slider("Health", module.m_health, 1f, 1000f);
        module.m_attackPower = EditorGUILayout.Slider("Attack Power", module.m_attackPower, 0f, 100f);
        module.m_attackFireCount = EditorGUILayout.IntSlider("Fire Count", module.m_attackFireCount, 0, 100);
        module.m_attackCoolTime = EditorGUILayout.Slider("Cool Time", module.m_attackCoolTime, 0.1f, 10f);

        EditorGUILayout.LabelField("Projectile Stats", EditorStyles.boldLabel);
        module.m_projectileLength = EditorGUILayout.Slider("Projectile Length", module.m_projectileLength, 1f, 100f);
        module.m_projectileWidth = EditorGUILayout.Slider("Projectile Width", module.m_projectileWidth, 0.01f, 5f);
        module.m_projectileSpeed = EditorGUILayout.Slider("Projectile Speed", module.m_projectileSpeed, 1f, 500f);

        EditorGUILayout.LabelField("Upgrade Cost", EditorStyles.boldLabel);
        module.m_upgradeCost.techLevel = EditorGUILayout.IntField("TechLevel", module.m_upgradeCost.techLevel);
        module.m_upgradeCost.mineral = EditorGUILayout.LongField("Mineral", module.m_upgradeCost.mineral);
        module.m_upgradeCost.mineralRare = EditorGUILayout.LongField("MineralRare", module.m_upgradeCost.mineralRare);
        module.m_upgradeCost.mineralExotic = EditorGUILayout.LongField("MineralExotic", module.m_upgradeCost.mineralExotic);
        module.m_upgradeCost.mineralDark = EditorGUILayout.LongField("MineralDark", module.m_upgradeCost.mineralDark);

        module.m_description = EditorGUILayout.TextField("Description", module.m_description);
    }
    #endregion

    #region Engine Modules
    private void DrawEngineModuleSection()
    {
        EditorGUILayout.BeginVertical("box");

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = engineColor;
        showEngineModules = EditorGUILayout.Foldout(showEngineModules, $"Engine Modules ({dataTableModule.EngineModules.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (showEngineModules)
        {
            foreach (var group in dataTableModule.EngineGroups)
            {
                DrawEngineSubTypeGroup(group);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawEngineSubTypeGroup(ModuleSubTypeGroup group)
    {
        if (!engineSubTypeFoldouts.ContainsKey(group.subType))
            engineSubTypeFoldouts[group.subType] = false;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();

        engineSubTypeFoldouts[group.subType] = EditorGUILayout.Foldout(engineSubTypeFoldouts[group.subType], $"{group.subType} ({group.modules.Count})", true);

        if (GUILayout.Button("Add", GUILayout.Width(50)))
        {
            EModuleType moduleType = CommonUtility.GetModuleTypeFromSubType(group.subType);
            var module = new ModuleData
            {
                m_moduleName = $"{group.subType} LV{group.modules.Count + 1}",
                m_moduleType = moduleType,
                m_moduleSubType = group.subType,
                m_moduleStyle = EModuleStyle.None,
                m_moduleLevel = group.modules.Count + 1,
                m_health = 50f,
                m_movementSpeed = 5f,
                m_description = $"{group.subType} LV{group.modules.Count + 1}"
            };
            group.modules.Add(module);
            EditorUtility.SetDirty(dataTableModule);
        }

        EditorGUILayout.EndHorizontal();

        if (engineSubTypeFoldouts[group.subType])
        {
            for (int i = 0; i < group.modules.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");
                DrawEngineModuleDetails(group.modules[i], group, i);
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawEngineModuleDetails(ModuleData module, ModuleSubTypeGroup group, int index)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Level {module.m_moduleLevel}", EditorStyles.boldLabel, GUILayout.Width(80));

        if (GUILayout.Button("Remove", GUILayout.Width(70)))
        {
            group.modules.RemoveAt(index);
            EditorUtility.SetDirty(dataTableModule);
            return;
        }
        EditorGUILayout.EndHorizontal();

        module.m_moduleName = EditorGUILayout.TextField("Name", module.m_moduleName);
        module.m_moduleStyle = (EModuleStyle)EditorGUILayout.EnumPopup("Style", module.m_moduleStyle);
        module.m_moduleLevel = EditorGUILayout.IntSlider("Level", module.m_moduleLevel, 1, 10);

        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.m_health = EditorGUILayout.Slider("Health", module.m_health, 1f, 1000f);
        module.m_movementSpeed = EditorGUILayout.Slider("Movement Speed", module.m_movementSpeed, 0f, 20f);
        
        EditorGUILayout.LabelField("Upgrade Cost", EditorStyles.boldLabel);
        module.m_upgradeCost.techLevel = EditorGUILayout.IntField("TechLevel", module.m_upgradeCost.techLevel);
        module.m_upgradeCost.mineral = EditorGUILayout.LongField("Mineral", module.m_upgradeCost.mineral);
        module.m_upgradeCost.mineralRare = EditorGUILayout.LongField("MineralRare", module.m_upgradeCost.mineralRare);
        module.m_upgradeCost.mineralExotic = EditorGUILayout.LongField("MineralExotic", module.m_upgradeCost.mineralExotic);
        module.m_upgradeCost.mineralDark = EditorGUILayout.LongField("MineralDark", module.m_upgradeCost.mineralDark);

        module.m_description = EditorGUILayout.TextField("Description", module.m_description);
    }
    #endregion

    #region Hanger Modules
    private void DrawHangerModuleSection()
    {
        EditorGUILayout.BeginVertical("box");

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = hangerColor;
        showHangerModules = EditorGUILayout.Foldout(showHangerModules, $"Hanger Modules ({dataTableModule.HangerModules.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (showHangerModules)
        {
            foreach (var group in dataTableModule.HangerGroups)
            {
                DrawHangerSubTypeGroup(group);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawHangerSubTypeGroup(ModuleSubTypeGroup group)
    {
        if (!hangerSubTypeFoldouts.ContainsKey(group.subType))
            hangerSubTypeFoldouts[group.subType] = false;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();

        hangerSubTypeFoldouts[group.subType] = EditorGUILayout.Foldout(hangerSubTypeFoldouts[group.subType], $"{group.subType} ({group.modules.Count})", true);

        if (GUILayout.Button("Add", GUILayout.Width(50)))
        {
            EModuleType moduleType = CommonUtility.GetModuleTypeFromSubType(group.subType);
            var module = new ModuleData
            {
                m_moduleName = $"{group.subType} LV{group.modules.Count + 1}",
                m_moduleType = moduleType,
                m_moduleSubType = group.subType,
                m_moduleStyle = EModuleStyle.None,
                m_moduleLevel = group.modules.Count + 1,
                m_health = 50f,
                m_hangarCapability = 5,
                m_scoutCapability = 5,
                m_launchCool = 1f,
                m_launchCount = 1,
                m_maintenanceTime = 10f,
                m_aircraftLaunchStraightDistance = 100f,
                m_aircraftHealth = 50f,
                m_aircraftAttackPower = 10f,
                m_aircraftAttackRange = 100f,
                m_aircraftAttackCooldown = 10f,
                m_aircraftDetectionRadius = 200f,
                m_aircraftAvoidanceRadius = 200f,
                m_description = $"{group.subType} LV{group.modules.Count + 1}"
            };
            group.modules.Add(module);
            EditorUtility.SetDirty(dataTableModule);
        }

        EditorGUILayout.EndHorizontal();

        if (hangerSubTypeFoldouts[group.subType])
        {
            for (int i = 0; i < group.modules.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");
                DrawHangerModuleDetails(group.modules[i], group, i);
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawHangerModuleDetails(ModuleData module, ModuleSubTypeGroup group, int index)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Level {module.m_moduleLevel}", EditorStyles.boldLabel, GUILayout.Width(80));

        if (GUILayout.Button("Remove", GUILayout.Width(70)))
        {
            group.modules.RemoveAt(index);
            EditorUtility.SetDirty(dataTableModule);
            return;
        }
        EditorGUILayout.EndHorizontal();

        module.m_moduleName = EditorGUILayout.TextField("Name", module.m_moduleName);
        module.m_moduleStyle = (EModuleStyle)EditorGUILayout.EnumPopup("Style", module.m_moduleStyle);
        module.m_moduleLevel = EditorGUILayout.IntSlider("Level", module.m_moduleLevel, 1, 10);

        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.m_health = EditorGUILayout.Slider("Health", module.m_health, 1f, 1000f);
        module.m_hangarCapability = EditorGUILayout.IntSlider("Hangar Capability", module.m_hangarCapability, 0, 1000);
        module.m_scoutCapability = EditorGUILayout.IntSlider("Scout Capability", module.m_scoutCapability, 0, 1000);
        module.m_launchCool = EditorGUILayout.Slider("Launch Cool", module.m_launchCool, 0f, 10f);
        module.m_launchCount = EditorGUILayout.IntSlider("Launch Count", module.m_launchCount, 0, 10);
        module.m_maintenanceTime = EditorGUILayout.Slider("Maintenance Time", module.m_maintenanceTime, 0f, 1000f);

        EditorGUILayout.LabelField("Aircraft Stats", EditorStyles.boldLabel);
        module.m_aircraftLaunchStraightDistance = EditorGUILayout.Slider("Aircraft Launch Straight Distance", module.m_aircraftLaunchStraightDistance, 1f, 1000f);
        module.m_aircraftHealth = EditorGUILayout.Slider("Aircraft Health", module.m_aircraftHealth, 1f, 1000f);
        module.m_aircraftAttackPower = EditorGUILayout.Slider("Aircraft Attack Power", module.m_aircraftAttackPower, 1f, 1000f);
        module.m_aircraftAttackRange = EditorGUILayout.Slider("Aircraft Attack Range", module.m_aircraftAttackRange, 1f, 1000f);
        module.m_aircraftAttackCooldown = EditorGUILayout.Slider("Aircraft Attack Cooldown", module.m_aircraftAttackCooldown, 1f, 1000f);
        module.m_aircraftSpeed = EditorGUILayout.Slider("Aircraft Speed", module.m_aircraftSpeed, 1f, 1000f);
        module.m_aircraftAmmo = EditorGUILayout.IntSlider("Aircraft Ammo", module.m_aircraftAmmo, 1, 100);
        module.m_aircraftDetectionRadius = EditorGUILayout.Slider("Aircraft Detection Radius", module.m_aircraftDetectionRadius, 1f, 1000f);
        module.m_aircraftAvoidanceRadius = EditorGUILayout.Slider("Aircraft Avoidance Radius", module.m_aircraftAvoidanceRadius, 1f, 1000f);

        EditorGUILayout.LabelField("Upgrade Cost", EditorStyles.boldLabel);
        module.m_upgradeCost.techLevel = EditorGUILayout.IntField("TechLevel", module.m_upgradeCost.techLevel);
        module.m_upgradeCost.mineral = EditorGUILayout.LongField("Mineral", module.m_upgradeCost.mineral);
        module.m_upgradeCost.mineralRare = EditorGUILayout.LongField("MineralRare", module.m_upgradeCost.mineralRare);
        module.m_upgradeCost.mineralExotic = EditorGUILayout.LongField("MineralExotic", module.m_upgradeCost.mineralExotic);
        module.m_upgradeCost.mineralDark = EditorGUILayout.LongField("MineralDark", module.m_upgradeCost.mineralDark);

        module.m_description = EditorGUILayout.TextField("Description", module.m_description);
    }
    #endregion

    #region Utility Tools
    private void DrawUtilityTools()
    {
        EditorGUILayout.BeginVertical("box");
        showUtilityTools = EditorGUILayout.Foldout(showUtilityTools, "Utility Tools", true, EditorStyles.foldoutHeader);

        if (showUtilityTools)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate Lv.1~10 Data"))
            {
                if (EditorUtility.DisplayDialog("Generate Data",
                    "Generate all module types with Level 1~10 data.\n\n" +
                    "Continue?", "Yes", "Cancel"))
                {
                    dataTableModule.GenerateLevel1to10Data();
                    EditorUtility.DisplayDialog("Complete", "Level 1~10 data generated successfully!", "OK");
                }
            }

            if (GUILayout.Button("Validate Data"))
            {
                bool isValid = dataTableModule.ValidateData();
                EditorUtility.DisplayDialog("Validation", isValid ? "Data is valid!" : "Data validation failed. Check console.", "OK");
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }
    #endregion

    #region JSON Tools
    private void DrawJsonTools()
    {
        EditorGUILayout.BeginVertical("box");
        showJsonTools = EditorGUILayout.Foldout(showJsonTools, "JSON Import/Export", true, EditorStyles.foldoutHeader);

        if (showJsonTools)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Export to JSON"))
            {
                string json = dataTableModule.ExportToJson();
                string path = EditorUtility.SaveFilePanel("Export Module Data", "", "DataTableModule.json", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    System.IO.File.WriteAllText(path, json);
                    EditorUtility.DisplayDialog("Export", "Module data exported successfully!", "OK");
                }
            }

            if (GUILayout.Button("Import from JSON"))
            {
                string path = EditorUtility.OpenFilePanel("Import Module Data", "", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    dataTableModule.ImportFromJson(json);
                    EditorUtility.DisplayDialog("Import", "Module data imported successfully!", "OK");
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }
    #endregion
}
#endif
