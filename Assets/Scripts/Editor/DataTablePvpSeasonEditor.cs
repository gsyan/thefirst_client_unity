// DataTablePvpSeason 커스텀 에디터 - 티어별 PvP 시즌 점수/보상 Inspector UI
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DataTablePvpSeason))]
public class DataTablePvpSeasonEditor : Editor
{
    private DataTablePvpSeason dataTable;

    private readonly Color[] tierColors = new Color[]
    {
        new Color(0.72f, 0.45f, 0.20f), // Bronze
        new Color(0.75f, 0.75f, 0.75f), // Silver
        new Color(1.0f,  0.84f, 0.0f),  // Gold
        new Color(0.68f, 0.85f, 0.90f), // Platinum
        new Color(0.54f, 0.81f, 0.94f), // Diamond
    };

    private void OnEnable()
    {
        dataTable = (DataTablePvpSeason)target;
    }

    public override void OnInspectorGUI()
    {
        if (dataTable == null) return;
        serializedObject.Update();

        EditorGUILayout.BeginHorizontal("box");
        EditorGUILayout.LabelField("PVP Season Config", EditorStyles.largeLabel);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);

        // 기본 시즌 기간
        dataTable.config.defaultSeasonDurationDays = EditorGUILayout.IntField(
            "기본 시즌 기간 (일)", dataTable.config.defaultSeasonDurationDays);
        EditorGUILayout.Space(10);

        // 티어 테이블 헤더
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Tier Table", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Tier",        GUILayout.Width(80));
        EditorGUILayout.LabelField("Min Score",   GUILayout.Width(80));
        EditorGUILayout.LabelField("Reset Score", GUILayout.Width(90));
        EditorGUILayout.LabelField("Reward",      GUILayout.Width(60));
        EditorGUILayout.LabelField("",            GUILayout.Width(30));
        EditorGUILayout.EndHorizontal();

        var tiers = dataTable.config.tiers;
        int removeIndex = -1;

        for (int i = 0; i < tiers.Count; i++)
        {
            var tier = tiers[i];
            Color bgColor = i < tierColors.Length ? tierColors[i] : Color.white;
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;

            EditorGUILayout.BeginHorizontal("box");
            GUI.backgroundColor = prev;

            tier.tierName    = EditorGUILayout.TextField(tier.tierName,    GUILayout.Width(80));
            tier.minScore    = EditorGUILayout.IntField(tier.minScore,     GUILayout.Width(80));
            tier.resetScore  = EditorGUILayout.IntField(tier.resetScore,   GUILayout.Width(90));
            tier.seasonReward= EditorGUILayout.IntField(tier.seasonReward, GUILayout.Width(60));

            if (GUILayout.Button("-", GUILayout.Width(25)))
                removeIndex = i;

            EditorGUILayout.EndHorizontal();
        }

        if (removeIndex >= 0)
            tiers.RemoveAt(removeIndex);

        EditorGUILayout.Space(3);
        if (GUILayout.Button("+ Add Tier"))
        {
            tiers.Add(new PvpSeasonTierEntry { tierName = "NewTier", minScore = 0, resetScore = 1000, seasonReward = 2 });
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // JSON Export/Import
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("JSON Export / Import", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Export JSON"))
        {
            string path = EditorUtility.SaveFilePanel("Export DataTablePvpSeason", "", "DataTablePvpSeason.json", "json");
            if (string.IsNullOrEmpty(path) == false)
            {
                System.IO.File.WriteAllText(path, dataTable.ExportToJson(), System.Text.Encoding.UTF8);
                EditorUtility.DisplayDialog("완료", $"Export 완료:\n{path}", "OK");
            }
        }

        if (GUILayout.Button("Import JSON"))
        {
            string path = EditorUtility.OpenFilePanel("Import DataTablePvpSeason", "", "json");
            if (string.IsNullOrEmpty(path) == false)
            {
                string json = System.IO.File.ReadAllText(path, System.Text.Encoding.UTF8);
                dataTable.ImportFromJson(json);
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("완료", "Import 완료", "OK");
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(dataTable);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
