using UnityEngine;

// 존 고정 셀 레이아웃 생성 — ZoneConfig.cellOverrides(희소 리스트, Normal이 아닌 셀만 저장)를 읽어 그리드를 채움
// 기존엔 System.Random 기반 절차적 생성이었으나, 기획자가 셀 단위로 디테일하게 통제하려는 요구로 고정 데이터 방식으로 전환.
// 랜덤 생성이 아니므로 서버(Java)도 같은 ZoneConfigData를 읽기만 하면 되고, 별도 포팅/결정론 동기화가 필요 없음.
public static class ExplorationGridGenerator
{
    // 셀 하나의 월드 크기 — 존마다 다르게 주지 않는 고정값. 그리드가 커지면(7x7 등) 이 값이 아니라
    // 해당 존의 ZoneConfig.galaxyCameraZoom(필요시 RotX)을 데이터로 조정해 화면에 맞춘다.
    // 실제 존들의 galaxyCameraZoom이 수천~만 단위라 60은 너무 작아 그리드 전체가 한 점으로 뭉쳐 보였음 — 실측 후 재조정
    public const float k_cellWorldSize = 800f;

    // 셀 (row,col) → 월드 좌표. Generate()와 에디터 툴(행성 배치 등)이 동일 공식을 공유하기 위해 static으로 노출.
    // 로컬 좌표계 원점(0,0)은 그리드 중앙 셀, 행/열 방향은 월드 고정 축(X=col, Z=row). 카메라 각도/줌과는 무관.
    public static Vector3 ComputeCellWorldPos(ZoneConfig zoneConfig, int row, int col)
    {
        int gridWidth  = zoneConfig != null ? zoneConfig.gridWidth  : 3;
        int gridHeight = zoneConfig != null ? zoneConfig.gridHeight : 3;
        Vector3 gridOrigin = zoneConfig != null ? zoneConfig.galaxyCameraTarget : Vector3.zero;

        float colOffset = (col - (gridWidth  - 1) * 0.5f) * k_cellWorldSize;
        float rowOffset = (row - (gridHeight - 1) * 0.5f) * k_cellWorldSize;
        Vector3 worldPos = gridOrigin + Vector3.right * colOffset + Vector3.forward * rowOffset;
        worldPos.y = 0f; // 함대 레이어 고정 Y(UIPanelExplorationGrid.k_fleetWorldY)와 동일한 스케일
        return worldPos;
    }

    // 월드 좌표 → 가장 가까운 셀 (row,col). ComputeCellWorldPos의 역변환 — 잔해 스포너가 "이 Blocked 셀이 행성 셀인지" 판정할 때 사용
    public static void WorldPosToNearestCell(ZoneConfig zoneConfig, Vector3 worldPos, out int row, out int col)
    {
        int gridWidth  = zoneConfig != null ? zoneConfig.gridWidth  : 3;
        int gridHeight = zoneConfig != null ? zoneConfig.gridHeight : 3;
        Vector3 gridOrigin = zoneConfig != null ? zoneConfig.galaxyCameraTarget : Vector3.zero;

        float colOffset = worldPos.x - gridOrigin.x;
        float rowOffset = worldPos.z - gridOrigin.z;
        col = Mathf.RoundToInt(colOffset / k_cellWorldSize + (gridWidth  - 1) * 0.5f);
        row = Mathf.RoundToInt(rowOffset / k_cellWorldSize + (gridHeight - 1) * 0.5f);
    }

    public static ExplorationGridData Generate(ZoneConfig zoneConfig)
    {
        int gridWidth  = zoneConfig != null ? zoneConfig.gridWidth  : 3;
        int gridHeight = zoneConfig != null ? zoneConfig.gridHeight : 3;

        ExplorationGridData gridData = new ExplorationGridData(gridWidth, gridHeight);

        // 셀별 3D 월드 좌표를 여기서 한 번만 계산해 캐싱
        for (int row = 0; row < gridHeight; row++)
        {
            for (int col = 0; col < gridWidth; col++)
            {
                GridCellData cellData = gridData.GetCell(row, col);
                cellData.worldPos = ComputeCellWorldPos(zoneConfig, row, col);
                gridData.SetCell(cellData);
            }
        }

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
