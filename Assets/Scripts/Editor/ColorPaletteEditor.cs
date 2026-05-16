using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(ColorPalette))]
public class ColorPaletteEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Apply All  (UI 프리팹 전체)"))
            ApplyAllPrefabs((ColorPalette)target);

        EditorGUILayout.Space(8);

        DrawDefaultInspector();
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
