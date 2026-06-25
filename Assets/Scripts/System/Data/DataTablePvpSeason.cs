// PVP 시즌 티어 설정 ScriptableObject — 티어별 최소 점수, 시즌 시작 점수, 보상 pvpMineral 개수
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class PvpSeasonTierEntry
{
    public string tierName;
    public int minScore;
    public int resetScore;
    public int seasonReward;
}

[System.Serializable]
public class PvpSeasonConfig
{
    [Header("티어 목록 (minScore 오름차순)")]
    public List<PvpSeasonTierEntry> tiers = new List<PvpSeasonTierEntry>();
}

[CreateAssetMenu(fileName = "DataTablePvpSeason", menuName = "Custom/DataTablePvpSeason")]
public class DataTablePvpSeason : ScriptableObject
{
    public PvpSeasonConfig config = new PvpSeasonConfig();

    public string GetExportFileName() => "DataTablePvpSeason";

    // 점수에 해당하는 티어 엔트리 반환 (최고 티어 우선)
    public PvpSeasonTierEntry GetTierByScore(int score)
    {
        PvpSeasonTierEntry result = null;
        for (int i = 0; i < config.tiers.Count; i++)
        {
            if (score >= config.tiers[i].minScore)
                result = config.tiers[i];
        }
        return result;
    }

    public string ExportToJson()
    {
        string json = JsonConvert.SerializeObject(config, Formatting.Indented);
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
        return json;
    }

    public void ImportFromJson(string json)
    {
        var imported = JsonConvert.DeserializeObject<PvpSeasonConfig>(json);
        if (imported != null)
        {
            config = imported;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }
}
