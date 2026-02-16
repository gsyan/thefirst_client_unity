// -------------------------------------------------------------------------------------
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class ResearchNodeData
{
    public string m_researchId; // 고유 식별자 겸 로컬라이제이션 키
    public List<string> m_prerequisiteIds = new List<string>();
    public CostStruct m_researchCost = new CostStruct();
    [Newtonsoft.Json.JsonIgnore] public Vector2 m_uiPosition;
}

[System.Serializable]
public class ModuleResearchData : ResearchNodeData
{
    public EModuleType m_moduleType = EModuleType.none;
    public EModuleSubType m_moduleSubType = EModuleSubType.none;

    [Header("Description")]
    [TextArea(2, 4)]
    public string m_description = "Module Research";
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
        return researchDataList.Find(r => r.m_moduleSubType == subType);
    }

    public CostStruct GetResearchCost(EModuleSubType subType)
    {
        var data = GetResearchData(subType);
        return data?.m_researchCost ?? new CostStruct();
    }

    // 선행 연구 조건을 모두 충족하는지 확인
    public bool ArePrerequisitesMet(string researchId, HashSet<string> completedResearchIds)
    {
        var data = researchDataList.Find(r => r.m_researchId == researchId);
        if (data == null) return false;
        if (data.m_prerequisiteIds == null || data.m_prerequisiteIds.Count == 0) return true;

        for (int i = 0; i < data.m_prerequisiteIds.Count; i++)
        {
            if (completedResearchIds.Contains(data.m_prerequisiteIds[i]) == false)
                return false;
        }
        return true;
    }

    // 특정 모듈 타입의 연구 데이터만 반환
    public List<ModuleResearchData> GetResearchDataByType(EModuleType moduleType)
    {
        return researchDataList.FindAll(r => r.m_moduleType == moduleType);
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
                m_researchId = subType.ToString(),
                m_moduleType = moduleType,
                m_moduleSubType = subType,
                m_prerequisiteIds = prerequisiteIds,
                m_researchCost = new CostStruct(1, researchCost, 0, 0, 0),
                m_uiPosition = vector2Position,
                m_description = $"Research {subType} module technology"
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
