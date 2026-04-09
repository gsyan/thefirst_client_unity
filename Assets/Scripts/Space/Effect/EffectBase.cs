//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections;

public class EffectBase : MonoBehaviour
{
    // EPoolName 문자열 - enum 직렬화 시 중간 삽입에 의한 인덱스 밀림 방지
    [SerializeField] protected string m_poolName;

    public virtual void PlayEffect()
    {
        ParticleSystem ps = GetComponent<ParticleSystem>();
        ps.Play();
        // 루프가 아니라면 한번만 재생
        if (ps.main.loop == false)
            StartCoroutine(ReturnEffectAfterDuration(ps));
        else
            Debug.Log($"name : {this.name}");

    }
    
    public virtual void PlayEffectOnce()
    {
        ParticleSystem ps = GetComponent<ParticleSystem>();
        ps.Play();
        // 무조건 한번만 재생
        StartCoroutine(ReturnEffectAfterDuration(ps));
    }


    private IEnumerator ReturnEffectAfterDuration(ParticleSystem ps)
    {
        yield return new WaitForSeconds(ps.main.duration);

        if (ps == null || ps.gameObject == null)
        {
            Debug.LogWarning($"[PoolManager] ParticleSystem was destroyed during playback for pool: {m_poolName}");
            yield break;
        }

        ReturnEffect();
    }

    public virtual void ReturnEffect()
    {
        StopEffect();
        if (ObjectManager.Instance != null && System.Enum.TryParse(m_poolName, out EPoolName poolName))
            ObjectManager.Instance.m_poolManager.Return(poolName, this);
    }
    
    public virtual void StopEffect()
    {
        GetComponent<ParticleSystem>().Stop();
    }
    public virtual ParticleSystem GetParticleSystem()
    {
        return GetComponent<ParticleSystem>();
    }

    
}
