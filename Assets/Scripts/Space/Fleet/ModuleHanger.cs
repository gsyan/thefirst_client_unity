//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class AircraftInfo
{
    public float launchStraightDistance;
    public float health;    
    public float attackPower;
    public float attackRange;
    public float attackCooldown;
    public float moveSpeed;
    public int ammo;
    public float detectionRadius;
    public float avoidanceRadius;

    public float healthMax;
    public int ammoMax;
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
        this.launchStraightDistance = moduleData.m_aircraftLaunchStraightDistance;            
        this.health = moduleData.m_aircraftHealth;
        this.attackPower = moduleData.m_aircraftAttackPower;
        this.attackRange = moduleData.m_aircraftAttackRange;
        this.attackCooldown = moduleData.m_aircraftAttackCooldown;
        this.moveSpeed = moduleData.m_aircraftSpeed;            
        this.ammo = moduleData.m_aircraftAmmo;
        this.detectionRadius = moduleData.m_aircraftDetectionRadius;
        this.avoidanceRadius = moduleData.m_aircraftAvoidanceRadius;            
        
        this.healthMax = moduleData.m_aircraftHealth;
        this.ammoMax = moduleData.m_aircraftAmmo;
    }
}

public class ModuleHanger : ModuleBase
{
    [SerializeField] private ModuleBody m_parentBody;
    public ModuleInfo m_moduleInfo; 

    [SerializeField] private int m_hangarCapability;
    [SerializeField] private int m_scoutCapability;
    [SerializeField] private float m_launchCool;
    [SerializeField] private int m_launchCount;
    [SerializeField] private float m_maintenanceTime;

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

        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, m_moduleInfo.moduleLevel);
        if (moduleData == null)
        {
            Debug.LogError("Failed to restore module data for ModuleHanger");
            return;
        }

        // 복원된 데이터로 스탯 설정
        m_health = moduleData.m_health;
        m_healthMax = moduleData.m_health;
        

        m_hangarCapability = moduleData.m_hangarCapability;
        //m_hangarCapability = 1; // test
        m_scoutCapability = moduleData.m_scoutCapability;
        m_launchCool = moduleData.m_launchCool;
        m_launchCount = moduleData.m_launchCount;
        m_maintenanceTime = moduleData.m_maintenanceTime;
        //m_maintenanceTime = 1; // test

        // 업그레이드 비용 설정
        m_upgradeCost = moduleData.m_upgradeCost;

        m_lastLaunchTime = 0f;

        // 함재기 데이터 풀 초기화
        int totalAircraftCount = m_hangarCapability;
        for (int i = 0; i < totalAircraftCount; i++)
        {
            AircraftInfo aircraftInfo = new AircraftInfo(moduleData);
            m_aircraftPool.Add(aircraftInfo);
        }

        // 서브 타입 초기화
        InitializeHangerSubType(moduleData);

        AutoDetectFleetInfo();

        if (m_parentBody != null)
            m_parentBody.AddHanger(this);
    }

    private void InitializeHangerSubType(ModuleData moduleData)
    {
        switch (m_moduleInfo.moduleSubType)
        {
            case EModuleSubType.Hanger_Standard:
                for(int i=0; i< moduleData.m_launchCount; i++)
                {
                    LauncherAircraft launcher = gameObject.AddComponent<LauncherAircraft>();
                    launcher.InitializeLauncherAircraft(this);
                    m_launchers.Add(launcher);
                }
                break;
            case EModuleSubType.Hanger_Advanced:
                for(int i=0; i< moduleData.m_launchCount; i++)
                {
                    LauncherAircraft launcher = gameObject.AddComponent<LauncherAircraft>();
                    launcher.InitializeLauncherAircraft(this);
                    m_launchers.Add(launcher);
                }
                break;
            default:
                break;
        }
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

    private IEnumerator AutoAttack()
    {
        while (true)
        {
            if (m_health <= 0)
            {
                yield return null;
                continue;
            }

            if( m_moduleState != EModuleState.Battle ) yield return null;

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
                    if (elapsedTime >= m_maintenanceTime)
                    {
                        aircraft.health = aircraft.healthMax;
                        aircraft.ammo = aircraft.ammoMax;
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

    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);

        if (m_health <= 0)
        {
            Debug.Log($"[{GetFleetName()}] ModuleHanger[Body{m_moduleInfo.bodyIndex}-Slot{m_moduleSlot.m_moduleSlotInfo.slotIndex}] destroyed!");

            if (m_parentBody != null)
                m_parentBody.RemoveHanger(this);

            gameObject.SetActive(false);
        }
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
        m_healthMax = moduleData.m_health;
        m_health = Mathf.Min(m_health, m_healthMax);
        
        // 함재기 관련 스탯 (레벨업 전 용량 저장)
        int oldCapacity = m_hangarCapability; // 이전 레벨의 총 함재기 수

        m_hangarCapability = moduleData.m_hangarCapability; // 새 레벨의 총 함재기 수
        m_scoutCapability = moduleData.m_scoutCapability;
        m_launchCool = moduleData.m_launchCool;
        m_launchCount = moduleData.m_launchCount;
        m_maintenanceTime = moduleData.m_maintenanceTime;

        m_upgradeCost = moduleData.m_upgradeCost;

        // 함재기 풀 재조정 (데이터상 총 함재기 수 비교)
        int newCapacity = m_hangarCapability;

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

        Debug.Log($"ModuleHanger leveled up to {newLevel}: HP={m_healthMax}, HangarCap={oldCapacity}->{newCapacity}, InHangar={m_aircraftPool.Count}, AircraftAttack={m_attackPower}");
    }

    public override int GetModuleBodyIndex()
    {
        return m_moduleInfo.bodyIndex;
    }

    public override void SetModuleBodyIndex(int bodyIndex)
    {
        m_moduleInfo.bodyIndex = bodyIndex;
    }

    

    public int GetHangarCapability() => m_hangarCapability;
    public int GetScoutCapability() => m_scoutCapability;
    public float GetLaunchCool() => m_launchCool;
    public int GetLaunchCount() => m_launchCount;
    public float GetMaintenanceTime() => m_maintenanceTime;

    public void SetTarget(ModuleBody target)
    {
        m_currentTarget = target;
    }


    public override string GetUpgradeComparisonText()
    {
        ModuleData currentStats = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, m_moduleInfo.moduleLevel);
        ModuleData upgradeStats = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleInfo.moduleSubType, m_moduleInfo.moduleLevel + 1);

        if (currentStats == null)
            return "Current module data not found.";

        if (upgradeStats == null)
            return "Upgrade not available (max level reached).";

        string comparison = $"=== UPGRADE COMPARISON ===\n";
        comparison += $"Level: {currentStats.m_moduleLevel} -> {upgradeStats.m_moduleLevel}\n";
        comparison += $"HP: {currentStats.m_health:F0} -> {upgradeStats.m_health:F0}\n";
        comparison += $"Hangar: {currentStats.m_hangarCapability:F0} -> {upgradeStats.m_hangarCapability:F0}\n";
        comparison += $"Scout: {currentStats.m_scoutCapability:F0} -> {upgradeStats.m_scoutCapability:F0}\n";
        string costString = $"Cost: Tech Level {currentStats.m_upgradeCost.techLevel}";
        if (currentStats.m_upgradeCost.mineral > 0)
            costString += $", Mineral {currentStats.m_upgradeCost.mineral}";
        if (currentStats.m_upgradeCost.mineralRare > 0)
            costString += $", MineralRare {currentStats.m_upgradeCost.mineralRare}";
        if (currentStats.m_upgradeCost.mineralExotic > 0)
            costString += $", MineralExotic {currentStats.m_upgradeCost.mineralExotic}";
        if (currentStats.m_upgradeCost.mineralDark > 0)
            costString += $", MineralDark {currentStats.m_upgradeCost.mineralDark}";
        comparison += costString;

        return comparison;
    }
}
