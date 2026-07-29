using UnityEngine;
// 탐사 그리드 셀 하나의 데이터 — Docs/Exploration_Grid_Implementation.md §7 참고
// 적 함대 구성/보상카드는 필드로 저장하지 않음 (각각 결정적 재계산 / 즉석 랜덤 생성)
[System.Serializable]
public struct GridCellData
{
    public int row;
    public int col;
    public bool isStart;
    public bool isEscape;
    public bool isEvent;               // Empty(적 없음) 등 이벤트 셀 — 세부 종류는 eventType
    public EGridEventType eventType;   // isEvent == true 일 때만 유효
    public bool isCleared;
    public bool isBlocked; // 통행 불가 셀 — 이동/적함대 생성 대상에서 제외
    public Vector3 worldPos; // 3D 월드 좌표 — ExplorationGridGenerator.Generate()에서 1회 계산해 캐싱(galaxyCameraTarget 기준 월드 고정 축 배치, 카메라 상태와 무관)

    public GridCellData(int row, int col)
    {
        this.row = row;
        this.col = col;
        isStart = false;
        isEscape = false;
        isEvent = false;
        eventType = EGridEventType.NoEnemy;
        isCleared = false;
        isBlocked = false;
        worldPos = Vector3.zero;
    }
}
