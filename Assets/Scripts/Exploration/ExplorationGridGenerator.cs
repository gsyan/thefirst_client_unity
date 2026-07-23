// 존 시드 기반 결정적 그리드 생성 — 같은 (zoneNumber, seed) 입력이면 항상 같은 결과
// Docs/Exploration_Grid_Implementation.md §6~9 참고
using System;
using System.Collections.Generic;

public static class ExplorationGridGenerator
{
    // 시작-탈출점 최소 셀 간격, 빈 셀(무적 보상) 확률 — 프로토타입 체감 후 튜닝 예정 (미결 사항, §10)
    private const float k_minStartEscapeDistanceRatio = 0.5f;
    private const float k_emptyCellProbability = 0.05f;
    private const float k_blockedCellProbability = 0.15f;

    public static ExplorationGridData Generate(int seed, int gridWidth, int gridHeight)
    {
        ExplorationGridData gridData = new ExplorationGridData(gridWidth, gridHeight);

        System.Random random = new System.Random(seed);

        int minDistance = (int)((gridWidth + gridHeight) * 0.5f * k_minStartEscapeDistanceRatio);
        PlaceStartAndEscape(gridData, random, minDistance);
        PlaceEmptyCells(gridData, random);
        PlaceBlockedCells(gridData, random);

        return gridData;
    }

    private static void PlaceStartAndEscape(ExplorationGridData gridData, System.Random random, int minDistance)
    {
        int startX = random.Next(gridData.width);
        int startY = random.Next(gridData.height);

        int escapeX;
        int escapeY;
        int manhattanDistance;
        int attemptCount = 0;
        const int k_maxAttempt = 100;

        do
        {
            escapeX = random.Next(gridData.width);
            escapeY = random.Next(gridData.height);
            manhattanDistance = Math.Abs(escapeX - startX) + Math.Abs(escapeY - startY);
            attemptCount++;
        }
        while (manhattanDistance < minDistance && attemptCount < k_maxAttempt);

        gridData.startX = startX;
        gridData.startY = startY;
        gridData.escapeX = escapeX;
        gridData.escapeY = escapeY;

        GridCellData startCell = gridData.GetCell(startX, startY);
        startCell.isStart = true;
        gridData.SetCell(startCell);

        GridCellData escapeCell = gridData.GetCell(escapeX, escapeY);
        escapeCell.isEscape = true;
        gridData.SetCell(escapeCell);
    }

    private static void PlaceEmptyCells(ExplorationGridData gridData, System.Random random)
    {
        for (int x = 0; x < gridData.width; x++)
        {
            for (int y = 0; y < gridData.height; y++)
            {
                GridCellData cellData = gridData.GetCell(x, y);
                if (cellData.isStart || cellData.isEscape) continue;

                double roll = random.NextDouble();
                if (roll < k_emptyCellProbability)
                {
                    cellData.isEmpty = true;
                    gridData.SetCell(cellData);
                }
            }
        }
    }

    // 확률 기반 후보 배치 후 BFS로 시작점→탈출점 연결성 확인 — 끊기면 해당 셀은 블록 취소(연결성 최우선)
    private static void PlaceBlockedCells(ExplorationGridData gridData, System.Random random)
    {
        for (int x = 0; x < gridData.width; x++)
        {
            for (int y = 0; y < gridData.height; y++)
            {
                GridCellData cellData = gridData.GetCell(x, y);
                if (cellData.isStart || cellData.isEscape) continue;

                double roll = random.NextDouble();
                if (roll >= k_blockedCellProbability) continue;

                cellData.isBlocked = true;
                gridData.SetCell(cellData);

                if (IsEscapeReachable(gridData) == false)
                {
                    cellData.isBlocked = false;
                    gridData.SetCell(cellData);
                }
            }
        }
    }

    // 시작점에서 탈출점까지 4방향 인접 + isBlocked 미포함 경로가 존재하는지 BFS로 검증
    private static bool IsEscapeReachable(ExplorationGridData gridData)
    {
        bool[,] visited = new bool[gridData.width, gridData.height];
        Queue<(int x, int y)> queue = new Queue<(int x, int y)>();

        queue.Enqueue((gridData.startX, gridData.startY));
        visited[gridData.startX, gridData.startY] = true;

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            (int x, int y) current = queue.Dequeue();
            if (current.x == gridData.escapeX && current.y == gridData.escapeY)
                return true;

            for (int i = 0; i < 4; i++)
            {
                int nx = current.x + dx[i];
                int ny = current.y + dy[i];
                if (gridData.IsInBounds(nx, ny) == false) continue;
                if (visited[nx, ny]) continue;
                if (gridData.GetCell(nx, ny).isBlocked) continue;

                visited[nx, ny] = true;
                queue.Enqueue((nx, ny));
            }
        }

        return false;
    }
}
