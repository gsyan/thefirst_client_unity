// 탐사 그리드 패널 — 그리드 생성 + 셀 배치 + 인접 이동 + 갤럭시뷰 연동 + 셀 진입(워프인) 담당. UIManager가 관리하는 독립 패널(다른 진입 화면과 배타적)
// 패널 자체(존 탭/포인트 표시 등)는 열리자마자 바로 보임 — 그리드 셀 버튼만 갤럭시뷰 카메라가 실제로 정착한 시점(SettleZoneEntry)에 생성됨.
// 이전엔 CanvasGroup 알파로 패널 전체를 가려뒀었으나, 버튼을 미리 비우는 것만으로 충분해 제거함(향후 버튼 순차/랜덤 등장 연출의 선행 작업)
using System.Collections.Generic;
using UnityEngine;

public class UIPanelExplorationGrid : UIPanelBase
{
    [SerializeField] private GridCell3D m_cellPrefab;

    // 3D 그리드 루트 — 프리팹 에셋은 특정 씬의 오브젝트를 참조할 수 없어 런타임에 생성. 셀 좌표가 이미 절대 월드 좌표라 루트 자체의 위치/회전은 항상 원점 고정
    private Transform m_cellRoot;

    [Header("탐험 포인트 / 런 포기")]
    [SerializeField] private RowLabelValue m_bankedExplorationPointRow; // 진행 중인 런의 적립(미확정) 탐험 포인트 상시 표시
    [SerializeField] private RowLabelValue m_ownedExplorationPointRow; // 확정 지급되어 실제 보유 중인 탐험 포인트(지휘력 증가 등에 소모하는 값) 상시 표시
    [SerializeField] private UnityEngine.UI.Button m_abandonRunButton; // 진행 중인 런을 포기(적립분의 50%만 확정 지급)

    [Header("존 선택 스크롤")]
    [SerializeField] private InfiniteScrollViewH m_zoneTabScroll;
    [SerializeField] private GameObject m_zoneTabNodePrefab;

    // 셀 클리어 직후부터 탈출 확정 팝업이 실제로 뜨기 전까지, 그리드/빈 공간 조작(3D 레이캐스트)을 막기 위한 화면 전체 raycastable 오버레이
    [Header("입력 차단")]
    [SerializeField] private GameObject m_inputBlockOverlay;
    // 락/클리어 표시 등 실제 진행도 연동은 후속 작업(clearedZones가 아직 구식 스테이지 포맷)

    [Header("보상카드 지속버프 표시")]
    [SerializeField] private UIRewardCardBuffDisplay m_rewardCardBuffDisplay; // 확보한 지속버프 아이콘 상시 표시(좌측 상단 등)

    private const float k_enemyEncounterDistance = 50f; // 셀 안 전투 조우 거리 — datatable_zone_enemy_fleet_position.csv grade1과 동일 스케일(국소 전술 거리, 갤럭시뷰 좌표와는 별개)

    private int m_zoneGroupCount;
    private int m_pendingZoneNumber;
    private bool m_pendingFleetRepositionForZoneAdvance; // 탈출 성공으로 다음 존이 확정된 뒤, 로컬 함대뷰로 나갈 때 함대를 새 존 시작 셀로 재배치해야 함을 표시

    private ExplorationGridData m_gridData;
    private int m_currentZoneNumber;
    private int m_currentSeed;
    private int m_currentRow;
    private int m_currentCol;

    // 브라우징(탭/스크롤)으로 진행중인 존을 벗어났다가 돌아올 때 재생성/서버 재조회 없이 그대로 복원하기 위한 스냅샷
    private int m_activeZoneSnapshotZoneNumber;
    private ExplorationGridData m_activeZoneSnapshotGridData;
    private int m_activeZoneSnapshotRow;
    private int m_activeZoneSnapshotCol;
    private BankedRunReward m_activeZoneSnapshotBankedReward = new();

    private bool m_pendingCellEntry;
    private int m_pendingCellRow;
    private int m_pendingCellCol;
    private Vector3 m_pendingCellWorldPos;
    private FleetInfo m_pendingEnemyFleetInfo; // EnterExplorationCellResponse가 전투 있음으로 확정한 적 함대 구성 — 함대뷰 전환 완료(FleetViewRestored) 후 SpawnConfirmedEnemyFleet에서 스폰
    private string m_activeChallengeToken; // EnterExplorationCellResponse가 발급한 1회용 토큰 — ClearExplorationCellRequest에 그대로 실어 보냄(enter-cell 생략한 clear 반복 호출 차단용)

    // 대치 상태(UIPanelPrepareBattle) 중 퇴각 시 되돌아갈 이전 위치 — 로컬뷰 카메라 전환 이후엔 그리드 좌표→월드좌표 역산이 불가능(카메라 자세가 이미 바뀜)하므로 이동 직전 좌표를 스냅샷
    private int m_previousRow;
    private int m_previousCol;
    private Vector3 m_previousFleetWorldPos;
    // 전투 진입 직전 체력/전술력 스냅샷 — 셀 클리어 실패(패배/퇴각) 시 이 상태로 롤백(성공 시엔 롤백하지 않고 클리어 시점 값을 그대로 확정)
    private List<ShipHealthRatioInfo> m_previousShipHealthRatios;
    private int m_previousTacticPower;
    private SpaceFleet m_standoffEnemyFleet;

    private BankedRunReward m_bankedReward = new(); // 진행 중인 런의 적립(미확정) 보상(탐험 포인트/경험치 등) — 클리어마다 누적, 탈출/포기 확정 시 리셋
    private BankedRunReward m_pendingBankedRewardGain = new(); // 전투 중(패널 비활성 상태)에 획득해서 아직 화면에 반영 안 한 적립량 — 패널이 열릴 때 한 번에 반영+애니메이션
    private bool m_pendingShowEscapeConfirmOnSettle; // 탈출 셀 클리어 직후 세팅 — SettleZoneEntry가 현재 존 그리드에 최종 적립 포인트를 반영한 뒤에만 탈출 확정 팝업을 띄우기 위함
    private long m_displayedBankedPoint = -1; // 롤링 애니메이션의 시작값 추적 — 최초 1회는 -1이라 즉시 표시됨(UIResourceBar와 동일 패턴)
    private long m_displayedOwnedPoint = -1;

    private readonly List<GridCell3D> m_buttonPool = new();
    private readonly Dictionary<(int row, int col), GridCell3D> m_activeButtons = new();
    private readonly Dictionary<(int row, int col), List<FleetInfo>> m_cellEnemyWaves = new(); // 셀별 순차 웨이브 — 저장하지 않고 메모리 캐싱만

    public override void InitializeUIPanel()
    {
        EventManager.Subscribe_ZoneStageBattleEnd(OnZoneStageBattleEnd);
        EventManager.Subscribe_RetreatExploration(OnRetreatDuringBattle);

        if (m_abandonRunButton != null)
            m_abandonRunButton.onClick.AddListener(OnAbandonRunButtonClicked);

        // 캔버스 계층 밖의 독립 루트 — 3D 셀이 UI Canvas의 스케일/회전에 영향받지 않도록 별도로 생성
        m_cellRoot = new GameObject("ExplorationGridCellRoot").transform;
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_ZoneStageBattleEnd(OnZoneStageBattleEnd);
        EventManager.Unsubscribe_RetreatExploration(OnRetreatDuringBattle);
    }

    // 전투 중 후퇴 버튼(UIBattleView) — 확인 후 전투 강제 종료. 레거시 UITabExploration.OnRetreatZoneStage와 동일 패턴/로컬라이즈 키 재사용
    private void OnRetreatDuringBattle()
    {
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message   = LocalizationManager.Instance.Get("UITabExploration_RetreatConfirm"),
            onConfirm = () => ObjectManager.Instance.ForceEndBattle(false, 0f), // 유저가 직접 누른 퇴각은 격추 연출 대기 없이 즉시 처리
            onCancel  = () => { }
        });
    }

    public override void OnShowUIPanel()
    {
        // CameraController.HandleGalaxyGridSelection이 셀 히트 시 발행 — 셀 아닌 빈 곳은 EmptySpaceTapped로 흘러 UIManager가 패널을 닫음
        EventManager.Subscribe_ExplorationGridCellClicked(OnGridCellClicked);

        EnterZone(ObjectManager.Instance.GetInitialZoneIndex());
        RefreshRewardCardBuffDisplay();
    }

    // 보상카드 지속버프 상태(ObjectManager.m_rewardCardSessionState)가 바뀔 때마다(선택/재접속복구/런종료) 호출해 아이콘 나열을 다시 그림
    private void RefreshRewardCardBuffDisplay()
    {
        if (m_rewardCardBuffDisplay != null)
            m_rewardCardBuffDisplay.Refresh(ObjectManager.Instance.m_rewardCardSessionState);
    }

    // 존 진입(카메라 갤럭시뷰 전환 → 정착 시 SettleZoneEntry) — OnShowUIPanel(패널이 새로 열릴 때) 전용이 아니라,
    // 패널이 이미 열려있는 채로 존을 다시 진입해야 할 때(탈출 성공 등)도 재사용. UIManager.ShowPanel은 "이미 활성 상태인
    // 패널"이면 그냥 리턴해버려(OnShowUIPanel 재호출 안 됨) 그런 경우엔 이 메서드를 직접 불러야 실제로 다시 그려짐
    private void EnterZone(int zoneNumber)
    {
        // 존/그리드가 실제로 정착하기 전(카메라 갤럭시뷰 전환 중)에는 m_currentZoneNumber를 건드리지 않음 —
        // SelectZoneTab의 isSameZoneReentry 판정이 "직전에 로드돼있던 존이 무엇인지"를 정확히 알아야 하므로(조기 대입 시 오판 버그 발생)
        m_pendingZoneNumber = zoneNumber;

        // 존 탭/포인트 표시/버튼/그리드 버튼 전부 SettleZoneEntry(카메라 정착 후)에서 한 번에 세팅됨 —
        // 그 전까지 이전 존의 잔상이 보이지 않도록 버튼만 즉시 비우고, 포기 버튼/존 탭은 기본값(비표시)으로 되돌려둠
        ReturnAllButtonsToPool();
        if (m_abandonRunButton != null)
            m_abandonRunButton.gameObject.SetActive(false);
        if (m_zoneTabScroll != null)
            m_zoneTabScroll.gameObject.SetActive(false);

        m_pendingCellEntry = false;

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

    // CameraController.HandleGalaxyGridSelection이 3D 셀 레이캐스트로 히트했을 때 발행 — (row,col)로 변환해 기존 클릭 흐름 재사용
    private void OnGridCellClicked(GridCell3D cell)
    {
        OnCellClicked(cell.GetRow(), cell.GetCol());
    }

    public override void OnHideUIPanel()
    {
        EventManager.Unsubscribe_ExplorationGridCellClicked(OnGridCellClicked);
        EventManager.Unsubscribe_GalaxyViewSettled(OnGalaxyViewSettled);

        // 존 탭 스크롤 관성이 남아있으면 패널이 닫힌 뒤에도 onCenterIndexChanged -> NavigateToZone -> BuildCellButtons()가
        // 뒤늦게 실행되어 m_cellRoot(패널과 무관한 독립 루트)에 그리드 셀이 다시 살아날 수 있음 — 먼저 관성부터 멈춤
        if (m_zoneTabScroll != null)
            m_zoneTabScroll.StopScrolling();

        // 셀이 이제 독립된 3D 루트(m_cellRoot)에 있어 패널 자신을 꺼도 같이 안 꺼짐 — 로컬뷰로 나갈 때 명시적으로 회수
        ReturnAllButtonsToPool();

        if (CameraController.Instance == null) return;

        if (m_pendingCellEntry == true)
        {
            EventManager.Subscribe_FleetViewRestored(OnFleetViewRestoredForCellEntry);
            CameraController.Instance.ExitGalaxyView(m_pendingCellWorldPos, ignoreFleetTarget: true);
        }
        else
        {
            // 존 탭 스크롤/네비게이션으로 다른 존을 브라우징만 하다가 그냥 닫는 경우 —
            // ChangeZone(NavigateToZone)이 바꿔둔 천체(m_activeZoneIndex)가 그대로 남아있으므로,
            // 함대뷰로 복귀하기 전에 원래 있어야 할 존(진행중인 런 → 없으면 최고 클리어 존의 다음 존)으로 되돌림
            int correctZoneNumber = ObjectManager.Instance.GetInitialZoneIndex();
            ObjectManager.Instance.ChangeZone(correctZoneNumber);

            Vector3 returnPos;
            if (m_pendingFleetRepositionForZoneAdvance == true)
            {
                // 탈출 성공으로 새 존이 확정된 직후 — 함대를 새 존의 시작 셀로 옮겨야 함(기존 위치는 이전 존의 탈출 셀)
                m_pendingFleetRepositionForZoneAdvance = false;
                returnPos = ObjectManager.Instance.GetInitialGridStartCellPosition();
                ObjectManager.Instance.SetMyFleetPosition(returnPos, 0f);
            }
            else
            {
                SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
                returnPos = myFleet != null ? myFleet.transform.position : Vector3.zero;
            }
            CameraController.Instance.ExitGalaxyView(returnPos);
        }
    }

    // 카메라가 목표 자세(줌/각도)에 완전히 도달한 시점 — 셀 오브젝트를 스폰해도 카메라 프레이밍이 완성된 채로 자연스럽게 보임
    private void OnGalaxyViewSettled()
    {
        EventManager.Unsubscribe_GalaxyViewSettled(OnGalaxyViewSettled);
        SettleZoneEntry(m_pendingZoneNumber);
    }

    // "실제로 이 존에 정착한다"는 단일 지점 — 존 탭/보유 포인트/그리드(좌표·적립포인트·버튼)를 전부 이 시점에 한 번에 확정
    private void SettleZoneEntry(int zoneNumber)
    {
        // 전투 중(패널 비활성 상태)에 획득한 적립 보상은 m_pendingBankedRewardGain에 쌓여있음 —
        // GameObject가 active인 지금 한 번에 반영해야 롤링 애니메이션 코루틴이 실제로 재생됨
        m_bankedReward.Add(EBankedRewardType.ExplorationPoint, m_pendingBankedRewardGain.Get(EBankedRewardType.ExplorationPoint));
        m_bankedReward.Add(EBankedRewardType.Exp, m_pendingBankedRewardGain.Get(EBankedRewardType.Exp));
        m_pendingBankedRewardGain.Clear();

        // 탈출 셀 클리어로 여기까지 왔으면, 롤링 애니메이션이 실제로 끝나는 콜백 시점에 탈출 확정 팝업을 띄움(별도 타이머 추정 없이 정확한 동기화)
        System.Action onBankedPointAnimComplete = null;
        if (m_pendingShowEscapeConfirmOnSettle == true)
        {
            m_pendingShowEscapeConfirmOnSettle = false;
            onBankedPointAnimComplete = ShowEscapeConfirmPopup;
        }
        RefreshBankedPointText(onBankedPointAnimComplete);
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

    }

    private void OnZoneTabNodeBind(int dataIndex, GameObject obj)
    {
        UIZoneTabNode node = obj.GetComponent<UIZoneTabNode>();
        if (node == null) return;
        node.SetData(dataIndex, OnZoneTabNodeClicked);
        int zoneNumber = dataIndex + 1;
        bool selected = zoneNumber == m_currentZoneNumber;
        int highestClearedZoneNumber = GetHighestClearedZoneNumber();
        bool isCleared = zoneNumber <= highestClearedZoneNumber;
        bool isLocked = zoneNumber > highestClearedZoneNumber + 1;
        node.SetState_UIZoneTabNode(selected, isCleared, isLocked);
    }

    // 입장 가능 판정(NavigateToZone)과 항상 같은 기준을 써야 함 — 표시와 실제 진입 가능 여부가 어긋나면 안 됨
    private int GetHighestClearedZoneNumber()
    {
        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        return commanderInfo != null ? commanderInfo.highestClearedZoneNumber : 0;
    }

    private void OnZoneTabNodeClicked(int groupIndex)
    {
        int targetZoneNumber = groupIndex + 1;
        if (targetZoneNumber == m_currentZoneNumber) return;
        NavigateToZone(targetZoneNumber, recenterScroll: true);
    }

    // 스크롤 드래그로 중앙 인덱스 변경 — 이미 사용자의 손가락 위치가 중앙에 와 있는 상태이므로 재센터링 애니메이션을 얹으면
    // 코루틴이 매 프레임 덮어쓰는 content 위치와 드래그가 충돌해 스크롤이 뻑뻑해짐(recenterScroll: false)
    private void OnZoneScrollCenterChanged(int dataIndex)
    {
        int newZoneNumber = dataIndex + 1;
        if (newZoneNumber == m_currentZoneNumber) return;
        NavigateToZone(newZoneNumber, recenterScroll: false);
    }

    // 존 전환 — 3D 그리드로 바뀐 뒤로는 존마다 galaxyCameraZoom/Target이 실제 셀 배치 스케일과 직결되므로
    // (그리드 크기가 커지면 줌도 더 빠지도록 존별로 데이터 튜닝됨) 카메라도 그 존의 값으로 같이 옮겨줘야 함
    private void NavigateToZone(int zoneNumber, bool recenterScroll)
    {
        zoneNumber = Mathf.Clamp(zoneNumber, 1, m_zoneGroupCount > 0 ? m_zoneGroupCount : zoneNumber);
        if (zoneNumber == m_currentZoneNumber && m_gridData != null) return;

        SnapshotActiveZoneStateIfNeeded();

        ObjectManager.Instance.ChangeZone(zoneNumber);

        ZoneConfig zoneConfig = DataManager.Instance.m_dataTableZone.GetZoneByZoneIndex(zoneNumber);
        if (zoneConfig != null && CameraController.Instance != null)
            CameraController.Instance.FocusOnZoneAnchor(zoneConfig.galaxyCameraTarget, zoneConfig.galaxyCameraZoom, zoneConfig.galaxyCameraRotX, zoneConfig.galaxyCameraRotY);

        // m_currentZoneNumber를 여기서 먼저 확정해야, 이어지는 탭 스크롤 이동 중에 발생하는 rebind/onCenterIndexChanged가
        // 이미 최신 존 번호를 기준으로 판정됨 — 순서가 바뀌면 하이라이트가 한 스텝 밀리거나 재귀 호출이 발생함
        SelectZoneTab(zoneNumber, ComputeZoneSeed(zoneNumber));

        if (recenterScroll == true && m_zoneTabScroll != null)
            m_zoneTabScroll.ScrollToCenterSmooth(zoneNumber - 1);
    }

    // 로그인 시 받은 유저 무관 공통 시드(explorationSeedBase — 모든 유저 동일)와 zoneNumber를 조합 — 서버 재요청 없이 클라에서 결정론적으로 계산
    // 모든 유저가 같은 Zone/셀에서 같은 적함대를 만나야 하므로 유저별로 달라지면 안 됨(난이도 공정성)
    private int ComputeZoneSeed(int zoneNumber)
    {
        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        int seedBase = commanderInfo != null ? commanderInfo.explorationSeedBase : 0;
        return CommonUtility.ComputeExplorationZoneSeed(zoneNumber, seedBase);
    }

    // 지금 보고 있는 존이 진행중인 런의 존이면, 떠나기 직전 상태를 스냅샷 — 참조타입인 m_gridData는 플레이 중 갱신되는 객체 그대로 저장되므로 별도 동기화 불필요
    private void SnapshotActiveZoneStateIfNeeded()
    {
        if (m_gridData == null) return;
        if (IsActiveExplorationZone(m_currentZoneNumber) == false) return;

        m_activeZoneSnapshotZoneNumber = m_currentZoneNumber;
        m_activeZoneSnapshotGridData = m_gridData;
        m_activeZoneSnapshotRow = m_currentRow;
        m_activeZoneSnapshotCol = m_currentCol;
        m_activeZoneSnapshotBankedReward.CopyFrom(m_bankedReward);
    }

    public void SelectZoneTab(int zoneNumber, int seed)
    {
        ZoneConfig zoneConfig = DataManager.Instance.m_dataTableZone.GetZoneByZoneIndex(zoneNumber);

        // 전투 승리 후 그리드 복귀(ShowPanel 재호출)처럼 같은 존 재진입 시에는 시작 좌표로 리셋하지 않고
        // 직전에 있던 셀 좌표를 유지 — 존 자체가 바뀌면(최초 진입/존 이동) 시작 좌표로 초기화
        // 그리드 레이아웃은 이제 고정 데이터라 seed와 무관 — zoneNumber만 같으면 동일 그리드
        bool isSameZoneReentry = m_gridData != null && m_currentZoneNumber == zoneNumber;

        // 브라우징으로 잠시 벗어났던 진행중인 존으로 돌아온 경우 — 재생성/서버 재조회 없이 떠나기 직전 스냅샷을 그대로 복원
        bool hasActiveZoneSnapshot = isSameZoneReentry == false
            && zoneNumber == m_activeZoneSnapshotZoneNumber
            && m_activeZoneSnapshotGridData != null
            && IsActiveExplorationZone(zoneNumber) == true;

        int preservedRow = m_currentRow;
        int preservedCol = m_currentCol;

        m_currentZoneNumber = zoneNumber;
        m_currentSeed = seed;

        if (hasActiveZoneSnapshot == true)
        {
            m_gridData = m_activeZoneSnapshotGridData;
            m_currentRow = m_activeZoneSnapshotRow;
            m_currentCol = m_activeZoneSnapshotCol;
            m_bankedReward.CopyFrom(m_activeZoneSnapshotBankedReward);
            RefreshBankedPointText();

            BuildCellButtons();
            BuildCellEnemyFleets(zoneConfig);
            RefreshCellStates();
            RefreshAbandonRunButtonState();
            return;
        }

        // 같은 존 재진입(isSameZoneReentry)이면 그리드를 다시 만들지 않고 기존 객체를 재사용 —
        // 새로 만들면 로컬에서 SetCellCleared로 켜둔 클리어 상태가 전부 초기화돼버림(레이아웃 자체는 zoneNumber만 같으면 항상 동일해서 다시 만들 이유도 없음)
        if (isSameZoneReentry == false)
            m_gridData = ExplorationGridGenerator.Generate(zoneConfig);

        if (isSameZoneReentry == true && m_gridData.IsInBounds(preservedRow, preservedCol) == true)
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
        if (isSameZoneReentry == false)
        {
            m_bankedReward.Clear();
            RefreshBankedPointText();
        }

        BuildCellButtons();
        BuildCellEnemyFleets(zoneConfig);
        RefreshCellStates();
        RefreshAbandonRunButtonState();

        // 세션 내 메모리 좌표가 없는 첫 진입이면서 서버에 진행 중인 런이 있는 존이면, 지나온 셀들의 방문 표시도 복구
        if (isSameZoneReentry == false && IsActiveExplorationZone(zoneNumber) == true)
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

        m_bankedReward.Set(EBankedRewardType.ExplorationPoint, response.data.explorationPointBanked);
        m_bankedReward.Set(EBankedRewardType.Exp, response.data.commanderExpBanked);
        RefreshBankedPointText();
        RefreshAbandonRunButtonState();
        ApplyFleetHealthSnapshot(response.data.shipHealthRatios);
        // 보상카드 지속버프 복원은 ObjectManager.StartNormalPlay()에서 로그인 시점에 이미 끝남(중복 적용 방지) — 여기서는 재적용하지 않음

        string[] clearedCells = response.data.clearedCells;
        if (clearedCells != null)
        {
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

        // 마지막 클리어 셀에 아직 선택 확정 안 된 카드 후보가 있으면(팝업이 뜨기 전에 앱이 꺼진 경우) 재접속 시 다시 띄움
        if (response.data.pendingRewardCardCandidates != null && response.data.pendingRewardCardCandidates.Count > 0)
        {
            UIManager.Instance.ShowRewardCardSelectPopup(0, 0, response.data.pendingRewardCardCandidates, selectedCardId =>
            {
                if (selectedCardId != null)
                    OnRewardCardSelected(selectedCardId);
            });
        }
    }

    // 슬롯 포지션 인덱스 기준으로 함대 체력 비율 스냅샷을 적용 — (1) 재접속 시 서버 저장값 복구, (2) 전투 퇴각/패배 시 진입 직전 상태로 롤백, 두 용도에 재사용
    private void ApplyFleetHealthSnapshot(List<ShipHealthRatioInfo> shipHealthRatios)
    {
        if (shipHealthRatios == null || shipHealthRatios.Count == 0) return;

        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet == null) return;

        foreach (ShipHealthRatioInfo entry in shipHealthRatios)
        {
            SpaceShip ship = myFleet.m_ships.Find(s => s != null && s.m_shipInfo.positionIndex == entry.positionIndex);
            if (ship != null)
                ship.ApplyHealthRatio(entry.healthRatio);
        }
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
                    zoneConfig, m_currentSeed, row, col, DataManager.Instance.m_dataTableModule);
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

    // 셀마다 ExplorationGridGenerator.Generate()에서 이미 계산해 캐싱해둔 worldPos를 그대로 씀 — 카메라/UI 상태와 무관
    private void BuildCellButtons()
    {
        ReturnAllButtonsToPool();

        Camera cam = CameraController.Instance != null ? CameraController.Instance.m_targetCamera : Camera.main;

        for (int row = 0; row < m_gridData.height; row++)
        {
            for (int col = 0; col < m_gridData.width; col++)
            {
                GridCellData cellData = m_gridData.GetCell(row, col);
                GridCell3D button = GetButtonFromPool();
                button.gameObject.SetActive(true);
                button.transform.position = cellData.worldPos;
                button.Initialize(cellData);                
                m_activeButtons[(row, col)] = button;
            }
        }
    }

    private void OnCellClicked(int row, int col)
    {
        Debug.Log($"[DEBUG-CELL] OnCellClicked row={row} col={col} (currentRow={m_currentRow} currentCol={m_currentCol})");

        // 브라우징(스크롤/탭 이동)은 잠긴 존도 허용하되, 실제 입장(전투 진입)만 여기서 차단
        if (m_currentZoneNumber > GetHighestClearedZoneNumber() + 1)
        {
            UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
            {
                message = LocalizationManager.Instance.Get("UIPopupMessage_ZoneLocked"),
                autoCloseSec = 5f,
            });
            return;
        }

        bool isCurrentCell = row == m_currentRow && col == m_currentCol;
        bool isAdjacent = m_gridData.IsAdjacent(m_currentRow, m_currentCol, row, col);

        if (isCurrentCell == false && isAdjacent == false) return;
        if (isCurrentCell == false && m_gridData.GetCell(row, col).isBlocked) return;

        // 현재 서 있는 셀(이미 클리어됨)을 다시 클릭하는 건 아무 동작도 하지 않음 —
        // 탈출 셀도 클리어 즉시 SettleZoneEntry가 자동으로 탈출 확정 팝업을 띄우므로 여기 도달할 일 자체가 없음
        if (isCurrentCell) return;

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
    // — 로컬 캐시(m_bankedReward)는 그 존을 벗어나며 리셋되므로 여기선 신뢰할 수 없어 서버에 재조회
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
                message = LocalizationManager.Instance.Get("UIPanelExplorationGrid_OtherRunInProgress",
                    otherZoneNumber, otherCellDisplay, CommonUtility.FormatNumber(otherBanked)),
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
            ApplyExpAndLevel(response.data.totalExp, response.data.commanderLevel);
            ApplyTacticPowerRecovered(response.data.tacticPower);
            ClearActiveRunZoneCache();

            ConfirmEnterCell(row, col);
        });
    }

    // 확인 팝업 승인 — 전투 여부를 아직 모르므로 카메라는 갤럭시뷰에 그대로 둔 채 서버(EnterExplorationCell)부터 물어봄.
    // 함대뷰 전환(ExitGalaxyView)은 응답이 "전투 있음"으로 확정된 뒤(OnEnterExplorationCellResponse)에만 시작 — 전투 없는
    // 재방문/빈 셀은 카메라 전환 자체가 불필요하므로 갤럭시뷰에 머문 채 위치만 갱신(MoveToConfirmedCellWithoutBattle)
    private void ConfirmEnterCell(int row, int col)
    {
        m_pendingCellRow = row;
        m_pendingCellCol = col;
        m_pendingCellWorldPos = m_gridData.GetCell(row, col).worldPos;

        RequestEnemyFleetForCurrentCell();
    }

    private void OnFleetViewRestoredForCellEntry()
    {
        EventManager.Unsubscribe_FleetViewRestored(OnFleetViewRestoredForCellEntry);
        m_pendingCellEntry = false;

        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet == null) return;

        m_currentRow = m_pendingCellRow;
        m_currentCol = m_pendingCellCol;

        ObjectManager.Instance.SetMyFleetPosition(m_pendingCellWorldPos, 0f);
        myFleet.StartFleetWarpIn();
        SpawnConfirmedEnemyFleet(myFleet);
    }

    // OnConfirmAbandonAnotherRunAndRetry(다른 존 런 포기 후 재시도)에서도 재사용 — m_pendingCellRow/Col은
    // ConfirmEnterCell이 이미 세팅해둔 값을 그대로 씀
    private void RequestEnemyFleetForCurrentCell()
    {
        EnterExplorationCellRequest request = new EnterExplorationCellRequest
        {
            zoneNumber = m_currentZoneNumber,
            cellRow = m_pendingCellRow,
            cellCol = m_pendingCellCol,
            fleetInfo = DataManager.Instance.m_currentFleetComposition != null
                ? DataManager.Instance.m_currentFleetComposition.ToNetworkFleetInfo()
                : null,
        };
        NetworkManager.Instance.EnterExplorationCell(request, OnEnterExplorationCellResponse);
    }

    // 대치 상태로 전환, 전투시작/퇴각/함대설정 3버튼 노출 — 워프인 연출 완료를 기다리지 않고 스폰 즉시 표시
    private void ShowPrepareBattlePanel(SpaceFleet myFleet, SpaceFleet enemyFleet)
    {
        UIPanelPrepareBattle panel = UIManager.Instance.GetPanel<UIPanelPrepareBattle>("UIPanelPrepareBattle");
        if (panel == null) return;

        panel.Open(myFleet, enemyFleet, OnConfirmStartBattle, OnConfirmRetreat);
    }

    // 전투시작 확정 — 적 함대는 서버 응답으로 이미 스폰돼 있으므로 별도 통신 없이 바로 교전 전환
    private void OnConfirmStartBattle()
    {
        if (m_standoffEnemyFleet == null) return;

        // 교전 상태 진입 즉시 발사되면 한방짜리 전투가 너무 순식간에 끝나 보이므로, 양측 사격 개시 전 0.5초 텀을 둠
        const float BATTLE_START_DELAY_SEC = 0.5f;
        ObjectManager.Instance.TryStartCombat(m_standoffEnemyFleet, EUnitState.BattleExploration, BATTLE_START_DELAY_SEC, BATTLE_START_DELAY_SEC);
        m_standoffEnemyFleet = null;
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

        m_activeChallengeToken = response.data.challengeToken;

        // 이 존에 런이 확정 시작됨 — 로컬 캐시도 즉시 갱신해야 이번 세션 안에서 "다른 존 진행중" 판정이 정확함
        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        if (commanderInfo != null)
            commanderInfo.explorationZoneNumber = m_currentZoneNumber;

        List<StageEnemyFleetSpawnConfig> enemyFleets = response.data.enemyFleets;
        FleetInfo enemyFleetInfo = enemyFleets != null && enemyFleets.Count > 0 ? enemyFleets[0].fleetInfo : null;
        bool hasEnemies = enemyFleetInfo != null && enemyFleetInfo.ships != null && enemyFleetInfo.ships.Count > 0;

        if (hasEnemies == false)
        {
            // 전투 없음(재방문/빈 셀) — 서버가 이미 위치를 확정해뒀으므로 카메라 전환(갤럭시뷰→함대뷰) 없이 그 자리에서 위치만 갱신
            MoveToConfirmedCellWithoutBattle();
            return;
        }

        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet == null) return;

        // 카메라 전환 전(그리드 좌표→월드좌표 역산이 가능한 마지막 시점)에 퇴각 복귀용 스냅샷 저장
        m_previousRow = m_currentRow;
        m_previousCol = m_currentCol;
        m_previousFleetWorldPos = myFleet.transform.position;
        m_previousShipHealthRatios = myFleet.BuildHealthRatioSnapshot();
        CommanderInfo snapshotCommanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        m_previousTacticPower = snapshotCommanderInfo != null ? snapshotCommanderInfo.tacticPower : 0;

        m_pendingEnemyFleetInfo = enemyFleetInfo;

        // 전투가 확정된 뒤에야 함대뷰로 전환 — OnHideUIPanel(ExitGalaxyView) → FleetViewRestored 이후 OnFleetViewRestoredForCellEntry에서
        // 실제 함대 이동/워프인 + 적 함대 스폰(SpawnConfirmedEnemyFleet) 처리
        m_pendingCellEntry = true;
        UIManager.Instance.HideCurrentPanel(showMainPanelIfEmpty: false);
    }

    // 전투 없이 확정된 셀(재방문/빈 셀)로 이동 — 카메라는 갤럭시뷰에 그대로 둔 채 함대 오브젝트만 워프인.
    // 탈출 셀 재방문(예: 탈출 확정 팝업 뜨기 전에 앱 종료 후 재접속)도 여기서 처리 — 카메라 정착 대기 없이 이미 갤럭시뷰이므로 팝업 즉시 표시
    private void MoveToConfirmedCellWithoutBattle()
    {
        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet == null) return;

        m_currentRow = m_pendingCellRow;
        m_currentCol = m_pendingCellCol;

        ObjectManager.Instance.SetMyFleetPosition(m_pendingCellWorldPos, 0f);
        myFleet.StartFleetWarpIn();

        RefreshCellStates();

        bool reachedEscape = m_gridData != null && m_gridData.GetCell(m_currentRow, m_currentCol).isEscape;
        if (reachedEscape == true)
            ShowEscapeConfirmPopup();
    }

    // 대치 상태로 전환, 전투시작/퇴각/함대설정 3버튼 노출 — 워프인 연출 완료를 기다리지 않고 스폰 즉시 표시
    private void SpawnConfirmedEnemyFleet(SpaceFleet myFleet)
    {
        FleetInfo enemyFleetInfo = m_pendingEnemyFleetInfo;
        m_pendingEnemyFleetInfo = null;
        if (enemyFleetInfo == null) return;

        Vector3 enemyPos = m_pendingCellWorldPos + Vector3.forward * k_enemyEncounterDistance;
        Quaternion enemyRot = Quaternion.LookRotation(m_pendingCellWorldPos - enemyPos);

        ETeam enemyTeam = ObjectManager.Instance.GetOpposingTeam(ObjectManager.Instance.m_myTeam);
        // 체력/공격력 배율은 서버가 enemyFleetInfo.ships[i].healthMultiplier/attackMultiplier로 실어 보낸 값을 그대로 씀(SpawnFleetFromPreset 내부에서 처리)
        SpaceFleet enemyFleet = ObjectManager.Instance.SpawnFleetFromPreset(enemyFleetInfo, enemyTeam, EFleetSource.fleet_source_zone_data, enemyPos, enemyRot, "EnemyFleet");
        m_standoffEnemyFleet = enemyFleet;
        enemyFleet.StartFleetWarpIn();

        ShowPrepareBattlePanel(myFleet, enemyFleet);
    }

    // 이미 다른 존에 IN_PROGRESS 런이 있을 때 — 포기 후 재시도 확인
    private void ShowAbandonAnotherRunConfirmPopup()
    {
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message = LocalizationManager.Instance.Get("UIPanelExplorationGrid_AbandonAnotherRunConfirm"),
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
            ApplyOwnedPointRemain(response.data.explorationPointRemain);
            ApplyExpAndLevel(response.data.totalExp, response.data.commanderLevel);
            ApplyTacticPowerRecovered(response.data.tacticPower);
            RequestEnemyFleetForCurrentCell(); // 기존 런 정리 완료 — 원래 셀 도전 요청을 재시도
        });
    }

    // 전투 종료(승리/패배) 공통 이벤트 — 승리 시 서버에 클리어 통지 후 그리드(갤럭시뷰)로 복귀, 패배(퇴각 포함)는 이전 셀 위치로 되돌아감
    private void OnZoneStageBattleEnd(bool isVictory)
    {
        m_standoffEnemyFleet = null;

        if (isVictory == false)
        {
            RestorePreviousCellAfterRetreat();
            return;
        }

        Debug.Log($"[DEBUG-CELL] ClearExplorationCellRequest zone={m_currentZoneNumber} row={m_currentRow} col={m_currentCol}");
        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        ClearExplorationCellRequest request = new ClearExplorationCellRequest
        {
            zoneNumber = m_currentZoneNumber,
            cellRow = m_currentRow,
            cellCol = m_currentCol,
            shipHealthRatios = myFleet != null ? myFleet.BuildHealthRatioSnapshot() : null,
            tacticPower = commanderInfo != null ? commanderInfo.tacticPower : 0,
            challengeToken = m_activeChallengeToken,
        };
        NetworkManager.Instance.ClearExplorationCell(request, OnClearExplorationCellResponse);
    }

    private void OnClearExplorationCellResponse(ApiResponse<ClearExplorationCellResponse> response)
    {
        int pointGained = 0;
        int expGained = 0;
        List<string> rewardCardCandidates = null;
        if (response.errorCode != 0)
            Debug.LogError($"[UIPanelExplorationGrid] ClearExplorationCell 실패: {response.errorCode}");
        else if (response.data != null)
        {
            pointGained = response.data.explorationPointGained;
            expGained = response.data.expGained;
            rewardCardCandidates = response.data.rewardCardCandidates; // 탈출 셀/빈 셀은 null — 카드 선택 단계를 건너뜀
            // 이 시점엔 패널이 비활성 상태(전투 화면에 가려짐) — 값은 버퍼에만 쌓아두고, 화면 반영은 OnShowUIPanel에서 처리
            m_pendingBankedRewardGain.Add(EBankedRewardType.ExplorationPoint, pointGained);
            m_pendingBankedRewardGain.Add(EBankedRewardType.Exp, expGained);
        }

        if (m_gridData != null && m_gridData.IsInBounds(m_currentRow, m_currentCol) == true)
            m_gridData.SetCellCleared(m_currentRow, m_currentCol, true);

        // 빈 셀(적 없음)은 획득 포인트도 카드도 없음 — 팝업 없이 바로 기존 흐름 진행
        if (pointGained <= 0 && rewardCardCandidates == null)
        {
            ContinueAfterCellClear();
            return;
        }

        // 탐험 포인트/경험치 안내와 보상카드 3택1을 한 팝업에서 함께 처리 — 카드 후보가 없으면(탈출 셀) 팝업이 카드 섹션만 숨기고 포인트 안내만 보여줌
        UIManager.Instance.ShowRewardCardSelectPopup(pointGained, expGained, rewardCardCandidates, selectedCardId =>
        {
            if (selectedCardId == null)
            {
                ContinueAfterCellClear();
                return;
            }
            OnRewardCardSelected(selectedCardId);
        });
    }

    private void OnRewardCardSelected(string selectedCardId)
    {
        ConfirmRewardCardRequest request = new ConfirmRewardCardRequest
        {
            zoneNumber = m_currentZoneNumber,
            cellRow = m_currentRow,
            cellCol = m_currentCol,
            selectedCardId = selectedCardId,
        };
        NetworkManager.Instance.ConfirmRewardCard(request, response =>
        {
            if (response.errorCode != 0)
                Debug.LogError($"[UIPanelExplorationGrid] ConfirmRewardCard 실패: {response.errorCode}");
            else if (response.data != null)
                ApplySelectedRewardCard(selectedCardId, response.data.explorationPointGained);

            ContinueAfterCellClear();
        });
    }

    // 지속버프면 세션 상태에 누적, 즉시효과면 그 자리에서 소모(체력 회복 등). 즉시 가산 포인트는 다음 SettleZoneEntry에 반영되도록 버퍼에 더함
    private void ApplySelectedRewardCard(string selectedCardId, int explorationPointGainedFromCard)
    {
        RewardCardData card = DataManager.Instance.m_dataTableRewardCard.GetCard(selectedCardId);
        if (card == null) return;

        if (card.isPersistent == true)
        {
            ObjectManager.Instance.m_rewardCardSessionState.ApplyCard(card);
            ObjectManager.Instance.RefreshRewardCardBuffsOnMyFleet();
            RefreshRewardCardBuffDisplay();
        }
        else if (card.effectType == ECardEffectType.Instant_HealthHeal)
        {
            SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
            Debug.Log($"[디버그-체력회복] card.effectType={card.effectType} card.value1={card.value1} myFleet={(myFleet != null ? myFleet.name : "null")}");
            if (myFleet != null)
                myFleet.HealAllShipsByRatio(card.value1);
        }
        // Instant_ShieldHeal / Instant_InterceptorHeal — 실드/요격체 시스템이 아직 없어 향후 연결 예정(TODO)

        m_pendingBankedRewardGain.Add(EBankedRewardType.ExplorationPoint, explorationPointGainedFromCard);
    }

    // 셀 클리어 후 그리드 흐름 계속 — 탈출 셀이든 아니든 먼저 그리드로 복귀시켜 최종 적립 포인트를 화면에 반영하고,
    // 탈출 셀이면 그 반영이 끝난 시점(SettleZoneEntry)에 탈출 확정 팝업을 띄움
    private void ContinueAfterCellClear()
    {
        bool reachedEscape = m_gridData != null && m_gridData.GetCell(m_currentRow, m_currentCol).isEscape;
        m_pendingShowEscapeConfirmOnSettle = reachedEscape;

        // 탈출 확정 팝업이 실제로 뜨기 전까지(카메라 정착+롤링 애니메이션 대기 구간) 그리드/빈 공간 조작이 끼어들지 못하게 미리 차단
        if (reachedEscape == true && m_inputBlockOverlay != null)
            m_inputBlockOverlay.SetActive(true);

        UIManager.Instance.ShowPanel(panelName);
    }

    // 탈출 셀 클리어 직후(SettleZoneEntry 콜백)에만 호출됨 — 확정 전 실제로 받게 될 보상(경험치/탐험 포인트)을 미리 보여줌
    // 탐험 포인트는 ExplorationService.settleZoneRun()과 동일한 계산식(적립값 * 배율, 올림)으로 미리 계산 —
    // 서버 왕복 없이 로컬에서 정확한 예상 지급액을 보여주기 위함(서버 확정 전 미리보기로 설계됨)
    private void ShowEscapeConfirmPopup()
    {
        if (m_inputBlockOverlay != null)
            m_inputBlockOverlay.SetActive(false);

        float pointRateMultiplier = ObjectManager.Instance.m_rewardCardSessionState.GetExplorationPointRateMultiplier();
        int bankedPointWithRate = Mathf.CeilToInt(m_bankedReward.Get(EBankedRewardType.ExplorationPoint) * pointRateMultiplier);

        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message = LocalizationManager.Instance.Get("UIPanelExplorationGrid_EscapeConfirmMessage"),
            rewardAmounts = new List<int> { m_bankedReward.Get(EBankedRewardType.Exp), bankedPointWithRate },
            onConfirm = OnConfirmEscape,
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
            {
                Debug.LogError($"[UIPanelExplorationGrid] EscapeExplorationZone 실패: {response.errorCode}");
                UIManager.Instance.ShowPanel(panelName); // 그리드 패널이 이미 열려 있으면 no-op, 어떤 이유로 닫혀 있었다면 복구
                return;
            }

            m_bankedReward.Clear();
            m_pendingBankedRewardGain.Clear(); // 탈출로 이미 100% 정산됨 — 다음 존으로 누수되지 않도록 함께 리셋
            RefreshBankedPointText();
            RefreshAbandonRunButtonState();
            ApplyOwnedPointRemain(response.data.explorationPointRemain);
            ApplyExpAndLevel(response.data.totalExp, response.data.commanderLevel);
            ApplyTacticPowerRecovered(response.data.tacticPower);
            ClearActiveRunZoneCache();
            ApplyHighestClearedZoneNumber(response.data.highestClearedZoneNumber);
            m_pendingFleetRepositionForZoneAdvance = true;

            // 이번 런이 종료됐으므로 다음 진입 존 번호가 이전과 같더라도(예: 같은 존 재도전) 낡은 그리드 상태(클리어 표시 등)가
            // 새는 걸 막기 위해 강제로 비움 — ConfirmAbandonRun(포기)과 동일한 이유/처리
            m_gridData = null;

            // 존 클리어(탈출 확정) 시 함대 전액 회복 — 셀 단위로는 손상이 유지되지만(로그라이트성), 존을 넘어갈 때는 초기화
            SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
            if (myFleet != null)
                myFleet.FullRepair();

            // 보상카드 지속버프도 이번 런 한정(세션 스코프)이므로 런 종료와 함께 초기화
            ObjectManager.Instance.m_rewardCardSessionState.Reset();
            ObjectManager.Instance.RefreshRewardCardBuffsOnMyFleet();
            RefreshRewardCardBuffDisplay();

            // 이미 갤럭시뷰(그리드) 중이라 EnterZone(EnterGalaxyView 기반)은 못 씀 — EnterGalaxyView는 "m_isGalaxyView==true면 no-op"라
            // 정착 이벤트가 영원히 안 옴. NavigateToZone(존 탭 브라우징)과 동일하게 FocusOnZoneAnchor로 즉시 리타겟 후 바로 재구성
            int nextZoneNumber = ObjectManager.Instance.GetInitialZoneIndex();
            ObjectManager.Instance.ChangeZone(nextZoneNumber);

            ZoneConfig nextZoneConfig = DataManager.Instance.m_dataTableZone.GetZoneByZoneIndex(nextZoneNumber);
            if (nextZoneConfig != null && CameraController.Instance != null)
                CameraController.Instance.FocusOnZoneAnchor(nextZoneConfig.galaxyCameraTarget, nextZoneConfig.galaxyCameraZoom, nextZoneConfig.galaxyCameraRotX, nextZoneConfig.galaxyCameraRotY);

            SettleZoneEntry(nextZoneNumber);
        });
    }

    // 적립 포인트가 있을 때만 포기 가능 — 없으면 포기해도 얻을 게 없어 버튼을 비활성화
    private void RefreshAbandonRunButtonState()
    {
        if (m_abandonRunButton != null)
            m_abandonRunButton.gameObject.SetActive(m_bankedReward.Get(EBankedRewardType.ExplorationPoint) > 0);
    }

    // onAnimComplete: 롤링 애니메이션이 실제로 끝난 프레임에 호출됨(코루틴을 아예 못 도는 경로에서도 즉시 호출됨) — 애니메이션과 정확히 동기화해야 하는 후속 로직(예: 탈출 확정 팝업)에 사용
    private void RefreshBankedPointText(System.Action onAnimComplete = null)
    {
        if (m_bankedExplorationPointRow == null)
        {
            onAnimComplete?.Invoke();
            return;
        }

        int bankedExplorationPoint = m_bankedReward.Get(EBankedRewardType.ExplorationPoint);
        m_bankedExplorationPointRow.SetLabel("UIPanelExplorationGrid_BankedPoint");
        m_bankedExplorationPointRow.SetValueAnimated(m_displayedBankedPoint, bankedExplorationPoint, onAnimComplete);
        m_displayedBankedPoint = bankedExplorationPoint;
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(m_bankedExplorationPointRow.transform as RectTransform);
    }

    // 탈출/포기 응답의 explorationPointRemain(확정 지급 후 보유 잔액)을 커맨더 정보에도 반영해 다른 화면(지휘력 증가 등)과 일치시킴
    // Commander.UpdateExplorationPoint()를 거쳐야 EventManager.OnExplorationPointChanged가 발행되어 다른 열린 패널도 즉시 갱신됨
    private void ApplyOwnedPointRemain(int explorationPointRemain)
    {
        if (DataManager.Instance.m_currentCommander != null)
            DataManager.Instance.m_currentCommander.UpdateExplorationPoint(explorationPointRemain);

        RefreshOwnedPointText();
    }

    // 탈출/포기 응답의 tacticPower(런 종료로 회복된 전술력 현재치)를 커맨더 정보에 반영 — 게이지 등 다른 열린 UI도 즉시 갱신
    private void ApplyTacticPowerRecovered(int tacticPower)
    {
        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        if (commanderInfo == null) return;

        commanderInfo.tacticPower = tacticPower;
        EventManager.Trigger_TacticPowerChanged(commanderInfo.tacticPower, commanderInfo.tacticPowerMax);
    }

    // 탈출/포기 정산 응답(totalExp/commanderLevel, 권위값)을 커맨더 정보에 반영 — 레벨업 시 알림 표시
    private void ApplyExpAndLevel(int totalExp, int commanderLevel)
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        int prevLevel = commander.GetCommanderLevel();
        commander.UpdateExp(totalExp);
        commander.UpdateCommanderLevel(commanderLevel);
        if (commanderLevel > prevLevel)
            UIManager.Instance.ShowCommanderLevelupNotify(commanderLevel);
    }

    // 탈출/포기로 런이 종료된 직후 로컬 캐시도 즉시 비워야 "다른 존 진행중" 판정이 이번 세션 내내 정확함
    private void ClearActiveRunZoneCache()
    {
        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        if (commanderInfo == null) return;

        commanderInfo.explorationZoneNumber = 0;
        commanderInfo.explorationCell = "";

        // 런이 끝났으니 브라우징 왕복용 스냅샷도 함께 비움 — 안 비우면 같은 존 번호로 재도전할 때 지난 런의 낡은 그리드가 재사용됨
        m_activeZoneSnapshotZoneNumber = 0;
        m_activeZoneSnapshotGridData = null;
    }

    // 탈출 성공 응답(권위값)을 즉시 반영해야, 이어지는 ShowPanel -> OnShowUIPanel의 GetInitialZoneIndex()가 다음 존을 정확히 계산함
    private void ApplyHighestClearedZoneNumber(int highestClearedZoneNumber)
    {
        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        if (commanderInfo == null) return;

        commanderInfo.highestClearedZoneNumber = highestClearedZoneNumber;
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
            message = LocalizationManager.Instance.Get("UIPanelExplorationGrid_AbandonRunConfirm"),
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

            m_bankedReward.Clear();
            m_pendingBankedRewardGain.Clear(); // 포기로 이미 정산됨 — 다음 진입 시 누수되지 않도록 함께 리셋
            RefreshBankedPointText();
            RefreshAbandonRunButtonState();
            ApplyOwnedPointRemain(response.data.explorationPointRemain);
            ApplyExpAndLevel(response.data.totalExp, response.data.commanderLevel);
            ApplyTacticPowerRecovered(response.data.tacticPower);
            ClearActiveRunZoneCache();

            // 런 자체가 완전히 종료되므로 함대 손상(체력/실드)도 다음 런을 위해 전부 복구
            SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
            if (myFleet != null)
                myFleet.FullRepair();

            // 보상카드 지속버프도 이번 런 한정(세션 스코프)이므로 런 종료와 함께 초기화
            ObjectManager.Instance.m_rewardCardSessionState.Reset();
            ObjectManager.Instance.RefreshRewardCardBuffsOnMyFleet();
            RefreshRewardCardBuffDisplay();

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

        UIManager.Instance.ShowPanel(panelName); // 승리/전투 중 퇴각과 동일하게 그리드(갤럭시뷰)로 복귀
    }

    // 전투 중 퇴각(패배 포함) — OnConfirmRetreat(대치 화면 퇴각)와 동일한 위치 복원. 적 함대 제거는 ForceEndBattle이 이미 처리했으므로 생략.
    // 서버에 셀 클리어 통지를 하지 않으므로 이 셀은 클리어되지 않은 채로 남아 나중에 다시 도전 가능.
    // 체력/전술력도 전투 진입 직전 스냅샷으로 롤백 — 클리어 실패 시 손실 없이 다시 도전 가능해야 퇴각 기능이 실질적 의미를 가짐(성공 시엔 롤백하지 않고 클리어 확정값을 그대로 서버에 전송)
    private void RestorePreviousCellAfterRetreat()
    {
        m_currentRow = m_previousRow;
        m_currentCol = m_previousCol;
        ObjectManager.Instance.SetMyFleetPosition(m_previousFleetWorldPos, 0f);
        CameraController.Instance.SnapToTarget();

        ApplyFleetHealthSnapshot(m_previousShipHealthRatios);

        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        if (commanderInfo != null)
        {
            commanderInfo.tacticPower = m_previousTacticPower;
            EventManager.Trigger_TacticPowerChanged(commanderInfo.tacticPower, commanderInfo.tacticPowerMax);
        }

        UIManager.Instance.ShowPanel(panelName);
    }

    private void RefreshCellStates()
    {
        // 존이 순차 진행이라는 전제하에, 현재 존 번호가 서버 권위값(최고 탈출 존) 이하면 이 존은 예전에 반드시 한 번 탈출한 적이 있음
        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        bool escapedZoneBefore = commanderInfo != null && m_currentZoneNumber <= commanderInfo.highestClearedZoneNumber;

        foreach (var kv in m_activeButtons)
        {
            int row = kv.Key.row;
            int col = kv.Key.col;
            GridCell3D button = kv.Value;
            GridCellData cellData = m_gridData.GetCell(row, col);

            bool isCurrentCell = row == m_currentRow && col == m_currentCol;
            bool isAdjacent = m_gridData.IsAdjacent(m_currentRow, m_currentCol, row, col);

            EGridCellVisualState state;
            if (cellData.isBlocked) state = EGridCellVisualState.Blocked; // 통행불가 셀은 인접해도 항상 숨김
            else if (isCurrentCell) state = EGridCellVisualState.Current;
            else if (isAdjacent) state = EGridCellVisualState.Reachable;
            else if (cellData.isCleared) state = EGridCellVisualState.Cleared;
            else state = EGridCellVisualState.Unvisited;

            button.SetVisualState(state, cellData.isCleared, escapedZoneBefore);
        }
    }

    private GridCell3D GetButtonFromPool()
    {
        if (m_buttonPool.Count > 0)
        {
            GridCell3D button = m_buttonPool[^1];
            m_buttonPool.RemoveAt(m_buttonPool.Count - 1);
            return button;
        }
        return Instantiate(m_cellPrefab, m_cellRoot);
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
