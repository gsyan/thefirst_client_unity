
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(DataTableResearch))]
public class DataTableResearchEditor : Editor
{
    private DataTableResearch dataTable;
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
        dataTable = (DataTableResearch)target;
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
            .GroupBy(r => r.moduleType)
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
            $"{researchData.moduleSubType} ({researchData.researchId})",
            true
        );
        EditorGUILayout.EndHorizontal();

        if (dataFoldouts[researchData])
        {
            EditorGUI.indentLevel++;

            // ResearchNodeData 공통 필드
            EditorGUILayout.LabelField("Node Info", EditorStyles.boldLabel);
            researchData.researchId = EditorGUILayout.TextField("Research ID", researchData.researchId);

            EditorGUILayout.Space(5);

            // 선행 연구 조건 (string ID를 EModuleSubType 드롭다운으로 편집)
            EditorGUILayout.LabelField("Prerequisites", EditorStyles.boldLabel);
            if (researchData.prerequisiteIds == null)
                researchData.prerequisiteIds = new List<string>();

            for (int i = 0; i < researchData.prerequisiteIds.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                // string → EModuleSubType 변환 시도, 실패하면 텍스트 필드
                EModuleSubType prereqEnum = EModuleSubType.none;
                bool isModuleSubType = System.Enum.TryParse(researchData.prerequisiteIds[i], out prereqEnum);

                if (isModuleSubType)
                {
                    prereqEnum = (EModuleSubType)EditorGUILayout.EnumPopup($"Prerequisite {i + 1}", prereqEnum);
                    researchData.prerequisiteIds[i] = prereqEnum.ToString();
                }
                else
                {
                    researchData.prerequisiteIds[i] = EditorGUILayout.TextField($"Prerequisite {i + 1}", researchData.prerequisiteIds[i]);
                }

                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    researchData.prerequisiteIds.RemoveAt(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ Add Prerequisite", GUILayout.Width(150)))
            {
                researchData.prerequisiteIds.Add(EModuleSubType.none.ToString());
            }

            EditorGUILayout.Space(5);

            // UI 위치
            researchData.uiPosition = EditorGUILayout.Vector2Field("UI Position", researchData.uiPosition);

            EditorGUILayout.Space(5);

            // 연구 비용
            EditorGUILayout.LabelField("Research Cost", EditorStyles.boldLabel);
            researchData.researchCost.techLevel = EditorGUILayout.IntField("Tech Level", researchData.researchCost.techLevel);
            researchData.researchCost.mineral = EditorGUILayout.LongField("Mineral", researchData.researchCost.mineral);
            researchData.researchCost.mineralRare = EditorGUILayout.LongField("Mineral Rare", researchData.researchCost.mineralRare);
            researchData.researchCost.mineralExotic = EditorGUILayout.LongField("Mineral Exotic", researchData.researchCost.mineralExotic);
            researchData.researchCost.mineralDark = EditorGUILayout.LongField("Mineral Dark", researchData.researchCost.mineralDark);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Description", EditorStyles.boldLabel);
            researchData.description = EditorGUILayout.TextArea(researchData.description, GUILayout.Height(60));

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
            case EModuleType.body:
                return bodyColor;
            case EModuleType.engine:
                return engineColor;
            case EModuleType.beam:
                return beamColor;
            case EModuleType.missile:
                return missileColor;
            case EModuleType.hanger:
                return hangerColor;
            default:
                return Color.white;
        }
    }
}
#endif
