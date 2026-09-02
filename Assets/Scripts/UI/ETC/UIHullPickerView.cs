// 함체 선택 팝업 — UIPanelFleet 프리팹에 내장된 오버레이(별도 UIManager 팝업 스택 대상 아님). 리스트에서 함체를
// 고르고 확인/취소로 결정만 알려줄 뿐, 실제로 어느 슬롯에 어떻게 적용할지는 모른다(호출부가 콜백에서 처리) — 재사용성을
// 위해 이 컴포넌트는 "선택기" 역할만 담당한다. 선택된 함체의 스탯을 현재 장착 함체와 비교해서 함께 보여준다
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIHullPickerView : MonoBehaviour
{
    [SerializeField] private InfiniteScrollView m_scrollView;
    [SerializeField] private UIAvailableHullRow m_rowPrefab;
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private Button m_cancelButton;

    [SerializeField] private UIStatRow m_statsRowPrefab; // 선택된 함체의 스탯 — Column_Stats와 동일한 구조/프리팹 재사용
    [SerializeField] private InfiniteScrollView m_statsScrollView;

    [SerializeField] private RowLabelValue m_commandPowerRow; // 선택 후보를 적용했을 때의 지휘력 미리보기 — UIPanelFleet 성능 컬럼의 지휘력 행과 동일한 구성

    [SerializeField] private RawImage m_previewImage; // 선택된 함체의 3D 바디 미리보기 — ShipPreviewManager가 렌더링한 텍스처

    private readonly List<ModuleData> m_hullsCache = new();
    private List<ShipStatRowEntry> m_statEntries = new();
    private Dictionary<string, ShipStatRowEntry> m_currentEntriesByLabel; // 비교 기준(현재 장착 함체) — 라벨로 조회

    private string m_selectedHullSubType;
    private ModuleHullInfo m_currentModules; // 이 슬롯에 실제로 장착돼 있던 모듈 — 함체 변경 시 슬롯 유지 계산의 기준
    private int m_currentSlotCommandCost; // 이 슬롯이 지금 실제로 쓰고 있는 지휘력 — 각 후보 함체 행의 증감(+/-) 표시 기준
    private int m_baseUsedCommandPower; // 이 슬롯이 점유 중이던 지휘력을 미리 뺀 값 — 후보 함체 비용만 더하면 미리보기 완성
    private int m_maxCommandPower;
    private System.Action<string> m_onConfirm; // 확인 시 선택된 hullSubType 전달
    private System.Action m_onCancel;

    private void Awake()
    {
        if (m_confirmButton != null)
            m_confirmButton.onClick.AddListener(OnConfirmClicked);
        if (m_cancelButton != null)
            m_cancelButton.onClick.AddListener(OnCancelClicked);
        if (m_scrollView != null)
            m_scrollView.onItemBind = OnItemBind;
        if (m_statsScrollView != null)
            m_statsScrollView.onItemBind = OnStatsItemBind;

        gameObject.SetActive(false);
    }

    // availableHulls: 고를 수 있는 함체 목록, currentHull: 비교 기준이 되는 현재 장착 함체(빈 슬롯이면 null → 비교 없이 수치만 표시)
    // currentModules: 그 슬롯에 실제로 장착된 모듈 구성(로드아웃) — null이면 currentHull의 기본 장착 구성으로 비교
    // currentSlotCommandCost: 이 슬롯이 지금 실제로 쓰고 있는 지휘력 — 리스트 각 행의 증감(+/-) 표시 기준
    // baseUsedCommandPower: 이 슬롯이 점유 중이던 몫을 이미 뺀 사용 지휘력(호출부가 계산해서 넘김)
    public void Open(List<ModuleData> availableHulls, ModuleData currentHull, ModuleHullInfo currentModules, int currentSlotCommandCost, int baseUsedCommandPower, int maxCommandPower, System.Action<string> onConfirm, System.Action onCancel = null)
    {
        m_hullsCache.Clear();
        m_hullsCache.AddRange(availableHulls);
        m_selectedHullSubType = currentHull != null ? currentHull.moduleSubType.ToString() : null;
        m_currentModules = currentModules;
        m_currentSlotCommandCost = currentSlotCommandCost;
        m_baseUsedCommandPower = baseUsedCommandPower;
        m_maxCommandPower = maxCommandPower;
        m_onConfirm = onConfirm;
        m_onCancel = onCancel;

        m_currentEntriesByLabel = null;
        if (currentHull != null)
        {
            m_currentEntriesByLabel = new Dictionary<string, ShipStatRowEntry>();
            List<ShipStatRowEntry> currentEntries = ShipStatGaugeBuilder.Build(currentHull, currentModules);
            for (int i = 0; i < currentEntries.Count; i++)
                m_currentEntriesByLabel[currentEntries[i].label] = currentEntries[i];
        }

        // Initialize는 viewport 크기를 읽으므로 반드시 활성화 다음에 호출 — 비활성 상태로 호출하면 NRE 위험
        gameObject.SetActive(true);
        m_scrollView.Initialize(m_hullsCache.Count, m_rowPrefab.gameObject);
        RefreshStatsDisplay();
        RefreshCommandPowerPreview();
        RefreshPreview();
    }

    public void Close()
    {
        gameObject.SetActive(false);
        m_onConfirm = null;
        m_onCancel = null;
        ShipPreviewManager.Instance.Clear();
    }

    private void RefreshPreview()
    {
        if (m_previewImage == null) return;

        Rect previewRect = m_previewImage.rectTransform.rect;
        float aspect = previewRect.height > 0f ? previewRect.width / previewRect.height : 1f;

        m_previewImage.texture = ShipPreviewManager.Instance.GetPreviewTexture(aspect);
        ModuleData selectedHull = m_hullsCache.Find(p => p.moduleSubType.ToString() == m_selectedHullSubType);
        ShipPreviewManager.Instance.ShowHull(selectedHull);
    }

    private void OnItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_hullsCache.Count) return;

        UIAvailableHullRow row = rowObject.GetComponent<UIAvailableHullRow>();
        if (row == null) return;

        ModuleData hull = m_hullsCache[dataIndex];
        string hullSubType = hull.moduleSubType.ToString();
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        ModuleHullInfo keptModulesForRow = GetKeptModules(hullSubType);
        int projectedCost = composition != null ? composition.ComputeProjectedSlotCommandCost(hullSubType, keptModulesForRow) : hull.statPoint;
        int deltaCost = projectedCost - m_currentSlotCommandCost;

        row.Setup(hull, deltaCost, OnHullClicked);
        row.SetSelectedAvailableHullRow(hullSubType == m_selectedHullSubType);

        LayoutRebuilder.ForceRebuildLayoutImmediate(rowObject.transform as RectTransform);
    }

    private void OnHullClicked(ModuleData hull)
    {
        m_selectedHullSubType = hull.moduleSubType.ToString();
        m_scrollView.RefreshVisible(); // 재바인드되며 OnItemBind가 다시 불려 하이라이트가 새 선택으로 갱신됨
        RefreshStatsDisplay();
        RefreshCommandPowerPreview();
        RefreshPreview();
    }

    // 선택 후보를 실제로 적용했다고 가정했을 때의 지휘력 사용량을 미리 계산해서 보여줌 — 최대치 초과 시 경고색 + 확인 버튼 비활성화
    // 슬롯이 이미 점유 중이던 모듈(m_currentModules)은 새 함체의 슬롯 범위 안에서 그대로 유지되므로, 정적 statPoint가 아니라
    // 실제 유지될 모듈 구성(GetKeptModules) 기준으로 계산해야 정확함 — 서버 FleetService.placeFleetShip과 동일 규칙
    private void RefreshCommandPowerPreview()
    {
        if (m_commandPowerRow == null) return;

        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        ModuleHullInfo keptModules = GetKeptModules(m_selectedHullSubType);
        int selectedCost = composition != null ? composition.ComputeProjectedSlotCommandCost(m_selectedHullSubType, keptModules) : 0;
        int projectedUsedCommandPower = m_baseUsedCommandPower + selectedCost;
        bool isOverCommandPower = projectedUsedCommandPower > m_maxCommandPower;

        m_commandPowerRow.SetRow("UITabCommander_CommandPower", $"{projectedUsedCommandPower} / {m_maxCommandPower}", rawValue: true);
        m_commandPowerRow.SetValueColor(CommonUtility.PaletteColor(isOverCommandPower == true ? "Text.Warning" : "Text.Dark1"));
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_commandPowerRow.transform as RectTransform);

        bool hasAnyAttackModule = HasAnyAttackModule(keptModules);
        if (m_confirmButton != null)
            m_confirmButton.interactable = isOverCommandPower == false && hasAnyAttackModule == true;
    }

    // m_currentModules를 targetHullSubType의 슬롯 범위로 필터링한 결과 — 리스트 각 행의 비용 미리보기와 선택된 함체의 미리보기/Confirm에 공용으로 사용
    // m_currentModules가 null이면(원래 비어있던 슬롯) null을 그대로 반환해 기본 로드아웃 시딩 분기를 그대로 타게 함
    private ModuleHullInfo GetKeptModules(string targetHullSubType)
    {
        return FleetComposition.FilterModulesForNewHull(m_currentModules, targetHullSubType);
    }

    // 유지된 모듈이 하나도 없던 원래 빈 슬롯(null)이면 기본 로드아웃(빔slot0)이 시딩되므로 항상 공격모듈이 있는 것으로 취급
    private bool HasAnyAttackModule(ModuleHullInfo modules)
    {
        if (modules == null) return true;
        bool hasBeam = modules.beams != null && modules.beams.Count > 0;
        bool hasMissile = modules.missiles != null && modules.missiles.Count > 0;
        bool hasHangar = modules.hangars != null && modules.hangars.Count > 0;
        return hasBeam || hasMissile || hasHangar;
    }

    // 선택된 함체의 스탯을 현재 장착 함체와 비교해서 표시 — InfiniteScrollView가 화면에 보이는 행만 OnStatsItemBind로 바인딩하므로
    // 여기서는 m_statEntries만 갱신하고 Initialize로 스크롤뷰에 개수만 알려줌
    private void RefreshStatsDisplay()
    {
        ModuleData selectedHull = m_hullsCache.Find(p => p.moduleSubType.ToString() == m_selectedHullSubType);
        if (selectedHull == null)
        {
            m_statEntries.Clear();
            if (m_statsScrollView != null && m_statsRowPrefab != null)
                m_statsScrollView.Initialize(0, m_statsRowPrefab.gameObject);
            return;
        }

        ModuleHullInfo keptModules = GetKeptModules(m_selectedHullSubType);
        List<ShipStatRowEntry> entries = ShipStatGaugeBuilder.Build(selectedHull, keptModules);
        AppendRemovedStatEntries(entries);
        m_statEntries = entries;

        if (m_statsScrollView != null && m_statsRowPrefab != null)
            m_statsScrollView.Initialize(m_statEntries.Count, m_statsRowPrefab.gameObject);
    }

    // InfiniteScrollView가 dataIndex번 스탯 행을 화면에 배치할 때마다 호출 — 캐시된 m_statEntries로 바인딩
    private void OnStatsItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_statEntries.Count) return;

        UIStatRow row = rowObject.GetComponent<UIStatRow>();
        if (row == null) return;

        ShipStatRowEntry entry = m_statEntries[dataIndex];
        string diffText = BuildDiffText(entry);

        if (entry.isNumericValue == true)
            row.SetStatRow(entry.label, entry.value, diffText);
        else
            row.SetValueOnly(entry.label, entry.rawValueText, diffText);
    }

    // 현재 장착 함체엔 있었지만 선택한 함체에는 없어진 스탯(예: 미사일 미장착으로 변경)도 0값 항목으로 추가해
    // 감소(-) diff를 보여준다 — Normal 모드(게이지형 DPS 등)만 대상. None/Reverse는 0으로 대체할 의미있는 표시값이 없어 제외
    private void AppendRemovedStatEntries(List<ShipStatRowEntry> entries)
    {
        if (m_currentEntriesByLabel == null) return;

        HashSet<string> selectedLabels = new();
        for (int i = 0; i < entries.Count; i++)
            selectedLabels.Add(entries[i].label);

        foreach (KeyValuePair<string, ShipStatRowEntry> pair in m_currentEntriesByLabel)
        {
            if (selectedLabels.Contains(pair.Key) == true) continue;
            if (pair.Value.isNumericValue == false) continue;

            ShipStatRowEntry removedEntry = pair.Value;
            removedEntry.value = 0f;
            removedEntry.rawValueText = "0.0";
            removedEntry.compareValue = 0f;
            entries.Add(removedEntry);
        }
    }

    // 라벨로 현재 장착 함체의 동일 스탯을 찾아 증감을 리치텍스트로 포맷.
    // 현재 함체에 아예 없던 스탯(예: 미사일 미장착 → 장착)은 기준값 0으로 취급해 신규 획득으로 표시
    private string BuildDiffText(ShipStatRowEntry entry)
    {
        return ShipStatGaugeBuilder.BuildDiffText(entry, m_currentEntriesByLabel);
    }

    private void OnConfirmClicked()
    {
        System.Action<string> onConfirm = m_onConfirm;
        string selected = m_selectedHullSubType;
        Close();
        if (onConfirm != null) onConfirm(selected);
    }

    private void OnCancelClicked()
    {
        System.Action onCancel = m_onCancel;
        Close();
        if (onCancel != null) onCancel();
    }
}
