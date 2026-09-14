using System.Collections.Generic;
using UnityEngine;

// 튜토리얼 오프닝 시네마틱(관전 전투 연출) 전용 — 플레이어 개입 없는 NPC 함대 즉석 생성
public static class TutorialCinematicController
{
    // gradeLevel(함체 티어) 함선 1척 정보 생성 — 그 티어의 함체 데이터(실드/요격체 없는 기본형)를 찾아 빔/미사일/격납고 슬롯을 전부 같은 티어 모듈로 채움
    public static ShipInfo BuildCinematicShipInfo(int gradeLevel, int positionIndex)
    {
        DataTableModule moduleTable = DataManager.Instance.m_dataTableModule;
        ModuleData hullData = FindBasicHullDataAtTier(moduleTable, gradeLevel);
        if (hullData == null) return null;

        int[] maxSlots = FleetComposition.ParseMaxSlotsFromHullSubType(hullData.moduleSubType); // [beam, missile, hangar, shield, interceptor]

        List<ModuleInfo> beams    = BuildFullModuleList(EModuleType.beam,    moduleTable.BeamModules,    gradeLevel, maxSlots[0]);
        List<ModuleInfo> missiles = BuildFullModuleList(EModuleType.missile, moduleTable.MissileModules, gradeLevel, maxSlots[1]);
        List<ModuleInfo> hangars  = BuildFullModuleList(EModuleType.hangar,  moduleTable.HangarModules,  gradeLevel, maxSlots[2]);

        ModuleHullInfo hull = new ModuleHullInfo
        {
            moduleType = EModuleType.hull,
            moduleSubType = hullData.moduleSubType,
            beams = beams,
            missiles = missiles,
            hangars = hangars,
        };

        return new ShipInfo
        {
            // id를 안 채우면 전부 기본값 0이 되어 SpaceFleet.FindShip(id)가 함대 내 모든 함선을 기함(첫 매치)으로 착각함
            // — 서버 등록 함선의 양수 id와 절대 겹치지 않도록 음수로 지정, 같은 함대 안에서 positionIndex 기준 유일성만 보장하면 충분
            id = -(positionIndex + 1),
            shipName = $"CinematicShip_{positionIndex}",
            positionIndex = positionIndex,
            hullSubType = hullData.moduleSubType,
            hulls = new List<ModuleHullInfo> { hull },
        };
    }

    // gradeLevel 티어의 함체 데이터 중 실드/요격체가 없는 기본형을 찾음 — 없으면 그 티어에서 찾은 첫 항목으로 대체
    private static ModuleData FindBasicHullDataAtTier(DataTableModule moduleTable, int gradeLevel)
    {
        List<ModuleData> hulls = moduleTable.HullModules.modules;
        ModuleData fallback = null;

        for (int i = 0; i < hulls.Count; i++)
        {
            ModuleData data = hulls[i];
            if (CommonUtility.ParseTier(data.moduleSubType) != gradeLevel) continue;
            if (fallback == null) fallback = data;

            int[] composition = CommonUtility.ParseHullSlotComposition(data.moduleSubType);
            if (composition[3] == 0 && composition[4] == 0) return data; // 실드/요격체 없는 기본형
        }

        return fallback;
    }

    // slotCount칸 전부를 gradeLevel 티어 모듈로 채움 — 정확히 그 티어 데이터가 없으면 그보다 낮은 티어 중 가장 높은 것으로 대체
    private static List<ModuleInfo> BuildFullModuleList(EModuleType moduleType, ModuleDataList categoryModules, int gradeLevel, int slotCount)
    {
        List<ModuleInfo> list = new List<ModuleInfo>();
        if (slotCount <= 0) return list;

        ModuleData data = FindClosestModuleDataAtOrBelowTier(categoryModules, gradeLevel);
        if (data == null) return list;

        for (int i = 0; i < slotCount; i++)
            list.Add(new ModuleInfo { moduleType = moduleType, moduleSubType = data.moduleSubType, slotIndex = i });

        return list;
    }

    private static ModuleData FindClosestModuleDataAtOrBelowTier(ModuleDataList categoryModules, int gradeLevel)
    {
        ModuleData best = null;
        int bestTier = 0;
        for (int i = 0; i < categoryModules.modules.Count; i++)
        {
            ModuleData data = categoryModules.modules[i];
            int tier = CommonUtility.ParseTier(data.moduleSubType);
            if (tier <= gradeLevel && tier > bestTier)
            {
                bestTier = tier;
                best = data;
            }
        }
        return best;
    }

    // gradeLevel 등급 함선 shipCount척으로 구성된 함대 정보 생성 (positionIndex 0 = 기함)
    public static FleetInfo BuildCinematicFleetInfo(string fleetName, int gradeLevel, int shipCount)
    {
        List<ShipInfo> ships = new List<ShipInfo>();
        for (int i = 0; i < shipCount; i++)
        {
            ShipInfo ship = BuildCinematicShipInfo(gradeLevel, i);
            if (ship != null) ships.Add(ship);
        }

        return new FleetInfo { fleetName = fleetName, ships = ships };
    }

    // 지크프리트 기함 등급 — 이 등급의 모듈을 레벨업하려면 커맨더가 이 등급까지 서브타입 상한이 열려있어야 함
    // (ObjectManager.GrantTutorialCommanderLevel이 참조)
    public const int SIEGFRIED_FLAGSHIP_GRADE = 14;

    // 지크프리트 함대 초기 구성 — 기함 T14, 1번함 T8, 2번함 T6, 3번함 T4 (모두 슬롯 풀 오픈)
    // 4번함(유저가 튜토리얼 중 직접 추가)은 이 함대에 포함하지 않음 — ExecuteAddShipTutorialOnly가 별도 추가
    private static readonly int[] k_siegfriedGradeLevels = { SIEGFRIED_FLAGSHIP_GRADE, 8, 6, 4 };

    public static FleetInfo BuildSiegfriedFleetInfo(string fleetName)
    {
        List<ShipInfo> ships = new List<ShipInfo>();
        for (int i = 0; i < k_siegfriedGradeLevels.Length; i++)
        {
            ShipInfo ship = BuildCinematicShipInfo(k_siegfriedGradeLevels[i], i);
            if (ship == null) continue;

            ship.isFront = true; // 지크프리트 함대는 전원 전방 배치
            ships.Add(ship);
        }

        return new FleetInfo { fleetName = fleetName, ships = ships };
    }

    // 탈출 함선 1척 스폰 — 지크프리트 기함 뒤쪽(후방)에서 등장, 이 함선이 이후 실제 유저 함대의 기함이 됨
    public static SpaceFleet SpawnEscapeFleet(SpaceFleet siegfriedFleet, int gradeLevel = 1)
    {
        if (siegfriedFleet == null) return null;
        SpaceShip flagship = siegfriedFleet.GetFlagship();
        if (flagship == null) return null;

        // 기함이 바라보는 방향의 반대(후방)에서 등장, 계속 그 방향으로 나아가면 자연스럽게 멀어짐
        Quaternion escapeRotation = flagship.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
        Vector3 escapePosition = flagship.transform.position + escapeRotation * Vector3.forward;

        FleetInfo fleetInfo = new FleetInfo { fleetName = "Escape Fleet", ships = new List<ShipInfo> { BuildCinematicShipInfo(gradeLevel, 0) } };
        return SpawnCinematicFleet(fleetInfo, ETeam.TeamA, escapePosition, escapeRotation);
    }

    // shipGradeLevels 배열(positionIndex 0=기함) 구성의 적 함대 정보 생성
    private static FleetInfo BuildWaveFleetInfo(string fleetName, int[] shipGradeLevels)
    {
        List<ShipInfo> ships = new List<ShipInfo>();
        for (int i = 0; i < shipGradeLevels.Length; i++)
        {
            ShipInfo ship = BuildCinematicShipInfo(shipGradeLevels[i], i);
            if (ship != null) ships.Add(ship);
        }

        return new FleetInfo { fleetName = fleetName, ships = ships };
    }

    // 적 웨이브 함대 1개 스폰 — basePos/forward/up은 이 인카운터 시작 시점에 한 번만 캡처된 고정 기준(호출부 TutorialBattleCinematic이 들고 있음).
    // 전투 중 내 함대가 적을 조준하며 계속 회전하는데, 매 스폰마다 siegfriedFleet.transform.forward를 새로 읽으면 나중에 스폰되는
    // 함대일수록 기준 방향이 달라져서 이전에 스폰된 함대와 위치가 겹쳐버림 — 그래서 실시간으로 다시 읽지 않고 고정값을 그대로 받아씀
    // TODO(3단계): 스폰 위치 산출이 임시 배치임 — 삭제된 DataTableZone.GetFleetPosition/FleetPositionPreset을 대체할
    // 신규 탐사 그리드 기준 위치 산출 로직으로 교체 필요. 지금은 내 함대 정면 반지름 고정 부채꼴로 좌우 번갈아 배치.
    public static SpaceFleet SpawnEnemyWaveFleet(int[] shipGradeLevels, int positionIndex, Vector3 basePos, Vector3 forward, Vector3 up)
    {
        SpaceFleet siegfriedFleet = ObjectManager.Instance.GetMyFleet();
        if (siegfriedFleet == null) return null;

        // UIPanelExplorationGrid.k_enemyEncounterDistance(실제 존 전투 조우 거리, 50f)와 동일 스케일로 맞춤 —
        // 이전엔 150f라 실제 게임보다 훨씬 멀리 스폰되고 있었음
        const float k_tempDistance = 50f;
        const float k_tempAngleStepDeg = 20f; // 자리 하나당 좌우로 벌어지는 각도 — 반지름은 고정이라 옆으로 갈수록 멀어지지 않음. 인접 자리 겹침 방지용 임시값(반지름 50 기준 약 17유닛 간격)

        // positionIndex(0,1,2,3,...) → 중앙 기준 좌우 대칭 각도로 매핑: 0=중앙, 1=+step(우), 2=-step(좌), 3=+2step(우), 4=-2step(좌) ...
        int sideMagnitude = (positionIndex + 1) / 2;
        float sign = (positionIndex % 2 == 1) ? 1f : -1f;
        float angleDeg = positionIndex == 0 ? 0f : sign * sideMagnitude * k_tempAngleStepDeg;

        Vector3 spawnDir = Quaternion.AngleAxis(angleDeg, up) * forward;
        Vector3 spawnPos = basePos + spawnDir * k_tempDistance;

        Vector3 dirToPlayer = basePos - spawnPos;
        Quaternion spawnRot = dirToPlayer != Vector3.zero ? Quaternion.LookRotation(dirToPlayer) : Quaternion.identity;

        FleetInfo fleetInfo = BuildWaveFleetInfo($"EnemyWave_{positionIndex}", shipGradeLevels);

        GameObject fleetObj = new GameObject($"CinematicFleet_{fleetInfo.fleetName}");
        fleetObj.transform.position = spawnPos;
        fleetObj.transform.rotation = spawnRot;

        SpaceFleet fleet = fleetObj.AddComponent<SpaceFleet>();
        fleet.InitializeSpaceFleet(fleetInfo, ETeam.TeamB, EFleetSource.fleet_source_cinematic, EUnitState.Move);
        ObjectManager.Instance.GetTeamFleets(ETeam.TeamB).Add(fleet);

        // 지크프리트 기함은 다른 함선이 모두 전멸하기 전까지 공격 대상에서 제외
        foreach (SpaceShip ship in fleet.m_ships)
        {
            if (ship != null)
                ship.m_targetingRule = ETargetingRule.FlagshipLast;
        }

        fleet.StartFleetWarpIn(() =>
        {
            // 실제 게임의 TryStartCombat()과 동일 — 적뿐 아니라 내 함대도 같이 전투 상태로 전환해야 교전이 실제로 진행됨
            siegfriedFleet.SetFleetState(EUnitState.BattleExploration);
            fleet.SetFleetState(EUnitState.BattleExploration);
            siegfriedFleet.StartCombat();
            fleet.StartCombat();
        });

        return fleet;
    }

    // 시네마틱 함대를 씬에 생성하고 지정된 팀 리스트에 등록
    public static SpaceFleet SpawnCinematicFleet(FleetInfo fleetInfo, ETeam team, Vector3 position, Quaternion rotation)
    {
        GameObject fleetObj = new GameObject($"CinematicFleet_{fleetInfo.fleetName}");
        fleetObj.transform.position = position;
        fleetObj.transform.rotation = rotation;

        SpaceFleet fleet = fleetObj.AddComponent<SpaceFleet>();
        fleet.InitializeSpaceFleet(fleetInfo, team, EFleetSource.fleet_source_cinematic, EUnitState.Idle);

        ObjectManager.Instance.GetTeamFleets(team).Add(fleet);
        return fleet;
    }

    // 시네마틱 함대 제거 (팀 리스트에서 해제 후 파괴)
    public static void DespawnCinematicFleet(SpaceFleet fleet)
    {
        if (fleet == null) return;

        ObjectManager.Instance.GetTeamFleets(fleet.m_team).Remove(fleet);
        Object.Destroy(fleet.gameObject);
    }
}
