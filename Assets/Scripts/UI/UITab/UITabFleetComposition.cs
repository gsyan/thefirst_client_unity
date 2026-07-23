// 함대편성 UI — FleetComposition 기반, 3개 진입점(함대 탭 버튼/함선 피킹/탐험 대치 함대설정)에서 공용으로 쓸 예정
// 3단계: 배치가능 프리셋 클릭(스탯 팝업)/드래그(배치), 배치된 함선 전방/후방 토글 + 클릭 시 성능 컬럼에 상세 스탯 표시
// 상단 FleetStats(항상 보이는 지휘력/배치 수 요약)와 3열(함대구성/성능/배치가능 함선)은 역할이 분리됨 —
// 성능 컬럼은 순수하게 "선택한 함선의 상세 스탯"만 담당(선택 없으면 비어있음)
// 행은 프리팹에 미리 배치하지 않고, 필요한 개수만큼 풀에서 동적으로 늘려가며 사용(부족하면 Instantiate, 남으면 비활성화)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UITabFleetComposition : UITabBase
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

    private readonly List<RowLabelValue> m_fleetStatsRows = new();
    private readonly List<UIPlacedShipRow> m_placedShipRows = new();
    private readonly List<UIStatGaugeRow> m_statsRows = new();
    private readonly List<UIAvailablePresetRow> m_availablePresetRows = new();

    private string m_selectedShipPresetId; // null이면 성능 컬럼은 비어있음, 값이 있으면 그 함선 상세 스탯 표시

    public override void InitializeUITab()
    {
        // 프리팹에 미리 배치해둔 행이 있으면 초기 풀로 흡수 — 없어도 무방(전부 동적 생성으로 채워짐)
        if (m_fleetStatsContainer != null)
            m_fleetStatsRows.AddRange(m_fleetStatsContainer.GetComponentsInChildren<RowLabelValue>(true));
        if (m_placedShipsContainer != null)
            m_placedShipRows.AddRange(m_placedShipsContainer.GetComponentsInChildren<UIPlacedShipRow>(true));
        if (m_statsContainer != null)
            m_statsRows.AddRange(m_statsContainer.GetComponentsInChildren<UIStatGaugeRow>(true));
        if (m_availablePresetsContainer != null)
            m_availablePresetRows.AddRange(m_availablePresetsContainer.GetComponentsInChildren<UIAvailablePresetRow>(true));

        EventManager.Subscribe_CommanderLevelChanged(OnCommanderLevelChanged);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_CommanderLevelChanged(OnCommanderLevelChanged);
    }

    public override void OnTabActivated()
    {
        base.OnTabActivated();
        HideTabButtons();
        RefreshFleetComposition();
    }

    public override void OnTabDeactivated()
    {
        base.OnTabDeactivated();
        RefreshTabButtons();
    }

    private void OnCommanderLevelChanged(int commanderLevel)
    {
        RefreshFleetComposition();
    }

    // deferReveal 때문에 OnTabActivated() 시점엔 패널이 아직 실제로 안 보이는(비활성) 상태일 수 있음 —
    // 그 상태에서 행을 만들고 레이아웃을 리빌드하면 사이즈 계산이 틀어짐(2번째 진입부터 정상으로 보이는 원인).
    // UIPanelSpace.Co_AnimateViewport()가 RevealDeferredPanel() 직후(패널이 실제로 보이는 시점) 이 메서드를 다시 호출해줌
    public void RefreshFleetComposition()
    {
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition == null) return;

        RefreshFleetStatsSummary(composition);
        RefreshPlacedShips(composition);
        RefreshStats();
        RefreshAvailablePresets();

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
            m_fleetStatsRows[1].SetRow("UITabFleetComposition_PlacedShipCount", $"{placedShipCount}", rawValue: true);

        for (int i = k_summaryRowCount; i < m_fleetStatsRows.Count; i++)
            m_fleetStatsRows[i].Hide();
    }

    private void RefreshPlacedShips(FleetComposition composition)
    {
        List<FleetSlotEntry> placedShips = composition.GetPlacedShips();
        EnsureRowCount(m_placedShipRows, m_placedShipsContainer, m_placedShipRowPrefab, placedShips.Count);

        for (int i = 0; i < m_placedShipRows.Count; i++)
        {
            if (i >= placedShips.Count)
            {
                m_placedShipRows[i].Hide();
                continue;
            }

            FleetSlotEntry entry = placedShips[i];
            m_placedShipRows[i].Setup(i, entry.shipPresetId, entry.isFront, OnShipFrontToggled, OnPlacedShipRowClicked);
        }
    }

    // 성능 컬럼 — 선택된 함선이 없으면 비어있음, 있으면 그 함선의 상세 스탯(팝업과 동일한 항목 구성)
    private void RefreshStats()
    {
        if (string.IsNullOrEmpty(m_selectedShipPresetId) == true)
        {
            for (int i = 0; i < m_statsRows.Count; i++)
                m_statsRows[i].Hide();
            return;
        }

        ShipPresetData selectedPreset = DataManager.Instance.m_dataTableShipPreset.GetShipPreset(m_selectedShipPresetId);
        if (selectedPreset == null)
        {
            m_selectedShipPresetId = null; // 프리셋을 못 찾으면(배치 해제 등) 선택 해제
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
            if (entry.gaugeMax > 0f)
                m_statsRows[i].SetGauge(entry.label, entry.value, entry.gaugeMax);
            else
                m_statsRows[i].SetValueOnly(entry.label, entry.rawValueText);
        }
    }

    private void RefreshAvailablePresets()
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        int commanderLevel = commander != null ? commander.GetCommanderLevel() : 0;
        List<ShipPresetData> allPresets = DataManager.Instance.m_dataTableShipPreset.GetShipPresetDataList();

        int unlockedCount = 0;
        for (int i = 0; i < allPresets.Count; i++)
        {
            if (allPresets[i].unlockCommanderLevel <= commanderLevel)
                unlockedCount++;
        }
        EnsureRowCount(m_availablePresetRows, m_availablePresetsContainer, m_availablePresetRowPrefab, unlockedCount);

        int rowIndex = 0;
        for (int i = 0; i < allPresets.Count; i++)
        {
            ShipPresetData preset = allPresets[i];
            if (preset.unlockCommanderLevel > commanderLevel) continue;

            m_availablePresetRows[rowIndex].Setup(preset, OnAvailablePresetClicked, OnAvailablePresetDropped);
            rowIndex++;
        }

        for (; rowIndex < m_availablePresetRows.Count; rowIndex++)
            m_availablePresetRows[rowIndex].Hide();
    }

    // ── 배치된 함선 — 전방/후방 토글 ──────────────────────────────────
    private void OnShipFrontToggled(int index, bool isFront)
    {
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition == null) return;

        composition.SetFront(index, isFront);
        ObjectManager.Instance.RebuildMyFleetFromComposition(composition);
        RefreshFleetComposition();
    }

    // ── 배치된 함선 — 행 클릭(성능 컬럼에 상세 스탯 표시) ───────────────
    private void OnPlacedShipRowClicked(int index, string shipPresetId)
    {
        m_selectedShipPresetId = shipPresetId;
        RefreshStats();
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_columnContainer);
    }

    // ── 배치가능 프리셋 — 클릭(스탯 팝업) ─────────────────────────────
    private void OnAvailablePresetClicked(ShipPresetData preset)
    {
        UIManager.Instance.ShowShipStatsPopup(preset);
    }

    // ── 배치가능 프리셋 — 드래그로 배치 ───────────────────────────────
    private void OnAvailablePresetDropped(ShipPresetData preset, PointerEventData eventData)
    {
        int dropIndex = ComputeDropIndexInPlacedShips(eventData.position);
        if (dropIndex < 0) return; // 함대구성 컬럼 밖에 놓음 — 취소

        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition == null) return;

        EFleetPlaceResult result = composition.TryPlaceShipAt(dropIndex, preset.presetId, isFront: false);
        if (result != EFleetPlaceResult.Success) return; // TODO: 실패 피드백(지휘력 부족 등)은 후속 작업

        ObjectManager.Instance.RebuildMyFleetFromComposition(composition);
        RefreshFleetComposition();
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
