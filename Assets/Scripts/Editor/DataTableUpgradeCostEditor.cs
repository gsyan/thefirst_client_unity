// DataTableUpgradeCost 커스텀 에디터 — 등급업 비용 데이터 Inspector UI 및 CSV Import 툴
// CSV 경로: Assets/Resources/DataTable/Module/datatable_upgrade_cost.csv

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DataTableUpgradeCost))]
public class DataTableUpgradeCostEditor : Editor
{
    private DataTableUpgradeCost dataTable;
    private bool m_foldout = false;

    private void OnEnable()
    {
        dataTable = (DataTableUpgradeCost)target;
    }

    public override void OnInspectorGUI()
    {
        if (dataTable == null) return;

        serializedObject.Update();

        DrawCsvTools();
        EditorGUILayout.Space(10);
        DrawUpgradeCostList();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(dataTable);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawUpgradeCostList()
    {
        var list = dataTable.GetUpgradeCostDataList();

        EditorGUILayout.BeginVertical("box");
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.8f, 0.8f, 1.0f);
        m_foldout = EditorGUILayout.Foldout(m_foldout, $"Upgrade Cost Data ({list.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (m_foldout)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var data = list[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Grade {data.subtypeGrade}", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                data.subtypeGrade    = EditorGUILayout.IntField("Subtype Grade",     data.subtypeGrade);
                data.modulePointCost = EditorGUILayout.IntField("Module Point Cost", data.modulePointCost);
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

        string csvPath = Application.dataPath + "/Resources/DataTable/Module/datatable_upgrade_cost.csv";

        if (GUILayout.Button("Import Upgrade Cost CSV"))
        {
            if (System.IO.File.Exists(csvPath) == false)
            {
                EditorUtility.DisplayDialog("Error", $"파일 없음:\n{csvPath}", "OK");
            }
            else if (EditorUtility.DisplayDialog("Import Upgrade Cost CSV",
                "datatable_upgrade_cost.csv 를 읽어 등급업 비용 데이터를 갱신합니다.\n기존 데이터는 삭제됩니다.", "Import", "Cancel"))
            {
                string csvText = System.IO.File.ReadAllText(csvPath, System.Text.Encoding.UTF8);
                dataTable.LoadFromCsv(csvText);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete",
                    $"Import 완료!\n등급업 비용: {dataTable.GetUpgradeCostDataList().Count}개", "OK");
            }
        }

        EditorGUILayout.EndVertical();
    }
}
#endif
