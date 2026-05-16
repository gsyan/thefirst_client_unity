using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class ColorSetterEntry
{
    public Graphic graphic;
    public string  colorRole;
}

// Inspector에서 각 Graphic마다 colorRole을 지정해 팔레트 색을 적용하는 에디터 전용 컴포넌트
// 런타임 로직 없음 — 색은 에디터 Apply 시점에 컴포넌트에 직접 저장됨
public class ColorSetter : MonoBehaviour
{
    public ColorPalette           palette;
    public List<ColorSetterEntry> targets = new List<ColorSetterEntry>();

#if UNITY_EDITOR
    private void Reset()
    {
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ColorPalette");
        if (guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            palette = UnityEditor.AssetDatabase.LoadAssetAtPath<ColorPalette>(path);
        }
    }
#endif
}
