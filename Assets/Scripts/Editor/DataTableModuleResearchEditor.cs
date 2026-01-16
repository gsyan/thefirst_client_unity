
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(DataTableModuleResearch))]
public class DataTableModuleResearchEditor : Editor
{
    private DataTableModuleResearch dataTable;
    private Vector2 scrollPosition;

    private Dictionary<EModuleType, bool> typeFoldouts = new Dictionary<EModuleType, bool>();
    private Dictionary<ModuleResearchData, bool> dataFoldouts = new Dictionary<ModuleResearchData, bool>();

    private readonly Color bodyColor = new Color(0.7f, 0.9f, 0.7f);
    private readonly Color engineColor = new Color(0.7f, 0.7f, 0.9f);
    private readonly Color beamColor = new Color(0.9f, 0.7f, 0.7f);
    private readonly Color missileColor = new Color(0.9f, 0.7f, 0.7f);
    private readonly Color hangerColor = new Color(0.9f, 0.9f, 0.7f);

    private void OnEnable()
    {
        dataTable = (DataTableModuleResearch)target;
    }

    public override void OnInspectorGUI()
    {
        if (dataTable == null) return;

        serializedObject.Update();

        EditorGUILayout.Space(5);
        DrawCustomHeader();
        EditorGUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // Group research data by module type
        var groupedData = dataTable.ResearchDataList
            .GroupBy(r => r.m_moduleType)
            .OrderBy(g => g.Key);

        foreach (var group in groupedData)
        {
            DrawModuleTypeGroup(group.Key, group.ToList());
        }

        EditorGUILayout.Space(20);
        DrawUtilityTools();

        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(dataTable);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawCustomHeader()
    {
        EditorGUILayout.BeginHorizontal("box");
        GUILayout.Label("Data Table Module Research", EditorStyles.largeLabel);
        GUILayout.FlexibleSpace();

        GUILayout.Label($"Total: {dataTable.ResearchDataList.Count}", EditorStyles.miniLabel);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawModuleTypeGroup(EModuleType moduleType, List<ModuleResearchData> researchDataList)
    {
        if (!typeFoldouts.ContainsKey(moduleType))
            typeFoldouts[moduleType] = false;

        EditorGUILayout.BeginVertical("box");

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = GetColorForModuleType(moduleType);

        EditorGUILayout.BeginHorizontal();
        typeFoldouts[moduleType] = EditorGUILayout.Foldout(
            typeFoldouts[moduleType],
            $"{moduleType} Research ({researchDataList.Count})",
            true,
            EditorStyles.foldoutHeader
        );
        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = originalColor;

        if (typeFoldouts[moduleType])
        {
            foreach (var researchData in researchDataList)
            {
                DrawResearchData(researchData);
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawResearchData(ModuleResearchData researchData)
    {
        if (!dataFoldouts.ContainsKey(researchData))
            dataFoldouts[researchData] = false;

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        dataFoldouts[researchData] = EditorGUILayout.Foldout(
            dataFoldouts[researchData],
            $"{researchData.m_moduleSubType}",
            true
        );
        EditorGUILayout.EndHorizontal();

        if (dataFoldouts[researchData])
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Research Cost", EditorStyles.boldLabel);
            researchData.m_researchCost.techLevel = EditorGUILayout.IntField("Tech Level", researchData.m_researchCost.techLevel);
            researchData.m_researchCost.mineral = EditorGUILayout.LongField("Mineral", researchData.m_researchCost.mineral);
            researchData.m_researchCost.mineralRare = EditorGUILayout.LongField("Mineral Rare", researchData.m_researchCost.mineralRare);
            researchData.m_researchCost.mineralExotic = EditorGUILayout.LongField("Mineral Exotic", researchData.m_researchCost.mineralExotic);
            researchData.m_researchCost.mineralDark = EditorGUILayout.LongField("Mineral Dark", researchData.m_researchCost.mineralDark);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
            researchData.m_description = EditorGUILayout.TextArea(researchData.m_description, GUILayout.Height(60));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawUtilityTools()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Utility Tools", EditorStyles.foldoutHeader);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Generate Default Research Data"))
        {
            if (EditorUtility.DisplayDialog("Generate Data",
                "This will clear existing data and generate default research data for all module subtypes.\n\n" +
                "Continue?", "Yes", "Cancel"))
            {
                dataTable.InitializeResearchData();
                EditorUtility.DisplayDialog("Complete", "Research data generated successfully!", "OK");
            }
        }

        if (GUILayout.Button("Validate Data"))
        {
            bool isValid = dataTable.ValidateData();
            EditorUtility.DisplayDialog("Validation",
                isValid ? "Data is valid!" : "Data validation failed. Check console.",
                "OK");
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        DrawJsonTools();

        EditorGUILayout.EndVertical();
    }

    private void DrawJsonTools()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("JSON Import/Export", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Export to JSON"))
        {
            string json = dataTable.ExportToJson();
            string path = EditorUtility.SaveFilePanel("Export Module Research Data", "", "DataTableModuleResearch.json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, json);
                EditorUtility.DisplayDialog("Export", "Module research data exported successfully!", "OK");
            }
        }

        if (GUILayout.Button("Import from JSON"))
        {
            string path = EditorUtility.OpenFilePanel("Import Module Research Data", "", "json");
            if (!string.IsNullOrEmpty(path))
            {
                string json = System.IO.File.ReadAllText(path);
                dataTable.ImportFromJson(json);
                EditorUtility.DisplayDialog("Import", "Module research data imported successfully!", "OK");
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private Color GetColorForModuleType(EModuleType moduleType)
    {
        switch (moduleType)
        {
            case EModuleType.Body:
                return bodyColor;
            case EModuleType.Engine:
                return engineColor;
            case EModuleType.Beam:
                return beamColor;
            case EModuleType.Missile:
                return missileColor;
            case EModuleType.Hanger:
                return hangerColor;
            default:
                return Color.white;
        }
    }
}
#endif
