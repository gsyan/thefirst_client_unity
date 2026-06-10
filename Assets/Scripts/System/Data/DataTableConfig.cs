// 게임 전역 설정 ScriptableObject — 함선 추가 비용(addShipCost), PvP 설정, 모듈 해금 비용 관리
// 기술레벨별 최대 함선 수(ship_count)는 DataTableResearch.GetShipCount()에서 조회
using UnityEngine;
using Newtonsoft.Json;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class GameSettings
{
    [Header("Game Settings")]
    public string version = "0.0.1";

    [Tooltip("함선 추가 시 필요한 ModulePoint 비용")]
    public int addShipCost = 10;

[Header("Pvp Settings")]
    public int pvpListCount = 3;
    public int pvpListRefreshCount = 5;    
    public int pvpRankScoreInit = 1000;
    public int pvpRankScorePenalty = 1;
    
    public int moduleUnlockPrice = 1;

    [Tooltip("전투 중 수리 flag ON 시 1초당 소모하는 미네랄 (함대 단위)")]
    public int battleRepairMineralPerSec = 1;
    [Tooltip("즉시 수리 비용 기준 시간(초) — 비용 = battleRepairMineralPerSec × instantRepairBaseSecs")]
    public int instantRepairBaseSecs = 60;
}

[CreateAssetMenu(fileName = "DataTableConfig", menuName = "Custom/DataTableConfig")]
public class DataTableConfig : ScriptableObject
{
    public GameSettings gameSettings = new GameSettings();

    [HideInInspector]
    [SerializeField] private string exportedJson = "";

    public bool IsValid()
    {
        return gameSettings != null;
    }

    public string GetExportFileName()
    {
        return "DataTableConfig";
    }

    public string GetDefaultServerPath()
    {
        return System.IO.Path.Combine(Application.dataPath, "..", "..", "server", "src", "main", "resources", "data", GetExportFileName() + ".json");
    }

    #region JSON Export/Import

    public string ExportToJson()
    {
        string json = JsonConvert.SerializeObject(gameSettings, Formatting.Indented);
        exportedJson = json;

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif

        return json;
    }

    public void ImportFromJson(string json)
    {
        try
        {
            var importData = JsonConvert.DeserializeObject<GameSettings>(json);
            if (importData != null)
            {
                gameSettings = importData;

#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to import GameSettings JSON: {e.Message}");
        }
    }

    #endregion
}