
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
    private Dictionary<EModuleSubType, bool> beamSubTypeFoldouts = new Dictionary<EModuleSubType, bool>();
    private Dictionary<EModuleSubType, bool> missileSubTypeFoldouts = new Dictionary<EModuleSubType, bool>();
    private Dictionary<EModuleSubType, bool> hangerSubTypeFoldouts = new Dictionary<EModuleSubType, bool>();
    private Dictionary<ModuleData, bool> moduleSlotFoldouts = new Dictionary<ModuleData, bool>();

    private bool showBodyModules = false;
    private bool showEngineModules = false;
    private bool showBeamModules = false;
    private bool showMissileModules = false;
    private bool showHangerModules = false;
    private bool showUtilityTools = true;
    private bool showJsonTools = true;

    private readonly Color bodyColor = new Color(0.7f, 0.9f, 0.7f);
    private readonly Color engineColor = new Color(0.7f, 0.7f, 0.9f);
    private readonly Color beamColor = new Color(0.9f, 0.7f, 0.7f);
    private readonly Color missileColor = new Color(0.9f, 0.7f, 0.7f);
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
        DrawBeamModuleSection();
        DrawMissileModuleSection();
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

        int totalModules = dataTableModule.BodyModules.Count + dataTableModule.EngineModules.Count + dataTableModule.BeamModules.Count + dataTableModule.MissileModules.Count + dataTableModule.HangerModules.Count;
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
                moduleName = $"{group.subType} LV.{group.modules.Count + 1}",
                moduleType = moduleType,
                moduleSubType = group.subType,
                moduleLevel = group.modules.Count + 1,
                health = 200f,
                repairPower = 1f,
                description = $"{group.subType} LV.{group.modules.Count + 1}"
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
        EditorGUILayout.LabelField($"Level {module.moduleLevel}", EditorStyles.boldLabel, GUILayout.Width(80));

        if (GUILayout.Button("Remove", GUILayout.Width(70)))
        {
            group.modules.RemoveAt(index);
            EditorUtility.SetDirty(dataTableModule);
            return;
        }
        EditorGUILayout.EndHorizontal();

        module.moduleName = EditorGUILayout.TextField("Name", module.moduleName);
        module.moduleLevel = EditorGUILayout.IntSlider("Level", module.moduleLevel, 1, 10);

        // Module Slots (from prefab)
        int slotCount = module.moduleSlots != null ? module.moduleSlots.Length : 0;
        if (!moduleSlotFoldouts.ContainsKey(module))
            moduleSlotFoldouts[module] = false;

        moduleSlotFoldouts[module] = EditorGUILayout.Foldout(moduleSlotFoldouts[module], $"Module Slots ({slotCount})", true);
        if (moduleSlotFoldouts[module] && module.moduleSlots != null)
        {
            EditorGUI.indentLevel++;
            foreach (var slot in module.moduleSlots)
            {
                EditorGUILayout.LabelField($"{slot.moduleType} / Slot:{slot.slotIndex}");
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.health = EditorGUILayout.Slider("Health", module.health, 1f, 1000f);
        module.repairPower = EditorGUILayout.Slider("Repair", module.repairPower, 0f, 1000f);
        
        EditorGUILayout.LabelField("Upgrade Cost", EditorStyles.boldLabel);
        module.upgradeCost.techLevel = EditorGUILayout.IntField("TechLevel", module.upgradeCost.techLevel);
        module.upgradeCost.mineral = EditorGUILayout.LongField("Mineral", module.upgradeCost.mineral);
        module.upgradeCost.mineralRare = EditorGUILayout.LongField("MineralRare", module.upgradeCost.mineralRare);
        module.upgradeCost.mineralExotic = EditorGUILayout.LongField("MineralExotic", module.upgradeCost.mineralExotic);
        module.upgradeCost.mineralDark = EditorGUILayout.LongField("MineralDark", module.upgradeCost.mineralDark);

        module.description = EditorGUILayout.TextField("Description", module.description);
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
                moduleName = $"{group.subType} LV{group.modules.Count + 1}",
                moduleType = moduleType,
                moduleSubType = group.subType,
                moduleLevel = group.modules.Count + 1,
                health = 0f,
                speed = 5f,
                description = $"{group.subType} LV{group.modules.Count + 1}"
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
        EditorGUILayout.LabelField($"Level {module.moduleLevel}", EditorStyles.boldLabel, GUILayout.Width(80));

        if (GUILayout.Button("Remove", GUILayout.Width(70)))
        {
            group.modules.RemoveAt(index);
            EditorUtility.SetDirty(dataTableModule);
            return;
        }
        EditorGUILayout.EndHorizontal();

        module.moduleName = EditorGUILayout.TextField("Name", module.moduleName);
        module.moduleLevel = EditorGUILayout.IntSlider("Level", module.moduleLevel, 1, 10);

        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.health = EditorGUILayout.Slider("Health", module.health, 0f, 1000f);
        module.speed = EditorGUILayout.Slider("Movement Speed", module.speed, 0f, 20f);
        
        EditorGUILayout.LabelField("Upgrade Cost", EditorStyles.boldLabel);
        module.upgradeCost.techLevel = EditorGUILayout.IntField("TechLevel", module.upgradeCost.techLevel);
        module.upgradeCost.mineral = EditorGUILayout.LongField("Mineral", module.upgradeCost.mineral);
        module.upgradeCost.mineralRare = EditorGUILayout.LongField("MineralRare", module.upgradeCost.mineralRare);
        module.upgradeCost.mineralExotic = EditorGUILayout.LongField("MineralExotic", module.upgradeCost.mineralExotic);
        module.upgradeCost.mineralDark = EditorGUILayout.LongField("MineralDark", module.upgradeCost.mineralDark);

        module.description = EditorGUILayout.TextField("Description", module.description);
    }
    #endregion

    #region Beam Modules
    private void DrawBeamModuleSection()
    {
        EditorGUILayout.BeginVertical("box");

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = beamColor;
        showBeamModules = EditorGUILayout.Foldout(showBeamModules, $"Beam Modules ({dataTableModule.BeamModules.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (showBeamModules)
        {
            foreach (var group in dataTableModule.BeamGroups)
            {
                DrawBeamSubTypeGroup(group);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBeamSubTypeGroup(ModuleSubTypeGroup group)
    {
        if (!beamSubTypeFoldouts.ContainsKey(group.subType))
            beamSubTypeFoldouts[group.subType] = false;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();

        beamSubTypeFoldouts[group.subType] = EditorGUILayout.Foldout(beamSubTypeFoldouts[group.subType], $"{group.subType} ({group.modules.Count})", true);

        if (GUILayout.Button("Add", GUILayout.Width(50)))
        {
            EModuleType moduleType = CommonUtility.GetModuleTypeFromSubType(group.subType);
            var module = new ModuleData
            {
                moduleName = $"{group.subType} LV{group.modules.Count + 1}",
                moduleType = moduleType,
                moduleSubType = group.subType,
                moduleLevel = group.modules.Count + 1,
                health = 0f,
                attackPower = 25f,
                attackFireCount = 1,
                attackCoolTime = 2f,
                description = $"{group.subType} LV{group.modules.Count + 1}"
            };
            group.modules.Add(module);
            EditorUtility.SetDirty(dataTableModule);
        }

        EditorGUILayout.EndHorizontal();

        if (beamSubTypeFoldouts[group.subType])
        {
            for (int i = 0; i < group.modules.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");
                DrawBeamModuleDetails(group.modules[i], group, i);
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBeamModuleDetails(ModuleData module, ModuleSubTypeGroup group, int index)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Level {module.moduleLevel}", EditorStyles.boldLabel, GUILayout.Width(80));

        if (GUILayout.Button("Remove", GUILayout.Width(70)))
        {
            group.modules.RemoveAt(index);
            EditorUtility.SetDirty(dataTableModule);
            return;
        }
        EditorGUILayout.EndHorizontal();

        module.moduleName = EditorGUILayout.TextField("Name", module.moduleName);
        module.moduleLevel = EditorGUILayout.IntSlider("Level", module.moduleLevel, 1, 10);

        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.health = EditorGUILayout.Slider("Health", module.health, 0f, 1000f);
        module.attackPower = EditorGUILayout.Slider("Attack Power", module.attackPower, 0f, 100f);
        module.attackFireCount = EditorGUILayout.IntSlider("Fire Count", module.attackFireCount, 0, 100);
        module.attackCoolTime = EditorGUILayout.Slider("Cool Time", module.attackCoolTime, 0.1f, 10f);

        EditorGUILayout.LabelField("Projectile Stats", EditorStyles.boldLabel);
        module.projectileWidth = EditorGUILayout.Slider("Projectile Width", module.projectileWidth, 0.01f, 5f);
        module.projectileSpeed = EditorGUILayout.Slider("Projectile Speed", module.projectileSpeed, 1f, 5000f);

        EditorGUILayout.LabelField("Upgrade Cost", EditorStyles.boldLabel);
        module.upgradeCost.techLevel = EditorGUILayout.IntField("TechLevel", module.upgradeCost.techLevel);
        module.upgradeCost.mineral = EditorGUILayout.LongField("Mineral", module.upgradeCost.mineral);
        module.upgradeCost.mineralRare = EditorGUILayout.LongField("MineralRare", module.upgradeCost.mineralRare);
        module.upgradeCost.mineralExotic = EditorGUILayout.LongField("MineralExotic", module.upgradeCost.mineralExotic);
        module.upgradeCost.mineralDark = EditorGUILayout.LongField("MineralDark", module.upgradeCost.mineralDark);

        module.description = EditorGUILayout.TextField("Description", module.description);
    }
    #endregion

    #region Missile Modules
    private void DrawMissileModuleSection()
    {
        EditorGUILayout.BeginVertical("box");

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = missileColor;
        showMissileModules = EditorGUILayout.Foldout(showMissileModules, $"Missile Modules ({dataTableModule.MissileModules.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (showMissileModules)
        {
            foreach (var group in dataTableModule.MissileGroups)
            {
                DrawMissileSubTypeGroup(group);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawMissileSubTypeGroup(ModuleSubTypeGroup group)
    {
        if (!missileSubTypeFoldouts.ContainsKey(group.subType))
            missileSubTypeFoldouts[group.subType] = false;

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();

        missileSubTypeFoldouts[group.subType] = EditorGUILayout.Foldout(missileSubTypeFoldouts[group.subType], $"{group.subType} ({group.modules.Count})", true);

        if (GUILayout.Button("Add", GUILayout.Width(50)))
        {
            EModuleType moduleType = CommonUtility.GetModuleTypeFromSubType(group.subType);
            var module = new ModuleData
            {
                moduleName = $"{group.subType} LV{group.modules.Count + 1}",
                moduleType = moduleType,
                moduleSubType = group.subType,
                moduleLevel = group.modules.Count + 1,
                health = 00f,
                attackPower = 25f,
                attackFireCount = 1,
                attackCoolTime = 2f,
                description = $"{group.subType} LV{group.modules.Count + 1}"
            };
            group.modules.Add(module);
            EditorUtility.SetDirty(dataTableModule);
        }

        EditorGUILayout.EndHorizontal();

        if (missileSubTypeFoldouts[group.subType])
        {
            for (int i = 0; i < group.modules.Count; i++)
            {
                EditorGUILayout.BeginVertical("box");
                DrawMissileModuleDetails(group.modules[i], group, i);
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawMissileModuleDetails(ModuleData module, ModuleSubTypeGroup group, int index)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Level {module.moduleLevel}", EditorStyles.boldLabel, GUILayout.Width(80));

        if (GUILayout.Button("Remove", GUILayout.Width(70)))
        {
            group.modules.RemoveAt(index);
            EditorUtility.SetDirty(dataTableModule);
            return;
        }
        EditorGUILayout.EndHorizontal();

        module.moduleName = EditorGUILayout.TextField("Name", module.moduleName);
        module.moduleLevel = EditorGUILayout.IntSlider("Level", module.moduleLevel, 1, 10);

        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.health = EditorGUILayout.Slider("Health", module.health, 0f, 1000f);
        module.attackPower = EditorGUILayout.Slider("Attack Power", module.attackPower, 0f, 100f);
        module.attackFireCount = EditorGUILayout.IntSlider("Fire Count", module.attackFireCount, 0, 100);
        module.attackCoolTime = EditorGUILayout.Slider("Cool Time", module.attackCoolTime, 0.1f, 10f);

        EditorGUILayout.LabelField("Projectile Stats", EditorStyles.boldLabel);
        module.projectileWidth = EditorGUILayout.Slider("Projectile Width", module.projectileWidth, 0.01f, 5f);
        module.projectileSpeed = EditorGUILayout.Slider("Projectile Speed", module.projectileSpeed, 1f, 5000f);

        EditorGUILayout.LabelField("Upgrade Cost", EditorStyles.boldLabel);
        module.upgradeCost.techLevel = EditorGUILayout.IntField("TechLevel", module.upgradeCost.techLevel);
        module.upgradeCost.mineral = EditorGUILayout.LongField("Mineral", module.upgradeCost.mineral);
        module.upgradeCost.mineralRare = EditorGUILayout.LongField("MineralRare", module.upgradeCost.mineralRare);
        module.upgradeCost.mineralExotic = EditorGUILayout.LongField("MineralExotic", module.upgradeCost.mineralExotic);
        module.upgradeCost.mineralDark = EditorGUILayout.LongField("MineralDark", module.upgradeCost.mineralDark);

        module.description = EditorGUILayout.TextField("Description", module.description);
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
                moduleName = $"{group.subType} LV{group.modules.Count + 1}",
                moduleType = moduleType,
                moduleSubType = group.subType,
                moduleLevel = group.modules.Count + 1,
                health = 0f,
                airCount = 5,
                attackCoolTime = 1f,
                attackFireCount = 1,
                maintenanceTime = 10f,
                aircraftLaunchStraightDistance = 100f,
                aircraftHealth = 50f,
                aircraftAttackPower = 10f,
                aircraftAttackRange = 100f,
                aircraftAttackCooldown = 10f,
                aircraftDetectionRadius = 200f,
                aircraftAvoidanceRadius = 200f,
                description = $"{group.subType} LV{group.modules.Count + 1}"
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
        EditorGUILayout.LabelField($"Level {module.moduleLevel}", EditorStyles.boldLabel, GUILayout.Width(80));

        if (GUILayout.Button("Remove", GUILayout.Width(70)))
        {
            group.modules.RemoveAt(index);
            EditorUtility.SetDirty(dataTableModule);
            return;
        }
        EditorGUILayout.EndHorizontal();

        module.moduleName = EditorGUILayout.TextField("Name", module.moduleName);
        module.moduleLevel = EditorGUILayout.IntSlider("Level", module.moduleLevel, 1, 10);

        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.health = EditorGUILayout.Slider("Health", module.health, 0f, 1000f);
        module.airCount = EditorGUILayout.IntSlider("Hangar Capability", module.airCount, 0, 1000);
        module.attackCoolTime = EditorGUILayout.Slider("Launch Cool", module.attackCoolTime, 0f, 10f);
        module.attackFireCount = EditorGUILayout.IntSlider("Launch Count", module.attackFireCount, 0, 10);
        module.maintenanceTime = EditorGUILayout.Slider("Maintenance Time", module.maintenanceTime, 0f, 1000f);

        EditorGUILayout.LabelField("Aircraft Stats", EditorStyles.boldLabel);
        module.aircraftLaunchStraightDistance = EditorGUILayout.Slider("Aircraft Launch Straight Distance", module.aircraftLaunchStraightDistance, 1f, 1000f);
        module.aircraftHealth = EditorGUILayout.Slider("Aircraft Health", module.aircraftHealth, 1f, 1000f);
        module.aircraftAttackPower = EditorGUILayout.Slider("Aircraft Attack Power", module.aircraftAttackPower, 1f, 1000f);
        module.aircraftAttackRange = EditorGUILayout.Slider("Aircraft Attack Range", module.aircraftAttackRange, 1f, 1000f);
        module.aircraftAttackCooldown = EditorGUILayout.Slider("Aircraft Attack Cooldown", module.aircraftAttackCooldown, 1f, 1000f);
        module.aircraftSpeed = EditorGUILayout.Slider("Aircraft Speed", module.aircraftSpeed, 1f, 1000f);
        module.aircraftAmmo = EditorGUILayout.IntSlider("Aircraft Ammo", module.aircraftAmmo, 1, 100);
        module.aircraftDetectionRadius = EditorGUILayout.Slider("Aircraft Detection Radius", module.aircraftDetectionRadius, 1f, 1000f);
        module.aircraftAvoidanceRadius = EditorGUILayout.Slider("Aircraft Avoidance Radius", module.aircraftAvoidanceRadius, 1f, 1000f);

        EditorGUILayout.LabelField("Upgrade Cost", EditorStyles.boldLabel);
        module.upgradeCost.techLevel = EditorGUILayout.IntField("TechLevel", module.upgradeCost.techLevel);
        module.upgradeCost.mineral = EditorGUILayout.LongField("Mineral", module.upgradeCost.mineral);
        module.upgradeCost.mineralRare = EditorGUILayout.LongField("MineralRare", module.upgradeCost.mineralRare);
        module.upgradeCost.mineralExotic = EditorGUILayout.LongField("MineralExotic", module.upgradeCost.mineralExotic);
        module.upgradeCost.mineralDark = EditorGUILayout.LongField("MineralDark", module.upgradeCost.mineralDark);

        module.description = EditorGUILayout.TextField("Description", module.description);
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
