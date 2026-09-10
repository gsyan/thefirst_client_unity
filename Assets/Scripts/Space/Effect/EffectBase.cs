//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EffectBase : MonoBehaviour
{
    // EPoolName 문자열 - enum 직렬화 시 중간 삽입에 의한 인덱스 밀림 방지
    [SerializeField] protected string m_poolName;

    private ParticleSystem m_ps;
    private static readonly List<ParticleSystem> s_particleSystemBuffer = new List<ParticleSystem>();

    protected virtual void Awake()
    {
        m_ps = GetComponent<ParticleSystem>();
    }

    public virtual void PlayEffect()
    {
        if (m_ps == null) return;
        m_ps.Play();
        // 루프가 아니라면 한번만 재생
        if (m_ps.main.loop == false)
            StartCoroutine(ReturnEffectAfterDuration(m_ps));
    }

    public virtual void PlayEffectOnce()
    {
        if (m_ps == null) return;
        m_ps.Play();
        // 무조건 한번만 재생
        StartCoroutine(ReturnEffectAfterDuration(m_ps));
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
        if (m_ps == null) return;
        m_ps.Stop();
    }

    public virtual ParticleSystem GetParticleSystem()
    {
        return m_ps;
    }

    // 인스턴스별 색 지정(빔 색 등) — 자식까지 포함한 모든 ParticleSystem의 시작 색을 덮어씀
    public virtual void SetColor(Color color)
    {
        GetComponentsInChildren(true, s_particleSystemBuffer);
        for (int i = 0; i < s_particleSystemBuffer.Count; i++)
        {
            ParticleSystem.MainModule main = s_particleSystemBuffer[i].main;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
        }
    }

    
}
