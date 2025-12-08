//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections;
using Unity.VisualScripting;

public class LauncherBeam : LauncherBase
{
    private ModuleWeaponData m_moduleWeaponData;

    [SerializeField] private Color m_beamColor = Color.cyan;

    public void InitializeLauncherBeam(ModuleWeaponData moduleData)
    {
        if (m_isInitialized == true) return;

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

        m_moduleWeaponData = moduleData;

        m_isInitialized = true;
    }



    public override void Fire(ModuleBase target, float damage, ModuleBase sourceModuleBase = null)
    {
        if (m_isInitialized == false) return;
        StartCoroutine(FireBeamCoroutine(target, damage, sourceModuleBase));
    }

    private IEnumerator FireBeamCoroutine(ModuleBase target, float damage, ModuleBase sourceModuleBase)
    {
        ParticleSystem muzzleEffect = ObjectManager.Instance.m_poolManager.GetParticleSystem_Play_AutoReturn(EPoolName.EFFECT_BEAM_MUZZLE, m_firePoint);

        if (m_audioSource != null && m_audioSource.clip != null)
            m_audioSource.Play();

        yield return new WaitForSeconds(muzzleEffect.main.duration * 0.5f);
        if (target == null) yield break;

        ProjectileBeam beam = ObjectManager.Instance.m_poolManager.Get<ProjectileBeam>(EPoolName.PROJECTILE_BEAM);
        if (beam == null) yield break;
        beam.transform.position = m_firePoint.position;

        beam.InitializeProjectile(m_firePoint, target, damage, m_moduleWeaponData, m_beamColor, sourceModuleBase);
    }

}
