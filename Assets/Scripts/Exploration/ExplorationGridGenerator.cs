// 존 고정 셀 레이아웃 생성 — ZoneConfig.cellOverrides(희소 리스트, Normal이 아닌 셀만 저장)를 읽어 그리드를 채움
// 기존엔 System.Random 기반 절차적 생성이었으나, 기획자가 셀 단위로 디테일하게 통제하려는 요구로 고정 데이터 방식으로 전환.
// 랜덤 생성이 아니므로 서버(Java)도 같은 ZoneConfigData를 읽기만 하면 되고, 별도 포팅/결정론 동기화가 필요 없음.
public static class ExplorationGridGenerator
{
    public static ExplorationGridData Generate(ZoneConfig zoneConfig)
    {
        int gridWidth  = zoneConfig != null ? zoneConfig.gridWidth  : 3;
        int gridHeight = zoneConfig != null ? zoneConfig.gridHeight : 3;

        ExplorationGridData gridData = new ExplorationGridData(gridWidth, gridHeight);
        if (zoneConfig == null || zoneConfig.cellOverrides == null) return gridData;

        foreach (GridCellOverride cellOverride in zoneConfig.cellOverrides)
        {
            if (gridData.IsInBounds(cellOverride.row, cellOverride.col) == false) continue;

            GridCellData cellData = gridData.GetCell(cellOverride.row, cellOverride.col);
            switch (cellOverride.type)
            {
                case EGridCellType.Blocked:
                    cellData.isBlocked = true;
                    break;
                case EGridCellType.Start:
                    cellData.isStart = true;
                    gridData.startRow = cellOverride.row;
                    gridData.startCol = cellOverride.col;
                    break;
                case EGridCellType.Escape:
                    cellData.isEscape = true;
                    gridData.escapeRow = cellOverride.row;
                    gridData.escapeCol = cellOverride.col;
                    break;
                case EGridCellType.Event:
                    cellData.isEvent = true;
                    cellData.eventType = cellOverride.eventType;
                    break;
            }
            gridData.SetCell(cellData);
        }

        return gridData;
    }
}
