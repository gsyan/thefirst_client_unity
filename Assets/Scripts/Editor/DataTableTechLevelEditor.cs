// DataTableTechLevel 커스텀 에디터 — 기술레벨 데이터 Inspector UI 및 CSV Import 툴
// CSV 경로: Assets/Resources/DataTable/Tech/datatable_tech_level.csv

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DataTableTechLevel))]
public class DataTableTechLevelEditor : Editor
{
    private DataTableTechLevel dataTable;
    private bool m_foldout = false;

    private void OnEnable()
    {
        dataTable = (DataTableTechLevel)target;
    }

    public override void OnInspectorGUI()
    {
        if (dataTable == null) return;

        serializedObject.Update();

        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal("box");
        GUILayout.Label("DataTable Tech Level", EditorStyles.largeLabel);
        GUILayout.FlexibleSpace();
        var list = dataTable.GetTechLevelDataList();
        GUILayout.Label($"Total: {list.Count}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        DrawTechLevelList();
        EditorGUILayout.Space(10);
        DrawCsvTools();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(dataTable);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawTechLevelList()
    {
        var list = dataTable.GetTechLevelDataList();

        EditorGUILayout.BeginVertical("box");
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.8f, 0.8f, 1.0f);
        m_foldout = EditorGUILayout.Foldout(m_foldout, $"Tech Level Data ({list.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (m_foldout)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var data = list[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Level {data.targetTechLevel}", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                data.targetTechLevel   = EditorGUILayout.IntField("Target Tech Level",   data.targetTechLevel);
                data.requiredTechPoint = EditorGUILayout.IntField("Required Tech Point", data.requiredTechPoint);
                data.shipCount         = EditorGUILayout.IntField("Ship Count",          data.shipCount);
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawCsvTools()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("CSV Import", EditorStyles.boldLabel);

        string csvPath = Application.dataPath + "/Resources/DataTable/Tech/datatable_tech_level.csv";

        if (GUILayout.Button("Import Tech Level CSV"))
        {
            if (System.IO.File.Exists(csvPath) == false)
            {
                EditorUtility.DisplayDialog("Error", $"파일 없음:\n{csvPath}", "OK");
            }
            else if (EditorUtility.DisplayDialog("Import Tech Level CSV",
                "datatable_tech_level.csv 를 읽어 기술레벨 데이터를 갱신합니다.\n기존 데이터는 삭제됩니다.", "Import", "Cancel"))
            {
                string csvText = System.IO.File.ReadAllText(csvPath, System.Text.Encoding.UTF8);
                dataTable.LoadFromCsv(csvText);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete",
                    $"Import 완료!\n기술레벨: {dataTable.GetTechLevelDataList().Count}개", "OK");
            }
        }

        EditorGUILayout.EndVertical();
    }
}
#endif
