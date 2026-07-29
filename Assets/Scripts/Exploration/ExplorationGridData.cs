// 탐사 그리드 컨테이너 — 셀 배열 + 시작/탈출점 좌표 보관
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
}
