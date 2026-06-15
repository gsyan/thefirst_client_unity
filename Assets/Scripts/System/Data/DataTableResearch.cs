// 모듈 연구 트리 ScriptableObject — 모듈 해금/교체 비용 관리
// CSV: datatable_research_subtype.csv (모듈 서브타입)
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class ResearchNodeData
{
    public string researchId;
    public List<string> prerequisiteIds = new List<string>();
}

[System.Serializable]
public class ModuleResearchData : ResearchNodeData
{
    public EModuleType moduleType = EModuleType.none;
    public EModuleSubType moduleSubType = EModuleSubType.none;
    public int pointCost;
}

[CreateAssetMenu(fileName = "DataTableResearch", menuName = "Custom/DataTableResearch")]
public class DataTableResearch : ScriptableObject
{
    [Header("Research Data")]
    [SerializeField] private List<ModuleResearchData> researchDataList = new();

    public List<ModuleResearchData> GetResearchDataList() { return researchDataList; }

    #region Public Methods

    public ModuleResearchData GetResearchData(EModuleSubType subType)
    {
        return researchDataList.Find(r => r.moduleSubType == subType);
    }

    public long GetResearchCost(EModuleSubType subType)
    {
        var data = GetResearchData(subType);
        if (data == null) return 0;
        return data.pointCost;
    }

    public bool ArePrerequisitesMet(string researchId, HashSet<string> completedResearchIds)
    {
        var data = researchDataList.Find(r => r.researchId == researchId);
        if (data == null) return false;
        if (data.prerequisiteIds == null || data.prerequisiteIds.Count == 0) return true;

        for (int i = 0; i < data.prerequisiteIds.Count; i++)
        {
            if (completedResearchIds.Contains(data.prerequisiteIds[i]) == false)
                return false;
        }
        return true;
    }

    public List<ModuleResearchData> GetResearchDataByType(EModuleType moduleType)
    {
        return researchDataList.FindAll(r => r.moduleType == moduleType);
    }

    #endregion

    #region Validation

    public bool ValidateData()
    {
        if (researchDataList.Count == 0)
        {
            Debug.LogWarning("No research data defined!");
            return false;
        }
        return true;
    }

    #endregion

    #region CSV Import (Editor only)

#if UNITY_EDITOR
    // datatable_research_subtype.csv — ui_pos_x/y 컬럼 포함
    public void LoadSubtypeFromCsv(string csvText)
    {
        researchDataList.Clear();

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
            string researchId = GetCol(cols, col, "research_id");
            if (string.IsNullOrEmpty(researchId)) continue;

            if (System.Enum.TryParse(researchId, out EModuleSubType moduleSubType) == false) continue;
            researchDataList.Add(new ModuleResearchData
            {
                researchId      = researchId,
                moduleType      = (EModuleType)moduleSubType.GetModuleType(),
                moduleSubType   = moduleSubType,
                prerequisiteIds = ParseStringList(GetCol(cols, col, "prerequisites")),
                pointCost       = (int)ParseLong(GetCol(cols, col, "cost_mp")),
            });
        }

        Debug.Log($"[DataTableResearch] CSV Import 완료: 모듈 {researchDataList.Count}개");
        EditorUtility.SetDirty(this);
    }

    private List<string> ParseStringList(string s)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(s)) return list;
        string[] parts = s.Split('|');
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i].Trim();
            if (string.IsNullOrEmpty(part) == false)
                list.Add(part);
        }
        return list;
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

    private long ParseLong(string s)
    {
        s = s.Replace(",", "").Trim();
        return long.TryParse(s, out long r) ? r : 0L;
    }
#endif

    #endregion

    #region JSON Export/Import

    public string ExportToJson()
    {
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
        };
        return Newtonsoft.Json.JsonConvert.SerializeObject(researchDataList, settings);
    }

    public void ImportFromJson(string json)
    {
        var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ModuleResearchData>>(json);
        if (list != null)
        {
            researchDataList = list;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }

    #endregion
}
