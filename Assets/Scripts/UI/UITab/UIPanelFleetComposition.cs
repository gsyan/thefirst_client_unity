// 함대편성 UI — FleetComposition 기반, UIManager가 관리하는 독립 패널(다른 진입 화면과 배타적)
// 3단계: 배치가능 프리셋 클릭(스탯 팝업)/드래그(배치), 배치된 함선 전방/후방 토글 + 클릭 시 성능 컬럼에 상세 스탯 표시
// 상단 FleetStats(항상 보이는 지휘력/배치 수 요약)와 3열(함대구성/성능/배치가능 함선)은 역할이 분리됨 —
// 성능 컬럼은 순수하게 "선택한 함선의 상세 스탯"만 담당(선택 없으면 비어있음)
// 행은 프리팹에 미리 배치하지 않고, 필요한 개수만큼 풀에서 동적으로 늘려가며 사용(부족하면 Instantiate, 남으면 비활성화)
// 이 패널이 열리면 화면 좌측 3D 카메라 viewport를 축소해 우측에 자리를 만듦(카메라 애니메이션은 CameraController가 담당,
// 여기서는 열고 닫을 시점과 목표 폭만 결정) — 애니메이션이 끝나기 전엔 내용을 CanvasGroup으로 가려둠(구 TabSystem deferReveal 대체)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIPanelFleetComposition : UIPanelBase
{
    [SerializeField] private RowLabelValue m_fleetStatsRowPrefab; // 상단 FleetStats 전용(지휘력/배치 함선 수) — 항상 텍스트로 표시
    [SerializeField] private UIStatGaugeRow m_statsRowPrefab;     // 성능 컬럼 전용 — 선택한 함선의 상세 스탯
    [SerializeField] private UIPlacedShipRow m_placedShipRowPrefab;
    [SerializeField] private UIAvailablePresetRow m_availablePresetRowPrefab;

    [SerializeField] private RectTransform m_columnContainer; // 3열을 감싸는 최상위 컨테이너 — Horizontal Layout Group. 각 열 안에도 Title+행 컨테이너를 감싸는 Vertical Layout Group이 있어, 리빌드는 이 최상위에서 한 번만 해도 하위가 전부 재계산됨
    [SerializeField] private RectTransform m_fleetStatsContainer; // 상단 요약 영역(FleetStats/Container)
    [SerializeField] private RectTransform m_placedShipsContainer;
    [SerializeField] private RectTransform m_statsContainer;
    [SerializeField] private RectTransform m_availablePresetsContainer;
    [SerializeField] private InfiniteScrollView m_availablePresetsScrollView; // 배치가능 프리셋 — 세로 가상 스크롤(항목 수가 많아 풀링 대신 InfiniteScrollView 사용)
    [SerializeField] private Button m_increaseCommandPowerButton; // 탐험 포인트 100 소모 -> 지휘력 최대치 10 증가(교환비는 ExplorationService 서버값과 항상 함께 수정)
    [SerializeField] private RowLabelValue m_ownedExplorationPointRow; // 보유 탐험 포인트 — 지휘력 증가 버튼이 바로 이 값을 소모하므로 버튼 옆에 전용으로 배치

    [Header("카메라 Viewport 애니메이션")]
    [SerializeField] private float m_animDuration = 0.3f;

    private readonly List<RowLabelValue> m_fleetStatsRows = new();
    private readonly List<UIPlacedShipRow> m_placedShipRows = new();
    private readonly List<UIStatGaugeRow> m_statsRows = new();
    private readonly List<ShipPresetData> m_unlockedPresetsCache = new(); // InfiniteScrollView.onItemBind에서 index로 조회할 용도

    private long m_displayedOwnedExplorationPoint = -1; // 롤링 애니메이션의 시작값 추적 — 최초 1회는 -1이라 즉시 표시됨(UIResourceBar와 동일 패턴)

    private int m_selectedSlotIndex = -1; // 선택된 배치 슬롯 — -1이면 성능 컬럼은 비어있음. 프리셋 ID는 저장하지 않고 매번 슬롯에서 다시 조회(드래그로 함선이 교체돼도 항상 최신 반영)
    private int m_highlightedSlotIndex = -1; // 드래그 중 호버된 배치 슬롯 — 없으면 -1

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
        if (m_placedShipsContainer != null)
            m_placedShipRows.AddRange(m_placedShipsContainer.GetComponentsInChildren<UIPlacedShipRow>(true));
        if (m_statsContainer != null)
            m_statsRows.AddRange(m_statsContainer.GetComponentsInChildren<UIStatGaugeRow>(true));
        if (m_availablePresetsScrollView != null)
            m_availablePresetsScrollView.onItemBind = OnAvailablePresetItemBind;

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
        // 카메라 viewport 축소 애니메이션이 끝나기 전엔 내용을 가려둠 — RevealAfterViewportAnimation()에서 복원
        m_canvasGroup.alpha = 0f;
        m_canvasGroup.blocksRaycasts = false;

        // 이 패널이 열려있는 동안엔 3D에서 다른 내 함선을 클릭해도 그 함선으로 선택이 갱신되어야 함 —
        // UIPanelSpace는 자기가 하이드된 동안 이 이벤트 구독을 끊으므로, 여기서 이 패널이 열려있는 동안만 직접 구독
        EventManager.Subscribe_SpaceShipSelected(OnShipSelectedWhileOpen);

        RefreshFleetComposition();
        StartViewportAnimation(open: true);
    }

    public override void OnHideUIPanel()
    {
        EventManager.Unsubscribe_SpaceShipSelected(OnShipSelectedWhileOpen);
        StartViewportAnimation(open: false);
    }

    private void OnShipSelectedWhileOpen(SpaceShip ship)
    {
        SelectPlacedShipByPositionIndex(ship.m_shipInfo.positionIndex);
    }

    // 애니메이션 종료 직후 호출 — 내용 노출 + 애니메이션 도중 바뀐 사이즈 기준으로 레이아웃 재계산
    private void RevealAfterViewportAnimation()
    {
        m_canvasGroup.alpha = 1f;
        m_canvasGroup.blocksRaycasts = true;
        RefreshFleetComposition();
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
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition == null) return;

        RefreshFleetStatsSummary(composition);
        RefreshPlacedShips(composition);
        RefreshStats();
        RefreshAvailablePresets();

        RebuildAllLayouts();
    }

    // 함선 배치/선택 등으로 컬럼 내용이 바뀔 때마다 관련 컨테이너 전체를 한 번에 리빌드
    private void RebuildAllLayouts()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_fleetStatsContainer);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_placedShipsContainer);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_statsContainer);
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_availablePresetsContainer);
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

    // 확인 팝업 없이 즉시 소모 — 탐험 포인트 100 -> 지휘력 최대치 10(서버 ExplorationService 교환비와 동일)
    private void OnIncreaseCommandPowerButtonClicked()
    {
        NetworkManager.Instance.IncreaseCommandPowerMax(new IncreaseCommandPowerMaxRequest(), response =>
        {
            if (response.errorCode != 0)
            {
                Debug.LogError($"[UIPanelFleetComposition] IncreaseCommandPowerMax 실패: {response.errorCode}");
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

    private void RefreshPlacedShips(FleetComposition composition)
    {
        List<FleetSlotEntry> placedShips = composition.GetPlacedShips();

        // 지휘관 레벨에 따라 배치 가능한 함선 수(=열려있는 슬롯 수)가 달라짐 — 커맨더 탭의 "최대 함선 수"와 동일한 값 재사용
        Commander commander = DataManager.Instance.m_currentCommander;
        int commanderLevel = commander != null ? commander.GetCommanderLevel() : 0;
        int openSlotCount = DataManager.Instance.m_dataTableCommander.GetShipCount(commanderLevel);

        // 디자인상 최대 슬롯 수(잠긴 칸 포함)까지는 항상 노출 — 레벨업 동기부여를 위해 잠긴 칸도 미리 보여줌
        int maxSlotCount = DataManager.Instance.m_dataTableCommander.GetMaxShipCount();
        int slotCount = Mathf.Max(maxSlotCount, placedShips.Count); // 배치 수가 어떤 이유로 슬롯 수를 넘어도(레벨 하락 등) 표시는 유지

        EnsureRowCount(m_placedShipRows, m_placedShipsContainer, m_placedShipRowPrefab, slotCount);

        for (int i = 0; i < m_placedShipRows.Count; i++)
        {
            if (i >= slotCount)
            {
                m_placedShipRows[i].Hide();
                continue;
            }

            if (i < placedShips.Count)
            {
                FleetSlotEntry entry = placedShips[i];
                m_placedShipRows[i].Setup(i, entry.shipPresetId, entry.isFront, OnShipFrontToggled, OnPlacedShipRowClicked);
                m_placedShipRows[i].SetSelected(i == m_selectedSlotIndex);
            }
            else if (i < openSlotCount)
            {
                m_placedShipRows[i].SetEmpty(i);
            }
            else
            {
                m_placedShipRows[i].SetLocked(i);
            }
        }
    }

    // 성능 컬럼 — 선택된 함선이 없으면 비어있음, 있으면 그 함선의 상세 스탯(팝업과 동일한 항목 구성)
    // 선택은 슬롯 인덱스로만 추적하고 프리셋은 매번 여기서 다시 조회 — 드래그로 그 슬롯의 함선이 교체돼도 최신 프리셋을 보여줌
    private void RefreshStats()
    {
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        List<FleetSlotEntry> placedShips = composition != null ? composition.GetPlacedShips() : null;

        bool hasSelection = m_selectedSlotIndex >= 0 && placedShips != null && m_selectedSlotIndex < placedShips.Count;
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

    private void RefreshAvailablePresets()
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        int commanderLevel = commander != null ? commander.GetCommanderLevel() : 0;
        List<ShipPresetData> allPresets = DataManager.Instance.m_dataTableShipPreset.GetShipPresetDataList();

        m_unlockedPresetsCache.Clear();
        for (int i = 0; i < allPresets.Count; i++)
        {
            if (allPresets[i].unlockCommanderLevel <= commanderLevel)
                m_unlockedPresetsCache.Add(allPresets[i]);
        }

        if (m_availablePresetsScrollView != null && m_availablePresetRowPrefab != null)
            m_availablePresetsScrollView.Initialize(m_unlockedPresetsCache.Count, m_availablePresetRowPrefab.gameObject);
    }

    // InfiniteScrollView가 dataIndex번 행을 화면에 배치할 때마다 호출 — m_unlockedPresetsCache[dataIndex]로 실제 데이터 바인딩
    private void OnAvailablePresetItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_unlockedPresetsCache.Count) return;

        UIAvailablePresetRow row = rowObject.GetComponent<UIAvailablePresetRow>();
        if (row == null) return;

        row.Setup(m_unlockedPresetsCache[dataIndex], OnAvailablePresetClicked, OnAvailablePresetDropped, OnAvailablePresetDragging);
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
        RefreshFleetComposition();
    }

    // ── 배치된 함선 — 행 클릭(성능 컬럼에 상세 스탯 표시) ───────────────
    private void OnPlacedShipRowClicked(int index, string shipPresetId)
    {
        m_selectedSlotIndex = index;
        for (int i = 0; i < m_placedShipRows.Count; i++)
            m_placedShipRows[i].SetSelected(i == m_selectedSlotIndex);
        RefreshStats();
        RebuildAllLayouts();
    }

    // 3D 함선 클릭 등 외부 진입점에서 특정 함선을 선택 상태로 표시 — positionIndex는 SpaceShip.m_shipInfo.positionIndex와 동일 기준(슬롯 순서)
    public void SelectPlacedShipByPositionIndex(int positionIndex)
    {
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition == null) return;

        List<FleetSlotEntry> placedShips = composition.GetPlacedShips();
        if (positionIndex < 0 || positionIndex >= placedShips.Count) return;

        OnPlacedShipRowClicked(positionIndex, placedShips[positionIndex].shipPresetId);
    }

    // ── 배치가능 프리셋 — 클릭(스탯 팝업) ─────────────────────────────
    private void OnAvailablePresetClicked(ShipPresetData preset)
    {
        UIManager.Instance.ShowShipStatsPopup(preset);
    }

    // ── 배치가능 프리셋 — 드래그 중 호버된 슬롯 하이라이트 ───────────────
    private void OnAvailablePresetDragging(PointerEventData eventData)
    {
        int hoverIndex = ComputeDropIndexInPlacedShips(eventData.position);
        if (hoverIndex == m_highlightedSlotIndex) return;

        ClearSlotHighlight();

        if (hoverIndex >= 0 && hoverIndex < m_placedShipRows.Count && m_placedShipRows[hoverIndex].IsLocked() == false)
        {
            m_placedShipRows[hoverIndex].SetHighlighted(true);
            m_highlightedSlotIndex = hoverIndex;
        }
    }

    private void ClearSlotHighlight()
    {
        if (m_highlightedSlotIndex >= 0 && m_highlightedSlotIndex < m_placedShipRows.Count)
            m_placedShipRows[m_highlightedSlotIndex].SetHighlighted(false);
        m_highlightedSlotIndex = -1;
    }

    // ── 배치가능 프리셋 — 드래그로 배치 ───────────────────────────────
    private void OnAvailablePresetDropped(ShipPresetData preset, PointerEventData eventData)
    {
        ClearSlotHighlight();

        int dropIndex = ComputeDropIndexInPlacedShips(eventData.position);
        if (dropIndex < 0) return; // 함대구성 컬럼 밖에 놓음 — 단순 취소, 팝업 없음

        if (dropIndex < m_placedShipRows.Count && m_placedShipRows[dropIndex].IsLocked() == true)
        {
            ShowPlaceFailedPopup("UIFleet_PlaceFailed_Locked");
            return;
        }

        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition == null) return;

        // 전/후방은 함선이 아니라 슬롯(인덱스)에 종속된 값 — 기존에 함선이 있던 슬롯을 교체할 때는 그 슬롯의 기존 전/후방을 유지,
        // 비어있던 슬롯에 처음 놓을 때만 기본값(전방)을 사용
        List<FleetSlotEntry> placedShipsBeforePlace = composition.GetPlacedShips();
        bool slotIsFront = dropIndex < placedShipsBeforePlace.Count ? placedShipsBeforePlace[dropIndex].isFront : true;

        EFleetPlaceResult result = composition.TryPlaceShipAt(dropIndex, preset.presetId, slotIsFront);
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
            slotIndex = dropIndex,
            shipPresetId = preset.presetId,
            isFront = slotIsFront,
        });

        ObjectManager.Instance.ReplaceMyFleetShipAt(dropIndex, preset.presetId, slotIsFront);
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

    // 드롭 지점 아래 UIPlacedShipRow가 있으면 그 행의 위치에 삽입, 그 외엔 컬럼 영역 안인지 사각형으로 판정해 끝에 추가, 컬럼 밖이면 -1
    private int ComputeDropIndexInPlacedShips(Vector2 screenPosition)
    {
        if (m_placedShipsContainer == null) return -1;

        if (EventSystem.current != null)
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
            List<RaycastResult> results = new();
            EventSystem.current.RaycastAll(pointerData, results);

            for (int i = 0; i < results.Count; i++)
            {
                UIPlacedShipRow row = results[i].gameObject.GetComponentInParent<UIPlacedShipRow>();
                if (row != null)
                    return row.transform.GetSiblingIndex();
            }
        }

        // 배치된 함선이 아직 없어 컨테이너에 레이캐스트 가능한 Graphic이 전혀 없는 경우(빈 컬럼) 대비 —
        // 화면 사각형 판정으로 폴백. Screen Space - Overlay 캔버스 기준(카메라 null)
        bool insideColumn = RectTransformUtility.RectangleContainsScreenPoint(m_placedShipsContainer, screenPosition, null);
        if (insideColumn == false) return -1;

        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        return composition != null ? composition.GetPlacedShips().Count : 0;
    }
}
