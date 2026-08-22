// 함선 프리셋 교체 팝업 — UIPanelFleet 프리팹에 내장된 오버레이(별도 UIManager 팝업 스택 대상 아님). 리스트에서 프리셋을
// 고르고 확인/취소로 결정만 알려줄 뿐, 실제로 어느 슬롯에 어떻게 적용할지는 모른다(호출부가 콜백에서 처리) — 재사용성을
// 위해 이 컴포넌트는 "선택기" 역할만 담당한다. 선택된 프리셋의 스탯을 현재 장착 프리셋과 비교해서 함께 보여준다
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIShipPresetPickerView : MonoBehaviour
{
    [SerializeField] private InfiniteScrollView m_scrollView;
    [SerializeField] private UIAvailablePresetRow m_rowPrefab;
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private Button m_cancelButton;

    [SerializeField] private UIStatRow m_statsRowPrefab; // 선택된 프리셋의 스탯 — Column_Stats와 동일한 구조/프리팹 재사용
    [SerializeField] private RectTransform m_statsContainer;

    [SerializeField] private RowLabelValue m_commandPowerRow; // 선택 후보를 적용했을 때의 지휘력 미리보기 — UIPanelFleet 성능 컬럼의 지휘력 행과 동일한 구성

    private readonly List<ShipPresetData> m_presetsCache = new();
    private readonly List<UIStatRow> m_statsRows = new();
    private Dictionary<string, ShipStatRowEntry> m_currentEntriesByLabel; // 비교 기준(현재 장착 프리셋) — 라벨로 조회

    private string m_selectedPresetId;
    private int m_baseUsedCommandPower; // 이 슬롯이 점유 중이던 지휘력을 미리 뺀 값 — 후보 프리셋 비용만 더하면 미리보기 완성
    private int m_maxCommandPower;
    private System.Action<string> m_onConfirm; // 확인 시 선택된 presetId 전달
    private System.Action m_onCancel;

    private void Awake()
    {
        if (m_confirmButton != null)
            m_confirmButton.onClick.AddListener(OnConfirmClicked);
        if (m_cancelButton != null)
            m_cancelButton.onClick.AddListener(OnCancelClicked);
        if (m_scrollView != null)
            m_scrollView.onItemBind = OnItemBind;

        gameObject.SetActive(false);
    }

    // availablePresets: 고를 수 있는 프리셋 목록, currentPreset: 비교 기준이 되는 현재 장착 프리셋(빈 슬롯이면 null → 비교 없이 수치만 표시)
    // currentModules: 그 슬롯에 실제로 장착된 모듈 구성(로드아웃) — null이면 currentPreset의 기본 장착 구성으로 비교
    // baseUsedCommandPower: 이 슬롯이 점유 중이던 몫을 이미 뺀 사용 지휘력(호출부가 계산해서 넘김)
    public void Open(List<ShipPresetData> availablePresets, ShipPresetData currentPreset, ModuleBodyInfo currentModules, int baseUsedCommandPower, int maxCommandPower, System.Action<string> onConfirm, System.Action onCancel = null)
    {
        m_presetsCache.Clear();
        m_presetsCache.AddRange(availablePresets);
        m_selectedPresetId = currentPreset != null ? currentPreset.presetId : null;
        m_baseUsedCommandPower = baseUsedCommandPower;
        m_maxCommandPower = maxCommandPower;
        m_onConfirm = onConfirm;
        m_onCancel = onCancel;

        m_currentEntriesByLabel = null;
        if (currentPreset != null)
        {
            m_currentEntriesByLabel = new Dictionary<string, ShipStatRowEntry>();
            List<ShipStatRowEntry> currentEntries = ShipStatGaugeBuilder.Build(currentPreset, currentModules);
            for (int i = 0; i < currentEntries.Count; i++)
                m_currentEntriesByLabel[currentEntries[i].label] = currentEntries[i];
        }

        // Initialize는 viewport 크기를 읽으므로 반드시 활성화 다음에 호출 — 비활성 상태로 호출하면 NRE 위험
        gameObject.SetActive(true);
        m_scrollView.Initialize(m_presetsCache.Count, m_rowPrefab.gameObject);
        RefreshStatsDisplay();
        RefreshCommandPowerPreview();
    }

    public void Close()
    {
        gameObject.SetActive(false);
        m_onConfirm = null;
        m_onCancel = null;
    }

    private void OnItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_presetsCache.Count) return;

        UIAvailablePresetRow row = rowObject.GetComponent<UIAvailablePresetRow>();
        if (row == null) return;

        ShipPresetData preset = m_presetsCache[dataIndex];
        row.Setup(preset, OnPresetClicked);
        row.SetSelectedAvailablePresetRow(preset.presetId == m_selectedPresetId);

        LayoutRebuilder.ForceRebuildLayoutImmediate(rowObject.transform as RectTransform);
    }

    private void OnPresetClicked(ShipPresetData preset)
    {
        m_selectedPresetId = preset.presetId;
        m_scrollView.RefreshVisible(); // 재바인드되며 OnItemBind가 다시 불려 하이라이트가 새 선택으로 갱신됨
        RefreshStatsDisplay();
        RefreshCommandPowerPreview();
    }

    // 선택 후보를 실제로 적용했다고 가정했을 때의 지휘력 사용량을 미리 계산해서 보여줌 — 최대치 초과 시 경고색 + 확인 버튼 비활성화
    private void RefreshCommandPowerPreview()
    {
        if (m_commandPowerRow == null) return;

        ShipPresetData selectedPreset = m_presetsCache.Find(p => p.presetId == m_selectedPresetId);
        int selectedCost = selectedPreset != null ? selectedPreset.commandCost : 0;
        int projectedUsedCommandPower = m_baseUsedCommandPower + selectedCost;
        bool isOverCommandPower = projectedUsedCommandPower > m_maxCommandPower;

        m_commandPowerRow.SetRow("UITabCommander_CommandPower", $"{projectedUsedCommandPower} / {m_maxCommandPower}", rawValue: true);
        m_commandPowerRow.SetValueColor(CommonUtility.PaletteColor(isOverCommandPower == true ? "Text.Warning" : "Text.Dark1"));

        if (m_confirmButton != null)
            m_confirmButton.interactable = isOverCommandPower == false;
    }

    // 선택된 프리셋의 스탯을 현재 장착 프리셋과 비교해서 표시 — Column_Stats(UIPanelFleet)와 동일한 풀링 패턴
    private void RefreshStatsDisplay()
    {
        ShipPresetData selectedPreset = m_presetsCache.Find(p => p.presetId == m_selectedPresetId);
        if (selectedPreset == null)
        {
            for (int i = 0; i < m_statsRows.Count; i++)
                m_statsRows[i].Hide();

            if (m_statsContainer != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(m_statsContainer);
            return;
        }

        List<ShipStatRowEntry> entries = ShipStatGaugeBuilder.Build(selectedPreset);
        AppendRemovedStatEntries(entries);
        EnsureStatsRowCount(entries.Count);

        for (int i = 0; i < m_statsRows.Count; i++)
        {
            if (i >= entries.Count)
            {
                m_statsRows[i].Hide();
                continue;
            }

            ShipStatRowEntry entry = entries[i];
            string diffText = BuildDiffText(entry);

            if (entry.isNumericValue == true)
                m_statsRows[i].SetStatRow(entry.label, entry.value, diffText);
            else
                m_statsRows[i].SetValueOnly(entry.label, entry.rawValueText, diffText);
        }

        if (m_statsContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_statsContainer);
    }

    // 현재 장착 프리셋엔 있었지만 선택한 프리셋에는 없어진 스탯(예: 미사일 미장착으로 변경)도 0값 항목으로 추가해
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

    // 라벨로 현재 장착 프리셋의 동일 스탯을 찾아 증감을 리치텍스트로 포맷.
    // 현재 프리셋에 아예 없던 스탯(예: 미사일 미장착 → 장착)은 기준값 0으로 취급해 신규 획득으로 표시
    private string BuildDiffText(ShipStatRowEntry entry)
    {
        return ShipStatGaugeBuilder.BuildDiffText(entry, m_currentEntriesByLabel);
    }

    private void EnsureStatsRowCount(int neededCount)
    {
        if (m_statsContainer == null || m_statsRowPrefab == null) return;

        while (m_statsRows.Count < neededCount)
            m_statsRows.Add(Instantiate(m_statsRowPrefab, m_statsContainer));
    }

    private void OnConfirmClicked()
    {
        System.Action<string> onConfirm = m_onConfirm;
        string selected = m_selectedPresetId;
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
