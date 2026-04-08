//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections;

public class LauncherMissile : LauncherBase
{
    private ModuleData m_moduleData;
    private EPoolName m_missilePoolName;

    public void InitializeLauncherMissile(ModuleData moduleData, int firePointIndex, EPoolName missilePoolName)
    {
        if (m_isInitialized == true) return;

        // 인덱스에 맞는 FirePoint 찾기
        m_firePoint = FindFirePointByIndex(firePointIndex);
        if (m_firePoint == null)
            m_firePoint = transform;

        if (m_audioSource == null)
        {
            m_audioSource = GetComponent<AudioSource>();
            if (m_audioSource == null)
            {
                m_audioSource = gameObject.AddComponent<AudioSource>();
                m_audioSource.playOnAwake = false;
            }
        }

        m_moduleData = moduleData;
        m_missilePoolName = missilePoolName;

        m_isInitialized = true;
    }

    public override void Fire(ModuleBase target, float damage, ModuleBase sourceModuleBase = null)
    {
        if (m_isInitialized == false) return;
        StartCoroutine(FireMissileCoroutine(target, damage, sourceModuleBase));
    }

    private IEnumerator FireMissileCoroutine(ModuleBase target, float damage, ModuleBase sourceModuleBase)
    {
        //ParticleSystem muzzleEffect = ObjectManager.Instance.m_poolManager.GetParticleSystem_Play_AutoReturn(EPoolName.EFFECT_BEAM_MUZZLE, m_firePoint);

        if (m_audioSource != null && m_audioSource.clip != null)
            m_audioSource.Play();

        //yield return new WaitForSeconds(muzzleEffect.main.duration * 0.5f);
        if (target == null) yield break;

        ProjectileMissile missile = ObjectManager.Instance.m_poolManager.Get<ProjectileMissile>(m_missilePoolName);
        if (missile == null) yield break;
        missile.transform.position = m_firePoint.position;
        missile.transform.rotation = m_firePoint.rotation;
        missile.SetPoolName(m_missilePoolName);

        // 함선 발사대: 발사구 위쪽 방향으로 콜드런치
        missile.InitializeProjectile(m_firePoint, target, damage, m_moduleData, Color.black, sourceModuleBase, m_firePoint.forward);
    }

}
