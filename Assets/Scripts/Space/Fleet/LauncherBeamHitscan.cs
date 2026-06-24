// Hitscan 빔 런처 - ProjectileBeamHitscan 을 풀에서 꺼내 발사
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

    public override void Fire(ModuleBase target, float damage, ModuleBase sourceModuleBase = null, Vector3 hitPoint = default)
    {
        if (m_isInitialized == false) return;
        StartCoroutine(FireCoroutine(target, damage, sourceModuleBase, hitPoint));
    }

    private IEnumerator FireCoroutine(ModuleBase target, float damage, ModuleBase sourceModuleBase, Vector3 hitPoint)
    {
        if (target == null) yield break;

        ProjectileBeamHitscan beam = ObjectManager.Instance.m_poolManager.Get<ProjectileBeamHitscan>(EPoolName.PROJECTILE_BEAM_HITSCAN);
        if (beam == null) yield break;

        beam.transform.position = m_firePoint.position;
        beam.InitializeProjectileBeamHitscan(m_firePoint, target, damage, sourceModuleBase, hitPoint);
    }
}
