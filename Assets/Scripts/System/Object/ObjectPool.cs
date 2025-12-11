//------------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public class ObjectPool<T> where T : Component
{
    private Queue<T> m_pool;
    private T m_prefab;
    private Transform m_parent;
    private int m_initialSize;
    private int m_maxSize;

    public ObjectPool(T prefab, int initialSize = 10, int maxSize = 100, Transform parent = null)
    {
        m_prefab = prefab;
        m_initialSize = initialSize;
        m_maxSize = maxSize;
        m_parent = parent;
        m_pool = new Queue<T>();

        for (int i = 0; i < m_initialSize; i++)
            CreateNewObject();
    }

    private T CreateNewObject()
    {
        T obj = Object.Instantiate(m_prefab, m_parent);
        obj.gameObject.SetActive(false);
        m_pool.Enqueue(obj);
        return obj;
    }

    public T Get()
    {
        T obj;

        if (m_pool.Count > 0)
        {
            obj = m_pool.Dequeue();
        }
        else
        {
            obj = Object.Instantiate(m_prefab, m_parent);
        }

        obj.gameObject.SetActive(true);
        return obj;
    }

    public void Return(T obj)
    {
        if (obj == null) return;

        obj.gameObject.SetActive(false);

        if (m_pool.Count < m_maxSize)
        {
            m_pool.Enqueue(obj);
        }
        else
        {
            Object.Destroy(obj.gameObject);
        }
    }

    public void Clear()
    {
        while (m_pool.Count > 0)
        {
            T obj = m_pool.Dequeue();
            if (obj != null)
                Object.Destroy(obj.gameObject);
        }
    }

    public int PoolCount => m_pool.Count;
}
