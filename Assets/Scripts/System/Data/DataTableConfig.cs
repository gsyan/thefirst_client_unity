using UnityEngine;
using Newtonsoft.Json;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class GameSettings
{
    [Header("Game Settings")]
    public string version = "1.0.0";
    public int maxLives = 3;

    [Header("Fleet Settings")]
    public int maxShipsPerFleet = 10;
    public int shipAddMoneyCost = 1000;
    public int shipAddMineralCost = 500;

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