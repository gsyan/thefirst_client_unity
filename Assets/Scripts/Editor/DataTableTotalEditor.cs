#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using Newtonsoft.Json;

public class DataTableTotalEditor : EditorWindow
{
    private DataTableConfig dataTableConfig;
    private DataTableModule dataTableModule;
    private Vector2 scrollPosition;

    [MenuItem("Tools/DataTable Total Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<DataTableTotalEditor>("DataTable Total Manager");
        window.AutoAssignAssets();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("DataTable Total Export Manager", EditorStyles.largeLabel);
        EditorGUILayout.Space(10);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // Data Sources 섹션
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Data Sources", EditorStyles.boldLabel);

        dataTableConfig = (DataTableConfig)EditorGUILayout.ObjectField(
            "DataTable Config", dataTableConfig, typeof(DataTableConfig), false);

        dataTableModule = (DataTableModule)EditorGUILayout.ObjectField(
            "DataTable Module", dataTableModule, typeof(DataTableModule), false);

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // Export 섹션
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Total Export Options", EditorStyles.boldLabel);

        GUI.enabled = IsValid();

        if (GUILayout.Button("Export All", GUILayout.Height(40)))
        {
            ExportAll();
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Export to Server Directory", GUILayout.Height(30)))
        {
            ExportToServerDirectory();
        }

        GUI.enabled = true;

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // 정보 표시
        if (dataTableModule != null)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Module Data Info", EditorStyles.boldLabel);

            int totalModules = dataTableModule.BodyModules.Count +
                               dataTableModule.WeaponModules.Count +
                               dataTableModule.EngineModules.Count;

            EditorGUILayout.LabelField($"Total Modules: {totalModules}");
            EditorGUILayout.LabelField($"Body Modules: {dataTableModule.BodyModules.Count}");
            EditorGUILayout.LabelField($"Weapon Modules: {dataTableModule.WeaponModules.Count}");
            EditorGUILayout.LabelField($"Engine Modules: {dataTableModule.EngineModules.Count}");

            EditorGUILayout.EndVertical();
        }

        if (dataTableConfig != null)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Game Settings Info", EditorStyles.boldLabel);

            var settings = dataTableConfig.gameSettings;
            EditorGUILayout.LabelField($"Version: {settings.version}");
            EditorGUILayout.LabelField($"Max Ships Per Fleet: {settings.maxShipsPerFleet}");

            // 함선 추가 비용 배열 표시
            if (settings.addShipCosts != null && settings.addShipCosts.Length > 0)
            {
                EditorGUILayout.LabelField("Ship Add Costs:");
                for (int i = 0; i < settings.addShipCosts.Length; i++)
                {
                    var cost = settings.addShipCosts[i];
                    string costText = $"  Ship {i}: M:{cost.mineral}";
                    if (cost.mineralRare > 0) costText += $", R:{cost.mineralRare}";
                    if (cost.mineralExotic > 0) costText += $", E:{cost.mineralExotic}";
                    if (cost.mineralDark > 0) costText += $", D:{cost.mineralDark}";
                    EditorGUILayout.LabelField(costText);
                }
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
    }

    private void AutoAssignAssets()
    {
        if (dataTableConfig == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:DataTableConfig", new[] { "Assets/Resources/DataTable" });
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                dataTableConfig = AssetDatabase.LoadAssetAtPath<DataTableConfig>(path);
            }
        }

        if (dataTableModule == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:DataTableModule", new[] { "Assets/Resources/DataTable" });
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                dataTableModule = AssetDatabase.LoadAssetAtPath<DataTableModule>(path);
            }
        }
    }

    private bool IsValid()
    {
        return dataTableModule != null && dataTableConfig != null;
    }

    private void ExportAll()
    {
        if (!IsValid())
        {
            EditorUtility.DisplayDialog("Error", "Please assign both DataTableModule and DataTableConfig!", "OK");
            return;
        }

        string folderPath = EditorUtility.SaveFolderPanel("Export Game Configs", "", "");
        if (!string.IsNullOrEmpty(folderPath))
        {
            // DataTableConfig.json 내보내기
            string configJson = dataTableConfig.ExportToJson();
            string configPath = Path.Combine(folderPath, "DataTableConfig.json");
            File.WriteAllText(configPath, configJson);

            // DataTableModule.json 내보내기
            string moduleJson = dataTableModule.ExportToJson();
            string modulePath = Path.Combine(folderPath, "DataTableModule.json");
            File.WriteAllText(modulePath, moduleJson);

            EditorUtility.DisplayDialog("Export Successful",
                $"Game Configs exported to:\n{configPath}\n{modulePath}", "OK");
        }
    }

    private void ExportToServerDirectory()
    {
        if (!IsValid())
        {
            EditorUtility.DisplayDialog("Error", "Please assign both DataTableModule and DataTableConfig!", "OK");
            return;
        }

        // Application.dataPath = D:\BK\thefirst\thefirst_client_unity\Assets
        // 목표: D:\BK\thefirst\thefirst_server\src\main\resources\data
        string serverDataPath = Path.Combine(Application.dataPath, "..", "..", "thefirst_server", "src", "main", "resources", "data");
        serverDataPath = Path.GetFullPath(serverDataPath);

        try
        {
            Directory.CreateDirectory(serverDataPath);

            // DataTableConfig.json 서버로 내보내기
            string configJson = dataTableConfig.ExportToJson();
            string configServerPath = Path.Combine(serverDataPath, "DataTableConfig.json");
            File.WriteAllText(configServerPath, configJson);

            // DataTableModule.json 서버로 내보내기
            string moduleJson = dataTableModule.ExportToJson();
            string moduleServerPath = Path.Combine(serverDataPath, "DataTableModule.json");
            File.WriteAllText(moduleServerPath, moduleJson);

            EditorUtility.DisplayDialog("Export Successful",
                $"Game Configs exported to server:\n{configServerPath}\n{moduleServerPath}", "OK");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Export Failed",
                $"Failed to export to server directory:\n{e.Message}", "OK");
        }
    }

    private TotalGameConfigData CreateTotalGameConfig()
    {
        var totalConfig = new TotalGameConfigData();

        // DataTableModule 데이터 추가
        totalConfig.modules = new System.Collections.Generic.Dictionary<string, object>();
        totalConfig.modules["0"] = dataTableModule.BodyModules.modules;
        totalConfig.modules["1"] = dataTableModule.WeaponModules.modules;
        totalConfig.modules["2"] = dataTableModule.EngineModules.modules;

        // GameSettings 데이터 추가
        totalConfig.gameSettings = dataTableConfig.gameSettings;

        return totalConfig;
    }

    [System.Serializable]
    public class TotalGameConfigData
    {
        public System.Collections.Generic.Dictionary<string, object> modules;
        public GameSettings gameSettings;
    }
}
#endif