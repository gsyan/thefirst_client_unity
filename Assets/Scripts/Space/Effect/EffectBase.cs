//------------------------------------------------------------------------------
using UnityEngine;

public class EffectBase : MonoBehaviour
{
    [SerializeField] protected EPoolName m_poolName;

    public virtual void ReturnToPool()
    {
        ObjectManager.Instance.m_poolManager.Return(m_poolName, this);
    }
    
    public virtual void Play()
    {
        GetComponent<ParticleSystem>().Play();
    }
    public virtual void Stop()
    {
        GetComponent<ParticleSystem>().Stop();
    }
    public virtual ParticleSystem GetParticleSystem()
    {
        return GetComponent<ParticleSystem>();
    }
}
