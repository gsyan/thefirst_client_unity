// 연구 트리 ScriptableObject - 모듈 연구 노드 데이터 및 선행 조건 관리
// CSV Import(에디터 전용): datatable_research.csv 기반 로드 지원, prerequisites는 '|' 구분
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class ResearchNodeData
{
    public string researchId; // 고유 식별자 겸 로컬라이제이션 키
    public List<string> prerequisiteIds = new List<string>();
    public CostStruct researchCost = new CostStruct();
    [Newtonsoft.Json.JsonIgnore] public Vector2 uiPosition;
}

[System.Serializable]
public class ModuleResearchData : ResearchNodeData
{
    public EModuleType moduleType = EModuleType.none;
    public EModuleSubType moduleSubType = EModuleSubType.none;
}

[CreateAssetMenu(fileName = "DataTableResearch", menuName = "Custom/DataTableResearch")]
public class DataTableResearch : ScriptableObject
{
    [Header("Research Data")]
    [SerializeField] private List<ModuleResearchData> researchDataList = new();

    public List<ModuleResearchData> ResearchDataList => researchDataList;

    #region Public Methods

    public ModuleResearchData GetResearchData(EModuleSubType subType)
    {
        return researchDataList.Find(r => r.moduleSubType == subType);
    }

    public CostStruct GetResearchCost(EModuleSubType subType)
    {
        var data = GetResearchData(subType);
        return data?.researchCost ?? new CostStruct();
    }

    // 선행 연구 조건을 모두 충족하는지 확인
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

    // 특정 모듈 타입의 연구 데이터만 반환
    public List<ModuleResearchData> GetResearchDataByType(EModuleType moduleType)
    {
        return researchDataList.FindAll(r => r.moduleType == moduleType);
    }

    public void InitializeResearchData()
    {
        researchDataList.Clear();

        // Add research data for each subtype
        foreach (EModuleSubType subType in System.Enum.GetValues(typeof(EModuleSubType)))
        {
            if (subType == EModuleSubType.none) continue;

            EModuleType moduleType = CommonUtility.GetModuleTypeFromSubType(subType);

            // subType의 마지막 자리 숫자로 tier 결정 (1→1000, 2→10000, 3→100000)
            int tier = (int)subType % 10;
            if (tier < 1) continue;
            long researchCost = 10000L * (long)System.Math.Pow(10, tier - 1);

            // UI 배치: 같은 그룹(선후행 관계)은 좌→우, 다른 그룹은 위→아래
            int group = ((int)subType % 100) / 10; // 0: x001~x009, 1: x011~x019
            var vector2Position = new Vector2(80 + (tier - 1) * 200, -40 - group * 120);

            // 같은 모듈 타입 내에서 tier가 낮은 것을 선행 조건으로 설정
            var prerequisiteIds = new List<string>();
            
            if (tier > 1)
            {
                EModuleSubType prevTier = (EModuleSubType)((int)subType - 1);
                if (System.Enum.IsDefined(typeof(EModuleSubType), prevTier))
                {
                    prerequisiteIds.Add(prevTier.ToString());
                }
            }

            var researchData = new ModuleResearchData
            {
                researchId = subType.ToString(),
                moduleType = moduleType,
                moduleSubType = subType,
                prerequisiteIds = prerequisiteIds,
                researchCost = new CostStruct(1, researchCost, 0, 0, 0),
                uiPosition = vector2Position,
            };

            researchDataList.Add(researchData);
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
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

    #region CSV Import/Export

#if UNITY_EDITOR
    public void LoadFromCsv(string csvText)
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

            if (!int.TryParse(GetCol(cols, col, "module_type"), out int typeInt)) continue;
            if (!int.TryParse(GetCol(cols, col, "module_sub_type"), out int subTypeInt)) continue;

            EModuleType moduleType = (EModuleType)typeInt;
            EModuleSubType moduleSubType = (EModuleSubType)subTypeInt;

            string researchId = GetCol(cols, col, "research_id");
            if (string.IsNullOrEmpty(researchId))
                researchId = moduleSubType.ToString();

            var data = new ModuleResearchData
            {
                researchId      = researchId,
                moduleType      = moduleType,
                moduleSubType   = moduleSubType,
                prerequisiteIds = ParseCsvStringList(GetCol(cols, col, "prerequisites")),
                uiPosition      = new Vector2(
                    ParseCsvFloat(GetCol(cols, col, "ui_pos_x")),
                    ParseCsvFloat(GetCol(cols, col, "ui_pos_y"))),
                researchCost    = new CostStruct(
                    ParseCsvInt (GetCol(cols, col, "tech_level")),
                    ParseCsvLong(GetCol(cols, col, "cost_m")),
                    ParseCsvLong(GetCol(cols, col, "cost_mr")),
                    ParseCsvLong(GetCol(cols, col, "cost_me")),
                    ParseCsvLong(GetCol(cols, col, "cost_md")))                
            };

            researchDataList.Add(data);
        }

        Debug.Log($"[DataTableResearch] CSV Import 완료: {researchDataList.Count}개 연구");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private List<string> ParseCsvStringList(string s)
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

    private float ParseCsvFloat(string s)
    {
        s = s.Replace(",", "").Trim();
        return float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float r) ? r : 0f;
    }

    private int ParseCsvInt(string s) { s = s.Trim(); return int.TryParse(s, out int r) ? r : 1; }
    private long ParseCsvLong(string s) { s = s.Replace(",", "").Trim(); return long.TryParse(s, out long r) ? r : 0L; }
#endif

    #endregion

    #region JSON Export/Import

    public string ExportToJson()
    {
        var exportData = new ModuleResearchExportData
        {
            researchDataList = researchDataList
        };
        return Newtonsoft.Json.JsonConvert.SerializeObject(exportData, Newtonsoft.Json.Formatting.Indented);
    }

    public void ImportFromJson(string json)
    {
        var importData = Newtonsoft.Json.JsonConvert.DeserializeObject<ModuleResearchExportData>(json);
        if (importData != null)
        {
            researchDataList = importData.researchDataList;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    [System.Serializable]
    private class ModuleResearchExportData
    {
        public List<ModuleResearchData> researchDataList;
    }

    #endregion
}
