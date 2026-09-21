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

        // 출격 시 최신 격납고 스펙으로 세팅 — 공격력/체력/탄약은 원본 moduleData가 아니라 ModuleHangar가 강화 포인트를 반영해 확정해둔 값을 씀
        // (그렇지 않으면 재출격마다 강화 포인트가 반영 안 된 원본 티어값으로 되돌아감)
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(
            m_moduleHangar.m_moduleInfo.moduleSubType);
        if (moduleData != null)
            aircraftInfo.UpdateAircraftInfo(moduleData, m_moduleHangar.GetFinalAttackToShip(), m_moduleHangar.GetFinalAttackToFighter(),
                m_moduleHangar.GetFinalAirHealth(), m_moduleHangar.GetFinalAirAmmo(), m_moduleHangar.GetFinalAirDisrupt());

        SpaceShip carrierShip = m_moduleHangar.GetSpaceShip();
        SpaceFleet ownerFleet = carrierShip != null ? carrierShip.m_ownerFleet : null;

        SoundManager.Instance.PlayFX(EFx.Aircraft_Launch, transform.position);

        AircraftStandard aircraft = ObjectManager.Instance.m_poolManager.Get<AircraftStandard>(EPoolName.AIRCRAFT_STANDARD);
        if (aircraft == null)
        {
            Debug.LogError("[LauncherAircraft] AIRCRAFT_STANDARD 풀 고갈 — aircraftInfo 반환");
            m_moduleHangar.ReturnAircraft(aircraftInfo);
            yield break;
        }

        // 실제 발진이 확정된 시점에만 공격 배율 조립 — airAttack은 원본 유지, 배율만 airAttackMultiplier에 저장(귀환 시 UpdateAircraftInfo로 1f 원복)
        // 전술 보너스가 실제로 적용 중일 때만 발진 1건당 과금 — 여유가 없으면 이번 발진부터 보너스 없이(토글은 TryChargeAircraftTacticCost가 이미 꺼둠) 그대로 발진
        float shipCountMultiplier = ownerFleet != null ? ownerFleet.GetShipCountAttackMultiplier() : 1f;
        float formationMultiplier = ownerFleet != null ? ownerFleet.GetFormationAttackMultiplier() : 1f;
        float tacticMultiplier    = ownerFleet != null ? ownerFleet.GetAircraftTacticAttackMultiplier() : 1f;
        if (tacticMultiplier > 1f)
        {
            int tacticHangerCost = DataManager.Instance.m_dataTableConfig.gameSettings.tactic.tacticHangerCost;
            if (ownerFleet.TryChargeAircraftTacticCost(tacticHangerCost) == false)
                tacticMultiplier = 1f;
        }
        aircraftInfo.airAttackMultiplier = shipCountMultiplier * formationMultiplier * tacticMultiplier;

        aircraft.transform.position = m_firePoint.position;
        aircraft.transform.rotation = m_firePoint.rotation;
        aircraft.InitializeAirCraft(m_firePoint, target, aircraftInfo, m_moduleHangar, Color.black);
    }

}
