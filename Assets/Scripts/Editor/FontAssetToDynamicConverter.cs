// TMP 폰트 에셋을 Static → Dynamic 으로 일괄 변환하는 에디터 유틸
using UnityEditor;
using UnityEngine;
using TMPro;

public static class FontAssetToDynamicConverter
{
    [MenuItem("Tools/TMP/Convert KR Fonts to Dynamic")]
    public static void ConvertKRFontsToDynamic()
    {
        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets/Fonts/Noto_Sans_KR" });
        if (guids.Length == 0)
        {
            Debug.LogWarning("[FontConverter] Assets/Fonts/Noto_Sans_KR 에서 TMP_FontAsset 를 찾지 못했습니다.");
            return;
        }

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fontAsset == null) continue;

            if (fontAsset.atlasPopulationMode == AtlasPopulationMode.Dynamic)
            {
                Debug.Log($"[FontConverter] 이미 Dynamic: {path}");
                continue;
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            // 내장된 Static 아틀라스 데이터 제거
            fontAsset.ClearFontAssetData(setAtlasSizeToZero: false);

            EditorUtility.SetDirty(fontAsset);
            Debug.Log($"[FontConverter] Dynamic 변환 완료: {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[FontConverter] 모든 변환 완료. 빌드 후 APK 크기를 확인하세요.");
    }
}
