#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using Newtonsoft.Json;

[CustomEditor(typeof(DataTableConfig))]
public class DataTableConfigEditor : Editor
{
    private DataTableConfig dataTableConfig;
    private Vector2 scrollPosition;

    private void OnEnable()
    {
        dataTableConfig = (DataTableConfig)target;
    }

    public override void OnInspectorGUI()
    {
        if (dataTableConfig == null) return;

        serializedObject.Update();


        // Reset Tools 섹션 (최상단)
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Reset Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Reset All GameSettings to Default"))
        {
            if (EditorUtility.DisplayDialog("Reset All", "모든 GameSettings를 초기화하시겠습니까?", "Yes", "No"))
            {
                ResetAllGameSettings();
            }
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Default Inspector 그리기
        DrawDefaultInspector();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(dataTableConfig);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private void ExportGameSettings()
    {
        string json = dataTableConfig.ExportToJson();
        string path = EditorUtility.SaveFilePanel("Export Game Settings", "", dataTableConfig.GetExportFileName(), "json");
        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, json);
            EditorUtility.DisplayDialog("Export Successful", $"Game Settings exported to:\n{path}", "OK");
        }
    }

    private void ImportGameSettings()
    {
        string path = EditorUtility.OpenFilePanel("Import Game Settings", "", "json");
        if (!string.IsNullOrEmpty(path))
        {
            string json = File.ReadAllText(path);
            dataTableConfig.ImportFromJson(json);
            EditorUtility.SetDirty(dataTableConfig);
            EditorUtility.DisplayDialog("Import Successful", "Game Settings imported successfully!", "OK");
        }
    }

    private void ResetAllGameSettings()
    {
        dataTableConfig.gameSettings = new GameSettings();
        EditorUtility.SetDirty(dataTableConfig);
        AssetDatabase.SaveAssets();
        Debug.Log("All GameSettings reset to default!");
    }

}
#endif