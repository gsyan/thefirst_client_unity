#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(DataTableDailyBonus))]
public class DataTableDailyBonusEditor : Editor
{
    private DataTableDailyBonus dataTable;
    private Vector2 scrollPosition;

    private void OnEnable()
    {
        dataTable = (DataTableDailyBonus)target;
    }

    public override void OnInspectorGUI()
    {
        if (dataTable == null) return;
        serializedObject.Update();

        // CSV Import
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("CSV Import", EditorStyles.boldLabel);
        if (GUILayout.Button("Load from CSV"))
        {
            string path = EditorUtility.OpenFilePanel("Import DailyBonus CSV", "Assets/Resources/DataTable/DailyBonus", "csv");
            if (string.IsNullOrEmpty(path) == false)
            {
                string csv = File.ReadAllText(path, System.Text.Encoding.UTF8);
                dataTable.ImportFromCsv(csv);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("완료", $"CSV Import 완료\n{dataTable.days.Length}일치 로드됨", "OK");
            }
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);

        // JSON Export / Import
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("JSON Export / Import", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Export JSON"))
        {
            string path = EditorUtility.SaveFilePanel("Export DataTableDailyBonus", "", "DataTableDailyBonus.json", "json");
            if (string.IsNullOrEmpty(path) == false)
            {
                File.WriteAllText(path, dataTable.ExportToJson(), System.Text.Encoding.UTF8);
                EditorUtility.DisplayDialog("완료", $"Export 완료:\n{path}", "OK");
            }
        }
        if (GUILayout.Button("Import JSON"))
        {
            string path = EditorUtility.OpenFilePanel("Import DataTableDailyBonus", "", "json");
            if (string.IsNullOrEmpty(path) == false)
            {
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                dataTable.ImportFromJson(json);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("완료", "JSON Import 완료", "OK");
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // 테이블 내용 미리보기
        if (dataTable.days != null && dataTable.days.Length > 0)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Days Preview ({dataTable.days.Length}일)", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(300));

            for (int i = 0; i < dataTable.days.Length; i++)
            {
                DailyBonusDayConfig day = dataTable.days[i];
                if (day == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Day {day.day}", GUILayout.Width(50));

                if (day.rewards != null)
                {
                    for (int r = 0; r < day.rewards.Length; r++)
                    {
                        DailyBonusRewardEntry reward = day.rewards[r];
                        EditorGUILayout.LabelField($"[{reward.tier}] {reward.rewardType} +{reward.amount}", GUILayout.Width(180));
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        // 기본 Inspector
        DrawDefaultInspector();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(dataTable);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
