//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class AircraftInfo
{
    public float airLaunchDist;
    public float airHealth;
    public float airAttack;
    public float airAttackRange;
    public float airAttackCool;
    public float airSpeed;
    public int airAmmo;
    public float airDetectRadius;
    public float airAvoidRadius;

    public float airHealthMax;
    public int airAmmoMax;
    public float lastReturnTime;
    public bool isReady;

    public AircraftInfo(ModuleData moduleData)
    {
        UpdateAircraftInfo(moduleData);
        this.lastReturnTime = 0f;
        this.isReady = true;
    }

    public void UpdateAircraftInfo(ModuleData moduleData)
    {
        this.airLaunchDist = moduleData.airLaunchDist;
        this.airHealth = moduleData.airHealth;
        this.airAttack = moduleData.airAttack;
        this.airAttackRange = moduleData.airAttackRange;
        this.airAttackCool = moduleData.airAttackCool;
        this.airSpeed = moduleData.airSpeed;
        this.airAmmo = moduleData.airAmmo;
        this.airDetectRadius = moduleData.airDetectRadius;
        this.airAvoidRadius = moduleData.airAvoidRadius;

        this.airHealthMax = moduleData.airHealth;
        this.airAmmoMax = moduleData.airAmmo;
    }
}

public class ModuleHanger : ModuleBase
{
    [SerializeField] private ModuleBody m_parentBody;
    public ModuleInfo m_moduleInfo; 
    
    [SerializeField] private float m_launchCool;
    [SerializeField] private int m_airCount;
    [SerializeField] private float m_airMaintenanceTime;

    [SerializeField] private float m_lastLaunchTime;
    [SerializeField] private List<AircraftInfo> m_aircraftPool = new List<AircraftInfo>();

    // 발사대 관련
    [SerializeField] private List<LauncherBase> m_launchers = new List<LauncherBase>();

    private ModuleBody m_currentTarget;
    private Coroutine m_autoAttackCoroutine;
    private Coroutine m_maintenanceCoroutine;

    public void InitializeModuleHanger(ModuleInfo moduleInfo, ModuleBody parentBody, ModuleSlot moduleSlot)
    {
        m_moduleInfo = moduleInfo;
        m_parentBody = parentBody;
        m_moduleSlot = moduleSlot;
        SetInvestedModulePoint(moduleInfo.investedModulePoint);
        SetInvestedMineral(moduleInfo.investedMineral);

        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, m_moduleInfo.moduleLevel);
        if (moduleData == null)
        {
            Debug.LogError("Failed to restore module data for ModuleHanger");
            return;
        }

        // 복원된 데이터로 스탯 설정
        m_health = moduleData.health;
        m_healthMax = moduleData.health;
        

        m_airCount = moduleData.airCount;
        //m_airCount = 1; // test
        m_launchCool = moduleData.attackCool;
        m_airMaintenanceTime = moduleData.airMaintenanceTime;
        //m_airMaintenanceTime = 1; // test

        // 업그레이드 비용 설정
        m_modulePointCostLevelup = moduleData.modulePointCost;

        m_lastLaunchTime = 0f;

        // 함재기 데이터 풀 초기화
        int totalAircraftCount = m_airCount;
        for (int i = 0; i < totalAircraftCount; i++)
        {
            AircraftInfo aircraftInfo = new AircraftInfo(moduleData);
            m_aircraftPool.Add(aircraftInfo);
        }

        // 런처 설정
        LauncherAircraft launcher = gameObject.AddComponent<LauncherAircraft>();
        launcher.InitializeLauncherAircraft(this, 0);
        m_launchers.Add(launcher);

        AutoDetectFleetInfo();

        // Zone 적 함선일 때 격납고 체력·함재기 스탯에 배율 적용
        if (m_ownerFleet != null && m_ownerFleet.IsZoneEnemy == true)
        {
            m_health    *= m_myShip.m_hangerMultiplier;
            m_healthMax *= m_myShip.m_hangerMultiplier;
            foreach (var info in m_aircraftPool)
            {
                info.airHealth      *= m_myShip.m_hangerMultiplier;
                info.airHealthMax   *= m_myShip.m_hangerMultiplier;
                info.airAttack      *= m_myShip.m_hangerMultiplier;
                info.airSpeed       *= m_myShip.m_hangerMultiplier;
                info.airAttackRange *= m_myShip.m_hangerMultiplier;
                info.airAmmo        = Mathf.Max(1, Mathf.RoundToInt(info.airAmmo    * m_myShip.m_hangerMultiplier));
                info.airAmmoMax     = Mathf.Max(1, Mathf.RoundToInt(info.airAmmoMax * m_myShip.m_hangerMultiplier));
            }
        }

        if (m_parentBody != null)
            m_parentBody.AddHanger(this);
    }

    public override void Start()
    {
        m_autoAttackCoroutine = StartCoroutine(AutoAttack());
        m_maintenanceCoroutine = StartCoroutine(MaintenanceProcess());
    }

    public override void RestartCoroutines()
    {
        if (m_autoAttackCoroutine != null)
        {
            StopCoroutine(m_autoAttackCoroutine);
        }
        if (m_maintenanceCoroutine != null)
        {
            StopCoroutine(m_maintenanceCoroutine);
        }
        m_autoAttackCoroutine = StartCoroutine(AutoAttack());
        m_maintenanceCoroutine = StartCoroutine(MaintenanceProcess());
    }

    public override float GetLastAttackTime() { return m_lastLaunchTime; }
    public override void SetLastAttackTime(float t) { m_lastLaunchTime = t; }

    private IEnumerator AutoAttack()
    {
        while (true)
        {
            if (m_moduleState.IsBattleState() == false) yield return null;

            if (m_currentTarget != null && m_currentTarget.m_health > 0)
            {
                if (Time.time >= m_lastLaunchTime + m_launchCool)
                {
                    ExecuteLaunchOnTarget(m_currentTarget);
                    m_lastLaunchTime = Time.time;
                }
            }

            yield return null;
        }
    }
    
    private void ExecuteLaunchOnTarget(ModuleBody target)
    {
        foreach (var launcher in m_launchers)
        {
            if (launcher != null)
                launcher.FireAtTarget(target, 0f, this);
        }
    }

    private IEnumerator MaintenanceProcess()
    {
        while (true)
        {
            foreach (AircraftInfo aircraft in m_aircraftPool)
            {
                if (aircraft.isReady == false)
                {
                    float elapsedTime = Time.time - aircraft.lastReturnTime;
                    if (elapsedTime >= m_airMaintenanceTime)
                    {
                        aircraft.airHealth = aircraft.airHealthMax;
                        aircraft.airAmmo = aircraft.airAmmoMax;
                        aircraft.isReady = true;
                    }
                }
            }
            yield return new WaitForSeconds(1f);
        }
    }

    public AircraftInfo GetReadyAircraft()
    {
        for (int i = 0; i < m_aircraftPool.Count; i++)
        {
            if (m_aircraftPool[i].isReady == true)
            {
                AircraftInfo aircraft = m_aircraftPool[i];
                m_aircraftPool.RemoveAt(i);
                return aircraft;
            }
        }
        return null;
    }

    public void ReturnAircraft(AircraftInfo aircraftInfo)
    {
        // 복귀 시 현재 격납고의 최신 스펙으로 재정비
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, m_moduleInfo.moduleLevel);
        if (moduleData != null)
            aircraftInfo.UpdateAircraftInfo(moduleData);

        aircraftInfo.lastReturnTime = Time.time;
        aircraftInfo.isReady = false;
        m_aircraftPool.Add(aircraftInfo);
    }

    public int GetReadyAircraftCount()
    {
        int count = 0;
        foreach (AircraftInfo aircraft in m_aircraftPool)
        {
            if (aircraft.isReady == true)
                count++;
        }
        return count;
    }

    public override EModuleType GetModuleType()
    {
        return m_moduleInfo.moduleType;
    }
    public override EModuleSubType GetModuleSubType()
    {
        return m_moduleInfo.moduleSubType;
    }
    public override int GetModuleSlotIndex()
    {
        return m_moduleInfo.slotIndex;
    }
    public override int GetModuleLevel()
    {
        return m_moduleInfo.moduleLevel;
    }

    public override void SetModuleLevel(int level)
    {
        m_moduleInfo.moduleLevel = level;
    }

    public override void ApplyModuleLevelUp(int newLevel)
    {
        // 레벨 설정
        SetModuleLevel(newLevel);

        // 새 레벨의 ModuleData 가져오기
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, newLevel);
        if (moduleData == null) return;
        
        // 스탯 갱신
        m_healthMax = moduleData.health;
        m_health = Mathf.Min(m_health, m_healthMax);
        
        // 함재기 관련 스탯 (레벨업 전 용량 저장)
        int oldCapacity = m_airCount; // 이전 레벨의 총 함재기 수

        m_airCount = moduleData.airCount; // 새 레벨의 총 함재기 수
        m_launchCool = moduleData.attackCool;
        m_airMaintenanceTime = moduleData.airMaintenanceTime;

        m_modulePointCostLevelup = moduleData.modulePointCost;

        // 함재기 풀 재조정 (데이터상 총 함재기 수 비교)
        int newCapacity = m_airCount;

        int capacityDiff = newCapacity - oldCapacity;

        if (capacityDiff > 0)
        {
            // 용량 증가: 새 함재기를 격납고에 추가
            for (int i = 0; i < capacityDiff; i++)
            {
                AircraftInfo aircraftInfo = new AircraftInfo(moduleData);
                m_aircraftPool.Add(aircraftInfo);
            }
        }
        else if (capacityDiff < 0)
        {
            // 용량 감소: 격납고에서 함재기 제거 (정비 중인 것 우선)
            int toRemove = -capacityDiff;
            // 정비 중인 함재기부터 제거
            for (int i = m_aircraftPool.Count - 1; i >= 0 && toRemove > 0; i--)
            {
                if (!m_aircraftPool[i].isReady)
                {
                    m_aircraftPool.RemoveAt(i);
                    toRemove--;
                }
            }
            // 아직 제거할 게 남았다면 준비된 함재기도 제거
            for (int i = m_aircraftPool.Count - 1; i >= 0 && toRemove > 0; i--)
            {
                m_aircraftPool.RemoveAt(i);
                toRemove--;
            }
        }

        // 격납고에 있는 함재기들의 스펙 업데이트 (출격 중인 함재기는 복귀 시 자동 업데이트)
        foreach (var aircraft in m_aircraftPool)
            aircraft.UpdateAircraftInfo(moduleData);
    }

    public override int GetModuleBodyIndex()
    {
        return m_moduleInfo.bodyIndex;
    }

    public override void SetModuleBodyIndex(int bodyIndex)
    {
        m_moduleInfo.bodyIndex = bodyIndex;
    }

    

    public int GetHangarCapability() => m_airCount;
    public float GetLaunchCool() => m_launchCool;
    public float GetMaintenanceTime() => m_airMaintenanceTime;

    public void SetTarget(ModuleBody target)
    {
        m_currentTarget = target;
    }

    // 현재 모함의 타겟 반환 (함재기 목표 재할당용)
    public ModuleBody GetCurrentTarget()
    {
        if (m_currentTarget != null && m_currentTarget.gameObject.activeSelf && m_currentTarget.m_health > 0)
            return m_currentTarget;
        return null;
    }


    public override CapabilityProfile GetModuleCapabilityProfile(bool bByInfo)
    {
        if (bByInfo == true) return CommonUtility.GetModuleCapabilityProfile(m_moduleInfo);

        CapabilityProfile stats = new CapabilityProfile();
        stats.totalWeapons = 1;
        // 함재기 데이터로부터 계산
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, m_moduleInfo.moduleLevel);
        stats.airAttack = moduleData.airAttack;
        stats.airCount = moduleData.airCount;

        return stats;
    }

    // 파괴 시 정리
    private void OnDestroy()
    {
        if (m_parentBody != null)
            m_parentBody.RemoveHanger(this);
    }
}
