// 발사기 기반 클래스 — 빔/미사일/함재기 공통 Fire 인터페이스 및 AudioSource 피치 동기화
using UnityEngine;
using System.Collections;

public class LauncherBase : MonoBehaviour
{
    protected bool m_isInitialized = false;
    protected Transform m_firePoint;
    [SerializeField] protected AudioSource m_audioSource;

    protected virtual void Awake()
    {
        EventManager.Subscribe_GameSpeedChanged(OnGameSpeedChanged);
    }

    public void FireAtTarget(ModuleBase target, float damage, ModuleBase sourceModuleBase = null)
    {
        if (target != null)
            Fire(target, damage, sourceModuleBase);
    }

    public virtual void Fire(ModuleBase target, float damage, ModuleBase sourceModuleBase = null)
    {
        if (m_isInitialized == false) return;
    }

    public Transform GetFirePoint()
    {
        return m_firePoint;
    }

    private void OnGameSpeedChanged(float speed, float pitch)
    {
        if (m_audioSource != null)
            m_audioSource.pitch = pitch;
    }

    protected Transform FindFirePointByIndex(int index)
    {
        FirePoint[] firePoints = GetComponentsInChildren<FirePoint>();
        foreach (var fp in firePoints)
        {
            if (fp.Index == index)
                return fp.transform;
        }
        return null;
    }

    protected virtual void OnDestroy()
    {
        EventManager.Unsubscribe_GameSpeedChanged(OnGameSpeedChanged);

        // Launcher가 파괴될 때 자식으로 붙어있는 파티클/이펙트를 보호
        // (PoolManager의 AutoReturn 코루틴이 완료되기 전에 파괴되는 것 방지)
        if (m_firePoint != null && m_firePoint.childCount > 0)
        {
            for (int i = m_firePoint.childCount - 1; i >= 0; i--)
            {
                Transform child = m_firePoint.GetChild(i);
                if (child != null)
                {
                    // ParticleSystem 또는 EffectBase를 가진 자식만 분리
                    if (child.GetComponent<ParticleSystem>() != null || child.GetComponent<EffectBase>() != null)
                    {
                        child.SetParent(null);
                    }
                }
            }
        }
    }

}
