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

        // 값 입력란 폭을 좁게 고정해서 그림(기본 Inspector는 남는 가로 공간을 값 칸이 전부 채워 불필요하게 넓어짐)
        DrawInspectorWithNarrowFields();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(dataTableConfig);
            serializedObject.ApplyModifiedProperties();
        }
    }

    private const float k_narrowFieldWidth = 80f; // 값 입력란 고정 폭(px) — 기존 가변폭(남는 공간 전부 차지) 대비 체감상 30% 수준

    // DrawDefaultInspector 대체 — 라벨은 그대로 두고 값 칸만 고정폭으로 그려서 가로로 쓸데없이 늘어지는 것을 막음
    private void DrawInspectorWithNarrowFields()
    {
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(iterator, true);
                continue;
            }
            DrawPropertyNarrow(iterator);
        }
    }

    // 자식이 있는 중첩 클래스(Generic)는 기존처럼 폴드아웃+들여쓰기로 재귀, 실제 값(int/float/bool 등)만 좁은 폭으로 그림
    private void DrawPropertyNarrow(SerializedProperty property)
    {
        bool isContainer = property.propertyType == SerializedPropertyType.Generic && property.isArray == false;
        if (isContainer == true)
        {
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, property.displayName, true);
            if (property.isExpanded == true)
            {
                EditorGUI.indentLevel++;
                SerializedProperty child = property.Copy();
                SerializedProperty end = property.GetEndProperty();
                bool enterChildren = true;
                while (child.NextVisible(enterChildren) == true && SerializedProperty.EqualContents(child, end) == false)
                {
                    enterChildren = false;
                    DrawPropertyNarrow(child);
                }
                EditorGUI.indentLevel--;
            }
            return;
        }

        Rect rowRect = EditorGUILayout.GetControlRect();
        float labelWidth = rowRect.width - k_narrowFieldWidth;
        Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height);
        Rect fieldRect = new Rect(rowRect.x + labelWidth, rowRect.y, k_narrowFieldWidth, rowRect.height);
        EditorGUI.LabelField(labelRect, property.displayName);
        EditorGUI.PropertyField(fieldRect, property, GUIContent.none);
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