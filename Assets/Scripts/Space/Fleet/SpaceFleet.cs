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
    public EFleetState m_fleetState = EFleetState.None;

    // 편의 프로퍼티
    public bool IsEnemy => m_fleetSide == EFleetSide.fleet_side_enemy;
    public bool IsZoneEnemy => m_fleetSource == EFleetSource.fleet_source_zone_data;
    public bool IsPvpEnemy => m_fleetSource == EFleetSource.fleet_source_player_remote;
    public EFormationType m_currentFormationType = EFormationType.formation_type_linear_horizontal;
    [SerializeField] public List<SpaceShip> m_ships = new List<SpaceShip>();

    // 수리 설정
    public ERepairThreshold m_repairThreshold = ERepairThreshold.Full;
    public ERepairConcurrency m_repairConcurrency = ERepairConcurrency.One;

    // 적 함대 모듈별 스탯 배율 (IsZoneEnemy일 때만 적용, 기본값 1.0)
    public float m_bodyMultiplier    = 1.0f;
    public float m_beamMultiplier    = 1.0f;
    public float m_missileMultiplier = 1.0f;
    public float m_hangerMultiplier  = 1.0f;
    
    private void Start()
    {
        EventManager.Subscribe_ShipBodyChanged(OnShipBodyChanged);
        if (m_fleetSource == EFleetSource.fleet_source_player || m_fleetSource == EFleetSource.fleet_source_player_remote)
            StartCoroutine(AutoRepair());
    }

    // Zone 적 전용 — 배율을 먼저 설정 후 기본 초기화 위임
    public void InitializeZoneEnemyFleet(FleetInfo fleetInfo, ZoneConfig zoneConfig)
    {
        if (zoneConfig != null)
        {
            m_bodyMultiplier    = zoneConfig.enemyBodyMultiplier;
            m_beamMultiplier    = zoneConfig.enemyBeamMultiplier;
            m_missileMultiplier = zoneConfig.enemyMissileMultiplier;
            m_hangerMultiplier  = zoneConfig.enemyHangerMultiplier;
        }
        // Move 상태로 초기화 — 워프 완료 후 Battle로 전환
        InitializeSpaceFleet(fleetInfo, EFleetSide.fleet_side_enemy, EFleetSource.fleet_source_zone_data, EFleetState.Move);
        StartEnemyFleetWarpIn();
    }

    // 적 함대 스폰 시 fleet 오브젝트를 기함 크기 기준 오프셋만큼 뒤에서 시작, 진형 유지하며 전진
    public void StartEnemyFleetWarpIn()
    {
        SpaceShip flagship = m_ships.Find(s => s != null && s.m_shipInfo.positionIndex == 0);
        if (flagship == null && m_ships.Count > 0) flagship = m_ships[0];
        if (flagship == null) return;

        // 기함 z 크기 * 플레이어 스폰과 동일한 배율로 뒤 오프셋
        float offsetDist = flagship.CalculateShipBounds().size.z * m_spawnOffsetMultiplier;
        Vector3 finalPos  = transform.position;
        transform.position = finalPos - transform.forward * offsetDist;

        // 각 함선에 워프 이펙트 — fleet 오브젝트가 이동하는 동안 유지
        foreach (SpaceShip ship in m_ships)
        {
            if (ship == null) continue;
            if (ship.TryGetComponent(out WarpEffectShip warpEffect) == false)
            {
                warpEffect = ship.gameObject.AddComponent<WarpEffectShip>();
                warpEffect.InitializeWarpEffect();
            }
            warpEffect.StartEnemyFleetWarpIn();
        }

        float warpSpeed = flagship.m_spaceShipStatsCur.speed * m_spawnApproachSpeedMult;
        float normalSpeed = flagship.m_spaceShipStatsCur.speed;
        StartCoroutine(EnemyFleetWarpInMove(finalPos, warpSpeed, normalSpeed));
    }

    private const float WARP_STOP_DIST = 1f; // 목표 1유닛 전에 워프 이펙트 종료

    // fleet 오브젝트를 finalPos까지 이동, 1유닛 전 워프 종료 → 기본속도로 마저 이동 → Battle 전환
    private IEnumerator EnemyFleetWarpInMove(Vector3 finalPos, float warpSpeed, float normalSpeed)
    {
        bool warpStopped = false;

        while (true)
        {
            Vector3 toTarget = finalPos - transform.position;
            float dist = toTarget.magnitude;

            // 1유닛 전 워프 이펙트 종료, 이후 기본 속도로 진입
            if (warpStopped == false && dist <= WARP_STOP_DIST)
            {
                foreach (SpaceShip ship in m_ships)
                {
                    if (ship != null && ship.TryGetComponent(out WarpEffectShip warpEffect))
                        warpEffect.StopWarp();
                }
                warpStopped = true;
            }

            float speed = warpStopped == false ? warpSpeed : normalSpeed;
            float moveDist = speed * Time.deltaTime;

            if (dist <= moveDist)
            {
                transform.position = finalPos;
                break;
            }
            transform.position += toTarget.normalized * moveDist;
            yield return null;
        }

        // 도착 — Battle 전환, 아군 FindTargetModuleBody가 자연히 적을 탐색함
        SetFleetState(EFleetState.Battle);
    }

    public void InitializeSpaceFleet(FleetInfo fleetInfo, EFleetSide side = EFleetSide.fleet_side_player, EFleetSource source = EFleetSource.fleet_source_player, EFleetState fleetState = EFleetState.None)
    {
        m_fleetInfo = fleetInfo;
        m_fleetSide = side;
        m_fleetSource = source;
        m_fleetState = fleetState;

        if (m_fleetInfo.ships != null && m_fleetInfo.ships.Count > 0)
        {
            for (int i = 0; i < m_fleetInfo.ships.Count; i++)
                CreateSpaceShipFromData(fleetInfo.ships[i]);

            UpdateShipFormation(m_fleetInfo.formation, false);
        }
        
        SetFleetState(fleetState);
    }
    // smoothSpawn: true면 기함 뒤에서 스폰 후 이동, false면 즉시 진형 위치에 배치
    public void CreateSpaceShipFromData(ShipInfo shipInfo, bool smoothSpawn = false)
    {
        GameObject shipGo = new GameObject($"{shipInfo.shipName}");
        SpaceShip spaceShip = shipGo.AddComponent<SpaceShip>();
        spaceShip.InitializeSpaceShip(this, shipInfo);
        AddShip(spaceShip, smoothSpawn);
    }
    // 함선 추가 시 스폰 오프셋 배율 (함선 z크기 * 배율 만큼 목적지 뒤에서 워프 진입)
    private float m_spawnOffsetMultiplier = 20f;
    // 워프 진입 시 이동 속도 배율
    private float m_spawnApproachSpeedMult = 60f;

    public void AddShip(SpaceShip ship, bool placeInFormation = false)
    {
        if (ship == null) return;
        m_ships.Add(ship);
        ship.transform.SetParent(transform);
        ship.transform.localRotation = Quaternion.identity;

        if (placeInFormation == false) return;

        // 신규 함선의 진형 목적지 계산 (positionIndex 기반, 기존 함선 위치 불변)
        var targets = CalculateFormationTargets(m_currentFormationType);

        if (targets.TryGetValue(ship, out Vector3 newShipTarget))
        {
            // 목적지 뒤쪽(-Z)에 스폰 — 함선 z크기 * m_spawnOffsetMultiplier 만큼
            float spawnOffsetZ = ship.CalculateShipBounds().size.z * m_spawnOffsetMultiplier;
            ship.transform.localPosition = new Vector3(newShipTarget.x, newShipTarget.y, newShipTarget.z - spawnOffsetZ);

            // 고속 워프 진입
            ship.MoveToFormation(newShipTarget, m_spawnApproachSpeedMult);

            if (ship.TryGetComponent(out WarpEffectShip warpEffect) == false)
            {
                warpEffect = ship.gameObject.AddComponent<WarpEffectShip>();
                warpEffect.InitializeWarpEffect();
            }
            warpEffect.StartApproachWarp();
        }
        else
        {
            // 진형 슬롯 없으면 기본 위치
            ship.transform.localPosition = new Vector3(0, 0, -20f);
        }
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_ShipBodyChanged(OnShipBodyChanged);
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

        UpdateShipFormation(m_currentFormationType, smooth: true);
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

    public void UpdateShipFormation(EFormationType formationType = EFormationType.formation_type_linear_horizontal, bool smooth = true)
    {
        m_currentFormationType = formationType;
        var targets = CalculateFormationTargets(formationType);

        foreach (var kv in targets)
        {
            if (kv.Key == null) continue;
            if (smooth == true)
                kv.Key.MoveToFormation(kv.Value);
            else
                kv.Key.transform.localPosition = kv.Value;
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

        SpaceShip flagship = validShips.Find(s => s.m_shipInfo.positionIndex == 0);

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


    public void ChangeFormation(EFormationType newFormationType)
    {
        if (IsEnemy) return;

        var request = new ChangeFormationRequest
        {
            fleetId = m_fleetInfo.id,
            formationType = newFormationType
        };

        NetworkManager.Instance.ChangeFormation(request, (response) =>
        {
            if (response.errorCode == 0)
            {
                UpdateShipFormation(newFormationType);
                if (response.data.updatedFleetInfo != null)
                    DataManager.Instance.SetFleetData(response.data.updatedFleetInfo);
            }
        });
    }

    public void RemoveShip(SpaceShip ship, bool refreshFormation = false)
    {
        if (ship == null) return;
        m_ships.Remove(ship);

        if (IsFleetAlive() == false)
        {
            if (IsEnemy)
                ObjectManager.Instance.RemoveEnemyFleet(this);
            else
                EventManager.Trigger_MyFleetDestroyed();
        }
        else if (refreshFormation)
        {
            RefreshFormation();
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
                CreateSpaceShipFromData(m_fleetInfo.ships[i]);

            UpdateShipFormation(m_fleetInfo.formation, false);
        }

        ApplyHealthRatio(healthRatio);

        if (m_fleetSource == EFleetSource.fleet_source_player || m_fleetSource == EFleetSource.fleet_source_player_remote)
            StartCoroutine(AutoRepair());
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

            CreateSpaceShipFromData(shipInfo);
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
            RefreshFormation();
            EventManager.Trigger_FleetUpdateHP();
            EventManager.Trigger_ShipUpdateHP();
        }
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
        }
        EventManager.Trigger_FleetUpdateHP();
        EventManager.Trigger_ShipUpdateHP();
    }

    public bool IsFleetAlive()
    {
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

    public void SetFleetState(EFleetState fleetState)
    {
        m_fleetState = fleetState;
        foreach (SpaceShip ship in m_ships)
        {
            if (ship != null && ship.IsAlive())
                ship.ApplyFleetStateToShip();
        }
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
        while (IsFleetAlive() == true)
        {
            yield return new WaitForSeconds(1.0f);

            CapabilityProfile fleetStats = GetFleetCapabilityProfile(true);
            float totalRepair = fleetStats.repair;
            if (totalRepair <= 0f) continue;

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
                    body.m_health = Mathf.Min(body.m_health + repairPerBody, body.m_healthMax);
                }

                ship.UpdateShipStatCur();
                EventManager.Trigger_ShipUpdateHP();
            }
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
            totalStats.airLaunchCount += shipStats.airLaunchCount;
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

    #region Fleet Warp
    private List<WarpEffectShip> m_warpEffects = new List<WarpEffectShip>();
    private bool m_isFleetWarping = false;

    // 함대 워프 시작 (모든 함선 동시에)
    public void StartFleetWarp(Material skyBoxMaterial, System.Action onWarpComplete = null)
    {
        if (m_isFleetWarping) return;

        m_isFleetWarping = true;
        EnsureWarpEffects();

        // PP가 글로우/스피드라인까지 통합 제어 — warpEffects 리스트를 함께 전달
        var pp = WarpPostProcessing.Instance;
        if (pp != null)
        {
            pp.StartWarpSequence(skyBoxMaterial, m_warpEffects, () =>
            {
                m_isFleetWarping = false;
                onWarpComplete?.Invoke();
            });
        }
        else
        {
            m_isFleetWarping = false;
            onWarpComplete?.Invoke();
        }
    }

    // 함대 워프 중단
    public void StopFleetWarp()
    {
        // 함선별 효과 중단
        foreach (var warpEffect in m_warpEffects)
        {
            if (warpEffect != null)
                warpEffect.StopWarp();
        }

        // 글로벌 효과 중단
        var pp = WarpPostProcessing.Instance;
        if (pp != null)
            pp.StopWarpSequence();

        m_isFleetWarping = false;
    }

    // WarpEffectShip 컴포넌트 확보
    private void EnsureWarpEffects()
    {
        m_warpEffects.Clear();

        foreach (var ship in m_ships)
        {
            if (ship == null) continue;

            WarpEffectShip warpEffect = ship.GetComponent<WarpEffectShip>();
            if (warpEffect == null)
            {
                warpEffect = ship.gameObject.AddComponent<WarpEffectShip>();
                warpEffect.InitializeWarpEffect();
            }

            m_warpEffects.Add(warpEffect);
        }
    }

    public bool IsFleetWarping => m_isFleetWarping;
    #endregion

}