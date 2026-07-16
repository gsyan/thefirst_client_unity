using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(ColorSetter))]
public class ColorSetterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var setter = (ColorSetter)target;
        Undo.RecordObject(setter, "ColorSetter");

        setter.palette = (ColorPalette)EditorGUILayout.ObjectField("Palette", setter.palette, typeof(ColorPalette), false);

        EditorGUILayout.Space(8);

        // 타겟 목록 — 각 항목마다 Graphic + colorRole 드롭다운
        List<string> keys = setter.palette != null ? setter.palette.GetAllKeys() : new List<string>();
        string[] keyArray = keys.ToArray();

        EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);

        for (int i = 0; i < setter.targets.Count; i++)
        {
            ColorSetterEntry entry = setter.targets[i];

            EditorGUILayout.BeginHorizontal();

            entry.graphic = (Graphic)EditorGUILayout.ObjectField(entry.graphic, typeof(Graphic), true, GUILayout.Width(180));

            entry.colorRole = EditorGUILayout.TextField(entry.colorRole);

            if (setter.palette != null && string.IsNullOrEmpty(entry.colorRole) == false)
            {
                Color preview = setter.palette.GetColor(entry.colorRole);
                Rect r = GUILayoutUtility.GetRect(20, 18, GUILayout.Width(20));
                EditorGUI.DrawRect(r, new Color(preview.r, preview.g, preview.b, 1f));
            }

            if (GUILayout.Button("✕", GUILayout.Width(24)))
            {
                setter.targets.RemoveAt(i);
                EditorUtility.SetDirty(setter);
                break;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4);

        if (GUILayout.Button("+ Add Entry"))
        {
            Undo.RecordObject(setter, "ColorSetter AddEntry");
            setter.targets.Add(new ColorSetterEntry { colorRole = keyArray.Length > 0 ? keyArray[0] : "" });
            EditorUtility.SetDirty(setter);
        }

        EditorGUILayout.Space(4);

        // Auto Collect — 계층 순서 기준 재정렬, 기존 colorRole 이어받음
        if (GUILayout.Button("Auto Collect  (자식 Graphic 수집)"))
        {
            Undo.RecordObject(setter, "ColorSetter AutoCollect");
            Graphic[] graphics = setter.GetComponentsInChildren<Graphic>(true);

            // 기존 graphic → colorRole 맵 보존
            var prevRoles = new Dictionary<Graphic, string>();
            for (int j = 0; j < setter.targets.Count; j++)
                if (setter.targets[j].graphic != null)
                    prevRoles[setter.targets[j].graphic] = setter.targets[j].colorRole;

            setter.targets.Clear();
            foreach (Graphic g in graphics)
            {
                string role = prevRoles.TryGetValue(g, out string prev) ? prev : (keyArray.Length > 0 ? keyArray[0] : "");
                setter.targets.Add(new ColorSetterEntry { graphic = g, colorRole = role });
            }
            EditorUtility.SetDirty(setter);
        }

        EditorGUILayout.Space(8);

        GUI.enabled = setter.palette != null;
        if (GUILayout.Button("Apply"))
            ApplySetter(setter);
        GUI.enabled = true;

        if (GUI.changed)
            EditorUtility.SetDirty(setter);
    }

    public static bool ApplySetter(ColorSetter setter)
    {
        if (setter.palette == null) return false;
        bool applied = false;
        foreach (ColorSetterEntry entry in setter.targets)
        {
            if (entry.graphic == null || string.IsNullOrEmpty(entry.colorRole)) continue;
            Undo.RecordObject(entry.graphic, "ColorSetter Apply");
            entry.graphic.color = setter.palette.GetColor(entry.colorRole);
            EditorUtility.SetDirty(entry.graphic);
            applied = true;
        }
        return applied;
    }
}
