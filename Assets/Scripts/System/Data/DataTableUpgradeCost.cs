// 모듈 등급업 비용 ScriptableObject — subtype_grade(tier)별 module_point_cost 관리 (DataTableResearch 대체, prerequisites 개념 폐기)
// CSV: Assets/Resources/DataTable/Module/datatable_upgrade_cost.csv
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "DataTableUpgradeCost", menuName = "Custom/DataTableUpgradeCost")]
public class DataTableUpgradeCost : ScriptableObject
{
    [SerializeField] private List<UpgradeCostData> upgradeCostDataList = new();

    public List<UpgradeCostData> GetUpgradeCostDataList() { return upgradeCostDataList; }

    public long GetCost(int subtypeGrade)
    {
        for (int i = 0; i < upgradeCostDataList.Count; i++)
        {
            if (upgradeCostDataList[i].subtypeGrade == subtypeGrade)
                return upgradeCostDataList[i].modulePointCost;
        }
        return 0;
    }

    public long GetCost(EModuleSubType subType)
    {
        return GetCost(subType.GetTechTier());
    }

    #region JSON Export/Import

    public string ExportToJson()
    {
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
        };
        return Newtonsoft.Json.JsonConvert.SerializeObject(upgradeCostDataList, settings);
    }

    public void ImportFromJson(string json)
    {
        var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<UpgradeCostData>>(json);
        if (list != null)
        {
            upgradeCostDataList = list;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    #endregion

    #region CSV Import (Editor only)

#if UNITY_EDITOR
    // datatable_upgrade_cost.csv 컬럼 순서(인덱스 고정) — subtype_grade, module_point_cost
    public void LoadFromCsv(string csvText)
    {
        upgradeCostDataList.Clear();

        string[] lines = csvText.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
        if (lines.Length < 2) return;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = ParseCsvLine(line);
            bool parsed = int.TryParse(GetCol(cols, 0), out int grade);
            if (parsed == false || grade <= 0) continue;

            upgradeCostDataList.Add(new UpgradeCostData
            {
                subtypeGrade    = grade,
                modulePointCost = ParseLong(GetCol(cols, 1)),
            });
        }

        Debug.Log($"[DataTableUpgradeCost] CSV Import 완료: {upgradeCostDataList.Count}개");
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

    private long ParseLong(string s)
    {
        s = s.Replace(",", "").Trim();
        return long.TryParse(s, out long r) ? r : 0L;
    }
#endif

    #endregion
}

[System.Serializable]
public class UpgradeCostData
{
    public int subtypeGrade;
    public long modulePointCost;
}
