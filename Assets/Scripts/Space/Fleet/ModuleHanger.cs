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
    public float airAdditionalDelay;

    public float airHealthMax;
    public int airAmmoMax;
    public float lastReturnTime;
    public bool isReady;

    // 출격 시 누적된 공격 배율 (진형·함선수·전술). 귀환 시 UpdateAircraftInfo에서 1f로 원복
    public float airAttackMultiplier;

    public AircraftInfo(ModuleData moduleData)
    {
        UpdateAircraftInfo(moduleData);
        this.lastReturnTime = 0f;
        this.isReady = true;
    }

    public void UpdateAircraftInfo(ModuleData moduleData)
    {
        this.airLaunchDist       = moduleData.airLaunchDist;
        this.airHealth           = moduleData.airHealth;
        this.airAttack           = moduleData.airAttack;
        this.airAttackRange      = moduleData.airAttackRange;
        this.airAttackCool       = moduleData.airAttackCool;
        this.airSpeed            = moduleData.airSpeed;
        this.airAmmo             = moduleData.airAmmo;
        this.airDetectRadius     = moduleData.airDetectRadius;
        this.airAvoidRadius      = moduleData.airAvoidRadius;
        this.airAdditionalDelay  = moduleData.airAdditionalDelay;

        this.airHealthMax        = moduleData.airHealth;
        this.airAmmoMax          = moduleData.airAmmo;
        this.airAttackMultiplier = 1f;
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

    // Body 교체 시 기존 모듈 승계용 — 새 부모 body로 갱신
    public void SetParentBody(ModuleBody parentBody)
    {
        m_parentBody = parentBody;
    }

    public void InitializeModuleHanger(ModuleInfo moduleInfo, ModuleBody parentBody, ModuleSlot moduleSlot)
    {
        m_moduleInfo = moduleInfo;
        m_parentBody = parentBody;
        m_moduleSlot = moduleSlot;
        SetInvestedModulePoint(moduleInfo.investedModulePoint);
        SetAddShipModulePoint(moduleInfo.addShipModulePoint);
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
            m_health    *= m_ownerShip.m_hangerMultiplier;
            m_healthMax *= m_ownerShip.m_hangerMultiplier;
            foreach (var info in m_aircraftPool)
            {
                info.airHealth      *= m_ownerShip.m_hangerMultiplier;
                info.airHealthMax   *= m_ownerShip.m_hangerMultiplier;
                info.airAttack      *= m_ownerShip.m_hangerMultiplier;
                info.airSpeed       *= m_ownerShip.m_hangerMultiplier;
                info.airAttackRange *= m_ownerShip.m_hangerMultiplier;
                info.airAmmo        = Mathf.Max(1, Mathf.RoundToInt(info.airAmmo    * m_ownerShip.m_hangerMultiplier));
                info.airAmmoMax     = Mathf.Max(1, Mathf.RoundToInt(info.airAmmoMax * m_ownerShip.m_hangerMultiplier));
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
            if (m_moduleState.IsBattleState() == false) { yield return null; continue; }

            if (IsSilenced() == false && m_currentTarget != null && m_currentTarget.m_health > 0)
            {
                float hangerHarassDelay = m_ownerShip != null ? m_ownerShip.GetHarassAdditionalCool() : 0f;
                if (Time.time >= m_lastLaunchTime + m_launchCool + hangerHarassDelay)
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
        // LauncherAircraft는 DamageInfo를 사용하지 않음 — 배율은 출격 시 FireCoroutine에서 AircraftInfo에 저장
        DamageInfo unused = default;
        foreach (var launcher in m_launchers)
        {
            if (launcher != null)
                launcher.FireAtTarget(target.transform, unused, this);
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
        if (m_aircraftPool.Contains(aircraftInfo) == true) return; // 중복 반환 방지
        if (m_aircraftPool.Count >= m_airCount) return;            // 교체/업그레이드 후 한도 초과 방지
        aircraftInfo.airHealth      = 0f;
        aircraftInfo.lastReturnTime = Time.time;
        aircraftInfo.isReady        = false;
        m_aircraftPool.Add(aircraftInfo);
    }

    // 모듈 교체 전 상태 캡처용 스냅샷 (호출 즉시 복사해서 사용할 것)
    public List<AircraftInfo> GetAircraftPoolSnapshot()
    {
        return m_aircraftPool;
    }

    // 모듈 교체 시 이전 격납고의 함재기 상태 승계 — 출격 중이던 수량만큼 슬롯을 비워둬 복귀 시 초과 폐기 방지
    public void InheritAircraftPool(List<AircraftInfo> previousPool, int previousOutstandingCount)
    {
        m_aircraftPool.Clear();

        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, m_moduleInfo.moduleLevel);
        if (moduleData == null) return;

        foreach (AircraftInfo aircraftInfo in previousPool)
        {
            if (m_aircraftPool.Count >= m_airCount) break;
            aircraftInfo.UpdateAircraftInfo(moduleData);
            m_aircraftPool.Add(aircraftInfo);
        }

        int reservedForOutstanding = Mathf.Min(previousOutstandingCount, m_airCount - m_aircraftPool.Count);
        int remainingNewSlots = m_airCount - m_aircraftPool.Count - reservedForOutstanding;
        for (int i = 0; i < remainingNewSlots; i++)
        {
            m_aircraftPool.Add(new AircraftInfo(moduleData));
        }
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

    // 비전투→전투 전환 시 호출 — 미귀환 상태인 풀 내 함재기도 준비 상태로 초기화, 정원 대비 부족분은 신규 보충
    public void ReadyAllAircraft()
    {
        for (int i = 0; i < m_aircraftPool.Count; i++)
            m_aircraftPool[i].isReady = true;

        int shortageCount = m_airCount - m_aircraftPool.Count;
        if (shortageCount <= 0) return;

        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, m_moduleInfo.moduleLevel);
        if (moduleData == null) return;

        for (int i = 0; i < shortageCount; i++)
            m_aircraftPool.Add(new AircraftInfo(moduleData));
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
        // airAttack은 함재기 1기당 공격력이라, 이 격납고의 총 화력(1기당 공격력 × 함재기 수)으로 환산
        // — 함선에 격납고가 여러 개면 상위(ModuleBody)에서 단순 합산되므로 여기서 미리 곱해둬야 총 화력 합산이 맞음
        stats.airAttack = moduleData.airAttack * moduleData.airCount;
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
