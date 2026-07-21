// 탐사 그리드 셀 하나의 데이터 — Docs/Exploration_Grid_Implementation.md §7 참고
// 적 함대 구성/보상카드는 필드로 저장하지 않음 (각각 결정적 재계산 / 즉석 랜덤 생성)
[System.Serializable]
public struct GridCellData
{
    public int x;
    public int y;
    public bool isStart;
    public bool isEscape;
    public bool isEmpty;
    public bool isCleared;
    public bool isBlocked; // 통행 불가 셀 — 이동/적함대 생성 대상에서 제외

    public GridCellData(int x, int y)
    {
        this.x = x;
        this.y = y;
        isStart = false;
        isEscape = false;
        isEmpty = false;
        isCleared = false;
        isBlocked = false;
    }
}
