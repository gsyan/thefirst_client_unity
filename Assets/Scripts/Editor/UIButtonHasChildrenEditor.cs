using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(UIButtonHasChildren))]
public class UIButtonHasChildrenEditor : Editor
{
    private ColorPalette m_palette;
    private string[]     m_keyArray;

    void OnEnable()
    {
        LoadPalette();
    }

    private void LoadPalette()
    {
        string[] guids = AssetDatabase.FindAssets("t:ColorPalette");
        if (guids.Length == 0) return;
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        m_palette = AssetDatabase.LoadAssetAtPath<ColorPalette>(path);
        if (m_palette != null)
        {
            List<string> keys = m_palette.GetAllKeys();
            m_keyArray = keys.ToArray();
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        bool hasPalette = m_palette != null && m_keyArray != null && m_keyArray.Length > 0;

        // --- 기본 색상 ---
        EditorGUILayout.LabelField("Default", EditorStyles.boldLabel);
        DrawColorKeyField("m_activeColorKey",   "Active Color",   hasPalette);
        DrawColorKeyField("m_inactiveColorKey", "Inactive Color", hasPalette);

        EditorGUILayout.Space(8);

        if (!hasPalette)
            EditorGUILayout.HelpBox("ColorPalette 에셋을 찾을 수 없습니다.", MessageType.Warning);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawColorKeyField(string propName, string label, bool hasPalette)
    {
        SerializedProperty prop = serializedObject.FindProperty(propName);
        if (hasPalette == true)
        {
            DrawColorKeyPopup(prop, label, m_keyArray);
        }
        else
        {
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
        }
    }

    private void DrawColorKeyPopup(SerializedProperty prop, string label, string[] keys)
    {
        List<string> keyList = new List<string>(keys);
        int idx = keyList.IndexOf(prop.stringValue);
        if (idx < 0) idx = 0;

        EditorGUILayout.BeginHorizontal();
        int newIdx = EditorGUILayout.Popup(label, idx, keys);
        prop.stringValue = keys[newIdx];
        Color preview = m_palette.GetColor(prop.stringValue);
        Rect r = GUILayoutUtility.GetRect(20, 18, GUILayout.Width(40));
        EditorGUI.DrawRect(r, new Color(preview.r, preview.g, preview.b, 1f));
        EditorGUILayout.EndHorizontal();
    }
}
