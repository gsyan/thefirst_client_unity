// 구식 그레이드 기반 함대 구성 전체 주석처리 — 프리셋 기반으로 재작성 전까지 비활성화
#if false
using System.Collections.Generic;
using UnityEngine;

// 튜토리얼 오프닝 시네마틱(관전 전투 연출) 전용 — 플레이어 개입 없는 NPC 함대 즉석 생성
public static class TutorialCinematicController
{
    // T1~T14 함체 프리팹의 빔/미사일/격납고 슬롯 개수 상한 (datatable_module.csv 기준, moduleLevel=1 슬롯 구성)
    // DataTableZoneEditor.k_slotCap과 동일 데이터 — 그쪽은 에디터 전용 코드라 런타임에서 재사용 불가해 복제
    private static readonly Dictionary<int, (int beam, int missile, int hangar)> k_slotCap = new Dictionary<int, (int, int, int)>
    {
        { 1,  (1, 1, 1) }, { 2,  (2, 1, 1) }, { 3,  (2, 1, 1) }, { 4,  (2, 2, 1) },
        { 5,  (2, 2, 2) }, { 6,  (2, 2, 2) }, { 7,  (3, 2, 2) }, { 8,  (3, 3, 2) },
        { 9,  (3, 3, 3) }, { 10, (4, 3, 3) }, { 11, (4, 4, 3) }, { 12, (4, 4, 4) },
        { 13, (5, 4, 4) }, { 14, (5, 5, 4) },
    };

    // gradeLevel 함체 등급(1~14) 함선 1척 정보 생성 — 빔/미사일/격납고 슬롯을 풀로 채움
    public static ShipInfo BuildCinematicShipInfo(int gradeLevel, int positionIndex)
    {
        (int beam, int missile, int hangar) cap;
        if (k_slotCap.TryGetValue(gradeLevel, out cap) == false)
            cap = k_slotCap[14];

        // moduleLevel은 서브타입(등급) 안에서의 세부 레벨(1부터 시작) — 등급 자체는 moduleSubType(t{gradeLevel})으로 이미 표현됨
        const int moduleLevel = 1;

        List<ModuleInfo> beams = new List<ModuleInfo>();
        for (int b = 0; b < cap.beam; b++)
            beams.Add(new ModuleInfo { moduleType = EModuleType.beam, moduleSubType = ParseSubType($"beam_t{gradeLevel}_m1"), moduleLevel = moduleLevel, bodyIndex = 0, slotIndex = b });

        List<ModuleInfo> missiles = new List<ModuleInfo>();
        for (int m = 0; m < cap.missile; m++)
            missiles.Add(new ModuleInfo { moduleType = EModuleType.missile, moduleSubType = ParseSubType($"missile_t{gradeLevel}_m1"), moduleLevel = moduleLevel, bodyIndex = 0, slotIndex = m });

        List<ModuleInfo> hangars = new List<ModuleInfo>();
        for (int h = 0; h < cap.hangar; h++)
            hangars.Add(new ModuleInfo { moduleType = EModuleType.hangar, moduleSubType = ParseSubType($"hangar_t{gradeLevel}_m1"), moduleLevel = moduleLevel, bodyIndex = 0, slotIndex = h });

        ModuleBodyInfo body = new ModuleBodyInfo
        {
            moduleType = EModuleType.hull,
            moduleSubType = ParseSubType($"body_t{gradeLevel}_m1"),
            moduleLevel = moduleLevel,
            bodyIndex = 0,
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
            bodies = new List<ModuleBodyInfo> { body },
        };
    }

    // gradeLevel 등급 함선 shipCount척으로 구성된 함대 정보 생성 (positionIndex 0 = 기함)
    public static FleetInfo BuildCinematicFleetInfo(string fleetName, int gradeLevel, int shipCount)
    {
        List<ShipInfo> ships = new List<ShipInfo>();
        for (int i = 0; i < shipCount; i++)
            ships.Add(BuildCinematicShipInfo(gradeLevel, i));

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
            ships.Add(BuildCinematicShipInfo(k_siegfriedGradeLevels[i], i));

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
            ships.Add(BuildCinematicShipInfo(shipGradeLevels[i], i));

        return new FleetInfo { fleetName = fleetName, ships = ships };
    }

    // 적 웨이브 함대 1개 스폰 — 실제 게임의 ObjectManager.SpawnWave()/GetEnemySpawnPositionFromWave()와 동일한 방식
    // 기함 그레이드(shipGradeLevels[0])에 맞는 grade 그룹의 프리셋을 사용 — 등급이 높을수록(함선이 커질수록) distance가 넓은 세트로 자동 전환됨
    public static SpaceFleet SpawnEnemyWaveFleet(int[] shipGradeLevels, int positionIndex)
    {
        SpaceFleet siegfriedFleet = ObjectManager.Instance.GetMyFleet();
        if (siegfriedFleet == null) return null;

        int flagshipGrade = shipGradeLevels.Length > 0 ? shipGradeLevels[0] : 1;
        FleetPositionPreset preset = DataManager.Instance.m_dataTableZone.GetFleetPosition(flagshipGrade, positionIndex);
        if (preset == null) return null;

        // 내 함대를 중심으로 한 구 표면 위에 배치 — distance가 항상 실제 거리(구의 반지름)가 되도록 y를 눌러 깎지 않음
        Vector3 basePos = siegfriedFleet.transform.position;
        Vector3 localDir = Quaternion.Euler(preset.rotX, preset.rotY, preset.rotZ) * Vector3.forward;
        Vector3 worldDir = siegfriedFleet.transform.TransformDirection(localDir);
        Vector3 spawnPos = basePos + worldDir * preset.distance;

        Vector3 dirToPlayer = basePos - spawnPos;
        Quaternion spawnRot = dirToPlayer != Vector3.zero ? Quaternion.LookRotation(dirToPlayer) : Quaternion.identity;

        FleetInfo fleetInfo = BuildWaveFleetInfo($"EnemyWave_{positionIndex}", shipGradeLevels);

        GameObject fleetObj = new GameObject($"CinematicFleet_{fleetInfo.fleetName}");
        fleetObj.transform.position = spawnPos;
        fleetObj.transform.rotation = spawnRot;

        SpaceFleet fleet = fleetObj.AddComponent<SpaceFleet>();
        fleet.InitializeSpaceFleet(fleetInfo, ETeam.TeamB, EFleetSource.fleet_source_cinematic, EUnitState.Move);
        ObjectManager.Instance.GetTeamFleets(ETeam.TeamB).Add(fleet);
        // TEMP DEBUG — 튜토리얼 적함대 잔존 버그 추적용, 확인 끝나면 제거
        Debug.Log($"[Tutorial][DEBUG] SpawnEnemyWaveFleet: {fleetObj.name} spawned, instanceID={fleet.GetInstanceID()}, TeamB count={ObjectManager.Instance.GetTeamFleets(ETeam.TeamB).Count}");

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

        // TEMP DEBUG — 튜토리얼 적함대 잔존 버그 추적용, 확인 끝나면 제거
        Debug.Log($"[Tutorial][DEBUG] DespawnCinematicFleet: {fleet.name} instanceID={fleet.GetInstanceID()} team={fleet.m_team}");

        ObjectManager.Instance.GetTeamFleets(fleet.m_team).Remove(fleet);
        Object.Destroy(fleet.gameObject);
    }

    private static EModuleSubType ParseSubType(string name)
    {
        return System.Enum.TryParse(name, out EModuleSubType result) ? result : EModuleSubType.none;
    }
}
#endif
