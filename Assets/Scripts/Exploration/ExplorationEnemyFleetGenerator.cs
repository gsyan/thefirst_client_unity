// 셀별 적함대 절차적 생성 — (seed, x, y, fleetIndex) 조합이면 항상 같은 결과(결정론적, 공략 공유 가능)
// 저장하지 않음: 그리드 진입 시 셀마다 즉석 계산 후 메모리 캐싱만 (Exploration_Grid_Implementation.md §7)
// 구식 DataTableZoneEditor.GenGradePartition과 동일 의도를 프리셋 기반으로 재구성 — commandCost가 구식의 "등급" 역할을 대신함
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ExplorationEnemyFleetGenerator
{
    // 존별로 설정된 순차 웨이브(fleets) 전체 생성 — 웨이브 0번만 워프인 트리거가 연결되어 있고(전투 시스템 미구현),
    // 나머지는 캐싱만 해두고 후속 전투 시스템에서 이어서 사용
    public static List<TempFleetInfo> GenerateWaves(ZoneConfig zoneConfig, int seed, int x, int y, DataTableShipPreset presetTable)
    {
        List<TempFleetInfo> waves = new List<TempFleetInfo>();

        List<ShipPresetData> presets = presetTable != null ? presetTable.GetShipPresetDataList() : null;
        if (presets == null || presets.Count == 0 || zoneConfig == null) return waves;

        for (int fleetIndex = 0; fleetIndex < zoneConfig.enemyFleetsPerCell; fleetIndex++)
            waves.Add(GenerateOneWave(seed, x, y, fleetIndex, presets, zoneConfig));

        return waves;
    }

    // 구식 DataTableZoneEditor.GetBlockMaxTier와 동일 공식 — (x,y,fleetIndex) 결정론적, deviation만큼 costCap을 무작위로 낮춤
    private static int ResolveCostCap(int seed, int x, int y, int fleetIndex, int maxCost, int deviation)
    {
        // deviation<=0이면 costCap = 1000 그대로
        if (deviation <= 0) return maxCost;
        // deviation>0이면 [1000-deviation, 1000] 범위에서 랜덤 — 서버(CrossPlatformRandom.java)와 동일 알고리즘이어야 함
        CrossPlatformRandom random = new CrossPlatformRandom(seed ^ (x * 73856093) ^ (y * 19349663) ^ (fleetIndex * 83492791));
        return Mathf.Max(1, maxCost - random.Next(0, deviation + 1));
    }

    // 구식 GenGradePartition과 동일 의도: 예산을 무작위로 소진하되, 함선 수 상한에 걸리면
    // 마지막 한 척은 "남은 예산에 가장 가까운(낭비 최소)" 프리셋으로 확정해서 채움
    private static TempFleetInfo GenerateOneWave(int seed, int x, int y, int fleetIndex,
        List<ShipPresetData> presets, ZoneConfig zoneConfig)
    {
        TempFleetInfo fleetInfo = new TempFleetInfo { ships = new List<TempShipInfo>() };
        int costCap = ResolveCostCap(seed, x, y, fleetIndex, zoneConfig.enemyMaxCost, zoneConfig.enemyDeviation);
        CrossPlatformRandom random = new CrossPlatformRandom(seed ^ (x * 73856093) ^ (y * 19349663) ^ (fleetIndex * 83492791) ^ 0x5EED);

        int remaining = zoneConfig.enemyBudget;
        List<ShipPresetData> affordable = new List<ShipPresetData>();

        while (remaining > 0 && fleetInfo.ships.Count < zoneConfig.enemyMaxShipsPerFleet - 1)
        {
            int cap = Mathf.Min(remaining, costCap);
            affordable.Clear();
            for (int i = 0; i < presets.Count; i++)
                if (presets[i].commandCost > 0 && presets[i].commandCost <= cap)
                    affordable.Add(presets[i]);
            if (affordable.Count == 0) break;

            ShipPresetData chosen = affordable[random.Next(affordable.Count)];
            fleetInfo.ships.Add(new TempShipInfo { shipPresetId = chosen.presetId });
            remaining -= chosen.commandCost;
        }

        // 함선 수 상한에 걸려 예산이 남았으면, 남은 예산에 가장 가깝게 맞는(낭비 최소) 프리셋으로 마지막 한 척 채움
        if (remaining > 0)
        {
            int cap = Mathf.Min(remaining, costCap);
            ShipPresetData bestFit = null;
            for (int i = 0; i < presets.Count; i++)
            {
                ShipPresetData p = presets[i];
                if (p.commandCost > 0 && p.commandCost <= cap && (bestFit == null || p.commandCost > bestFit.commandCost))
                    bestFit = p;
            }
            if (bestFit != null)
                fleetInfo.ships.Add(new TempShipInfo { shipPresetId = bestFit.presetId });
        }

        if (fleetInfo.ships.Count == 0 && presets.Count > 0)
            fleetInfo.ships.Add(new TempShipInfo { shipPresetId = presets[0].presetId }); // 폴백 — 구식 "grade=1" 폴백과 동일 의도

        // commandCost 내림차순 정렬 후 절반은 전방/절반은 후방 — 구식 "기함(최고 등급)=ship_index 0" 관례를 전/후방 배치에 반영
        // List.Sort는 불안정 정렬(동률 순서 미보장)이라 서버(Java List.sort, 안정 정렬)와 결과가 어긋날 수 있음 — 반드시 안정 정렬(OrderByDescending)만 사용할 것
        fleetInfo.ships = fleetInfo.ships.OrderByDescending(s => GetCost(presets, s)).ToList();
        for (int i = 0; i < fleetInfo.ships.Count; i++)
            fleetInfo.ships[i].isFront = i < (fleetInfo.ships.Count + 1) / 2;

        return fleetInfo;
    }

    private static int GetCost(List<ShipPresetData> presets, TempShipInfo ship)
    {
        for (int i = 0; i < presets.Count; i++)
            if (presets[i].presetId == ship.shipPresetId)
                return presets[i].commandCost;
        return 0;
    }
}
