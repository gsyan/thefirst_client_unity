// DataTableCommander 커스텀 에디터 — 커맨더 레벨 데이터 Inspector UI 및 CSV Import 툴
// CSV 경로: Assets/Resources/DataTable/Commander/datatable_commander.csv

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DataTableCommander))]
public class DataTableCommanderEditor : Editor
{
    private DataTableCommander dataTable;
    private bool m_foldout = false;

    private void OnEnable()
    {
        dataTable = (DataTableCommander)target;
    }

    public override void OnInspectorGUI()
    {
        if (dataTable == null) return;

        serializedObject.Update();

        DrawCsvTools();
        EditorGUILayout.Space(10);
        DrawCommanderList();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(dataTable);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawCommanderList()
    {
        var list = dataTable.GetCommanderDataList();

        EditorGUILayout.BeginVertical("box");
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.8f, 0.8f, 1.0f);
        m_foldout = EditorGUILayout.Foldout(m_foldout, $"Commander Data ({list.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (m_foldout)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var data = list[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Level {data.commanderLevel}", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                data.commanderLevel = EditorGUILayout.IntField("Commander Level", data.commanderLevel);
                data.requireExp     = EditorGUILayout.IntField("Point",           data.requireExp);
                data.shipCount      = EditorGUILayout.IntField("Ship Count",      data.shipCount);
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

        string csvPath = Application.dataPath + "/Resources/DataTable/Commander/datatable_commander.csv";

        if (GUILayout.Button("Import Commander CSV"))
        {
            if (System.IO.File.Exists(csvPath) == false)
            {
                EditorUtility.DisplayDialog("Error", $"파일 없음:\n{csvPath}", "OK");
            }
            else if (EditorUtility.DisplayDialog("Import Commander CSV",
                "datatable_commander.csv 를 읽어 커맨더 레벨 데이터를 갱신합니다.\n기존 데이터는 삭제됩니다.", "Import", "Cancel"))
            {
                string csvText = System.IO.File.ReadAllText(csvPath, System.Text.Encoding.UTF8);
                dataTable.LoadFromCsv(csvText);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete",
                    $"Import 완료!\n커맨더 레벨: {dataTable.GetCommanderDataList().Count}개", "OK");
            }
        }

        EditorGUILayout.EndVertical();
    }
}
#endif
