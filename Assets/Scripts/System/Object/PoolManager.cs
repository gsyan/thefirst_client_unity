//------------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolManager
{
    private readonly Dictionary<EPoolName, object> m_pools = new();
    private Transform m_poolRoot;
    private MonoBehaviour m_owner;

    public void Initialize(MonoBehaviour owner)
    {
        m_owner = owner;
        GameObject poolRootObj = new GameObject("PoolRoot");
        m_poolRoot = poolRootObj.transform;
        m_poolRoot.SetParent(owner.transform);
    }

    public void CreatePool<T>(EPoolName poolName, T prefab, int initialSize = 10, int maxSize = 100) where T : Component
    {
        if (m_pools.ContainsKey(poolName))
        {
            Debug.LogWarning($"Pool '{poolName}' already exists.");
            return;
        }

        Transform poolParent = new GameObject(poolName.ToString()).transform;
        poolParent.SetParent(m_poolRoot);

        ObjectPool<T> pool = new ObjectPool<T>(prefab, initialSize, maxSize, poolParent);
        m_pools[poolName] = pool;
    }

    public T Get<T>(EPoolName poolName) where T : Component
    {
        if (m_pools.TryGetValue(poolName, out object poolObj) == false)
        {
            Debug.LogError($"Pool '{poolName}' does not exist.");
            return null;
        }

        ObjectPool<T> pool = poolObj as ObjectPool<T>;
        if (pool == null)
        {
            Debug.LogError($"Pool '{poolName}' is not of type ObjectPool<{typeof(T).Name}>.");
            return null;
        }

        return pool.Get();
    }

    public void Return<T>(EPoolName poolName, T obj) where T : Component
    {
        if (obj == null) return;

        if (m_pools.TryGetValue(poolName, out object poolObj) == false)
        {
            Debug.LogWarning($"Pool '{poolName}' does not exist. Destroying object.");
            Object.Destroy(obj.gameObject);
            return;
        }

        ObjectPool<T> pool = poolObj as ObjectPool<T>;
        if (pool == null)
        {
            Debug.LogError($"Pool '{poolName}' is not of type ObjectPool<{typeof(T).Name}>.");
            Object.Destroy(obj.gameObject);
            return;
        }

        pool.Return(obj);
    }

    public void ClearPool(EPoolName poolName)
    {
        if (m_pools.TryGetValue(poolName, out object poolObj) == false)
        {
            Debug.LogWarning($"Pool '{poolName}' does not exist.");
            return;
        }

        if (poolObj is ObjectPool<Component> pool)
            pool.Clear();

        m_pools.Remove(poolName);
    }

    public void ClearAllPools()
    {
        foreach (var poolObj in m_pools.Values)
        {
            if (poolObj is ObjectPool<Component> pool)
                pool.Clear();
        }

        m_pools.Clear();
    }

    public bool HasPool(EPoolName poolName)
    {
        return m_pools.ContainsKey(poolName);
    }

    public int GetPoolCount(EPoolName poolName)
    {
        if (m_pools.TryGetValue(poolName, out object poolObj) == false)
            return -1;

        if (poolObj is ObjectPool<Component> pool)
            return pool.PoolCount;

        return -1;
    }

    public ParticleSystem GetParticleSystem_Play_AutoReturn(EPoolName poolName, Vector3 position)
    {
        ParticleSystem ps = Get<ParticleSystem>(poolName);
        if (ps == null) return null;
        ps.transform.position = position;
        ps.Play();
        m_owner.StartCoroutine(AutoReturnParticle(poolName, ps));
        return ps;
    }

    private IEnumerator AutoReturnParticle(EPoolName poolName, ParticleSystem ps)
    {
        yield return new WaitForSeconds(ps.main.duration);

        if (ps == null || ps.gameObject == null)
        {
            Debug.LogWarning($"[PoolManager] ParticleSystem was destroyed during playback for pool: {poolName}");
            yield break;
        }

        ps.Stop();
        Return(poolName, ps);
    }

    public ParticleSystem GetParticleSystem_Play_AutoReturn(EPoolName poolName, Transform parent)
    {
        ParticleSystem ps = Get<ParticleSystem>(poolName);
        if (ps == null) return null;

        Transform originalParent = ps.transform.parent;
        ps.transform.SetParent(parent);
        ps.transform.localPosition = Vector3.zero;
        ps.Play();
        m_owner.StartCoroutine(AutoReturnParticleWithParent(poolName, ps, originalParent));
        return ps;
    }

    private IEnumerator AutoReturnParticleWithParent(EPoolName poolName, ParticleSystem ps, Transform originalParent)
    {
        yield return new WaitForSeconds(ps.main.duration);

        if (ps == null || ps.gameObject == null)
        {
            Debug.LogWarning($"[PoolManager] ParticleSystem was destroyed during playback for pool: {poolName}");
            yield break;
        }

        ps.Stop();
        ps.transform.SetParent(originalParent);
        Return(poolName, ps);
    }

    public EffectBase GetEffect_Play_AutoReturn(EPoolName poolName, Vector3 position)
    {
        EffectBase effect = Get<EffectBase>(poolName);
        if (effect == null) return null;
        effect.transform.position = position;
        effect.Play();
        m_owner.StartCoroutine(AutoReturnEffect(poolName, effect));
        return effect;
    }

    private IEnumerator AutoReturnEffect(EPoolName poolName, EffectBase effect)
    {
        float ddd = effect.GetParticleSystem().main.duration;
        yield return new WaitForSeconds(effect.GetParticleSystem().main.duration);

        if (effect == null || effect.gameObject == null)
        {
            Debug.LogWarning($"[PoolManager] EffectBase was destroyed during playback for pool: {poolName}");
            yield break;
        }

        effect.Stop();
        Return(poolName, effect);
    }

    public EffectBase GetEffect_Play_AutoReturn(EPoolName poolName, Transform parent)
    {
        EffectBase effect = Get<EffectBase>(poolName);
        if (effect == null) return null;

        Transform originalParent = effect.transform.parent;
        effect.transform.SetParent(parent);
        effect.transform.localPosition = Vector3.zero;
        effect.Play();
        m_owner.StartCoroutine(AutoReturnEffectWithParent(poolName, effect, originalParent));
        return effect;
    }

    private IEnumerator AutoReturnEffectWithParent(EPoolName poolName, EffectBase effect, Transform originalParent)
    {
        yield return new WaitForSeconds(effect.GetParticleSystem().main.duration);

        if (effect == null || effect.gameObject == null)
        {
            Debug.LogWarning($"[PoolManager] EffectBase was destroyed during playback for pool: {poolName}");
            yield break;
        }

        effect.Stop();
        effect.transform.SetParent(originalParent);
        Return(poolName, effect);
    }

}
