// 탐사 그리드 탭 컨트롤러 — UITabExploration(구 존/스테이지 선형모델)과 별개의 신규 클래스
// 그리드 생성 + 셀 배치 + 인접 이동 + 갤럭시뷰 연동 + 셀 진입(워프인) 담당. 전투 진입은 후속 작업에서 연결
using System.Collections.Generic;
using UnityEngine;

public class UITabExplorationGrid : UITabBase
{
    [SerializeField] private GridCellButton m_cellButtonPrefab;
    [SerializeField] private RectTransform m_cellRoot;
    [SerializeField] private DataTableShipPreset m_shipPresetTable;
    [SerializeField] private float m_cellSize = 200f; // 화면상 셀 간격(px) — 고정, 3D 좌표는 이 화면 위치에서 카메라 광선을 Y=0 평면에 쏴서 역산
    [SerializeField] private UnityEngine.UI.Button m_backgroundCloseButton; // 빈 곳 클릭 시 탭 닫기용 투명 풀스크린 버튼

    [Header("존 선택 스크롤")]
    [SerializeField] private InfiniteScrollViewH m_zoneTabScroll;
    [SerializeField] private GameObject m_zoneTabNodePrefab;
    [SerializeField] private UnityEngine.UI.Button m_zoneNavPrev;
    [SerializeField] private UnityEngine.UI.Button m_zoneNavNext;
    // 존 진행에 상한이 없어(zone_grid_size 마지막 구간이 fallback으로 무한 적용) 클리어 진행도 기준으로 넉넉히 늘려 잡음 —
    // 락/클리어 표시 등 실제 진행도 연동은 후속 작업(clearedZones가 아직 구식 스테이지 포맷)
    private const int k_zoneScrollLookahead = 10;
    private const int k_zoneScrollMinCount = 20;

    private const float k_fleetWorldY = 0f;             // 함대 레이어 — 천체는 pos_y=-550으로 분리(별도 작업)
    private const float k_enemyEncounterDistance = 50f; // 셀 안 전투 조우 거리 — datatable_zone_enemy_fleet_position.csv grade1과 동일 스케일(국소 전술 거리, 갤럭시뷰 좌표와는 별개)

    private int m_zoneGroupCount;
    private int m_pendingZoneNumber;

    private ExplorationGridData m_gridData;
    private int m_currentZoneNumber;
    private int m_currentSeed;
    private int m_currentX;
    private int m_currentY;

    private bool m_pendingCellEntry;
    private int m_pendingCellX;
    private int m_pendingCellY;
    private Vector3 m_pendingCellWorldPos;

    // 대치 상태(UIPanelPrepareBattle) 중 퇴각 시 되돌아갈 이전 위치 — 로컬뷰 카메라 전환 이후엔 그리드 좌표→월드좌표 역산이 불가능(카메라 자세가 이미 바뀜)하므로 이동 직전 좌표를 스냅샷
    private int m_previousX;
    private int m_previousY;
    private Vector3 m_previousFleetWorldPos;
    private SpaceFleet m_standoffEnemyFleet;

    private readonly List<GridCellButton> m_buttonPool = new();
    private readonly Dictionary<(int x, int y), GridCellButton> m_activeButtons = new();
    private readonly Dictionary<(int x, int y), List<TempFleetInfo>> m_cellEnemyWaves = new(); // 셀별 순차 웨이브 — 저장하지 않고 메모리 캐싱만

    // ObjectManager가 그리드 UI를 연 적 없이 로그인 시 초기 함대 위치를 계산할 때, 여기 설정된 값과 동일한 스케일을 쓰기 위해 참조
    public float GetCellSizePixels() { return m_cellSize; }

    // 갤럭시뷰 카메라 전환이 끝나기 전엔 패널을 보여주지 않음 — UIPanelSpace가 UITabShip에 쓰던 deferReveal 방식과 동일 패턴,
    // OnGalaxyViewSettled에서 RevealDeferredPanel로 실제 노출
    public override void InitializeUITab()
    {
        if (m_tabSystemParent == null) return;
        for (int i = 0; i < m_tabSystemParent.tabs.Count; i++)
        {
            if (m_tabSystemParent.tabs[i].tabPanel == gameObject)
            {
                m_tabSystemParent.tabs[i].deferReveal = true;
                break;
            }
        }
    }

    public override void OnTabActivated()
    {
        int zoneNumber = ObjectManager.Instance.GetInitialZoneIndex();
        m_currentZoneNumber = zoneNumber; // EnterZone(그리드 생성)은 갤럭시뷰 정착 후로 미뤄지지만, 존 스크롤 초기 선택 하이라이트는 그 전에 필요
        InitializeZoneTabScroll(zoneNumber);
        m_pendingCellEntry = false;
        m_pendingZoneNumber = zoneNumber;

        HideTabButtons();

        if (m_backgroundCloseButton != null)
        {
            m_backgroundCloseButton.onClick.RemoveAllListeners();
            m_backgroundCloseButton.onClick.AddListener(() =>
            {
                if (m_tabSystemParent != null) m_tabSystemParent.CloseAllTabs();
            });
        }

        ObjectManager.Instance.ChangeZone(zoneNumber);

        ZoneConfig zoneConfig = DataManager.Instance.m_dataTableZone.GetZoneByZoneIndex(zoneNumber);
        if (zoneConfig != null && CameraController.Instance != null)
        {
            EventManager.Subscribe_GalaxyViewSettled(OnGalaxyViewSettled);
            CameraController.Instance.EnterGalaxyView(zoneConfig.galaxyCameraTarget, zoneConfig.galaxyCameraZoom, zoneConfig.galaxyCameraRotX, zoneConfig.galaxyCameraRotY);
        }
        else
        {
            EnterZone(zoneNumber, ComputeZoneSeed(zoneNumber));
        }
    }

    public override void OnTabDeactivated()
    {
        EventManager.Unsubscribe_GalaxyViewSettled(OnGalaxyViewSettled);
        RefreshTabButtons();
        if (CameraController.Instance == null) return;

        if (m_pendingCellEntry == true)
        {
            EventManager.Subscribe_FleetViewRestored(OnFleetViewRestoredForCellEntry);
            CameraController.Instance.ExitGalaxyView(m_pendingCellWorldPos, ignoreFleetTarget: true);
        }
        else
        {
            SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
            Vector3 returnPos = myFleet != null ? myFleet.transform.position : Vector3.zero;
            CameraController.Instance.ExitGalaxyView(returnPos);
        }
    }

    // 카메라가 목표 자세(줌/각도)에 완전히 도달한 시점 — 이제부터 버튼 화면 위치→3D 좌표 역산이 정확함
    private void OnGalaxyViewSettled()
    {
        EventManager.Unsubscribe_GalaxyViewSettled(OnGalaxyViewSettled);

        // EnterZone이 셀 버튼을 생성/활성화하면서 코루틴(깜빡임 등)을 시작하므로, 패널(및 조상 계층)이 실제로 활성화된 뒤에 호출해야 함
        if (m_tabSystemParent != null)
            m_tabSystemParent.RevealDeferredPanel(m_tabSystemParent.GetCurrentActiveTab());

        EnterZone(m_pendingZoneNumber, ComputeZoneSeed(m_pendingZoneNumber));
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

    // 존 전환 — 구식 시스템도 존마다 갤럭시뷰 카메라를 재이동시키지 않았음(galaxyCameraTarget이 대부분 공유 지점이라 카메라는 고정,
    // 그 아래 배치되는 셀레스티얼/버튼만 교체)
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

        ObjectManager.Instance.ChangeZone(zoneNumber);
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
        return CommonUtility.ComputeExplorationZoneSeed(zoneNumber, seedBase);
    }

    public void EnterZone(int zoneNumber, int seed)
    {
        ZoneConfig zoneConfig = DataManager.Instance.m_dataTableZone.GetZoneByZoneIndex(zoneNumber);

        m_currentZoneNumber = zoneNumber;
        m_currentSeed = seed;
        m_gridData = ExplorationGridGenerator.Generate(seed, zoneConfig != null ? zoneConfig.gridWidth : 3, zoneConfig != null ? zoneConfig.gridHeight : 3);
        m_currentX = m_gridData.startX;
        m_currentY = m_gridData.startY;

        BuildCellButtons();
        BuildCellEnemyFleets(zoneConfig);
        RefreshCellStates();
    }

    // 통행 가능한 셀마다 적함대 웨이브 구성을 즉석 계산해 캐싱 — (zoneConfig, seed, x, y) 결정론적이라 재계산해도 항상 동일
    private void BuildCellEnemyFleets(ZoneConfig zoneConfig)
    {
        m_cellEnemyWaves.Clear();
        if (zoneConfig == null) return;

        for (int x = 0; x < m_gridData.width; x++)
        {
            for (int y = 0; y < m_gridData.height; y++)
            {
                GridCellData cellData = m_gridData.GetCell(x, y);
                if (cellData.isBlocked || cellData.isStart || cellData.isEscape || cellData.isEmpty) continue;

                List<TempFleetInfo> waves = ExplorationEnemyFleetGenerator.GenerateWaves(
                    zoneConfig, m_currentSeed, x, y, m_shipPresetTable);
                m_cellEnemyWaves[(x, y)] = waves;
            }
        }
    }

    // 셀의 전체 웨이브 목록 — 전투 시스템(§1-3 6번, 아직 미구현)에서 웨이브 순차 진행에 사용 예정
    public List<TempFleetInfo> GetCellEnemyWaves(int x, int y)
    {
        m_cellEnemyWaves.TryGetValue((x, y), out List<TempFleetInfo> waves);
        return waves;
    }

    // 그리드 전체를 CellRoot(화면 중앙 앵커) 기준으로 가운데 정렬하기 위한 보정값
    private Vector2 ComputeGridOriginOffset()
    {
        return new Vector2(-(m_gridData.width - 1) * m_cellSize * 0.5f, -(m_gridData.height - 1) * m_cellSize * 0.5f);
    }

    // 셀(x,y)의 3D 월드 좌표 — 실제 버튼의 화면 위치에서 카메라 광선을 Y=k_fleetWorldY 평면에 쏴서 역산(항상 화면에 보이는 그대로의 정확한 위치)
    private Vector3 ResolveCellWorldPosition(int x, int y)
    {
        if (m_activeButtons.TryGetValue((x, y), out GridCellButton button) == false) return Vector3.zero;
        Camera cam = CameraController.Instance != null ? CameraController.Instance.m_targetCamera : Camera.main;
        return CommonUtility.RaycastScreenPointToGroundPlane(cam, button.GetScreenPosition(), k_fleetWorldY);
    }

    // 그리드 UI를 연 적이 없어도(예: 로그인 시 초기 함대 배치) 특정 셀의 3D 월드 좌표를 정확히 계산 —
    // 실제 버튼을 만들지 않고 m_cellRoot(비활성 상태여도 좌표 변환은 유효)로 화면 위치를 구한 뒤, 갤럭시뷰 카메라 자세를 순간 시뮬레이션해서 레이캐스트
    public Vector3 ComputeCellWorldPositionWithoutOpening(int zoneNumber, int x, int y, int gridWidth, int gridHeight)
    {
        if (m_cellRoot == null || CameraController.Instance == null) return Vector3.zero;

        ZoneConfig zoneConfig = DataManager.Instance.m_dataTableZone.GetZoneByZoneIndex(zoneNumber);
        if (zoneConfig == null) return Vector3.zero;

        Vector2 originOffset = new Vector2(-(gridWidth - 1) * m_cellSize * 0.5f, -(gridHeight - 1) * m_cellSize * 0.5f);
        Vector2 anchoredPos = new Vector2(x * m_cellSize + originOffset.x, y * m_cellSize + originOffset.y);
        Vector3 screenPoint = m_cellRoot.TransformPoint(new Vector3(anchoredPos.x, anchoredPos.y, 0f));

        Vector3 result = Vector3.zero;
        CameraController.Instance.SimulateGalaxyViewPose(zoneConfig.galaxyCameraTarget, zoneConfig.galaxyCameraZoom, zoneConfig.galaxyCameraRotX, zoneConfig.galaxyCameraRotY, () =>
        {
            result = CommonUtility.RaycastScreenPointToGroundPlane(CameraController.Instance.m_targetCamera, screenPoint, k_fleetWorldY);
        });
        return result;
    }

    private void BuildCellButtons()
    {
        ReturnAllButtonsToPool();

        Vector2 originOffset = ComputeGridOriginOffset();

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

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title = $"{m_currentZoneNumber} ({x}, {y})",
            message = LocalizationManager.Instance.Get("UIPopupMessage_ConfirmTryCell"),
            onConfirm = () => ConfirmEnterCell(x, y),
            onCancel = () => { }
        });
    }

    // 확인 팝업 승인 — 탭을 닫아 OnTabDeactivated가 갤럭시뷰 해제(ExitGalaxyView)를 시작하게 함.
    // 실제 함대 이동/워프인은 카메라 복귀 완료(FleetViewRestored) 이후 OnFleetViewRestoredForCellEntry에서 처리
    private void ConfirmEnterCell(int x, int y)
    {
        m_pendingCellEntry = true;
        m_pendingCellX = x;
        m_pendingCellY = y;
        m_pendingCellWorldPos = ResolveCellWorldPosition(x, y);

        if (m_tabSystemParent != null)
            m_tabSystemParent.CloseAllTabs();
    }

    private void OnFleetViewRestoredForCellEntry()
    {
        EventManager.Unsubscribe_FleetViewRestored(OnFleetViewRestoredForCellEntry);
        m_pendingCellEntry = false;

        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet == null) return;

        m_previousX = m_currentX;
        m_previousY = m_currentY;
        m_previousFleetWorldPos = myFleet.transform.position;

        m_currentX = m_pendingCellX;
        m_currentY = m_pendingCellY;

        ObjectManager.Instance.SetMyFleetPosition(m_pendingCellWorldPos, 0f);
        myFleet.StartFleetWarpIn(() => SpawnEnemyFleetAndWarpIn(m_currentX, m_currentY, m_pendingCellWorldPos));
    }

    // 내 함대 워프인 완료 후 호출 — 캐싱된 적함대 웨이브의 0번을 스폰
    // TODO: 2번째 이후 웨이브는 이전 웨이브 전멸 시 이어서 스폰해야 함 — 전투 시스템(§1-3 6번) 미구현이라 아직 트리거 불가
    private void SpawnEnemyFleetAndWarpIn(int x, int y, Vector3 myFleetPos)
    {
        List<TempFleetInfo> waves = GetCellEnemyWaves(x, y);
        TempFleetInfo enemyFleetInfo = waves != null && waves.Count > 0 ? waves[0] : null;
        if (enemyFleetInfo == null || enemyFleetInfo.ships == null || enemyFleetInfo.ships.Count == 0)
            return; // 빈 셀(적 없음) — 탐험 포인트 지급 등은 후속 작업

        Vector3 enemyPos = myFleetPos + Vector3.forward * k_enemyEncounterDistance;
        Quaternion enemyRot = Quaternion.LookRotation(myFleetPos - enemyPos);

        ETeam enemyTeam = ObjectManager.Instance.GetOpposingTeam(ObjectManager.Instance.m_myTeam);
        SpaceFleet enemyFleet = ObjectManager.Instance.SpawnFleetFromPreset(enemyFleetInfo, enemyTeam, EFleetSource.fleet_source_zone_data, enemyPos, enemyRot, "EnemyFleet");
        m_standoffEnemyFleet = enemyFleet;
        enemyFleet.StartFleetWarpIn(ShowPrepareBattlePanel); // 함대 단위 워프인 — 구식 ObjectManager.SpawnWave와 동일 패턴
    }

    // 내 함대 + 적 함대 워프인이 모두 끝난 시점 — 대치 상태로 전환, 전투시작/퇴각/함대설정 3버튼 노출
    private void ShowPrepareBattlePanel()
    {
        UIPanelPrepareBattle panel = UIManager.Instance.GetPanel<UIPanelPrepareBattle>("UIPanelPrepareBattle");
        if (panel == null) return;

        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        panel.Open(myFleet, m_standoffEnemyFleet, OnConfirmStartBattle, OnConfirmRetreat);
    }

    // 전투시작 확정 — 전투 시스템(§1-3 6번) 자체는 미구현이라, 우선 양 함대를 실제 교전 상태로 전환하는 부분까지만 연결
    private void OnConfirmStartBattle()
    {
        if (m_standoffEnemyFleet == null) return;
        ObjectManager.Instance.TryStartCombat(m_standoffEnemyFleet, EUnitState.BattleExploration);
        m_standoffEnemyFleet = null;
    }

    // 퇴각 — 적 함대만 제거하고 내 함대를 대치 진입 직전 위치/셀로 복귀
    private void OnConfirmRetreat()
    {
        if (m_standoffEnemyFleet != null)
        {
            ObjectManager.Instance.RemoveEnemyFleet(m_standoffEnemyFleet);
            m_standoffEnemyFleet = null;
        }

        m_currentX = m_previousX;
        m_currentY = m_previousY;
        ObjectManager.Instance.SetMyFleetPosition(m_previousFleetWorldPos, 0f);
        CameraController.Instance.SnapToTarget(); // 함대가 순간이동했으므로 카메라도 즉시 스냅 — 워프인 때는 ExitGalaxyView가 미리 그 위치로 이동해둬서 필요 없었지만, 퇴각은 카메라 이동 없이 함대만 텔레포트되므로 별도 스냅 필요
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
            if (cellData.isBlocked) state = EGridCellVisualState.Blocked; // 통행불가 셀은 인접해도 항상 숨김
            else if (isCurrentCell) state = EGridCellVisualState.Current;
            else if (isAdjacent) state = EGridCellVisualState.Reachable;
            else if (cellData.isCleared) state = EGridCellVisualState.Cleared;
            else state = EGridCellVisualState.Unvisited;

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
