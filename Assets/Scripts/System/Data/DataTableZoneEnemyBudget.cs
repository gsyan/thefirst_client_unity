// 존 번호별 셀 적함대 지휘력 예산 규칙표 ScriptableObject — DataTableZoneGridSize와 동일한 패턴
// CSV Import(에디터 전용) → ScriptableObject 갱신 → JSON Export → 서버 배포 순서로 사용
// CSV: Assets/Resources/DataTable/Exploration/datatable_zone_enemy_budget.csv
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "DataTableZoneEnemyBudget", menuName = "Custom/DataTableZoneEnemyBudget")]
public class DataTableZoneEnemyBudget : ScriptableObject
{
    [SerializeField] private List<ZoneEnemyBudgetData> zoneEnemyBudgetDataList = new();

    public List<ZoneEnemyBudgetData> GetZoneEnemyBudgetDataList() { return zoneEnemyBudgetDataList; }

    // zoneNumber가 속한 구간의 셀당 지휘력 예산 반환. CSV는 zone_max 오름차순 정렬 전제
    // zoneMin은 저장하지 않음 — 이전 행의 zoneMax+1로 유추 가능한 중복 데이터이므로 제외
    public int GetCommandPowerBudget(int zoneNumber)
    {
        for (int i = 0; i < zoneEnemyBudgetDataList.Count; i++)
        {
            ZoneEnemyBudgetData data = zoneEnemyBudgetDataList[i];
            if (zoneNumber <= data.zoneMax)
                return data.commandPowerBudget;
        }

        if (zoneEnemyBudgetDataList.Count > 0)
            return zoneEnemyBudgetDataList[zoneEnemyBudgetDataList.Count - 1].commandPowerBudget;

        return 100;
    }

    #region JSON Export/Import

    public string ExportToJson()
    {
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
        };
        return Newtonsoft.Json.JsonConvert.SerializeObject(zoneEnemyBudgetDataList, settings);
    }

    public void ImportFromJson(string json)
    {
        var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ZoneEnemyBudgetData>>(json);
        if (list != null)
        {
            zoneEnemyBudgetDataList = list;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    #endregion

    #region CSV Import (Editor only)

#if UNITY_EDITOR
    public string ExportToCsv()
    {
        var ic = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("zone_max,command_power_budget");

        for (int i = 0; i < zoneEnemyBudgetDataList.Count; i++)
        {
            ZoneEnemyBudgetData data = zoneEnemyBudgetDataList[i];
            sb.AppendLine(string.Format(ic, "{0},{1}", data.zoneMax, data.commandPowerBudget));
        }

        return sb.ToString();
    }

    // datatable_zone_enemy_budget.csv 컬럼 순서(인덱스 고정) — zone_max, command_power_budget
    public void LoadFromCsv(string csvText)
    {
        zoneEnemyBudgetDataList.Clear();

        string[] lines = csvText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        if (lines.Length < 2) return;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = ParseCsvLine(line);
            bool parsed = int.TryParse(GetCol(cols, 0), out int zoneMax);
            if (parsed == false) continue;

            zoneEnemyBudgetDataList.Add(new ZoneEnemyBudgetData
            {
                zoneMax            = zoneMax,
                commandPowerBudget = ParseInt(GetCol(cols, 1)),
            });
        }

        Debug.Log($"[DataTableZoneEnemyBudget] CSV Import 완료: {zoneEnemyBudgetDataList.Count}개");
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
public class ZoneEnemyBudgetData
{
    public int zoneMax;
    public int commandPowerBudget;
}
