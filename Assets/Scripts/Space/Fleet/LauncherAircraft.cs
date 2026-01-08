//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections;

public class LauncherAircraft : LauncherBase
{
    private ModuleHanger m_moduleHanger;

    public void InitializeLauncherAircraft(ModuleHanger moduleHanger)
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

        m_moduleHanger = moduleHanger;

        m_isInitialized = true;
    }

    public override void Fire(ModuleBase target, float damage, ModuleBase sourceModuleBase = null)
    {
        if (m_isInitialized == false) return;
        StartCoroutine(FireCoroutine(target, sourceModuleBase));
    }

    private IEnumerator FireCoroutine(ModuleBase target, ModuleBase sourceModuleBase)
    {
        AircraftInfo aircraftInfo = m_moduleHanger.GetReadyAircraft();
        if (aircraftInfo == null) yield break;

        //ParticleSystem muzzleEffect = ObjectManager.Instance.m_poolManager.GetParticleSystem_Play_AutoReturn(EPoolName.EFFECT_BEAM_MUZZLE, m_firePoint);

        if (m_audioSource != null && m_audioSource.clip != null)
            m_audioSource.Play();

        //yield return new WaitForSeconds(muzzleEffect.main.duration * 0.5f);
        if (target == null)
        {
            m_moduleHanger.ReturnAircraft(aircraftInfo);
            yield break;
        }

        AircraftStandard aircraft = ObjectManager.Instance.m_poolManager.Get<AircraftStandard>(EPoolName.AIRCRAFT_STANDARD);
        if (aircraft == null)
        {
            m_moduleHanger.ReturnAircraft(aircraftInfo);
            yield break;
        }

        aircraft.transform.position = m_firePoint.position;
        aircraft.transform.rotation = m_firePoint.rotation;
        aircraft.InitializeAirCraft(m_firePoint, target, aircraftInfo, m_moduleHanger, Color.black, sourceModuleBase);
    }

}
