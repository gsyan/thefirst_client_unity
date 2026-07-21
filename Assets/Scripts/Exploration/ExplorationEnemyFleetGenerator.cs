// 셀별 적함대 절차적 생성 — (zoneNumber, seed, x, y) 조합이면 항상 같은 결과(결정론적, 공략 공유 가능)
// 저장하지 않음: 그리드 진입 시 셀마다 즉석 계산 후 메모리 캐싱만 (Exploration_Grid_Implementation.md §7)
// 프리셋→3D 스폰 브릿지는 별도 후속 작업 — 여기서는 TempFleetInfo 데이터 생성까지만 담당
using System.Collections.Generic;

public static class ExplorationEnemyFleetGenerator
{
    private const int k_maxShips = 20; // 안전장치 — 무한 루프 방지용 상한

    public static TempFleetInfo Generate(int zoneNumber, int seed, int x, int y, DataTableShipPreset presetTable, DataTableZoneEnemyBudget budgetTable)
    {
        TempFleetInfo fleetInfo = new TempFleetInfo { ships = new List<ExplorationShipSlot>() };

        List<ShipPresetData> presets = presetTable != null ? presetTable.GetShipPresetDataList() : null;
        if (presets == null || presets.Count == 0) return fleetInfo;

        int budget = budgetTable != null ? budgetTable.GetCommandPowerBudget(zoneNumber) : 0;
        if (budget <= 0) return fleetInfo;

        // DataTableZoneEditor.GenGradePartition과 동일한 해시 조합 관례 — 좌표별 결정론적 시드
        System.Random random = new System.Random(seed ^ (x * 73856093) ^ (y * 19349663));

        int remaining = budget;
        List<ShipPresetData> affordable = new List<ShipPresetData>();

        while (remaining > 0 && fleetInfo.ships.Count < k_maxShips)
        {
            affordable.Clear();
            for (int i = 0; i < presets.Count; i++)
            {
                if (presets[i].commandCost > 0 && presets[i].commandCost <= remaining)
                    affordable.Add(presets[i]);
            }
            if (affordable.Count == 0) break;

            ShipPresetData chosen = affordable[random.Next(affordable.Count)];
            bool isFront = fleetInfo.ships.Count % 2 == 0; // 전방/후방 번갈아 배치 — 단순 기본값, 밸런스는 후속 튜닝
            fleetInfo.ships.Add(new ExplorationShipSlot { shipPresetId = chosen.presetId, isFront = isFront });
            remaining -= chosen.commandCost;
        }

        return fleetInfo;
    }
}
