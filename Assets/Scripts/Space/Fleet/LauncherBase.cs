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
}
