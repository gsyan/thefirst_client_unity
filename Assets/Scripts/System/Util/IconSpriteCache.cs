using System.Collections.Generic;
using UnityEngine;

public static class IconSpriteCache
{
    static Dictionary<string, Sprite> s_cache;

    static void Init()
    {
        if (s_cache != null) return;
        s_cache = new Dictionary<string, Sprite>();
        foreach (Sprite sprite in Resources.LoadAll<Sprite>("Icon/IconAtlas"))
            s_cache[sprite.name] = sprite;
    }

    public static Sprite Get(string name)
    {
        Init();
        s_cache.TryGetValue(name, out Sprite sprite);
        return sprite;
    }
}
