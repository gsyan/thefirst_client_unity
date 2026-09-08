// 업적 데이터 테이블 ScriptableObject
// CSV Import(에디터 전용) → ScriptableObject 갱신 → JSON Export → 서버 배포 순서로 사용 (DataTableRewardCard와 동일 컨벤션)
// CSV: Assets/Resources/DataTable/Achievement/datatable_achievement.csv
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "DataTableAchievement", menuName = "Custom/DataTableAchievement")]
public class DataTableAchievement : ScriptableObject
{
    [SerializeField] private List<AchievementData> achievementDataList = new();

    public List<AchievementData> GetAchievementDataList() { return achievementDataList; }

    public AchievementData GetAchievement(string achievementId)
    {
        for (int i = 0; i < achievementDataList.Count; i++)
        {
            if (achievementDataList[i].achievementId == achievementId)
                return achievementDataList[i];
        }
        return null;
    }

    #region JSON Export/Import

    public string ExportToJson()
    {
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
        };
        return Newtonsoft.Json.JsonConvert.SerializeObject(achievementDataList, settings);
    }

    public void ImportFromJson(string json)
    {
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() }
        };
        var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<AchievementData>>(json, settings);
        if (list != null)
        {
            achievementDataList = list;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    #endregion

    #region CSV Import/Export (Editor only)

#if UNITY_EDITOR
    public string ExportCsv()
    {
        var ic = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("achievement_id,condition_type,condition_param,threshold,achievement_point_reward,name_key,desc_key");

        for (int i = 0; i < achievementDataList.Count; i++)
        {
            AchievementData data = achievementDataList[i];
            sb.AppendLine(string.Format(ic, "{0},{1},{2},{3},{4},{5},{6}",
                data.achievementId, data.conditionType, data.conditionParam,
                data.threshold, data.achievementPointReward, data.nameKey, data.descKey));
        }

        return sb.ToString();
    }

    public void LoadCsv(string csvText)
    {
        achievementDataList.Clear();

        string[] lines = csvText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        if (lines.Length < 2) return;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = ParseCsvLine(line);
            string achievementId = GetCol(cols, 0);
            if (string.IsNullOrEmpty(achievementId)) continue;

            if (System.Enum.TryParse(GetCol(cols, 1), out EAchievementConditionType conditionType) == false)
            {
                Debug.LogWarning($"[DataTableAchievement] 알 수 없는 condition_type '{GetCol(cols, 1)}' (줄 {i + 1})");
                continue;
            }

            achievementDataList.Add(new AchievementData
            {
                achievementId = achievementId,
                conditionType = conditionType,
                conditionParam = GetCol(cols, 2),
                threshold = ParseInt(GetCol(cols, 3)),
                achievementPointReward = ParseInt(GetCol(cols, 4)),
                nameKey = GetCol(cols, 5),
                descKey = GetCol(cols, 6),
            });
        }

        Debug.Log($"[DataTableAchievement] CSV Import 완료: {achievementDataList.Count}개");
        EditorUtility.SetDirty(this);
    }

    private string GetCol(string[] cols, int idx)
    {
        if (idx >= cols.Length) return "";
        return cols[idx].Trim();
    }

    private string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();
        foreach (char c in line)
        {
            if (c == '"')
                inQuotes = !inQuotes;
            else if (c == ',' && inQuotes == false)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
                current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    private int ParseInt(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        return int.TryParse(s, out int r) ? r : 0;
    }
#endif

    #endregion
}
