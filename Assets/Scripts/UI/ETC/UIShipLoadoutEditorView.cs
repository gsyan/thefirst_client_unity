// 함선 로드아웃 편집 화면 — UIShipPresetPickerView가 "바디(함체) 선택"을 담당한다면, 이 컴포넌트는 그 다음 단계인
// "슬롯별 모듈 on/off 편집"을 담당한다. 토글은 전부 로컬에서만 미리보기 상태로 바뀌고, Confirm을 눌러야 서버에 실제로 반영됨
// (UIShipPresetPickerView와 동일 패턴 — 예산 초과 상태에선 Confirm 버튼 비활성화)
// 카테고리별 최대 슬롯 수는 FleetComposition.ParseMaxSlotsFromPresetId로 presetId에서 파싱(빔/미사일/격납고/실드/요격체 — 현재 실드/요격체는 항상 0)
// 빔/미사일/격납고 슬롯을 하나의 InfiniteScrollView에 순서대로(빔 전부 → 미사일 전부 → 격납고 전부) 나열
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIShipLoadoutEditorView : MonoBehaviour
{
    [SerializeField] private InfiniteScrollView m_moduleScrollView;
    [SerializeField] private UIModuleSlotToggleRow m_rowPrefab; // 카테고리 공용 — 슬롯 1칸당 행 1개

    [SerializeField] private RowLabelValue m_commandPowerRow; // 상단 지휘력 요약(UIPanelFleet 성능 컬럼과 동일 구성)
    [SerializeField] private Button m_confirmButton; // 예산 초과 시 비활성화
    [SerializeField] private Button m_cancelButton;

    [SerializeField] private UIStatRow m_statsRowPrefab; // 토글 미리보기 스탯 — UIShipPresetPickerView.Column_Stats와 동일 구조/프리팹 재사용
    [SerializeField] private RectTransform m_statsContainer;

    // 빔 slot0(항상 장착 고정) 여부까지 포함해 미리 계산해둔 슬롯 목록 — dataIndex 순서 = 빔 전체 → 미사일 전체 → 격납고 전체
    private readonly struct ModuleSlotEntry
    {
        public readonly EModuleType moduleType;
        public readonly int slotIndex;
        public readonly bool isLocked;
        public ModuleSlotEntry(EModuleType moduleType, int slotIndex, bool isLocked)
        {
            this.moduleType = moduleType;
            this.slotIndex = slotIndex;
            this.isLocked = isLocked;
        }
    }

    private readonly List<ModuleSlotEntry> m_moduleSlotEntries = new();
    private readonly List<bool> m_pendingInstalled = new(); // m_moduleSlotEntries와 1:1 대응 — 로컬 편집 상태(Confirm 전까지 서버 미반영)
    private ModuleBodyInfo m_originalModules; // Confirm 시 이 값과 비교해 실제로 바뀐 슬롯만 서버로 전송

    private readonly List<UIStatRow> m_statsRows = new();
    private Dictionary<string, ShipStatRowEntry> m_originalEntriesByLabel; // 비교 기준(팝업을 열 때의 장착 상태) — RefreshRows에서 1회만 계산, 토글해도 안 바뀜
    private string m_presetId; // Stats 재계산(RefreshStatsDisplay)에서도 필요해 필드로 승격

    private int m_slotIndex = -1; // 편집 대상 — 함대편성(FleetComposition) 슬롯 인덱스
    private System.Action m_onChanged; // Confirm으로 실제 반영이 끝난 뒤 호출 — 호출부(UIPanelFleet)가 함대구성 화면(지휘력 요약 등)을 새로고침하도록

    private void Awake()
    {
        if (m_confirmButton != null)
            m_confirmButton.onClick.AddListener(OnConfirmClicked);
        if (m_cancelButton != null)
            m_cancelButton.onClick.AddListener(OnCancelClicked);
        if (m_moduleScrollView != null)
            m_moduleScrollView.onItemBind = OnModuleSlotItemBind;

        gameObject.SetActive(false);
    }

    public void Open(int slotIndex, System.Action onChanged)
    {
        m_slotIndex = slotIndex;
        m_onChanged = onChanged;
        gameObject.SetActive(true);
        RefreshRows();
    }

    public void Close()
    {
        gameObject.SetActive(false);
        m_slotIndex = -1;
        m_onChanged = null;
    }

    // 서버의 현재 장착 상태를 그대로 로컬 미리보기 상태(m_pendingInstalled)의 시작값으로 삼음
    private void RefreshRows()
    {
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition == null) return;

        List<FleetSlotEntry> placedShips = composition.GetPlacedShips();
        if (m_slotIndex < 0 || m_slotIndex >= placedShips.Count) return;

        m_presetId = placedShips[m_slotIndex].shipPresetId;
        m_originalModules = placedShips[m_slotIndex].modules;
        int[] maxSlots = FleetComposition.ParseMaxSlotsFromPresetId(m_presetId); // [beam, missile, hangar, shield, interceptor]

        m_moduleSlotEntries.Clear();
        m_pendingInstalled.Clear();
        // 슬롯 잠금 없음 — 모든 슬롯이 자유롭게 토글 가능. 공격 모듈 0개 방지는 Confirm 버튼 비활성화(RefreshCommandPowerPreview)로 처리
        AppendCategorySlots(EModuleType.beam, maxSlots[0]);
        AppendCategorySlots(EModuleType.missile, maxSlots[1]);
        AppendCategorySlots(EModuleType.hangar, maxSlots[2]);

        if (m_moduleScrollView != null && m_rowPrefab != null)
            m_moduleScrollView.Initialize(m_moduleSlotEntries.Count, m_rowPrefab.gameObject);

        RefreshCommandPowerPreview(composition);

        // 비교 기준 — 팝업을 여는 시점(토글 전)의 장착 상태. 이후 토글해도 재계산하지 않음
        ShipPresetData presetData = DataManager.Instance.m_dataTableShipPreset.GetShipPreset(m_presetId);
        m_originalEntriesByLabel = new Dictionary<string, ShipStatRowEntry>();
        if (presetData != null)
        {
            List<ShipStatRowEntry> originalEntries = ShipStatGaugeBuilder.Build(presetData, m_originalModules);
            for (int i = 0; i < originalEntries.Count; i++)
                m_originalEntriesByLabel[originalEntries[i].label] = originalEntries[i];
        }
        RefreshStatsDisplay();
    }

    private void AppendCategorySlots(EModuleType moduleType, int maxSlotCount)
    {
        List<ModuleInfo> installedList = GetModulesListForType(m_originalModules, moduleType);
        for (int i = 0; i < maxSlotCount; i++)
        {
            m_moduleSlotEntries.Add(new ModuleSlotEntry(moduleType, i, isLocked: false));
            m_pendingInstalled.Add(IsSlotInstalled(installedList, i));
        }
    }

    // InfiniteScrollView가 dataIndex번 슬롯 행을 화면에 배치할 때마다 호출 — 토글하면 서버 호출 없이 m_pendingInstalled만 갱신
    private void OnModuleSlotItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_moduleSlotEntries.Count) return;

        UIModuleSlotToggleRow row = rowObject.GetComponent<UIModuleSlotToggleRow>();
        if (row == null) return;

        ModuleSlotEntry entry = m_moduleSlotEntries[dataIndex];
        row.Setup(entry.moduleType, entry.slotIndex, m_pendingInstalled[dataIndex], entry.isLocked,
            (moduleType, slotIndex, install) => OnLocalToggleChanged(dataIndex, install));
    }

    private void OnLocalToggleChanged(int dataIndex, bool install)
    {
        if (dataIndex < 0 || dataIndex >= m_pendingInstalled.Count) return;
        m_pendingInstalled[dataIndex] = install;

        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition != null)
            RefreshCommandPowerPreview(composition);

        RefreshStatsDisplay();
    }

    // 현재 m_pendingInstalled(로컬 토글 상태) 기준으로 스탯을 다시 계산해 비교 기준(m_originalEntriesByLabel)과 비교 표시
    // — UIShipPresetPickerView.RefreshStatsDisplay와 동일한 풀링 패턴
    private void RefreshStatsDisplay()
    {
        ShipPresetData presetData = DataManager.Instance.m_dataTableShipPreset.GetShipPreset(m_presetId);
        if (presetData == null)
        {
            for (int i = 0; i < m_statsRows.Count; i++)
                m_statsRows[i].Hide();
            return;
        }

        ModuleBodyInfo pending = BuildPendingModuleBodyInfo();
        List<ShipStatRowEntry> entries = ShipStatGaugeBuilder.Build(presetData, pending);
        EnsureStatsRowCount(entries.Count);

        for (int i = 0; i < m_statsRows.Count; i++)
        {
            if (i >= entries.Count)
            {
                m_statsRows[i].Hide();
                continue;
            }

            ShipStatRowEntry entry = entries[i];
            string diffText = ShipStatGaugeBuilder.BuildDiffText(entry, m_originalEntriesByLabel);

            if (entry.isNumericValue == true)
                m_statsRows[i].SetStatRow(entry.label, entry.value, diffText);
            else
                m_statsRows[i].SetValueOnly(entry.label, entry.rawValueText, diffText);
        }

        if (m_statsContainer != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_statsContainer);
    }

    private void EnsureStatsRowCount(int neededCount)
    {
        if (m_statsContainer == null || m_statsRowPrefab == null) return;

        while (m_statsRows.Count < neededCount)
            m_statsRows.Add(Instantiate(m_statsRowPrefab, m_statsContainer));
    }

    // 현재 로컬 토글 상태(m_pendingInstalled)를 ModuleBodyInfo로 구성 — OnConfirmClicked의 desired 구성과 동일 로직
    private ModuleBodyInfo BuildPendingModuleBodyInfo()
    {
        ModuleBodyInfo pending = new ModuleBodyInfo { beams = new List<ModuleInfo>(), missiles = new List<ModuleInfo>(), hangars = new List<ModuleInfo>() };
        for (int i = 0; i < m_moduleSlotEntries.Count; i++)
        {
            if (m_pendingInstalled[i] == false) continue;

            ModuleSlotEntry entry = m_moduleSlotEntries[i];
            ModuleInfo moduleInfo = new ModuleInfo { moduleType = entry.moduleType, slotIndex = entry.slotIndex, moduleSubType = GetDefaultSubType(entry.moduleType) };
            GetModulesListForType(pending, entry.moduleType).Add(moduleInfo);
        }
        return pending;
    }

    private List<ModuleInfo> GetModulesListForType(ModuleBodyInfo modules, EModuleType moduleType)
    {
        if (modules == null) return null;
        if (moduleType == EModuleType.beam) return modules.beams;
        if (moduleType == EModuleType.missile) return modules.missiles;
        if (moduleType == EModuleType.hangar) return modules.hangars;
        return null;
    }

    private bool IsSlotInstalled(List<ModuleInfo> installedModules, int slotIndex)
    {
        if (installedModules == null) return false;
        for (int i = 0; i < installedModules.Count; i++)
        {
            if (installedModules[i].slotIndex == slotIndex) return true;
        }
        return false;
    }

    // 선택 후보를 실제로 적용했다고 가정했을 때의 지휘력 사용량을 미리 계산 — UIShipPresetPickerView.RefreshCommandPowerPreview와 동일한 표시 방식
    private void RefreshCommandPowerPreview(FleetComposition composition)
    {
        if (m_commandPowerRow == null) return;

        int usedByOtherSlots = composition.GetUsedCommandPower() - composition.GetSlotCommandCost(m_slotIndex);
        int pendingSlotCost = ComputePendingSlotCost();
        int projectedUsed = usedByOtherSlots + pendingSlotCost;
        int max = composition.GetMaxCommandPower();
        bool isOverCommandPower = projectedUsed > max;

        m_commandPowerRow.SetRow("UITabCommander_CommandPower", $"{projectedUsed} / {max}", rawValue: true);
        m_commandPowerRow.SetValueColor(CommonUtility.PaletteColor(isOverCommandPower == true ? "Text.Warning" : "Text.Dark1"));

        // 예산 초과거나 공격 모듈(빔/미사일/격납고)이 하나도 없으면 Confirm 불가 — 서버도 동일 조건을 별도로 검증함(방어선 이중화)
        bool hasAnyAttackModule = HasAnyPendingModuleInstalled();
        if (m_confirmButton != null)
            m_confirmButton.interactable = isOverCommandPower == false && hasAnyAttackModule == true;
    }

    private bool HasAnyPendingModuleInstalled()
    {
        for (int i = 0; i < m_pendingInstalled.Count; i++)
        {
            if (m_pendingInstalled[i] == true) return true;
        }
        return false;
    }

    // 바디 설치비 + 로컬로 켜둔(m_pendingInstalled) 모듈들의 설치비 합 — 서버 FleetService.computeSlotCommandCost와 동일 계산식을 미리보기용으로 재현
    private int ComputePendingSlotCost()
    {
        ShipPresetData presetData = DataManager.Instance.m_dataTableShipPreset.GetShipPreset(m_presetId);
        DataTableModule moduleTable = DataManager.Instance.m_dataTableModule;
        if (presetData == null || moduleTable == null) return 0;

        int bodyCost = 0;
        if (System.Enum.TryParse(presetData.prefabName, out EModuleSubType bodySubType))
        {
            ModuleData bodyData = moduleTable.GetModuleDataFromTable(bodySubType);
            bodyCost = bodyData != null ? bodyData.statPoint : 0;
        }

        int modulesCost = 0;
        for (int i = 0; i < m_moduleSlotEntries.Count; i++)
        {
            if (m_pendingInstalled[i] == false) continue;
            modulesCost += GetModuleInstallCost(moduleTable, m_moduleSlotEntries[i].moduleType);
        }

        return bodyCost + modulesCost;
    }

    // on/off만 지원하므로 카테고리당 서브타입은 항상 이 값 하나 — 서버 FleetService.getDefaultSubTypeForCategory와 동일 규칙
    private EModuleSubType GetDefaultSubType(EModuleType moduleType)
    {
        if (moduleType == EModuleType.beam) return EModuleSubType.beam_t1;
        if (moduleType == EModuleType.missile) return EModuleSubType.missile_t1;
        if (moduleType == EModuleType.hangar) return EModuleSubType.hangar_t1;
        return EModuleSubType.none;
    }

    private int GetModuleInstallCost(DataTableModule moduleTable, EModuleType moduleType)
    {
        EModuleSubType subType = GetDefaultSubType(moduleType);
        if (subType == EModuleSubType.none) return 0;

        ModuleData data = moduleTable.GetModuleDataFromTable(subType);
        return data != null ? data.statPoint : 0;
    }

    // 로컬 편집 상태(m_pendingInstalled) 전체를 최종 장착 구성으로 한 번에 서버에 전송 — 낱개로 순서대로 보내면
    // (해제 먼저든 장착 먼저든) 중간 상태에서 예산/공격모듈 0개 검증에 걸릴 수 있어, 서버가 결과 상태만 검증하도록 배치 전송
    private void OnConfirmClicked()
    {
        ModuleBodyInfo desired = BuildPendingModuleBodyInfo();

        SetFleetPresetSlotModulesRequest request = new SetFleetPresetSlotModulesRequest
        {
            slotIndex = m_slotIndex,
            modules = desired,
        };

        NetworkManager.Instance.SetFleetPresetSlotModules(request, response =>
        {
            if (response.errorCode != 0)
            {
                Debug.LogError($"[UIShipLoadoutEditorView] SetFleetPresetSlotModules 실패: {response.errorCode}");
                return;
            }

            FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
            if (composition != null)
                composition.ApplyModuleToggleResult(m_slotIndex, response.data.body);

            if (m_onChanged != null) m_onChanged();
            Close();
        });
    }

    private void OnCancelClicked()
    {
        Close();
    }
}
