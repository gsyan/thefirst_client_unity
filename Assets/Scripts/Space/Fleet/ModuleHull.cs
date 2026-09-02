//------------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class ModuleHull : ModuleBase
{
    [HideInInspector] public ModuleHullInfo m_moduleHullInfo;

    [HideInInspector] public List<ModuleSlot> m_moduleSlots = new List<ModuleSlot>();
    [HideInInspector] public List<ModuleBeam> m_beams = new List<ModuleBeam>();
    [HideInInspector] public List<ModuleMissile> m_missiles = new List<ModuleMissile>();
    [HideInInspector] public List<ModuleHangar> m_hangars = new List<ModuleHangar>();
    [HideInInspector] public ModuleShield m_shield; // 슬롯/3D 배치 없음 — InitializeModuleHull에서 자식으로 동적 생성

    private const float k_hitPenetrationRatio = 0.1f; // 충돌 지점에서 중심부로 파고드는 비율
    [SerializeField] private List<Vector3> m_hitPoints = new List<Vector3>(); // 에디터에서 베이킹, 로컬 좌표

    // 이 함체를 선택하거나 기함으로 볼 때 적용할 카메라 줌 범위 (프리팹에서 설정)
    [SerializeField] public float m_cameraMinZoom = 4f;
    [SerializeField] public float m_cameraMaxZoom = 20f;

    private float m_repair;
    private float m_speed;

    // 보상카드 지속버프(Buff_ShipHealth) 미반영 원본 최대체력 — RefreshRewardCardBuff()가 이 값 기준으로 m_healthMax를 다시 계산
    private float m_baseHealthMax;


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
        return m_moduleHullInfo.moduleType;
    }
    public override EModuleSubType GetModuleSubType()
    {
        return m_moduleHullInfo.moduleSubType;
    }
    public override int GetModuleSlotIndex()
    {
        return m_moduleHullInfo.hullIndex;
    }
    public override int GetModuleLevel()
    {
        return m_moduleHullInfo.moduleLevel;
    }
    public override void SetModuleLevel(int level)
    {
        m_moduleHullInfo.moduleLevel = level;
    }

    public override int GetModuleHullIndex()
    {
        return m_moduleHullInfo.hullIndex;
    }
    public override void SetModuleHullIndex(int hullIndex)
    {
        m_moduleHullInfo.hullIndex = hullIndex;
    }

    // Hull 초기화 (기존 모듈 재사용 가능)
    // statOverride: 성능포인트 프리셋 기반 스폰 시 테이블 값 대신 사용할 계산값(체력/수리력/슬롯별 공격력). null이면 기존처럼 테이블값 그대로 사용
    public void InitializeModuleHull(ModuleHullInfo moduleHullInfo, List<ModuleBase> savedModules, ShipFinalStats? statOverride = null)
    {
        m_moduleHullInfo = moduleHullInfo;
        m_moduleSlot = null;

        // 서버 데이터로부터 완전한 모듈 데이터 복원
        ModuleData moduleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(moduleHullInfo.moduleSubType);
        if (moduleData == null) return;

        // 복원된 데이터로 초기화 — 체력/수리력은 프리셋 계산값 있으면 그걸로 대체, 없으면 기존처럼 테이블값
        m_baseHealthMax = statOverride?.health ?? moduleData.health;
        m_healthMax = m_baseHealthMax;
        m_health = moduleHullInfo.currentHealth > 0f ? Mathf.Min(moduleHullInfo.currentHealth, m_healthMax) : m_healthMax;

        m_attack = 0.0f; // Hull은 직접 공격하지 않음

        // Hull 전용 능력치
        m_repair = statOverride?.repair ?? moduleData.repair;
        m_speed  = moduleData.speed; // 선회력(turnRate) 반영은 후속 작업

        // 함대 정보 자동 설정
        AutoDetectFleetInfo();

        // 보상카드 지속버프(내 함대만 배율 1 이상) 반영 — m_healthMax를 여기서 처음 세팅, m_health도 그만큼 같이 늘려줌
        RefreshRewardCardBuff();

        // Zone 적 함선일 때 체력에 배율 적용 (프리셋 스폰은 배율 1.0 고정이라 실질적으로 no-op)
        if (m_ownerFleet != null && m_ownerFleet.IsZoneEnemy == true)
        {
            m_health    *= m_ownerShip.m_healthMultiplier;
            m_healthMax *= m_ownerShip.m_healthMultiplier;
            m_repair    *= m_ownerShip.m_healthMultiplier;
        }

        CollectAndSortModuleSlots();

        // savedModules가 있으면 먼저 재배치
        if (savedModules != null && savedModules.Count > 0)
            RestoreSavedModules(savedModules);

        // 빈 슬롯에 서버 정보대로 모듈 생성 (savedModules 유무와 관계없이)
        CreateMissingModules(moduleHullInfo, statOverride);

    }

#if UNITY_EDITOR
    [ContextMenu("Bake Hit Points")]
    private void BakeHitPoints()
    {
        m_hitPoints.Clear();

        Transform pointsRoot = transform.Find("Points");
        if (pointsRoot == null)
        {
            Debug.LogError($"[{name}] Points 자식을 찾을 수 없습니다.");
            return;
        }

        Bounds bounds = CommonUtility.CalculateRendererBounds(transform, excludeParticles: true, excludeTrails: true, excludeDisabled: false);
        Vector3 center = bounds.center;

        MeshCollider meshCollider = GetComponentInChildren<MeshCollider>();
        if (meshCollider == null)
        {
            Debug.LogError($"[{name}] MeshCollider를 찾을 수 없습니다.");
            return;
        }

        int raycastHits = 0;
        foreach (Transform point in pointsRoot)
        {
            Vector3 origin = point.position;
            Vector3 dir = (center - origin).normalized;
            float maxDist = Vector3.Distance(origin, center);

            Vector3 worldHitPoint;
            Ray ray = new Ray(origin, dir);
            if (meshCollider.Raycast(ray, out RaycastHit hit, maxDist))
            {
                // 충돌 지점에서 중심까지 남은 거리의 비율로 depth 계산 → 부위마다 균일
                float distHitToCenter = Vector3.Distance(hit.point, center);
                float depth = distHitToCenter * k_hitPenetrationRatio;
                worldHitPoint = hit.point + dir * depth;
                raycastHits++;
            }
            else
            {
                // 레이 미스 시 Point에서 중심 방향으로 maxDist * ratio 만큼 들어간 지점으로 폴백
                float depth = maxDist * k_hitPenetrationRatio;
                worldHitPoint = origin + dir * depth;
            }

            m_hitPoints.Add(transform.InverseTransformPoint(worldHitPoint));
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[{name}] HitPoints 베이킹 완료: {m_hitPoints.Count}개 (레이캐스트 성공: {raycastHits}개)");
    }

    [HideInInspector] public bool bShowHitPointGizmos = true;
    private void OnDrawGizmos()
    {
        if (bShowHitPointGizmos == false) return;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
        foreach (Vector3 local in m_hitPoints)
        {
            Vector3 world = transform.TransformPoint(local);
            Gizmos.DrawSphere(world, 0.04f);
        }
    }
#endif

    public Vector3 GetRandomHitPoint()
    {
        if (m_hitPoints.Count == 0) return transform.position;
        int idx = UnityEngine.Random.Range(0, m_hitPoints.Count);
        return transform.TransformPoint(m_hitPoints[idx]);
    }

    // 슬롯에 실제 모듈(Beam/Missile/Hangar)이 배치된 경우만 true
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

                // 새 body로 부모 참조 갱신 및 이 body의 무기/함재기 리스트에 재등록
                // (RemoveMissile/RemoveHangar 등이 옛 body가 아닌 새 body를 대상으로 동작하도록)
                if (module is ModuleBeam beam)
                {
                    beam.SetParentBody(this);
                    AddBeam(beam);
                }
                else if (module is ModuleMissile missile)
                {
                    missile.SetParentBody(this);
                    AddMissile(missile);
                }
                else if (module is ModuleHangar hangar)
                {
                    hangar.SetParentBody(this);
                    AddHangar(hangar);
                }

                // 코루틴 재시작 (각 모듈에서 필요시 override)
                module.RestartCoroutines();

            }
            else
            {
                // 새 body가 지원하지 않는 슬롯 (다운그레이드 시 정상 경로)
                Destroy(module.gameObject);
            }
        }
    }
    
    // 서버 정보에 있지만 재배치되지 못한 모듈들을 생성
    // statOverride: 성능포인트 프리셋 기반 스폰 시 슬롯별(beamInfo.slotIndex 기준) 공격력 계산값 — null이면 기존처럼 테이블값 사용
    private void CreateMissingModules(ModuleHullInfo bodyInfo, ShipFinalStats? statOverride = null)
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
                    float? attackOverride = GetSlotAttackOverride(statOverride?.beamAttacks, beamInfo.slotIndex);
                    InitializeBeam(beamInfo, attackOverride);
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
                    float? attackOverride = GetSlotAttackOverride(statOverride?.missileAttacks, missileInfo.slotIndex);
                    InitializeMissile(missileInfo, attackOverride);
                }
            }
        }

        // 행거 생성
        if (bodyInfo.hangars != null)
        {
            foreach (var hangarInfo in bodyInfo.hangars)
            {
                ModuleSlot slot = FindModuleSlot(hangarInfo.moduleType, hangarInfo.slotIndex);
                if (slot != null && !HasRealModule(slot))
                {
                    DisablePlaceholderIfExists(slot);
                    InitializeHangar(hangarInfo);
                }
            }
        }

        // 실제 모듈이 없는 슬롯의 Placeholder 초기화
        FillEmptySlotsWithPlaceholders();

        // 실드 — 슬롯/3D 배치 없이 논리적으로만 존재하는 자식 컴포넌트
        InitializeShield(bodyInfo.shieldModuleSubType);
    }

    private void InitializeShield(string shieldSubTypeName)
    {
        EModuleSubType shieldSubType = System.Enum.TryParse(shieldSubTypeName, out EModuleSubType parsed) ? parsed : EModuleSubType.none;

        if (m_shield == null)
        {
            GameObject shieldObj = new GameObject("ModuleShield");
            shieldObj.transform.SetParent(transform, false);
            m_shield = shieldObj.AddComponent<ModuleShield>();
        }

        m_shield.SetFleetInfo(m_ownerFleet, m_ownerShip);
        m_shield.InitializeModuleShield(shieldSubType);
    }

    // 프리셋 계산값 배열에서 slotIndex에 해당하는 값을 안전하게 조회 (범위 밖/null이면 override 없음)
    private float? GetSlotAttackOverride(float[] attackArray, int slotIndex)
    {
        if (attackArray == null || slotIndex < 0 || slotIndex >= attackArray.Length) return null;
        return attackArray[slotIndex];
    }

    private void InitializeBeam(ModuleInfo moduleInfo, float? attackOverride = null)
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

        moduleBeam.InitializeModuleBeam(moduleInfo, this, targetSlot, attackOverride);
    }

    private void InitializeMissile(ModuleInfo moduleInfo, float? attackOverride = null)
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

        moduleMissile.InitializeModuleMissile(moduleInfo, this, targetSlot, attackOverride);
    }

    private void InitializeHangar(ModuleInfo moduleInfo)
    {
        GameObject modulePrefab = ObjectManager.Instance.LoadShipModulePrefab(moduleInfo.moduleType.ToString(), moduleInfo.moduleSubType.ToString());
        if (modulePrefab == null)
        {
            Debug.LogWarning($"InitializeHangar: Cannot find module prefab - Level: {moduleInfo.moduleLevel}");
            return;
        }

        ModuleSlot targetSlot = FindModuleSlot(moduleInfo.moduleType, moduleInfo.slotIndex);
        if (targetSlot == null)
        {
            Debug.LogWarning($"InitializeHangar: Cannot find hangar slot {moduleInfo.slotIndex}");
            return;
        }

        if (HasRealModule(targetSlot))
        {
            Debug.LogWarning($"InitializeHangar: Hangar slot {moduleInfo.slotIndex} is already occupied");
            return;
        }

        GameObject hangarObj = Instantiate(modulePrefab, targetSlot.transform.position, targetSlot.transform.rotation);
        hangarObj.transform.SetParent(targetSlot.transform);
        hangarObj.transform.localScale = Vector3.one;

        ModuleHangar moduleHangar = hangarObj.GetComponent<ModuleHangar>();
        if (moduleHangar == null)
            moduleHangar = hangarObj.AddComponent<ModuleHangar>();

        moduleHangar.InitializeModuleHangar(moduleInfo, this, targetSlot);
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
        if (m_moduleHullInfo.beams.Contains(beam.m_moduleInfo) == false)
            m_moduleHullInfo.beams.Add(beam.m_moduleInfo);

        if (m_beams.Contains(beam) == false)
            m_beams.Add(beam);
    }

    // Missile 추가
    public void AddMissile(ModuleMissile missile)
    {
        if (m_moduleHullInfo.missiles.Contains(missile.m_moduleInfo) == false)
            m_moduleHullInfo.missiles.Add(missile.m_moduleInfo);

        if (m_missiles.Contains(missile) == false)
            m_missiles.Add(missile);
    }

    // 행거 추가
    public void AddHangar(ModuleHangar hangar)
    {
        if (m_moduleHullInfo.hangars.Contains(hangar.m_moduleInfo) == false)
            m_moduleHullInfo.hangars.Add(hangar.m_moduleInfo);

        if (!m_hangars.Contains(hangar))
            m_hangars.Add(hangar);
    }

    // 무기 제거
    public void RemoveBeam(ModuleBeam beam, bool bRemoveFromInfo = false)
    {
        if( bRemoveFromInfo == true)
            m_moduleHullInfo.beams.Remove(beam.m_moduleInfo);
        m_beams.Remove(beam);
    }

    public void RemoveMissile(ModuleMissile missile, bool bRemoveFromInfo = false)
    {
        if( bRemoveFromInfo == true)
            m_moduleHullInfo.missiles.Remove(missile.m_moduleInfo);
        m_missiles.Remove(missile);
    }

    // 행거 제거
    public void RemoveHangar(ModuleHangar hangar, bool bRemoveFromInfo = false)
    {
        if( bRemoveFromInfo == true)
            m_moduleHullInfo.hangars.Remove(hangar.m_moduleInfo);
        m_hangars.Remove(hangar);
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
                moduleType    = s.moduleType,
                moduleSubType = s.moduleSubType,
                moduleLevel   = s.moduleLevel,
                hullIndex     = s.hullIndex,
                slotIndex     = s.slotIndex,
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

    public void SetTarget(ModuleHull target)
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

                ModuleHangar hangar = slot.GetComponentInChildren<ModuleHangar>();
                if (hangar != null)
                {
                    hangar.SetTarget(target);
                    continue;
                }
            }
        }
    }

    public override void TakeDamage(float damage)
    {
        bool wasAlive = m_health > 0;
        base.TakeDamage(damage);

        // 살아있다가 죽는 시점에만 발동 — 이미 0인 상태에서 중복 발동 방지
        if (wasAlive == true && m_health <= 0)
        {
            EventManager.Trigger_ModuleHullDestroyed(this);

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
        if (bByInfo == true) return CommonUtility.GetBodyCapabilityProfile(m_moduleHullInfo);

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
                    stats.beamAttack += moduleStats.beamAttack;
                    stats.missileAttack += moduleStats.missileAttack;
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

    // 보상카드 지속버프 배율이 바뀔 때마다(카드 선택/런 종료 초기화) 호출 — 이 바디의 체력과 하위 무기 모듈들을 모두 갱신
    public void RefreshRewardCardBuff()
    {
        float oldHealthMax = m_healthMax;
        m_healthMax = m_baseHealthMax * GetRewardCardBuffMultiplier(ECardEffectType.Buff_ShipHealth);
        // 최대체력이 늘어난 만큼 현재체력도 같이 늘려줌(그 자리에서 즉시 회복은 아님) — 줄어드는 경우는 없음(버프는 항상 증가)
        float healthMaxDelta = m_healthMax - oldHealthMax;
        if (healthMaxDelta != 0f)
            m_health = Mathf.Clamp(m_health + healthMaxDelta, 0f, m_healthMax);

        foreach (ModuleBeam beam in m_beams)
            if (beam != null) beam.RefreshRewardCardBuff();
        foreach (ModuleMissile missile in m_missiles)
            if (missile != null) missile.RefreshRewardCardBuff();
    }

}
