using UnityEngine;
using Newtonsoft.Json;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public struct CostStruct
{
    public int techLevel;
    public int mineral;
    public int mineralRare;
    public int mineralExotic;
    public int mineralDark;

    public CostStruct(int techLevel, int mineral, int mineralRare, int mineralExotic, int mineralDark)
    {
        this.techLevel = techLevel;
        this.mineral = mineral;
        this.mineralRare = mineralRare;
        this.mineralExotic = mineralExotic;
        this.mineralDark = mineralDark;
    }
}

[System.Serializable]
public class GameSettings
{
    [Header("Game Settings")]
    public string version = "0.0.1";
    public int maxLives = 3;

    [Header("Fleet Settings")]
    public int maxShipsPerFleet = 8;

    [Tooltip("함선 추가 시 필요한 Mineral 비용 (함선 개수별 차등 적용)")]
    // public CostStruct[] addShipCosts = new CostStruct[]
    // {
    //     new CostStruct(0, 0, 0, 0, 0),
    //     new CostStruct(2, 100, 0, 0, 0),
    //     new CostStruct(4, 200, 0, 0, 0),
    //     new CostStruct(6, 300, 0, 0, 0),
    //     new CostStruct(8, 500, 0, 0, 0),
    //     new CostStruct(10, 800, 0, 0, 0),
    //     new CostStruct(15, 1300, 0, 0, 0),
    //     new CostStruct(20, 2100, 0, 0, 0),
    //     new CostStruct(25, 3400, 0, 0, 0),
    //     new CostStruct(30, 5500, 0, 0, 0),
    //     new CostStruct(40, 8900, 0, 0, 0)
    // };
    public CostStruct[] addShipCosts = new CostStruct[]
    {
        new CostStruct(0, 0, 0, 0, 0),
        new CostStruct(1, 100, 0, 0, 0),
        new CostStruct(1, 200, 0, 0, 0),
        new CostStruct(1, 400, 0, 0, 0),
        new CostStruct(1, 800, 0, 0, 0),
        new CostStruct(1, 1600, 0, 0, 0),
        new CostStruct(1, 3200, 0, 0, 0),
        new CostStruct(1, 6400, 0, 0, 0),
        new CostStruct(1, 12800, 0, 0, 0),
        new CostStruct(1, 25600, 0, 0, 0),
        new CostStruct(1, 51200, 0, 0, 0)
    };


    
    // 현재 함선 개수에 따른 다음 함선 추가 비용 반환
    public CostStruct GetAddShipCost(int currentShipCount)
    {
        // 기본값
        CostStruct defaultCost = new CostStruct(1, 500, 0, 0, 0);

        // 배열 유효성 체크
        if (addShipCosts == null || addShipCosts.Length == 0)
            return defaultCost;

        // 배열 범위 체크
        if (currentShipCount < addShipCosts.Length)
            return addShipCosts[currentShipCount];

        // 배열 범위를 초과하면 마지막 값 사용
        return addShipCosts[^1];
    }

    [Header("Enemy Settings")]
    public float enemyFleetSpawnInterval = 5.0f;
    public float explorationInterval = 15.0f;
    public float enemySpawnRate = 2.0f;

    [Header("Formation Settings")]
    public float linearFormationSpacing = 3.0f;
    public float gridFormationSpacing = 5.0f;
    public float circleFormationSpacing = 8.0f;
    public float diamondFormationSpacing = 6.0f;
    public float wedgeFormationSpacing = 3.0f;
}

[CreateAssetMenu(fileName = "DataTableConfig", menuName = "Custom/DataTableConfig")]
public class DataTableConfig : ScriptableObject
{
    public GameSettings gameSettings = new GameSettings();

    [Header("Export Settings")]
    [SerializeField, TextArea(5, 15)] private string exportedJson = "";

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