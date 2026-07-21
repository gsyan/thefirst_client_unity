// DataTableZoneGridSize 커스텀 에디터 — 존 그리드 크기 규칙표 Inspector UI 및 CSV Import 툴
// CSV 경로: Assets/Resources/DataTable/Exploration/datatable_zone_grid_size.csv

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DataTableZoneGridSize))]
public class DataTableZoneGridSizeEditor : Editor
{
    private DataTableZoneGridSize dataTable;
    private bool m_foldout = false;

    private void OnEnable()
    {
        dataTable = (DataTableZoneGridSize)target;
    }

    public override void OnInspectorGUI()
    {
        if (dataTable == null) return;

        serializedObject.Update();

        DrawCsvTools();
        EditorGUILayout.Space(10);
        DrawZoneGridSizeList();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(dataTable);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawZoneGridSizeList()
    {
        var list = dataTable.GetZoneGridSizeDataList();

        EditorGUILayout.BeginVertical("box");
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.8f, 0.8f, 1.0f);
        m_foldout = EditorGUILayout.Foldout(m_foldout, $"Zone Grid Size Data ({list.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (m_foldout)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var data = list[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"~ Zone {data.zoneMax}", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                data.zoneMax    = EditorGUILayout.IntField("Zone Max",    data.zoneMax);
                data.gridWidth  = EditorGUILayout.IntField("Grid Width",  data.gridWidth);
                data.gridHeight = EditorGUILayout.IntField("Grid Height", data.gridHeight);
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

        string csvPath = Application.dataPath + "/Resources/DataTable/Exploration/datatable_zone_grid_size.csv";

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Import CSV"))
        {
            if (System.IO.File.Exists(csvPath) == false)
            {
                EditorUtility.DisplayDialog("Error", $"파일 없음:\n{csvPath}", "OK");
            }
            else if (EditorUtility.DisplayDialog("Import Zone Grid Size CSV",
                "datatable_zone_grid_size.csv 를 읽어 존 그리드 크기 데이터를 갱신합니다.\n기존 데이터는 삭제됩니다.", "Import", "Cancel"))
            {
                string csvText = System.IO.File.ReadAllText(csvPath, System.Text.Encoding.UTF8);
                dataTable.LoadFromCsv(csvText);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete",
                    $"Import 완료!\n존 그리드 크기 구간: {dataTable.GetZoneGridSizeDataList().Count}개", "OK");
            }
        }

        if (GUILayout.Button("Export to CSV"))
        {
            if (EditorUtility.DisplayDialog("Export to CSV",
                $"현재 데이터를 CSV 파일로 덮어씁니다.\n\n{csvPath}\n\n계속하시겠습니까?", "Export", "Cancel"))
            {
                string csv = dataTable.ExportToCsv();
                System.IO.File.WriteAllText(csvPath, csv, System.Text.Encoding.UTF8);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete", "CSV Export가 완료되었습니다.", "OK");
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }
}
#endif
