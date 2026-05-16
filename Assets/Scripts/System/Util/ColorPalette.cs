using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ColorEntry
{
    public string key;
    public Color  color = Color.white;
}

[CreateAssetMenu(fileName = "ColorPalette", menuName = "Custom/ColorPalette")]
public class ColorPalette : ScriptableObject
{
    public List<ColorEntry> entries = new List<ColorEntry>();

    public Color GetColor(string key)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].key == key) return entries[i].color;
        return Color.white;
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
