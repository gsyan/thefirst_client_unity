using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

public static class UISpriteCache
{
    static Dictionary<string, Sprite> s_cache;

    static void Init()
    {
        if (s_cache != null) return;
        s_cache = new Dictionary<string, Sprite>();
        SpriteAtlas atlas = ResourceManager.Instance.Load<SpriteAtlas>("UIAtlas/UIAtlas");
        if (atlas == null) return;
        Sprite[] sprites = new Sprite[atlas.spriteCount];
        atlas.GetSprites(sprites);
        foreach (Sprite sprite in sprites)
            s_cache[sprite.name.Replace("(Clone)", "")] = sprite;
    }

    public static Sprite Get(string name)
    {
        Init();
        s_cache.TryGetValue(name, out Sprite sprite);
        return sprite;
    }
}
