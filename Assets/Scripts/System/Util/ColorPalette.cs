using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ColorEntry
{
    public string key;

    // 비어있지 않으면 이 항목은 Semantic 별칭 — primitiveKey가 가리키는 항목의 색을 대신 사용함.
    // 비어있으면 이 항목 자체가 Primitive(원색) — color 필드가 실제 값.
    public string primitiveKey;
    public Color  color = Color.white;
}

[CreateAssetMenu(fileName = "ColorPalette", menuName = "Custom/ColorPalette")]
public class ColorPalette : ScriptableObject
{
    private const int MaxResolveHops = 8;

    public List<ColorEntry> entries = new List<ColorEntry>();

    // Semantic 별칭(primitiveKey 있음)은 가리키는 Primitive까지 따라가서 색을 반환.
    // 순환 참조는 MaxResolveHops로 방지.
    public Color GetColor(string key)
    {
        string currentKey = key;
        for (int hop = 0; hop < MaxResolveHops; hop++)
        {
            ColorEntry entry = FindEntry(currentKey);
            if (entry == null) return Color.white;

            if (string.IsNullOrEmpty(entry.primitiveKey))
                return entry.color;

            currentKey = entry.primitiveKey;
        }

        Debug.LogWarning($"[ColorPalette] '{key}' 색상 참조가 {MaxResolveHops}단계를 넘어감 — 순환 참조 의심.");
        return Color.white;
    }

    private ColorEntry FindEntry(string key)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].key == key) return entries[i];
        return null;
    }

    public bool HasKey(string key)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].key == key) return true;
        return false;
    }

    public List<string> GetAllKeys()
    {
        var keys = new List<string>();
        for (int i = 0; i < entries.Count; i++)
            keys.Add(entries[i].key);
        return keys;
    }
}
