using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(ColorPalette))]
public class ColorPaletteEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var palette = (ColorPalette)target;
        Undo.RecordObject(palette, "ColorPalette");

        if (GUILayout.Button("Apply All  (UI 프리팹 전체)"))
            ApplyAllPrefabs(palette);

        EditorGUILayout.Space(8);

        List<ColorEntry> primitives = palette.entries.Where(e => string.IsNullOrEmpty(e.primitiveKey)).ToList();
        List<ColorEntry> aliases    = palette.entries.Where(e => !string.IsNullOrEmpty(e.primitiveKey)).ToList();
        string[] primitiveKeys = primitives.Select(e => e.key).ToArray();

        EditorGUILayout.LabelField("Primitive  (원색 — 실제 값의 근원)", EditorStyles.boldLabel);
        foreach (ColorEntry entry in primitives)
            DrawPrimitiveRow(palette, entry);

        EditorGUILayout.Space(14);
        EditorGUILayout.LabelField("Semantic  (별칭 — Primitive를 참조)", EditorStyles.boldLabel);
        foreach (ColorEntry entry in aliases)
            DrawAliasRow(palette, entry, primitiveKeys);

        EditorGUILayout.Space(10);
        if (GUI.changed)
            EditorUtility.SetDirty(palette);
    }

    private static void DrawPrimitiveRow(ColorPalette palette, ColorEntry entry)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(entry.key, GUILayout.Width(140));
        entry.color = EditorGUILayout.ColorField(entry.color);
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawAliasRow(ColorPalette palette, ColorEntry entry, string[] primitiveKeys)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(entry.key, GUILayout.Width(140));

        int idx = System.Array.IndexOf(primitiveKeys, entry.primitiveKey);
        if (idx < 0) idx = 0;
        int newIdx = EditorGUILayout.Popup(idx, primitiveKeys, GUILayout.Width(120));
        entry.primitiveKey = primitiveKeys.Length > 0 ? primitiveKeys[newIdx] : entry.primitiveKey;

        GUI.enabled = false;
        EditorGUILayout.ColorField(palette.GetColor(entry.key));
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    private static void ApplyAllPrefabs(ColorPalette palette)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Prefabs/UI" });

        PrefabStage openStage     = PrefabStageUtility.GetCurrentPrefabStage();
        string      openStagePath = openStage != null ? openStage.assetPath : null;

        int modified = 0;
        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EditorUtility.DisplayProgressBar("Apply All Prefabs", path, (float)i / guids.Length);

                if (openStagePath != null && path == openStagePath)
                {
                    ColorSetter[] setters = openStage.prefabContentsRoot.GetComponentsInChildren<ColorSetter>(true);
                    bool dirty = false;
                    foreach (ColorSetter s in setters)
                        if (ColorSetterEditor.ApplySetter(s)) dirty = true;
                    if (dirty)
                    {
                        EditorUtility.SetDirty(openStage.prefabContentsRoot);
                        modified++;
                    }
                }
                else
                {
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    ColorSetter[] setters = root.GetComponentsInChildren<ColorSetter>(true);
                    bool dirty = false;
                    foreach (ColorSetter s in setters)
                        if (ColorSetterEditor.ApplySetter(s)) dirty = true;

                    if (dirty)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        modified++;
                    }
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        Debug.Log($"[ColorPalette] Apply All 완료 — {modified}/{guids.Length}개 프리팹 수정됨");
    }
}
