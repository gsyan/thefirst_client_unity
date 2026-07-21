// 탐사 그리드 컨테이너 — 셀 배열 + 시작/탈출점 좌표 보관
public class ExplorationGridData
{
    public int width;
    public int height;
    public int startX;
    public int startY;
    public int escapeX;
    public int escapeY;

    private GridCellData[,] m_cells;

    public ExplorationGridData(int width, int height)
    {
        this.width = width;
        this.height = height;
        m_cells = new GridCellData[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                m_cells[x, y] = new GridCellData(x, y);
            }
        }
    }

    public bool IsInBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    public GridCellData GetCell(int x, int y)
    {
        return m_cells[x, y];
    }

    public void SetCell(GridCellData cellData)
    {
        m_cells[cellData.x, cellData.y] = cellData;
    }

    public void SetCellCleared(int x, int y, bool cleared)
    {
        GridCellData cellData = m_cells[x, y];
        cellData.isCleared = cleared;
        m_cells[x, y] = cellData;
    }

    // 4방향(상하좌우) 인접 여부만 허용 — 대각선 이동 불가 (Docs/Exploration_Grid_Implementation.md §3)
    public bool IsAdjacent(int x1, int y1, int x2, int y2)
    {
        int deltaX = System.Math.Abs(x1 - x2);
        int deltaY = System.Math.Abs(y1 - y2);
        return (deltaX == 1 && deltaY == 0) || (deltaX == 0 && deltaY == 1);
    }
}
