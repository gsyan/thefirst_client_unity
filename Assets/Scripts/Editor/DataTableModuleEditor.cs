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
    private Dictionary<EModuleSubType, bool> beamSubTypeFoldouts = new Dictionary<EModuleSubType, bool>();
    private Dictionary<EModuleSubType, bool> missileSubTypeFoldouts = new Dictionary<EModuleSubType, bool>();
    private Dictionary<EModuleSubType, bool> hangarSubTypeFoldouts = new Dictionary<EModuleSubType, bool>();
    private Dictionary<EModuleSubType, bool> shieldSubTypeFoldouts = new Dictionary<EModuleSubType, bool>();
    private Dictionary<EModuleSubType, bool> interceptorSubTypeFoldouts = new Dictionary<EModuleSubType, bool>();
    private Dictionary<ModuleData, bool> moduleSlotFoldouts = new Dictionary<ModuleData, bool>();

    private bool showBodyModules = false;
    private bool showBeamModules = false;
    private bool showMissileModules = false;
    private bool showHangarModules = false;
    private bool showShieldModules = false;
    private bool showInterceptorModules = false;
    private bool showUtilityTools = true;

    private readonly Color bodyColor = new Color(0.7f, 0.9f, 0.7f);
    private readonly Color beamColor = new Color(0.9f, 0.7f, 0.7f);
    private readonly Color missileColor = new Color(0.9f, 0.7f, 0.7f);
    private readonly Color hangarColor = new Color(0.9f, 0.9f, 0.7f);
    private readonly Color shieldColor = new Color(0.7f, 0.9f, 0.9f);
    private readonly Color interceptorColor = new Color(0.9f, 0.7f, 0.9f);

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
        DrawBeamModuleSection();
        DrawMissileModuleSection();
        DrawHangarModuleSection();
        DrawShieldModuleSection();
        DrawInterceptorModuleSection();

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

        int totalModules = dataTableModule.HullModules.Count + dataTableModule.BeamModules.Count
            + dataTableModule.MissileModules.Count + dataTableModule.HangarModules.Count
            + dataTableModule.ShieldModules.Count + dataTableModule.InterceptorModules.Count;
        GUILayout.Label($"Total: {totalModules}", EditorStyles.miniLabel);

        EditorGUILayout.EndHorizontal();
    }

    #region Body Modules
    private void DrawBodyModuleSection()
    {
        EditorGUILayout.BeginVertical("box");

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = bodyColor;
        showBodyModules = EditorGUILayout.Foldout(showBodyModules, $"Body Modules ({dataTableModule.HullModules.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (showBodyModules)
        {
            foreach (var group in dataTableModule.HullGroups)
            {
                DrawSubTypeGroup(group, bodySubTypeFoldouts, DrawBodyModuleDetails);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBodyModuleDetails(ModuleData module)
    {
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

        module.unlockCommanderLevel = EditorGUILayout.IntField(new GUIContent("Unlock Commander Level", "이 값 이상의 커맨더 레벨부터 사용 가능 (예: 10 = 10레벨부터)"), module.unlockCommanderLevel);

        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.health = EditorGUILayout.FloatField("Health", module.health);
        module.repair = EditorGUILayout.FloatField("Repair", module.repair);
        module.speed = EditorGUILayout.FloatField("Speed", module.speed);
        module.turnRate = EditorGUILayout.FloatField("Turn Rate", module.turnRate);

        DrawCostFields(module);
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
                DrawSubTypeGroup(group, beamSubTypeFoldouts, DrawBeamModuleDetails);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBeamModuleDetails(ModuleData module)
    {
        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.attack = EditorGUILayout.FloatField("Attack", module.attack);
        module.attackCool = EditorGUILayout.FloatField("Cool", module.attackCool);

        EditorGUILayout.LabelField("Projectile Stats", EditorStyles.boldLabel);
        module.speed = EditorGUILayout.FloatField("Projectile Speed (speed 재사용)", module.speed);

        DrawCostFields(module);
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
                DrawSubTypeGroup(group, missileSubTypeFoldouts, DrawMissileModuleDetails);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawMissileModuleDetails(ModuleData module)
    {
        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.attack = EditorGUILayout.FloatField("Attack", module.attack);
        module.attackCool = EditorGUILayout.FloatField("Attack Cool", module.attackCool);

        EditorGUILayout.LabelField("Projectile Stats", EditorStyles.boldLabel);
        module.speed = EditorGUILayout.FloatField("Projectile Speed (speed 재사용)", module.speed);
        module.silenceTime     = EditorGUILayout.FloatField("Silence Time", module.silenceTime);

        DrawCostFields(module);
    }
    #endregion

    #region Hangar Modules
    private void DrawHangarModuleSection()
    {
        EditorGUILayout.BeginVertical("box");

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = hangarColor;
        showHangarModules = EditorGUILayout.Foldout(showHangarModules, $"Hangar Modules ({dataTableModule.HangarModules.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (showHangarModules)
        {
            foreach (var group in dataTableModule.HangarGroups)
            {
                DrawSubTypeGroup(group, hangarSubTypeFoldouts, DrawHangarModuleDetails);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawHangarModuleDetails(ModuleData module)
    {
        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.airCount = EditorGUILayout.IntField("Aircraft Count", module.airCount);
        module.attackCool = EditorGUILayout.FloatField("Attack Cool", module.attackCool);
        module.airMaintenanceTime = EditorGUILayout.FloatField("Air Maintenance Time", module.airMaintenanceTime);

        EditorGUILayout.LabelField("Aircraft Stats", EditorStyles.boldLabel);
        module.airHealth = EditorGUILayout.FloatField("Aircraft Health", module.airHealth);
        module.airAttack = EditorGUILayout.FloatField("Aircraft Attack Power", module.airAttack);
        module.airAttackRange = EditorGUILayout.FloatField("Aircraft Attack Range", module.airAttackRange);
        module.airAttackCool = EditorGUILayout.FloatField("Aircraft Attack Cool", module.airAttackCool);
        module.airSpeed = EditorGUILayout.FloatField("Aircraft Speed", module.airSpeed);
        module.airAmmo = EditorGUILayout.IntField("Aircraft Ammo", module.airAmmo);
        module.airDetectRadius     = EditorGUILayout.FloatField("Aircraft Detect Radius",   module.airDetectRadius);
        module.airAvoidRadius      = EditorGUILayout.FloatField("Aircraft Avoid Radius",    module.airAvoidRadius);
        module.airDisrupt          = EditorGUILayout.FloatField("Air Disrupt",              module.airDisrupt);

        DrawCostFields(module);
    }
    #endregion

    #region Shield Modules
    private void DrawShieldModuleSection()
    {
        EditorGUILayout.BeginVertical("box");

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = shieldColor;
        showShieldModules = EditorGUILayout.Foldout(showShieldModules, $"Shield Modules ({dataTableModule.ShieldModules.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (showShieldModules)
        {
            foreach (var group in dataTableModule.ShieldGroups)
            {
                DrawSubTypeGroup(group, shieldSubTypeFoldouts, DrawShieldModuleDetails);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawShieldModuleDetails(ModuleData module)
    {
        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.health           = EditorGUILayout.FloatField("Health", module.health);
        module.shieldGauge       = EditorGUILayout.FloatField("Shield Gauge", module.shieldGauge);
        module.shieldRegenRate   = EditorGUILayout.FloatField("Shield Regen Rate", module.shieldRegenRate);

        DrawCostFields(module);
    }
    #endregion

    #region Interceptor Modules
    private void DrawInterceptorModuleSection()
    {
        EditorGUILayout.BeginVertical("box");

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = interceptorColor;
        showInterceptorModules = EditorGUILayout.Foldout(showInterceptorModules, $"Interceptor Modules ({dataTableModule.InterceptorModules.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (showInterceptorModules)
        {
            foreach (var group in dataTableModule.InterceptorGroups)
            {
                DrawSubTypeGroup(group, interceptorSubTypeFoldouts, DrawInterceptorModuleDetails);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawInterceptorModuleDetails(ModuleData module)
    {
        EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
        module.health               = EditorGUILayout.FloatField("Health", module.health);
        module.interceptorCount     = EditorGUILayout.IntField("Interceptor Count", module.interceptorCount);
        module.interceptorDelay     = EditorGUILayout.FloatField("Interceptor Delay", module.interceptorDelay);
        module.interceptorRegenRate = EditorGUILayout.FloatField("Interceptor Regen Rate", module.interceptorRegenRate);

        DrawCostFields(module);
    }
    #endregion

    // 서브타입(티어)당 1개뿐인 모듈 항목을 공통으로 그림 — 레벨 축 삭제로 Add/Remove 없음
    private void DrawSubTypeGroup(ModuleSubTypeGroup group, Dictionary<EModuleSubType, bool> foldouts, System.Action<ModuleData> drawDetails)
    {
        if (!foldouts.ContainsKey(group.subType))
            foldouts[group.subType] = false;

        EditorGUILayout.BeginVertical("box");
        foldouts[group.subType] = EditorGUILayout.Foldout(foldouts[group.subType], $"{group.subType}", true);

        if (foldouts[group.subType] && group.modules.Count > 0)
        {
            EditorGUILayout.BeginVertical("box");
            drawDetails(group.modules[0]);
            group.modules[0].description = EditorGUILayout.TextField("Description", group.modules[0].description);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawCostFields(ModuleData module)
    {
        EditorGUILayout.LabelField("Install Cost", EditorStyles.boldLabel);
        module.statPoint = EditorGUILayout.IntField("Stat Point", module.statPoint);
    }

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
        sb.AppendLine("sub_type,unlock_commander_level,stat_point,health,repair,speed,turn_rate,attack,splash_radius,attack_cool,silence_time,air_count,air_maintenance_time,air_health,air_attack,air_attack_range,air_attack_cool,air_speed,air_ammo,air_detect_radius,air_avoid_radius,air_disrupt,shield_gauge,shield_regen_rate,interceptor_count,interceptor_delay,interceptor_regen_rate,description");

        var allGroups = new List<ModuleSubTypeGroup>();
        allGroups.AddRange(dataTableModule.HullGroups);
        allGroups.AddRange(dataTableModule.BeamGroups);
        allGroups.AddRange(dataTableModule.MissileGroups);
        allGroups.AddRange(dataTableModule.HangarGroups);
        allGroups.AddRange(dataTableModule.ShieldGroups);
        allGroups.AddRange(dataTableModule.InterceptorGroups);

        foreach (var group in allGroups)
        {
            foreach (var d in group.modules)
            {
                sb.AppendLine(string.Format(ic,
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27}",
                    (int)d.moduleSubType,
                    d.unlockCommanderLevel,
                    d.statPoint,
                    d.health, d.repair, d.speed, d.turnRate,
                    d.attack, d.splashRadius, d.attackCool,
                    d.silenceTime,
                    d.airCount, d.airMaintenanceTime,
                    d.airHealth, d.airAttack, d.airAttackRange,
                    d.airAttackCool, d.airSpeed, d.airAmmo,
                    d.airDetectRadius, d.airAvoidRadius, d.airDisrupt,
                    d.shieldGauge, d.shieldRegenRate,
                    d.interceptorCount, d.interceptorDelay, d.interceptorRegenRate,
                    d.description));
            }
        }
        return sb.ToString();
    }
}
#endif
