// 게임 전역 설정 ScriptableObject — 함선 추가 비용(addShipCosts), PvP 설정, 모듈 해금 비용 관리
// addShipCosts[currentShipCount]: 함선 추가 시점 비용, 현재 M+MR만 사용 (ME/MD는 추후 콘텐츠 전용)
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
    
    [Header("Fleet Settings")]
    public int maxShipsPerFleet = 9;

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
        new CostStruct(0, 0, 0, 0, 0),                      // idx 0: 초기 함선 (무료)
        new CostStruct(1, 5000, 0, 0, 0),                   // idx 1: 2번째
        new CostStruct(2, 10000, 0, 0, 0),                  // idx 2: 3번째
        new CostStruct(4, 20000, 60000, 0, 0),               // idx 3: 4번째 (zone 3-X에서 MR 수확 시작, ~15일)
        new CostStruct(6, 40000, 170000, 0, 0),             // idx 4: 5번째 (~15일)
        new CostStruct(8, 80000, 370000, 0, 0),             // idx 5: 6번째 (~15일)
        new CostStruct(10, 160000, 1400000, 0, 0),          // idx 6: 7번째 (~30일)
        new CostStruct(12, 320000, 2500000, 0, 0),          // idx 7: 8번째 (~30일)
        new CostStruct(14, 640000, 4000000, 0, 0),          // idx 8: 9번째 (~30일)
    };

    // 현재 함선 개수에 따른 다음 함선 추가 비용 반환
    public CostStruct GetAddShipCost(int currentShipCount)
    {
        // 기본값
        CostStruct defaultCost = new CostStruct(1, 5000, 0, 0, 0);

        // 배열 유효성 체크
        if (addShipCosts == null || addShipCosts.Length == 0)
            return defaultCost;

        // 배열 범위 체크
        if (currentShipCount < addShipCosts.Length)
            return addShipCosts[currentShipCount];

        // 배열 범위를 초과하면 마지막 값 사용
        return addShipCosts[^1];
    }

    [Header("Pvp Settings")]
    public int pvpListCount = 3;
    public int pvpListRefreshCount = 5;    
    public int pvpRankScoreInit = 1000;
    public int pvpRankScorePenalty = 1;
    
    public int moduleUnlockPrice = 5000;


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