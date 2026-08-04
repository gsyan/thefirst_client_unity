using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(ColorPalette))]
public class ColorPaletteEditor : Editor
{
    private static bool s_primitiveFoldout = true;
    private static bool s_aliasFoldout = true;

    public override void OnInspectorGUI()
    {
        var palette = (ColorPalette)target;
        Undo.RecordObject(palette, "ColorPalette");

        if (GUILayout.Button("Apply All  (UI 프리팹 전체)"))
            ApplyAllPrefabs(palette);

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Primitive 추가"))
            AddEntry(palette, isPrimitive: true);
        if (GUILayout.Button("+ Semantic 추가"))
            AddEntry(palette, isPrimitive: false);
        if (GUILayout.Button("알파벳순 정렬"))
            SortEntriesAlphabetically(palette);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        List<ColorEntry> primitives = palette.entries.Where(e => string.IsNullOrEmpty(e.primitiveKey)).ToList();
        List<ColorEntry> aliases    = palette.entries.Where(e => !string.IsNullOrEmpty(e.primitiveKey)).ToList();
        string[] primitiveKeys = primitives.Select(e => e.key).ToArray();

        var toDelete = new List<ColorEntry>();

        s_primitiveFoldout = EditorGUILayout.Foldout(s_primitiveFoldout, $"Primitive  (원색 — 실제 값의 근원)  [{primitives.Count}]", true, EditorStyles.foldoutHeader);
        if (s_primitiveFoldout == true)
            foreach (ColorEntry entry in primitives)
                DrawPrimitiveRow(palette, entry, toDelete);

        EditorGUILayout.Space(14);
        s_aliasFoldout = EditorGUILayout.Foldout(s_aliasFoldout, $"Semantic  (별칭 — Primitive를 참조)  [{aliases.Count}]", true, EditorStyles.foldoutHeader);
        if (s_aliasFoldout == true)
            foreach (ColorEntry entry in aliases)
                DrawAliasRow(palette, entry, primitiveKeys, toDelete);

        if (toDelete.Count > 0)
        {
            foreach (ColorEntry entry in toDelete)
                palette.entries.Remove(entry);
            EditorUtility.SetDirty(palette);
        }

        EditorGUILayout.Space(10);
        if (GUI.changed)
            EditorUtility.SetDirty(palette);
    }

    private static void AddEntry(ColorPalette palette, bool isPrimitive)
    {
        string newKey = isPrimitive ? "NewPrimitive" : "NewSemantic";
        string primitiveKey = "";
        if (isPrimitive == false)
        {
            ColorEntry firstPrimitive = palette.entries.FirstOrDefault(e => string.IsNullOrEmpty(e.primitiveKey));
            primitiveKey = firstPrimitive != null ? firstPrimitive.key : "";
        }

        palette.entries.Add(new ColorEntry { key = newKey, primitiveKey = primitiveKey, color = Color.white });
        EditorUtility.SetDirty(palette);
    }

    // Primitive(원색, primitiveKey 없음) 그룹을 먼저 알파벳순으로, 그 다음 Semantic(별칭) 그룹을 알파벳순으로 정렬
    private static void SortEntriesAlphabetically(ColorPalette palette)
    {
        palette.entries = palette.entries
            .OrderBy(e => string.IsNullOrEmpty(e.primitiveKey) ? 0 : 1)
            .ThenBy(e => e.key, System.StringComparer.Ordinal)
            .ToList();
        EditorUtility.SetDirty(palette);
    }

    private static void DrawPrimitiveRow(ColorPalette palette, ColorEntry entry, List<ColorEntry> toDelete)
    {
        EditorGUILayout.BeginHorizontal();

        string oldKey = entry.key;
        string newKey = EditorGUILayout.TextField(oldKey, GUILayout.Width(140));
        if (newKey != oldKey)
            RenamePrimitiveKey(palette, oldKey, newKey);

        entry.color = EditorGUILayout.ColorField(entry.color);

        if (GUILayout.Button("X", GUILayout.Width(24)))
            toDelete.Add(entry);

        EditorGUILayout.EndHorizontal();
    }

    // Primitive 이름이 바뀌면 그걸 참조하던 Semantic 항목들의 primitiveKey도 같이 갱신해야 참조가 안 끊김
    private static void RenamePrimitiveKey(ColorPalette palette, string oldKey, string newKey)
    {
        ColorEntry renamed = palette.entries.FirstOrDefault(e => e.key == oldKey);
        if (renamed != null)
            renamed.key = newKey;

        foreach (ColorEntry alias in palette.entries)
            if (alias.primitiveKey == oldKey)
                alias.primitiveKey = newKey;
    }

    private static void DrawAliasRow(ColorPalette palette, ColorEntry entry, string[] primitiveKeys, List<ColorEntry> toDelete)
    {
        EditorGUILayout.BeginHorizontal();

        entry.key = EditorGUILayout.TextField(entry.key, GUILayout.Width(140));

        int idx = System.Array.IndexOf(primitiveKeys, entry.primitiveKey);
        if (idx < 0) idx = 0;
        int newIdx = EditorGUILayout.Popup(idx, primitiveKeys, GUILayout.Width(120));
        entry.primitiveKey = primitiveKeys.Length > 0 ? primitiveKeys[newIdx] : entry.primitiveKey;

        GUI.enabled = false;
        EditorGUILayout.ColorField(palette.GetColor(entry.key));
        GUI.enabled = true;

        if (GUILayout.Button("X", GUILayout.Width(24)))
            toDelete.Add(entry);

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
