//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections;

public class LauncherAircraft : LauncherBase
{
    private ModuleHanger m_moduleHanger;

    public void InitializeLauncherAircraft(ModuleHanger moduleHanger, int firePointIndex = 0)
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

        m_moduleHanger = moduleHanger;

        m_isInitialized = true;
    }

    public override void Fire(ModuleBase target, float damage, ModuleBase sourceModuleBase = null, Vector3 hitPoint = default, float explosionMultiplier = 1f)
    {
        if (m_isInitialized == false) return;
        StartCoroutine(FireCoroutine(target));
    }

    private IEnumerator FireCoroutine(ModuleBase target)
    {
        AircraftInfo aircraftInfo = m_moduleHanger.GetReadyAircraft();
        if (aircraftInfo == null) yield break;

        // 함재기 전술 강화 ON: 출격 시 공격력·탄약 배율 적용 (귀환 시 UpdateAircraftInfo로 원복)
        SpaceShip carrierShip = m_moduleHanger.GetSpaceShip();
        SpaceFleet ownerFleet = carrierShip != null ? carrierShip.m_ownerFleet : null;
        bool aircraftTacticOn = ownerFleet != null
            && ownerFleet.m_fleetInfo != null
            && (ownerFleet.m_fleetInfo.tacticOptions & 4) != 0;
        if (aircraftTacticOn == true)
        {
            GameSettings settings = DataManager.Instance.m_dataTableConfig.gameSettings;
            aircraftInfo.airAttack = aircraftInfo.airAttack * settings.aircraftTacticDamageMultiplier;
            aircraftInfo.airAmmo   = Mathf.RoundToInt(aircraftInfo.airAmmoMax * settings.aircraftTacticAmmoMultiplier);
        }

        //ParticleSystem muzzleEffect = ObjectManager.Instance.m_poolManager.GetParticleSystem_Play_AutoReturn(EPoolName.EFFECT_BEAM_MUZZLE, m_firePoint);

        SoundManager.Instance.PlayFX(EFx.Aircraft_Launch, transform.position);

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
        aircraft.InitializeAirCraft(m_firePoint, target, aircraftInfo, m_moduleHanger, Color.black);
    }

}
