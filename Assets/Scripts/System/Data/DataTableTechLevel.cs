// 기술 레벨 ScriptableObject — ship_count, requiredTechPoint 관리
// CSV: Assets/Resources/DataTable/Tech/datatable_tech_level.csv
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "DataTableTechLevel", menuName = "Custom/DataTableTechLevel")]
public class DataTableTechLevel : ScriptableObject
{
    [SerializeField] private List<TechLevelData> techLevelDataList = new();

    private int m_cachedMaxShipCount = -1;

    public List<TechLevelData> GetTechLevelDataList() { return techLevelDataList; }

    public int GetMaxShipCount()
    {
        if (m_cachedMaxShipCount >= 0) return m_cachedMaxShipCount;
        RebuildCache();
        return m_cachedMaxShipCount;
    }

    public int GetShipCount(int techLevel)
    {
        for (int i = 0; i < techLevelDataList.Count; i++)
        {
            if (techLevelDataList[i].targetTechLevel == techLevel)
                return techLevelDataList[i].shipCount;
        }
        return 1;
    }

    public int GetRequiredTechPoint(int techLevel)
    {
        for (int i = 0; i < techLevelDataList.Count; i++)
        {
            if (techLevelDataList[i].targetTechLevel == techLevel)
                return techLevelDataList[i].requiredTechPoint;
        }
        return 0;
    }

    // shipCount번째 함선을 허용하는 최소 기술레벨 반환
    public int GetRequiredTechLevel(int shipCount)
    {
        int minLevel = int.MaxValue;
        for (int i = 0; i < techLevelDataList.Count; i++)
        {
            var data = techLevelDataList[i];
            if (data.shipCount >= shipCount && data.targetTechLevel < minLevel)
                minLevel = data.targetTechLevel;
        }
        return minLevel == int.MaxValue ? 1 : minLevel;
    }

    private void RebuildCache()
    {
        int max = 1;
        for (int i = 0; i < techLevelDataList.Count; i++)
        {
            if (techLevelDataList[i].shipCount > max)
                max = techLevelDataList[i].shipCount;
        }
        m_cachedMaxShipCount = max;
    }

    #region JSON Export/Import

    public string ExportToJson()
    {
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
        };
        return Newtonsoft.Json.JsonConvert.SerializeObject(techLevelDataList, settings);
    }

    public void ImportFromJson(string json)
    {
        var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<TechLevelData>>(json);
        if (list != null)
        {
            techLevelDataList = list;
            RebuildCache();
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    #endregion

    #region CSV Import (Editor only)

#if UNITY_EDITOR
    // datatable_tech_level.csv — tech_level, require_tech_point, ship_count
    public void LoadFromCsv(string csvText)
    {
        techLevelDataList.Clear();

        string[] lines = csvText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        if (lines.Length < 2) return;

        string[] headers = ParseCsvLine(lines[0].Trim());
        var col = new Dictionary<string, int>();
        for (int i = 0; i < headers.Length; i++)
            col[headers[i].Trim()] = i;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = ParseCsvLine(line);
            bool parsed = int.TryParse(GetCol(cols, col, "tech_level"), out int targetLevel);
            if (parsed == false || targetLevel <= 0) continue;

            techLevelDataList.Add(new TechLevelData
            {
                targetTechLevel   = targetLevel,
                requiredTechPoint = ParseInt(GetCol(cols, col, "require_tech_point")),
                shipCount         = ParseInt(GetCol(cols, col, "ship_count")),
            });
        }

        RebuildCache();
        Debug.Log($"[DataTableTechLevel] CSV Import 완료: {techLevelDataList.Count}개");
        EditorUtility.SetDirty(this);
    }

    private string GetCol(string[] cols, Dictionary<string, int> colMap, string name)
    {
        if (colMap.TryGetValue(name, out int idx) == false || idx >= cols.Length) return "";
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
        s = s.Replace(",", "").Trim();
        return int.TryParse(s, out int r) ? r : 0;
    }
#endif

    #endregion
}

[System.Serializable]
public class TechLevelData
{
    public int targetTechLevel;
    public int requiredTechPoint;
    public int shipCount;
}
