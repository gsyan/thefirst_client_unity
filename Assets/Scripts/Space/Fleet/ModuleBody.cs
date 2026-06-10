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
    [HideInInspector] public List<ModuleBeam> m_beams = new List<ModuleBeam>();
    [HideInInspector] public List<ModuleMissile> m_missiles = new List<ModuleMissile>();
    [HideInInspector] public List<ModuleHanger> m_hangers = new List<ModuleHanger>();

    // 이 함체를 선택하거나 기함으로 볼 때 적용할 카메라 줌 범위 (프리팹에서 설정)
    [SerializeField] public float m_cameraMinZoom = 4f;
    [SerializeField] public float m_cameraMaxZoom = 20f;

    private float m_repair;
    private float m_speed;

    
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
        return m_moduleBodyInfo.moduleType;
    }
    public override EModuleSubType GetModuleSubType()
    {
        return m_moduleBodyInfo.moduleSubType;
    }
    public override int GetModuleSlotIndex()
    {
        return m_moduleBodyInfo.bodyIndex;
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
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(m_moduleBodyInfo.moduleSubType, newLevel);
        if (moduleData == null) return;
        
        // 스탯 갱신
        m_healthMax = moduleData.health;
        m_health = Mathf.Min(m_health, m_healthMax);
        m_repair = moduleData.repair;
        m_speed  = moduleData.speed;
        m_modulePointCostLevelup = moduleData.modulePointCost;
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
        SetInvestedModulePoint(moduleBodyInfo.investedModulePoint);

        // 서버 데이터로부터 완전한 모듈 데이터 복원
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(moduleBodyInfo.moduleSubType, moduleBodyInfo.moduleLevel);
        if (moduleData == null) return;

        // 복원된 데이터로 초기화
        m_healthMax = moduleData.health;
        m_health = moduleBodyInfo.currentHealth > 0f ? Mathf.Min(moduleBodyInfo.currentHealth, m_healthMax) : 1f;
        // 업그레이드 비용 설정
        m_modulePointCostLevelup = moduleData.modulePointCost;
        
        m_attack = 0.0f; // Body는 직접 공격하지 않음

        // Body 전용 능력치
        m_repair = moduleData.repair;
        m_speed  = moduleData.speed;

        // 함대 정보 자동 설정
        AutoDetectFleetInfo();

        // Zone 적 함선일 때 체력에 배율 적용
        if (m_ownerFleet != null && m_ownerFleet.IsZoneEnemy == true)
        {
            m_health    *= m_myShip.m_bodyMultiplier;
            m_healthMax *= m_myShip.m_bodyMultiplier;
            m_repair    *= m_myShip.m_bodyMultiplier;
        }

        CollectAndSortModuleSlots();

        // savedModules가 있으면 먼저 재배치
        if (savedModules != null && savedModules.Count > 0)
            RestoreSavedModules(savedModules);

        // 빈 슬롯에 서버 정보대로 모듈 생성 (savedModules 유무와 관계없이)
        CreateMissingModules(moduleBodyInfo);
    }

    // 슬롯에 실제 모듈(Beam/Missile/Hanger)이 배치된 경우만 true
    private bool HasRealModule(ModuleSlot slot)
    {
        foreach (Transform child in slot.transform)
        {
            ModuleBase module = child.GetComponent<ModuleBase>();
            if (module != null && module is ModulePlaceholder == false)
                return true;
        }
        return false;
    }

    // 슬롯의 Placeholder를 비활성화
    private void DisablePlaceholderIfExists(ModuleSlot slot)
    {
        ModulePlaceholder placeholder = slot.GetComponentInChildren<ModulePlaceholder>(true);
        if (placeholder != null)
            placeholder.gameObject.SetActive(false);
    }

    // 저장된 모듈을 슬롯에 재배치
    private void RestoreSavedModules(List<ModuleBase> savedModules)
    {
        SpaceShip myShip = GetSpaceShip();
        SpaceFleet myFleet = myShip != null ? myShip.m_ownerFleet : null;

        foreach (var module in savedModules)
        {
            EModuleType moduleType = module.GetModuleType();
            int oldSlotIndex = module.GetModuleSlotIndex();

            // 새 body에서 같은 타입과 인덱스의 슬롯 찾기
            ModuleSlot targetSlot = FindModuleSlot(moduleType, oldSlotIndex);

            if (targetSlot != null && !HasRealModule(targetSlot))
            {
                // 기존 placeholder 비활성화 후 모듈 배치
                DisablePlaceholderIfExists(targetSlot);
                module.transform.SetParent(targetSlot.transform);
                module.transform.localPosition = Vector3.zero;
                module.transform.localRotation = Quaternion.identity;
                module.transform.localScale = Vector3.one;
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
                Debug.LogWarning($"Cannot find compatible slot for {module.GetType().Name} (type={moduleType}, slot={oldSlotIndex}). Module destroyed.");
                Destroy(module.gameObject);
            }
        }
    }
    
    // 서버 정보에 있지만 재배치되지 못한 모듈들을 생성
    private void CreateMissingModules(ModuleBodyInfo bodyInfo)
    {
        // Beam 생성
        if (bodyInfo.beams != null)
        {
            foreach (var beamInfo in bodyInfo.beams)
            {
                ModuleSlot slot = FindModuleSlot(beamInfo.moduleType, beamInfo.slotIndex);
                if (slot != null && !HasRealModule(slot))
                {
                    DisablePlaceholderIfExists(slot);
                    InitializeBeam(beamInfo);
                }
            }
        }

        // Missile 생성
        if (bodyInfo.missiles != null)
        {
            foreach (var missileInfo in bodyInfo.missiles)
            {
                ModuleSlot slot = FindModuleSlot(missileInfo.moduleType, missileInfo.slotIndex);
                if (slot != null && !HasRealModule(slot))
                {
                    DisablePlaceholderIfExists(slot);
                    InitializeMissile(missileInfo);
                }
            }
        }

        // 행거 생성
        if (bodyInfo.hangers != null)
        {
            foreach (var hangerInfo in bodyInfo.hangers)
            {
                ModuleSlot slot = FindModuleSlot(hangerInfo.moduleType, hangerInfo.slotIndex);
                if (slot != null && !HasRealModule(slot))
                {
                    DisablePlaceholderIfExists(slot);
                    InitializeHanger(hangerInfo);
                }
            }
        }

        // 실제 모듈이 없는 슬롯의 Placeholder 초기화
        FillEmptySlotsWithPlaceholders();
    }

    private void InitializeBeam(ModuleInfo moduleInfo)
    {
        GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(moduleInfo.moduleType.ToString(), moduleInfo.moduleSubType.ToString());
        if (modulePrefab == null) return;
        ModuleSlot targetSlot = FindModuleSlot(moduleInfo.moduleType, moduleInfo.slotIndex);
        if (targetSlot == null) return;
        if (HasRealModule(targetSlot)) return;
        
        GameObject beamObj = Instantiate(modulePrefab, targetSlot.transform.position, targetSlot.transform.rotation);
        beamObj.transform.SetParent(targetSlot.transform);
        beamObj.transform.localScale = Vector3.one;

        ModuleBeam moduleBeam = beamObj.GetComponent<ModuleBeam>();
        if (moduleBeam == null)
            moduleBeam = beamObj.AddComponent<ModuleBeam>();

        moduleBeam.InitializeModuleBeam(moduleInfo, this, targetSlot);
    }

    private void InitializeMissile(ModuleInfo moduleInfo)
    {
        GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(moduleInfo.moduleType.ToString(), moduleInfo.moduleSubType.ToString());
        if (modulePrefab == null) return;
        ModuleSlot targetSlot = FindModuleSlot(moduleInfo.moduleType, moduleInfo.slotIndex);
        if (targetSlot == null) return;
        if (HasRealModule(targetSlot)) return;
        
        GameObject missileObj = Instantiate(modulePrefab, targetSlot.transform.position, targetSlot.transform.rotation);
        missileObj.transform.SetParent(targetSlot.transform);
        missileObj.transform.localScale = Vector3.one;

        ModuleMissile moduleMissile = missileObj.GetComponent<ModuleMissile>();
        if (moduleMissile == null)
            moduleMissile = missileObj.AddComponent<ModuleMissile>();

        moduleMissile.InitializeModuleMissile(moduleInfo, this, targetSlot);
    }

    private void InitializeHanger(ModuleInfo moduleInfo)
    {
        GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(moduleInfo.moduleType.ToString(), moduleInfo.moduleSubType.ToString());
        if (modulePrefab == null)
        {
            Debug.LogWarning($"InitializeHanger: Cannot find module prefab - Level: {moduleInfo.moduleLevel}");
            return;
        }

        ModuleSlot targetSlot = FindModuleSlot(moduleInfo.moduleType, moduleInfo.slotIndex);
        if (targetSlot == null)
        {
            Debug.LogWarning($"InitializeHanger: Cannot find hanger slot {moduleInfo.slotIndex}");
            return;
        }

        if (HasRealModule(targetSlot))
        {
            Debug.LogWarning($"InitializeHanger: Hanger slot {moduleInfo.slotIndex} is already occupied");
            return;
        }

        GameObject hangerObj = Instantiate(modulePrefab, targetSlot.transform.position, targetSlot.transform.rotation);
        hangerObj.transform.SetParent(targetSlot.transform);
        hangerObj.transform.localScale = Vector3.one;

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
            int typeComparison = slot1.m_moduleSlotInfo.moduleType.CompareTo(slot2.m_moduleSlotInfo.moduleType);
            if (typeComparison != 0)
                return typeComparison;
            return slot1.m_moduleSlotInfo.slotIndex.CompareTo(slot2.m_moduleSlotInfo.slotIndex);
        });
    }

    private void FillEmptySlotsWithPlaceholders()
    {
        foreach (ModuleSlot slot in m_moduleSlots)
        {
            // 실제 모듈이 이미 있는 슬롯은 건너뜀
            if (HasRealModule(slot))
                continue;

            // 프리팹에 내장된 Placeholder를 찾아 초기화
            ModulePlaceholder modulePlaceholder = slot.GetComponentInChildren<ModulePlaceholder>(true);
            if (modulePlaceholder == null)
            {
                Debug.LogWarning($"FillEmptySlotsWithPlaceholders: No placeholder in slot {slot.m_moduleSlotInfo.slotIndex}");
                continue;
            }

            modulePlaceholder.gameObject.SetActive(true);
            modulePlaceholder.InitializeModulePlaceholder(this, slot);
        }
    }

    // Beam 추가
    public void AddBeam(ModuleBeam beam)
    {
        if (m_moduleBodyInfo.beams.Contains(beam.m_moduleInfo) == false)
            m_moduleBodyInfo.beams.Add(beam.m_moduleInfo);

        if (m_beams.Contains(beam) == false)
            m_beams.Add(beam);
    }

    // Missile 추가
    public void AddMissile(ModuleMissile missile)
    {
        if (m_moduleBodyInfo.missiles.Contains(missile.m_moduleInfo) == false)
            m_moduleBodyInfo.missiles.Add(missile.m_moduleInfo);

        if (m_missiles.Contains(missile) == false)
            m_missiles.Add(missile);
    }

    // 행거 추가
    public void AddHanger(ModuleHanger hanger)
    {
        if (m_moduleBodyInfo.hangers.Contains(hanger.m_moduleInfo) == false)
            m_moduleBodyInfo.hangers.Add(hanger.m_moduleInfo);

        if (!m_hangers.Contains(hanger))
            m_hangers.Add(hanger);
    }

    // 무기 제거
    public void RemoveBeam(ModuleBeam beam, bool bRemoveFromInfo = false)
    {
        if( bRemoveFromInfo == true)
            m_moduleBodyInfo.beams.Remove(beam.m_moduleInfo);
        m_beams.Remove(beam);
    }

    public void RemoveMissile(ModuleMissile missile, bool bRemoveFromInfo = false)
    {
        if( bRemoveFromInfo == true)
            m_moduleBodyInfo.missiles.Remove(missile.m_moduleInfo);
        m_missiles.Remove(missile);
    }

    // 행거 제거
    public void RemoveHanger(ModuleHanger hanger, bool bRemoveFromInfo = false)
    {
        if( bRemoveFromInfo == true)
            m_moduleBodyInfo.hangers.Remove(hanger.m_moduleInfo);
        m_hangers.Remove(hanger);
    }

    private List<ModuleInfo> CopyModuleInfoList(List<ModuleInfo> source)
    {
        if (source == null) return new List<ModuleInfo>();
        var copy = new List<ModuleInfo>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            var s = source[i];
            copy.Add(new ModuleInfo
            {
                moduleType = s.moduleType,
                moduleSubType = s.moduleSubType,
                moduleLevel = s.moduleLevel,
                bodyIndex = s.bodyIndex,
                slotIndex = s.slotIndex
            });
        }
        return copy;
    }

    // 특정 타입과 인덱스의 슬롯 찾기
    public ModuleSlot FindModuleSlot(EModuleType moduleType, int slotIndex)
    {
        return m_moduleSlots.FirstOrDefault(slot => 
            slot.m_moduleSlotInfo.moduleType == moduleType
            && slot.m_moduleSlotInfo.slotIndex == slotIndex
            );
    }

    // moduleTypePacked와 slotIndex로 특정 모듈 찾기
    public ModuleBase FindModule(EModuleType moduleType, int slotIndex)
    {
        ModuleSlot slot = FindModuleSlot(moduleType, slotIndex);
        if (slot == null) return null;

        if (slot.transform.childCount > 0)
            return slot.GetComponentInChildren<ModuleBase>();

        return null;
    }

    public void SetTarget(ModuleBody target)
    {
        foreach (ModuleSlot slot in m_moduleSlots)
        {
            if (slot != null && slot.transform.childCount > 0)
            {
                ModuleBeam beam = slot.GetComponentInChildren<ModuleBeam>();
                if (beam != null)
                {
                    beam.SetTarget(target);
                    continue;
                }

                ModuleMissile missile = slot.GetComponentInChildren<ModuleMissile>();
                if (missile != null)
                {
                    missile.SetTarget(target);
                    continue;
                }

                ModuleHanger hanger = slot.GetComponentInChildren<ModuleHanger>();
                if (hanger != null)
                {
                    hanger.SetTarget(target);
                    continue;
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
        }
    }

    // Body의 능력치 프로파일 계산
    public override CapabilityProfile GetModuleCapabilityProfile(bool bByInfo)
    {
        if (bByInfo == true) return CommonUtility.GetBodyCapabilityProfile(m_moduleBodyInfo);

        CapabilityProfile stats = new CapabilityProfile();
        if (m_health <= 0) return stats;

        // Body 자체의 능력치
        stats.health = m_health;
        stats.repair = m_repair;
        stats.speed  = m_speed;

        // 모든 슬롯의 모듈들을 순회하며 능력치 합산
        foreach (ModuleSlot slot in m_moduleSlots)
        {
            if (slot != null && slot.transform.childCount > 0)
            {
                ModuleBase module = slot.GetComponentInChildren<ModuleBase>();
                if (module != null)
                {
                    CapabilityProfile moduleStats = module.GetModuleCapabilityProfile(false);
                    stats.totalWeapons += moduleStats.totalWeapons;
                    stats.attack += moduleStats.attack;
                    stats.health += moduleStats.health;
                    stats.speed += moduleStats.speed;
                    stats.airAttack += moduleStats.airAttack;
                    stats.airCount += moduleStats.airCount;
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

    // 슬롯의 모듈을 플레이스홀더로 복귀 (언락 리셋용)
    public bool ResetModuleToPlaceholder(EModuleType moduleType, int slotIndex)
    {
        ModuleSlot targetSlot = FindModuleSlot(moduleType, slotIndex);
        if (targetSlot == null)
        {
            Debug.LogError($"ResetModuleToPlaceholder: Cannot find slot - moduleType: {moduleType}, slotIndex: {slotIndex}");
            return false;
        }

        ModuleBase existingModule = targetSlot.GetComponentInChildren<ModuleBase>();
        if (existingModule != null && (existingModule is ModulePlaceholder) == false)
        {
            EventManager.TriggerModuleReplaced(existingModule, null);
            if (existingModule is ModuleBeam beam)             RemoveBeam(beam, bRemoveFromInfo: true);
            else if (existingModule is ModuleMissile missile)  RemoveMissile(missile, bRemoveFromInfo: true);
            else if (existingModule is ModuleHanger hanger)    RemoveHanger(hanger, bRemoveFromInfo: true);
            DestroyImmediate(existingModule.gameObject);
            // placeholder는 파괴하지 않았으므로 FillEmptySlotsWithPlaceholders가 재활성화함
        }

        FillEmptySlotsWithPlaceholders();
        return true;
    }

    // 슬롯의 모듈을 교체
    public bool ReplaceModuleInSlot(EModuleType moduleType, EModuleSubType moduleSubType, int moduleLevel, int slotIndex)
    {
        // 슬롯 찾기
        ModuleSlot targetSlot = FindModuleSlot(moduleType, slotIndex);
        if (targetSlot == null)
        {
            Debug.LogError($"ReplaceModuleInSlot: Cannot find slot - moduleType: {moduleType}, slotIndex: {slotIndex}");
            return false;
        }

        // 기존 모듈 제거
        if (targetSlot.transform.childCount > 0)
        {
            ModuleBase existingModule = targetSlot.GetComponentInChildren<ModuleBase>();
            if (existingModule != null)
            {
                EventManager.TriggerModuleReplaced(existingModule, null);

                if (existingModule is ModulePlaceholder)
                {
                    // placeholder는 리셋 시 재사용을 위해 비활성화만 함
                    existingModule.gameObject.SetActive(false);
                }
                else
                {
                    if (existingModule is ModuleBeam beam)
                        RemoveBeam(beam, bRemoveFromInfo: true);
                    else if (existingModule is ModuleMissile missile)
                        RemoveMissile(missile, bRemoveFromInfo: true);
                    else if (existingModule is ModuleHanger hanger)
                        RemoveHanger(hanger, bRemoveFromInfo: true);

                    DestroyImmediate(existingModule.gameObject);
                }
            }
        }

        ModuleBase newModule = CreateAndPlaceModule(targetSlot, moduleType, moduleSubType, moduleLevel);

        // 전투 중 추가 시 즉시 전함 상태 적용 (뚜껑 열림 등 애니메이터 반영)
        if (newModule != null)
            newModule.ApplyShipStateToModule();

        // 새 모듈 생성 이벤트 발행
        if (newModule != null)
            EventManager.TriggerModuleReplaced(null, newModule);

        return newModule != null;
    }

    private ModuleBase CreateAndPlaceModule(ModuleSlot targetSlot, EModuleType moduleType, EModuleSubType moduleSubType, int moduleLevel)
    {
        GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(moduleType.ToString(), moduleSubType.ToString());
        if (modulePrefab == null) return null;
        GameObject moduleObj = Instantiate(modulePrefab, targetSlot.transform.position, targetSlot.transform.rotation);
        moduleObj.transform.SetParent(targetSlot.transform);
        moduleObj.transform.localScale = Vector3.one;
        ModuleInfo moduleInfo = new ModuleInfo
        {
            moduleType = moduleType,
            moduleSubType = moduleSubType,
            moduleLevel = moduleLevel,
            slotIndex = targetSlot.m_moduleSlotInfo.slotIndex
        };

        // 타입별 컴포넌트 추가 및 초기화
        switch (moduleType)
        {
            case EModuleType.beam:
                ModuleBeam moduleBeam = moduleObj.GetComponent<ModuleBeam>();
                if (moduleBeam == null)
                    moduleBeam = moduleObj.AddComponent<ModuleBeam>();
                moduleBeam.InitializeModuleBeam(moduleInfo, this, targetSlot);
                return moduleBeam;

            case EModuleType.missile:
                ModuleMissile moduleMissile = moduleObj.GetComponent<ModuleMissile>();
                if (moduleMissile == null)
                    moduleMissile = moduleObj.AddComponent<ModuleMissile>();
                moduleMissile.InitializeModuleMissile(moduleInfo, this, targetSlot);
                return moduleMissile;

            case EModuleType.hanger:
                ModuleHanger moduleHanger = moduleObj.GetComponent<ModuleHanger>();
                if (moduleHanger == null)
                    moduleHanger = moduleObj.AddComponent<ModuleHanger>();
                moduleHanger.InitializeModuleHanger(moduleInfo, this, targetSlot);
                return moduleHanger;

            default:
                Debug.LogError($"CreateAndPlaceModule: Unsupported module type: {moduleType}");
                Destroy(moduleObj); // 생성한 오브젝트 정리
                return null;
        }
    }

}
