// 셀별 적함대 절차적 생성 — (seed, row, col) 조합이면 항상 같은 결과(결정론적, 공략 공유 가능)
// 저장하지 않음: 그리드 진입 시 셀마다 즉석 계산 후 메모리 캐싱만 (Exploration_Grid_Implementation.md §7)
// 클라 전용 — 서버는 이 데이터를 쓰지 않음(셀 타입 기반 hasCombatCell 판정만 서버에 별도로 있음, ExplorationService.java)
//
// 티어합 분배: 이 셀 전체가 함체티어합(enemyHullTierSum) 하나의 예산을 공유하며, 이 예산을 다 쓸 때까지 함선이 계속 생성됨
// (종료 조건은 오직 "예산 소진"뿐 — 특정 함선의 티어가 낮다고 멈추지 않음). 함대는 포메이션 슬롯 수(k_maxShipsPerFleet)마다 자동으로 나뉘고,
// 새 함대가 시작될 때마다 그 함대의 1번 함선(기함)은 다시 enemyBaseHullTier를 목표로 삼음(남은 예산이 그보다 적으면 남은 예산 전부를 씀 —
// 이 경우 그 함대는 그 한 척으로 끝남). 그 함대의 2번 함선부터는 남은 예산에서 랜덤(1~min(남은예산,데이터 최대치,그 함대 자신의 기함 티어))
// 만큼 나눠 가짐 — "그 함대 자신의 기함 티어"로 캡을 거는 이유는, 예산 부족으로 기함이 enemyBaseHullTier보다 낮게 확정된 함대에서도
// 뒤 함선이 그 함대의 기함보다 세지지 않게 하기 위함.
// 모듈(빔/미사일/격납고)은 독립 예산 없이 그 함선 자신의 함체티어에 종속 — 슬롯 하나하나마다 enemyModulePlacementProbability로 장착 여부를,
// enemyModulePerformanceProbability로 [max(1,함체티어*이값) ~ 함체티어] 범위에서 모듈 티어를 굴림(1이면 항상 함체티어 그대로).
// (예산 기반으로 하면 슬롯 수가 다른 함체끼리 "모듈티어 롤 1회=슬롯 전부 무료 적용"이 되어 슬롯 많은 함체가 상대적으로 이득 보는 문제가 있었음 — 슬롯 단위로 바꿔서 해결)
using System.Collections.Generic;
using System.Linq;

public static class ExplorationEnemyFleetGenerator
{
    // 함대(웨이브) 1개의 최대 함선 수 — 포메이션 프리셋이 positionIndex 0~8(9개)만 정의함(FormationPresetGenerator.cs),
    // 플레이어 커맨더 최대 함선수(DataTableCommander, datatable_commander.csv)도 9로 고정되어 있어 이 값과 맞춤
    private const int k_maxShipsPerFleet = 9;

    private class BuildingShip
    {
        public ModuleData hull;
        public List<ModuleInfo> beams = new List<ModuleInfo>();
        public List<ModuleInfo> missiles = new List<ModuleInfo>();
        public List<ModuleInfo> hangars = new List<ModuleInfo>();
        public string shieldSubType = "";
        public string interceptorSubType = "";
    }

    // 셀 전체 함선을 한 번에 생성한 뒤 9척 단위로 잘라 여러 웨이브(함대)로 나눔 — 웨이브 0번만 워프인 트리거가 연결되어 있고
    // (전투 시스템 미구현), 나머지는 캐싱만 해두고 후속 전투 시스템에서 이어서 사용
    public static List<FleetInfo> GenerateWaves(ZoneConfig zoneConfig, int seed, int row, int col, DataTableModule moduleTable)
    {
        List<FleetInfo> waves = new List<FleetInfo>();

        List<ModuleData> hulls = moduleTable != null ? moduleTable.HullModules.modules : null;
        if (hulls == null || hulls.Count == 0 || zoneConfig == null || moduleTable == null) return waves;

        List<BuildingShip> allShips = GenerateAllShips(seed, row, col, hulls, zoneConfig, moduleTable);

        for (int i = 0; i < allShips.Count; i += k_maxShipsPerFleet)
        {
            int count = System.Math.Min(k_maxShipsPerFleet, allShips.Count - i);
            waves.Add(FinalizeWave(allShips.GetRange(i, count)));
        }

        return waves;
    }

    private static List<BuildingShip> GenerateAllShips(int seed, int row, int col,
        List<ModuleData> hulls, ZoneConfig zoneConfig, DataTableModule moduleTable)
    {
        CrossPlatformRandom random = new CrossPlatformRandom(seed ^ (row * 73856093) ^ (col * 19349663) ^ 0x5EED);

        int maxHullTier = ResolveMaxAvailableTier(hulls);

        List<BuildingShip> ships = new List<BuildingShip>();
        int remainingHullSum = zoneConfig.enemyHullTierSum;
        int shipsInCurrentFleet = 0;
        int currentFleetFlagshipTier = 0;

        while (remainingHullSum > 0)
        {
            int hullTier;
            if (shipsInCurrentFleet == 0)
            {
                // 새 함대 시작 — 그 함대의 기함 티어(남은 예산을 넘을 수 없음)
                hullTier = System.Math.Min(System.Math.Min(zoneConfig.enemyBaseHullTier, maxHullTier), remainingHullSum);
                currentFleetFlagshipTier = hullTier;
            }
            else
            {
                // 뒤 함선이 그 함대의 기함보다 세지지 않도록 currentFleetFlagshipTier로 상한을 씌움
                int hullRollMax = System.Math.Min(System.Math.Min(remainingHullSum, maxHullTier), currentFleetFlagshipTier);
                hullTier = random.Next(1, hullRollMax + 1);
            }

            BuildingShip ship = BuildShipAtTier(hullTier, hulls, zoneConfig, moduleTable, random);
            if (ship == null) break; // 해당 티어 함체 데이터 없음(설정 오류) — 생성 중단, 지금까지 만든 함선만 반환

            ships.Add(ship);
            remainingHullSum -= hullTier;
            shipsInCurrentFleet++;
            if (shipsInCurrentFleet >= k_maxShipsPerFleet) shipsInCurrentFleet = 0; // 다음 함선은 새 함대 시작
        }

        return ships;
    }

    // 랜덤 롤 상한을 실제 데이터에 있는 최대 티어로 clamp — 티어합 설정이 데이터 범위(현재 1~14)를 넘어도
    // 존재하지 않는 티어를 요청해 웨이브가 중간에 끊기는 일이 없도록 방지
    private static int ResolveMaxAvailableTier(List<ModuleData> list)
    {
        int max = 1;
        for (int i = 0; i < list.Count; i++)
        {
            int tier = CommonUtility.ParseTier(list[i].moduleSubType);
            if (tier > max) max = tier;
        }
        return max;
    }

    private static BuildingShip BuildShipAtTier(int hullTier, List<ModuleData> hulls, ZoneConfig zoneConfig, DataTableModule moduleTable, CrossPlatformRandom random)
    {
        List<ModuleData> candidates = hulls.FindAll(h => CommonUtility.ParseTier(h.moduleSubType) == hullTier);
        if (candidates.Count == 0) return null;

        ModuleData chosenHull = ResolveHullByShieldProbability(candidates, zoneConfig.enemyShieldProbability, random);
        return NewBuildingShip(chosenHull, hullTier, moduleTable, zoneConfig, random);
    }

    // 후보 중 gen2(실드형)가 있으면 enemyShieldProbability 확률로 선택, 없으면(현재 CSV 기준 tier 1~5) gen1로 폴백.
    // CrossPlatformRandom이 정수 전용 API라 확률을 10000분율 정수로 스케일링해서 비교
    private static ModuleData ResolveHullByShieldProbability(List<ModuleData> candidates, float shieldProbability, CrossPlatformRandom random)
    {
        ModuleData gen1 = candidates.Find(h => CommonUtility.ParseGen(h.moduleSubType) == 1);
        ModuleData gen2 = candidates.Find(h => CommonUtility.ParseGen(h.moduleSubType) == 2);
        if (gen2 == null) return gen1 != null ? gen1 : candidates[0];
        if (gen1 == null) return gen2;

        int threshold = UnityEngine.Mathf.RoundToInt(shieldProbability * 10000f);
        return random.Next(10000) < threshold ? gen2 : gen1;
    }

    private static BuildingShip NewBuildingShip(ModuleData hull, int hullTier, DataTableModule moduleTable, ZoneConfig zoneConfig, CrossPlatformRandom random)
    {
        BuildingShip ship = new BuildingShip();
        ship.hull = hull;
        int[] maxSlots = FleetComposition.ParseMaxSlotsFromHullSubType(hull.moduleSubType); // [beam, missile, hangar, shield, interceptor]

        FillSlotsBySlotChance(ship.beams, EModuleType.beam, moduleTable.BeamModules, hullTier, maxSlots[0], zoneConfig, random);
        FillSlotsBySlotChance(ship.missiles, EModuleType.missile, moduleTable.MissileModules, hullTier, maxSlots[1], zoneConfig, random);
        FillSlotsBySlotChance(ship.hangars, EModuleType.hangar, moduleTable.HangarModules, hullTier, maxSlots[2], zoneConfig, random);

        // 배치확률이 낮으면 슬롯 전부가 미장착으로 굴러 공격수단이 하나도 없는 함선이 나올 수 있음 —
        // 최소한의 전투력 보장을 위해 그럴 땐 빔 슬롯 0번만은 배치확률 체크 없이 강제 장착(티어는 enemyModulePerformanceProbability를 그대로 적용)
        if (ship.beams.Count == 0 && ship.missiles.Count == 0 && ship.hangars.Count == 0 && maxSlots[0] > 0)
            TryEquipModuleAtSlot(ship.beams, EModuleType.beam, moduleTable.BeamModules, hullTier, 0, zoneConfig, random);

        // 실드는 함체 gen 선택으로 이미 유무가 결정됨(슬롯 있으면 항상 장착) — 인터셉터는 이번 개편 범위 밖, 기존 0/1 스위치 그대로 유지
        ship.shieldSubType = maxSlots[3] > 0 ? "shield_1_1" : "";
        ship.interceptorSubType = maxSlots[4] > 0 && zoneConfig.enemyInterceptorEquipSlots > 0 ? "interceptor_1_1" : "";
        return ship;
    }

    // 슬롯 하나하나마다: enemyModulePlacementProbability로 장착 여부를, 장착이 확정되면 enemyModulePerformanceProbability로
    // [max(1, 함체티어*이값) ~ 함체티어] 범위에서 모듈 티어를 굴려 그 카테고리 데이터를 찾아 채움(모듈은 그 함선 자신의 함체티어에 종속, 별도 예산 없음)
    private static void FillSlotsBySlotChance(List<ModuleInfo> list, EModuleType type, ModuleDataList categoryModules, int hullTier, int slotCount, ZoneConfig zoneConfig, CrossPlatformRandom random)
    {
        int placementThreshold = UnityEngine.Mathf.RoundToInt(zoneConfig.enemyModulePlacementProbability * 10000f);

        for (int i = 0; i < slotCount; i++)
        {
            if (random.Next(10000) >= placementThreshold) continue; // 이 슬롯은 미장착으로 확정
            TryEquipModuleAtSlot(list, type, categoryModules, hullTier, i, zoneConfig, random);
        }
    }

    // enemyModulePerformanceProbability로 [max(1, 함체티어*이값) ~ 함체티어] 범위에서 모듈 티어를 굴려 슬롯 하나를 채움(배치확률 체크 없이 바로 장착 시도)
    private static void TryEquipModuleAtSlot(List<ModuleInfo> list, EModuleType type, ModuleDataList categoryModules, int hullTier, int slotIndex, ZoneConfig zoneConfig, CrossPlatformRandom random)
    {
        int lowerTier = System.Math.Max(1, UnityEngine.Mathf.RoundToInt(hullTier * zoneConfig.enemyModulePerformanceProbability));
        int tier = random.Next(lowerTier, hullTier + 1);
        ModuleData data = categoryModules.Find(d => CommonUtility.ParseTier(d.moduleSubType) == tier);
        if (data == null) return; // 해당 티어 모듈 데이터 없음 — 이 슬롯만 자연스럽게 빈 채로 스킵

        list.Add(new ModuleInfo { moduleType = type, moduleSubType = data.moduleSubType, slotIndex = slotIndex });
    }

    // 장착 모듈 개수 내림차순으로 정렬해 절반은 전방/절반은 후방 — 반드시 안정 정렬(OrderByDescending)만 사용할 것(List.Sort는 불안정 정렬이라 서버와 어긋날 수 있음)
    private static FleetInfo FinalizeWave(List<BuildingShip> ships)
    {
        FleetInfo fleetInfo = new FleetInfo { ships = new List<ShipInfo>() };

        // OrderByDescending은 안정 정렬(동률 시 삽입 순서 보존) 보장 — 서버 List.sort와 동일하게 유지
        List<BuildingShip> order = ships.OrderByDescending(EquippedCount).ToList();

        for (int i = 0; i < order.Count; i++)
        {
            BuildingShip ship = order[i];
            bool isFront = i < (order.Count + 1) / 2;

            ModuleHullInfo modules = new ModuleHullInfo
            {
                beams = ship.beams,
                missiles = ship.missiles,
                hangars = ship.hangars,
                shieldModuleSubType = ship.shieldSubType,
                interceptorModuleSubType = ship.interceptorSubType,
            };

            fleetInfo.ships.Add(new ShipInfo
            {
                hullSubType = ship.hull.moduleSubType,
                isFront = isFront,
                hulls = new List<ModuleHullInfo> { modules },
            });
        }

        return fleetInfo;
    }

    private static int EquippedCount(BuildingShip ship)
    {
        return ship.beams.Count + ship.missiles.Count + ship.hangars.Count;
    }
}
