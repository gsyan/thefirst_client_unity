// 즉시 발사 빔 런처 - ProjectileBeamInstant 를 풀에서 꺼내 발사
using UnityEngine;
using System.Collections;

public class LauncherBeamInstant : LauncherBase
{
    private ModuleData m_moduleData;

    public void InitializeLauncherBeamInstant(ModuleData moduleData, int firePointIndex, bool isEnemy = false)
    {
        if (m_isInitialized == true) return;

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
        m_isInitialized = true;
    }

    public override void Fire(ModuleBase target, float damage, ModuleBase sourceModuleBase = null)
    {
        if (m_isInitialized == false) return;
        StartCoroutine(FireCoroutine(target, damage, sourceModuleBase));
    }

    private IEnumerator FireCoroutine(ModuleBase target, float damage, ModuleBase sourceModuleBase)
    {
        if (target == null) yield break;

        ProjectileBeamInstant beam = ObjectManager.Instance.m_poolManager.Get<ProjectileBeamInstant>(EPoolName.PROJECTILE_BEAM_INSTANT);
        if (beam == null) yield break;

        beam.transform.position = m_firePoint.position;
        beam.InitializeProjectile(m_firePoint, target, damage, m_moduleData, Color.white, sourceModuleBase);
    }
}
