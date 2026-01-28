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

        EditorGUILayout.LabelField("Data Table Config Manager", EditorStyles.largeLabel);
        EditorGUILayout.Space(10);

        // Default Inspector 그리기
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        // JSON Tools 섹션
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("JSON Import/Export", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Export to JSON"))
        {
            ExportGameSettings();
        }

        if (GUILayout.Button("Import from JSON"))
        {
            ImportGameSettings();
        }
        EditorGUILayout.EndHorizontal();

        // JSON Preview (read-only)
        EditorGUILayout.LabelField("JSON Preview", EditorStyles.boldLabel);
        var jsonProp = serializedObject.FindProperty("exportedJson");
        if (jsonProp != null)
        {
            GUI.enabled = false;
            EditorGUILayout.TextArea(jsonProp.stringValue, GUILayout.Height(100));
            GUI.enabled = true;
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // Reset Tools 섹션
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Reset Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Reset AddShipCosts to Default"))
        {
            ResetAddShipCosts();
        }

        if (GUILayout.Button("Reset All GameSettings to Default"))
        {
            if (EditorUtility.DisplayDialog("Reset All", "모든 GameSettings를 초기화하시겠습니까?", "Yes", "No"))
            {
                ResetAllGameSettings();
            }
        }

        EditorGUILayout.EndVertical();

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

    private void ResetAddShipCosts()
    {
        // GameSettings 기본값에서 가져옴 (중복 방지)
        dataTableConfig.gameSettings.m_addShipCosts = new GameSettings().m_addShipCosts;

        EditorUtility.SetDirty(dataTableConfig);
        AssetDatabase.SaveAssets();
        Debug.Log("AddShipCosts reset to default!");
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