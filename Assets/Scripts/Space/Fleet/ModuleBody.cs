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

        // Apply ship state to all modules in slots
        foreach (ModuleSlot slot in m_moduleSlots)
        {
            if (slot != null && slot.transform.childCount > 0)
            {
                ModuleBase module = slot.GetComponentInChildren<ModuleBase>();
                if (module != null && (module is ModulePlaceholder) == false)
                    module.ApplyShipStateToModule();
            }
        }
    }
    
    public override EModuleType GetModuleType()
    {
        return m_moduleBodyInfo.ModuleType;
    }
    public override EModuleSubType GetModuleSubType()
    {
        return m_moduleBodyInfo.ModuleSubType;
    }
    public override EModuleSlotType GetModuleSlotType()
    {
        return m_moduleBodyInfo.ModuleSlotType;
    }
    public override int GetModuleTypePacked()
    {
        return m_moduleBodyInfo.moduleTypePacked;
    }
    public override int GetModuleLevel()
    {
        return m_moduleBodyInfo.moduleLevel;
    }
    public override void SetModuleLevel(int level)
    {
        m_moduleBodyInfo.moduleLevel = level;
    }

    public override void ApplyModuleLevelUp(int newLevel)
    {
        // 레벨 설정
        SetModuleLevel(newLevel);

        // 새 레벨의 ModuleData 가져오기
        ModuleData moduleData = DataManager.Instance.RestoreModuleData(m_moduleBodyInfo.moduleTypePacked, newLevel);
        if (moduleData == null)
        {
            Debug.LogError($"Failed to restore module data for level {newLevel}");
            return;
        }

        // 스탯 갱신
        m_healthMax = moduleData.m_health;
        m_health = Mathf.Min(m_health, m_healthMax);
        m_cargoCapacity = moduleData.m_cargoCapacity;
        m_upgradeCost = moduleData.m_upgradeCost;

        Debug.Log($"ModuleBody leveled up to {newLevel}: HP={m_healthMax}, CargoCapacity={m_cargoCapacity}");
    }

    public override int GetModuleBodyIndex()
    {
        return m_moduleBodyInfo.bodyIndex;
    }
    public override void SetModuleBodyIndex(int bodyIndex)
    {
        m_moduleBodyInfo.bodyIndex = bodyIndex;
    }

    // Body 초기화 (기존 모듈 재사용 가능)
    public void InitializeModuleBody(ModuleBodyInfo moduleBodyInfo, List<ModuleBase> savedModules)
    {
        m_moduleBodyInfo = moduleBodyInfo;
        m_moduleSlot = null;

        // 서버 데이터로부터 완전한 모듈 데이터 복원
        ModuleData moduleData = DataManager.Instance.RestoreModuleData(moduleBodyInfo.ModuleSubType, moduleBodyInfo.moduleLevel);
        if (moduleData == null) return;

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

        // savedModules가 있으면 먼저 재배치
        if (savedModules != null && savedModules.Count > 0)
            RestoreSavedModules(savedModules);

        // 빈 슬롯에 서버 정보대로 모듈 생성 (savedModules 유무와 관계없이)
        CreateMissingModules(moduleBodyInfo);
    }

    // 저장된 모듈을 슬롯에 재배치
    private void RestoreSavedModules(List<ModuleBase> savedModules)
    {
        SpaceShip myShip = GetSpaceShip();
        SpaceFleet myFleet = myShip != null ? myShip.m_myFleet : null;

        foreach (var module in savedModules)
        {
            int moduleTypePacked = module.GetModuleTypePacked();
            int oldSlotIndex = module.m_moduleSlot != null ? module.m_moduleSlot.m_moduleSlotInfo.slotIndex : 0;

            // 새 body에서 같은 타입과 인덱스의 슬롯 찾기
            ModuleSlot targetSlot = FindModuleSlot(moduleTypePacked, oldSlotIndex);

            if (targetSlot != null && targetSlot.transform.childCount == 0)
            {
                // 슬롯 찾음 - 모듈 배치
                module.transform.SetParent(targetSlot.transform);
                module.transform.localPosition = Vector3.zero;
                module.transform.localRotation = Quaternion.identity;
                module.gameObject.SetActive(true);

                // 모듈의 슬롯 참조 업데이트
                module.m_moduleSlot = targetSlot;

                // 모듈의 함대 정보 재설정 (부모가 바뀌었으므로)
                if (myShip != null && myFleet != null)
                    module.SetFleetInfo(myFleet, myShip);

                // 코루틴 재시작 (각 모듈에서 필요시 override)
                module.RestartCoroutines();

                Debug.Log($"Module preserved: {module.GetType().Name} at slot {oldSlotIndex}");
            }
            else
            {
                // 슬롯을 찾을 수 없음 - 모듈 파괴
                Debug.LogWarning($"Cannot find compatible slot for {module.GetType().Name} (type={moduleTypePacked}, slot={oldSlotIndex}). Module destroyed.");
                Destroy(module.gameObject);
            }
        }
    }

    // 서버 정보에 있지만 재배치되지 못한 모듈들을 생성
    private void CreateMissingModules(ModuleBodyInfo bodyInfo)
    {
        // 엔진 생성
        if (bodyInfo.engines != null)
        {
            foreach (var engineInfo in bodyInfo.engines)
            {
                ModuleSlot slot = FindModuleSlot(engineInfo.moduleTypePacked, engineInfo.slotIndex);
                if (slot != null && slot.transform.childCount == 0)
                    InitializeEngine(engineInfo);
            }
        }

        // 무기 생성
        if (bodyInfo.weapons != null)
        {
            foreach (var weaponInfo in bodyInfo.weapons)
            {
                ModuleSlot slot = FindModuleSlot(weaponInfo.moduleTypePacked, weaponInfo.slotIndex);
                if (slot != null && slot.transform.childCount == 0)
                    InitializeWeapon(weaponInfo);
            }
        }

        // 행거 생성
        if (bodyInfo.hangers != null)
        {
            foreach (var hangerInfo in bodyInfo.hangers)
            {
                ModuleSlot slot = FindModuleSlot(hangerInfo.moduleTypePacked, hangerInfo.slotIndex);
                if (slot != null && slot.transform.childCount == 0)
                    InitializeHanger(hangerInfo);
            }
        }

        // 빈 슬롯에 Placeholder 배치
        FillEmptySlotsWithPlaceholders();
    }

    private void InitializeEngine(ModuleInfo moduleInfo)
    {
        GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(moduleInfo.ModuleType.ToString(), moduleInfo.ModuleSubType.ToString(), moduleInfo.moduleLevel);
        if (modulePrefab == null)
        {
            Debug.LogWarning($"InitializeEngine: Cannot find module prefab - Level: {moduleInfo.moduleLevel}");
            return;
        }

        ModuleSlot targetSlot = FindModuleSlot(moduleInfo.moduleTypePacked, moduleInfo.slotIndex);
        if (targetSlot == null)
        {
            Debug.LogWarning($"InitializeEngine: Cannot find engine slot {moduleInfo.slotIndex}");
            return;
        }

        if (targetSlot.transform.childCount > 0)
        {
            Debug.LogWarning($"InitializeEngine: Engine slot {moduleInfo.slotIndex} is already occupied");
            return;
        }

        GameObject engineObj = Instantiate(modulePrefab, targetSlot.transform.position, targetSlot.transform.rotation);
        engineObj.transform.SetParent(targetSlot.transform);

        ModuleEngine moduleEngine = engineObj.GetComponent<ModuleEngine>();
        if (moduleEngine == null)
            moduleEngine = engineObj.AddComponent<ModuleEngine>();

        moduleEngine.InitializeModuleEngine(moduleInfo, this, targetSlot);
    }

    private void InitializeWeapon(ModuleInfo moduleInfo)
    {
        GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(moduleInfo.ModuleType.ToString(), moduleInfo.ModuleSubType.ToString(), moduleInfo.moduleLevel);
        if (modulePrefab == null)
        {
            Debug.LogWarning($"InitializeWeapon: Cannot find module prefab - ModuleType: {moduleInfo.ModuleType},  ModuleSubType: {moduleInfo.ModuleSubType},  Level: {moduleInfo.moduleLevel}");
            return;
        }

        ModuleSlot targetSlot = FindModuleSlot(moduleInfo.moduleTypePacked, moduleInfo.slotIndex);
        if (targetSlot == null)
        {
            Debug.LogWarning($"InitializeWeapon: Cannot find weapon slot {moduleInfo.slotIndex}");
            return;
        }

        if (targetSlot.transform.childCount > 0)
        {
            Debug.LogWarning($"InitializeWeapon: Weapon slot {moduleInfo.slotIndex} is already occupied");
            return;
        }

        GameObject weaponObj = Instantiate(modulePrefab, targetSlot.transform.position, targetSlot.transform.rotation);
        weaponObj.transform.SetParent(targetSlot.transform);

        ModuleWeapon moduleWeapon = weaponObj.GetComponent<ModuleWeapon>();
        if (moduleWeapon == null)
            moduleWeapon = weaponObj.AddComponent<ModuleWeapon>();

        moduleWeapon.InitializeModuleWeapon(moduleInfo, this, targetSlot);
    }

    private void InitializeHanger(ModuleInfo moduleInfo)
    {
        GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(moduleInfo.ModuleType.ToString(), moduleInfo.ModuleSubType.ToString(), moduleInfo.moduleLevel);
        if (modulePrefab == null)
        {
            Debug.LogWarning($"InitializeHanger: Cannot find module prefab - Level: {moduleInfo.moduleLevel}");
            return;
        }

        ModuleSlot targetSlot = FindModuleSlot(moduleInfo.moduleTypePacked, moduleInfo.slotIndex);
        if (targetSlot == null)
        {
            Debug.LogWarning($"InitializeHanger: Cannot find hanger slot {moduleInfo.slotIndex}");
            return;
        }

        if (targetSlot.transform.childCount > 0)
        {
            Debug.LogWarning($"InitializeHanger: Hanger slot {moduleInfo.slotIndex} is already occupied");
            return;
        }

        GameObject hangerObj = Instantiate(modulePrefab, targetSlot.transform.position, targetSlot.transform.rotation);
        hangerObj.transform.SetParent(targetSlot.transform);

        ModuleHanger moduleHanger = hangerObj.GetComponent<ModuleHanger>();
        if (moduleHanger == null)
            moduleHanger = hangerObj.AddComponent<ModuleHanger>();

        moduleHanger.InitializeModuleHanger(moduleInfo, this, targetSlot);
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
            int typeComparison = slot1.m_moduleTypePacked.CompareTo(slot2.m_moduleTypePacked);
            if (typeComparison != 0)
                return typeComparison;
            return slot1.m_moduleSlotInfo.slotIndex.CompareTo(slot2.m_moduleSlotInfo.slotIndex);
        });

    }

    private void FillEmptySlotsWithPlaceholders()
    {
        GameObject placeholderPrefab = ObjectManager.Instance.LoadModulePlaceholderPrefab();
        if (placeholderPrefab == null)
        {
            Debug.LogError("ModulePlaceholder prefab not found. Skipping empty slot filling.");
            return;
        }

        foreach (ModuleSlot slot in m_moduleSlots)
        {
            // 이미 모듈이 배치된 슬롯은 건너뜀
            if (slot.transform.childCount > 0)
                continue;

            // ModulePlaceholder 생성 및 배치
            GameObject placeholderObj = Instantiate(placeholderPrefab, slot.transform.position, slot.transform.rotation);
            placeholderObj.transform.SetParent(slot.transform);

            // ModulePlaceholder 컴포넌트 추가 및 초기화
            ModulePlaceholder modulePlaceholder = placeholderObj.GetComponent<ModulePlaceholder>();
            if (modulePlaceholder == null)
                modulePlaceholder = placeholderObj.AddComponent<ModulePlaceholder>();

            modulePlaceholder.InitializeModulePlaceholder(this, slot);
        }
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
    public ModuleSlot FindModuleSlot(int moduleTypePacked, int slotIndex)
    {
        EModuleSlotType moduleSlotType = CommonUtility.GetModuleSlotType(moduleTypePacked);

        return m_moduleSlots.FirstOrDefault(slot =>
            CommonUtility.CompareModuleTypeForSlot(slot.m_moduleTypePacked, moduleTypePacked)
            && slot.m_moduleSlotInfo.slotIndex == slotIndex
            && CommonUtility.CanAttachToSlot(moduleSlotType, slot.m_moduleSlotInfo.moduleSlotType));
    }

    // moduleTypePacked와 slotIndex로 특정 모듈 찾기
    public ModuleBase FindModule(int moduleTypePacked, int slotIndex)
    {
        ModuleSlot slot = FindModuleSlot(moduleTypePacked, slotIndex);
        if (slot == null) return null;

        if (slot.transform.childCount > 0)
            return slot.GetComponentInChildren<ModuleBase>();

        return null;
    }

    // 사용 가능한 슬롯 찾기 (빈 슬롯), 자동으로 관리해주는 기능 개발시 쓸 가능성 있음. 그러나 사용자가 직접 moduleSlot 을 골라서 할는 것만 허용될 때는 쓸 일이 없음
    public ModuleSlot FindAvailableSlot(int moduleTypePacked)
    {
        EModuleSlotType moduleSlotType = CommonUtility.GetModuleSlotType(moduleTypePacked);

        return m_moduleSlots.FirstOrDefault(slot =>
            CommonUtility.CompareModuleTypeForSlot(slot.m_moduleTypePacked, moduleTypePacked)
            && slot.transform.childCount == 0
            && CommonUtility.CanAttachToSlot(moduleSlotType, slot.m_moduleSlotInfo.moduleSlotType));
    }

    public void SetTarget(ModuleBody target)
    {
        foreach (ModuleSlot slot in m_moduleSlots)
        {
            if (slot != null && slot.transform.childCount > 0)
            {
                ModuleWeapon weapon = slot.GetComponentInChildren<ModuleWeapon>();
                if (weapon != null && weapon.m_health > 0)
                {
                    weapon.SetTarget(target);
                    continue;
                }

                ModuleHanger hanger = slot.GetComponentInChildren<ModuleHanger>();
                if (hanger != null && hanger.m_health > 0)
                {
                    hanger.SetTarget(target);
                }
            }
        }
    }

    public override void TakeDamage(float damage)
    {
        base.TakeDamage(damage);

        // 바디가 파괴되면 장착된 모든 모듈도 비활성화
        if (m_health <= 0)
        {
            //Debug.Log($"[{GetFleetName()}] ModuleBody[{m_moduleBodyInfo.bodyIndex}] destroyed!");

            // 모든 슬롯의 모듈 비활성화
            foreach (ModuleSlot slot in m_moduleSlots)
            {
                if (slot != null && slot.transform.childCount > 0)
                {
                    ModuleBase module = slot.GetComponentInChildren<ModuleBase>();
                    if (module != null)
                        module.gameObject.SetActive(false);
                }
            }

            // 상위 SpaceShip에 자신의 파괴를 알림
            SpaceShip parentShip = GetComponentInParent<SpaceShip>();
            if (parentShip != null)
                parentShip.CheckForDestruction();
        }
    }

    // 총 엔진 속도 계산 (모든 엔진의 합)
    public float GetTotalEngineSpeed()
    {
        float totalSpeed = 0f;
        foreach (ModuleSlot slot in m_moduleSlots)
        {
            if (slot != null && slot.transform.childCount > 0)
            {
                ModuleEngine engine = slot.GetComponentInChildren<ModuleEngine>();
                if (engine != null && engine.m_health > 0)
                {
                    totalSpeed += engine.GetEngineSpeed();
                }
            }
        }
        return totalSpeed;
    }

    // 총 화물 용량 반환 (Body 자체 용량)
    public float GetTotalCargoCapacity()
    {
        return m_cargoCapacity;
    }

    // Body의 능력치 프로파일 계산
    public override CapabilityProfile GetCapabilityProfile()
    {
        CapabilityProfile stats = new CapabilityProfile();

        if (m_health <= 0) return stats;

        // Body 자체의 능력치
        stats.hp = m_health;
        stats.cargoCapacity = m_cargoCapacity;

        // 모든 슬롯의 모듈들을 순회하며 능력치 합산
        foreach (ModuleSlot slot in m_moduleSlots)
        {
            if (slot != null && slot.transform.childCount > 0)
            {
                ModuleBase module = slot.GetComponentInChildren<ModuleBase>();
                if (module != null && module.m_health > 0)
                {
                    CapabilityProfile moduleStats = module.GetCapabilityProfile();
                    stats.engineSpeed += moduleStats.engineSpeed;
                    stats.attackDps += moduleStats.attackDps;
                    stats.totalWeapons += moduleStats.totalWeapons;
                    stats.totalEngines += moduleStats.totalEngines;
                }
            }
        }

        return stats;
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
        ModuleData currentStats = DataManager.Instance.RestoreModuleData(m_moduleBodyInfo.ModuleSubType, m_moduleBodyInfo.moduleLevel);
        ModuleData upgradeStats = DataManager.Instance.RestoreModuleData(m_moduleBodyInfo.ModuleSubType, m_moduleBodyInfo.moduleLevel + 1);

        if (currentStats == null)
            return "Current module data not found.";

        if (upgradeStats == null)
            return "Upgrade not available (max level reached).";

        string comparison = $"=== UPGRADE COMPARISON ===\n";
        comparison += $"Level: {currentStats.m_moduleLevel} -> {upgradeStats.m_moduleLevel}\n";
        comparison += $"HP: {currentStats.m_health:F0} -> {upgradeStats.m_health:F0}\n";
        comparison += $"Cargo: {currentStats.m_cargoCapacity:F0} -> {upgradeStats.m_cargoCapacity:F0}\n";
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

    // 슬롯의 모듈을 교체
    public bool ReplaceModuleInSlot(int slotIndex, int moduleTypePacked, EModuleType moduleType, int moduleLevel)
    {
        // 슬롯 찾기
        ModuleSlot targetSlot = FindModuleSlot(moduleTypePacked, slotIndex);
        if (targetSlot == null)
        {
            Debug.LogError($"ReplaceModuleInSlot: Cannot find slot - moduleTypePacked: {moduleTypePacked}, slotIndex: {slotIndex}");
            return false;
        }

        // 기존 모듈 제거
        if (targetSlot.transform.childCount > 0)
        {
            ModuleBase existingModule = targetSlot.GetComponentInChildren<ModuleBase>();
            if (existingModule != null)
            {
                // 리스트에서 제거
                if (existingModule is ModuleWeapon weapon)
                    RemoveWeapon(weapon);
                else if (existingModule is ModuleEngine engine)
                    RemoveEngine(engine);
                else if (existingModule is ModuleHanger hanger)
                    RemoveHanger(hanger);

                // 게임 오브젝트 즉시 삭제 (같은 프레임 내에서 새 모듈을 생성하므로 DestroyImmediate 사용)
                DestroyImmediate(existingModule.gameObject);
            }
        }

        // 새 모듈 생성
        return CreateAndPlaceModule(targetSlot, moduleTypePacked, moduleLevel);
    }

    private bool CreateAndPlaceModule(ModuleSlot targetSlot, int moduleTypePacked, int moduleLevel)
    {
        EModuleType moduleType = CommonUtility.GetModuleType(moduleTypePacked);
        EModuleSubType moduleSubType = CommonUtility.GetModuleSubType(moduleTypePacked);
        GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(moduleType.ToString(), moduleSubType.ToString(), moduleLevel);
        if (modulePrefab == null) return false;        
        GameObject moduleObj = Instantiate(modulePrefab, targetSlot.transform.position, targetSlot.transform.rotation);
        moduleObj.transform.SetParent(targetSlot.transform);
        ModuleInfo moduleInfo = new ModuleInfo
        {
            moduleTypePacked = moduleTypePacked,
            moduleLevel = moduleLevel,
            slotIndex = targetSlot.m_moduleSlotInfo.slotIndex
        };

        // 타입별 컴포넌트 추가 및 초기화
        switch (moduleType)
        {
            case EModuleType.Engine:
                ModuleEngine moduleEngine = moduleObj.GetComponent<ModuleEngine>();
                if (moduleEngine == null)
                    moduleEngine = moduleObj.AddComponent<ModuleEngine>();
                moduleEngine.InitializeModuleEngine(moduleInfo, this, targetSlot);
                return true;

            case EModuleType.Weapon:
                ModuleWeapon moduleWeapon = moduleObj.GetComponent<ModuleWeapon>();
                if (moduleWeapon == null)
                    moduleWeapon = moduleObj.AddComponent<ModuleWeapon>();
                moduleWeapon.InitializeModuleWeapon(moduleInfo, this, targetSlot);
                return true;

            case EModuleType.Hanger:
                ModuleHanger moduleHanger = moduleObj.GetComponent<ModuleHanger>();
                if (moduleHanger == null)
                    moduleHanger = moduleObj.AddComponent<ModuleHanger>();
                moduleHanger.InitializeModuleHanger(moduleInfo, this, targetSlot);
                return true;

            default:
                Debug.LogError($"CreateAndPlaceModule: Unsupported module type: {moduleType}");
                Destroy(moduleObj); // 생성한 오브젝트 정리
                return false;
        }
    }

    
}
