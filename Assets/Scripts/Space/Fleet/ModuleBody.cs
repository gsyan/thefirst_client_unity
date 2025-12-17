//------------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class ModuleBody : ModuleBase
{
    [HideInInspector] public ModuleBodyInfo m_moduleBodyInfo;

    [HideInInspector] public List<ModuleSlot> m_moduleSlots = new List<ModuleSlot>();
    [HideInInspector] public List<ModuleEngine> m_engines = new List<ModuleEngine>();
    [HideInInspector] public List<ModuleWeapon> m_weapons = new List<ModuleWeapon>();    
    [HideInInspector] public List<ModuleHanger> m_hangers = new List<ModuleHanger>();

    [HideInInspector] public float m_cargoCapacity;

    private ModuleBody m_currentTarget;

    public override void ApplyShipStateToModule()
    {
        base.ApplyShipStateToModule();
        
        for (int i = 0; i < m_engines.Count; i++)
        {
            if (m_engines[i] != null)
                m_engines[i].ApplyShipStateToModule();
        }
        
        for (int i = 0; i < m_weapons.Count; i++)
        {
            if (m_weapons[i] != null)
                m_weapons[i].ApplyShipStateToModule();
        }

        for (int i = 0; i < m_hangers.Count; i++)
        {
            if (m_hangers[i] != null)
                m_hangers[i].ApplyShipStateToModule();
        }
    }
    
    public override EModuleType GetModuleType()
    {
        return EModuleType.Body;
    }
    public override int GetPackedModuleType()
    {
        return m_moduleBodyInfo.moduleType;
    }
    public override int GetModuleLevel()
    {
        return m_moduleBodyInfo.moduleLevel;
    }
    public override void SetModuleLevel(int level)
    {
        m_moduleBodyInfo.moduleLevel = level;
    }
    public override int GetModuleBodyIndex()
    {
        return m_moduleBodyInfo.bodyIndex;
    }
    public override void SetModuleBodyIndex(int bodyIndex)
    {
        m_moduleBodyInfo.bodyIndex = bodyIndex;
    }

    public void InitializeModuleBody(ModuleBodyInfo moduleBodyInfo)
    {
        m_moduleBodyInfo = moduleBodyInfo;
        m_moduleSlot = null;

        // 서버 데이터로부터 완전한 모듈 데이터 복원
        ModuleBodyData moduleData = DataManager.Instance.RestoreBodyModuleData(moduleBodyInfo.ModuleSubType, moduleBodyInfo.moduleLevel);
        if (moduleData == null)
        {
            Debug.LogError("Failed to restore module data for ModuleBody");
            return;
        }

        // 복원된 데이터로 초기화
        m_health = moduleData.m_health;
        m_healthMax = moduleData.m_health;
        m_attackPower = 0.0f; // Body는 직접 공격하지 않음

        // Body 전용 능력치
        m_cargoCapacity = moduleData.m_cargoCapacity;

        // 업그레이드 비용 설정
        m_upgradeCost = moduleData.m_upgradeCost;

        // 함대 정보 자동 설정
        AutoDetectFleetInfo();

        CollectAndSortModuleSlots();

        if (moduleBodyInfo.engines != null)
        {
            foreach (ModuleEngineInfo engineInfo in moduleBodyInfo.engines)
                InitializeEngine(engineInfo);
        }

        if (moduleBodyInfo.weapons != null)
        {
            foreach (ModuleWeaponInfo weaponInfo in moduleBodyInfo.weapons)
                InitializeWeapon(weaponInfo);
        }

        if (moduleBodyInfo.hangers != null)
        {
            foreach (ModuleHangerInfo hangerInfo in moduleBodyInfo.hangers)
                InitializeHanger(hangerInfo);
        }
    }

    private void InitializeEngine(ModuleEngineInfo engineInfo)
    {
        GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(engineInfo.ModuleType.ToString(), engineInfo.ModuleSubType.ToString(), engineInfo.moduleLevel);
        if (modulePrefab == null)
        {
            Debug.LogWarning($"InitializeEngine: Cannot find module prefab - Level: {engineInfo.moduleLevel}");
            return;
        }

        ModuleSlot targetSlot = FindModuleSlot(engineInfo.moduleType, engineInfo.slotIndex);
        if (targetSlot == null)
        {
            Debug.LogWarning($"InitializeEngine: Cannot find engine slot {engineInfo.slotIndex}");
            return;
        }

        if (targetSlot.transform.childCount > 0)
        {
            Debug.LogWarning($"InitializeEngine: Engine slot {engineInfo.slotIndex} is already occupied");
            return;
        }

        GameObject engineObj = Instantiate(modulePrefab, targetSlot.transform.position, targetSlot.transform.rotation);
        engineObj.transform.SetParent(targetSlot.transform);

        ModuleEngine moduleEngine = engineObj.GetComponent<ModuleEngine>();
        if (moduleEngine == null)
            moduleEngine = engineObj.AddComponent<ModuleEngine>();

        moduleEngine.InitializeModuleEngine(engineInfo, this, targetSlot);
    }

    private void InitializeWeapon(ModuleWeaponInfo weaponInfo)
    {
        GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(weaponInfo.ModuleType.ToString(), weaponInfo.ModuleSubType.ToString(), weaponInfo.moduleLevel);
        if (modulePrefab == null)
        {
            Debug.LogWarning($"InitializeWeapon: Cannot find module prefab - ModuleType: {weaponInfo.ModuleType},  ModuleSubType: {weaponInfo.ModuleSubType},  Level: {weaponInfo.moduleLevel}");
            return;
        }

        ModuleSlot targetSlot = FindModuleSlot(weaponInfo.moduleType, weaponInfo.slotIndex);
        if (targetSlot == null)
        {
            Debug.LogWarning($"InitializeWeapon: Cannot find weapon slot {weaponInfo.slotIndex}");
            return;
        }

        if (targetSlot.transform.childCount > 0)
        {
            Debug.LogWarning($"InitializeWeapon: Weapon slot {weaponInfo.slotIndex} is already occupied");
            return;
        }

        GameObject weaponObj = Instantiate(modulePrefab, targetSlot.transform.position, targetSlot.transform.rotation);
        weaponObj.transform.SetParent(targetSlot.transform);

        ModuleWeapon moduleWeapon = weaponObj.GetComponent<ModuleWeapon>();
        if (moduleWeapon == null)
            moduleWeapon = weaponObj.AddComponent<ModuleWeapon>();

        moduleWeapon.InitializeModuleWeapon(weaponInfo, this, targetSlot);
    }

    private void InitializeHanger(ModuleHangerInfo hangerInfo)
    {
        GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(hangerInfo.ModuleType.ToString(), hangerInfo.ModuleSubType.ToString(), hangerInfo.moduleLevel);
        if (modulePrefab == null)
        {
            Debug.LogWarning($"InitializeHanger: Cannot find module prefab - Level: {hangerInfo.moduleLevel}");
            return;
        }

        ModuleSlot targetSlot = FindModuleSlot(hangerInfo.moduleType, hangerInfo.slotIndex);
        if (targetSlot == null)
        {
            Debug.LogWarning($"InitializeHanger: Cannot find hanger slot {hangerInfo.slotIndex}");
            return;
        }

        if (targetSlot.transform.childCount > 0)
        {
            Debug.LogWarning($"InitializeHanger: Hanger slot {hangerInfo.slotIndex} is already occupied");
            return;
        }

        GameObject hangerObj = Instantiate(modulePrefab, targetSlot.transform.position, targetSlot.transform.rotation);
        hangerObj.transform.SetParent(targetSlot.transform);

        ModuleHanger moduleHanger = hangerObj.GetComponent<ModuleHanger>();
        if (moduleHanger == null)
            moduleHanger = hangerObj.AddComponent<ModuleHanger>();

        moduleHanger.InitializeModuleHanger(hangerInfo, this, targetSlot);
    }

    public void CollectAndSortModuleSlots()
    {
        m_moduleSlots.Clear();

        // 이 바디의 자식들로부터 모든 ModuleSlot 수집
        ModuleSlot[] childSlots = GetComponentsInChildren<ModuleSlot>();
        m_moduleSlots.AddRange(childSlots);

        // 모듈 타입별, 슬롯 인덱스별로 정렬
        m_moduleSlots.Sort((slot1, slot2) =>
        {
            int typeComparison = slot1.m_moduleType.CompareTo(slot2.m_moduleType);
            if (typeComparison != 0)
                return typeComparison;
            return slot1.m_slotIndex.CompareTo(slot2.m_slotIndex);
        });

    }

    // 엔진 추가
    public void AddEngine(ModuleEngine engine)
    {
        if (!m_engines.Contains(engine))
        {
            m_engines.Add(engine);
        }
    }

    // 무기 추가
    public void AddWeapon(ModuleWeapon weapon)
    {
        if (!m_weapons.Contains(weapon))
        {
            m_weapons.Add(weapon);
        }
    }

    // 행거 추가
    public void AddHanger(ModuleHanger hanger)
    {
        if (!m_hangers.Contains(hanger))
        {
            m_hangers.Add(hanger);
        }
    }

    // 엔진 제거
    public void RemoveEngine(ModuleEngine engine)
    {
        if (m_engines.Remove(engine))
        {
        }
    }

    // 무기 제거
    public void RemoveWeapon(ModuleWeapon weapon)
    {
        if (m_weapons.Remove(weapon))
        {
        }
    }

    // 행거 제거
    public void RemoveHanger(ModuleHanger hanger)
    {
        if (m_hangers.Remove(hanger))
        {
        }
    }

    // 특정 타입과 인덱스의 슬롯 찾기
    public ModuleSlot FindModuleSlot(int moduleType, int slotIndex)
    {
        return m_moduleSlots.FirstOrDefault(slot =>
            CommonUtility.CompareModuleTypeForSlot(slot.m_moduleType, moduleType)
            && slot.m_slotIndex == slotIndex);
    }

    // 사용 가능한 슬롯 찾기 (빈 슬롯)
    public ModuleSlot FindAvailableSlot(int moduleType)
    {
        return m_moduleSlots.FirstOrDefault(slot =>
            CommonUtility.CompareModuleTypeForSlot(slot.m_moduleType, moduleType)
            && slot.transform.childCount == 0);
    }

    public void SetTarget(ModuleBody target)
    {
        foreach (ModuleWeapon weapon in m_weapons)
        {
            if (weapon != null && weapon.m_health > 0)
                weapon.SetTarget(target);
        }

        foreach (ModuleHanger hanger in m_hangers)
        {
            if (hanger != null && hanger.m_health > 0)
                hanger.SetTarget(target);
        }

    }

    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);

        // 바디가 파괴되면 장착된 모든 모듈도 비활성화
        if (m_health <= 0)
        {
            //Debug.Log($"[{GetFleetName()}] ModuleBody[{m_moduleBodyInfo.bodyIndex}] destroyed!");

            // 엔진들 비활성화
            foreach (var engine in m_engines)
            {
                if (engine != null)
                    engine.gameObject.SetActive(false);
            }

            // 무기들 비활성화
            foreach (var weapon in m_weapons)
            {
                if (weapon != null)
                    weapon.gameObject.SetActive(false);
            }

            // 행거들 비활성화
            foreach (var hanger in m_hangers)
            {
                if (hanger != null)
                    hanger.gameObject.SetActive(false);
            }

            // 상위 SpaceShip에 자신의 파괴를 알림
            SpaceShip parentShip = GetComponentInParent<SpaceShip>();
            if (parentShip != null)
                parentShip.CheckForDestruction();
        }
    }

    // 총 이동 속도 계산 (모든 엔진의 합)
    public float GetTotalMovementSpeed()
    {
        float totalSpeed = 0f;
        foreach (var engine in m_engines)
        {
            if (engine != null && engine.m_health > 0)
            {
                totalSpeed += engine.GetMovementSpeed();
            }
        }
        return totalSpeed;
    }

    // 총 회전 속도 계산 (모든 엔진의 합)
    public float GetTotalRotationSpeed()
    {
        float totalSpeed = 0f;
        foreach (var engine in m_engines)
        {
            if (engine != null && engine.m_health > 0)
            {
                totalSpeed += engine.GetRotationSpeed();
            }
        }
        return totalSpeed;
    }

    // 총 화물 용량 반환 (Body 자체 용량)
    public float GetTotalCargoCapacity()
    {
        return m_cargoCapacity;
    }
    
    // 체력 비율 반환
    public float GetHealthRatio()
    {
        return m_healthMax > 0 ? m_health / m_healthMax : 0f;
    }
    
    // 바디가 사용 가능한 상태인지 체크
    public bool IsOperational()
    {
        return m_health > 0;
    }

    public override string GetUpgradeComparisonText()
    {
        var currentStats = DataManager.Instance.RestoreBodyModuleData(m_moduleBodyInfo.ModuleSubType, m_moduleBodyInfo.moduleLevel);
        var upgradeStats = DataManager.Instance.RestoreBodyModuleData(m_moduleBodyInfo.ModuleSubType, m_moduleBodyInfo.moduleLevel + 1);

        if (currentStats == null)
            return "Current module data not found.";

        if (upgradeStats == null)
            return "Upgrade not available (max level reached).";

        string comparison = $"=== UPGRADE COMPARISON ===\n";
        comparison += $"Level: {currentStats.m_level} -> {upgradeStats.m_level}\n";
        comparison += $"HP: {currentStats.m_health:F0} -> {upgradeStats.m_health:F0}\n";
        comparison += $"Cargo: {currentStats.m_cargoCapacity:F0} -> {upgradeStats.m_cargoCapacity:F0}\n";
        string costString = $"Cost: Tech Level {currentStats.m_upgradeCost.techLevel}";
        if (currentStats.m_upgradeCost.mineral > 0)
            costString += $", Mineral {currentStats.m_upgradeCost.mineral}";
        if (currentStats.m_upgradeCost.mineralRare > 0)
            costString += $", MineralRare {currentStats.m_upgradeCost.mineral}";
        if (currentStats.m_upgradeCost.mineralExotic > 0)
            costString += $", MineralExotic {currentStats.m_upgradeCost.mineral}";
        if (currentStats.m_upgradeCost.mineralDark > 0)
            costString += $", MineralDark {currentStats.m_upgradeCost.mineral}";
        comparison += costString;

        return comparison;
    }
}
