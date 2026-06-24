//------------------------------------------------------------------------------
using UnityEngine;

public abstract class ProjectileBase : MonoBehaviour
{
    protected Transform m_firePointTransform;
    protected ModuleBase m_target;
    protected float m_damage;
    // Body 교체 시에도 유효한 발사 함선 참조
    protected SpaceShip m_sourceShip = null;
    // 발사 시 결정된 함체 타격 지점 (월드 좌표 고정값)
    protected Vector3 m_hitPoint;

    protected void SetCommonData(Transform firePointTransform, ModuleBase target, float damage, ModuleBase sourceModuleBase, Vector3 hitPoint = default)
    {
        m_firePointTransform = firePointTransform;
        m_target = target;
        m_damage = damage;
        m_hitPoint = (hitPoint == default && target != null) ? target.transform.position : hitPoint;

        // Body 교체 시에도 유효하도록 SpaceShip 참조 미리 저장
        if (sourceModuleBase != null)
            m_sourceShip = sourceModuleBase.GetSpaceShip();
    }

}
