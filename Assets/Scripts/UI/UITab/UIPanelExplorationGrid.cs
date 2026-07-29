// 탐사 그리드 패널 — 그리드 생성 + 셀 배치 + 인접 이동 + 갤럭시뷰 연동 + 셀 진입(워프인) 담당. UIManager가 관리하는 독립 패널(다른 진입 화면과 배타적)
// 패널 자체(존 탭/포인트 표시 등)는 열리자마자 바로 보임 — 그리드 셀 버튼만 갤럭시뷰 카메라가 실제로 정착한 시점(SettleZoneEntry)에 생성됨.
// 이전엔 CanvasGroup 알파로 패널 전체를 가려뒀었으나, 버튼을 미리 비우는 것만으로 충분해 제거함(향후 버튼 순차/랜덤 등장 연출의 선행 작업)
using System.Collections.Generic;
using UnityEngine;

public class UIPanelExplorationGrid : UIPanelBase
{
    [SerializeField] private GridCellButton m_cellButtonPrefab;
    [SerializeField] private RectTransform m_cellRoot;
    [SerializeField] private DataTableShipPreset m_shipPresetTable;
    [SerializeField] private float m_cellSize = 200f; // 화면상 셀 간격(px) — 고정, 3D 좌표는 이 화면 위치에서 카메라 광선을 Y=0 평면에 쏴서 역산
    [SerializeField] private UnityEngine.UI.Button m_backgroundCloseButton; // 빈 곳 클릭 시 패널 닫기용 투명 풀스크린 버튼

    [Header("탐험 포인트 / 런 포기")]
    [SerializeField] private RowLabelValue m_bankedExplorationPointRow; // 진행 중인 런의 적립(미확정) 탐험 포인트 상시 표시
    [SerializeField] private RowLabelValue m_ownedExplorationPointRow; // 확정 지급되어 실제 보유 중인 탐험 포인트(지휘력 증가 등에 소모하는 값) 상시 표시
    [SerializeField] private UnityEngine.UI.Button m_abandonRunButton; // 진행 중인 런을 포기(적립분의 50%만 확정 지급)

    [Header("존 선택 스크롤")]
    [SerializeField] private InfiniteScrollViewH m_zoneTabScroll;
    [SerializeField] private GameObject m_zoneTabNodePrefab;
    [SerializeField] private UnityEngine.UI.Button m_zoneNavPrev;
    [SerializeField] private UnityEngine.UI.Button m_zoneNavNext;
    // 락/클리어 표시 등 실제 진행도 연동은 후속 작업(clearedZones가 아직 구식 스테이지 포맷)

    private const float k_fleetWorldY = 0f;             // 함대 레이어 — 천체는 pos_y=-550으로 분리(별도 작업)
    private const float k_enemyEncounterDistance = 50f; // 셀 안 전투 조우 거리 — datatable_zone_enemy_fleet_position.csv grade1과 동일 스케일(국소 전술 거리, 갤럭시뷰 좌표와는 별개)

    private int m_zoneGroupCount;
    private int m_pendingZoneNumber;

    private ExplorationGridData m_gridData;
    private int m_currentZoneNumber;
    private int m_currentSeed;
    private int m_currentRow;
    private int m_currentCol;

    private bool m_pendingCellEntry;
    private int m_pendingCellRow;
    private int m_pendingCellCol;
    private Vector3 m_pendingCellWorldPos;

    // 대치 상태(UIPanelPrepareBattle) 중 퇴각 시 되돌아갈 이전 위치 — 로컬뷰 카메라 전환 이후엔 그리드 좌표→월드좌표 역산이 불가능(카메라 자세가 이미 바뀜)하므로 이동 직전 좌표를 스냅샷
    private int m_previousRow;
    private int m_previousCol;
    private Vector3 m_previousFleetWorldPos;
    private SpaceFleet m_standoffEnemyFleet;

    private int m_bankedExplorationPoint; // 진행 중인 런의 적립(미확정) 탐험 포인트 — 클리어마다 누적, 탈출/포기 확정 시 0으로 리셋
    private int m_pendingBankedPointGain; // 전투 중(패널 비활성 상태)에 획득해서 아직 화면에 반영 안 한 적립량 — 패널이 열릴 때 한 번에 반영+애니메이션
    private long m_displayedBankedPoint = -1; // 롤링 애니메이션의 시작값 추적 — 최초 1회는 -1이라 즉시 표시됨(UIResourceBar와 동일 패턴)
    private long m_displayedOwnedPoint = -1;

    private readonly List<GridCellButton> m_buttonPool = new();
    private readonly Dictionary<(int row, int col), GridCellButton> m_activeButtons = new();
    private readonly Dictionary<(int row, int col), List<FleetInfo>> m_cellEnemyWaves = new(); // 셀별 순차 웨이브 — 저장하지 않고 메모리 캐싱만

    // ObjectManager가 그리드 UI를 연 적 없이 로그인 시 초기 함대 위치를 계산할 때, 여기 설정된 값과 동일한 스케일을 쓰기 위해 참조
    public float GetCellSizePixels() { return m_cellSize; }

    public override void InitializeUIPanel()
    {
        EventManager.Subscribe_ZoneStageBattleEnd(OnZoneStageBattleEnd);

        if (m_abandonRunButton != null)
            m_abandonRunButton.onClick.AddListener(OnAbandonRunButtonClicked);
    }

    public override void OnShowUIPanel()
    {
        int zoneNumber = ObjectManager.Instance.GetInitialZoneIndex();

        // 존/그리드가 실제로 정착하기 전(카메라 갤럭시뷰 전환 중)에는 m_currentZoneNumber를 건드리지 않음 —
        // SelectZoneTab의 preservePosition 판정이 "직전에 로드돼있던 존이 무엇인지"를 정확히 알아야 하므로(조기 대입 시 오판 버그 발생)
        m_pendingZoneNumber = zoneNumber;

        // 존 탭/포인트 표시/버튼/그리드 버튼 전부 SettleZoneEntry(카메라 정착 후)에서 한 번에 세팅됨 —
        // 그 전까지 이전 존의 잔상이 보이지 않도록 버튼만 즉시 비우고, 포기 버튼/존 탭은 기본값(비표시)으로 되돌려둠
        ReturnAllButtonsToPool();
        if (m_abandonRunButton != null)
            m_abandonRunButton.gameObject.SetActive(false);
        if (m_zoneTabScroll != null)
            m_zoneTabScroll.gameObject.SetActive(false);

        m_pendingCellEntry = false;

        if (m_backgroundCloseButton != null)
        {
            m_backgroundCloseButton.onClick.RemoveAllListeners();
            m_backgroundCloseButton.onClick.AddListener(() => UIManager.Instance.HideCurrentPanel());
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
            SettleZoneEntry(zoneNumber);
        }
    }

    public override void OnHideUIPanel()
    {
        EventManager.Unsubscribe_GalaxyViewSettled(OnGalaxyViewSettled);
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
        SettleZoneEntry(m_pendingZoneNumber);
    }

    // "실제로 이 존에 정착한다"는 단일 지점 — 존 탭/보유 포인트/그리드(좌표·적립포인트·버튼)를 전부 이 시점에 한 번에 확정
    private void SettleZoneEntry(int zoneNumber)
    {
        // 전투 중(패널 비활성 상태)에 획득한 적립 포인트는 m_pendingBankedPointGain에 쌓여있음 —
        // GameObject가 active인 지금 한 번에 반영해야 롤링 애니메이션 코루틴이 실제로 재생됨
        if (m_pendingBankedPointGain > 0)
        {
            m_bankedExplorationPoint += m_pendingBankedPointGain;
            m_pendingBankedPointGain = 0;
        }
        RefreshOwnedPointText();

        // SelectZoneTab이 m_currentZoneNumber를 확정한 뒤에 탭 스크롤을 초기화해야 OnZoneTabNodeBind의 하이라이트 판정이 정확함
        SelectZoneTab(zoneNumber, ComputeZoneSeed(zoneNumber));
        InitializeZoneTabScroll(zoneNumber);
    }

    private void InitializeZoneTabScroll(int initialZoneNumber)
    {
        if (m_zoneTabScroll == null || m_zoneTabNodePrefab == null) return;

        m_zoneTabScroll.gameObject.SetActive(true); // OnShowUIPanel에서 정착 전까지 비활성화해둔 것을 복원
        m_zoneGroupCount = DataManager.Instance.m_dataTableZone.zoneList.Count; // 탭 개수는 데이터테이블에 정의된 실제 존 개수 그대로

        // Initialize()/ScrollToCenter()는 스크롤 위치를 리셋/이동시키면서 그 위치 기준으로 onCenterIndexChanged를 동기적으로 쏨 —
        // 콜백이 미리 연결돼 있으면 이 초기화 과정에서 스퓨리어스하게 NavigateToZone(재진입으로 SelectZoneTab까지 재호출)이 발동해버림.
        // 그래서 목표 존으로 정확히 자리잡을 때까지는 콜백을 연결하지 않고, 자리잡은 뒤에만 연결(이후엔 진짜 사용자 스크롤에만 반응)
        m_zoneTabScroll.onItemBind = OnZoneTabNodeBind;
        m_zoneTabScroll.onCenterIndexChanged = null;
        m_zoneTabScroll.Initialize(m_zoneGroupCount, m_zoneTabNodePrefab);
        m_zoneTabScroll.ScrollToCenter(initialZoneNumber - 1);
        m_zoneTabScroll.onCenterIndexChanged = OnZoneScrollCenterChanged;

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
        SelectZoneTab(zoneNumber, ComputeZoneSeed(zoneNumber));
    }

    private void RefreshZoneNavButtons(int zoneNumber)
    {
        if (m_zoneNavPrev != null) m_zoneNavPrev.interactable = zoneNumber > 1;
        if (m_zoneNavNext != null) m_zoneNavNext.interactable = m_zoneGroupCount <= 0 || zoneNumber < m_zoneGroupCount;
    }

    // 로그인 시 받은 유저 무관 공통 시드(explorationSeedBase — 모든 유저 동일)와 zoneNumber를 조합 — 서버 재요청 없이 클라에서 결정론적으로 계산
    // 모든 유저가 같은 Zone/셀에서 같은 적함대를 만나야 하므로 유저별로 달라지면 안 됨(난이도 공정성)
    private int ComputeZoneSeed(int zoneNumber)
    {
        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        int seedBase = commanderInfo != null ? commanderInfo.explorationSeedBase : 0;
        return CommonUtility.ComputeExplorationZoneSeed(zoneNumber, seedBase);
    }

    public void SelectZoneTab(int zoneNumber, int seed)
    {
        ZoneConfig zoneConfig = DataManager.Instance.m_dataTableZone.GetZoneByZoneIndex(zoneNumber);

        // 전투 승리 후 그리드 복귀(ShowPanel 재호출)처럼 같은 존 재진입 시에는 시작 좌표로 리셋하지 않고
        // 직전에 있던 셀 좌표를 유지 — 존 자체가 바뀌면(최초 진입/존 이동) 시작 좌표로 초기화
        // 그리드 레이아웃은 이제 고정 데이터라 seed와 무관 — zoneNumber만 같으면 동일 그리드
        bool preservePosition = m_gridData != null && m_currentZoneNumber == zoneNumber;
        int preservedRow = m_currentRow;
        int preservedCol = m_currentCol;

        m_currentZoneNumber = zoneNumber;
        m_currentSeed = seed;
        m_gridData = ExplorationGridGenerator.Generate(zoneConfig);

        if (preservePosition == true && m_gridData.IsInBounds(preservedRow, preservedCol) == true)
        {
            m_currentRow = preservedRow;
            m_currentCol = preservedCol;
        }
        else if (IsActiveExplorationZone(zoneNumber) == true
            && ObjectManager.Instance.TryGetActiveExplorationCell(out int activeRow, out int activeCol) == true
            && m_gridData.IsInBounds(activeRow, activeCol) == true)
        {
            // 세션 내 메모리 좌표가 없는 첫 진입(재접속/재오픈)이면서 진입하려는 존이 서버에 저장된 진행 중인 런의 존과 같은 경우 —
            // 마지막 클리어 셀에서 시작 (다른 존으로 처음 진입하는 경우엔 이 좌표를 쓰면 안 됨)
            m_currentRow = activeRow;
            m_currentCol = activeCol;
        }
        else
        {
            m_currentRow = m_gridData.startRow;
            m_currentCol = m_gridData.startCol;
        }

        // 존 자체가 바뀌는 진입(재진입 제외)에서는 로컬 적립 표시를 일단 비움 — 진행 중인 런이면 아래 서버 응답으로 곧 채워짐
        if (preservePosition == false)
        {
            m_bankedExplorationPoint = 0;
            RefreshBankedPointText();
        }

        BuildCellButtons();
        BuildCellEnemyFleets(zoneConfig);
        RefreshCellStates();
        RefreshAbandonRunButtonState();

        // 세션 내 메모리 좌표가 없는 첫 진입이면서 서버에 진행 중인 런이 있는 존이면, 지나온 셀들의 방문 표시도 복구
        if (preservePosition == false && IsActiveExplorationZone(zoneNumber) == true)
            RequestActiveZoneRunProgress();
    }

    // 서버에 저장된 "진행 중인 탐험 런"의 존 번호와 일치하는지 — 다른 존에 처음 진입할 때 엉뚱한 좌표를 쓰지 않도록 방지
    private bool IsActiveExplorationZone(int zoneNumber)
    {
        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        return commanderInfo != null && commanderInfo.explorationZoneNumber == zoneNumber;
    }

    // 진행 중인 런의 클리어 셀 목록을 받아와 그리드 방문 표시를 복구 — SpaceScene 재로드로 m_gridData가 초기화된 직후에만 호출됨
    private void RequestActiveZoneRunProgress()
    {
        int requestedZoneNumber = m_currentZoneNumber;
        NetworkManager.Instance.GetActiveZoneRunProgress(new GetActiveZoneRunProgressRequest(), response => OnGetActiveZoneRunProgressResponse(response, requestedZoneNumber));
    }

    private void OnGetActiveZoneRunProgressResponse(ApiResponse<GetActiveZoneRunProgressResponse> response, int requestedZoneNumber)
    {
        if (response == null || response.errorCode != 0 || response.data == null) return;
        // 응답 도착 전 유저가 이미 다른 존으로 이동했으면 지금 그리드와 무관한 데이터이므로 버림
        if (requestedZoneNumber != m_currentZoneNumber || response.data.zoneNumber != m_currentZoneNumber) return;

        m_bankedExplorationPoint = response.data.explorationPointBanked;
        RefreshBankedPointText();
        RefreshAbandonRunButtonState();

        string[] clearedCells = response.data.clearedCells;
        if (clearedCells == null) return;

        for (int i = 0; i < clearedCells.Length; i++)
        {
            int dashIdx = clearedCells[i].IndexOf('-');
            if (dashIdx <= 0) continue;

            bool rowParsed = int.TryParse(clearedCells[i].Substring(0, dashIdx), out int row);
            bool colParsed = int.TryParse(clearedCells[i].Substring(dashIdx + 1), out int col);
            if (rowParsed == true && colParsed == true && m_gridData.IsInBounds(row, col) == true)
                m_gridData.SetCellCleared(row, col, true);
        }

        RefreshCellStates();
    }

    // 통행 가능한 셀마다 적함대 웨이브 구성을 즉석 계산해 캐싱 — (zoneConfig, seed, x, y) 결정론적이라 재계산해도 항상 동일
    private void BuildCellEnemyFleets(ZoneConfig zoneConfig)
    {
        m_cellEnemyWaves.Clear();
        if (zoneConfig == null) return;

        for (int row = 0; row < m_gridData.height; row++)
        {
            for (int col = 0; col < m_gridData.width; col++)
            {
                GridCellData cellData = m_gridData.GetCell(row, col);
                // 탈출 셀은 적을 클리어해야 탈출 확정 — 일반 셀과 동일하게 적함대 생성 대상에 포함
                if (cellData.isBlocked || cellData.isStart || cellData.isEvent) continue;

                List<FleetInfo> waves = ExplorationEnemyFleetGenerator.GenerateWaves(
                    zoneConfig, m_currentSeed, row, col, m_shipPresetTable);
                m_cellEnemyWaves[(row, col)] = waves;
            }
        }
    }

    // 셀의 전체 웨이브 목록 — 전투 시스템(§1-3 6번, 아직 미구현)에서 웨이브 순차 진행에 사용 예정
    public List<FleetInfo> GetCellEnemyWaves(int row, int col)
    {
        m_cellEnemyWaves.TryGetValue((row, col), out List<FleetInfo> waves);
        return waves;
    }

    // 그리드 전체를 CellRoot(화면 중앙 앵커) 기준으로 가운데 정렬하기 위한 보정값
    private Vector2 ComputeGridOriginOffset()
    {
        return new Vector2(-(m_gridData.width - 1) * m_cellSize * 0.5f, -(m_gridData.height - 1) * m_cellSize * 0.5f);
    }

    // 셀(row,col)의 3D 월드 좌표 — 실제 버튼의 화면 위치에서 카메라 광선을 Y=k_fleetWorldY 평면에 쏴서 역산(항상 화면에 보이는 그대로의 정확한 위치)
    private Vector3 ResolveCellWorldPosition(int row, int col)
    {
        if (m_activeButtons.TryGetValue((row, col), out GridCellButton button) == false) return Vector3.zero;
        Camera cam = CameraController.Instance != null ? CameraController.Instance.m_targetCamera : Camera.main;
        return CommonUtility.RaycastScreenPointToGroundPlane(cam, button.GetScreenPosition(), k_fleetWorldY);
    }

    // 그리드 UI를 연 적이 없어도(예: 로그인 시 초기 함대 배치) 특정 셀의 3D 월드 좌표를 정확히 계산 —
    // 실제 버튼을 만들지 않고 m_cellRoot(비활성 상태여도 좌표 변환은 유효)로 화면 위치를 구한 뒤, 갤럭시뷰 카메라 자세를 순간 시뮬레이션해서 레이캐스트
    public Vector3 ComputeCellWorldPositionWithoutOpening(int zoneNumber, int row, int col, int gridWidth, int gridHeight)
    {
        if (m_cellRoot == null || CameraController.Instance == null) return Vector3.zero;

        ZoneConfig zoneConfig = DataManager.Instance.m_dataTableZone.GetZoneByZoneIndex(zoneNumber);
        if (zoneConfig == null) return Vector3.zero;

        Vector2 originOffset = new Vector2(-(gridWidth - 1) * m_cellSize * 0.5f, -(gridHeight - 1) * m_cellSize * 0.5f);
        Vector2 anchoredPos = new Vector2(col * m_cellSize + originOffset.x, row * m_cellSize + originOffset.y);
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

        for (int row = 0; row < m_gridData.height; row++)
        {
            for (int col = 0; col < m_gridData.width; col++)
            {
                GridCellData cellData = m_gridData.GetCell(row, col);
                GridCellButton button = GetButtonFromPool();
                button.gameObject.SetActive(true);
                button.Initialize(cellData, OnCellClicked);
                button.SetAnchoredPosition(m_cellSize, originOffset);
                m_activeButtons[(row, col)] = button;
            }
        }
    }

    private void OnCellClicked(int row, int col)
    {
        Debug.Log($"[DEBUG-CELL] OnCellClicked row={row} col={col} (currentRow={m_currentRow} currentCol={m_currentCol})");
        bool isCurrentCell = row == m_currentRow && col == m_currentCol;
        bool isAdjacent = m_gridData.IsAdjacent(m_currentRow, m_currentCol, row, col);

        if (isCurrentCell == false && isAdjacent == false) return;
        if (isCurrentCell == false && m_gridData.GetCell(row, col).isBlocked) return;

        if (isCurrentCell)
        {
            // 탈출 셀에 서 있는 상태에서 재클릭 — 탈출 확정 팝업 재호출(취소 후 다시 결정하고 싶을 때)
            if (m_gridData.GetCell(row, col).isEscape == true)
                ShowEscapeConfirmPopup();
            return;
        }

        // 지금 보고 있는 존이 아닌 다른 존에 진행 중인 런이 있으면, 일반 입장 확인 대신
        // 그 런을 포기할지부터 먼저 물어봄(전투 준비 화면까지 갔다가 서버가 거부하는 것보다 먼저 걸러냄)
        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        bool hasAnotherZoneRun = commanderInfo != null
            && commanderInfo.explorationZoneNumber != 0
            && commanderInfo.explorationZoneNumber != m_currentZoneNumber;
        if (hasAnotherZoneRun == true)
        {
            ShowSwitchZoneAbandonPopup(row, col);
            return;
        }

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message = LocalizationManager.Instance.Get("UIPopupMessage_ConfirmTryCell"),
            onConfirm = () => ConfirmEnterCell(row, col),
            onCancel = () => { }
        });
    }

    // 다른 존에 진행 중인 런의 최신 정보(존/셀/적립량)를 서버에서 받아와 포기 여부를 묻는 팝업 표시
    // — 로컬 캐시(m_bankedExplorationPoint)는 그 존을 벗어나며 리셋되므로 여기선 신뢰할 수 없어 서버에 재조회
    private void ShowSwitchZoneAbandonPopup(int newRow, int newCol)
    {
        NetworkManager.Instance.GetActiveZoneRunProgress(new GetActiveZoneRunProgressRequest(), response =>
        {
            if (response.errorCode != 0 || response.data == null)
            {
                Debug.LogError($"[UIPanelExplorationGrid] GetActiveZoneRunProgress 실패: {response.errorCode}");
                return;
            }

            int otherZoneNumber = response.data.zoneNumber;
            int otherBanked = response.data.explorationPointBanked;
            string[] clearedCells = response.data.clearedCells;
            string lastCell = (clearedCells != null && clearedCells.Length > 0) ? clearedCells[^1] : null;
            string otherCellDisplay = FormatCellForDisplay(lastCell);

            UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
            {
                message = "진행 중인 다른 탐험이 있습니다.\n"
                    + $"존 {otherZoneNumber} / 구역 {otherCellDisplay} / 적립된 탐험 포인트 {CommonUtility.FormatNumber(otherBanked)}\n"
                    + "포기하고 선택한 존에 새로 도전하시겠습니까?\n"
                    + "* 포기 시 적립된 포인트의 50%만 회수 가능합니다",
                onConfirm = () => ConfirmAbandonThenEnterCell(newRow, newCol),
                onCancel = () => { }
            });
        });
    }

    // 서버 셀 좌표("row-col", 0-indexed)를 유저에게 보여줄 1-indexed 표기로 변환 — GridCellData/ZoneRun 등 내부 좌표계는 항상 0-indexed 유지, 표시할 때만 +1
    private string FormatCellForDisplay(string cell)
    {
        if (string.IsNullOrEmpty(cell)) return "-";

        int dashIdx = cell.IndexOf('-');
        if (dashIdx <= 0) return cell;

        bool rowParsed = int.TryParse(cell.Substring(0, dashIdx), out int row);
        bool colParsed = int.TryParse(cell.Substring(dashIdx + 1), out int col);
        if (rowParsed == false || colParsed == false) return cell;

        return $"{row + 1}-{col + 1}";
    }

    // 다른 존 런을 포기하고, 곧바로 지금 클릭한 셀로 새 런 진입을 확정
    private void ConfirmAbandonThenEnterCell(int row, int col)
    {
        NetworkManager.Instance.AbandonZoneRun(new AbandonZoneRunRequest(), response =>
        {
            if (response.errorCode != 0)
            {
                Debug.LogError($"[UIPanelExplorationGrid] AbandonZoneRun 실패: {response.errorCode}");
                return;
            }

            ApplyOwnedPointRemain(response.data.explorationPointRemain);
            ClearActiveRunZoneCache();

            ConfirmEnterCell(row, col);
        });
    }

    // 확인 팝업 승인 — 패널을 닫아 OnHideUIPanel이 갤럭시뷰 해제(ExitGalaxyView)를 시작하게 함.
    // 실제 함대 이동/워프인은 카메라 복귀 완료(FleetViewRestored) 이후 OnFleetViewRestoredForCellEntry에서 처리
    private void ConfirmEnterCell(int row, int col)
    {
        m_pendingCellEntry = true;
        m_pendingCellRow = row;
        m_pendingCellCol = col;
        m_pendingCellWorldPos = ResolveCellWorldPosition(row, col);

        // false: 카메라가 로컬뷰로 복귀하고 적 함대가 스폰될 때까지는 UIPanelPrepareBattle을 곧 띄울 예정이므로,
        // 그 사이에 메인 패널(UIPanelSpace)이 잠깐 비쳤다 사라지는 걸 막음
        UIManager.Instance.HideCurrentPanel(showMainPanelIfEmpty: false);
    }

    private void OnFleetViewRestoredForCellEntry()
    {
        EventManager.Unsubscribe_FleetViewRestored(OnFleetViewRestoredForCellEntry);
        m_pendingCellEntry = false;

        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet == null) return;

        m_previousRow = m_currentRow;
        m_previousCol = m_currentCol;
        m_previousFleetWorldPos = myFleet.transform.position;

        m_currentRow = m_pendingCellRow;
        m_currentCol = m_pendingCellCol;

        ObjectManager.Instance.SetMyFleetPosition(m_pendingCellWorldPos, 0f);
        SpawnEnemyFleetAndShowPrepareBattle(m_currentRow, m_currentCol, m_pendingCellWorldPos, myFleet);
    }

    // 적 함대를 즉시 스폰하고 대치 패널(UIPanelPrepareBattle)을 바로 띄움 — 워프인 연출(StartFleetWarpIn)은
    // 패널 표시를 기다리지 않고 별도로 재생(연출이 끝나야 버튼이 눌리는 게 아니라, 대치 상태 자체는 이미 확정된 것이므로)
    // TODO: 2번째 이후 웨이브는 이전 웨이브 전멸 시 이어서 스폰해야 함 — 전투 시스템(§1-3 6번) 미구현이라 아직 트리거 불가
    private void SpawnEnemyFleetAndShowPrepareBattle(int row, int col, Vector3 myFleetPos, SpaceFleet myFleet)
    {
        List<FleetInfo> waves = GetCellEnemyWaves(row, col);
        FleetInfo enemyFleetInfo = waves != null && waves.Count > 0 ? waves[0] : null;
        if (enemyFleetInfo == null || enemyFleetInfo.ships == null || enemyFleetInfo.ships.Count == 0)
        {
            myFleet.StartFleetWarpIn();
            return; // 빈 셀(적 없음) — 탐험 포인트 지급 등은 후속 작업
        }

        Vector3 enemyPos = myFleetPos + Vector3.forward * k_enemyEncounterDistance;
        Quaternion enemyRot = Quaternion.LookRotation(myFleetPos - enemyPos);

        ETeam enemyTeam = ObjectManager.Instance.GetOpposingTeam(ObjectManager.Instance.m_myTeam);
        SpaceFleet enemyFleet = ObjectManager.Instance.SpawnFleetFromPreset(enemyFleetInfo, enemyTeam, EFleetSource.fleet_source_zone_data, enemyPos, enemyRot, "EnemyFleet");
        m_standoffEnemyFleet = enemyFleet;

        myFleet.StartFleetWarpIn();
        enemyFleet.StartFleetWarpIn();

        ShowPrepareBattlePanel(myFleet, enemyFleet);
    }

    // 대치 상태로 전환, 전투시작/퇴각/함대설정 3버튼 노출 — 워프인 연출 완료를 기다리지 않고 셀 확정 즉시 표시
    private void ShowPrepareBattlePanel(SpaceFleet myFleet, SpaceFleet enemyFleet)
    {
        UIPanelPrepareBattle panel = UIManager.Instance.GetPanel<UIPanelPrepareBattle>("UIPanelPrepareBattle");
        if (panel == null) return;

        panel.Open(myFleet, enemyFleet, OnConfirmStartBattle, OnConfirmRetreat);
    }

    // 전투시작 확정 — 서버에 셀 도전을 먼저 통지(EnterExplorationCell)하고 승인된 경우에만 실제 교전 상태로 전환
    private void OnConfirmStartBattle()
    {
        if (m_standoffEnemyFleet == null) return;

        EnterExplorationCellRequest request = new EnterExplorationCellRequest
        {
            zoneNumber = m_currentZoneNumber,
            cellRow = m_currentRow,
            cellCol = m_currentCol,
            fleetInfo = DataManager.Instance.m_currentFleetComposition != null
                ? DataManager.Instance.m_currentFleetComposition.ToNetworkFleetInfo()
                : null,
        };
        NetworkManager.Instance.EnterExplorationCell(request, OnEnterExplorationCellResponse);
    }

    private void OnEnterExplorationCellResponse(ApiResponse<EnterExplorationCellResponse> response)
    {
        if (response.errorCode == (int)ServerErrorCode.EXPLORATION_ANOTHER_ZONE_IN_PROGRESS)
        {
            ShowAbandonAnotherRunConfirmPopup();
            return;
        }

        if (response.errorCode != 0)
        {
            Debug.LogError($"[UIPanelExplorationGrid] EnterExplorationCell 실패: {response.errorCode}");
            return;
        }

        // 이 존에 런이 확정 시작됨 — 로컬 캐시도 즉시 갱신해야 이번 세션 안에서 "다른 존 진행중" 판정이 정확함
        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        if (commanderInfo != null)
            commanderInfo.explorationZoneNumber = m_currentZoneNumber;

        if (m_standoffEnemyFleet == null) return;
        ObjectManager.Instance.TryStartCombat(m_standoffEnemyFleet, EUnitState.BattleExploration);
        m_standoffEnemyFleet = null;
    }

    // 이미 다른 존에 IN_PROGRESS 런이 있을 때 — 포기 후 재시도 확인
    private void ShowAbandonAnotherRunConfirmPopup()
    {
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message = "다른 존에 진행 중인 탐험이 있습니다. 지금 포기하면 쌓인 탐험 포인트의 50%만 획득합니다. 포기하고 새로 도전하시겠습니까?",
            onConfirm = OnConfirmAbandonAnotherRunAndRetry,
            onCancel = () => { }
        });
    }

    private void OnConfirmAbandonAnotherRunAndRetry()
    {
        NetworkManager.Instance.AbandonZoneRun(new AbandonZoneRunRequest(), response =>
        {
            if (response.errorCode != 0)
            {
                Debug.LogError($"[UIPanelExplorationGrid] AbandonZoneRun 실패: {response.errorCode}");
                return;
            }
            OnConfirmStartBattle(); // 기존 런 정리 완료 — 원래 도전을 재시도
        });
    }

    // 전투 종료(승리/패배) 공통 이벤트 — 승리 시 서버에 클리어 통지 후 그리드(갤럭시뷰)로 복귀. 패배 처리는 범위 밖(추후 별도 작업)
    private void OnZoneStageBattleEnd(bool isVictory)
    {
        if (isVictory == false) return;

        m_standoffEnemyFleet = null;

        Debug.Log($"[DEBUG-CELL] ClearExplorationCellRequest zone={m_currentZoneNumber} row={m_currentRow} col={m_currentCol}");
        ClearExplorationCellRequest request = new ClearExplorationCellRequest
        {
            zoneNumber = m_currentZoneNumber,
            cellRow = m_currentRow,
            cellCol = m_currentCol,
        };
        NetworkManager.Instance.ClearExplorationCell(request, OnClearExplorationCellResponse);
    }

    private void OnClearExplorationCellResponse(ApiResponse<ClearExplorationCellResponse> response)
    {
        int pointGained = 0;
        if (response.errorCode != 0)
            Debug.LogError($"[UIPanelExplorationGrid] ClearExplorationCell 실패: {response.errorCode}");
        else if (response.data != null)
        {
            pointGained = response.data.explorationPointGained;
            // 이 시점엔 패널이 비활성 상태(전투 화면에 가려짐) — 값은 버퍼에만 쌓아두고, 화면 반영은 OnShowUIPanel에서 처리
            m_pendingBankedPointGain += pointGained;
        }

        if (m_gridData != null && m_gridData.IsInBounds(m_currentRow, m_currentCol) == true)
            m_gridData.SetCellCleared(m_currentRow, m_currentCol, true);

        // 빈 셀(적 없음)은 획득 포인트가 0 — 팝업 없이 바로 기존 흐름 진행
        if (pointGained <= 0)
        {
            ContinueAfterCellClear();
            return;
        }

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message = "탐험 포인트를 획득했습니다!",
            rewardAmounts = new List<int> { 0, pointGained, 0 },
            onConfirm = ContinueAfterCellClear,
        });
    }

    // 셀 클리어 후 그리드 흐름 계속 — 탈출 셀이면 탈출 확정 팝업, 아니면 그리드 화면으로 복귀
    private void ContinueAfterCellClear()
    {
        bool reachedEscape = m_gridData != null && m_gridData.GetCell(m_currentRow, m_currentCol).isEscape;
        if (reachedEscape == true)
        {
            ShowEscapeConfirmPopup();
            return;
        }

        UIManager.Instance.ShowPanel(panelName);
    }

    // 탈출 셀 클리어 직후, 또는 탈출 셀에 서 있는 상태에서 재클릭 시 호출 — 탈출 확정 여부 확인
    private void ShowEscapeConfirmPopup()
    {
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message = "탈출하시겠습니까? 지금까지 쌓인 탐험 포인트를 100% 획득합니다.",
            onConfirm = OnConfirmEscape,
            onCancel = () => UIManager.Instance.ShowPanel(panelName)
        });
    }

    private void OnConfirmEscape()
    {
        EscapeExplorationZoneRequest request = new EscapeExplorationZoneRequest
        {
            zoneNumber = m_currentZoneNumber,
            isSuccess = true,
        };
        NetworkManager.Instance.EscapeExplorationZone(request, response =>
        {
            if (response.errorCode != 0)
                Debug.LogError($"[UIPanelExplorationGrid] EscapeExplorationZone 실패: {response.errorCode}");
            else
            {
                m_bankedExplorationPoint = 0;
                RefreshBankedPointText();
                RefreshAbandonRunButtonState();
                ApplyOwnedPointRemain(response.data.explorationPointRemain);
                ClearActiveRunZoneCache();
            }

            UIManager.Instance.ShowPanel(panelName);
        });
    }

    // 적립 포인트가 있을 때만 포기 가능 — 없으면 포기해도 얻을 게 없어 버튼을 비활성화
    private void RefreshAbandonRunButtonState()
    {
        if (m_abandonRunButton != null)
            m_abandonRunButton.gameObject.SetActive(m_bankedExplorationPoint > 0);
    }

    private void RefreshBankedPointText()
    {
        if (m_bankedExplorationPointRow == null) return;

        m_bankedExplorationPointRow.SetLabel("UIPanelExplorationGrid_BankedPoint");
        m_bankedExplorationPointRow.SetValueAnimated(m_displayedBankedPoint, m_bankedExplorationPoint);
        m_displayedBankedPoint = m_bankedExplorationPoint;
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(m_bankedExplorationPointRow.transform as RectTransform);
    }

    // 탈출/포기 응답의 explorationPointRemain(확정 지급 후 보유 잔액)을 커맨더 정보에도 반영해 다른 화면(지휘력 증가 등)과 일치시킴
    private void ApplyOwnedPointRemain(int explorationPointRemain)
    {
        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        if (commanderInfo != null)
            commanderInfo.explorationPoint = explorationPointRemain;

        RefreshOwnedPointText();
    }

    // 탈출/포기로 런이 종료된 직후 로컬 캐시도 즉시 비워야 "다른 존 진행중" 판정이 이번 세션 내내 정확함
    private void ClearActiveRunZoneCache()
    {
        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        if (commanderInfo == null) return;

        commanderInfo.explorationZoneNumber = 0;
        commanderInfo.explorationCell = "";
    }

    private void RefreshOwnedPointText()
    {
        if (m_ownedExplorationPointRow == null) return;

        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        int ownedPoint = commanderInfo != null ? commanderInfo.explorationPoint : 0;

        m_ownedExplorationPointRow.SetLabel("UIPanelExplorationGrid_OwnedPoint");
        m_ownedExplorationPointRow.SetValueAnimated(m_displayedOwnedPoint, ownedPoint);
        m_displayedOwnedPoint = ownedPoint;
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(m_ownedExplorationPointRow.transform as RectTransform);
    }

    private void OnAbandonRunButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message = "진행 중인 탐험을 포기하시겠습니까? 지금까지 쌓인 탐험 포인트의 50%만 획득합니다.",
            onConfirm = ConfirmAbandonRun,
            onCancel = () => { }
        });
    }

    private void ConfirmAbandonRun()
    {
        NetworkManager.Instance.AbandonZoneRun(new AbandonZoneRunRequest(), response =>
        {
            if (response.errorCode != 0)
            {
                Debug.LogError($"[UIPanelExplorationGrid] AbandonZoneRun 실패: {response.errorCode}");
                return;
            }

            m_bankedExplorationPoint = 0;
            RefreshBankedPointText();
            RefreshAbandonRunButtonState();
            ApplyOwnedPointRemain(response.data.explorationPointRemain);
            ClearActiveRunZoneCache();

            // 진행 중이던 런이 종료되었으므로 현재 존을 시작 셀 상태로 다시 진입 — m_gridData를 비워 SelectZoneTab의 좌표 보존 경로를 건너뜀
            m_gridData = null;
            SelectZoneTab(m_currentZoneNumber, ComputeZoneSeed(m_currentZoneNumber));
        });
    }

    // 퇴각 — 적 함대만 제거하고 내 함대를 대치 진입 직전 위치/셀로 복귀
    private void OnConfirmRetreat()
    {
        if (m_standoffEnemyFleet != null)
        {
            ObjectManager.Instance.RemoveEnemyFleet(m_standoffEnemyFleet);
            m_standoffEnemyFleet = null;
        }

        m_currentRow = m_previousRow;
        m_currentCol = m_previousCol;
        ObjectManager.Instance.SetMyFleetPosition(m_previousFleetWorldPos, 0f);
        CameraController.Instance.SnapToTarget(); // 함대가 순간이동했으므로 카메라도 즉시 스냅 — 워프인 때는 ExitGalaxyView가 미리 그 위치로 이동해둬서 필요 없었지만, 퇴각은 카메라 이동 없이 함대만 텔레포트되므로 별도 스냅 필요
    }

    private void RefreshCellStates()
    {
        foreach (var kv in m_activeButtons)
        {
            int row = kv.Key.row;
            int col = kv.Key.col;
            GridCellButton button = kv.Value;
            GridCellData cellData = m_gridData.GetCell(row, col);

            bool isCurrentCell = row == m_currentRow && col == m_currentCol;
            bool isAdjacent = m_gridData.IsAdjacent(m_currentRow, m_currentCol, row, col);

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
