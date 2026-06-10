using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RowImageText))]
public class RowImageTextEditor : Editor
{
    private ColorPalette m_palette;
    private string[]     m_keyArray;

    void OnEnable()
    {
        string[] guids = AssetDatabase.FindAssets("t:ColorPalette");
        if (guids.Length == 0) return;
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        m_palette = AssetDatabase.LoadAssetAtPath<ColorPalette>(path);
        if (m_palette != null)
            m_keyArray = m_palette.GetAllKeys().ToArray();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_image"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_text"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_image2"));

        EditorGUILayout.Space(4);

        bool hasPalette = m_palette != null && m_keyArray != null && m_keyArray.Length > 0;

        DrawColorKeyField("m_imageColorKey", "Image Color", hasPalette);
        DrawColorKeyField("m_textColorKey",  "Text Color",  hasPalette);

        if (!hasPalette)
            EditorGUILayout.HelpBox("ColorPalette 에셋을 찾을 수 없습니다.", MessageType.Warning);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawColorKeyField(string propName, string label, bool hasPalette)
    {
        SerializedProperty prop = serializedObject.FindProperty(propName);
        if (hasPalette == true)
        {
            List<string> keyList = new List<string>(m_keyArray);
            int idx = keyList.IndexOf(prop.stringValue);
            if (idx < 0) idx = 0;

            EditorGUILayout.BeginHorizontal();
            int newIdx = EditorGUILayout.Popup(label, idx, m_keyArray);
            prop.stringValue = m_keyArray[newIdx];
            Color preview = m_palette.GetColor(prop.stringValue);
            Rect r = GUILayoutUtility.GetRect(20, 18, GUILayout.Width(40));
            EditorGUI.DrawRect(r, new Color(preview.r, preview.g, preview.b, 1f));
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.PropertyField(prop, new GUIContent(label + " Key"));
        }
    }
}
