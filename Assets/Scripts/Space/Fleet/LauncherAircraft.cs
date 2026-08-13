//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections;

public class LauncherAircraft : LauncherBase
{
    private ModuleHangar m_moduleHangar;

    public void InitializeLauncherAircraft(ModuleHangar moduleHangar, int firePointIndex = 0)
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

        m_moduleHangar = moduleHangar;

        m_isInitialized = true;
    }

    public override void Fire(Transform target, DamageInfo damageInfo, ModuleBase sourceModuleBase = null, Vector3 hitPoint = default, float explosionMultiplier = 1f)
    {
        if (m_isInitialized == false) return;
        StartCoroutine(FireCoroutine(target));
    }

    private IEnumerator FireCoroutine(Transform target)
    {
        if (target == null) yield break;

        AircraftInfo aircraftInfo = m_moduleHangar.GetReadyAircraft();
        if (aircraftInfo == null) yield break;

        // 출격 시 최신 격납고 스펙으로 세팅
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(
            m_moduleHangar.m_moduleInfo.moduleSubType);
        if (moduleData != null)
            aircraftInfo.UpdateAircraftInfo(moduleData);

        // 출격 시 공격 배율 조립 — airAttack은 원본 유지, 배율만 airAttackMultiplier에 저장 (귀환 시 UpdateAircraftInfo로 1f 원복)
        SpaceShip carrierShip = m_moduleHangar.GetSpaceShip();
        SpaceFleet ownerFleet = carrierShip != null ? carrierShip.m_ownerFleet : null;
        float shipCountMultiplier = ownerFleet != null ? ownerFleet.GetShipCountAttackMultiplier() : 1f;
        float formationMultiplier = ownerFleet != null ? ownerFleet.GetFormationAttackMultiplier() : 1f;
        aircraftInfo.airAttackMultiplier = shipCountMultiplier * formationMultiplier;

        SoundManager.Instance.PlayFX(EFx.Aircraft_Launch, transform.position);

        AircraftStandard aircraft = ObjectManager.Instance.m_poolManager.Get<AircraftStandard>(EPoolName.AIRCRAFT_STANDARD);
        if (aircraft == null)
        {
            Debug.LogError("[LauncherAircraft] AIRCRAFT_STANDARD 풀 고갈 — aircraftInfo 반환");
            m_moduleHangar.ReturnAircraft(aircraftInfo);
            yield break;
        }
        
        aircraft.transform.position = m_firePoint.position;
        aircraft.transform.rotation = m_firePoint.rotation;
        aircraft.InitializeAirCraft(m_firePoint, target, aircraftInfo, m_moduleHangar, Color.black);
    }

}
