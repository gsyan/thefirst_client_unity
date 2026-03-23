// DataTableModule ScriptableObject 커스텀 에디터
// CSV Import / JSON Export / 데이터 생성 유틸리티 버튼 제공

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

        DrawUtilityTools();
        EditorGUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawBodyModuleSection();
        DrawEngineModuleSection();
        DrawBeamModuleSection();
        DrawMissileModuleSection();
        DrawHangerModuleSection();

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
                repair = 1f,
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
        module.moduleLevel = EditorGUILayout.IntField("Level", module.moduleLevel);

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
        module.health = EditorGUILayout.FloatField("Health", module.health);
        module.repair = EditorGUILayout.FloatField("Repair", module.repair);
        
        EditorGUILayout.LabelField("Upgrade Cost", EditorStyles.boldLabel);
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
        module.moduleLevel = EditorGUILayout.IntField("Level", module.moduleLevel);

        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.speed = EditorGUILayout.FloatField("Speed", module.speed);

        EditorGUILayout.LabelField("Upgrade Cost", EditorStyles.boldLabel);
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
                attack = 25f,
                attackFireCount = 1,
                attackCool = 2f,
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
        module.moduleLevel = EditorGUILayout.IntField("Level", module.moduleLevel);

        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.attack = EditorGUILayout.FloatField("Attack", module.attack);
        module.attackFireCount = EditorGUILayout.IntField("Fire Count", module.attackFireCount);
        module.attackCool = EditorGUILayout.FloatField("Cool", module.attackCool);

        EditorGUILayout.LabelField("Projectile Stats", EditorStyles.boldLabel);
        module.projectileWidth = EditorGUILayout.FloatField("Projectile Width", module.projectileWidth);
        module.projectileSpeed = EditorGUILayout.FloatField("Projectile Speed", module.projectileSpeed);

        EditorGUILayout.LabelField("Upgrade Cost", EditorStyles.boldLabel);
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
                attack = 25f,
                attackFireCount = 1,
                attackCool = 2f,
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
        module.moduleLevel = EditorGUILayout.IntField("Level", module.moduleLevel);

        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.attack = EditorGUILayout.FloatField("Attack", module.attack);
        module.attackFireCount = EditorGUILayout.IntField("Fire Count", module.attackFireCount);
        module.attackCool = EditorGUILayout.FloatField("Attack Cool", module.attackCool);

        EditorGUILayout.LabelField("Projectile Stats", EditorStyles.boldLabel);
        module.projectileWidth = EditorGUILayout.FloatField("Projectile Width", module.projectileWidth);
        module.projectileSpeed = EditorGUILayout.FloatField("Projectile Speed", module.projectileSpeed);

        EditorGUILayout.LabelField("Upgrade Cost", EditorStyles.boldLabel);
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
                attackCool = 1f,
                attackFireCount = 1,
                airMaintenanceTime = 10f,
                airLaunchDist = 100f,
                airHealth = 50f,
                airAttack = 10f,
                airAttackRange = 100f,
                airAttackCool = 10f,
                airDetectRadius = 200f,
                airAvoidRadius = 200f,
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
        module.moduleLevel = EditorGUILayout.IntField("Level", module.moduleLevel);

        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.airCount = EditorGUILayout.IntField("Aircraft Count", module.airCount);
        module.attackCool = EditorGUILayout.FloatField("Attack Cool", module.attackCool);
        module.attackFireCount = EditorGUILayout.IntField("Attack Count", module.attackFireCount);
        module.airMaintenanceTime = EditorGUILayout.FloatField("Air Maintenance Time", module.airMaintenanceTime);

        EditorGUILayout.LabelField("Aircraft Stats", EditorStyles.boldLabel);
        module.airLaunchDist = EditorGUILayout.FloatField("Aircraft Launch Dist", module.airLaunchDist);
        module.airHealth = EditorGUILayout.FloatField("Aircraft Health", module.airHealth);
        module.airAttack = EditorGUILayout.FloatField("Aircraft Attack Power", module.airAttack);
        module.airAttackRange = EditorGUILayout.FloatField("Aircraft Attack Range", module.airAttackRange);
        module.airAttackCool = EditorGUILayout.FloatField("Aircraft Attack Cool", module.airAttackCool);
        module.airSpeed = EditorGUILayout.FloatField("Aircraft Speed", module.airSpeed);
        module.airAmmo = EditorGUILayout.IntField("Aircraft Ammo", module.airAmmo);
        module.airDetectRadius = EditorGUILayout.FloatField("Aircraft Detect Radius", module.airDetectRadius);
        module.airAvoidRadius = EditorGUILayout.FloatField("Aircraft Avoid Radius", module.airAvoidRadius);

        EditorGUILayout.LabelField("Upgrade Cost", EditorStyles.boldLabel);
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

            if (GUILayout.Button("Import from CSV"))
            {
                string csvPath = Application.dataPath + "/Resources/DataTable/Module/datatable_module.csv";
                if (System.IO.File.Exists(csvPath) == false)
                {
                    EditorUtility.DisplayDialog("Error", $"CSV 파일을 찾을 수 없습니다:\n{csvPath}", "OK");
                }
                else if (EditorUtility.DisplayDialog("Import from CSV",
                    $"CSV 파일을 읽어 모듈 데이터를 갱신합니다.\nmoduleSlots 데이터 업데이트 됩니다.\n\n{csvPath}\n\n계속하시겠습니까?", "Import", "Cancel"))
                {
                    string csvText = System.IO.File.ReadAllText(csvPath, System.Text.Encoding.UTF8);
                    dataTableModule.LoadFromCsv(csvText);
                    EditorUtility.DisplayDialog("완료", "CSV Import가 완료되었습니다.", "OK");
                }
            }

            if (GUILayout.Button("Export to CSV"))
            {
                string csvPath = Application.dataPath + "/Resources/DataTable/Module/datatable_module.csv";
                if (EditorUtility.DisplayDialog("Export to CSV",
                    $"현재 데이터를 CSV 파일로 덮어씁니다.\n\n{csvPath}\n\n계속하시겠습니까?", "Export", "Cancel"))
                {
                    string csv = ExportToCsv();
                    System.IO.File.WriteAllText(csvPath, csv, System.Text.Encoding.UTF8);
                    AssetDatabase.Refresh();
                    EditorUtility.DisplayDialog("완료", "CSV Export가 완료되었습니다.", "OK");
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }
    #endregion

    private string ExportToCsv()
    {
        var ic = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("type,sub_type,level,health,repair,speed,attack,attack_count,attack_cool,projectile_width,projectile_speed,air_count,air_maintenance_time,air_launch_dist,air_health,air_attack,air_attack_range,air_attack_cool,air_speed,air_ammo,air_detect_radius,air_avoid_radius,cost_m,cost_mr,cost_me,cost_md,description");

        var allGroups = new List<ModuleSubTypeGroup>();
        allGroups.AddRange(dataTableModule.BodyGroups);
        allGroups.AddRange(dataTableModule.EngineGroups);
        allGroups.AddRange(dataTableModule.BeamGroups);
        allGroups.AddRange(dataTableModule.MissileGroups);
        allGroups.AddRange(dataTableModule.HangerGroups);

        foreach (var group in allGroups)
        {
            foreach (var d in group.modules)
            {
                sb.AppendLine(string.Format(ic,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26}",
                    (int)d.moduleType, (int)d.moduleSubType, d.moduleLevel,
                    d.health, d.repair, d.speed,
                    d.attack, d.attackFireCount, d.attackCool,
                    d.projectileWidth, d.projectileSpeed,
                    d.airCount, d.airMaintenanceTime, d.airLaunchDist,
                    d.airHealth, d.airAttack, d.airAttackRange,
                    d.airAttackCool, d.airSpeed, d.airAmmo,
                    d.airDetectRadius, d.airAvoidRadius,
                    d.upgradeCost.mineral, d.upgradeCost.mineralRare,
                    d.upgradeCost.mineralExotic, d.upgradeCost.mineralDark,
                    d.description));
            }
        }
        return sb.ToString();
    }
}
#endif
