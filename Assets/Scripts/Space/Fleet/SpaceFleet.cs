//------------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum EFleetSide
{
    fleet_side_player,
    fleet_side_enemy
}

public enum EFleetSource
{
    fleet_source_player,
    fleet_source_player_remote,
    fleet_source_zone_data,
}

public class SpaceFleet : MonoBehaviour
{
    public FleetInfo m_fleetInfo;
    public EFleetSide m_fleetSide = EFleetSide.fleet_side_player;
    public EFleetSource m_fleetSource = EFleetSource.fleet_source_player;
    public EUnitState m_fleetState = EUnitState.Idle;

    // 편의 프로퍼티
    public bool IsEnemy => m_fleetSide == EFleetSide.fleet_side_enemy;
    public bool IsZoneEnemy => m_fleetSource == EFleetSource.fleet_source_zone_data;
    public bool IsPvpEnemy => m_fleetSource == EFleetSource.fleet_source_player_remote;
    public EFormationType m_currentFormationType = EFormationType.linear_horizontal;
    [SerializeField] public List<SpaceShip> m_ships = new List<SpaceShip>();

    // Zone 스폰 상태 (IsZoneEnemy 전용)
    public Queue<ShipInfo> m_shipSpawnQueue;
    private Coroutine m_spawnCoroutine;
    private Coroutine m_warpInCoroutine;
    
    // 자연 수리 임계값 저장: 10%~100% 돌파 시 서버 저장 (Idle 한정)
    private int m_lastSavedHealthTier = 0;
    private static readonly float[] k_healthSaveTiers = { 0.10f, 0.20f, 0.30f, 0.40f, 0.50f, 0.60f, 0.70f, 0.80f, 0.90f, 1.00f };

    private int GetHealthTier(float ratio)
    {
        for (int i = k_healthSaveTiers.Length - 1; i >= 0; i--)
        {
            if (ratio >= k_healthSaveTiers[i]) return i + 1;
        }
        return 0;
    }

    private float GetFleetHealthRatio()
    {
        float total = 0f, max = 0f;
        foreach (SpaceShip ship in m_ships)
        {
            if (ship == null) continue;
            foreach (ModuleBody body in ship.m_moduleBodys)
            {
                if (body == null) continue;
                total += body.m_health;
                max   += body.m_healthMax;
            }
        }
        return max > 0f ? total / max : 1f;
    }

    private void OnFleetHPUpdatedForSave()
    {
        if (m_fleetSource != EFleetSource.fleet_source_player) return;
        if (m_fleetState != EUnitState.Idle) return;

        float ratio = GetFleetHealthRatio();
        int tier = GetHealthTier(ratio);
        if (tier > m_lastSavedHealthTier)
        {
            m_lastSavedHealthTier = tier;
            SaveHealthToServer();
        }
    }

    private void Start()
    {
        EventManager.Subscribe_ShipBodyChanged(OnShipBodyChanged);
        EventManager.Subscribe_FleetUpdateHP(OnFleetHPUpdatedForSave);
    }

    // fleet 오브젝트를 현재 위치 뒤에서 targetPos까지 워프 이펙트로 진입, 도착 시 콜백
    public void StartFleetWarpIn(System.Action onArrived = null)
    {
        CancelFleetWarpIn();
        SpaceShip flagship = GetFlagship();
        if (flagship == null)
        {
            if (onArrived != null)
                onArrived.Invoke();
            return;
        }

        float offsetDist = 40.0f + flagship.CalculateShipBounds().size.z * 0.5f;
        Vector3 finalPos  = transform.position;
        transform.position = finalPos - transform.forward * offsetDist;

        foreach (SpaceShip ship in m_ships)
        {
            if (ship == null) continue;
            if (ship.TryGetComponent(out WarpEffectShip warpEffect) == false)
            {
                warpEffect = ship.gameObject.AddComponent<WarpEffectShip>();
                warpEffect.InitializeWarpEffect();
            }
            warpEffect.StartFleetWarpIn();
        }

        float warpSpeed = flagship.m_spaceShipStatsCur.speed * m_spawnApproachSpeedMult;
        float normalSpeed = flagship.m_spaceShipStatsCur.speed;
        m_warpInCoroutine = StartCoroutine(FleetWarpInMove(finalPos, warpSpeed, normalSpeed, onArrived));
    }

    public void CancelFleetWarpIn()
    {
        if (m_warpInCoroutine != null)
        {
            StopCoroutine(m_warpInCoroutine);
            m_warpInCoroutine = null;
        }
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null && ship.TryGetComponent(out WarpEffectShip warpEffect))
                warpEffect.StopWarp();
        }
    }

    private const float WARP_STOP_DIST = 2f;

    private IEnumerator FleetWarpInMove(Vector3 finalPos, float warpSpeed, float normalSpeed, System.Action onArrived)
    {
        bool warpStopped = false;

        while (true)
        {
            Vector3 toTarget = finalPos - transform.position;
            float dotForward = Vector3.Dot(transform.forward, toTarget);
            float speed = warpStopped == false ? warpSpeed : normalSpeed;
            float moveDist = speed * Time.deltaTime;

            if (warpStopped == false && (dotForward <= WARP_STOP_DIST || dotForward <= moveDist))
            {
                foreach (SpaceShip ship in m_ships)
                {
                    if (ship != null && ship.TryGetComponent(out WarpEffectShip warpEffect))
                        warpEffect.StopWarp();
                }
                warpStopped = true;
            }

            if (dotForward <= moveDist)
            {
                transform.position = finalPos;
                break;
            }
            transform.position += transform.forward * moveDist;
            yield return null;
        }

        onArrived?.Invoke();
    }

    public void InitializeZoneSpawn(ZoneStageConfig config)
    {
        m_shipSpawnQueue = new Queue<ShipInfo>();
        if (config.enemyFleet == null || config.enemyFleet.ships == null) return;
        foreach (var shipInfo in config.enemyFleet.ships)
            m_shipSpawnQueue.Enqueue(shipInfo);
    }

    public void StartSpawning(ZoneStageConfig config)
    {
        InitializeZoneSpawn(config);
        m_spawnCoroutine = StartCoroutine(SpawnShipCoroutine(config));
    }

    public void StopSpawning()
    {
        if (m_spawnCoroutine != null)
        {
            StopCoroutine(m_spawnCoroutine);
            m_spawnCoroutine = null;
        }
        if (m_shipSpawnQueue != null)
            m_shipSpawnQueue.Clear();
    }

    private IEnumerator SpawnShipCoroutine(ZoneStageConfig config)
    {
        if (config.delayBeforeSpawn > 0)
            yield return new WaitForSeconds(config.delayBeforeSpawn);

        while (m_shipSpawnQueue.Count > 0)
        {
            ShipInfo next = m_shipSpawnQueue.Dequeue();
            SpawnSingleShip(next);

            if (m_shipSpawnQueue.Count > 0)
                yield return new WaitForSeconds(config.shipSpawnInterval);
        }

        m_spawnCoroutine = null;
        if (IsFleetAlive() == false)
            ObjectManager.Instance.OnZoneEnemyFleetDefeated(this);
    }

    private void SpawnSingleShip(ShipInfo shipInfo)
    {
        GameObject shipGo = new GameObject(shipInfo.shipName);
        SpaceShip spaceShip = shipGo.AddComponent<SpaceShip>();
        spaceShip.m_bodyMultiplier    = shipInfo.bodyMultiplier;
        spaceShip.m_beamMultiplier    = shipInfo.beamMultiplier;
        spaceShip.m_missileMultiplier = shipInfo.missileMultiplier;
        spaceShip.m_hangerMultiplier  = shipInfo.hangerMultiplier;
        spaceShip.InitializeSpaceShip(this, shipInfo);
        AddShip(spaceShip, bWarp: true);
    }

    // Zone 적 함선을 순차적으로 받아들이기 위한 빈 함대 초기화
    public void InitializeAsZoneEnemyFleetShell(string fleetName, EFormationType formation)
    {
        m_fleetSide   = EFleetSide.fleet_side_enemy;
        m_fleetSource = EFleetSource.fleet_source_zone_data;
        m_fleetInfo   = new FleetInfo
        {
            fleetName = fleetName,
            formation = formation,
            ships     = new List<ShipInfo>()
        };
        m_currentFormationType = formation;
        SetFleetState(EUnitState.BattleExploration);
    }

    public void InitializeSpaceFleet(FleetInfo fleetInfo, EFleetSide side = EFleetSide.fleet_side_player, EFleetSource source = EFleetSource.fleet_source_player, EUnitState fleetState = EUnitState.Idle)
    {
        m_fleetInfo = fleetInfo;
        m_fleetSide = side;
        m_fleetSource = source;
        m_fleetState = fleetState;

        if (m_fleetInfo.ships != null && m_fleetInfo.ships.Count > 0)
        {
            for (int i = 0; i < m_fleetInfo.ships.Count; i++)
                CreateSpaceShipByInfo(fleetInfo.ships[i]);

            UpdateShipFormation(m_fleetInfo.formation, bSmooth: false);
        }
        
        SetFleetState(fleetState);
    }
    // m_fleetInfo.ships에서 id로 항목을 찾아 생성 — 외부에서 ShipInfo 객체 없이 id만 알 때 사용
    public void CreateSpaceShipById(long shipId, bool bWarp = false)
    {
        if (m_fleetInfo.ships == null) return;
        ShipInfo found = null;
        foreach (ShipInfo si in m_fleetInfo.ships)
        {
            if (si.id == shipId) { found = si; break; }
        }
        if (found != null)
            CreateSpaceShipByInfo(found, bWarp);
    }

    // bWarp: 항상 후방 스폰. true면 워프 이펙트+고속 이동, false면 UpdateShipFormation이 배치 담당
    // bFillNullSlot: true면 파괴된 슬롯(null) 자리에 복원, false면 신규 추가(null 슬롯 무시)
    public void CreateSpaceShipByInfo(ShipInfo shipInfo, bool bWarp = false, bool bFillNullSlot = false)
    {
        GameObject shipGo = new GameObject($"{shipInfo.shipName}");
        SpaceShip spaceShip = shipGo.AddComponent<SpaceShip>();
        spaceShip.m_bodyMultiplier    = shipInfo.bodyMultiplier;
        spaceShip.m_beamMultiplier    = shipInfo.beamMultiplier;
        spaceShip.m_missileMultiplier = shipInfo.missileMultiplier;
        spaceShip.m_hangerMultiplier  = shipInfo.hangerMultiplier;
        spaceShip.InitializeSpaceShip(this, shipInfo);
        AddShip(spaceShip, bWarp: bWarp, bFillNullSlot: bFillNullSlot);
    }
    // 워프 진입 시 이동 속도 배율
    private float m_spawnApproachSpeedMult = 60f;

    public void AddShip(SpaceShip ship, bool bWarp = false, bool bFillNullSlot = false)
    {
        if (ship == null) return;

        if (bFillNullSlot == true)
        {
            // 파괴된 함선 복원: positionIndex 순서상 올바른 null 슬롯에 직접 대입
            int nullSlotIdx = -1;
            for (int i = 0; i < m_ships.Count; i++)
            {
                if (m_ships[i] != null) continue;
                int prevPos = -1;
                for (int j = i - 1; j >= 0; j--)
                { if (m_ships[j] != null) { prevPos = m_ships[j].m_shipInfo.positionIndex; break; } }
                int nextPos = int.MaxValue;
                for (int j = i + 1; j < m_ships.Count; j++)
                { if (m_ships[j] != null) { nextPos = m_ships[j].m_shipInfo.positionIndex; break; } }
                if (prevPos < ship.m_shipInfo.positionIndex && ship.m_shipInfo.positionIndex < nextPos)
                { nullSlotIdx = i; break; }
            }
            if (nullSlotIdx >= 0)
            {
                m_ships[nullSlotIdx] = ship;
            }
            else
            {
                Debug.LogWarning($"[AddShip] null 슬롯 없음, 일반 삽입 fallback: positionIndex={ship.m_shipInfo.positionIndex}");
                m_ships.Add(ship);
            }
        }
        else
        {
            // 신규 함선 추가: positionIndex 기준 정렬 삽입 (null 슬롯 무시)
            int insertIdx = m_ships.Count;
            for (int i = 0; i < m_ships.Count; i++)
            {
                if (m_ships[i] != null && m_ships[i].m_shipInfo.positionIndex > ship.m_shipInfo.positionIndex)
                {
                    insertIdx = i;
                    break;
                }
            }
            m_ships.Insert(insertIdx, ship);
        }
        ship.transform.SetParent(transform);
        ship.transform.localRotation = Quaternion.identity;

        // 항상 함대 후방(-Z)에 스폰 — bWarp는 워프 이펙트·고속 여부만 결정
        var targets = CalculateFormationTargets(m_currentFormationType);

        if (targets.TryGetValue(ship, out Vector3 newShipTarget))
        {
            float spawnOffsetZ = 40.0f + ship.CalculateShipBounds().size.z * 0.5f;
            ship.transform.localPosition = new Vector3(newShipTarget.x, newShipTarget.y, newShipTarget.z - spawnOffsetZ);

            if (bWarp == true)
            {
                // 고속 워프 진입 — Moving 상태로 전환되므로 ApplyFleetStateToShip은 Arrived에서 호출됨
                ship.MoveToFormation(newShipTarget, bWarp: true, speedMult: m_spawnApproachSpeedMult);

                if (ship.TryGetComponent(out WarpEffectShip warpEffect) == false)
                {
                    warpEffect = ship.gameObject.AddComponent<WarpEffectShip>();
                    warpEffect.InitializeWarpEffect();
                }
                warpEffect.StartApproachWarp();
            }
            // bWarp=false: 후방 스폰만
        }
        else
        {
            // 진형 슬롯 없으면 에러 상황
            Debug.LogWarning("AddShip formation position is not exist");
        }
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_ShipBodyChanged(OnShipBodyChanged);
        EventManager.Unsubscribe_FleetUpdateHP(OnFleetHPUpdatedForSave);
    }

    public void SaveHealthToServer()
    {
        if (m_fleetSource != EFleetSource.fleet_source_player) return;
        if (NetworkManager.Instance == null) return;
        
        var request = new FleetHealthSaveRequest { ships = new List<ShipHealthInfo>() };
        foreach (SpaceShip ship in m_ships)
        {
            if (ship == null) continue;
            var shipHealth = new ShipHealthInfo
            {
                shipId = ship.m_shipInfo.id,
                bodies = new List<BodyHealthEntry>()
            };
            foreach (ModuleBody body in ship.m_moduleBodys)
            {
                if (body == null) continue;
                shipHealth.bodies.Add(new BodyHealthEntry
                {
                    bodyIndex = body.GetModuleBodyIndex(),
                    currentHealth = body.m_health
                });
            }
            request.ships.Add(shipHealth);
        }
        
        NetworkManager.Instance.FleetHealthSave(request);
    }

    // 소속 함선의 Body 교체로 크기가 바뀌면 간격 재조정
    private void OnShipBodyChanged(SpaceShip ship)
    {
        if (m_ships.Contains(ship) == true)
            RefreshFormation();
    }

    // 진형 재계획 (함선 추가/제거 시 호출)
    public void RefreshFormation()
    {
        // 이동 중인 함선들 중지 후 재계획
        foreach (var ship in m_ships)
        {
            if (ship != null)
                ship.StopFormationMovement();
        }

        UpdateShipFormation(m_currentFormationType, bSmooth: true);
    }

    // shipId로 함선 찾기
    public SpaceShip FindShip(long shipId)
    {
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null && ship.m_shipInfo.id == shipId)
                return ship;
        }
        return null;
    }

    // shipId, bodyIndex, moduleType, slotIndex로 특정 모듈 찾기
    public ModuleBase FindModule(long shipId, int bodyIndex, EModuleType moduleType, int slotIndex)
    {
        SpaceShip ship = FindShip(shipId);
        if (ship == null) return null;

        return ship.FindModule(bodyIndex, moduleType, slotIndex);
    }

    public int GetTotalInvestedMineral()
    {
        int total = 0;
        for (int i = 0; i < m_ships.Count; i++)
        {
            if (m_ships[i] != null)
                total += m_ships[i].GetTotalInvestedMineral();
        }
        return total;
    }

    public void NotifyInvestedMineralChanged()
    {
        int total = GetTotalInvestedMineral();
        EventManager.TriggerInvestedMineralChanged(total);
    }

    // 미네랄 강화 초기화 — 서버에서 내려온 updatedFleetInfo 기준으로 모듈 복원
    public void ApplyMineralReset(FleetInfo updatedFleetInfo)
    {
        if (updatedFleetInfo == null || updatedFleetInfo.ships == null) return;

        foreach (ShipInfo updatedShip in updatedFleetInfo.ships)
        {
            SpaceShip ship = FindShip(updatedShip.id);
            if (ship == null || updatedShip.bodies == null) continue;

            foreach (ModuleBodyInfo updatedBody in updatedShip.bodies)
                ApplyMineralResetBody(ship, updatedBody);

            EventManager.Trigger_ShipStatsChanged(ship);
        }
    }

    private void ApplyMineralResetBody(SpaceShip ship, ModuleBodyInfo updatedBody)
    {
        ModuleBase bodyModule = ship.FindModule(updatedBody.bodyIndex, EModuleType.body, 0);
        if (bodyModule != null)
        {
            bool bodySubTypeChanged = bodyModule.GetModuleSubType() != updatedBody.moduleSubType;
            bool bodyLevelChanged   = bodyModule.GetModuleLevel()   != updatedBody.moduleLevel;
            if (bodySubTypeChanged == true || bodyLevelChanged == true)
                ship.ApplyModuleChange(updatedBody.bodyIndex, EModuleType.body, updatedBody.moduleSubType, 0, updatedBody.moduleLevel, updatedBody.investedMineral, updatedBody.investedModulePoint);
        }

        ModuleBody body = ship.FindModuleBodyByIndex(updatedBody.bodyIndex);
        if (body == null) return;

        ApplyMineralResetModuleList(ship, body, updatedBody.beams,    EModuleType.beam);
        ApplyMineralResetModuleList(ship, body, updatedBody.missiles, EModuleType.missile);
        ApplyMineralResetModuleList(ship, body, updatedBody.hangers,  EModuleType.hanger);
    }

    private void ApplyMineralResetModuleList(SpaceShip ship, ModuleBody body, List<ModuleInfo> moduleInfos, EModuleType moduleType)
    {
        int bodyIndex = body.GetModuleBodyIndex();

        if (moduleInfos == null)
        {
            foreach (ModuleSlot slot in body.m_moduleSlots)
            {
                if (slot.m_moduleSlotInfo.moduleType != moduleType) continue;
                ModuleBase module = slot.GetComponentInChildren<ModuleBase>();
                bool isPlaceholder = module != null && module is ModulePlaceholder;
                if (isPlaceholder == true) continue;
                ship.Apply_ResetModuleToPlaceholder(bodyIndex, moduleType, slot.m_moduleSlotInfo.slotIndex);
            }
            return;
        }

        HashSet<int> serverSlotIndexes = new HashSet<int>();
        foreach (ModuleInfo updatedModule in moduleInfos)
        {
            serverSlotIndexes.Add(updatedModule.slotIndex);

            ModuleBase existing = ship.FindModule(bodyIndex, moduleType, updatedModule.slotIndex);
            if (existing == null) continue;

            bool subTypeChanged = existing.GetModuleSubType() != updatedModule.moduleSubType;
            bool levelChanged   = existing.GetModuleLevel()   != updatedModule.moduleLevel;
            if (subTypeChanged == true || levelChanged == true)
                ship.ApplyModuleChange(bodyIndex, moduleType, updatedModule.moduleSubType, updatedModule.slotIndex, updatedModule.moduleLevel, updatedModule.investedMineral, updatedModule.investedModulePoint);
        }

        foreach (ModuleSlot slot in body.m_moduleSlots)
        {
            if (slot.m_moduleSlotInfo.moduleType != moduleType) continue;
            int slotIndex = slot.m_moduleSlotInfo.slotIndex;
            if (serverSlotIndexes.Contains(slotIndex) == true) continue;

            ModuleBase module = slot.GetComponentInChildren<ModuleBase>();
            bool isPlaceholder = module != null && module is ModulePlaceholder;
            if (isPlaceholder == true) continue;

            ship.Apply_ResetModuleToPlaceholder(bodyIndex, moduleType, slotIndex);
        }
    }

    // 기함 반환 (positionIndex == 0, 없으면 첫 번째 non-null 함선)
    public SpaceShip GetFlagship()
    {
        SpaceShip flagship = m_ships.Find(s => s != null && s.m_shipInfo.positionIndex == 0);
        if (flagship == null) flagship = m_ships.Find(s => s != null);
        return flagship;
    }

    // 살아있는 첫 번째 함선 반환
    public SpaceShip GetFirstAliveShip()
    {
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null && ship.IsAlive())
                return ship;
        }
        return null;
    }

    public void UpdateShipFormation(EFormationType formationType = EFormationType.linear_horizontal, bool bSmooth = true)
    {
        m_currentFormationType = formationType;
        Dictionary<SpaceShip, Vector3> targets = CalculateFormationTargets(formationType);

        foreach (var kv in targets)
        {
            if (kv.Key == null) continue;
            if (bSmooth == true)
                kv.Key.MoveToFormation(kv.Value, bWarp: false, speedMult: 1f);
            else
            {
                kv.Key.transform.localPosition = kv.Value;
                kv.Key.ApplyFleetStateToShip();
            }
        }

        EventManager.Trigger_FormationChanged(formationType);
    }

    private static readonly EFormationType[] k_cycleFormations =
    {
        EFormationType.linear_horizontal,
        EFormationType.x_offensive,
        EFormationType.x_defensive,
        //EFormationType.cross_defensive,
    };

    public void CycleFormation()
    {
        int current = System.Array.IndexOf(k_cycleFormations, m_currentFormationType);
        int next = (current + 1) % k_cycleFormations.Length;
        UpdateShipFormation(k_cycleFormations[next], bSmooth: true);
    }

    // 전체 함선 크기 누적 기반 진형 목적지 계산 (positionIndex 고정, 교환 없음)
    public Dictionary<SpaceShip, Vector3> CalculateFormationTargets(EFormationType formationType)
    {
        var result = new Dictionary<SpaceShip, Vector3>();
        var validShips = m_ships.FindAll(s => s != null);
        if (validShips.Count == 0) return result;

        // Renderer 반복 접근 방지용 바운드 캐시
        var boundsCache = new Dictionary<SpaceShip, Bounds>();
        foreach (var s in validShips)
            boundsCache[s] = s.CalculateFormationBounds();

        SpaceShip flagship = GetFlagship();

        var preset = FormationPresetDB.Get(formationType);
        if (preset == null)
        {
            Debug.LogWarning($"[SpaceFleet] 프리셋 없음: {formationType}");
            return result;
        }

        if (preset.parseType == EFormationParseType.CubeGrid)
            ParseCubeGrid(preset, validShips, flagship, boundsCache, result);
        else
            ParseCircle(preset, validShips, flagship, boundsCache, result);

        return result;
    }

    // 함선 간 최소 여백
    // CubeGrid: 정수 격자 좌표 → 축별 누적 간격 변환
    private void ParseCubeGrid(FormationPreset preset, List<SpaceShip> ships, SpaceShip flagship,
        Dictionary<SpaceShip, Bounds> bc, Dictionary<SpaceShip, Vector3> result)
    {
        if (flagship != null) result[flagship] = Vector3.zero;

        // positionIndex → SpaceShip 맵
        var indexToShip = new Dictionary<int, SpaceShip>();
        foreach (var s in ships)
            indexToShip[s.m_shipInfo.positionIndex] = s;

        // positionIndex → 슬롯 맵
        var indexToSlot = new Dictionary<int, FormationSlot>();
        foreach (var slot in preset.slots)
            indexToSlot[slot.positionIndex] = slot;

        // 각 축 최대 슬롯 번호
        int maxAbsX = 0, maxAbsY = 0, maxAbsZ = 0;
        foreach (var slot in preset.slots)
        {
            maxAbsX = Mathf.Max(maxAbsX, Mathf.Abs(slot.gridCoord.x));
            maxAbsY = Mathf.Max(maxAbsY, Mathf.Abs(slot.gridCoord.y));
            maxAbsZ = Mathf.Max(maxAbsZ, Mathf.Abs(slot.gridCoord.z));
        }

        // X/Y: 열별 최대 반폭 사전 계산
        var maxHalfX = new float[maxAbsX + 1];
        var maxHalfY = new float[maxAbsY + 1];
        foreach (var slot in preset.slots)
        {
            if (!indexToShip.TryGetValue(slot.positionIndex, out var s)) continue;
            int ax = Mathf.Abs(slot.gridCoord.x);
            int ay = Mathf.Abs(slot.gridCoord.y);
            if (ax > 0) maxHalfX[ax] = Mathf.Max(maxHalfX[ax], bc[s].size.x * 0.5f);
            if (ay > 0) maxHalfY[ay] = Mathf.Max(maxHalfY[ay], bc[s].size.y * 0.5f);
        }

        // X축 누적 간격 (좌/우, bounds-based)
        float[] cumX = new float[maxAbsX + 1];
        float cursorX = flagship != null ? bc[flagship].size.x * 0.5f : 1f;
        for (int n = 1; n <= maxAbsX; n++)
        {
            float half = maxHalfX[n] < 0.1f ? 1f : maxHalfX[n];
            cumX[n] = cursorX + preset.gridGap.x + half;
            cursorX  = cumX[n] + half;
        }

        // Y축 누적 간격 (상/하, bounds-based)
        float[] cumY = new float[maxAbsY + 1];
        float cursorY = flagship != null ? bc[flagship].size.y * 0.5f : 1f;
        for (int n = 1; n <= maxAbsY; n++)
        {
            float half = maxHalfY[n] < 0.1f ? 1f : maxHalfY[n];
            cumY[n] = cursorY + preset.gridGap.y + half;
            cursorY  = cumY[n] + half;
        }

        // Z축: 선두 기준 목표 위치 사전 계산 (각 함선 개별 halfZ는 위치 적용 시 차감)
        float flagshipHalfZ = flagship != null ? flagship.CalculateShipBounds().size.z * 0.5f : 1f;
        float[] zFwdLeadTarget = new float[maxAbsZ + 1]; // Forward: 함선 선두 = flagshipHalfZ + x*n
        float[] zBwdLeadTarget = new float[maxAbsZ + 1]; // Backward: 함선 선두 = flagshipHalfZ - x*n
        for (int n = 1; n <= maxAbsZ; n++)
        {
            // 전진형 진형은 기함의 선두 기준
            zFwdLeadTarget[n] = flagshipHalfZ + preset.gridGap.z * n;
            // 후퇴형 진형은 기함의 중심부 기준
            zBwdLeadTarget[n] = -preset.gridGap.z * n;
        }

        // 각 함선에 위치 적용 (X=좌우, Y=상하, Z=전후)
        foreach (var slot in preset.slots)
        {
            if (slot.positionIndex == 0) continue;
            if (indexToShip.TryGetValue(slot.positionIndex, out var ship) == false) continue;

            float x = slot.gridCoord.x != 0 ? cumX[Mathf.Abs(slot.gridCoord.x)] * Mathf.Sign(slot.gridCoord.x) : 0f;
            float y = slot.gridCoord.y != 0 ? cumY[Mathf.Abs(slot.gridCoord.y)] * Mathf.Sign(slot.gridCoord.y) : 0f;
            float z;
            int gz = slot.gridCoord.z;
            float shipHalfZ = ship.CalculateShipBounds().size.z * 0.5f;
            if (preset.zPlacement == EZPlacement.Forward)
                z = zFwdLeadTarget[Mathf.Abs(gz)] - shipHalfZ;
            else if (preset.zPlacement == EZPlacement.Backward)
                z = zBwdLeadTarget[Mathf.Abs(gz)] - shipHalfZ;
            else // Center
            {
                if (gz == 0)      z = 0f;
                else if (gz > 0)  z =  flagshipHalfZ + (gz - 1) * preset.gridGap.z;
                else              z = -flagshipHalfZ - (Mathf.Abs(gz) - 1) * preset.gridGap.z;
            }
            result[ship] = new Vector3(x, y, z);
        }
    }

    // Circle: 각도(도) → 반지름 기반 원주 위치 변환
    private void ParseCircle(FormationPreset preset, List<SpaceShip> ships, SpaceShip flagship,
        Dictionary<SpaceShip, Bounds> bc, Dictionary<SpaceShip, Vector3> result)
    {
        if (flagship != null) result[flagship] = Vector3.zero;

        var indexToShip = new Dictionary<int, SpaceShip>();
        foreach (var s in ships)
            indexToShip[s.m_shipInfo.positionIndex] = s;

        // 반지름: 함선 사이즈 기반 자동 계산 (XY 평면 기준 — x/y 크기 사용)
        var nonFlagship = ships.FindAll(s => s != flagship);
        float maxSize = 0f;
        float maxHalfZ = 0f;
        foreach (var s in nonFlagship)
        {
            maxSize  = Mathf.Max(maxSize,  bc[s].size.x, bc[s].size.y);
            maxHalfZ = Mathf.Max(maxHalfZ, bc[s].size.z * 0.5f);
        }

        // 반지름은 현재 함선 수가 아닌 프리셋 최대 슬롯 수 기준 — 함선 추가 시 원 크기 변화 방지
        int nForRadius = 0;
        foreach (var slot in preset.slots)
            if (slot.positionIndex != 0) nForRadius++;
        int n = nonFlagship.Count;
        float radiusBySpacing  = nForRadius > 1 ? nForRadius * (maxSize + preset.gridGap.x) / (2f * Mathf.PI) : maxSize + preset.gridGap.x;
        float radiusByFlagship = (flagship != null ? bc[flagship].size.x * 0.5f : 1f) + preset.gridGap.x + maxSize * 0.5f;
        float radius = Mathf.Max(radiusBySpacing, radiusByFlagship);

        // Z: zPlacement 로 단일 Z값 계산 (Circle은 모든 함선이 동일 Z)
        float flagshipHalfZ = flagship != null ? bc[flagship].size.z * 0.5f : 1f;
        float circleZ;
        if (preset.zPlacement == EZPlacement.Forward)
            circleZ = flagshipHalfZ + preset.gridGap.z;
        else if (preset.zPlacement == EZPlacement.Backward)
            circleZ = -(flagshipHalfZ + preset.gridGap.z);
        else // Center: gridGap.z를 직접 Z 오프셋으로 사용
            circleZ = preset.gridGap.z;

        foreach (var slot in preset.slots)
        {
            if (slot.positionIndex == 0) continue;
            if (indexToShip.TryGetValue(slot.positionIndex, out var ship) == false) continue;

            float rad = slot.circleAngle * Mathf.Deg2Rad;
            result[ship] = new Vector3(Mathf.Sin(rad) * radius, Mathf.Cos(rad) * radius, circleZ);
        }
    }


    // 전투 중 파괴 전용 — 슬롯 인덱스를 유지한 채 null로 표시
    public void SetShipNullified(SpaceShip ship)
    {
        if (ship == null) return;
        int idx = m_ships.IndexOf(ship);
        if (idx >= 0) m_ships[idx] = null;

        if (IsFleetAlive() == false)
        {
            if (IsZoneEnemy)
                ObjectManager.Instance.OnZoneEnemyFleetDefeated(this);
            else if (IsEnemy)
                ObjectManager.Instance.RemoveEnemyFleet(this);
            else
                EventManager.Trigger_MyFleetDestroyed();
        }
    }

    public void RemoveShip(SpaceShip ship, bool refreshFormation = false)
    {
        if (ship == null) return;
        m_ships.Remove(ship);

        if (IsFleetAlive() == true)
        {
            Destroy(ship.gameObject);
            if (refreshFormation)
                RefreshFormation();
        }
        else
        {
            if (IsZoneEnemy)
                ObjectManager.Instance.OnZoneEnemyFleetDefeated(this);
            else if (IsEnemy)
                ObjectManager.Instance.RemoveEnemyFleet(this);
            else
                EventManager.Trigger_MyFleetDestroyed();
        }
    }

    // 함대 전체 재건 (전멸 후 복구용, 모든 함선 재생성)
    public void RebuildFleet(float healthRatio = 0.1f)
    {
        StopAllCoroutines();

        for (int i = m_ships.Count - 1; i >= 0; i--)
        {
            if (m_ships[i] != null)
                Destroy(m_ships[i].gameObject);
        }
        m_ships.Clear();

        if (m_fleetInfo.ships != null && m_fleetInfo.ships.Count > 0)
        {
            for (int i = 0; i < m_fleetInfo.ships.Count; i++)
                CreateSpaceShipByInfo(m_fleetInfo.ships[i]);

            UpdateShipFormation(m_fleetInfo.formation, bSmooth: false);
        }

        // ShipSelector가 새 함선 객체를 참조하도록 먼저 갱신, 이후 HP 이벤트
        EventManager.Trigger_FleetShipCountChanged();
        ApplyHealthRatio(healthRatio);
    }

    // 파괴된 함선만 복구 (퇴각용, 살아있는 함선은 현재 체력 유지)
    public void RestoreDestroyedShips(float healthRatio = 0.1f)
    {
        HashSet<long> aliveShipIds = new HashSet<long>();
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null)
                aliveShipIds.Add(ship.m_shipInfo.id);
        }

        bool hasRestored = false;
        foreach (ShipInfo shipInfo in m_fleetInfo.ships)
        {
            if (aliveShipIds.Contains(shipInfo.id)) continue;

            CreateSpaceShipByInfo(shipInfo, bWarp: true, bFillNullSlot: true);
            SpaceShip newShip = FindShip(shipInfo.id);
            if (newShip != null)
            {
                foreach (ModuleBody body in newShip.m_moduleBodys)
                {
                    if (body != null)
                        body.m_health = body.m_healthMax * healthRatio;
                }
                newShip.UpdateShipStatCur();
            }
            hasRestored = true;
        }

        if (hasRestored)
        {
            EventManager.Trigger_FleetShipCountChanged();
            EventManager.Trigger_FleetUpdateHP();
            EventManager.Trigger_ShipUpdateHP();
        }
    }

    public void FullRepair()
    {
        ApplyHealthRatio(1f);
    }

    public float GetMissingHealth()
    {
        float missing = 0f;
        foreach (SpaceShip ship in m_ships)
        {
            if (ship == null) continue;
            foreach (ModuleBody body in ship.m_moduleBodys)
            {
                if (body == null) continue;
                missing += body.m_healthMax - body.m_health;
            }
        }
        return missing;
    }

    // 모든 함선의 체력을 지정 비율로 설정
    private void ApplyHealthRatio(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        foreach (SpaceShip ship in m_ships)
        {
            if (ship == null) continue;
            foreach (ModuleBody body in ship.m_moduleBodys)
            {
                if (body == null) continue;
                body.m_health = body.m_healthMax * ratio;
            }
            ship.UpdateShipStatCur();
            ship.CheckFireEffects();
        }
        EventManager.Trigger_FleetUpdateHP();
        EventManager.Trigger_ShipUpdateHP();
    }

    public bool IsFleetAlive()
    {
        if (m_shipSpawnQueue != null && m_shipSpawnQueue.Count > 0)
            return true;
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null && ship.IsAlive() == true)
                return true;
        }
        return false;
    }

    public SpaceShip GetRandomAliveShip()
    {
        List<SpaceShip> aliveShips = new List<SpaceShip>();
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null && ship.IsAlive() == true)
                aliveShips.Add(ship);
        }

        if (aliveShips.Count > 0)
        {
            int randomIndex = Random.Range(0, aliveShips.Count);
            return aliveShips[randomIndex];
        }

        return null;
    }

    // 워프 진입이 완료된 함선만 대상 — 워핑 중인 함선은 제외
    public SpaceShip GetRandomAliveShipWarpDone()
    {
        List<SpaceShip> candidates = new();
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null && ship.IsAlive() == true && ship.IsWarping == false)
                candidates.Add(ship);
        }

        if (candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];

        return null;
    }

    public ModuleBody GetRandomAliveBodyPart()
    {
        SpaceShip aliveShip = GetRandomAliveShip();
        if (aliveShip == null) return null;

        List<ModuleBody> aliveBodies = new List<ModuleBody>();
        foreach (ModuleBody body in aliveShip.m_moduleBodys)
        {
            if (body != null && body.m_health > 0)
                aliveBodies.Add(body);
        }

        if (aliveBodies.Count > 0)
        {
            int randomIndex = Random.Range(0, aliveBodies.Count);
            return aliveBodies[randomIndex];
        }

        return null;
    }

    public void RemoveDeadShips()
    {
        for (int i = m_ships.Count - 1; i >= 0; i--)
        {
            if (m_ships[i] == null || m_ships[i].IsAlive() == false)
            {
                if (m_ships[i] != null)
                {
                    Destroy(m_ships[i].gameObject);
                }
                m_ships.RemoveAt(i);
            }
        }
    }

    public void SetFleetState(EUnitState fleetState)
    {
        m_fleetState = fleetState;
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null && ship.IsAlive())
                ship.ApplyFleetStateToShip();
        }
        if (m_fleetSource == EFleetSource.fleet_source_player)
        {
            if (fleetState.IsBattleState() == true)
                StartBattleRepairCostLoop();
            else
                StopBattleRepairCostLoop();
        }
        if (m_fleetSide == EFleetSide.fleet_side_player)
            EventManager.TriggerMyFleetStateChanged(fleetState);
    }

    private static readonly WaitForSeconds k_battleRepairCostInterval = new WaitForSeconds(1f);
    private Coroutine m_battleRepairCostCoroutine;

    private void StartBattleRepairCostLoop()
    {
        if (m_battleRepairCostCoroutine != null) StopCoroutine(m_battleRepairCostCoroutine);
        m_battleRepairCostCoroutine = StartCoroutine(BattleRepairCostLoop());
    }

    private void StopBattleRepairCostLoop()
    {
        if (m_battleRepairCostCoroutine == null) return;
        StopCoroutine(m_battleRepairCostCoroutine);
        m_battleRepairCostCoroutine = null;
    }

    private IEnumerator BattleRepairCostLoop()
    {
        while (true)
        {
            yield return k_battleRepairCostInterval;
            if (m_fleetState.IsBattleState() == false) continue;
            if (m_fleetInfo == null || (m_fleetInfo.tacticOptions & 1) == 0) continue;
            if (HasAnyDamagedShip() == false) continue;

            Character character = DataManager.Instance.m_currentCharacter;
            if (character == null) continue;

            int cost = DataManager.Instance.m_dataTableConfig.gameSettings.battleRepairMineralPerSec;
            character.TryConsumeMineral(cost);
        }
    }

    private bool HasAnyDamagedShip()
    {
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null && ship.IsAlive() == true && ship.GetHealthRatio() < 1f)
                return true;
        }
        return false;
    }

    // 함대의 능력치 프로파일 계산
    public CapabilityProfile GetFleetCapabilityProfile(bool useCurrent = true)
    {
        CapabilityProfile totalStats = new CapabilityProfile();
        int shipCount = 0;
        foreach (SpaceShip ship in m_ships)
        {
            if (ship == null) continue;
            shipCount++;
            CapabilityProfile shipStats = useCurrent ? ship.m_spaceShipStatsCur : ship.m_spaceShipStatsOrg;
            totalStats.totalWeapons += shipStats.totalWeapons;
            totalStats.attack += shipStats.attack;
            totalStats.health += shipStats.health;
            totalStats.speed += shipStats.speed;
            totalStats.repair += shipStats.repair;
            totalStats.airAttack += shipStats.airAttack;
            totalStats.airCount += shipStats.airCount;
        }
        // 일단 평균
        totalStats.speed /= shipCount;

        return totalStats;
    }

    public void ClearAllSelectedModule()
    {
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null)
                ship.ClearSelectedModule();
        }
    }

    #region Fleet Balance Bonus -------------------------------------------------------

    public int GetAliveShipCount()
    {
        int count = 0;
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null && ship.IsAlive() == true)
                count++;
        }
        return count;
    }

    // 함선 수 공격력 배율: 1척=1.0, 2척부터 보너스, 9척=3.0
    public float GetShipCountAttackMultiplier()
    {
        int n = GetAliveShipCount();
        if (n <= 1) return 1f;
        return 1f + 2f * (n - 1) / 8f;
    }

    // 진형 단계: 3척=1, 5척=2, 7척=3, 9척=4, 미달=0
    public int GetFormationStep()
    {
        int n = GetAliveShipCount();
        if (n >= 9) return 4;
        if (n >= 7) return 3;
        if (n >= 5) return 2;
        if (n >= 3) return 1;
        return 0;
    }

    // 공격력 배율 — 1f + delta (보너스=양수, 패널티=음수)
    public float GetFormationAttackMultiplier()
    {
        int step = GetFormationStep();
        if (step == 0) return 1f;
        FormationPreset preset = FormationPresetDB.Get(m_currentFormationType);
        if (preset == null || preset.attackMultiplierPerStep == null || preset.attackMultiplierPerStep.Length < step) return 1f;
        return 1f + preset.attackMultiplierPerStep[step - 1];
    }

    // 피격 데미지 차감 비율 — 그대로 반환 (0=없음, 0.2~0.5=차감)
    public float GetFormationDefenseReduction()
    {
        int step = GetFormationStep();
        if (step == 0) return 0f;
        FormationPreset preset = FormationPresetDB.Get(m_currentFormationType);
        if (preset == null || preset.defenseReductionPerStep == null || preset.defenseReductionPerStep.Length < step) return 0f;
        return preset.defenseReductionPerStep[step - 1];
    }

    // 회복력 배율 — 1f + delta (보너스=양수, 패널티=음수)
    public float GetFormationRepairMultiplier()
    {
        int step = GetFormationStep();
        if (step == 0) return 1f;
        FormationPreset preset = FormationPresetDB.Get(m_currentFormationType);
        if (preset == null || preset.repairMultiplierPerStep == null || preset.repairMultiplierPerStep.Length < step) return 1f;
        return 1f + preset.repairMultiplierPerStep[step - 1];
    }

    #endregion Fleet Balance Bonus ----------------------------------------------------

}