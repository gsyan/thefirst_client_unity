// 아이콘 아틀라스 전체 빌드 파이프라인
// Tools > Icon Atlas > Build (Slice + Rename + Update Asset)
// 실행 순서: 1.pack_icons.bat → 2.이 메뉴 실행
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.TextCore;
using TMPro;
using Newtonsoft.Json;

public static class IconAtlasBuilder
{
    private const string ATLAS_PATH        = "Assets/Resources/Icon/IconAtlas.png";
    private const string JSON_PATH         = "Assets/Icon/IconAtlas.json";
    private const string SPRITE_ASSET_PATH = "Assets/Resources/Icon/Icons.asset";
    private const int    ICON_SIZE         = 128;
    private const int    PADDING           = 2;

    [MenuItem("Tools/Icon Atlas/Build (Slice + Rename + Update Asset)")]
    public static void Build()
    {
        if (SliceAtlas() == false) return;
        RenameSprites();
        UpdateSpriteAsset();
    }

    // Step 1: Sprite Editor 슬라이스 자동화
    static bool SliceAtlas()
    {
        AssetDatabase.ImportAsset(ATLAS_PATH, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(ATLAS_PATH) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"TextureImporter 없음: {ATLAS_PATH}");
            return false;
        }

        // Sprite (Multiple) 설정 강제 적용
        importer.textureType  = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode   = FilterMode.Bilinear;
        importer.wrapMode     = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ATLAS_PATH);
        if (tex == null)
        {
            Debug.LogError("텍스처 로드 실패");
            return false;
        }

        // JSON에서 슬라이스 정보 읽어 SpriteRect 구성
        string jsonFullPath = Path.Combine(Application.dataPath, "../", JSON_PATH);
        if (File.Exists(jsonFullPath) == false)
        {
            Debug.LogError($"JSON 없음: {jsonFullPath}");
            return false;
        }
        var root = JsonConvert.DeserializeObject<AtlasRoot>(File.ReadAllText(jsonFullPath));
        int texHeight = tex.height;

        var spriteRects = new List<SpriteRect>();
        foreach (FrameEntry entry in root.frames)
        {
            spriteRects.Add(new SpriteRect
            {
                name      = entry.filename,
                rect      = new Rect(entry.frame.x, texHeight - entry.frame.y - entry.frame.h, entry.frame.w, entry.frame.h),
                pivot     = new Vector2(0.5f, 0.5f),
                alignment = SpriteAlignment.Center
            });
        }

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();
        dataProvider.SetSpriteRects(spriteRects.ToArray());
        dataProvider.Apply();
        (dataProvider.targetObject as AssetImporter).SaveAndReimport();

        Debug.Log($"[1/3] 슬라이스 완료: {spriteRects.Count}개");
        return true;
    }

    // Step 2: 스프라이트 이름 재확인 (SliceAtlas에서 이미 이름 설정하므로 검증용)
    static void RenameSprites()
    {
        TextureImporter importer = AssetImporter.GetAtPath(ATLAS_PATH) as TextureImporter;
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ATLAS_PATH);
        string jsonFullPath = Path.Combine(Application.dataPath, "../", JSON_PATH);
        var root = JsonConvert.DeserializeObject<AtlasRoot>(File.ReadAllText(jsonFullPath));
        int texHeight = tex.height;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();
        SpriteRect[] spriteRects = dataProvider.GetSpriteRects();
        int renamed = 0;

        for (int i = 0; i < spriteRects.Length; i++)
        {
            SpriteRect sr = spriteRects[i];
            FrameEntry match = root.frames.Find(e =>
                Mathf.Approximately(e.frame.x, sr.rect.x) &&
                Mathf.Approximately(texHeight - e.frame.y - e.frame.h, sr.rect.y)
            );
            if (match != null)
            {
                sr.name = match.filename;
                spriteRects[i] = sr;
                renamed++;
            }
        }

        dataProvider.SetSpriteRects(spriteRects);
        dataProvider.Apply();
        (dataProvider.targetObject as AssetImporter).SaveAndReimport();

        Debug.Log($"[2/3] 이름 적용: {renamed}/{spriteRects.Length}개");
        AssetDatabase.Refresh();
    }

    // Step 3: TMP Sprite Asset In-Place 업데이트
    static void UpdateSpriteAsset()
    {
        string jsonFullPath = Path.Combine(Application.dataPath, "../", JSON_PATH);
        var root = JsonConvert.DeserializeObject<AtlasRoot>(File.ReadAllText(jsonFullPath));

        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(ATLAS_PATH);
        int texHeight = atlas.height;

        TMP_SpriteAsset spriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SPRITE_ASSET_PATH);
        if (spriteAsset == null)
        {
            spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            var mat = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "Icons Material" };
            AssetDatabase.CreateAsset(spriteAsset, SPRITE_ASSET_PATH);
            AssetDatabase.AddObjectToAsset(mat, SPRITE_ASSET_PATH);
            spriteAsset.material = mat;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            // 저장 후 재로드해야 Unity가 asset을 완전히 초기화함
            spriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SPRITE_ASSET_PATH);
            Debug.Log($"Icons.asset 신규 생성: {SPRITE_ASSET_PATH}");
        }

        spriteAsset.spriteSheet = atlas;
        if (spriteAsset.material != null)
            spriteAsset.material.SetTexture("_MainTex", atlas);

        spriteAsset.spriteGlyphTable.Clear();
        spriteAsset.spriteCharacterTable.Clear();

        for (int i = 0; i < root.frames.Count; i++)
        {
            FrameEntry entry = root.frames[i];
            int glyphY = texHeight - entry.frame.y - entry.frame.h;

            var glyph = new TMP_SpriteGlyph
            {
                index      = (uint)i,
                glyphRect  = new GlyphRect(entry.frame.x, glyphY, entry.frame.w, entry.frame.h),
                metrics    = new GlyphMetrics(entry.frame.w, entry.frame.h, 0, entry.frame.h, entry.frame.w),
                scale      = 1.0f,
                atlasIndex = 0
            };

            var character = new TMP_SpriteCharacter((uint)(0xE000 + i), spriteAsset, glyph)
            {
                name  = entry.filename,
                scale = 1.0f
            };

            spriteAsset.spriteGlyphTable.Add(glyph);
            spriteAsset.spriteCharacterTable.Add(character);
        }

        spriteAsset.UpdateLookupTables();
        EditorUtility.SetDirty(spriteAsset);
        if (spriteAsset.material != null)
            EditorUtility.SetDirty(spriteAsset.material);
        AssetDatabase.SaveAssets();

        Debug.Log($"[3/3] Icons.asset 업데이트 완료: {root.frames.Count}개 (GUID 유지)");
    }

    [System.Serializable] private class AtlasRoot  { public List<FrameEntry> frames; }
    [System.Serializable] private class FrameEntry { public string filename; public FrameRect frame; }
    [System.Serializable] private class FrameRect  { public int x, y, w, h; }
}
