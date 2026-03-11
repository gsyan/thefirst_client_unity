// 연구 트리 ScriptableObject - 모듈 연구/기술레벨 노드, 모듈 해금/교체 비용 관리
// CSV Import(에디터 전용): datatable_research.csv 기반 로드, "tech_level_N" 접두사로 분기 파싱
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

[System.Serializable]
public class TechLevelResearchData : ResearchNodeData
{
    public int targetTechLevel; // 이 연구를 완료하면 달성되는 기술레벨
}

[CreateAssetMenu(fileName = "DataTableResearch", menuName = "Custom/DataTableResearch")]
public class DataTableResearch : ScriptableObject
{
    [Header("Module SubType Add Cost")]
    // adv 모듈 추가 비용 — subType별 MR 비용, 슬롯 단위 최초 1회만 차감
    public List<ModuleChangeCostEntry> subTypeAddCosts = new List<ModuleChangeCostEntry>
    {
        new ModuleChangeCostEntry { moduleSubType = EModuleSubType.body_t1_adv_ver1,    cost = new CostStruct(0, 0, 5000, 0, 0) },
        new ModuleChangeCostEntry { moduleSubType = EModuleSubType.engine_t1_adv_ver1,  cost = new CostStruct(0, 0, 5000, 0, 0) },
        new ModuleChangeCostEntry { moduleSubType = EModuleSubType.beam_t1_adv_ver1,    cost = new CostStruct(0, 0, 5000, 0, 0) },
        new ModuleChangeCostEntry { moduleSubType = EModuleSubType.missile_t1_adv_ver1, cost = new CostStruct(0, 0, 5000, 0, 0) },
        new ModuleChangeCostEntry { moduleSubType = EModuleSubType.hanger_t1_adv_ver1,  cost = new CostStruct(0, 0, 5000, 0, 0) },
    };

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

    public CostStruct GetResearchCost(EModuleSubType subType)
    {
        var data = GetResearchData(subType);
        return data?.researchCost ?? new CostStruct();
    }

    // 새 모듈 subType 추가 비용 반환 (없으면 기본값 MR 5000)
    public CostStruct GetSubTypeAddCost(EModuleSubType newSubType)
    {
        if (subTypeAddCosts == null) return new CostStruct(0, 0, 5000, 0, 0);
        var entry = subTypeAddCosts.Find(e => e.moduleSubType == newSubType);
        return entry != null ? entry.cost : new CostStruct(0, 0, 5000, 0, 0);
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
    public CostStruct GetTechLevelUpgradeCost(int currentLevel)
    {
        var data = techLevelDataList.Find(r => r.targetTechLevel == currentLevel + 1);
        return data?.researchCost ?? new CostStruct();
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
            if (string.IsNullOrEmpty(researchId)) continue;

            var cost = new CostStruct(
                ParseCsvInt (GetCol(cols, col, "tech_level")),
                ParseCsvLong(GetCol(cols, col, "cost_m")),
                ParseCsvLong(GetCol(cols, col, "cost_mr")),
                ParseCsvLong(GetCol(cols, col, "cost_me")),
                ParseCsvLong(GetCol(cols, col, "cost_md")));

            var uiPos = new Vector2(
                ParseCsvFloat(GetCol(cols, col, "ui_pos_x")),
                ParseCsvFloat(GetCol(cols, col, "ui_pos_y")));

            var prereqs = ParseCsvStringList(GetCol(cols, col, "prerequisites"));

            // "tech_level_N" 접두사: 기술레벨 업그레이드 데이터로 파싱
            if (researchId.StartsWith("tech_level_"))
            {
                int.TryParse(researchId["tech_level_".Length..], out int targetLevel);
                techLevelDataList.Add(new TechLevelResearchData
                {
                    researchId      = researchId,
                    targetTechLevel = targetLevel,
                    prerequisiteIds = prereqs,
                    uiPosition      = uiPos,
                    researchCost    = cost,
                });
                continue;
            }

            // 일반 모듈 연구 데이터
            if (!int.TryParse(GetCol(cols, col, "module_type"), out int typeInt)) continue;
            if (!int.TryParse(GetCol(cols, col, "module_sub_type"), out int subTypeInt)) continue;

            researchDataList.Add(new ModuleResearchData
            {
                researchId      = researchId,
                moduleType      = (EModuleType)typeInt,
                moduleSubType   = (EModuleSubType)subTypeInt,
                prerequisiteIds = prereqs,
                uiPosition      = uiPos,
                researchCost    = cost,
            });
        }

        Debug.Log($"[DataTableResearch] CSV Import 완료: 모듈 {researchDataList.Count}개, 기술레벨 {techLevelDataList.Count}개");
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
        var exportData = new ResearchExportData
        {
            subTypeAddCosts = subTypeAddCosts,
            researchDataList  = researchDataList,
            techLevelDataList = techLevelDataList,
        };
        return Newtonsoft.Json.JsonConvert.SerializeObject(exportData, Newtonsoft.Json.Formatting.Indented);
    }

    public void ImportFromJson(string json)
    {
        var importData = Newtonsoft.Json.JsonConvert.DeserializeObject<ResearchExportData>(json);
        if (importData != null)
        {
            subTypeAddCosts = importData.subTypeAddCosts ?? subTypeAddCosts;
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
        public List<ModuleChangeCostEntry>  subTypeAddCosts;
        public List<ModuleResearchData>     researchDataList;
        public List<TechLevelResearchData>  techLevelDataList;
    }

    #endregion
}
