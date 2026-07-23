// 커맨더 레벨 ScriptableObject — 레벨별 필요 포인트(point), 최대 함선 수(ship_count) 관리
// CSV: Assets/Resources/DataTable/Commander/datatable_commander.csv
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "DataTableCommander", menuName = "Custom/DataTableCommander")]
public class DataTableCommander : ScriptableObject
{
    [SerializeField] private List<CommanderData> commanderDataList = new();

    private int m_cachedMaxShipCount = -1;

    public List<CommanderData> GetCommanderDataList() { return commanderDataList; }

    public int GetMaxShipCount()
    {
        if (m_cachedMaxShipCount >= 0) return m_cachedMaxShipCount;
        RebuildCache();
        return m_cachedMaxShipCount;
    }

    public int GetShipCount(int commanderLevel)
    {
        for (int i = 0; i < commanderDataList.Count; i++)
        {
            if (commanderDataList[i].commanderLevel == commanderLevel)
                return commanderDataList[i].shipCount;
        }
        return 1;
    }

    public int GetRequireExp(int commanderLevel)
    {
        for (int i = 0; i < commanderDataList.Count; i++)
        {
            if (commanderDataList[i].commanderLevel == commanderLevel)
                return commanderDataList[i].requireExp;
        }
        return 0;
    }

    // shipCount번째 함선을 허용하는 최소 커맨더 레벨 반환
    public int GetRequiredCommanderLevel(int shipCount)
    {
        int minLevel = int.MaxValue;
        for (int i = 0; i < commanderDataList.Count; i++)
        {
            var data = commanderDataList[i];
            if (data.shipCount >= shipCount && data.commanderLevel < minLevel)
                minLevel = data.commanderLevel;
        }
        return minLevel == int.MaxValue ? 1 : minLevel;
    }

    private void RebuildCache()
    {
        int max = 1;
        for (int i = 0; i < commanderDataList.Count; i++)
        {
            if (commanderDataList[i].shipCount > max)
                max = commanderDataList[i].shipCount;
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
        return Newtonsoft.Json.JsonConvert.SerializeObject(commanderDataList, settings);
    }

    public void ImportFromJson(string json)
    {
        var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CommanderData>>(json);
        if (list != null)
        {
            commanderDataList = list;
            RebuildCache();
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    #endregion

    #region CSV Import (Editor only)

#if UNITY_EDITOR
    // datatable_commander.csv 컬럼 순서(인덱스 고정) — commander_level, point, ship_count
    public void LoadFromCsv(string csvText)
    {
        commanderDataList.Clear();

        string[] lines = csvText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        if (lines.Length < 2) return;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = ParseCsvLine(line);
            bool parsed = int.TryParse(GetCol(cols, 0), out int level);
            if (parsed == false || level <= 0) continue;

            commanderDataList.Add(new CommanderData
            {
                commanderLevel = level,
                requireExp     = ParseInt(GetCol(cols, 1)),
                shipCount      = ParseInt(GetCol(cols, 2)),
            });
        }

        RebuildCache();
        Debug.Log($"[DataTableCommander] CSV Import 완료: {commanderDataList.Count}개");
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
        s = s.Replace(",", "").Trim();
        return int.TryParse(s, out int r) ? r : 0;
    }
#endif

    #endregion
}

[System.Serializable]
public class CommanderData
{
    public int commanderLevel;
    public int requireExp; // CSV 원본 컬럼명: point — 이 레벨에 도달하기 위해 필요한 누적 포인트
    public int shipCount;
}
