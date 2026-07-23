#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using Newtonsoft.Json;

public class DataTableTotalEditor : EditorWindow
{
    private DataTableConfig dataTableConfig;
    private DataTableModule dataTableModule;
    private DataTableCommander dataTableCommander;
    private DataTableZone dataTableZone;
    private DataTableShipPreset dataTableShipPreset;
    private DataTableForbiddenWords dataTableForbiddenWords;
    private DataTablePvpSeason dataTablePvpSeason;
    private DataTableDailyBonus dataTableDailyBonus;
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

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Data Sources", EditorStyles.boldLabel);

        dataTableConfig = (DataTableConfig)EditorGUILayout.ObjectField(
            "DataTable Config", dataTableConfig, typeof(DataTableConfig), false);

        dataTableModule = (DataTableModule)EditorGUILayout.ObjectField(
            "DataTable Module", dataTableModule, typeof(DataTableModule), false);

        dataTableCommander = (DataTableCommander)EditorGUILayout.ObjectField(
            "DataTable Commander", dataTableCommander, typeof(DataTableCommander), false);

        dataTableZone = (DataTableZone)EditorGUILayout.ObjectField(
            "DataTable Zone", dataTableZone, typeof(DataTableZone), false);

        dataTableShipPreset = (DataTableShipPreset)EditorGUILayout.ObjectField(
            "DataTable ShipPreset", dataTableShipPreset, typeof(DataTableShipPreset), false);

        dataTableForbiddenWords = (DataTableForbiddenWords)EditorGUILayout.ObjectField(
            "DataTable ForbiddenWords", dataTableForbiddenWords, typeof(DataTableForbiddenWords), false);

        dataTablePvpSeason = (DataTablePvpSeason)EditorGUILayout.ObjectField(
            "DataTable PvpSeason", dataTablePvpSeason, typeof(DataTablePvpSeason), false);

        dataTableDailyBonus = (DataTableDailyBonus)EditorGUILayout.ObjectField(
            "DataTable DailyBonus", dataTableDailyBonus, typeof(DataTableDailyBonus), false);

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Total Export Options", EditorStyles.boldLabel);

        if (GUILayout.Button("Export All", GUILayout.Height(40)))
            ExportAll();

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Export to Server Directory", GUILayout.Height(30)))
            ExportToServerDirectory();

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        if (dataTableModule != null)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Module Data Info", EditorStyles.boldLabel);

            int totalModules = dataTableModule.BodyModules.Count +
                               dataTableModule.BeamModules.Count +
                               dataTableModule.MissileModules.Count +
                               dataTableModule.HangerModules.Count;

            EditorGUILayout.LabelField($"Total Modules: {totalModules}");
            EditorGUILayout.LabelField($"Body Modules: {dataTableModule.BodyModules.Count}");
            EditorGUILayout.LabelField($"Beam Modules: {dataTableModule.BeamModules.Count}");
            EditorGUILayout.LabelField($"Missile Modules: {dataTableModule.MissileModules.Count}");
            EditorGUILayout.LabelField($"Hanger Modules: {dataTableModule.HangerModules.Count}");
            EditorGUILayout.EndVertical();
        }

        if (dataTableCommander != null)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Commander Level Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Commander Levels: {dataTableCommander.GetCommanderDataList().Count}");
            EditorGUILayout.LabelField($"Max Ship Count: {dataTableCommander.GetMaxShipCount()}");
            EditorGUILayout.EndVertical();
        }

        if (dataTableConfig != null)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Game Settings Info", EditorStyles.boldLabel);
            var settings = dataTableConfig.gameSettings;
            EditorGUILayout.LabelField($"Version: {settings.version}");
            EditorGUILayout.LabelField($"Add Ships Cost: {settings.addShipCost}");
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
    }

    private void AutoAssignAssets()
    {
        TryLoad(ref dataTableConfig,       "t:DataTableConfig");
        TryLoad(ref dataTableModule,        "t:DataTableModule");
        TryLoad(ref dataTableCommander,"t:DataTableCommander");
        TryLoad(ref dataTableZone,          "t:DataTableZone");
        TryLoad(ref dataTableShipPreset,    "t:DataTableShipPreset");
        TryLoad(ref dataTableForbiddenWords,"t:DataTableForbiddenWords");
        TryLoad(ref dataTablePvpSeason,     "t:DataTablePvpSeason");
        TryLoad(ref dataTableDailyBonus,    "t:DataTableDailyBonus");
    }

    private void TryLoad<T>(ref T field, string filter) where T : UnityEngine.Object
    {
        if (field != null) return;
        string[] guids = AssetDatabase.FindAssets(filter, new[] { "Assets/Resources/DataTable" });
        if (guids.Length > 0)
            field = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private void ExportAll()
    {
        string folderPath = EditorUtility.SaveFolderPanel("Export Game Configs", "", "");
        if (string.IsNullOrEmpty(folderPath)) return;

        if (dataTableConfig != null) WriteJson(folderPath, "DataTableConfig.json", dataTableConfig.ExportToJson());
        if (dataTableModule != null) WriteJson(folderPath, "DataTableModule.json", dataTableModule.ExportToJson());
        if (dataTableCommander != null) WriteJson(folderPath, "DataTableCommander.json", dataTableCommander.ExportToJson());
        if (dataTableZone != null) WriteJson(folderPath, "DataTableZone.json", dataTableZone.ExportToJson());
        if (dataTableShipPreset != null) WriteJson(folderPath, "DataTableShipPreset.json", dataTableShipPreset.ExportToJson());
        if (dataTableForbiddenWords != null) WriteJson(folderPath, "DataTableForbiddenWords.json", dataTableForbiddenWords.ExportToJson());
        if (dataTablePvpSeason != null) WriteJson(folderPath, "DataTablePvpSeason.json", dataTablePvpSeason.ExportToJson());
        if (dataTableDailyBonus != null) WriteJson(folderPath, "DataTableDailyBonus.json", dataTableDailyBonus.ExportToJson());

        EditorUtility.DisplayDialog("Export Successful", $"Exported to:\n{folderPath}", "OK");
    }

    private void ExportToServerDirectory()
    {
        string serverDataPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "..", "thefirst_server", "src", "main", "resources", "data"));

        try
        {
            Directory.CreateDirectory(serverDataPath);

            if (dataTableConfig != null) WriteJson(serverDataPath, "DataTableConfig.json", dataTableConfig.ExportToJson());
            if (dataTableModule != null) WriteJson(serverDataPath, "DataTableModule.json", dataTableModule.ExportToJson());
            if (dataTableCommander != null) WriteJson(serverDataPath, "DataTableCommander.json", dataTableCommander.ExportToJson());
            if (dataTableZone != null) WriteJson(serverDataPath, "DataTableZone.json", dataTableZone.ExportToJson());
            if (dataTableShipPreset != null) WriteJson(serverDataPath, "DataTableShipPreset.json", dataTableShipPreset.ExportToJson());
            if (dataTableForbiddenWords != null) WriteJson(serverDataPath, "DataTableForbiddenWords.json", dataTableForbiddenWords.ExportToJson());
            if (dataTablePvpSeason != null) WriteJson(serverDataPath, "DataTablePvpSeason.json", dataTablePvpSeason.ExportToJson());
            if (dataTableDailyBonus != null) WriteJson(serverDataPath, "DataTableDailyBonus.json", dataTableDailyBonus.ExportToJson());

            EditorUtility.DisplayDialog("Export Successful", $"Exported to server:\n{serverDataPath}", "OK");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Export Failed", $"Failed to export:\n{e.Message}", "OK");
        }
    }

    private void WriteJson(string folder, string fileName, string json)
    {
        File.WriteAllText(Path.Combine(folder, fileName), json);
    }

    [System.Serializable]
    public class TotalGameConfigData
    {
        public System.Collections.Generic.Dictionary<string, object> modules;
        public GameSettings gameSettings;
    }
}
#endif
