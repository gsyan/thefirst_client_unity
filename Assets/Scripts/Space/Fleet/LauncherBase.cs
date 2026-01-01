//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections;

public class LauncherBase : MonoBehaviour
{
    protected bool m_isInitialized = false;
    protected Transform m_firePoint;
    [SerializeField] protected AudioSource m_audioSource;

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

    protected virtual void OnDestroy()
    {
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
