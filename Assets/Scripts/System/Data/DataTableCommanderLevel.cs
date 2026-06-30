// 커맨더 레벨 ScriptableObject — require_exp_base_stage(기준 스테이지), ship_count 관리
// requireExp는 DataTableZone 누적 exp로부터 계산됨 (ApplyRequireExpFromZone)
// CSV: Assets/Resources/DataTable/Commander/datatable_commander_level.csv
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "DataTableCommanderLevel", menuName = "Custom/DataTableCommanderLevel")]
public class DataTableCommanderLevel : ScriptableObject
{
    [SerializeField] private List<CommanderLevelData> commanderLevelDataList = new();

    private int m_cachedMaxShipCount = -1;

    public List<CommanderLevelData> GetCommanderLevelDataList() { return commanderLevelDataList; }

    public int GetMaxShipCount()
    {
        if (m_cachedMaxShipCount >= 0) return m_cachedMaxShipCount;
        RebuildCache();
        return m_cachedMaxShipCount;
    }

    public int GetShipCount(int commanderLevel)
    {
        for (int i = 0; i < commanderLevelDataList.Count; i++)
        {
            if (commanderLevelDataList[i].commanderLevel == commanderLevel)
                return commanderLevelDataList[i].shipCount;
        }
        return 1;
    }

    public int GetRequireExp(int commanderLevel)
    {
        for (int i = 0; i < commanderLevelDataList.Count; i++)
        {
            if (commanderLevelDataList[i].commanderLevel == commanderLevel)
                return commanderLevelDataList[i].requireExp;
        }
        return 0;
    }

    // 해당 레벨 도달 시 지급되는 모듈포인트 보상
    public int GetModulePointReward(int commanderLevel)
    {
        for (int i = 0; i < commanderLevelDataList.Count; i++)
        {
            if (commanderLevelDataList[i].commanderLevel == commanderLevel)
                return commanderLevelDataList[i].modulePointReward;
        }
        return 0;
    }

    // 해당 커맨더 레벨에서 허용되는 모듈 서브타입 등급 상한
    public int GetSubtypeLevel(int commanderLevel)
    {
        for (int i = 0; i < commanderLevelDataList.Count; i++)
        {
            if (commanderLevelDataList[i].commanderLevel == commanderLevel)
                return commanderLevelDataList[i].subtypeLevel;
        }
        return 1;
    }

    // shipCount번째 함선을 허용하는 최소 커맨더 레벨 반환
    public int GetRequiredCommanderLevel(int shipCount)
    {
        int minLevel = int.MaxValue;
        for (int i = 0; i < commanderLevelDataList.Count; i++)
        {
            var data = commanderLevelDataList[i];
            if (data.shipCount >= shipCount && data.commanderLevel < minLevel)
                minLevel = data.commanderLevel;
        }
        return minLevel == int.MaxValue ? 1 : minLevel;
    }

    private void RebuildCache()
    {
        int max = 1;
        for (int i = 0; i < commanderLevelDataList.Count; i++)
        {
            if (commanderLevelDataList[i].shipCount > max)
                max = commanderLevelDataList[i].shipCount;
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
        return Newtonsoft.Json.JsonConvert.SerializeObject(commanderLevelDataList, settings);
    }

    public void ImportFromJson(string json)
    {
        var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CommanderLevelData>>(json);
        if (list != null)
        {
            commanderLevelDataList = list;
            RebuildCache();
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    #endregion

    #region CSV Import (Editor only)

#if UNITY_EDITOR
    // datatable_commander_level.csv 컬럼 순서(인덱스 고정) — commander_level, require_exp_base_stage, module_point_reward, ship_count, subtype_level
    public void LoadFromCsv(string csvText)
    {
        commanderLevelDataList.Clear();

        string[] lines = csvText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        if (lines.Length < 2) return;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = ParseCsvLine(line);
            bool parsed = int.TryParse(GetCol(cols, 0), out int level);
            if (parsed == false || level <= 0) continue;

            commanderLevelDataList.Add(new CommanderLevelData
            {
                commanderLevel      = level,
                requireExpBaseStage = GetCol(cols, 1),
                modulePointReward   = ParseInt(GetCol(cols, 2)),
                shipCount           = ParseInt(GetCol(cols, 3)),
                subtypeLevel        = ParseInt(GetCol(cols, 4)),
            });
        }

        RebuildCache();
        Debug.Log($"[DataTableCommanderLevel] CSV Import 완료: {commanderLevelDataList.Count}개");
        EditorUtility.SetDirty(this);
    }

    // require_exp_base_stage가 가리키는 스테이지까지 zoneTable 누적 exp를 requireExp에 반영
    public void ApplyRequireExpFromZone(DataTableZone zoneTable)
    {
        if (zoneTable == null) return;

        for (int i = 0; i < commanderLevelDataList.Count; i++)
        {
            var data = commanderLevelDataList[i];
            data.requireExp = zoneTable.GetCumulativeExpUpToStage(data.requireExpBaseStage);
        }

        Debug.Log($"[DataTableCommanderLevel] requireExp 계산 완료: {commanderLevelDataList.Count}개");
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
public class CommanderLevelData
{
    public int commanderLevel;
    public string requireExpBaseStage; // CSV 원본: 이 스테이지까지 클리어 시 누적 exp가 requireExp가 됨
    public int requireExp;             // requireExpBaseStage 기준으로 계산됨 (ApplyRequireExpFromZone)
    public int modulePointReward;      // 레벨업 시 지급되는 모듈포인트
    public int shipCount;
    public int subtypeLevel;           // 이 레벨에서 허용되는 모듈 서브타입 등급 상한
}
