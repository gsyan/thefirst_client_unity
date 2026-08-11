// DataTableRewardCard 커스텀 에디터 — 보상카드 Inspector UI 및 CSV/서버 JSON Import/Export 툴
// CSV 경로: Assets/Resources/DataTable/RewardCard/datatable_reward_card.csv
// 서버 JSON 경로: Assets/Resources/DataTable/RewardCard/DataTableRewardCard.json (서버 리소스 폴더에 수동 배치)

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DataTableRewardCard))]
public class DataTableRewardCardEditor : Editor
{
    private DataTableRewardCard dataTable;

    private void OnEnable()
    {
        dataTable = (DataTableRewardCard)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawCsvTools();
        EditorGUILayout.Space();

        SerializedProperty listProp = serializedObject.FindProperty("rewardCardDataList");
        EditorGUILayout.LabelField($"보상카드 목록 ({listProp.arraySize}개)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(listProp, true);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCsvTools()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("CSV / 서버 JSON Import / Export", EditorStyles.boldLabel);

        string csvPath = Application.dataPath + "/Resources/DataTable/RewardCard/datatable_reward_card.csv";
        string serverJsonPath = Application.dataPath + "/Resources/DataTable/RewardCard/DataTableRewardCard.json";

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Import CSV"))
        {
            if (System.IO.File.Exists(csvPath) == false)
            {
                EditorUtility.DisplayDialog("Error", $"파일 없음:\n{csvPath}", "OK");
            }
            else if (EditorUtility.DisplayDialog("Import Reward Card CSV",
                "datatable_reward_card.csv 를 읽어 보상카드 데이터를 갱신합니다.\n기존 데이터는 삭제됩니다.", "Import", "Cancel"))
            {
                string csvText = System.IO.File.ReadAllText(csvPath, System.Text.Encoding.UTF8);
                dataTable.LoadCsv(csvText);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Complete",
                    $"Import 완료!\n보상카드: {dataTable.GetRewardCardDataList().Count}개", "OK");
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

        if (GUILayout.Button("Export 서버용 JSON"))
        {
            string serverJson = dataTable.ExportToServerJson();
            System.IO.File.WriteAllText(serverJsonPath, serverJson, System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Complete", $"서버용 JSON Export가 완료되었습니다.\n{serverJsonPath}\n\n이 파일을 서버 src/main/resources/gamedata/ 폴더에 수동 배치해야 합니다.", "OK");
        }

        EditorGUILayout.EndVertical();
    }
}
#endif
