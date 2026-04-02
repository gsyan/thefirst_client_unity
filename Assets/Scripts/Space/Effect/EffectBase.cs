//------------------------------------------------------------------------------
using UnityEngine;

public class EffectBase : MonoBehaviour
{
    // EPoolName 문자열 - enum 직렬화 시 중간 삽입에 의한 인덱스 밀림 방지
    [SerializeField] protected string m_poolName;

    public virtual void ReturnToPool_Effect()
    {
        if (ObjectManager.Instance != null && System.Enum.TryParse(m_poolName, out EPoolName poolName))
            ObjectManager.Instance.m_poolManager.Return(poolName, this);
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
