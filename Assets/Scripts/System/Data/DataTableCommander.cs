// 커맨더 레벨 ScriptableObject — 레벨별 필요 경험치(exp), 지휘력(command_power) 관리
// 함대 슬롯 칸 수는 더 이상 레벨로 제한하지 않음 — DataTableConfig.GeneralSettings.maxFleetSlotCount(고정 상한), 배치 가능 여부는 지휘력만으로 결정
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

    public List<CommanderData> GetCommanderDataList() { return commanderDataList; }

    // 지휘력(캐파) 최대치 — 광물/탐험포인트 구매 없이 커맨더 레벨에 따라 자동으로 오름(M13)
    public int GetCommandPower(int commanderLevel)
    {
        for (int i = 0; i < commanderDataList.Count; i++)
        {
            if (commanderDataList[i].commanderLevel == commanderLevel)
                return commanderDataList[i].commandPower;
        }
        return 0;
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
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    #endregion

    #region CSV Import (Editor only)

#if UNITY_EDITOR
    // datatable_commander.csv 컬럼 순서(인덱스 고정) — commander_level, exp, command_power
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
                commandPower   = ParseInt(GetCol(cols, 2)),
            });
        }

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
    public int requireExp; // CSV 원본 컬럼명: exp — 이 레벨에 도달하기 위해 필요한 누적 경험치
    public int commandPower; // 이 레벨에서의 지휘력(캐파) 최대치 — M13부터 구매 없이 레벨만으로 결정
}
