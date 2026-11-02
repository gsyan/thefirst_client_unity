// DataTableHullArchetypePassive 커스텀 에디터 — 아키타입 패시브 4단계 데이터 Inspector UI 및 CSV Import 툴
// CSV 경로: Assets/Resources/DataTable/HullArchetypePassive/datatable_hull_archetype_passive.csv

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DataTableHullArchetypePassive))]
public class DataTableHullArchetypePassiveEditor : Editor
{
    private DataTableHullArchetypePassive dataTable;
    private bool m_foldout = false;

    private void OnEnable()
    {
        dataTable = (DataTableHullArchetypePassive)target;
    }

    public override void OnInspectorGUI()
    {
        if (dataTable == null) return;

        serializedObject.Update();

        DrawCsvTools();
        EditorGUILayout.Space(10);
        DrawPassiveList();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(dataTable);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawPassiveList()
    {
        var list = dataTable.GetPassiveDataList();

        EditorGUILayout.BeginVertical("box");
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.8f, 0.8f, 1.0f);
        m_foldout = EditorGUILayout.Foldout(m_foldout, $"Hull Archetype Passive Data ({list.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (m_foldout)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var data = list[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"{data.hullArchetype} - Stage {data.stage}", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                data.hullArchetype  = EditorGUILayout.TextField("Hull Archetype", data.hullArchetype);
                data.stage          = EditorGUILayout.IntField("Stage", data.stage);
                data.counterType    = EditorGUILayout.TextField("Counter Type", data.counterType);
                data.threshold      = EditorGUILayout.IntField("Threshold", data.threshold);
                data.descriptionKey = EditorGUILayout.TextField("Description Key", data.descriptionKey);
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

        string csvPath = Application.dataPath + "/Resources/DataTable/HullArchetypePassive/datatable_hull_archetype_passive.csv";

        if (GUILayout.Button("Import Hull Archetype Passive CSV"))
        {
            if (System.IO.File.Exists(csvPath) == false)
            {
                EditorUtility.DisplayDialog("Error", $"파일 없음:\n{csvPath}", "OK");
            }
            else if (EditorUtility.DisplayDialog("Import Hull Archetype Passive CSV",
                "datatable_hull_archetype_passive.csv 를 읽어 패시브 데이터를 갱신합니다.\n기존 데이터는 삭제됩니다.", "Import", "Cancel"))
            {
                string csvText = System.IO.File.ReadAllText(csvPath, System.Text.Encoding.UTF8);
                dataTable.LoadFromCsv(csvText);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete",
                    $"Import 완료!\n패시브 데이터: {dataTable.GetPassiveDataList().Count}개", "OK");
            }
        }

        EditorGUILayout.EndVertical();
    }
}
#endif
