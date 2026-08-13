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
    [SerializeField] private UIButtonHasChildren m_confirmButton; // 예산 초과 시 비활성화
    [SerializeField] private Button m_cancelButton;

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

    private int m_slotIndex = -1; // 편집 대상 — 함대편성(FleetComposition) 슬롯 인덱스
    private System.Action m_onChanged; // Confirm으로 실제 반영이 끝난 뒤 호출 — 호출부(UIPanelFleet)가 함대구성 화면(지휘력 요약 등)을 새로고침하도록

    private void Awake()
    {
        if (m_confirmButton != null)
        {
            m_confirmButton.GetButton().onClick.AddListener(OnConfirmClicked);
            m_confirmButton.SetActiveColorKey("GeneralNeon");
            m_confirmButton.SetInactiveColorKey("Button.Disabled");
        }
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

        string presetId = placedShips[m_slotIndex].shipPresetId;
        m_originalModules = placedShips[m_slotIndex].modules;
        int[] maxSlots = FleetComposition.ParseMaxSlotsFromPresetId(presetId); // [beam, missile, hangar, shield, interceptor]

        m_moduleSlotEntries.Clear();
        m_pendingInstalled.Clear();
        // 슬롯 잠금 없음 — 모든 슬롯이 자유롭게 토글 가능. 공격 모듈 0개 방지는 Confirm 버튼 비활성화(RefreshCommandPowerPreview)로 처리
        AppendCategorySlots(EModuleType.beam, maxSlots[0]);
        AppendCategorySlots(EModuleType.missile, maxSlots[1]);
        AppendCategorySlots(EModuleType.hangar, maxSlots[2]);

        if (m_moduleScrollView != null && m_rowPrefab != null)
            m_moduleScrollView.Initialize(m_moduleSlotEntries.Count, m_rowPrefab.gameObject);

        RefreshCommandPowerPreview(composition);
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
            m_confirmButton.SetInteractable(isOverCommandPower == false && hasAnyAttackModule == true);
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
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        List<FleetSlotEntry> placedShips = composition.GetPlacedShips();
        if (m_slotIndex < 0 || m_slotIndex >= placedShips.Count) return 0;

        string presetId = placedShips[m_slotIndex].shipPresetId;
        ShipPresetData presetData = DataManager.Instance.m_dataTableShipPreset.GetShipPreset(presetId);
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
    private int GetModuleInstallCost(DataTableModule moduleTable, EModuleType moduleType)
    {
        EModuleSubType subType;
        if (moduleType == EModuleType.beam) subType = EModuleSubType.beam_t1;
        else if (moduleType == EModuleType.missile) subType = EModuleSubType.missile_t1;
        else if (moduleType == EModuleType.hangar) subType = EModuleSubType.hangar_t1;
        else return 0;

        ModuleData data = moduleTable.GetModuleDataFromTable(subType);
        return data != null ? data.statPoint : 0;
    }

    // 로컬 편집 상태(m_pendingInstalled) 전체를 최종 장착 구성으로 한 번에 서버에 전송 — 낱개로 순서대로 보내면
    // (해제 먼저든 장착 먼저든) 중간 상태에서 예산/공격모듈 0개 검증에 걸릴 수 있어, 서버가 결과 상태만 검증하도록 배치 전송
    private void OnConfirmClicked()
    {
        ModuleBodyInfo desired = new ModuleBodyInfo { beams = new List<ModuleInfo>(), missiles = new List<ModuleInfo>(), hangars = new List<ModuleInfo>() };
        for (int i = 0; i < m_moduleSlotEntries.Count; i++)
        {
            if (m_pendingInstalled[i] == false) continue;

            ModuleSlotEntry entry = m_moduleSlotEntries[i];
            ModuleInfo moduleInfo = new ModuleInfo { moduleType = entry.moduleType, slotIndex = entry.slotIndex };
            GetModulesListForType(desired, entry.moduleType).Add(moduleInfo);
        }

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
