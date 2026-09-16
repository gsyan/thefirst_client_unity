// DataTableDailyBonus 커스텀 에디터 — 출석 보상 Inspector UI 및 CSV Import/Export 툴
// CSV 경로: Assets/Resources/DataTable/DailyBonus/datatable_daily_bonus.csv

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

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

        DrawCsvTools();
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

    private void DrawCsvTools()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("CSV Import / Export", EditorStyles.boldLabel);

        string csvPath = Application.dataPath + "/Resources/DataTable/DailyBonus/datatable_daily_bonus.csv";

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Import CSV"))
        {
            if (System.IO.File.Exists(csvPath) == false)
            {
                EditorUtility.DisplayDialog("Error", $"파일 없음:\n{csvPath}", "OK");
            }
            else if (EditorUtility.DisplayDialog("Import DailyBonus CSV",
                "datatable_daily_bonus.csv 를 읽어 출석 보상 데이터를 갱신합니다.\n기존 데이터는 삭제됩니다.", "Import", "Cancel"))
            {
                string csvText = System.IO.File.ReadAllText(csvPath, System.Text.Encoding.UTF8);
                dataTable.ImportFromCsv(csvText);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete", $"Import 완료!\n{dataTable.days.Length}일치 로드됨", "OK");
            }
        }

        if (GUILayout.Button("Export to CSV"))
        {
            if (EditorUtility.DisplayDialog("Export to CSV",
                $"현재 데이터를 CSV 파일로 덮어씁니다.\n\n{csvPath}\n\n계속하시겠습니까?", "Export", "Cancel"))
            {
                string csv = dataTable.ExportCsv();
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
