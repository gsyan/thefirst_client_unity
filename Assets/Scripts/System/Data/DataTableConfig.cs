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

    [Header("Tactic - Repair")]
    [Tooltip("수리 부스트 ON 시 1초당 소모하는 미네랄 (함대 단위)")]
    public int repairBoostMineralPerSec = 1;
    [Tooltip("수리 부스트 ON 시 수리 속도 배율")]
    public float repairBoostMultiplier = 2f;
    [Tooltip("즉시 수리 비용 기준 시간(초) — 비용 = repairBoostMineralPerSec × instantRepairBaseSecs")]
    public int instantRepairBaseSecs = 60;

    [Header("Tactic - Missile")]
    [Tooltip("미사일 전술 강화 ON 시 1초당 소모하는 미네랄 (함대 단위)")]
    public int missileTacticMineralPerSec = 1;
    [Tooltip("미사일 전술 강화 ON 시 데미지 배율")]
    public float missileTacticDamageMultiplier = 2f;
    [Tooltip("미사일 전술 강화 ON 시 폭발 반경 배율")]
    public float missileTacticExplosionMultiplier = 2f;

    [Header("Tactic - Aircraft")]
    [Tooltip("함재기 전술 강화 ON 시 1초당 소모하는 미네랄 (함대 단위)")]
    public int aircraftTacticMineralPerSec = 1;
    [Tooltip("함재기 전술 강화 ON 시 공격력 배율")]
    public float aircraftTacticDamageMultiplier = 2f;
    [Tooltip("함재기 전술 강화 ON 시 미사일 장착 개수 배율")]
    public float aircraftTacticAmmoMultiplier = 2f;
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