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

public class SpaceFleet : MonoBehaviour
{
    public FleetInfo m_fleetInfo;
    [SerializeField] public bool m_isEnemyFleet = false;
    public EFleetState m_fleetState = EFleetState.None;
    public EFormationType m_currentFormationType = EFormationType.formation_type_linear_horizontal;
    [SerializeField] public List<SpaceShip> m_ships = new List<SpaceShip>();

    // 수리 설정
    public ERepairThreshold m_repairThreshold = ERepairThreshold.Full;
    public ERepairConcurrency m_repairConcurrency = ERepairConcurrency.One;
    
    private void Start()
    {
        if (m_isEnemyFleet == false)
            StartCoroutine(AutoRepair());
    }

    public void InitializeSpaceFleet(FleetInfo fleetInfo, bool isEnemy = false, EFleetState fleetState = EFleetState.None)
    {
        m_fleetInfo = fleetInfo;
        m_isEnemyFleet = isEnemy;
        m_fleetState = fleetState;

        if (m_fleetInfo.ships != null && m_fleetInfo.ships.Count > 0)
        {
            for (int i = 0; i < m_fleetInfo.ships.Count; i++)
                CreateSpaceShipFromData(fleetInfo.ships[i]);

            UpdateShipFormation(m_fleetInfo.formation, false);
        }
        
        //if (isEnemy == false)
            SetFleetState(EFleetState.Battle);
    }
    // smoothSpawn: true면 기함 뒤에서 스폰 후 이동, false면 즉시 진형 위치에 배치
    public void CreateSpaceShipFromData(ShipInfo shipInfo, bool smoothSpawn = false)
    {
        GameObject shipGo = new GameObject($"{shipInfo.shipName}");
        SpaceShip spaceShip = shipGo.AddComponent<SpaceShip>();
        spaceShip.InitializeSpaceShip(this, shipInfo);
        AddShip(spaceShip, smoothSpawn);
    }
    public void AddShip(SpaceShip ship, bool placeInFormation = false)
    {
        if (ship == null) return;
        m_ships.Add(ship);
        ship.transform.SetParent(transform);
        ship.transform.localRotation = Quaternion.identity;

        if (placeInFormation)
        {
            // 기함 찾기
            SpaceShip flagship = m_ships.Find(s => s != null && s.m_shipInfo.positionIndex == 0);
            if (flagship != null)
            {
                // 새 함선을 기함 뒤쪽(z축 거리의 2배)에 배치
                float flagshipLength = flagship.CalculateShipBounds().size.z;
                Vector3 spawnPos = flagship.transform.localPosition + new Vector3(0, 0, -flagshipLength * 2f);
                ship.transform.localPosition = spawnPos;
            }
            else
            {
                // 기함이 없으면 원점 뒤쪽에 배치
                ship.transform.localPosition = new Vector3(0, 0, -20f);
            }

            // 모든 함선(기존 + 신규) 진형 재배치
            RefreshFormation();
        }
    }

    // 진형 재계획 (함선 추가/제거 시 호출)
    public void RefreshFormation()
    {
        // 이동 중인 함선들 중지
        foreach (var ship in m_ships)
        {
            if (ship != null)
                ship.StopFormationMovement();
        }

        // 진형 재계획 및 이동 시작
        UpdateShipFormationWithPlannedPath(m_currentFormationType);
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

        if (smooth)
        {
            // 새 시스템: 사전 경로 계획 후 이동
            UpdateShipFormationWithPlannedPath(formationType);
        }
        else
        {
            foreach (SpaceShip ship in m_ships)
            {
                if (ship != null)
                    ship.transform.localPosition = ship.CalculateShipPosition(formationType);
            }
        }
    }

    // 새 진형 이동 시스템: Hungarian Algorithm + 경로 계획
    private void UpdateShipFormationWithPlannedPath(EFormationType formationType)
    {
        // null이 아닌 함선만 필터링
        List<SpaceShip> validShips = m_ships.FindAll(s => s != null);
        if (validShips.Count == 0) return;

        // 경로 계획 생성
        var plannedPaths = FormationPathPlanner.PlanFormationChange(validShips, formationType);

        // 디버그 시각화 (에디터에서만)
        #if UNITY_EDITOR
        FormationPathPlanner.DebugDrawPaths(plannedPaths, 5f);
        #endif

        // 각 함선에 계획된 경로 전달
        foreach (var path in plannedPaths)
        {
            if (path.ship != null)
                path.ship.FollowPlannedPath(path);
        }
    }

    // 기존 방식 (레거시, 필요시 사용)
    private void UpdateShipFormationLegacy(EFormationType formationType)
    {
        List<SpaceShip> sortedShips = new List<SpaceShip>(m_ships);
        sortedShips.Sort((a, b) => a.m_shipInfo.positionIndex.CompareTo(b.m_shipInfo.positionIndex));

        for (int i = 0; i < sortedShips.Count; i++)
        {
            SpaceShip ship = sortedShips[i];
            if (ship == null) continue;
            float delay = i * 0.1f;
            StartCoroutine(DelayedFormationMove(ship, formationType, delay));
        }
    }

    private IEnumerator DelayedFormationMove(SpaceShip ship, EFormationType formationType, float delay)
    {
        if (delay > 0)
            yield return new WaitForSeconds(delay);
        ship.MoveToFormationPosition(formationType);
    }


    public void ChangeFormation(EFormationType newFormationType)
    {
        if (m_isEnemyFleet) return;

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
            if (m_isEnemyFleet == true)
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
        SetFleetState(EFleetState.Battle);

        if (m_isEnemyFleet == false)
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
            float totalRepair = fleetStats.repair_power;
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

    virtual protected void OnFleetDestroyed()
    {
        StopAllCoroutines();

        if (m_isEnemyFleet == true)
        {
            ObjectManager.Instance.RemoveEnemyFleet(this);
        }

        // gameObject.SetActive(false);
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
            totalStats.attack_power += shipStats.attack_power;
            totalStats.health_power += shipStats.health_power;
            totalStats.speed_power += shipStats.speed_power;
            totalStats.cargo_capacity += shipStats.cargo_capacity;
            totalStats.repair_power += shipStats.repair_power;
            totalStats.totalWeapons += shipStats.totalWeapons;
            totalStats.totalEngines += shipStats.totalEngines;
        }
        // 일단 평균
        totalStats.speed_power /= shipCount;

        return totalStats;
    }

    public int GetAverageShipLevel()
    {
        if (m_ships.Count == 0) return 0;

        int totalLevel = 0;
        foreach (SpaceShip ship in m_ships)
        {
            if (ship == null) continue;
            totalLevel += ship.GetAverageModuleLevel();
        }
        return totalLevel / m_ships.Count;
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

        // 글로벌 효과 (Skybox, PP) - 함대 단위로 1회만 호출
        var pp = WarpPostProcessing.Instance;
        if (pp != null)
        {
            pp.StartWarpSequence(skyBoxMaterial, () =>
            {
                m_isFleetWarping = false;
                onWarpComplete?.Invoke();
            });
        }

        // 함선별 개별 효과 (엔진 글로우, 스피드라인)
        foreach (var warpEffect in m_warpEffects)
        {
            if (warpEffect != null)
                warpEffect.StartWarp();
        }

        // PP가 없는 경우 즉시 완료
        if (pp == null)
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