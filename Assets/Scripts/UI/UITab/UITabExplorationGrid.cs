// 탐사 그리드 탭 컨트롤러 — UITabExploration(구 존/스테이지 선형모델)과 별개의 신규 클래스
// 그리드 생성 + 셀 배치 + 인접 이동까지 담당. 전투 진입/네트워크 연동은 후속 작업에서 연결
using System.Collections.Generic;
using UnityEngine;

public class UITabExplorationGrid : UITabBase
{
    [SerializeField] private GridCellButton m_cellButtonPrefab;
    [SerializeField] private RectTransform m_cellRoot;
    [SerializeField] private DataTableZoneGridSize m_gridSizeTable;
    [SerializeField] private DataTableShipPreset m_shipPresetTable;
    [SerializeField] private DataTableZoneEnemyBudget m_enemyBudgetTable;
    [SerializeField] private float m_cellSize = 200f;

    [Header("존 선택 스크롤")]
    [SerializeField] private InfiniteScrollViewH m_zoneTabScroll;
    [SerializeField] private GameObject m_zoneTabNodePrefab;
    [SerializeField] private UnityEngine.UI.Button m_zoneNavPrev;
    [SerializeField] private UnityEngine.UI.Button m_zoneNavNext;
    // 존 진행에 상한이 없어(zone_grid_size 마지막 구간이 fallback으로 무한 적용) 클리어 진행도 기준으로 넉넉히 늘려 잡음 —
    // 락/클리어 표시 등 실제 진행도 연동은 후속 작업(clearedZones가 아직 구식 스테이지 포맷)
    private const int k_zoneScrollLookahead = 10;
    private const int k_zoneScrollMinCount = 20;

    private int m_zoneGroupCount;

    private ExplorationGridData m_gridData;
    private int m_currentZoneNumber;
    private int m_currentSeed;
    private int m_currentX;
    private int m_currentY;

    private readonly List<GridCellButton> m_buttonPool = new();
    private readonly Dictionary<(int x, int y), GridCellButton> m_activeButtons = new();
    private readonly Dictionary<(int x, int y), TempFleetInfo> m_cellEnemyFleets = new(); // 셀별 적함대 — 저장하지 않고 메모리 캐싱만

    public override void OnTabActivated()
    {
        int zoneNumber = ObjectManager.Instance.GetInitialZoneIndex();
        InitializeZoneTabScroll(zoneNumber);
        NavigateToZone(zoneNumber);
    }

    private void InitializeZoneTabScroll(int initialZoneNumber)
    {
        if (m_zoneTabScroll == null || m_zoneTabNodePrefab == null) return;

        m_zoneGroupCount = Mathf.Max(k_zoneScrollMinCount, initialZoneNumber + k_zoneScrollLookahead);

        m_zoneTabScroll.onItemBind = OnZoneTabNodeBind;
        m_zoneTabScroll.onCenterIndexChanged = OnZoneScrollCenterChanged;
        m_zoneTabScroll.Initialize(m_zoneGroupCount, m_zoneTabNodePrefab);

        if (m_zoneNavPrev != null) m_zoneNavPrev.onClick.AddListener(() => { SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true); NavigateToZone(m_currentZoneNumber - 1); });
        if (m_zoneNavNext != null) m_zoneNavNext.onClick.AddListener(() => { SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true); NavigateToZone(m_currentZoneNumber + 1); });
    }

    private void OnZoneTabNodeBind(int dataIndex, GameObject obj)
    {
        UIZoneTabNode node = obj.GetComponent<UIZoneTabNode>();
        if (node == null) return;
        node.SetData(dataIndex, OnZoneTabNodeClicked);
        bool selected = dataIndex + 1 == m_currentZoneNumber;
        node.SetState(selected, isCleared: false, isLocked: false); // TODO: 그리드 시스템용 클리어/락 진행도 연동
    }

    private void OnZoneTabNodeClicked(int groupIndex)
    {
        int targetZoneNumber = groupIndex + 1;
        if (targetZoneNumber == m_currentZoneNumber) return;
        NavigateToZone(targetZoneNumber);
    }

    // 스크롤 드래그로 중앙 인덱스 변경
    private void OnZoneScrollCenterChanged(int dataIndex)
    {
        int newZoneNumber = dataIndex + 1;
        if (newZoneNumber == m_currentZoneNumber) return;
        NavigateToZone(newZoneNumber);
    }

    private void NavigateToZone(int zoneNumber)
    {
        zoneNumber = Mathf.Clamp(zoneNumber, 1, m_zoneGroupCount > 0 ? m_zoneGroupCount : zoneNumber);
        if (zoneNumber == m_currentZoneNumber && m_gridData != null) return;

        RefreshZoneNavButtons(zoneNumber);
        if (m_zoneTabScroll != null)
        {
            m_zoneTabScroll.ScrollToCenter(zoneNumber - 1);
            m_zoneTabScroll.RefreshVisible();
        }

        EnterZone(zoneNumber, ComputeZoneSeed(zoneNumber));
    }

    private void RefreshZoneNavButtons(int zoneNumber)
    {
        if (m_zoneNavPrev != null) m_zoneNavPrev.interactable = zoneNumber > 1;
        if (m_zoneNavNext != null) m_zoneNavNext.interactable = m_zoneGroupCount <= 0 || zoneNumber < m_zoneGroupCount;
    }

    // 로그인 시 받은 커맨더별 고정 시드(explorationSeedBase)와 zoneNumber를 조합 — 서버 재요청 없이 클라에서 결정론적으로 계산
    private int ComputeZoneSeed(int zoneNumber)
    {
        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        int seedBase = commanderInfo != null ? commanderInfo.explorationSeedBase : 0;
        return seedBase ^ (zoneNumber * 486187739);
    }

    public void EnterZone(int zoneNumber, int seed)
    {
        m_currentZoneNumber = zoneNumber;
        m_currentSeed = seed;
        m_gridData = ExplorationGridGenerator.Generate(zoneNumber, seed, m_gridSizeTable);
        m_currentX = m_gridData.startX;
        m_currentY = m_gridData.startY;

        BuildCellButtons();
        BuildCellEnemyFleets();
        RefreshCellStates();
    }

    // 통행 가능한 셀마다 적함대 구성을 즉석 계산해 캐싱 — (zoneNumber, seed, x, y) 결정론적이라 재계산해도 항상 동일
    private void BuildCellEnemyFleets()
    {
        m_cellEnemyFleets.Clear();

        for (int x = 0; x < m_gridData.width; x++)
        {
            for (int y = 0; y < m_gridData.height; y++)
            {
                GridCellData cellData = m_gridData.GetCell(x, y);
                if (cellData.isBlocked || cellData.isStart || cellData.isEscape || cellData.isEmpty) continue;

                TempFleetInfo fleetInfo = ExplorationEnemyFleetGenerator.Generate(
                    m_currentZoneNumber, m_currentSeed, x, y, m_shipPresetTable, m_enemyBudgetTable);
                m_cellEnemyFleets[(x, y)] = fleetInfo;
            }
        }
    }

    public TempFleetInfo GetCellEnemyFleet(int x, int y)
    {
        m_cellEnemyFleets.TryGetValue((x, y), out TempFleetInfo fleetInfo);
        return fleetInfo;
    }

    private void BuildCellButtons()
    {
        ReturnAllButtonsToPool();

        // 그리드 전체가 CellRoot(화면 중앙 앵커) 기준으로 가운데 정렬되도록 보정
        Vector2 originOffset = new Vector2(-(m_gridData.width - 1) * m_cellSize * 0.5f, -(m_gridData.height - 1) * m_cellSize * 0.5f);

        for (int x = 0; x < m_gridData.width; x++)
        {
            for (int y = 0; y < m_gridData.height; y++)
            {
                GridCellData cellData = m_gridData.GetCell(x, y);
                GridCellButton button = GetButtonFromPool();
                button.gameObject.SetActive(true);
                button.Initialize(cellData, OnCellClicked);
                button.SetAnchoredPosition(m_cellSize, originOffset);
                m_activeButtons[(x, y)] = button;
            }
        }
    }

    private void OnCellClicked(int x, int y)
    {
        bool isCurrentCell = x == m_currentX && y == m_currentY;
        bool isAdjacent = m_gridData.IsAdjacent(m_currentX, m_currentY, x, y);

        if (isCurrentCell == false && isAdjacent == false) return;
        if (isCurrentCell == false && m_gridData.GetCell(x, y).isBlocked) return;

        if (isCurrentCell)
        {
            // 현재 위치 재클릭 — 재방문 클리어 셀 재도전 등 후속 로직에서 처리 예정
            return;
        }

        m_currentX = x;
        m_currentY = y;
        RefreshCellStates();
    }

    private void RefreshCellStates()
    {
        foreach (var kv in m_activeButtons)
        {
            int x = kv.Key.x;
            int y = kv.Key.y;
            GridCellButton button = kv.Value;
            GridCellData cellData = m_gridData.GetCell(x, y);

            bool isCurrentCell = x == m_currentX && y == m_currentY;
            bool isAdjacent = m_gridData.IsAdjacent(m_currentX, m_currentY, x, y);

            EGridCellVisualState state;
            if (isCurrentCell) state = EGridCellVisualState.Current;
            else if (cellData.isBlocked) state = EGridCellVisualState.Locked; // 통행불가 셀은 인접해도 항상 잠김 표시
            else if (isAdjacent) state = EGridCellVisualState.Reachable;
            else if (cellData.isCleared) state = EGridCellVisualState.Cleared;
            else state = EGridCellVisualState.Locked;

            button.SetVisualState(state);
        }
    }

    private GridCellButton GetButtonFromPool()
    {
        if (m_buttonPool.Count > 0)
        {
            GridCellButton button = m_buttonPool[^1];
            m_buttonPool.RemoveAt(m_buttonPool.Count - 1);
            return button;
        }
        return Instantiate(m_cellButtonPrefab, m_cellRoot);
    }

    private void ReturnAllButtonsToPool()
    {
        foreach (var kv in m_activeButtons)
        {
            kv.Value.gameObject.SetActive(false);
            m_buttonPool.Add(kv.Value);
        }
        m_activeButtons.Clear();
    }
}
