using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Addressable 전환 시 Load/LoadAsync/Release 내부 구현만 교체하면 됨
public class ResourceManager : MonoSingleton<ResourceManager>
{
    protected override bool ShouldDontDestroyOnLoad => true;

    private Dictionary<string, UnityEngine.Object> m_cache = new Dictionary<string, UnityEngine.Object>();

    protected override void OnInitialize()
    {
    }

    // 동기 로드. 캐시 히트 시 즉시 반환
    public T Load<T>(string path) where T : UnityEngine.Object
    {
        bool hasCached = m_cache.TryGetValue(path, out UnityEngine.Object cached);
        if (hasCached == true)
        {
            T cachedTyped = cached as T;
            if (cachedTyped != null)
                return cachedTyped;

            // 씬 전환 등으로 레퍼런스가 null이 된 경우 재로드
            m_cache.Remove(path);
        }

        T loaded = Resources.Load<T>(path);
        if (loaded == null)
        {
            Debug.LogError($"[ResourceManager] Load failed: {path} ({typeof(T).Name})");
            return null;
        }

        m_cache[path] = loaded;
        return loaded;
    }

    // 폴더 내 전체 로드. 캐시 미적용 (Addressable 전환 시 LoadAssetsAsync로 교체)
    public T[] LoadAll<T>(string path) where T : UnityEngine.Object
    {
        T[] assets = Resources.LoadAll<T>(path);
        if (assets == null || assets.Length == 0)
            Debug.LogWarning($"[ResourceManager] LoadAll returned empty: {path} ({typeof(T).Name})");
        return assets;
    }

    // 비동기 로드. 캐시 히트 시 onComplete를 즉시 호출하고 null 반환
    // Addressable 전환 시 Addressables.LoadAssetAsync로 교체
    public Coroutine LoadAsync<T>(string path, Action<T> onComplete) where T : UnityEngine.Object
    {
        bool hasCached = m_cache.TryGetValue(path, out UnityEngine.Object cached);
        if (hasCached == true)
        {
            T cachedTyped = cached as T;
            if (cachedTyped != null)
            {
                onComplete(cachedTyped);
                return null;
            }
            m_cache.Remove(path);
        }

        return StartCoroutine(LoadAsyncCoroutine<T>(path, onComplete));
    }

    private IEnumerator LoadAsyncCoroutine<T>(string path, Action<T> onComplete) where T : UnityEngine.Object
    {
        ResourceRequest request = Resources.LoadAsync<T>(path);
        yield return request;

        T loaded = request.asset as T;
        if (loaded == null)
        {
            Debug.LogError($"[ResourceManager] LoadAsync failed: {path} ({typeof(T).Name})");
            onComplete(null);
            yield break;
        }

        m_cache[path] = loaded;
        onComplete(loaded);
    }

    // 캐시에서 제거. 씬 전환 후 특정 에셋을 명시적으로 해제할 때 사용
    public void Release(string path)
    {
        bool hasCached = m_cache.TryGetValue(path, out UnityEngine.Object obj);
        if (hasCached == false)
            return;

        m_cache.Remove(path);

        bool isGameObject = obj is GameObject;
        if (isGameObject == false)
            Resources.UnloadAsset(obj);
        // GameObject/Component는 UnloadAsset 불가 → DestroyImmediate 또는 그냥 GC에 맡김
    }

    // 씬 전환 시 전체 캐시 해제
    public void ReleaseAll()
    {
        m_cache.Clear();
        Resources.UnloadUnusedAssets();
    }
}
