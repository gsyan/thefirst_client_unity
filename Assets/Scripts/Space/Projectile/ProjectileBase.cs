//------------------------------------------------------------------------------
using UnityEngine;

public abstract class ProjectileBase : MonoBehaviour
{
    protected Transform m_firePointTransform;
    protected ModuleBase m_target;
    protected float m_damage;
    protected ModuleBase m_sourceModuleBase = null;

    public virtual void InitializeProjectile(Transform firePointTransform, ModuleBase targetTransform, float damage, ModuleData moduleData,
                          Color color, ModuleBase sourceModuleBase)
    {
        m_firePointTransform = firePointTransform;
        m_target = targetTransform;
        m_damage = damage;
        m_sourceModuleBase = sourceModuleBase;
    }

}
