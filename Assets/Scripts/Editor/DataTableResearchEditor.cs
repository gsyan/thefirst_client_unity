// DataTableResearch 커스텀 에디터 — 모듈 연구 트리 Inspector UI 및 CSV Import 툴
// CSV: datatable_research_subtype.csv (모듈 서브타입)

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

    private readonly Color bodyColor    = new Color(0.7f, 0.9f, 0.7f);
    private readonly Color beamColor    = new Color(0.9f, 0.7f, 0.7f);
    private readonly Color missileColor = new Color(0.9f, 0.7f, 0.7f);
    private readonly Color hangerColor  = new Color(0.9f, 0.9f, 0.7f);

    private void OnEnable()
    {
        dataTable = (DataTableResearch)target;
    }

    public override void OnInspectorGUI()
    {
        if (dataTable == null) return;

        serializedObject.Update();

        EditorGUILayout.Space(5);
        DrawHeader();
        EditorGUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        var groupedData = dataTable.GetResearchDataList()
            .GroupBy(r => r.moduleType)
            .OrderBy(g => g.Key);

        foreach (var group in groupedData)
            DrawModuleTypeGroup(group.Key, group.ToList());

        EditorGUILayout.Space(20);
        DrawCsvTools();

        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(dataTable);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal("box");
        GUILayout.Label("Data Table Module Research", EditorStyles.largeLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Total: {dataTable.GetResearchDataList().Count}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawModuleTypeGroup(EModuleType moduleType, List<ModuleResearchData> list)
    {
        if (typeFoldouts.ContainsKey(moduleType) == false)
            typeFoldouts[moduleType] = false;

        EditorGUILayout.BeginVertical("box");

        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = GetColorForModuleType(moduleType);

        EditorGUILayout.BeginHorizontal();
        typeFoldouts[moduleType] = EditorGUILayout.Foldout(
            typeFoldouts[moduleType],
            $"{moduleType} Research ({list.Count})",
            true,
            EditorStyles.foldoutHeader);
        EditorGUILayout.EndHorizontal();

        GUI.backgroundColor = originalColor;

        if (typeFoldouts[moduleType])
        {
            foreach (var researchData in list)
                DrawResearchData(researchData);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawResearchData(ModuleResearchData researchData)
    {
        if (dataFoldouts.ContainsKey(researchData) == false)
            dataFoldouts[researchData] = false;

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.BeginHorizontal();
        dataFoldouts[researchData] = EditorGUILayout.Foldout(
            dataFoldouts[researchData],
            $"{researchData.moduleSubType} ({researchData.researchId})",
            true);
        EditorGUILayout.EndHorizontal();

        if (dataFoldouts[researchData])
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Node Info", EditorStyles.boldLabel);
            researchData.researchId = EditorGUILayout.TextField("Research ID", researchData.researchId);

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Prerequisites", EditorStyles.boldLabel);
            if (researchData.prerequisiteIds == null)
                researchData.prerequisiteIds = new List<string>();

            for (int i = 0; i < researchData.prerequisiteIds.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

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
                researchData.prerequisiteIds.Add(EModuleSubType.none.ToString());

            EditorGUILayout.Space(5);

            researchData.uiPosition = EditorGUILayout.Vector2Field("UI Position", researchData.uiPosition);

            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField("Research Cost", EditorStyles.boldLabel);
            researchData.pointCost = EditorGUILayout.IntField("ModulePoint", researchData.pointCost);

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawCsvTools()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("CSV Import", EditorStyles.boldLabel);

        string subtypeCsvPath = Application.dataPath + "/Resources/DataTable/Research/datatable_research_subtype.csv";

        if (GUILayout.Button("Import Subtype CSV"))
        {
            if (System.IO.File.Exists(subtypeCsvPath) == false)
            {
                EditorUtility.DisplayDialog("Error", $"파일 없음:\n{subtypeCsvPath}", "OK");
            }
            else if (EditorUtility.DisplayDialog("Import Subtype CSV",
                "datatable_research_subtype.csv 를 읽어 모듈 연구 데이터를 갱신합니다.\n기존 데이터는 삭제됩니다.", "Import", "Cancel"))
            {
                string csvText = System.IO.File.ReadAllText(subtypeCsvPath, System.Text.Encoding.UTF8);
                dataTable.LoadSubtypeFromCsv(csvText);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete", $"Import 완료!\n모듈: {dataTable.GetResearchDataList().Count}개", "OK");
            }
        }

        EditorGUILayout.EndVertical();
    }

    private Color GetColorForModuleType(EModuleType moduleType)
    {
        switch (moduleType)
        {
            case EModuleType.body:    return bodyColor;
            case EModuleType.beam:    return beamColor;
            case EModuleType.missile: return missileColor;
            case EModuleType.hanger:  return hangerColor;
            default:                  return Color.white;
        }
    }
}
#endif
