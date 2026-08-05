// 함대편성 UI — FleetComposition 기반, UIManager가 관리하는 독립 패널(다른 진입 화면과 배타적)
// 배치된 함선 전방/후방 토글, 클릭 시 성능 컬럼에 상세 스탯 표시, 타입선택 버튼으로 함선 프리셋 교체(UIShipPresetPickerView)
// 상단 FleetStats(항상 보이는 지휘력/배치 수 요약)와 2열(함대구성/성능)은 역할이 분리됨 —
// 성능 컬럼은 순수하게 "선택한 함선의 상세 스탯"만 담당(선택 없으면 비어있음)
// 행은 프리팹에 미리 배치하지 않고, 필요한 개수만큼 풀에서 동적으로 늘려가며 사용(부족하면 Instantiate, 남으면 비활성화)
// 이 패널이 열리면 화면 좌측 3D 카메라 viewport를 축소해 우측에 자리를 만듦(카메라 애니메이션은 CameraController가 담당,
// 여기서는 열고 닫을 시점과 목표 폭만 결정) — 애니메이션이 끝나기 전엔 내용을 CanvasGroup으로 가려둠(구 TabSystem deferReveal 대체)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelFleet : UIPanelBase
{
    [SerializeField] private RowLabelValue m_fleetStatsRowPrefab; // 상단 FleetStats 전용(지휘력/배치 함선 수) — 항상 텍스트로 표시
    [SerializeField] private UIStatGaugeRow m_statsRowPrefab;     // 성능 컬럼 전용 — 선택한 함선의 상세 스탯
    [SerializeField] private UIPlacedShipRow m_placedShipRowPrefab;
    [SerializeField] private InfiniteScrollView m_placedShipsScrollView; // 배치된 함선 목록 — 세로 가상 스크롤(PlacedShipsContainer 아래 배치된 스크롤뷰)
    [SerializeField] private UIShipPresetPickerView m_shipPresetPicker; // 함선 타입선택 버튼을 누르면 뜨는 프리셋 선택 팝업(UIPanelFleet 루트 아래 내장, PlacedShipsContainer와는 별개)

    [SerializeField] private RectTransform m_columnContainer; // 2열(함대구성/성능)을 감싸는 최상위 컨테이너 — Horizontal Layout Group. 각 열 안에도 Title+행 컨테이너를 감싸는 Vertical Layout Group이 있어, 리빌드는 이 최상위에서 한 번만 해도 하위가 전부 재계산됨
    [SerializeField] private RectTransform m_fleetStatsContainer; // 상단 요약 영역(FleetStats/Container)
    [SerializeField] private RectTransform m_placedShipsContainer;
    [SerializeField] private RectTransform m_statsContainer;
    [SerializeField] private Button m_increaseCommandPowerButton; // 탐험 포인트 100 소모 -> 지휘력 최대치 10 증가(교환비는 ExplorationService 서버값과 항상 함께 수정)
    [SerializeField] private RowLabelValue m_ownedExplorationPointRow; // 보유 탐험 포인트 — 지휘력 증가 버튼이 바로 이 값을 소모하므로 버튼 옆에 전용으로 배치

    [Header("카메라 Viewport 애니메이션")]
    [SerializeField] private float m_animDuration = 0.3f;

    private readonly List<RowLabelValue> m_fleetStatsRows = new();
    private readonly List<UIStatGaugeRow> m_statsRows = new();

    // 배치된 함선 목록 — InfiniteScrollView가 화면에 보이는 행만 OnPlacedShipItemBind로 바인딩하므로,
    // 전체 슬롯 데이터/개수는 여기 캐싱해두고 바인드 시점에 조회
    private List<PlacedShipView> m_placedShipsCache = new();
    private int m_placedTotalSlotCount; // 잠긴 칸 포함 전체 슬롯 수(=InfiniteScrollView totalCount)
    private int m_placedOpenSlotCount;  // 잠기지 않은(배치 가능한) 슬롯 수

    private long m_displayedOwnedExplorationPoint = -1; // 롤링 애니메이션의 시작값 추적 — 최초 1회는 -1이라 즉시 표시됨(UIResourceBar와 동일 패턴)

    private int m_selectedSlotIndex = -1; // 선택된 배치 슬롯 — -1이면 성능 컬럼은 비어있음. 프리셋 ID는 저장하지 않고 매번 슬롯에서 다시 조회(교체돼도 항상 최신 반영)

    // 읽기전용 모드(적 함대 열람) — true면 편집 전용 UI(3열/지휘력 요약/전후방 토글)를 숨기고 데이터도 FleetComposition 대신 m_targetFleet에서 읽음
    private bool m_isReadOnlyMode = false;
    private SpaceFleet m_targetFleet;

    // OpenForFleet가 세팅하고 OnShowUIPanel이 소비하는 1회성 요청 — ShowPanel()이 이미 열려있는 패널에는 OnShowUIPanel을 다시 호출하지 않으므로,
    // "이미 열려있는데 다른 함대로 전환" 케이스는 OpenForFleet가 직접 SwitchTargetFleet으로 처리하고 이 플래그는 소비하지 않음
    private bool m_hasPendingOpenRequest = false;
    private SpaceFleet m_pendingTargetFleet;
    private bool m_pendingIsReadOnly;

    private CanvasGroup m_canvasGroup;

    // 이 패널 좌측 경계 기준 카메라 viewport 열림 폭
    private float m_openCameraWidth;
    private bool m_isViewportOpen = false;

    // 카메라 rect(viewport)가 축소되는 동안 그 바깥 영역(경계면 3D 잔상)을 가리는 배경 — 이 패널의 첫 자식으로 생성
    private RectTransform m_cameraViewportBgRect;
    private Image m_cameraViewportBgImage;

    // 닫힐 때 카메라가 복귀할 rect(x, width) — 기본은 풀스크린(0,1)이지만, UIFleetStandoffView처럼 좌우 분할 화면 위에서
    // 열렸을 경우 그 화면이 원하는 절반 rect로 복귀해야 하므로 활성 중일 때만 재정의 가능하게 함
    private System.Func<Rect> m_closedCameraRectProvider = null;
    public void SetClosedCameraRectProvider(System.Func<Rect> provider) { m_closedCameraRectProvider = provider; }

    private Rect GetClosedCameraRect()
    {
        return m_closedCameraRectProvider != null ? m_closedCameraRectProvider() : new Rect(0f, 0f, 1f, 1f);
    }

    public override void InitializeUIPanel()
    {
        m_canvasGroup = GetComponent<CanvasGroup>();
        if (m_canvasGroup == null)
            m_canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // 프리팹에 미리 배치해둔 행이 있으면 초기 풀로 흡수 — 없어도 무방(전부 동적 생성으로 채워짐)
        if (m_fleetStatsContainer != null)
            m_fleetStatsRows.AddRange(m_fleetStatsContainer.GetComponentsInChildren<RowLabelValue>(true));
        if (m_statsContainer != null)
            m_statsRows.AddRange(m_statsContainer.GetComponentsInChildren<UIStatGaugeRow>(true));
        if (m_placedShipsScrollView != null)
            m_placedShipsScrollView.onItemBind = OnPlacedShipItemBind;

        EventManager.Subscribe_CommanderLevelChanged(OnCommanderLevelChanged);

        if (m_increaseCommandPowerButton != null)
            m_increaseCommandPowerButton.onClick.AddListener(OnIncreaseCommandPowerButtonClicked);

        m_openCameraWidth = ComputeOpenCameraWidth();
        CreateCameraViewportBackground();
        ApplyViewportImmediate(open: false);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_CommanderLevelChanged(OnCommanderLevelChanged);
    }

    public override void OnShowUIPanel()
    {
        // OpenForFleet가 미리 세팅해둔 요청이 있으면 그 함대/모드로, 없으면(FLEET 진입 버튼 등 일반 경로) 항상 내 함대 편집 모드가 기본값
        if (m_hasPendingOpenRequest == true)
        {
            m_targetFleet = m_pendingTargetFleet;
            m_isReadOnlyMode = m_pendingIsReadOnly;
            m_hasPendingOpenRequest = false;
        }
        else
        {
            m_targetFleet = ObjectManager.Instance.GetMyFleet();
            m_isReadOnlyMode = false;
        }

        // 이 패널이 열려있는 동안엔 3D에서 다른 내 함선을 클릭해도 그 함선으로 선택이 갱신되어야 함 —
        // UIPanelSpace는 자기가 하이드된 동안 이 이벤트 구독을 끊으므로, 여기서 이 패널이 열려있는 동안만 직접 구독
        EventManager.Subscribe_SpaceShipSelected(OnShipSelectedWhileOpen);

        // 읽기전용(적 함대 열람)은 카메라를 전혀 건드리지 않음 — 호출부(UIFleetStandoffView)가 이미 구성해둔
        // 좌우 분할 상태(적 카메라 좌측/내 카메라 우측)를 그대로 두고 이 패널만 우측에 겹쳐 뜨게 함
        if (m_isReadOnlyMode == true)
        {
            m_canvasGroup.alpha = 1f;
            m_canvasGroup.blocksRaycasts = true;
            RefreshFleetComposition();
            SyncShipSelectionOnOpen();
            return;
        }

        // 카메라 viewport 축소 애니메이션이 끝나기 전엔 내용을 가려둠 — RevealAfterViewportAnimation()에서 복원
        m_canvasGroup.alpha = 0f;
        m_canvasGroup.blocksRaycasts = false;

        RefreshFleetComposition();
        StartViewportAnimation(open: true);
    }

    public override void OnHideUIPanel()
    {
        EventManager.Unsubscribe_SpaceShipSelected(OnShipSelectedWhileOpen);
        ClearSelectedShipOutline();
        StartViewportAnimation(open: false);
    }

    // 패널이 닫혀도 선택된 함선의 3D 아웃라인은 자동으로 꺼지지 않음(빈 공간 클릭 등 별도 이벤트가 있어야 꺼짐) —
    // 패널을 닫는 시점엔 그 선택 자체가 더 이상 유효하지 않으므로 여기서 직접 꺼준다
    private void ClearSelectedShipOutline()
    {
        if (m_selectedSlotIndex < 0) return;

        SpaceFleet syncFleet = m_isReadOnlyMode == true ? m_targetFleet : ObjectManager.Instance.GetMyFleet();
        if (syncFleet == null) return;

        SpaceShip ship = syncFleet.m_ships.Find(s => s != null && s.m_shipInfo.positionIndex == m_selectedSlotIndex);
        if (ship != null)
            ship.SetShipSelected(false);
    }

    // 3D 함선 클릭은 CameraController.HandleModuleSelection이 내 함대 소속이 아니면 애초에 이 이벤트를 발행하지 않으므로 항상 내 함대 기준 —
    // 패널이 직전에 적 함대 읽기전용 모드였더라도 여기서 내 함대 편집 모드로 되돌림
    private void OnShipSelectedWhileOpen(SpaceShip ship)
    {
        if (ship == null || ship.m_ownerFleet == null) return;
        // OnPlacedShipRowClickedFromUI(SyncShipSelectionOnOpen 등)가 읽기전용 모드에서 적 함선으로도 이 이벤트를 재발행하므로,
        // "3D 클릭은 항상 내 함대"라는 가정이 깨짐 — 실제로 내 함대 소속일 때만 편집 모드로 강제 전환
        if (ObjectManager.Instance.IsEnemyOfMyTeam(ship.m_ownerFleet) == true) return;

        SwitchTargetFleet(ObjectManager.Instance.GetMyFleet(), isReadOnly: false);
        SelectPlacedShipByPositionIndex(ship.m_shipInfo.positionIndex);
    }

    // 외부 진입점 — 적 함대(읽기전용) 또는 내 함대를 명시적으로 지정해 이 패널을 연다. UIFleetStandoffView가 좌측 적 함선 클릭 시 사용
    public void OpenForFleet(SpaceFleet fleet, bool isReadOnly, int selectedPositionIndex)
    {
        m_pendingTargetFleet = fleet;
        m_pendingIsReadOnly = isReadOnly;
        m_hasPendingOpenRequest = true;

        // 이미 열려있으면 ShowPanel이 OnShowUIPanel을 다시 호출하지 않아 위 pending 요청이 소비되지 않으므로 직접 전환
        bool wasAlreadyOpen = UIManager.Instance.GetCurrentActivePanelName() == panelName;
        UIManager.Instance.ShowPanel(panelName);

        if (wasAlreadyOpen == true)
        {
            m_hasPendingOpenRequest = false;
            SwitchTargetFleet(fleet, isReadOnly);
        }

        SelectPlacedShipByPositionIndex(selectedPositionIndex);
    }

    private void SwitchTargetFleet(SpaceFleet fleet, bool isReadOnly)
    {
        m_targetFleet = fleet;
        m_isReadOnlyMode = isReadOnly;

        // 읽기전용 -> 편집 모드로 전환되면 카메라를 확장해야 함(이미 열려있으면 StartViewportAnimation 내부 가드로 no-op).
        // 편집 -> 읽기전용 전환은 이미 확장돼 있던 카메라를 그대로 둬도 무방해 별도 축소 처리는 하지 않음
        if (isReadOnly == false)
            StartViewportAnimation(open: true);

        RefreshFleetComposition();
    }

    // 애니메이션 종료 직후 호출 — 내용 노출 + 애니메이션 도중 바뀐 사이즈 기준으로 레이아웃 재계산
    private void RevealAfterViewportAnimation()
    {
        m_canvasGroup.alpha = 1f;
        m_canvasGroup.blocksRaycasts = true;
        RefreshFleetComposition();
        SyncShipSelectionOnOpen();
    }

    // 패널이 열릴 때마다 항상 호출 — 3D 아웃라인은 패널이 닫히는 동안 꺼진 채로 남으므로(m_selectedSlotIndex는 그대로 유지),
    // "이미 선택돼 있으니 건너뛴다"는 판단을 하면 안 되고 매번 현재 선택 슬롯(없으면 0번=기함)을 3D에 재동기화해야 함.
    // 3D 함선 클릭으로 진입한 경우 OnShowUIPanel 직후 SelectPlacedShipByPositionIndex가 이미 m_selectedSlotIndex를 실제 클릭한 함선으로
    // 갱신해두므로, 여기선 그 값을 그대로 다시 선택해 재확인하는 셈이라 문제 없음
    private void SyncShipSelectionOnOpen()
    {
        List<PlacedShipView> placedShips = GetCurrentPlacedShips();
        if (placedShips.Count == 0) return;

        int index = m_selectedSlotIndex >= 0 && m_selectedSlotIndex < placedShips.Count ? m_selectedSlotIndex : 0;
        OnPlacedShipRowClickedFromUI(index, placedShips[index].shipPresetId);
    }

    // 이 패널 좌측 경계의 실제 스크린 좌표 기준으로 카메라 viewport 비율 계산
    // Screen Space - Overlay 캔버스는 world 좌표가 곧 스크린 픽셀 좌표와 1:1이라
    // CanvasScaler 스케일팩터/레퍼런스 해상도와 무관하게 항상 정확한 비율이 나옴
    private float ComputeOpenCameraWidth()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) return 1f;

        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        float leftEdgeScreenX = corners[0].x;

        return Mathf.Clamp01(leftEdgeScreenX / Screen.width);
    }

    // 패널 콘텐츠보다 먼저(가장 아래) 그려지도록 이 패널의 첫 자식으로 생성
    private void CreateCameraViewportBackground()
    {
        GameObject bgObj = new GameObject("CameraViewportBg");
        m_cameraViewportBgRect = bgObj.AddComponent<RectTransform>();
        bgObj.AddComponent<CanvasRenderer>();
        m_cameraViewportBgImage = bgObj.AddComponent<Image>();
        m_cameraViewportBgImage.color = Color.black;
        m_cameraViewportBgImage.raycastTarget = false;

        m_cameraViewportBgRect.SetParent(transform, false);
        m_cameraViewportBgRect.SetAsFirstSibling();
        m_cameraViewportBgRect.anchorMin = new Vector2(1f, 0f);
        m_cameraViewportBgRect.anchorMax = Vector2.one;
        m_cameraViewportBgRect.offsetMin = Vector2.zero;
        m_cameraViewportBgRect.offsetMax = Vector2.zero;

        bgObj.SetActive(false);
    }

    // 카메라 rect width에 맞춰 배경이 덮는 영역(camWidth ~ 1)을 갱신
    private void UpdateCameraViewportBackground(float camWidth)
    {
        if (m_cameraViewportBgRect == null) return;

        if (camWidth >= 1f)
        {
            m_cameraViewportBgRect.gameObject.SetActive(false);
            return;
        }

        m_cameraViewportBgRect.gameObject.SetActive(true);
        Vector2 anchorMin = m_cameraViewportBgRect.anchorMin;
        anchorMin.x = camWidth;
        m_cameraViewportBgRect.anchorMin = anchorMin;
    }

    // open=true: 좌측 카메라를 축소해 이 패널에 자리를 내줌 / false: 원래 rect(기본 풀스크린)로 복귀
    private void StartViewportAnimation(bool open)
    {
        if (open == m_isViewportOpen) return;
        m_isViewportOpen = open;

        Rect closedRect = GetClosedCameraRect();
        float targetX = open ? 0f : closedRect.x;
        float targetWidth = open ? m_openCameraWidth : closedRect.width;

        // 닫힘 목표가 풀스크린이 아니면(대치 화면 등) 카메라 우측을 덮는 로비 전용 배경 오버레이는 의미가 없으므로 끔
        bool useLobbyBackground = m_closedCameraRectProvider == null;
        if (useLobbyBackground == false)
            UpdateCameraViewportBackground(1f);

        CameraController.Instance.AnimateViewportRect(targetX, targetWidth, m_animDuration,
            onProgress: (x, width) => { if (useLobbyBackground == true) UpdateCameraViewportBackground(width); },
            onComplete: () =>
            {
                if (useLobbyBackground == false && open == false)
                    UpdateCameraViewportBackground(1f);
                if (open == true)
                    RevealAfterViewportAnimation();
            });
    }

    // 애니메이션 없이 즉시 카메라 rect를 적용 — 초기화 시점, 그리고 외부 진입점(UIFleetStandoffView 등)이
    // 자기 화면을 닫는 도중이라 카메라 rect provider가 곧 해제될 예정일 때 경쟁 상태를 피하기 위해 사용
    private void ApplyViewportImmediate(bool open)
    {
        Rect closedRect = GetClosedCameraRect();
        float targetX = open ? 0f : closedRect.x;
        float targetWidth = open ? m_openCameraWidth : closedRect.width;

        CameraController.Instance.SetViewportRect(targetX, targetWidth);
        UpdateCameraViewportBackground(m_closedCameraRectProvider == null ? targetWidth : 1f);
        EventManager.TriggerCameraViewportChanged(open ? 1f : 0f);
    }

    // 외부 진입점(UIFleetStandoffView 등)이 이 패널이 열려있으면 즉시(애니메이션 없이) 닫음 —
    // 자기 화면 자체를 닫는 도중이라 카메라 rect provider가 곧 해제될 예정이므로, 애니메이션 도중 provider가 사라져
    // 잘못된 목표(풀스크린)로 튀는 경쟁 상태를 피하기 위해 애니메이션을 쓰지 않고 즉시 스냅한다
    public void CloseImmediateIfOpen()
    {
        if (UIManager.Instance.GetCurrentActivePanelName() != panelName) return;

        CameraController.Instance.StopViewportAnimation();
        m_isViewportOpen = false; // OnHideUIPanel의 StartViewportAnimation 가드에 걸려 재실행되지 않게 미리 갱신
        UIManager.Instance.HideCurrentPanel();
        ApplyViewportImmediate(open: false);
    }

    private void OnCommanderLevelChanged(int commanderLevel)
    {
        RefreshFleetComposition();
    }

    public void RefreshFleetComposition()
    {
        if (m_isReadOnlyMode == false)
        {
            FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
            if (composition == null) return;
            RefreshFleetStatsSummary(composition);
        }

        SetEditOnlyUIVisible(m_isReadOnlyMode == false);
        RefreshPlacedShips();
        RefreshStats();

        RebuildAllLayouts();
    }

    // 편집 전용 UI(지휘력 요약/증가 버튼) — 읽기전용 모드(적 함대 열람)에서는 숨김
    private void SetEditOnlyUIVisible(bool visible)
    {
        if (m_fleetStatsContainer != null)
            m_fleetStatsContainer.gameObject.SetActive(visible);
        if (m_increaseCommandPowerButton != null)
            m_increaseCommandPowerButton.gameObject.SetActive(visible);
        if (m_ownedExplorationPointRow != null)
            m_ownedExplorationPointRow.gameObject.SetActive(visible);
    }

    // "배치 함선 1건"의 데이터 소스를 편집 모드(FleetComposition)/읽기전용 모드(SpaceFleet.m_fleetInfo)에서 공통 형태로 정규화
    private readonly struct PlacedShipView
    {
        public readonly string shipPresetId;
        public readonly bool isFront;
        public PlacedShipView(string shipPresetId, bool isFront)
        {
            this.shipPresetId = shipPresetId;
            this.isFront = isFront;
        }
    }

    private List<PlacedShipView> GetCurrentPlacedShips()
    {
        List<PlacedShipView> result = new();

        if (m_isReadOnlyMode == true)
        {
            List<ShipInfo> ships = m_targetFleet != null && m_targetFleet.m_fleetInfo != null ? m_targetFleet.m_fleetInfo.ships : null;
            if (ships == null) return result;

            for (int i = 0; i < ships.Count; i++)
                result.Add(new PlacedShipView(ships[i].shipPresetId, ships[i].isFront));
            return result;
        }

        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        List<FleetSlotEntry> entries = composition != null ? composition.GetPlacedShips() : null;
        if (entries == null) return result;

        for (int i = 0; i < entries.Count; i++)
            result.Add(new PlacedShipView(entries[i].shipPresetId, entries[i].isFront));
        return result;
    }

    // 함선 배치/선택 등으로 컬럼 내용이 바뀔 때마다 관련 컨테이너 전체를 한 번에 리빌드
    private void RebuildAllLayouts()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_placedShipsContainer);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_statsContainer);
        if (m_fleetStatsContainer != null && m_fleetStatsContainer.gameObject.activeInHierarchy == true)
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_fleetStatsContainer);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_columnContainer);
    }

    // 풀에 행이 부족하면 새로 Instantiate해서 채움 — 실제 활성/비활성은 호출부가 SetRow()/Hide()로 처리
    private void EnsureRowCount<T>(List<T> pool, RectTransform container, T prefab, int neededCount) where T : Component
    {
        if (container == null || prefab == null) return;

        while (pool.Count < neededCount)
            pool.Add(Instantiate(prefab, container));
    }

    // 상단 — 항상 보이는 요약(지휘력 사용량/최대치, 배치 함선 수)
    private void RefreshFleetStatsSummary(FleetComposition composition)
    {
        const int k_summaryRowCount = 2;
        EnsureRowCount(m_fleetStatsRows, m_fleetStatsContainer, m_fleetStatsRowPrefab, k_summaryRowCount);

        int usedCommandPower = composition.GetUsedCommandPower();
        int maxCommandPower = composition.GetMaxCommandPower();
        int placedShipCount = composition.GetPlacedShips().Count;

        if (m_fleetStatsRows.Count > 0)
            m_fleetStatsRows[0].SetRow("UITabCommander_CommandPower", $"{usedCommandPower} / {maxCommandPower}", rawValue: true);

        if (m_fleetStatsRows.Count > 1)
            m_fleetStatsRows[1].SetRow("UIFleet_PlacedShipCount", $"{placedShipCount}", rawValue: true);

        for (int i = k_summaryRowCount; i < m_fleetStatsRows.Count; i++)
            m_fleetStatsRows[i].Hide();

        RefreshOwnedExplorationPointRow();
    }

    // 지휘력 증가 버튼이 소모하는 값 — m_fleetStatsRows 풀과 무관하게 전용 필드로 직접 관리
    private void RefreshOwnedExplorationPointRow()
    {
        if (m_ownedExplorationPointRow == null) return;

        CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        int ownedExplorationPoint = commanderInfo != null ? commanderInfo.explorationPoint : 0;

        m_ownedExplorationPointRow.SetLabel("UIPanelExplorationGrid_OwnedPoint");
        m_ownedExplorationPointRow.SetValueAnimated(m_displayedOwnedExplorationPoint, ownedExplorationPoint);
        m_displayedOwnedExplorationPoint = ownedExplorationPoint;
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_ownedExplorationPointRow.transform as RectTransform);

    }

    // 서버 ExplorationService.COMMAND_POWER_MAX_COST와 동일한 값 — 서버 값 변경 시 반드시 함께 수정
    private const int k_increaseCommandPowerExplorationPointCost = 100;

    // 확인 팝업 없이 즉시 소모 — 탐험 포인트 100 -> 지휘력 최대치 10(서버 ExplorationService 교환비와 동일)
    private void OnIncreaseCommandPowerButtonClicked()
    {
        CommanderInfo currentCommanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
        int currentExplorationPoint = currentCommanderInfo != null ? currentCommanderInfo.explorationPoint : 0;
        if (currentExplorationPoint < k_increaseCommandPowerExplorationPointCost) return;

        NetworkManager.Instance.IncreaseCommandPowerMax(new IncreaseCommandPowerMaxRequest(), response =>
        {
            if (response.errorCode != 0)
            {
                Debug.LogError($"[UIPanelFleet] IncreaseCommandPowerMax 실패: {response.errorCode}");
                return;
            }

            FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
            if (composition != null)
                composition.SetMaxCommandPower(response.data.commandPowerMax);

            CommanderInfo commanderInfo = DataManager.Instance.m_currentCommander != null ? DataManager.Instance.m_currentCommander.m_commanderInfo : null;
            if (commanderInfo != null)
            {
                commanderInfo.commandPowerMax = response.data.commandPowerMax;
                commanderInfo.explorationPoint = response.data.explorationPointRemain;
            }

            RefreshFleetComposition();
        });
    }

    private void RefreshPlacedShips()
    {
        m_placedShipsCache = GetCurrentPlacedShips();

        if (m_isReadOnlyMode == true)
        {
            // 읽기전용(적 함대)에는 커맨더 레벨/잠긴 슬롯 개념이 없음 — 실제 배치된 함선 수만큼만 표시
            m_placedTotalSlotCount = m_placedShipsCache.Count;
            m_placedOpenSlotCount = m_placedShipsCache.Count;
        }
        else
        {
            // 지휘관 레벨에 따라 배치 가능한 함선 수(=열려있는 슬롯 수)가 달라짐 — 커맨더 탭의 "최대 함선 수"와 동일한 값 재사용
            Commander commander = DataManager.Instance.m_currentCommander;
            int commanderLevel = commander != null ? commander.GetCommanderLevel() : 0;
            m_placedOpenSlotCount = DataManager.Instance.m_dataTableCommander.GetShipCount(commanderLevel);

            // 디자인상 최대 슬롯 수(잠긴 칸 포함)까지는 항상 노출 — 레벨업 동기부여를 위해 잠긴 칸도 미리 보여줌
            int maxSlotCount = DataManager.Instance.m_dataTableCommander.GetMaxShipCount();
            m_placedTotalSlotCount = Mathf.Max(maxSlotCount, m_placedShipsCache.Count); // 배치 수가 어떤 이유로 슬롯 수를 넘어도(레벨 하락 등) 표시는 유지
        }

        if (m_placedShipsScrollView != null && m_placedShipRowPrefab != null)
            m_placedShipsScrollView.Initialize(m_placedTotalSlotCount, m_placedShipRowPrefab.gameObject);
    }

    // InfiniteScrollView가 dataIndex번 슬롯을 화면에 배치할 때마다 호출 — 캐시된 데이터로 실제 바인딩
    private void OnPlacedShipItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_placedTotalSlotCount) return;

        UIPlacedShipRow row = rowObject.GetComponent<UIPlacedShipRow>();
        if (row == null) return;

        if (dataIndex < m_placedShipsCache.Count)
        {
            PlacedShipView entry = m_placedShipsCache[dataIndex];
            System.Action<int, bool> onFrontToggled = m_isReadOnlyMode == true ? null : OnShipFrontToggled;
            System.Action<int> onTypeSelectClicked = m_isReadOnlyMode == true ? null : OnShipTypeSelectClicked;
            row.Setup(dataIndex, entry.shipPresetId, entry.isFront, onFrontToggled, OnPlacedShipRowClickedFromUI, onTypeSelectClicked, showFrontToggle: m_isReadOnlyMode == false);
            row.SetSelected(dataIndex == m_selectedSlotIndex);
        }
        else if (dataIndex < m_placedOpenSlotCount)
        {
            // 빈 슬롯도 읽기전용 모드에선 애초에 m_placedOpenSlotCount==m_placedShipsCache.Count라 이 분기에 오지 않음 — 편집 모드 전용
            row.SetEmpty(dataIndex, OnShipTypeSelectClicked);
        }
        else
        {
            row.SetLocked(dataIndex);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(rowObject.transform as RectTransform);
    }

    // 성능 컬럼 — 선택된 함선이 없으면 비어있음, 있으면 그 함선의 상세 스탯(팝업과 동일한 항목 구성)
    // 선택은 슬롯 인덱스로만 추적하고 프리셋은 매번 여기서 다시 조회 — 드래그로 그 슬롯의 함선이 교체돼도 최신 프리셋을 보여줌
    private void RefreshStats()
    {
        List<PlacedShipView> placedShips = GetCurrentPlacedShips();

        bool hasSelection = m_selectedSlotIndex >= 0 && m_selectedSlotIndex < placedShips.Count;
        if (hasSelection == false)
        {
            m_selectedSlotIndex = -1;
            for (int i = 0; i < m_statsRows.Count; i++)
                m_statsRows[i].Hide();
            return;
        }

        ShipPresetData selectedPreset = DataManager.Instance.m_dataTableShipPreset.GetShipPreset(placedShips[m_selectedSlotIndex].shipPresetId);
        if (selectedPreset == null)
        {
            m_selectedSlotIndex = -1; // 프리셋을 못 찾으면(배치 해제 등) 선택 해제
            for (int i = 0; i < m_statsRows.Count; i++)
                m_statsRows[i].Hide();
            return;
        }

        List<ShipStatGaugeEntry> entries = ShipStatGaugeBuilder.Build(selectedPreset);
        EnsureRowCount(m_statsRows, m_statsContainer, m_statsRowPrefab, entries.Count);

        for (int i = 0; i < m_statsRows.Count; i++)
        {
            if (i >= entries.Count)
            {
                m_statsRows[i].Hide();
                continue;
            }

            ShipStatGaugeEntry entry = entries[i];
            switch (entry.mode)
            {
                case EGaugeMode.Normal:
                    m_statsRows[i].SetGauge(entry.label, entry.value, entry.gaugeMax);
                    break;
                case EGaugeMode.Reverse:
                    m_statsRows[i].SetReverseGauge(entry.label, entry.rawValueText, entry.reverseFillAmount);
                    break;
                default:
                    m_statsRows[i].SetValueOnly(entry.label, entry.rawValueText);
                    break;
            }
        }
    }

    // 커맨더 레벨 기준으로 배치 가능한 프리셋만 필터링 — 함선 프리셋 선택 팝업(UIShipPresetPickerView)을 열 때마다 새로 계산
    private List<ShipPresetData> ComputeUnlockedPresets()
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        int commanderLevel = commander != null ? commander.GetCommanderLevel() : 0;
        List<ShipPresetData> allPresets = DataManager.Instance.m_dataTableShipPreset.GetShipPresetDataList();

        List<ShipPresetData> unlockedPresets = new();
        for (int i = 0; i < allPresets.Count; i++)
        {
            if (allPresets[i].unlockCommanderLevel <= commanderLevel)
                unlockedPresets.Add(allPresets[i]);
        }
        return unlockedPresets;
    }

    // ── 배치된 함선 — 전방/후방 토글 ──────────────────────────────────
    private void OnShipFrontToggled(int index, bool isFront)
    {
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition == null) return;

        composition.SetFront(index, isFront);
        ObjectManager.Instance.SetMyFleetShipFront(index, isFront);
        NetworkManager.Instance.SetFleetPresetShipFront(new FleetPresetSetFrontRequest
        {
            slotIndex = index,
            isFront = isFront,
        });

        // 전방/후방 값만 바뀌고 슬롯 구성(개수/배치)은 그대로라, 가상 스크롤 전체를 다시 Initialize하는
        // RefreshFleetComposition() 대신 캐시만 갱신하고 화면에 보이는 행만 다시 바인딩(타입선택 버튼 등이 움찔거리지 않게)
        m_placedShipsCache = GetCurrentPlacedShips();
        if (m_placedShipsScrollView != null)
            m_placedShipsScrollView.RefreshVisible();
    }

    // ── 배치된 함선 — 행 클릭(성능 컬럼에 상세 스탯 표시) ───────────────
    private void OnPlacedShipRowClicked(int index, string shipPresetId)
    {
        m_selectedSlotIndex = index;
        // 가상 스크롤이라 화면 밖 행은 살아있지 않음 — 현재 보이는 행들만 다시 바인딩해 선택 표시를 갱신
        if (m_placedShipsScrollView != null)
            m_placedShipsScrollView.RefreshVisible();
        RefreshStats();
        RebuildAllLayouts();
    }

    // UI 행 클릭 전용 진입점 — 3D 함선에도 선택 이벤트를 재발행해 아웃라인이 동기화되게 함.
    // SelectPlacedShipByPositionIndex(3D→UI 동기화 경로)에서는 이 래퍼를 거치지 않아 이벤트 재발행 무한루프를 피함
    private void OnPlacedShipRowClickedFromUI(int index, string shipPresetId)
    {
        OnPlacedShipRowClicked(index, shipPresetId);

        // 편집 모드는 항상 살아있는 내 함대를 다시 조회(전투/씬 전환으로 인스턴스가 바뀌어도 최신 반영), 읽기전용은 열릴 때 주입된 대상 함대 사용
        SpaceFleet syncFleet = m_isReadOnlyMode == true ? m_targetFleet : ObjectManager.Instance.GetMyFleet();
        if (syncFleet == null) return;

        SpaceShip ship = syncFleet.m_ships.Find(s => s != null && s.m_shipInfo.positionIndex == index);
        if (ship != null)
            EventManager.Trigger_SpaceShipSelected(ship);
    }

    // 3D 함선 클릭 등 외부 진입점에서 특정 함선을 선택 상태로 표시 — positionIndex는 SpaceShip.m_shipInfo.positionIndex와 동일 기준(슬롯 순서)
    public void SelectPlacedShipByPositionIndex(int positionIndex)
    {
        List<PlacedShipView> placedShips = GetCurrentPlacedShips();
        if (positionIndex < 0 || positionIndex >= placedShips.Count) return;

        OnPlacedShipRowClicked(positionIndex, placedShips[positionIndex].shipPresetId);
    }

    // ── 함선 타입선택 버튼 — 프리셋 선택 팝업 오픈/적용 ─────────────────
    private void OnShipTypeSelectClicked(int index)
    {
        if (m_isReadOnlyMode == true || m_shipPresetPicker == null) return;

        List<PlacedShipView> placedShips = GetCurrentPlacedShips();
        string currentPresetId = index < placedShips.Count ? placedShips[index].shipPresetId : null;
        ShipPresetData currentPreset = string.IsNullOrEmpty(currentPresetId) == false ? DataManager.Instance.m_dataTableShipPreset.GetShipPreset(currentPresetId) : null;

        m_shipPresetPicker.Open(ComputeUnlockedPresets(), currentPreset, selectedPresetId => ApplyPresetToSlot(index, selectedPresetId));
    }

    // 확인 버튼으로 선택된 프리셋을 해당 슬롯에 배치 — 기존 드래그앤드롭 배치 핵심 로직과 동일, dropIndex 대신 slotIndex 사용
    private void ApplyPresetToSlot(int slotIndex, string presetId)
    {
        if (string.IsNullOrEmpty(presetId) == true) return;

        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition == null) return;

        // 전/후방은 함선이 아니라 슬롯(인덱스)에 종속된 값 — 기존에 함선이 있던 슬롯을 교체할 때는 그 슬롯의 기존 전/후방을 유지,
        // 비어있던 슬롯에 처음 배치할 때만 기본값(전방)을 사용
        List<FleetSlotEntry> placedShipsBeforePlace = composition.GetPlacedShips();
        bool slotIsFront = slotIndex < placedShipsBeforePlace.Count ? placedShipsBeforePlace[slotIndex].isFront : true;

        EFleetPlaceResult result = composition.TryPlaceShipAt(slotIndex, presetId, slotIsFront);
        if (result != EFleetPlaceResult.Success)
        {
            string messageKey = result == EFleetPlaceResult.NotEnoughCommandPower
                ? "UIFleet_PlaceFailed_NotEnoughCommandPower"
                : "UIFleet_PlaceFailed_PresetNotFound";
            ShowPlaceFailedPopup(messageKey);
            return;
        }

        NetworkManager.Instance.PlaceFleetPresetShip(new FleetPresetPlaceShipRequest
        {
            slotIndex = slotIndex,
            shipPresetId = presetId,
            isFront = slotIsFront,
        });

        ObjectManager.Instance.ReplaceMyFleetShipAt(slotIndex, presetId, slotIsFront);
        RefreshFleetComposition();
    }

    private void ShowPlaceFailedPopup(string messageKey)
    {
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            message = LocalizationManager.Instance.Get(messageKey),
            onConfirm = () => { },
            autoCloseSec = 2.5f,
        });
    }
}
