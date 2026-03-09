// DataTableResearch 커스텀 에디터 - 연구 트리 데이터 Inspector UI 및 CSV/JSON Import/Export 툴

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
    private bool techLevelFoldout = false;

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

        EditorGUILayout.Space(10);
        DrawTechLevelGroup();

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

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawTechLevelGroup()
    {
        var list = dataTable.TechLevelDataList;

        EditorGUILayout.BeginVertical("box");
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.8f, 0.8f, 1.0f);
        techLevelFoldout = EditorGUILayout.Foldout(techLevelFoldout, $"Tech Level Upgrades ({list.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (techLevelFoldout)
        {
            foreach (var data in list)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"tech_level_{data.targetTechLevel}  ({data.researchId})", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.IntField("Target Tech Level", data.targetTechLevel);
                EditorGUILayout.LabelField("Prerequisites", string.Join(", ", data.prerequisiteIds));
                EditorGUILayout.LabelField("Cost M",  data.researchCost.mineral.ToString());
                EditorGUILayout.LabelField("Cost MR", data.researchCost.mineralRare.ToString());
                EditorGUILayout.LabelField("Cost ME", data.researchCost.mineralExotic.ToString());
                EditorGUILayout.LabelField("Cost MD", data.researchCost.mineralDark.ToString());
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawUtilityTools()
    {
        EditorGUILayout.BeginVertical("box");
        //EditorGUILayout.Space(10);
        DrawCsvTools();
        EditorGUILayout.EndVertical();
    }

    private void DrawCsvTools()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("CSV Import/Export", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Import from CSV"))
        {
            string csvPath = Application.dataPath + "/Resources/DataTable/Research/datatable_research.csv";
            if (System.IO.File.Exists(csvPath) == false)
            {
                EditorUtility.DisplayDialog("Error", $"CSV 파일을 찾을 수 없습니다:\n{csvPath}", "OK");
            }
            else if (EditorUtility.DisplayDialog("Import from CSV",
                "CSV 파일을 읽어 연구 데이터를 갱신합니다.\n기존 데이터는 모두 삭제됩니다.\n\n계속하시겠습니까?", "Import", "Cancel"))
            {
                string csvText = System.IO.File.ReadAllText(csvPath, System.Text.Encoding.UTF8);
                dataTable.LoadFromCsv(csvText);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete", $"CSV Import 완료!\n모듈: {dataTable.ResearchDataList.Count}개, 기술레벨: {dataTable.TechLevelDataList.Count}개", "OK");
            }
        }

        if (GUILayout.Button("Export to CSV"))
        {
            string dirPath = Application.dataPath + "/Resources/DataTable/Research";
            if (System.IO.Directory.Exists(dirPath) == false)
                System.IO.Directory.CreateDirectory(dirPath);

            string csvPath = dirPath + "/datatable_research.csv";
            string csv = ExportToCsv();
            System.IO.File.WriteAllText(csvPath, csv, System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Complete", $"CSV Export 완료!\n{csvPath}", "OK");
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private string ExportToCsv()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("research_id,module_type,module_sub_type,prerequisites,ui_pos_x,ui_pos_y,tech_level,cost_m,cost_mr,cost_me,cost_md,description");

        // tech_level 행 먼저 출력
        foreach (var d in dataTable.TechLevelDataList)
        {
            string prereqs = d.prerequisiteIds != null ? string.Join("|", d.prerequisiteIds) : "";
            sb.AppendLine($"{d.researchId},0,0,{prereqs},{d.uiPosition.x},{d.uiPosition.y},{d.researchCost.techLevel},{d.researchCost.mineral},{d.researchCost.mineralRare},{d.researchCost.mineralExotic},{d.researchCost.mineralDark},");
        }

        // 모듈 연구 행
        foreach (var d in dataTable.ResearchDataList)
        {
            string prereqs = d.prerequisiteIds != null ? string.Join("|", d.prerequisiteIds) : "";
            sb.AppendLine($"{d.researchId},{(int)d.moduleType},{(int)d.moduleSubType},{prereqs},{d.uiPosition.x},{d.uiPosition.y},{d.researchCost.techLevel},{d.researchCost.mineral},{d.researchCost.mineralRare},{d.researchCost.mineralExotic},{d.researchCost.mineralDark},");
        }
        return sb.ToString();
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
