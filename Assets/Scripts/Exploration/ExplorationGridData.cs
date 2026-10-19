// 탐사 그리드 컨테이너 — 셀 배열 + 시작/탈출점 좌표 보관
using System.Collections.Generic;

public class ExplorationGridData
{
    public int width;
    public int height;
    public int startRow;
    public int startCol;
    public int escapeRow;
    public int escapeCol;

    private GridCellData[,] m_cells;

    public ExplorationGridData(int width, int height)
    {
        this.width = width;
        this.height = height;
        m_cells = new GridCellData[height, width];
        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                m_cells[row, col] = new GridCellData(row, col);
            }
        }
    }

    public bool IsInBounds(int row, int col)
    {
        return row >= 0 && row < height && col >= 0 && col < width;
    }

    public GridCellData GetCell(int row, int col)
    {
        return m_cells[row, col];
    }

    public void SetCell(GridCellData cellData)
    {
        m_cells[cellData.row, cellData.col] = cellData;
    }

    public void SetCellCleared(int row, int col, bool cleared)
    {
        GridCellData cellData = m_cells[row, col];
        cellData.isCleared = cleared;
        m_cells[row, col] = cellData;
    }

    // 4방향(상하좌우) 인접 여부만 허용 — 대각선 이동 불가 (Docs/Exploration_Grid_Implementation.md §3)
    public bool IsAdjacent(int row1, int col1, int row2, int col2)
    {
        int deltaRow = System.Math.Abs(row1 - row2);
        int deltaCol = System.Math.Abs(col1 - col2);
        return (deltaRow == 1 && deltaCol == 0) || (deltaRow == 0 && deltaCol == 1);
    }

    private static readonly int[] s_deltaRow = { -1, 1, 0, 0 };
    private static readonly int[] s_deltaCol = { 0, 0, -1, 1 };

    // 경로 통과 가능 판정 — "클리어됨 또는 시작 칸이고 isBlocked==false이고 isEscape==false"만 통과 가능한 노드로 취급
    // (탈출 셀은 클리어 즉시 존이 끝나는 특수 셀이라 경유 노드에서 제외, 서버 ExplorationService.isReachableByBfs와 동일 규칙)
    private bool IsPassableCell(GridCellData cell)
    {
        return cell.isBlocked == false && cell.isEscape == false && (cell.isCleared || cell.isStart);
    }

    // (row,col) 자신은 통과 가능 칸이 아니지만, 그런 칸에 4방향으로 인접한지 — "지금 한 번의 터치로 도달 가능하고
    // 이동 확인 팝업이 뜨는 셀(미클리어 전투/이벤트/탈출)"을 가려내는 용도(그리드 발광 표시에 사용)
    public bool IsAdjacentToPassableCell(int row, int col)
    {
        for (int i = 0; i < 4; i++)
        {
            int neighborRow = row + s_deltaRow[i];
            int neighborCol = col + s_deltaCol[i];
            if (IsInBounds(neighborRow, neighborCol) == false) continue;
            if (IsPassableCell(GetCell(neighborRow, neighborCol)))
                return true;
        }
        return false;
    }

    // 4방향 BFS 최단경로 — 목적지가 통과 가능 칸이 아니어도(미클리어 전투/이벤트/탈출 셀) 통과 가능한 칸에
    // 인접하기만 하면 경로의 마지막 한 칸으로 포함한다.
    // 반환값: fromRow,fromCol부터 toRow,toCol까지 양끝 포함 좌표 리스트(인접이면 정확히 2개). 도달 불가/목적지가 Blocked면 null.
    public List<(int row, int col)> FindPath(int fromRow, int fromCol, int toRow, int toCol)
    {
        if (IsInBounds(fromRow, fromCol) == false || IsInBounds(toRow, toCol) == false) return null;
        if (GetCell(toRow, toCol).isBlocked) return null;

        bool IsPassable(int row, int col) => IsPassableCell(GetCell(row, col));

        int[,] distance = new int[height, width];
        for (int row = 0; row < height; row++)
            for (int col = 0; col < width; col++)
                distance[row, col] = -1;

        (int row, int col)[,] parent = new (int row, int col)[height, width];
        Queue<(int row, int col)> queue = new Queue<(int row, int col)>();

        distance[fromRow, fromCol] = 0;
        parent[fromRow, fromCol] = (-1, -1);
        queue.Enqueue((fromRow, fromCol));

        while (queue.Count > 0)
        {
            (int row, int col) current = queue.Dequeue();
            for (int i = 0; i < 4; i++)
            {
                int nextRow = current.row + s_deltaRow[i];
                int nextCol = current.col + s_deltaCol[i];
                if (IsInBounds(nextRow, nextCol) == false) continue;
                if (distance[nextRow, nextCol] != -1) continue;
                if (IsPassable(nextRow, nextCol) == false) continue;

                distance[nextRow, nextCol] = distance[current.row, current.col] + 1;
                parent[nextRow, nextCol] = current;
                queue.Enqueue((nextRow, nextCol));
            }
        }

        List<(int row, int col)> BuildPath(int endRow, int endCol)
        {
            List<(int row, int col)> path = new List<(int row, int col)>();
            (int row, int col) node = (endRow, endCol);
            while (node.row != -1)
            {
                path.Add(node);
                node = parent[node.row, node.col];
            }
            path.Reverse();
            return path;
        }

        if (distance[toRow, toCol] != -1)
            return BuildPath(toRow, toCol);

        // 목적지가 통과 불가 칸(미클리어/이벤트/탈출)이면, 통과 가능한 칸 중 목적지에 4방향 인접한 칸 가운데
        // 거리가 가장 짧은 칸을 찾아 그 뒤에 목적지를 마지막 한 칸으로 붙인다
        int bestRow = -1, bestCol = -1, bestDistance = int.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            int neighborRow = toRow + s_deltaRow[i];
            int neighborCol = toCol + s_deltaCol[i];
            if (IsInBounds(neighborRow, neighborCol) == false) continue;
            if (distance[neighborRow, neighborCol] == -1) continue;
            if (distance[neighborRow, neighborCol] < bestDistance)
            {
                bestDistance = distance[neighborRow, neighborCol];
                bestRow = neighborRow;
                bestCol = neighborCol;
            }
        }

        if (bestRow == -1) return null;

        List<(int row, int col)> path = BuildPath(bestRow, bestCol);
        path.Add((toRow, toCol));
        return path;
    }
}
