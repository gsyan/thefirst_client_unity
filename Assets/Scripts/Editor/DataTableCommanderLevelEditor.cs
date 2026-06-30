// DataTableCommanderLevel 커스텀 에디터 — 커맨더 레벨 데이터 Inspector UI 및 CSV Import 툴
// CSV 경로: Assets/Resources/DataTable/Commander/datatable_commander_level.csv

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DataTableCommanderLevel))]
public class DataTableCommanderLevelEditor : Editor
{
    private DataTableCommanderLevel dataTable;
    private DataTableZone m_dataTableZone;
    private bool m_foldout = false;

    private void OnEnable()
    {
        dataTable = (DataTableCommanderLevel)target;
        AutoAssignZoneTable();
    }

    private void AutoAssignZoneTable()
    {
        if (m_dataTableZone != null) return;
        string[] guids = AssetDatabase.FindAssets("t:DataTableZone", new[] { "Assets/Resources/DataTable" });
        if (guids.Length > 0)
            m_dataTableZone = AssetDatabase.LoadAssetAtPath<DataTableZone>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    public override void OnInspectorGUI()
    {
        if (dataTable == null) return;

        serializedObject.Update();

        DrawCsvTools();
        EditorGUILayout.Space(10);
        DrawCommanderLevelList();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(dataTable);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void DrawCommanderLevelList()
    {
        var list = dataTable.GetCommanderLevelDataList();

        EditorGUILayout.BeginVertical("box");
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.8f, 0.8f, 1.0f);
        m_foldout = EditorGUILayout.Foldout(m_foldout, $"Commander Level Data ({list.Count})", true, EditorStyles.foldoutHeader);
        GUI.backgroundColor = originalColor;

        if (m_foldout)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var data = list[i];
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Level {data.commanderLevel}", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                data.commanderLevel      = EditorGUILayout.IntField("Commander Level", data.commanderLevel);
                data.requireExpBaseStage = EditorGUILayout.TextField("Require Exp Base Stage", data.requireExpBaseStage);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.IntField("Require Exp (계산값)", data.requireExp);
                EditorGUI.EndDisabledGroup();
                data.modulePointReward = EditorGUILayout.IntField("Module Point Reward", data.modulePointReward);
                data.shipCount         = EditorGUILayout.IntField("Ship Count",          data.shipCount);
                data.subtypeLevel      = EditorGUILayout.IntField("Subtype Level",       data.subtypeLevel);
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

        string csvPath = Application.dataPath + "/Resources/DataTable/Commander/datatable_commander_level.csv";

        if (GUILayout.Button("Import Commander Level CSV"))
        {
            if (System.IO.File.Exists(csvPath) == false)
            {
                EditorUtility.DisplayDialog("Error", $"파일 없음:\n{csvPath}", "OK");
            }
            else if (EditorUtility.DisplayDialog("Import Commander Level CSV",
                "datatable_commander_level.csv 를 읽어 커맨더 레벨 데이터를 갱신합니다.\n기존 데이터는 삭제됩니다.", "Import", "Cancel"))
            {
                string csvText = System.IO.File.ReadAllText(csvPath, System.Text.Encoding.UTF8);
                dataTable.LoadFromCsv(csvText);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete",
                    $"Import 완료!\n커맨더 레벨: {dataTable.GetCommanderLevelDataList().Count}개", "OK");
            }
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Require Exp 계산", EditorStyles.boldLabel);

        m_dataTableZone = (DataTableZone)EditorGUILayout.ObjectField(
            "DataTable Zone", m_dataTableZone, typeof(DataTableZone), false);

        EditorGUI.BeginDisabledGroup(m_dataTableZone == null);
        if (GUILayout.Button("Apply Require Exp from Zone"))
        {
            dataTable.ApplyRequireExpFromZone(m_dataTableZone);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Complete", "requireExp 계산 완료!", "OK");
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndVertical();
    }
}
#endif
