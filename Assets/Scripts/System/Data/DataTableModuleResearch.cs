// -------------------------------------------------------------------------------------
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class ModuleResearchData
{
    public EModuleType m_moduleType = EModuleType.None;
    public EModuleSubType m_moduleSubType = EModuleSubType.None;

    [Header("Research Cost")]
    public CostStruct m_researchCost = new CostStruct();

    [Header("Description")]
    [TextArea(2, 4)]
    public string m_description = "Module Research";
}

[CreateAssetMenu(fileName = "DataTableModuleResearch", menuName = "Custom/DataTableModuleResearch")]
public class DataTableModuleResearch : ScriptableObject
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

    public void InitializeResearchData()
    {
        researchDataList.Clear();

        // Add research data for each subtype
        foreach (EModuleSubType subType in System.Enum.GetValues(typeof(EModuleSubType)))
        {
            if (subType == EModuleSubType.None) continue;

            EModuleType moduleType = CommonUtility.GetModuleTypeFromSubType(subType);

            // subType의 마지막 자리 숫자로 tier 결정 (1→1000, 2→10000, 3→100000)
            int tier = (int)subType % 10;
            long researchCost = 10000L * (long)System.Math.Pow(10, tier - 1);

            var researchData = new ModuleResearchData
            {
                m_moduleType = moduleType,
                m_moduleSubType = subType,
                m_researchCost = new CostStruct(1, researchCost, 0, 0, 0),
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
