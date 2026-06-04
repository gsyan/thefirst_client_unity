//------------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 수리 시 한 함선을 어느 수준까지 회복한 뒤 다음 함선으로 넘어갈지
public enum ERepairThreshold
{
    Full,       // 100%까지 회복
    TwoThirds,  // 2/3까지 회복
    OneThird,   // 1/3까지 회복
}

// 동시에 몇 대의 함선을 수리할지
public enum ERepairConcurrency
{
    One,    // 1대 집중
    Two,    // 2대 동시
    Three,  // 3대 동시
    All,    // 체력 100% 아닌 함선 모두
}

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

    // 수리 설정
    public ERepairThreshold m_repairThreshold = ERepairThreshold.Full;
    public ERepairConcurrency m_repairConcurrency = ERepairConcurrency.One;

    // Zone 스폰 상태 (IsZoneEnemy 전용)
    public Queue<EnemyShipConfig> m_shipSpawnQueue;
    private Coroutine m_spawnCoroutine;
    private Coroutine m_warpInCoroutine;
    
    private void Start()
    {
        EventManager.Subscribe_ShipBodyChanged(OnShipBodyChanged);
        if (m_fleetSource == EFleetSource.fleet_source_player || m_fleetSource == EFleetSource.fleet_source_player_remote)
            StartCoroutine(AutoRepair());
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

        float offsetDist = flagship.CalculateShipBounds().size.z * m_spawnOffsetMultiplier;
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
        m_shipSpawnQueue = new Queue<EnemyShipConfig>();
        foreach (var cfg in config.enemyShipConfigs)
            m_shipSpawnQueue.Enqueue(cfg);
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
            EnemyShipConfig next = m_shipSpawnQueue.Dequeue();
            SpawnSingleShip(next);

            if (m_shipSpawnQueue.Count > 0)
                yield return new WaitForSeconds(config.shipSpawnInterval);
        }

        m_spawnCoroutine = null;
        if (IsFleetAlive() == false)
            ObjectManager.Instance.OnZoneEnemyFleetDefeated(this);
    }

    private void SpawnSingleShip(EnemyShipConfig config)
    {
        ShipInfo shipInfo = CreateShipInfoFromConfig(config, config.shipIndex);
        GameObject shipGo = new GameObject(shipInfo.shipName);
        SpaceShip spaceShip = shipGo.AddComponent<SpaceShip>();
        spaceShip.m_bodyMultiplier    = config.bodyMultiplier;
        spaceShip.m_beamMultiplier    = config.beamMultiplier;
        spaceShip.m_missileMultiplier = config.missileMultiplier;
        spaceShip.m_hangerMultiplier  = config.hangerMultiplier;
        spaceShip.InitializeSpaceShip(this, shipInfo);
        AddShip(spaceShip, bWarp: true);
    }

    private ShipInfo CreateShipInfoFromConfig(EnemyShipConfig config, int positionIndex)
    {
        ModuleData bodyModuleData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(config.bodySubType, config.bodyLevel);
        var bodyInfo = new ModuleBodyInfo
        {
            moduleType = EModuleType.body,
            moduleSubType = config.bodySubType,
            moduleLevel = config.bodyLevel,
            bodyIndex = 0,
            currentHealth = bodyModuleData != null ? bodyModuleData.health : 0f,
            beams = new List<ModuleInfo>(),
            missiles = new List<ModuleInfo>(),
            hangers = new List<ModuleInfo>()
        };

        foreach (var slot in config.moduleSlots)
        {
            if (slot.moduleSubType == EModuleSubType.none) continue;

            var moduleInfo = new ModuleInfo
            {
                moduleType = slot.slotType,
                moduleSubType = slot.moduleSubType,
                moduleLevel = slot.moduleLevel,
                bodyIndex = 0,
                slotIndex = slot.slotIndex
            };

            switch (slot.slotType)
            {
                case EModuleType.beam:
                    bodyInfo.beams.Add(moduleInfo);
                    break;
                case EModuleType.missile:
                    bodyInfo.missiles.Add(moduleInfo);
                    break;
                case EModuleType.hanger:
                    bodyInfo.hangers.Add(moduleInfo);
                    break;
            }
        }

        return new ShipInfo
        {
            shipName = $"EnemyShip_{positionIndex}",
            positionIndex = positionIndex,
            bodies = new List<ModuleBodyInfo> { bodyInfo }
        };
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
        SetFleetState(EUnitState.Battle);
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
                CreateSpaceShipFromData(fleetInfo.ships[i]);

            UpdateShipFormation(m_fleetInfo.formation, bSmooth: false);
        }
        
        SetFleetState(fleetState);
    }
    // bWarp: 항상 후방 스폰. true면 워프 이펙트+고속 이동, false면 UpdateShipFormation이 배치 담당
    // bFillNullSlot: true면 파괴된 슬롯(null) 자리에 복원, false면 신규 추가(null 슬롯 무시)
    public void CreateSpaceShipFromData(ShipInfo shipInfo, bool bWarp = false, bool bFillNullSlot = false)
    {
        GameObject shipGo = new GameObject($"{shipInfo.shipName}");
        SpaceShip spaceShip = shipGo.AddComponent<SpaceShip>();
        spaceShip.InitializeSpaceShip(this, shipInfo);
        AddShip(spaceShip, bWarp: bWarp, bFillNullSlot: bFillNullSlot);
    }
    // 함선 추가 시 스폰 오프셋 배율 (함선 z크기 * 배율 만큼 목적지 뒤에서 워프 진입)
    private float m_spawnOffsetMultiplier = 40f;
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
            float spawnOffsetZ = ship.CalculateShipBounds().size.z * m_spawnOffsetMultiplier;
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
        SaveHealthToServer();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus == true)
            SaveHealthToServer();
    }

    private void SaveHealthToServer()
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
        // Z: Forward/Backward 별로 분리 (Center는 bounds 미사용)
        var maxHalfZFwd = new float[maxAbsZ + 1];
        var maxHalfZBwd = new float[maxAbsZ + 1];
        foreach (var slot in preset.slots)
        {
            if (!indexToShip.TryGetValue(slot.positionIndex, out var s)) continue;
            int ax = Mathf.Abs(slot.gridCoord.x);
            int ay = Mathf.Abs(slot.gridCoord.y);
            int az = Mathf.Abs(slot.gridCoord.z);
            if (ax > 0) maxHalfX[ax] = Mathf.Max(maxHalfX[ax], bc[s].size.x * 0.5f);
            if (ay > 0) maxHalfY[ay] = Mathf.Max(maxHalfY[ay], bc[s].size.y * 0.5f);
            if (az > 0)
            {
                if (preset.zPlacement == EZPlacement.Forward)
                    maxHalfZFwd[az] = Mathf.Max(maxHalfZFwd[az], bc[s].size.z * 0.5f);
                else if (preset.zPlacement == EZPlacement.Backward)
                    maxHalfZBwd[az] = Mathf.Max(maxHalfZBwd[az], bc[s].size.z * 0.5f);
            }
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

        // Z축: Forward/Backward bounds-based 누적, Center는 flagship halfZ 기준 고정 레이어
        float flagshipHalfZ = flagship != null ? bc[flagship].size.z * 0.5f : 1f;
        float[] cumZFwd = new float[maxAbsZ + 1];
        float cursorZFwd = flagshipHalfZ;
        for (int n = 1; n <= maxAbsZ; n++)
        {
            float half = maxHalfZFwd[n] < 0.1f ? 1f : maxHalfZFwd[n];
            cumZFwd[n] = preset.zIncludeHalfSize == false
                ? cursorZFwd + preset.gridGap.z
                : cursorZFwd + preset.gridGap.z + half;
            cursorZFwd  = cumZFwd[n] + half;
        }
        float[] cumZBwd = new float[maxAbsZ + 1];
        float cursorZBwd = flagshipHalfZ;
        for (int n = 1; n <= maxAbsZ; n++)
        {
            float half = maxHalfZBwd[n] < 0.1f ? 1f : maxHalfZBwd[n];
            cumZBwd[n] = preset.zIncludeHalfSize == false
                ? cursorZBwd + preset.gridGap.z
                : cursorZBwd + preset.gridGap.z + half;
            cursorZBwd  = cumZBwd[n] + half;
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
            if (preset.zPlacement == EZPlacement.Forward)
                z = cumZFwd[Mathf.Abs(gz)];
            else if (preset.zPlacement == EZPlacement.Backward)
                z = -cumZBwd[Mathf.Abs(gz)];
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

        // Z: zPlacement + zIncludeHalfSize 로 단일 Z값 계산 (Circle은 모든 함선이 동일 Z)
        float flagshipHalfZ = flagship != null ? bc[flagship].size.z * 0.5f : 1f;
        float circleZ;
        if (preset.zPlacement == EZPlacement.Forward)
            circleZ = preset.zIncludeHalfSize == true
                ? flagshipHalfZ + preset.gridGap.z + maxHalfZ
                : flagshipHalfZ + preset.gridGap.z;
        else if (preset.zPlacement == EZPlacement.Backward)
            circleZ = preset.zIncludeHalfSize == true
                ? -(flagshipHalfZ + preset.gridGap.z + maxHalfZ)
                : -(flagshipHalfZ + preset.gridGap.z);
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

        // AddShip 이후 SetFleetData 호출로 m_fleetInfo가 DataManager와 분리될 수 있으므로 동기화
        if (DataManager.Instance != null && DataManager.Instance.m_currentFleetInfo != null)
            m_fleetInfo = DataManager.Instance.m_currentFleetInfo;

        if (m_fleetInfo.ships != null && m_fleetInfo.ships.Count > 0)
        {
            for (int i = 0; i < m_fleetInfo.ships.Count; i++)
                CreateSpaceShipFromData(m_fleetInfo.ships[i]);

            UpdateShipFormation(m_fleetInfo.formation, bSmooth: false);
        }

        // ShipSelector가 새 함선 객체를 참조하도록 먼저 갱신, 이후 HP 이벤트
        EventManager.Trigger_FleetShipCountChanged();
        ApplyHealthRatio(healthRatio);

        if (m_fleetSource == EFleetSource.fleet_source_player || m_fleetSource == EFleetSource.fleet_source_player_remote)
            StartCoroutine(AutoRepair());
    }

    // 파괴된 함선만 복구 (퇴각용, 살아있는 함선은 현재 체력 유지)
    public void RestoreDestroyedShips(float healthRatio = 0.1f)
    {
        // AddShip 이후 SetFleetData 호출로 m_fleetInfo가 DataManager와 분리될 수 있으므로 동기화
        if (DataManager.Instance != null && DataManager.Instance.m_currentFleetInfo != null)
            m_fleetInfo = DataManager.Instance.m_currentFleetInfo;

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

            CreateSpaceShipFromData(shipInfo, bWarp: true, bFillNullSlot: true);
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
        SaveHealthToServer();
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
        if (m_fleetSide == EFleetSide.fleet_side_player)
            EventManager.TriggerMyFleetStateChanged(fleetState);
    }

    // 함선의 전체 체력 비율 계산 (모든 바디의 합산)
    private float GetShipHealthRatio(SpaceShip ship)
    {
        float totalHealth = 0f;
        float totalMaxHealth = 0f;
        foreach (ModuleBody body in ship.m_moduleBodys)
        {
            if (body == null) continue;
            totalHealth += body.m_health;
            totalMaxHealth += body.m_healthMax;
        }
        return totalMaxHealth > 0f ? totalHealth / totalMaxHealth : 1f;
    }

    // 임계값 enum → 실제 비율
    private float GetRepairThresholdRatio()
    {
        switch (m_repairThreshold)
        {
            case ERepairThreshold.TwoThirds: return 2f / 3f;
            case ERepairThreshold.OneThird:  return 1f / 3f;
            default:                         return 1f;
        }
    }

    public IEnumerator AutoRepair()
    {
        bool isPlayerFleet = m_fleetSource == EFleetSource.fleet_source_player;

        while (IsFleetAlive() == true)
        {
            yield return new WaitForSeconds(1.0f);

            bool isBattle = m_fleetState == EUnitState.Battle;

            // 전투 중: tacticOptions bit 0(UseBattleRepair)이 꺼져 있으면 건너뜀
            if (isBattle && m_fleetInfo != null && (m_fleetInfo.tacticOptions & 1) == 0) continue;

            CapabilityProfile fleetStats = GetFleetCapabilityProfile(true);
            float totalRepair = fleetStats.repair;
            if (totalRepair <= 0f) continue;

            // 전투 중, Player 함대: Mineral 잔액 없으면 수리 건너뜀
            Character character = isPlayerFleet ? DataManager.Instance.m_currentCharacter : null;
            if (isBattle && isPlayerFleet && (character == null || character.GetMineral() <= 0)) continue;

            float threshold = GetRepairThresholdRatio();

            // 수리가 필요한 함선 수집 (체력비율이 threshold 미만이거나, threshold 이상이지만 100% 미달)
            // 우선순위: 체력 비율이 낮은 함선부터
            List<SpaceShip> needRepair = new List<SpaceShip>();
            foreach (SpaceShip ship in m_ships)
            {
                if (ship == null || ship.IsAlive() == false) continue;
                if (GetShipHealthRatio(ship) < 1f)
                    needRepair.Add(ship);
            }

            if (needRepair.Count == 0) continue;

            // 체력 비율이 낮은 순으로 정렬
            needRepair.Sort((a, b) => GetShipHealthRatio(a).CompareTo(GetShipHealthRatio(b)));

            // 동시 수리 대수 결정
            int maxTargets;
            switch (m_repairConcurrency)
            {
                case ERepairConcurrency.Two:   maxTargets = 2; break;
                case ERepairConcurrency.Three: maxTargets = 3; break;
                case ERepairConcurrency.All:   maxTargets = needRepair.Count; break;
                default:                       maxTargets = 1; break;
            }

            // threshold 미달인 함선 우선, 그 다음 나머지
            List<SpaceShip> targets = new List<SpaceShip>();
            foreach (SpaceShip ship in needRepair)
            {
                if (targets.Count >= maxTargets) break;
                if (GetShipHealthRatio(ship) < threshold)
                    targets.Add(ship);
            }
            // 아직 자리가 남으면 threshold 이상~100% 미달 함선도 추가
            if (targets.Count < maxTargets)
            {
                foreach (SpaceShip ship in needRepair)
                {
                    if (targets.Count >= maxTargets) break;
                    if (targets.Contains(ship) == false)
                        targets.Add(ship);
                }
            }

            // 총 수리력을 대상 수로 균등 분배
            float repairPerTarget = totalRepair / targets.Count;
            float totalActualRepaired = 0f;

            foreach (SpaceShip ship in targets)
            {
                // 함선 내 바디별 균등 분배
                int aliveBodyCount = 0;
                foreach (ModuleBody body in ship.m_moduleBodys)
                {
                    if (body != null && body.m_health < body.m_healthMax)
                        aliveBodyCount++;
                }
                if (aliveBodyCount == 0) continue;

                float repairPerBody = repairPerTarget / aliveBodyCount;

                foreach (ModuleBody body in ship.m_moduleBodys)
                {
                    if (body == null || body.m_health >= body.m_healthMax) continue;
                    float before = body.m_health;
                    body.m_health = Mathf.Min(body.m_health + repairPerBody, body.m_healthMax);
                    totalActualRepaired += body.m_health - before;
                }

                ship.UpdateShipStatCur();
                ship.CheckFireEffects();
                EventManager.Trigger_ShipUpdateHP();
            }

            // 실제 회복된 HP만큼 Mineral 차감 (전투 중, Player 함대만)
            if (isBattle && isPlayerFleet && totalActualRepaired > 0f)
                character?.TryConsumeMineral(Mathf.CeilToInt(totalActualRepaired));

            EventManager.Trigger_FleetUpdateHP();
        }
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

}