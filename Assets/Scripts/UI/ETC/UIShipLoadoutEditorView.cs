// 함선 로드아웃 편집 화면 — UIHullPickerView가 "바디(함체) 선택"을 담당한다면, 이 컴포넌트는 그 다음 단계인
// "슬롯별 모듈 on/off 편집"을 담당한다. 토글은 전부 로컬에서만 미리보기 상태로 바뀌고, Confirm을 눌러야 서버에 실제로 반영됨
// (UIHullPickerView와 동일 패턴 — 예산 초과 상태에선 Confirm 버튼 비활성화)
// 카테고리별 최대 슬롯 수는 FleetComposition.ParseMaxSlotsFromHullSubType으로 hullSubType에서 파싱(빔/미사일/격납고/실드/요격체 — 현재 실드/요격체는 항상 0)
// 빔/미사일/격납고 슬롯을 하나의 InfiniteScrollView에 순서대로(빔 전부 → 미사일 전부 → 격납고 전부) 나열
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIShipLoadoutEditorView : MonoBehaviour
{
    [SerializeField] private InfiniteScrollView m_moduleScrollView;
    [SerializeField] private UIModuleSlotToggleRow m_rowPrefab; // 카테고리 공용 — 슬롯 1칸당 행 1개

    [SerializeField] private RowLabelValue m_commandPowerRow; // 상단 지휘력 요약(UIPanelFleet 성능 컬럼과 동일 구성)
    [SerializeField] private Button m_confirmButton; // 예산 초과 시 비활성화
    [SerializeField] private Button m_cancelButton;

    [SerializeField] private UIStatRow m_statsRowPrefab; // 선택된 슬롯 하나의 현재/팬딩 비교 — UIHullPickerView.Column_Stats와 동일 구조/프리팹 재사용
    [SerializeField] private InfiniteScrollView m_statsScrollView;
    [SerializeField] private TMP_Text m_notInstalledText; // 선택된 슬롯이 미설치일 때만 활성화
    // 강화 관리 팝업(UIPopupModuleReinforce)을 여는 버튼은 각 행(UIModuleSlotToggleRow)의 ManageButton — UIManager.ShowModuleReinforcePopup으로 열림

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
    private readonly List<int> m_pendingAttackPoints = new(); // 빔/미사일 공격력, 격납고는 대함 공격력 강화 포인트 — m_moduleSlotEntries와 1:1 대응
    private readonly List<int> m_pendingAttackToFighterPoints = new(); // 격납고 전용 대전투기 공격력 강화 포인트 — 빔/미사일은 항상 0
    private readonly List<string> m_pendingModuleSubType = new(); // 무기 모듈의 현재 티어 서브타입(예: beam_1_1) — m_moduleSlotEntries와 1:1 대응, 실드는 항상 기본값 고정
    private ModuleHullInfo m_originalModules; // Confirm 시 이 값과 비교해 실제로 바뀐 슬롯만 서버로 전송

    private List<ShipStatRowEntry> m_statEntries = new();
    private Dictionary<string, ShipStatRowEntry> m_selectedOriginalEntriesByLabel = new(); // 비교 기준 — 선택된 슬롯의 원래(팝업을 열 때) 장착 상태, 선택이 바뀔 때마다 재계산
    private string m_hullSubType; // Stats 재계산(RefreshStatsDisplay)에서도 필요해 필드로 승격
    private int m_selectedDataIndex; // 좌측에서 선택된 행 — 기본값은 항상 최상단(0)

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
        if (m_statsScrollView != null)
            m_statsScrollView.onItemBind = OnStatsItemBind;

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
        ClearSelectedModuleHighlight();

        // 편집 중 꺼뒀던 함체 전체 아웃라인을 원래대로 복원 — 모듈 Confirm으로 함선이 재스폰됐어도
        // FindEditingShip이 최신 인스턴스를 다시 조회하므로 새 인스턴스에도 정상 적용됨
        SpaceShip editingShip = FindEditingShip();
        if (editingShip != null)
            editingShip.SetShipSelected(true);

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

        m_hullSubType = placedShips[m_slotIndex].hullSubType;
        m_originalModules = placedShips[m_slotIndex].modules;
        int[] maxSlots = FleetComposition.ParseMaxSlotsFromHullSubType(m_hullSubType); // [beam, missile, hangar, shield, interceptor]

        m_moduleSlotEntries.Clear();
        m_pendingInstalled.Clear();
        m_pendingAttackPoints.Clear();
        m_pendingAttackToFighterPoints.Clear();
        m_pendingModuleSubType.Clear();
        // 슬롯 잠금 없음 — 모든 슬롯이 자유롭게 토글 가능. 공격 모듈 0개 방지는 Confirm 버튼 비활성화(RefreshCommandPowerPreview)로 처리
        AppendCategorySlots(EModuleType.beam, maxSlots[0]);
        AppendCategorySlots(EModuleType.missile, maxSlots[1]);
        AppendCategorySlots(EModuleType.hangar, maxSlots[2]);
        AppendShieldSlot(maxSlots[3]);

        if (m_moduleScrollView != null && m_rowPrefab != null)
            m_moduleScrollView.Initialize(m_moduleSlotEntries.Count, m_rowPrefab.gameObject);

        RefreshCommandPowerPreview(composition);

        // 편집 중엔 모듈 단위 하이라이트만 보이게, 함체 전체 아웃라인은 꺼둠(Close에서 복원)
        SpaceShip editingShip = FindEditingShip();
        if (editingShip != null)
            editingShip.SetShipSelected(false);

        m_selectedDataIndex = 0; // 기본 선택은 항상 최상단 행
        RefreshStatsDisplay();
        SyncSelectedModuleHighlight();
    }

    private void AppendCategorySlots(EModuleType moduleType, int maxSlotCount)
    {
        List<ModuleInfo> installedList = GetModulesListForType(m_originalModules, moduleType);
        for (int i = 0; i < maxSlotCount; i++)
        {
            m_moduleSlotEntries.Add(new ModuleSlotEntry(moduleType, i, isLocked: false));
            m_pendingInstalled.Add(IsSlotInstalled(installedList, i));

            ModuleInfo installedModule = FindInstalledModule(installedList, i);
            m_pendingAttackPoints.Add(installedModule != null ? installedModule.attackPoints : 0);
            m_pendingAttackToFighterPoints.Add(installedModule != null ? installedModule.attackToFighterPoints : 0);

            string installedSubType = installedModule != null && string.IsNullOrEmpty(installedModule.moduleSubType) == false
                ? installedModule.moduleSubType
                : GetDefaultSubType(moduleType);
            m_pendingModuleSubType.Add(installedSubType);
        }
    }

    // 실드는 슬롯이 없어(문자열 장착 여부만 존재) 리스트 카테고리와 별도 처리 — 함체에 실드 슬롯이 있을 때만(maxSlotCount>0) 행 1개 추가
    private void AppendShieldSlot(int maxSlotCount)
    {
        if (maxSlotCount <= 0) return;

        bool isInstalled = m_originalModules != null && string.IsNullOrEmpty(m_originalModules.shieldModuleSubType) == false;
        m_moduleSlotEntries.Add(new ModuleSlotEntry(EModuleType.shield, 0, isLocked: false));
        m_pendingInstalled.Add(isInstalled);
        m_pendingAttackPoints.Add(0);
        m_pendingAttackToFighterPoints.Add(0);
        m_pendingModuleSubType.Add(GetDefaultSubType(EModuleType.shield));
    }

    private ModuleInfo FindInstalledModule(List<ModuleInfo> installedModules, int slotIndex)
    {
        if (installedModules == null) return null;
        for (int i = 0; i < installedModules.Count; i++)
        {
            if (installedModules[i].slotIndex == slotIndex) return installedModules[i];
        }
        return null;
    }

    // InfiniteScrollView가 dataIndex번 슬롯 행을 화면에 배치할 때마다 호출 — 토글하면 서버 호출 없이 m_pendingInstalled만 갱신
    private void OnModuleSlotItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_moduleSlotEntries.Count) return;

        UIModuleSlotToggleRow row = rowObject.GetComponent<UIModuleSlotToggleRow>();
        if (row == null) return;

        ModuleSlotEntry entry = m_moduleSlotEntries[dataIndex];
        // 강화 포인트뿐 아니라 현재 티어의 설치비(statPoint)도 이 슬롯이 쓰는 지휘력이므로 합산해서 표시
        int tierCost = GetModuleInstallCostBySubType(DataManager.Instance.m_dataTableModule, m_pendingModuleSubType[dataIndex]);
        int investedPoints = tierCost + m_pendingAttackPoints[dataIndex] + m_pendingAttackToFighterPoints[dataIndex];
        bool isSelected = dataIndex == m_selectedDataIndex;

        row.Setup(entry.moduleType, entry.slotIndex, m_pendingInstalled[dataIndex], entry.isLocked,
            investedPoints, isSelected, m_pendingModuleSubType[dataIndex],
            (moduleType, slotIndex, install) => OnLocalToggleChanged(dataIndex, install),
            (moduleType, slotIndex) => OnRowSelected(dataIndex),
            (moduleType, slotIndex) => OnManageButtonClicked(dataIndex));
    }

    // 좌측 행 클릭 — 선택 상태만 바뀜(장착 여부와 무관). SelectedImage 갱신을 위해 보이는 행 전체를 다시 bind
    private void OnRowSelected(int dataIndex)
    {
        if (dataIndex < 0 || dataIndex >= m_moduleSlotEntries.Count) return;
        if (dataIndex == m_selectedDataIndex) return;

        m_selectedDataIndex = dataIndex;
        if (m_moduleScrollView != null)
            m_moduleScrollView.RefreshVisible();

        RefreshStatsDisplay();
        SyncSelectedModuleHighlight();
    }

    // 좌측에서 선택된 슬롯에 대응하는 실제 3D 모듈에 그리드 오버레이 하이라이트 표시 — SpaceShip.SetSelectedModule/ClearSelectedModule
    // (모듈 단위 선택 인프라, SelectedModuleVisual)은 이미 있었지만 아무 데서도 안 부르고 있던 코드라 여기서 처음 연결함
    private void SyncSelectedModuleHighlight()
    {
        SpaceShip ship = FindEditingShip();
        if (ship == null) return;

        ModuleBase module = null;
        if (m_selectedDataIndex >= 0 && m_selectedDataIndex < m_moduleSlotEntries.Count && ship.m_moduleHulls.Count > 0)
        {
            ModuleSlotEntry entry = m_moduleSlotEntries[m_selectedDataIndex];
            if (entry.moduleType != EModuleType.shield) // 실드는 SetupSelectedModuleVisualing 대상에서 빠져있어 하이라이트 대상 없음
                module = ship.m_moduleHulls[0].FindModuleOrPlaceholder(entry.moduleType, entry.slotIndex); // 미설치 슬롯도 Placeholder 위치에 하이라이트 표시
        }

        if (module != null)
            ship.SetSelectedModule(ship, module);
        else
            ship.ClearSelectedModule();
    }

    private void ClearSelectedModuleHighlight()
    {
        SpaceShip ship = FindEditingShip();
        if (ship != null)
            ship.ClearSelectedModule();
    }

    // m_slotIndex(FleetComposition 슬롯 인덱스) == SpaceShip.m_shipInfo.positionIndex 기준으로 실제 3D 함선 인스턴스 조회
    // — UIPanelFleet.Sync3DShipOutlineSelection과 동일한 조회 방식
    private SpaceShip FindEditingShip()
    {
        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet == null) return null;

        return myFleet.m_ships.Find(s => s != null && s.m_shipInfo.positionIndex == m_slotIndex);
    }

    private void OnLocalToggleChanged(int dataIndex, bool install)
    {
        if (dataIndex < 0 || dataIndex >= m_pendingInstalled.Count) return;
        m_pendingInstalled[dataIndex] = install;
        if (install == false)
        {
            m_pendingAttackPoints[dataIndex] = 0;
            m_pendingAttackToFighterPoints[dataIndex] = 0;
        }

        ApplyModulePreviewToShip(dataIndex, m_pendingModuleSubType[dataIndex], m_pendingAttackPoints[dataIndex], m_pendingAttackToFighterPoints[dataIndex], install);

        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition != null)
            RefreshCommandPowerPreview(composition);

        RefreshStatsDisplay();

        // 설치 여부가 바뀌면 ManageButton/Invested CP 노출 여부도 같이 바뀌어야 하므로 해당 행을 다시 bind
        if (m_moduleScrollView != null)
            m_moduleScrollView.RefreshVisible();
    }

    // 편집 대상 함선(FindEditingShip)의 해당 슬롯에 설치/해제/티어변경을 즉시 반영 — Confirm 전 3D 실시간 프리뷰
    // 실드는 3D 비주얼이 없는 논리 컴포넌트라(ModuleShield.cs 주석 참고) 로직 값만 갱신되고 외형 변화는 없음
    private void ApplyModulePreviewToShip(int dataIndex, string subType, int attackPoints, int attackToFighterPoints, bool installed)
    {
        if (dataIndex < 0 || dataIndex >= m_moduleSlotEntries.Count) return;

        SpaceShip ship = FindEditingShip();
        if (ship == null || ship.m_moduleHulls.Count == 0) return;

        ModuleHull body = ship.m_moduleHulls[0];
        ModuleSlotEntry entry = m_moduleSlotEntries[dataIndex];

        if (entry.moduleType == EModuleType.shield)
        {
            body.InitializeShield(installed == true ? subType : "");
            return;
        }

        body.SetModuleSlotState(entry.moduleType, entry.slotIndex, installed, subType, attackPoints, attackToFighterPoints);
    }

    // CANCEL 시 편집한 슬롯 전부를 원본 상태로 복원 — 파괴/재생성 없이 ModuleHull이 보관해둔 원본을 그대로 재사용
    private void RevertAllModulePreviewsToOriginal()
    {
        SpaceShip ship = FindEditingShip();
        if (ship == null || ship.m_moduleHulls.Count == 0) return;

        ship.m_moduleHulls[0].RevertAllSlotsToOriginal();
    }

    // ManageButton 클릭 — 강화 포인트 편집 팝업 오픈. 현재 pending 값을 팝업의 초기 로컬 버퍼로 전달
    private void OnManageButtonClicked(int dataIndex)
    {
        if (dataIndex < 0 || dataIndex >= m_moduleSlotEntries.Count) return;

        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition == null) return;

        ModuleSlotEntry entry = m_moduleSlotEntries[dataIndex];
        DataTableModule moduleTable = DataManager.Instance.m_dataTableModule;
        string currentSubType = m_pendingModuleSubType[dataIndex];
        int installCost = GetModuleInstallCostBySubType(moduleTable, currentSubType);

        int usedByOtherSlots = composition.GetUsedCommandPower() - composition.GetSlotCommandCost(m_slotIndex)
            + (ComputePendingSlotCost() - installCost - m_pendingAttackPoints[dataIndex] - m_pendingAttackToFighterPoints[dataIndex]);
        int maxCommandPower = composition.GetMaxCommandPower();

        int hullTier = CommonUtility.ParseTier(m_hullSubType);
        UIManager.Instance.ShowModuleReinforcePopup(entry.moduleType, currentSubType,
            m_pendingAttackPoints[dataIndex], m_pendingAttackToFighterPoints[dataIndex],
            maxCommandPower, usedByOtherSlots, installCost, hullTier,
            (confirmedSubType, confirmedAttackPoints, confirmedAttackToFighterPoints) => OnReinforceConfirmed(dataIndex, confirmedSubType, confirmedAttackPoints, confirmedAttackToFighterPoints));
    }

    private void OnReinforceConfirmed(int dataIndex, string moduleSubType, int attackPoints, int attackToFighterPoints)
    {
        if (dataIndex < 0 || dataIndex >= m_pendingAttackPoints.Count) return;
        m_pendingModuleSubType[dataIndex] = moduleSubType;
        m_pendingAttackPoints[dataIndex] = attackPoints;
        m_pendingAttackToFighterPoints[dataIndex] = attackToFighterPoints;

        ApplyModulePreviewToShip(dataIndex, moduleSubType, attackPoints, attackToFighterPoints, installed: true);

        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition != null)
            RefreshCommandPowerPreview(composition);

        RefreshStatsDisplay();

        if (m_moduleScrollView != null)
            m_moduleScrollView.RefreshVisible();
    }

    // 선택된 슬롯 하나(m_selectedDataIndex)의 현재 상태 vs 팬딩 상태 비교 — 함선 전체가 아니라 그 모듈 자체의 스탯만 표시
    // — InfiniteScrollView가 화면에 보이는 행만 OnStatsItemBind로 바인딩하므로 여기서는 m_statEntries만 갱신
    private void RefreshStatsDisplay()
    {
        bool hasSelection = m_selectedDataIndex >= 0 && m_selectedDataIndex < m_moduleSlotEntries.Count;
        bool isInstalled = hasSelection == true && m_pendingInstalled[m_selectedDataIndex] == true;

        if (m_notInstalledText != null)
            m_notInstalledText.gameObject.SetActive(hasSelection == true && isInstalled == false);

        if (isInstalled == false)
        {
            m_statEntries.Clear();
            if (m_statsScrollView != null && m_statsRowPrefab != null)
                m_statsScrollView.Initialize(0, m_statsRowPrefab.gameObject);
            return;
        }

        ModuleSlotEntry entry = m_moduleSlotEntries[m_selectedDataIndex];
        ModuleHullInfo pendingHull = BuildSingleModuleHullInfo(entry.moduleType, m_pendingModuleSubType[m_selectedDataIndex],
            m_pendingAttackPoints[m_selectedDataIndex], m_pendingAttackToFighterPoints[m_selectedDataIndex]);
        m_statEntries = ShipStatGaugeBuilder.Build(null, pendingHull, includeBodyStats: false);

        m_selectedOriginalEntriesByLabel = BuildOriginalEntriesByLabelForSelectedSlot(entry);

        if (m_statsScrollView != null && m_statsRowPrefab != null)
            m_statsScrollView.Initialize(m_statEntries.Count, m_statsRowPrefab.gameObject);
    }

    // 선택된 슬롯이 팝업을 열었던 시점(m_originalModules)에 실제로 설치돼 있던 모듈 기준 비교값 — 원래 미설치였으면 빈 딕셔너리(=0 기준 diff)
    private Dictionary<string, ShipStatRowEntry> BuildOriginalEntriesByLabelForSelectedSlot(ModuleSlotEntry entry)
    {
        Dictionary<string, ShipStatRowEntry> result = new Dictionary<string, ShipStatRowEntry>();

        bool wasInstalled = TryGetOriginalModuleState(entry, out string originalSubType, out int originalAttackPoints, out int originalAttackToFighterPoints);
        if (wasInstalled == false) return result;

        ModuleHullInfo originalHull = BuildSingleModuleHullInfo(entry.moduleType, originalSubType, originalAttackPoints, originalAttackToFighterPoints);
        List<ShipStatRowEntry> originalEntries = ShipStatGaugeBuilder.Build(null, originalHull, includeBodyStats: false);
        for (int i = 0; i < originalEntries.Count; i++)
            result[originalEntries[i].label] = originalEntries[i];

        return result;
    }

    // 이 슬롯이 편집을 시작한 시점(m_originalModules, 서버 확정 상태)에 실제로 설치돼 있었는지와 그 서브타입/포인트 — 스탯 비교 기준과 CANCEL 롤백 양쪽에서 재사용
    private bool TryGetOriginalModuleState(ModuleSlotEntry entry, out string subType, out int attackPoints, out int attackToFighterPoints)
    {
        subType = null;
        attackPoints = 0;
        attackToFighterPoints = 0;

        if (entry.moduleType == EModuleType.shield)
        {
            if (m_originalModules != null && string.IsNullOrEmpty(m_originalModules.shieldModuleSubType) == false)
                subType = m_originalModules.shieldModuleSubType;
        }
        else
        {
            ModuleInfo original = FindInstalledModule(GetModulesListForType(m_originalModules, entry.moduleType), entry.slotIndex);
            if (original != null)
            {
                subType = original.moduleSubType;
                attackPoints = original.attackPoints;
                attackToFighterPoints = original.attackToFighterPoints;
            }
        }

        return subType != null;
    }

    // 지금 pending 상태가 편집 시작 시점(원본)과 하나라도 다른 슬롯이 있는지 — CONFIRM에서 무변경이면 서버 요청/재스폰을 생략하기 위한 가드
    private bool HasPendingChanges()
    {
        for (int i = 0; i < m_moduleSlotEntries.Count; i++)
        {
            ModuleSlotEntry entry = m_moduleSlotEntries[i];
            bool wasInstalled = TryGetOriginalModuleState(entry, out string originalSubType, out int originalAttackPoints, out int originalAttackToFighterPoints);

            if (m_pendingInstalled[i] != wasInstalled) return true;
            if (wasInstalled == false) continue; // 둘 다 미설치면 이 슬롯은 비교할 게 없음

            if (m_pendingModuleSubType[i] != originalSubType) return true;
            if (m_pendingAttackPoints[i] != originalAttackPoints) return true;
            if (m_pendingAttackToFighterPoints[i] != originalAttackToFighterPoints) return true;
        }
        return false;
    }

    // 슬롯 1개짜리 ModuleHullInfo 조립 — BuildPendingModuleHullInfo와 동일 패턴을 단일 모듈에 적용
    private ModuleHullInfo BuildSingleModuleHullInfo(EModuleType moduleType, string moduleSubType, int attackPoints, int attackToFighterPoints)
    {
        ModuleHullInfo hull = new ModuleHullInfo { beams = new List<ModuleInfo>(), missiles = new List<ModuleInfo>(), hangars = new List<ModuleInfo>(), shieldModuleSubType = "" };

        if (moduleType == EModuleType.shield)
        {
            hull.shieldModuleSubType = moduleSubType;
            return hull;
        }

        ModuleInfo moduleInfo = new ModuleInfo
        {
            moduleType = moduleType,
            slotIndex = 0,
            moduleSubType = moduleSubType,
            attackPoints = attackPoints,
            attackToFighterPoints = attackToFighterPoints,
        };
        GetModulesListForType(hull, moduleType).Add(moduleInfo);
        return hull;
    }

    // InfiniteScrollView가 dataIndex번 스탯 행을 화면에 배치할 때마다 호출 — 캐시된 m_statEntries로 바인딩
    private void OnStatsItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_statEntries.Count) return;

        UIStatRow row = rowObject.GetComponent<UIStatRow>();
        if (row == null) return;

        ShipStatRowEntry entry = m_statEntries[dataIndex];
        string diffText = ShipStatGaugeBuilder.BuildDiffText(entry, m_selectedOriginalEntriesByLabel);

        if (entry.isNumericValue == true)
            row.SetStatRow(entry.label, entry.value, diffText);
        else
            row.SetValueOnly(entry.label, entry.rawValueText, diffText);
    }

    // 현재 로컬 토글 상태(m_pendingInstalled)를 ModuleHullInfo로 구성 — OnConfirmClicked의 desired 구성과 동일 로직
    private ModuleHullInfo BuildPendingModuleHullInfo()
    {
        ModuleHullInfo pending = new ModuleHullInfo { beams = new List<ModuleInfo>(), missiles = new List<ModuleInfo>(), hangars = new List<ModuleInfo>(), shieldModuleSubType = "" };
        for (int i = 0; i < m_moduleSlotEntries.Count; i++)
        {
            if (m_pendingInstalled[i] == false) continue;

            ModuleSlotEntry entry = m_moduleSlotEntries[i];

            if (entry.moduleType == EModuleType.shield)
            {
                pending.shieldModuleSubType = GetDefaultSubType(entry.moduleType);
                continue;
            }

            ModuleInfo moduleInfo = new ModuleInfo
            {
                moduleType = entry.moduleType,
                slotIndex = entry.slotIndex,
                moduleSubType = m_pendingModuleSubType[i],
                attackPoints = m_pendingAttackPoints[i],
                attackToFighterPoints = m_pendingAttackToFighterPoints[i],
            };
            GetModulesListForType(pending, entry.moduleType).Add(moduleInfo);
        }
        return pending;
    }

    private List<ModuleInfo> GetModulesListForType(ModuleHullInfo modules, EModuleType moduleType)
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

    // 선택 후보를 실제로 적용했다고 가정했을 때의 지휘력 사용량을 미리 계산 — UIHullPickerView.RefreshCommandPowerPreview와 동일한 표시 방식
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
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_commandPowerRow.transform as RectTransform);

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

    // 바디 설치비 + 로컬로 켜둔(m_pendingInstalled) 모듈들의 설치비/강화 포인트 합 — 서버 FleetService.computeSlotCommandCost와 동일 계산식을 미리보기용으로 재현
    private int ComputePendingSlotCost()
    {
        DataTableModule moduleTable = DataManager.Instance.m_dataTableModule;
        if (moduleTable == null) return 0;

        ModuleData bodyData = moduleTable.GetModuleDataFromTable(m_hullSubType);
        int bodyCost = bodyData != null ? bodyData.statPoint : 0;

        int modulesCost = 0;
        for (int i = 0; i < m_moduleSlotEntries.Count; i++)
        {
            if (m_pendingInstalled[i] == false) continue;
            int installCost = GetModuleInstallCostBySubType(moduleTable, m_pendingModuleSubType[i]);
            int reinforceCost = m_pendingAttackPoints[i] + m_pendingAttackToFighterPoints[i];
            modulesCost += installCost + reinforceCost;
        }

        return bodyCost + modulesCost;
    }

    // 새로 장착하는 빈 슬롯(기존 장착 이력 없음)의 초기 서브타입 — 무기 티어는 함체와 독립적인 별도 축이라 기본값은 항상 1티어.
    // 장착 후에는 UIPopupModuleReinforce의 티어업/다운으로 m_pendingModuleSubType이 바뀌므로 이 값은 시작점일 뿐임(실드는 티어 없이 항상 이 값 고정)
    private string GetDefaultSubType(EModuleType moduleType)
    {
        if (moduleType == EModuleType.beam) return "beam_1_1";
        if (moduleType == EModuleType.missile) return "missile_1_1";
        if (moduleType == EModuleType.hangar) return "hangar_1_1";
        if (moduleType == EModuleType.shield) return "shield_1_1";
        return "";
    }

    private int GetModuleInstallCostBySubType(DataTableModule moduleTable, string subType)
    {
        if (string.IsNullOrEmpty(subType) == true) return 0;

        ModuleData data = moduleTable.GetModuleDataFromTable(subType);
        return data != null ? data.statPoint : 0;
    }

    // 로컬 편집 상태(m_pendingInstalled) 전체를 최종 장착 구성으로 한 번에 서버에 전송 — 낱개로 순서대로 보내면
    // (해제 먼저든 장착 먼저든) 중간 상태에서 예산/공격모듈 0개 검증에 걸릴 수 있어, 서버가 결과 상태만 검증하도록 배치 전송
    private void OnConfirmClicked()
    {
        // 아무 것도 안 바뀌었으면 서버 요청/함선 재스폰 없이 그냥 닫음 — 3D 프리뷰는 이미 pending 값과 항상 동기화돼 있어 별도 되돌림도 불필요
        if (HasPendingChanges() == false)
        {
            Close();
            return;
        }

        ModuleHullInfo desired = BuildPendingModuleHullInfo();

        SetModuleRequest request = new SetModuleRequest
        {
            slotIndex = m_slotIndex,
            modules = desired,
        };

        NetworkManager.Instance.SetModule(request, response =>
        {
            if (response.errorCode != 0)
            {
                Debug.LogError($"[UIShipLoadoutEditorView] SetModule 실패: {response.errorCode}");
                return;
            }

            FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
            if (composition != null)
                composition.ApplyModuleToggleResult(m_slotIndex, response.data.hull);

            if (m_onChanged != null) m_onChanged();
            Close();
        });
    }

    private void OnCancelClicked()
    {
        RevertAllModulePreviewsToOriginal();
        Close();
    }
}
