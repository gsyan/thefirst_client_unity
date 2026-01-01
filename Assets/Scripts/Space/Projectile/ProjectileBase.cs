//------------------------------------------------------------------------------
using UnityEngine;

public abstract class ProjectileBase : MonoBehaviour
{
    protected Transform m_firePointTransform;
    protected ModuleBase m_target;
    protected float m_damage;
    protected ModuleBase m_sourceModuleBase = null;

    // Body 교체 시에도 유효한 발사 함선 참조
    protected SpaceShip m_sourceShip = null;

    public virtual void InitializeProjectile(Transform firePointTransform, ModuleBase targetTransform, float damage, ModuleData moduleData,
                          Color color, ModuleBase sourceModuleBase)
    {
        m_firePointTransform = firePointTransform;
        m_target = targetTransform;
        m_damage = damage;
        m_sourceModuleBase = sourceModuleBase;

        // Body 교체 시에도 유효하도록 SpaceShip 참조 미리 저장
        if (sourceModuleBase != null)
        {
            m_sourceShip = sourceModuleBase.GetSpaceShip();
        }
    }

}
