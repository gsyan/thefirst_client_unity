// 셀별 적함대 절차적 생성 — (seed, row, col, fleetIndex) 조합이면 항상 같은 결과(결정론적, 공략 공유 가능)
// 저장하지 않음: 그리드 진입 시 셀마다 즉석 계산 후 메모리 캐싱만 (Exploration_Grid_Implementation.md §7)
// 서버 대응 구현: com.bk.sbs.util.ZoneEnemyFleetGenerator.java — 두 파일은 항상 함께 수정할 것
//
// 예산 소진 순서: (1) 함체(bodyCost, 함선마다 편차 새로 굴림) → (2) 남은 함대 예산으로 빔/미사일/격납고 라운드로빈 구매
// → (3) 함선수 상한/예산 부족으로 루프가 끝나고도 남은 애매한 잔액을 1번 함선부터 다시 훑으며 흡수
using System.Collections.Generic;
using System.Linq;

public static class ExplorationEnemyFleetGenerator
{
    // 함선 하나를 만드는 동안 계속 누적되는 임시 상태 — 라운드로빈/잔액흡수 단계에서 공유
    private class BuildingShip
    {
        public ShipPresetData preset;
        public int bodyCost;
        public int defaultModuleCost; // 기본 로드아웃(빔1 등) 정가 합 — bodyCost와 별개로 예산에서 차감됨
        public List<ModuleInfo> beams = new List<ModuleInfo>();
        public List<ModuleInfo> missiles = new List<ModuleInfo>();
        public List<ModuleInfo> hangars = new List<ModuleInfo>();
        public int[] maxSlots; // [beam, missile, hangar, shield, interceptor]
        public int beamTarget;
        public int missileTarget;
        public int hangarTarget;
        public string shieldSubType = "";
        public string interceptorSubType = "";
    }

    // 존별로 설정된 순차 웨이브(fleets) 전체 생성 — 웨이브 0번만 워프인 트리거가 연결되어 있고(전투 시스템 미구현),
    // 나머지는 캐싱만 해두고 후속 전투 시스템에서 이어서 사용
    public static List<FleetInfo> GenerateWaves(ZoneConfig zoneConfig, int seed, int row, int col, DataTableShipPreset presetTable, DataTableModule moduleTable)
    {
        List<FleetInfo> waves = new List<FleetInfo>();

        List<ShipPresetData> presets = presetTable != null ? presetTable.GetShipPresetDataList() : null;
        if (presets == null || presets.Count == 0 || zoneConfig == null || moduleTable == null) return waves;

        for (int fleetIndex = 0; fleetIndex < zoneConfig.enemyFleetsPerCell; fleetIndex++)
            waves.Add(GenerateOneWave(seed, row, col, fleetIndex, presets, zoneConfig, moduleTable));

        return waves;
    }

    // 구식 DataTableZoneEditor.GetBlockMaxTier와 동일 공식 — (row,col,fleetIndex,shipIndex) 결정론적, deviation만큼 costCap을 무작위로 낮춤
    // shipIndex를 시드에 포함시켜 함선마다 편차가 다시 굴려지게 함 — 같은 함대예산이라도 판마다 척수/구성이 달라짐
    private static int ResolveCostCap(int seed, int row, int col, int fleetIndex, int shipIndex, int maxCost, int deviation)
    {
        if (deviation <= 0) return maxCost;
        CrossPlatformRandom random = new CrossPlatformRandom(seed ^ (row * 73856093) ^ (col * 19349663) ^ (fleetIndex * 83492791) ^ (shipIndex * 668265261));
        return System.Math.Max(1, maxCost - random.Next(0, deviation + 1));
    }

    // 순수 바디 설치비(모듈 미포함) — prefabName(예: body_t1_m111)에 대응하는 DataTableModule 원본 statPoint
    private static int ResolveBodyCost(ShipPresetData preset, DataTableModule moduleTable)
    {
        if (string.IsNullOrEmpty(preset.prefabName)) return 0;
        if (System.Enum.TryParse(preset.prefabName, out EModuleSubType bodySubType) == false) return 0;
        ModuleData data = moduleTable.GetModuleDataFromTable(bodySubType);
        return data != null ? data.statPoint : 0;
    }

    // presetData.statAllocation에서 비어있지 않은 슬롯(현재는 beam slot0=beam_t1)만 추출 — FleetComposition.BuildDefaultModules와 동일 규칙
    private static List<ModuleInfo> BuildDefaultModules(ShipPresetData preset)
    {
        List<ModuleInfo> result = new List<ModuleInfo>();
        ShipStatAllocation alloc = preset.statAllocation;
        if (alloc == null) return result;

        AppendDefaultModules(result, EModuleType.beam, alloc.beamModuleSubType);
        AppendDefaultModules(result, EModuleType.missile, alloc.missileModuleSubType);
        AppendDefaultModules(result, EModuleType.hangar, alloc.hangarModuleSubType);
        return result;
    }

    private static void AppendDefaultModules(List<ModuleInfo> target, EModuleType moduleType, string[] subTypeArray)
    {
        if (subTypeArray == null) return;
        for (int i = 0; i < subTypeArray.Length; i++)
        {
            if (string.IsNullOrEmpty(subTypeArray[i])) continue;
            if (System.Enum.TryParse(subTypeArray[i], out EModuleSubType subType) == false) continue;
            target.Add(new ModuleInfo { moduleType = moduleType, moduleSubType = subType, slotIndex = i });
        }
    }

    private static int SumDefaultModuleCost(List<ModuleInfo> defaultModules, DataTableModule moduleTable)
    {
        int sum = 0;
        for (int i = 0; i < defaultModules.Count; i++)
        {
            ModuleData data = moduleTable.GetModuleDataFromTable(defaultModules[i].moduleSubType);
            sum += data != null ? data.statPoint : 0;
        }
        return sum;
    }

    // 프리셋들 중 (bodyCost + 기본모듈 비용) 최솟값 — 함선 하나를 만드는 데 최소로 필요한 예산. 하드코딩하지 않고 데이터에서 매번 계산
    private static int ResolveMinShipCost(List<ShipPresetData> presets, DataTableModule moduleTable)
    {
        int minCost = int.MaxValue;
        for (int i = 0; i < presets.Count; i++)
        {
            int bodyCost = ResolveBodyCost(presets[i], moduleTable);
            int cost = bodyCost + SumDefaultModuleCost(BuildDefaultModules(presets[i]), moduleTable);
            if (cost < minCost) minCost = cost;
        }
        return minCost == int.MaxValue ? 0 : minCost;
    }

    private static FleetInfo GenerateOneWave(int seed, int row, int col, int fleetIndex,
        List<ShipPresetData> presets, ZoneConfig zoneConfig, DataTableModule moduleTable)
    {
        CrossPlatformRandom random = new CrossPlatformRandom(seed ^ (row * 73856093) ^ (col * 19349663) ^ (fleetIndex * 83492791) ^ 0x5EED);
        int minShipCost = ResolveMinShipCost(presets, moduleTable);

        List<BuildingShip> ships = new List<BuildingShip>();
        int remaining = zoneConfig.enemyBudget;

        // 1) 메인 루프: 함선 단위로 순차 생성, 편차는 함선마다 새로 굴림
        int shipIndex = 0;
        while (remaining >= minShipCost && minShipCost > 0 && ships.Count < zoneConfig.enemyMaxShipsPerFleet - 1)
        {
            int perShipCap = System.Math.Min(remaining, ResolveCostCap(seed, row, col, fleetIndex, shipIndex, zoneConfig.enemyMaxCostOfOneShip, zoneConfig.enemyDeviation));
            BuildingShip ship = BuildOneShip(presets, perShipCap, zoneConfig, moduleTable, random);
            if (ship == null) break;

            int spent = ship.bodyCost + ship.defaultModuleCost;
            int shipBudget = perShipCap - spent;
            spent += FillRoundRobin(ship, shipBudget, moduleTable, random);

            remaining -= spent;
            ships.Add(ship);
            shipIndex++;
        }

        // 2) 함선수 상한 때문에 루프가 끝났고 예산이 남았으면, 남은 예산에 가장 가깝게 맞는(낭비 최소) 함체로 마지막 한 척 확정
        if (remaining >= minShipCost && minShipCost > 0 && ships.Count < zoneConfig.enemyMaxShipsPerFleet)
        {
            BuildingShip lastShip = BuildBestFitShip(presets, remaining, zoneConfig, moduleTable);
            if (lastShip != null)
            {
                int spent = lastShip.bodyCost + lastShip.defaultModuleCost;
                int shipBudget = remaining - spent;
                spent += FillRoundRobin(lastShip, shipBudget, moduleTable, random);
                remaining -= spent;
                ships.Add(lastShip);
            }
        }

        if (ships.Count == 0 && presets.Count > 0)
            ships.Add(NewBuildingShip(presets[0], zoneConfig, moduleTable)); // 폴백 — 구식 "grade=1" 폴백과 동일 의도

        // 3) 잔액 흡수 — 1번 함선부터 순서대로 훑으며 남은 예산을 라운드로빈으로 마저 채움
        remaining = AbsorbLeftover(ships, remaining, moduleTable, random);

        return FinalizeWave(ships);
    }

    // 예산 상한(cap) 안에서 함체를 랜덤 선택 — 함체가 여러 등급이면 항상 제일 비싼 것만 고르지 않도록 랜덤 어포더블 방식 유지
    private static BuildingShip BuildOneShip(List<ShipPresetData> presets, int cap, ZoneConfig zoneConfig, DataTableModule moduleTable, CrossPlatformRandom random)
    {
        List<ShipPresetData> affordable = new List<ShipPresetData>();
        for (int i = 0; i < presets.Count; i++)
        {
            int bodyCost = ResolveBodyCost(presets[i], moduleTable);
            int minCostForPreset = bodyCost + SumDefaultModuleCost(BuildDefaultModules(presets[i]), moduleTable);
            if (bodyCost > 0 && minCostForPreset <= cap)
                affordable.Add(presets[i]);
        }
        if (affordable.Count == 0) return null;

        ShipPresetData chosen = affordable[random.Next(affordable.Count)];
        return NewBuildingShip(chosen, zoneConfig, moduleTable);
    }

    // 남은 예산에 가장 가깝게 맞는(낭비 최소) 함체 — 마지막 한 척 확정용, 의도적으로 그리디
    private static BuildingShip BuildBestFitShip(List<ShipPresetData> presets, int cap, ZoneConfig zoneConfig, DataTableModule moduleTable)
    {
        ShipPresetData bestFit = null;
        int bestFitBodyCost = 0;
        for (int i = 0; i < presets.Count; i++)
        {
            int bodyCost = ResolveBodyCost(presets[i], moduleTable);
            int minCostForPreset = bodyCost + SumDefaultModuleCost(BuildDefaultModules(presets[i]), moduleTable);
            if (bodyCost > 0 && minCostForPreset <= cap && (bestFit == null || bodyCost > bestFitBodyCost))
            {
                bestFit = presets[i];
                bestFitBodyCost = bodyCost;
            }
        }
        return bestFit == null ? null : NewBuildingShip(bestFit, zoneConfig, moduleTable);
    }

    private static BuildingShip NewBuildingShip(ShipPresetData preset, ZoneConfig zoneConfig, DataTableModule moduleTable)
    {
        BuildingShip ship = new BuildingShip();
        ship.preset = preset;
        ship.bodyCost = ResolveBodyCost(preset, moduleTable);
        ship.maxSlots = FleetComposition.ParseMaxSlotsFromPresetId(preset.presetId);

        List<ModuleInfo> defaultModules = BuildDefaultModules(preset);
        ship.defaultModuleCost = SumDefaultModuleCost(defaultModules, moduleTable);
        for (int i = 0; i < defaultModules.Count; i++)
            AddToCategory(ship, defaultModules[i].moduleType, defaultModules[i]);

        ship.beamTarget    = System.Math.Min(zoneConfig.enemyBeamEquipSlots, ship.maxSlots[0]);
        ship.missileTarget = System.Math.Min(zoneConfig.enemyMissileEquipSlots, ship.maxSlots[1]);
        ship.hangarTarget  = System.Math.Min(zoneConfig.enemyHangarEquipSlots, ship.maxSlots[2]);

        // 실드/인터셉터 — 슬롯 1개뿐이라 "장착 여부"만 존재. 클라이언트가 실제로 소비(스탯 반영/스폰)하는 로직은 아직 없음 — 후속 작업
        ship.shieldSubType = ship.maxSlots[3] > 0 && zoneConfig.enemyShieldEquipSlots > 0 ? EModuleSubType.shield_t1.ToString() : "";
        ship.interceptorSubType = ship.maxSlots[4] > 0 && zoneConfig.enemyInterceptorEquipSlots > 0 ? EModuleSubType.interceptor_t1.ToString() : "";
        return ship;
    }

    // shipBudget(이 함선에 배정된 여유분) 한도로, 빔→미사일→격납고 순서로 한 슬롯씩 라운드로빈 구매. 실제 소비한 총액을 반환.
    private static int FillRoundRobin(BuildingShip ship, int shipBudget, DataTableModule moduleTable, CrossPlatformRandom random)
    {
        int spent = 0;
        bool progressed = true;
        while (progressed && spent < shipBudget)
        {
            progressed = false;
            for (int c = 0; c < 3; c++)
            {
                EModuleType category = c == 0 ? EModuleType.beam : c == 1 ? EModuleType.missile : EModuleType.hangar;
                if (HasSlotRoom(ship, category) == false) continue;
                int cost = TryEquipOneModule(ship, category, shipBudget - spent, moduleTable, random);
                if (cost > 0)
                {
                    spent += cost;
                    progressed = true;
                }
            }
        }
        return spent;
    }

    // 메인 루프/마지막 척으로도 다 못 쓴 함대 예산을 1번 함선부터 순서대로 훑으며 흡수 — 한 척에만 몰아주지 않음
    private static int AbsorbLeftover(List<BuildingShip> ships, int remaining, DataTableModule moduleTable, CrossPlatformRandom random)
    {
        bool progressed = true;
        while (remaining > 0 && progressed)
        {
            progressed = false;
            for (int s = 0; s < ships.Count; s++)
            {
                BuildingShip ship = ships[s];
                for (int c = 0; c < 3; c++)
                {
                    EModuleType category = c == 0 ? EModuleType.beam : c == 1 ? EModuleType.missile : EModuleType.hangar;
                    if (HasSlotRoom(ship, category) == false) continue;
                    int cost = TryEquipOneModule(ship, category, remaining, moduleTable, random);
                    if (cost > 0)
                    {
                        remaining -= cost;
                        progressed = true;
                    }
                }
            }
        }
        return remaining;
    }

    private static bool HasSlotRoom(BuildingShip ship, EModuleType category)
    {
        switch (category)
        {
            case EModuleType.beam: return ship.beams.Count < ship.beamTarget;
            case EModuleType.missile: return ship.missiles.Count < ship.missileTarget;
            case EModuleType.hangar: return ship.hangars.Count < ship.hangarTarget;
            default: return false;
        }
    }

    // 해당 카테고리에서 budget 이하로 살 수 있는 서브타입 중 랜덤 선택해 장착 — 지금은 t1 하나뿐이라 결과는 동일하지만,
    // 나중에 모듈 등급이 늘어도 함체 선택과 동일한 "랜덤 어포더블" 패턴이라 코드 변경 없이 등급이 섞임. 반환값 0 = 못 삼
    private static int TryEquipOneModule(BuildingShip ship, EModuleType category, int budget, DataTableModule moduleTable, CrossPlatformRandom random)
    {
        ModuleDataList categoryModules = category == EModuleType.beam ? moduleTable.BeamModules
            : category == EModuleType.missile ? moduleTable.MissileModules
            : moduleTable.HangarModules;

        List<ModuleData> candidates = new List<ModuleData>();
        for (int i = 0; i < categoryModules.Count; i++)
        {
            ModuleData data = categoryModules[i];
            if (data.statPoint > 0 && data.statPoint <= budget && IsAlreadyEquipped(ship, category, data.moduleSubType) == false)
                candidates.Add(data);
        }
        if (candidates.Count == 0) return 0;

        ModuleData chosen = candidates[random.Next(candidates.Count)];
        int slotIndex = NextFreeSlotIndex(ship, category);
        AddToCategory(ship, category, new ModuleInfo { moduleType = category, moduleSubType = chosen.moduleSubType, slotIndex = slotIndex });
        return chosen.statPoint;
    }

    // 같은 슬롯 인덱스 중복 방지용 — 물리 슬롯 수(maxSlots)를 넘지 않는 범위에서 비어있는 가장 낮은 인덱스
    private static int NextFreeSlotIndex(BuildingShip ship, EModuleType category)
    {
        List<ModuleInfo> list = CategoryList(ship, category);
        int maxSlotCount = ship.maxSlots[CategoryOrdinal(category)];
        bool[] used = new bool[maxSlotCount];
        for (int i = 0; i < list.Count; i++)
            if (list[i].slotIndex >= 0 && list[i].slotIndex < used.Length) used[list[i].slotIndex] = true;
        for (int i = 0; i < used.Length; i++)
            if (used[i] == false) return i;
        return list.Count;
    }

    private static bool IsAlreadyEquipped(BuildingShip ship, EModuleType category, EModuleSubType subType)
    {
        List<ModuleInfo> list = CategoryList(ship, category);
        for (int i = 0; i < list.Count; i++)
            if (list[i].moduleSubType == subType) return true;
        return false;
    }

    private static int CategoryOrdinal(EModuleType category)
    {
        switch (category)
        {
            case EModuleType.beam: return 0;
            case EModuleType.missile: return 1;
            case EModuleType.hangar: return 2;
            default: return 0;
        }
    }

    private static List<ModuleInfo> CategoryList(BuildingShip ship, EModuleType category)
    {
        switch (category)
        {
            case EModuleType.beam: return ship.beams;
            case EModuleType.missile: return ship.missiles;
            case EModuleType.hangar: return ship.hangars;
            default: return new List<ModuleInfo>();
        }
    }

    private static void AddToCategory(BuildingShip ship, EModuleType moduleType, ModuleInfo info)
    {
        switch (moduleType)
        {
            case EModuleType.beam: ship.beams.Add(info); break;
            case EModuleType.missile: ship.missiles.Add(info); break;
            case EModuleType.hangar: ship.hangars.Add(info); break;
        }
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

            ModuleBodyInfo modules = new ModuleBodyInfo
            {
                beams = ship.beams,
                missiles = ship.missiles,
                hangars = ship.hangars,
                shieldModuleSubType = ship.shieldSubType,
                interceptorModuleSubType = ship.interceptorSubType,
            };

            fleetInfo.ships.Add(new ShipInfo
            {
                shipPresetId = ship.preset.presetId,
                isFront = isFront,
                bodies = new List<ModuleBodyInfo> { modules },
            });
        }

        return fleetInfo;
    }

    private static int EquippedCount(BuildingShip ship)
    {
        return ship.beams.Count + ship.missiles.Count + ship.hangars.Count;
    }
}
