// 연구 트리 ScriptableObject - 모듈 연구/기술레벨 노드, 모듈 해금/교체 비용 관리
// CSV 분리: datatable_research_tech.csv (기술레벨), datatable_research_subtype.csv (모듈 서브타입)
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
    public int mineralCost;
    [Newtonsoft.Json.JsonIgnore] public Vector2 uiPosition;
}

[System.Serializable]
public class ModuleResearchData : ResearchNodeData
{
    public EModuleType moduleType = EModuleType.none;
    public EModuleSubType moduleSubType = EModuleSubType.none;
}

[System.Serializable]
public class TechLevelResearchData : ResearchNodeData
{
    public int targetTechLevel; // 이 연구를 완료하면 달성되는 기술레벨
    public int shipCount;       // 이 기술레벨에서의 최대 함선 수
}

[CreateAssetMenu(fileName = "DataTableResearch", menuName = "Custom/DataTableResearch")]
public class DataTableResearch : ScriptableObject
{
    [Header("Research Data")]
    [SerializeField] private List<ModuleResearchData> researchDataList = new();
    [Header("Tech Level Upgrade Data")]
    [SerializeField] private List<TechLevelResearchData> techLevelDataList = new();

    public List<ModuleResearchData> ResearchDataList => researchDataList;
    public List<TechLevelResearchData> TechLevelDataList => techLevelDataList;

    #region Public Methods

    public ModuleResearchData GetResearchData(EModuleSubType subType)
    {
        return researchDataList.Find(r => r.moduleSubType == subType);
    }

    public long GetResearchCost(EModuleSubType subType)
    {
        var data = GetResearchData(subType);
        return data?.mineralCost ?? 0;
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

    // currentLevel → currentLevel+1 업그레이드 비용 반환
    public long GetTechLevelUpgradeCost(int currentLevel)
    {
        var data = techLevelDataList.Find(r => r.targetTechLevel == currentLevel + 1);
        return data?.mineralCost ?? 0;
    }

    // 해당 기술레벨에서 허용되는 최대 함선 수 반환
    public int GetShipCount(int techLevel)
    {
        var data = techLevelDataList.Find(r => r.targetTechLevel == techLevel);
        return data?.shipCount ?? 1;
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
    // datatable_research_tech.csv 임포트 — stack_time, ship_count 컬럼 포함
    public void LoadTechFromCsv(string csvText)
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
            string researchId = GetCol(cols, col, "research_id");
            if (string.IsNullOrEmpty(researchId) || researchId.StartsWith("tech_level_") == false) continue;

            int.TryParse(researchId["tech_level_".Length..], out int targetLevel);
            techLevelDataList.Add(new TechLevelResearchData
            {
                researchId      = researchId,
                targetTechLevel = targetLevel,
                prerequisiteIds = ParseCsvStringList(GetCol(cols, col, "prerequisites")),
                mineralCost     = (int)ParseCsvFloat(GetCol(cols, col, "cost_m")),
                shipCount       = (int)ParseCsvFloat(GetCol(cols, col, "ship_count")),
            });
        }

        Debug.Log($"[DataTableResearch] Tech CSV Import 완료: 기술레벨 {techLevelDataList.Count}개");
        UnityEditor.EditorUtility.SetDirty(this);
    }

    // datatable_research_subtype.csv 임포트 — ui_pos_x/y 컬럼 포함
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

            var uiPos = new Vector2(
                ParseCsvFloat(GetCol(cols, col, "ui_pos_x")),
                ParseCsvFloat(GetCol(cols, col, "ui_pos_y")));

            if (System.Enum.TryParse(researchId, out EModuleSubType moduleSubType) == false) continue;
            researchDataList.Add(new ModuleResearchData
            {
                researchId      = researchId,
                moduleType      = (EModuleType)moduleSubType.GetModuleType(),
                moduleSubType   = moduleSubType,
                prerequisiteIds = ParseCsvStringList(GetCol(cols, col, "prerequisites")),
                uiPosition      = uiPos,
                mineralCost     = (int)ParseCsvLong(GetCol(cols, col, "cost_m")),
            });
        }

        Debug.Log($"[DataTableResearch] Subtype CSV Import 완료: 모듈 {researchDataList.Count}개");
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

    private long ParseCsvLong(string s) { s = s.Replace(",", "").Trim(); return long.TryParse(s, out long r) ? r : 0L; }
#endif

    #endregion

    #region JSON Export/Import

    public string ExportToJson()
    {
        var exportData = new ResearchExportData
        {
            researchDataList  = researchDataList,
            techLevelDataList = techLevelDataList,
        };
        // enum을 이름(String)으로 직렬화 — 정수값 변경에 독립적
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            Formatting = Newtonsoft.Json.Formatting.Indented,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
        };
        return Newtonsoft.Json.JsonConvert.SerializeObject(exportData, settings);
    }

    public void ImportFromJson(string json)
    {
        var importData = Newtonsoft.Json.JsonConvert.DeserializeObject<ResearchExportData>(json);
        if (importData != null)
        {
            researchDataList  = importData.researchDataList;
            techLevelDataList = importData.techLevelDataList ?? new List<TechLevelResearchData>();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    [System.Serializable]
    private class ResearchExportData
    {
        public List<ModuleResearchData>    researchDataList;
        public List<TechLevelResearchData> techLevelDataList;
    }

    #endregion
}
