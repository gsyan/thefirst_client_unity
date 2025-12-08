//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class AircraftInfo
{
    public float health;
    public float healthMax;
    public float attackPower;
    public int ammo;
    public int ammoMax;
    public float lastReturnTime;
    public bool isReady;

    public AircraftInfo(float health, float attackPower, int ammo)
    {
        this.healthMax = health;
        this.health = health;
        this.attackPower = attackPower;
        this.ammoMax = ammo;
        this.ammo = ammo;
        this.lastReturnTime = 0f;
        this.isReady = true;
    }
}

public class ModuleHanger : ModuleBase
{
    [SerializeField] private ModuleBody m_parentBody;
    public ModuleHangerInfo m_moduleHangerInfo; 

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

    public void InitializeModuleHanger(ModuleHangerInfo moduleHangerInfo, ModuleBody parentBody, ModuleSlot slot)
    {
        m_moduleHangerInfo = moduleHangerInfo;
        m_parentBody = parentBody;
        m_moduleSlot = slot;

        ModuleHangerData moduleData = DataManager.Instance.RestoreHangerModuleData(m_moduleHangerInfo.ModuleSubType, m_moduleHangerInfo.moduleLevel);
        if (moduleData == null)
        {
            Debug.LogError("Failed to restore module data for ModuleHanger");
            return;
        }

        // 복원된 데이터로 스탯 설정
        m_health = moduleData.m_health;
        m_healthMax = moduleData.m_health;
        m_attackPower = moduleData.m_aircraftAttackPower;

        m_hangarCapability = moduleData.m_hangarCapability;
        //m_hangarCapability = 1; // test
        m_scoutCapability = moduleData.m_scoutCapability;
        m_launchCool = moduleData.m_launchCool;
        m_launchCount = moduleData.m_launchCount;
        m_maintenanceTime = moduleData.m_maintenanceTime;
        m_maintenanceTime = 1; // test

        // 업그레이드 비용 설정
        m_upgradeMoneyCost = moduleData.m_upgradeMoneyCost;
        m_upgradeMineralCost = moduleData.m_upgradeMineralCost;

        m_lastLaunchTime = 0f;

        // 함재기 데이터 풀 초기화
        int totalAircraftCount = m_hangarCapability;
        for (int i = 0; i < totalAircraftCount; i++)
        {
            AircraftInfo aircraftInfo = new AircraftInfo(
                moduleData.m_aircraftHealth,
                moduleData.m_aircraftAttackPower,
                moduleData.m_aircraftAmmo
            );
            m_aircraftPool.Add(aircraftInfo);
        }

        // 서브 타입 초기화
        InitializeHangerSubType(moduleData);

        AutoDetectFleetInfo();

        if (m_parentBody != null)
            m_parentBody.AddHanger(this);
    }

    private void InitializeHangerSubType(ModuleHangerData moduleData)
    {
        switch (m_moduleHangerInfo.ModuleSubType)
        {
            case EModuleHangerSubType.Standard:
                for(int i=0; i< moduleData.m_launchCount; i++)
                {
                    LauncherAircraft launcher = gameObject.AddComponent<LauncherAircraft>();
                    launcher.InitializeLauncherAircraft(this);
                    m_launchers.Add(launcher);
                }
                break;
            case EModuleHangerSubType.Advanced:
                for(int i=0; i< moduleData.m_launchCount; i++)
                {

                }
                break;
            case EModuleHangerSubType.Military:

                break;
            default:
                break;
        }
    }

    override public void Start()
    {
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

    override public void TakeDamage(float damage)
    {
        base.TakeDamage(damage);

        if (m_health <= 0)
        {
            Debug.Log($"[{GetFleetName()}] ModuleHanger[Body{m_moduleHangerInfo.bodyIndex}-Slot{m_moduleSlot.m_slotIndex}] destroyed!");

            if (m_parentBody != null)
                m_parentBody.RemoveHanger(this);

            gameObject.SetActive(false);
        }
    }

    override public EModuleType GetModuleType()
    {
        return EModuleType.Hanger;
    }

    public override int GetPackedModuleType()
    {
        return m_moduleHangerInfo.moduleType;
    }

    override public int GetModuleLevel()
    {
        return m_moduleHangerInfo.moduleLevel;
    }

    override public void SetModuleLevel(int level)
    {
        m_moduleHangerInfo.moduleLevel = level;
    }

    override public int GetModuleBodyIndex()
    {
        return m_moduleHangerInfo.bodyIndex;
    }

    override public void SetModuleBodyIndex(int bodyIndex)
    {
        m_moduleHangerInfo.bodyIndex = bodyIndex;
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
        var currentStats = DataManager.Instance.RestoreHangerModuleData(m_moduleHangerInfo.ModuleSubType, m_moduleHangerInfo.moduleLevel);
        var upgradeStats = DataManager.Instance.RestoreHangerModuleData(m_moduleHangerInfo.ModuleSubType, m_moduleHangerInfo.moduleLevel + 1);

        if (currentStats == null)
            return "Current module data not found.";

        if (upgradeStats == null)
            return "Upgrade not available (max level reached).";

        string comparison = $"=== UPGRADE COMPARISON ===\n";
        comparison += $"Level: {currentStats.m_level} -> {upgradeStats.m_level}\n";
        comparison += $"HP: {currentStats.m_health:F0} -> {upgradeStats.m_health:F0}\n";
        comparison += $"Hangar: {currentStats.m_hangarCapability:F0} -> {upgradeStats.m_hangarCapability:F0}\n";
        comparison += $"Scout: {currentStats.m_scoutCapability:F0} -> {upgradeStats.m_scoutCapability:F0}\n";
        comparison += $"Cost: Money {currentStats.m_upgradeMoneyCost}, Mineral {currentStats.m_upgradeMineralCost}";

        return comparison;
    }
}
