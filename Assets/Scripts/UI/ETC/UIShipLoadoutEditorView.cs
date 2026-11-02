// 함선 로드아웃 편집 화면 — UIHullPickerView가 "바디(함체) 선택"을 담당한다면, 이 컴포넌트는 그 다음 단계인
// "슬롯별 모듈 레벨 편집(ManageButton → M6 레벨업 팝업)"을 담당한다. 모듈은 항상 설치된 상태 — 설치/해제 개념 없음.
// 레벨업은 로컬 미리보기 상태로 바뀌고, Confirm을 눌러야 서버에 실제로 반영됨(UIHullPickerView와 동일 패턴)
// 카테고리별 최대 슬롯 수는 FleetComposition.ParseMaxSlotsFromHullSubType으로 hullSubType에서 파싱(빔/미사일/격납고/실드/요격체 —
// 티어6+ 함체는 실드/요격체 슬롯을 항상 보유, 슬롯만 있으면 바로 사용 가능)
// 빔/미사일/격납고 슬롯을 하나의 InfiniteScrollView에 순서대로(빔 전부 → 미사일 전부 → 격납고 전부) 나열
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIShipLoadoutEditorView : MonoBehaviour
{
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private TMP_Text m_statsTitleText;
    [SerializeField] private TMP_Text m_confirmButtonText;
    [SerializeField] private TMP_Text m_cancelButtonText;
    [SerializeField] private InfiniteScrollView m_moduleScrollView;
    [SerializeField] private UIModuleSlotToggleRow m_rowPrefab; // 카테고리 공용 — 슬롯 1칸당 행 1개

    [SerializeField] private RowLabelValue m_commandPowerRow; // 상단 지휘력 요약(UIPanelFleet 성능 컬럼과 동일 구성)
    [SerializeField] private Button m_confirmButton; // 예산 초과 시 비활성화
    [SerializeField] private Button m_cancelButton;

    [SerializeField] private UIStatRow m_statsRowPrefab; // 선택된 슬롯 하나의 현재/팬딩 비교 — UIHullPickerView.Column_Stats와 동일 구조/프리팹 재사용
    [SerializeField] private InfiniteScrollView m_statsScrollView;

    // 빔 slot0(항상 장착 고정) 여부까지 포함해 미리 계산해둔 슬롯 목록 — dataIndex 순서 = 빔 전체 → 미사일 전체 → 격납고 전체
    private readonly struct ModuleSlotEntry
    {
        public readonly EModuleType moduleType;
        public readonly int slotIndex;
        public ModuleSlotEntry(EModuleType moduleType, int slotIndex)
        {
            this.moduleType = moduleType;
            this.slotIndex = slotIndex;
        }
    }

    private readonly List<ModuleSlotEntry> m_moduleSlotEntries = new();
    // m_moduleSlotEntries와 1:1 대응 — 슬롯의 현재 편집 상태(서브타입). 모듈은 항상 설치된 상태로 취급(설치/해제 없음)
    // 실드/요격체는 ModuleHullInfo 필드가 아니라 이 형태로 편집(ApplyEditInfoToHull/CreateEditInfoFromHull로 변환)
    private readonly List<ModuleInfo> m_pendingModules = new();
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

        // 한 번 세팅되면 바뀌지 않는 정적 라벨 — Open()마다 반복 세팅하지 않고 Awake에서 1회만 처리
        if (m_titleText != null)
            CommonUtility.SetUILocText(m_titleText, "UIFleet_ShipLoadOut_Title");
        if (m_statsTitleText != null)
            CommonUtility.SetUILocText(m_statsTitleText, "UIFleet_StatsTitle");
        if (m_confirmButtonText != null)
            CommonUtility.SetUILocText(m_confirmButtonText, "UI_Confirm");
        if (m_cancelButtonText != null)
            CommonUtility.SetUILocText(m_cancelButtonText, "UI_Cancel");

        // 로드아웃 확인 버튼을 가리킬 수 있도록 리졸버 등록
        m_confirmButtonResolver = ResolveConfirmButton;
        TutorialManager.Instance.RegisterDynamicTarget(TutorialManager.DYNAMIC_TARGET_LOADOUT_CONFIRM_BUTTON, m_confirmButtonResolver);

        gameObject.SetActive(false);
    }

    private System.Func<RectTransform> m_confirmButtonResolver;

    private void OnDestroy()
    {
        TutorialManager tutorialManager = TutorialManager.Instance; // 종료 중이면 null
        if (tutorialManager == null) return;

        tutorialManager.UnregisterDynamicTarget(TutorialManager.DYNAMIC_TARGET_LOADOUT_CONFIRM_BUTTON, m_confirmButtonResolver);
    }

    private RectTransform ResolveConfirmButton()
    {
        if (m_confirmButton == null || m_confirmButton.gameObject.activeInHierarchy == false) return null;
        return m_confirmButton.GetComponent<RectTransform>();
    }

    public void Open(int slotIndex, System.Action onChanged)
    {
        m_slotIndex = slotIndex;
        m_onChanged = onChanged;
        gameObject.SetActive(true);
        RefreshRows();
    }

    // 성능 컬럼 모듈 아이콘 클릭 등 — 편집 화면을 열면서 그 슬롯을 선택하고 스크롤뷰 안에 보이게 함. 슬롯의 dataIndex를 반환(없으면 -1)
    public int OpenAndSelectSlot(int slotIndex, EModuleType moduleType, int categorySlotIndex, System.Action onChanged)
    {
        Open(slotIndex, onChanged);

        int dataIndex = FindDataIndex(moduleType, categorySlotIndex);
        if (dataIndex < 0) return -1;

        OnRowSelected(dataIndex);
        if (m_moduleScrollView != null)
            m_moduleScrollView.EnsureVisible(dataIndex); // 아래쪽 슬롯(실드/요격체 등)도 스크롤뷰 안에 보이도록

        return dataIndex;
    }

    private int FindDataIndex(EModuleType moduleType, int slotIndex)
    {
        for (int i = 0; i < m_moduleSlotEntries.Count; i++)
        {
            if (m_moduleSlotEntries[i].moduleType == moduleType && m_moduleSlotEntries[i].slotIndex == slotIndex)
                return i;
        }
        return -1;
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

    // 서버의 현재 장착 상태를 그대로 로컬 미리보기 상태(m_pendingModules)의 시작값으로 삼음
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
        m_pendingModules.Clear();
        // 모든 슬롯은 항상 설치된 상태 — 설치/해제 선택 없음
        AppendCategorySlots(EModuleType.beam, maxSlots[0]);
        AppendCategorySlots(EModuleType.missile, maxSlots[1]);
        AppendCategorySlots(EModuleType.hangar, maxSlots[2]);
        AppendShieldSlot(maxSlots[3]);
        AppendInterceptorSlot(maxSlots[4]);

        // 원래 미장착이던 슬롯도 이제 기본 설치 상태로 보여야 하므로, 3D 프리뷰를 pending 상태(기본값 포함)로 동기화
        for (int i = 0; i < m_moduleSlotEntries.Count; i++)
            ApplyModulePreviewToShip(i, m_pendingModules[i]);

        m_selectedDataIndex = 0; // 기본 선택은 항상 최상단 행 — 행 바인딩(Initialize)이 이 값으로 선택 표시를 그리므로 그 전에 초기화
        if (m_moduleScrollView != null && m_rowPrefab != null)
            m_moduleScrollView.Initialize(m_moduleSlotEntries.Count, m_rowPrefab.gameObject);

        RefreshCommandPowerPreview(composition);

        // 편집 중엔 모듈 단위 하이라이트만 보이게, 함체 전체 아웃라인은 꺼둠(Close에서 복원)
        SpaceShip editingShip = FindEditingShip();
        if (editingShip != null)
            editingShip.SetShipSelected(false);

        RefreshStatsDisplay();
        SyncSelectedModuleHighlight();
    }

    private void AppendCategorySlots(EModuleType moduleType, int maxSlotCount)
    {
        List<ModuleInfo> installedList = GetModulesListForType(m_originalModules, moduleType);
        for (int i = 0; i < maxSlotCount; i++)
        {
            m_moduleSlotEntries.Add(new ModuleSlotEntry(moduleType, i));

            ModuleInfo installedModule = FindInstalledModule(installedList, i);
            ModuleInfo editInfo = installedModule != null ? FleetComposition.CloneModuleInfo(installedModule) : new ModuleInfo { moduleType = moduleType, slotIndex = i };
            if (string.IsNullOrEmpty(editInfo.moduleSubType) == true) editInfo.moduleSubType = GetDefaultSubType(moduleType);
            m_pendingModules.Add(editInfo);
        }
    }

    // 실드는 슬롯이 없어(문자열 필드 하나) 리스트 카테고리와 별도 처리 — 함체에 실드 슬롯이 있을 때만(maxSlotCount>0) 행 1개 추가, 항상 설치된 상태
    private void AppendShieldSlot(int maxSlotCount)
    {
        if (maxSlotCount <= 0) return;

        m_moduleSlotEntries.Add(new ModuleSlotEntry(EModuleType.shield, 0));
        // 이미 장착돼 있으면 실제 레벨(예: shield_5_1)로 시드해야 강화 팝업이 그 레벨부터 시작함 — 미장착 이력이면 기본 레벨1
        ModuleInfo editInfo = CreateEditInfoFromHull(EModuleType.shield, m_originalModules);
        if (editInfo == null) editInfo = new ModuleInfo { moduleType = EModuleType.shield, slotIndex = 0, moduleSubType = GetDefaultSubType(EModuleType.shield) };
        m_pendingModules.Add(editInfo);
    }

    // 요격체는 슬롯이 없어(문자열 필드 하나) 리스트 카테고리와 별도 처리 — 함체에 요격체 슬롯이 있을 때만(maxSlotCount>0) 행 1개 추가, 항상 설치된 상태
    private void AppendInterceptorSlot(int maxSlotCount)
    {
        if (maxSlotCount <= 0) return;

        m_moduleSlotEntries.Add(new ModuleSlotEntry(EModuleType.interceptor, 0));
        // 이미 장착돼 있으면 실제 레벨로 시드해야 강화 팝업이 그 레벨부터 시작함 — 미장착 이력이면 기본 레벨1
        ModuleInfo editInfo = CreateEditInfoFromHull(EModuleType.interceptor, m_originalModules);
        if (editInfo == null) editInfo = new ModuleInfo { moduleType = EModuleType.interceptor, slotIndex = 0, moduleSubType = GetDefaultSubType(EModuleType.interceptor) };
        m_pendingModules.Add(editInfo);
    }

    // 실드/요격체 ModuleHullInfo 필드 → 편집용 ModuleInfo (미장착이면 null)
    private ModuleInfo CreateEditInfoFromHull(EModuleType moduleType, ModuleHullInfo hull)
    {
        if (hull == null) return null;

        if (moduleType == EModuleType.shield && string.IsNullOrEmpty(hull.shieldModuleSubType) == false)
            return new ModuleInfo { moduleType = moduleType, slotIndex = 0, moduleSubType = hull.shieldModuleSubType };

        if (moduleType == EModuleType.interceptor && string.IsNullOrEmpty(hull.interceptorModuleSubType) == false)
            return new ModuleInfo { moduleType = moduleType, slotIndex = 0, moduleSubType = hull.interceptorModuleSubType };

        return null;
    }

    // 편집용 ModuleInfo → ModuleHullInfo에 반영 (CreateEditInfoFromHull의 역변환) — 빔/미사일/격납고는 해당 리스트에 추가
    private void ApplyEditInfoToHull(ModuleHullInfo hull, ModuleInfo info)
    {
        if (info.moduleType == EModuleType.shield)
        {
            hull.shieldModuleSubType = info.moduleSubType;
            return;
        }

        if (info.moduleType == EModuleType.interceptor)
        {
            hull.interceptorModuleSubType = info.moduleSubType;
            return;
        }

        GetModulesListForType(hull, info.moduleType).Add(FleetComposition.CloneModuleInfo(info));
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

    // InfiniteScrollView가 dataIndex번 슬롯 행을 화면에 배치할 때마다 호출
    private void OnModuleSlotItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_moduleSlotEntries.Count) return;

        UIModuleSlotToggleRow row = rowObject.GetComponent<UIModuleSlotToggleRow>();
        if (row == null) return;

        ModuleSlotEntry entry = m_moduleSlotEntries[dataIndex];
        ModuleInfo pending = m_pendingModules[dataIndex];
        bool isSelected = dataIndex == m_selectedDataIndex;

        row.Setup(entry.moduleType, entry.slotIndex,
            isSelected, pending.moduleSubType,
            (moduleType, slotIndex) => OnRowSelected(dataIndex),
            (moduleType, slotIndex) => OnManageButtonClicked(moduleType, slotIndex));
    }

    // ManageButton 클릭 — 업적포인트 기반 아키타입 모듈 레벨업 팝업(M6) 오픈
    private void OnManageButtonClicked(EModuleType moduleType, int slotIndex)
    {
        DataTableModule moduleTable = DataManager.Instance.m_dataTableModule;
        ModuleData hullData = moduleTable != null ? moduleTable.GetModuleDataFromTable(m_hullSubType) : null;
        string hullArchetype = hullData != null ? hullData.hullArchetype : "";

        UIManager.Instance.ShowModuleReinforcePopup(hullArchetype, moduleType, slotIndex);
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
            // 실드/요격체는 SetupSelectedModuleVisualing 대상에서 빠져있어 하이라이트 대상 없음(둘 다 슬롯/3D 배치 없는 논리 컴포넌트)
            if (entry.moduleType != EModuleType.shield && entry.moduleType != EModuleType.interceptor)
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

    // 편집 대상 함선(FindEditingShip)의 해당 슬롯에 레벨 변경을 즉시 반영 — Confirm 전 3D 실시간 프리뷰
    // 실드는 3D 비주얼이 없는 논리 컴포넌트라(ModuleShield.cs 주석 참고) 로직 값만 갱신되고 외형 변화는 없음
    private void ApplyModulePreviewToShip(int dataIndex, ModuleInfo info)
    {
        if (dataIndex < 0 || dataIndex >= m_moduleSlotEntries.Count) return;

        SpaceShip ship = FindEditingShip();
        if (ship == null || ship.m_moduleHulls.Count == 0) return;

        ModuleHull body = ship.m_moduleHulls[0];
        ModuleSlotEntry entry = m_moduleSlotEntries[dataIndex];

        if (entry.moduleType == EModuleType.shield)
        {
            body.InitializeShield(info.moduleSubType);
            return;
        }

        if (entry.moduleType == EModuleType.interceptor)
        {
            body.InitializeInterceptor(info.moduleSubType);
            return;
        }

        body.SetModuleSlotState(entry.moduleType, entry.slotIndex, true, info);
    }

    // CANCEL 시 편집한 슬롯 전부를 원본 상태로 복원 — 파괴/재생성 없이 ModuleHull이 보관해둔 원본을 그대로 재사용
    private void RevertAllModulePreviewsToOriginal()
    {
        SpaceShip ship = FindEditingShip();
        if (ship == null || ship.m_moduleHulls.Count == 0) return;

        ModuleHull body = ship.m_moduleHulls[0];
        body.RevertAllSlotsToOriginal();

        // 실드/요격체는 슬롯 모듈이 아니라 위 복원 대상이 아님 — 편집으로 값이 바뀐 경우에만 원본으로 재초기화(안 바뀌었으면 현재 게이지/재고 유지)
        for (int i = 0; i < m_moduleSlotEntries.Count; i++)
        {
            ModuleSlotEntry entry = m_moduleSlotEntries[i];
            bool isLogicalModule = entry.moduleType == EModuleType.shield || entry.moduleType == EModuleType.interceptor;
            if (isLogicalModule == false) continue;
            if (IsSlotChangedFromOriginal(i) == false) continue;

            ModuleInfo original = TryGetOriginalModuleInfo(entry);
            string restoreSubType = original != null ? original.moduleSubType : "";
            if (entry.moduleType == EModuleType.shield)
                body.InitializeShield(restoreSubType);
            else
                body.InitializeInterceptor(restoreSubType);
        }
    }

    // 선택된 슬롯 하나(m_selectedDataIndex)의 현재 상태 vs 팬딩 상태 비교 — 함선 전체가 아니라 그 모듈 자체의 스탯만 표시
    // — InfiniteScrollView가 화면에 보이는 행만 OnStatsItemBind로 바인딩하므로 여기서는 m_statEntries만 갱신
    private void RefreshStatsDisplay()
    {
        bool hasSelection = m_selectedDataIndex >= 0 && m_selectedDataIndex < m_moduleSlotEntries.Count;

        if (hasSelection == false)
        {
            m_statEntries.Clear();
            if (m_statsScrollView != null && m_statsRowPrefab != null)
                m_statsScrollView.Initialize(0, m_statsRowPrefab.gameObject);
            return;
        }

        ModuleSlotEntry entry = m_moduleSlotEntries[m_selectedDataIndex];
        ModuleHullInfo pendingHull = BuildSingleModuleHullInfo(m_pendingModules[m_selectedDataIndex]);
        m_statEntries = ShipStatGaugeBuilder.Build(null, pendingHull, includeBodyStats: false);

        m_selectedOriginalEntriesByLabel = BuildOriginalEntriesByLabelForSelectedSlot(entry);

        if (m_statsScrollView != null && m_statsRowPrefab != null)
            m_statsScrollView.Initialize(m_statEntries.Count, m_statsRowPrefab.gameObject);
    }

    // 선택된 슬롯이 팝업을 열었던 시점(m_originalModules)에 실제로 설치돼 있던 모듈 기준 비교값 — 원래 미설치였으면 빈 딕셔너리(=0 기준 diff)
    private Dictionary<string, ShipStatRowEntry> BuildOriginalEntriesByLabelForSelectedSlot(ModuleSlotEntry entry)
    {
        Dictionary<string, ShipStatRowEntry> result = new Dictionary<string, ShipStatRowEntry>();

        ModuleInfo original = TryGetOriginalModuleInfo(entry);
        if (original == null) return result;

        ModuleHullInfo originalHull = BuildSingleModuleHullInfo(original);
        List<ShipStatRowEntry> originalEntries = ShipStatGaugeBuilder.Build(null, originalHull, includeBodyStats: false);
        for (int i = 0; i < originalEntries.Count; i++)
            result[originalEntries[i].label] = originalEntries[i];

        return result;
    }

    // 이 슬롯이 편집을 시작한 시점(m_originalModules, 서버 확정 상태)의 편집용 ModuleInfo — 미설치였으면 null. 스탯 비교 기준과 CANCEL 롤백 양쪽에서 재사용
    private ModuleInfo TryGetOriginalModuleInfo(ModuleSlotEntry entry)
    {
        if (entry.moduleType == EModuleType.shield || entry.moduleType == EModuleType.interceptor)
            return CreateEditInfoFromHull(entry.moduleType, m_originalModules);

        return FindInstalledModule(GetModulesListForType(m_originalModules, entry.moduleType), entry.slotIndex);
    }

    // 지금 pending 상태가 편집 시작 시점(원본)과 하나라도 다른 슬롯이 있는지 — CONFIRM에서 무변경이면 서버 요청/재스폰을 생략하기 위한 가드
    private bool HasPendingChanges()
    {
        for (int i = 0; i < m_moduleSlotEntries.Count; i++)
        {
            if (IsSlotChangedFromOriginal(i) == true) return true;
        }
        return false;
    }

    // dataIndex번 슬롯의 pending 상태(레벨)가 편집 시작 시점(원본)과 다른지 — pending은 항상 설치 상태이므로,
    // 원본이 애초에 미설치였다면(wasInstalled==false) 기본 레벨로 새로 설치되는 것이라 그 자체로 변경임
    private bool IsSlotChangedFromOriginal(int dataIndex)
    {
        ModuleSlotEntry entry = m_moduleSlotEntries[dataIndex];
        ModuleInfo original = TryGetOriginalModuleInfo(entry);
        bool wasInstalled = original != null;
        if (wasInstalled == false) return true;

        ModuleInfo pending = m_pendingModules[dataIndex];
        if (pending.moduleSubType != original.moduleSubType) return true;
        return false;
    }

    // 슬롯 1개짜리 ModuleHullInfo 조립 — BuildPendingModuleHullInfo와 동일 패턴을 단일 모듈에 적용
    private ModuleHullInfo BuildSingleModuleHullInfo(ModuleInfo info)
    {
        ModuleHullInfo hull = new ModuleHullInfo { beams = new List<ModuleInfo>(), missiles = new List<ModuleInfo>(), hangars = new List<ModuleInfo>(), shieldModuleSubType = "" };
        ApplyEditInfoToHull(hull, info);
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

    // 현재 로컬 편집 상태(m_pendingModules, 전 슬롯 설치 상태)를 ModuleHullInfo로 구성 — OnConfirmClicked의 desired 구성과 동일 로직
    private ModuleHullInfo BuildPendingModuleHullInfo()
    {
        ModuleHullInfo pending = new ModuleHullInfo { beams = new List<ModuleInfo>(), missiles = new List<ModuleInfo>(), hangars = new List<ModuleInfo>(), shieldModuleSubType = "" };
        for (int i = 0; i < m_moduleSlotEntries.Count; i++)
        {
            // 모든 슬롯은 항상 설치된 상태로 커밋 — 강화 팝업에서 레벨업/다운된 실제 서브타입을 그대로 사용(기본값을 쓰면 항상 레벨1로 되돌아감)
            ApplyEditInfoToHull(pending, m_pendingModules[i]);
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

    // 선택 후보를 실제로 적용했다고 가정했을 때의 지휘력 사용량을 미리 계산 — UIHullPickerView.RefreshCommandPowerPreview와 동일한 표시 방식
    private void RefreshCommandPowerPreview(FleetComposition composition)
    {
        if (m_commandPowerRow == null) return;

        int usedByOtherSlots = composition.GetUsedCommandPower() - composition.GetSlotCommandCost(m_slotIndex);
        int pendingSlotCost = ComputePendingSlotCost();
        int projectedUsed = usedByOtherSlots + pendingSlotCost;
        int max = composition.GetMaxCommandPower();
        bool isOverCommandPower = projectedUsed > max;

        string projectedUsedText = CommonUtility.FormatNumber(projectedUsed);
        string maxText = CommonUtility.FormatNumber(max);
        m_commandPowerRow.SetRow("UI_CommandPower", $"{projectedUsedText} / {maxText}", rawValue: true);
        m_commandPowerRow.SetValueColor(CommonUtility.PaletteColor(isOverCommandPower == true ? "Text.Warning" : "Text.Dark1"));
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_commandPowerRow.transform as RectTransform);

        if (m_confirmButton != null)
            m_confirmButton.interactable = isOverCommandPower == false;
    }

    // 바디(hull) 자신의 설치비만 지휘력(CP)에 반영 — 모듈 설치/해제는 캐파와 무관(M13, 서버 FleetService.computeShipCommandCost와 동일 계산)
    private int ComputePendingSlotCost()
    {
        DataTableModule moduleTable = DataManager.Instance.m_dataTableModule;
        if (moduleTable == null) return 0;

        ModuleData bodyData = moduleTable.GetModuleDataFromTable(m_hullSubType);
        return bodyData != null ? bodyData.statPoint : 0;
    }

    // 새로 장착하는 빈 슬롯(기존 장착 이력 없음)의 초기 서브타입 — 레벨은 함체와 독립적인 별도 축이라 기본값은 항상 레벨1
    private string GetDefaultSubType(EModuleType moduleType)
    {
        if (moduleType == EModuleType.beam) return "beam_1_1";
        if (moduleType == EModuleType.missile) return "missile_1_1";
        if (moduleType == EModuleType.hangar) return "hangar_1_1";
        if (moduleType == EModuleType.shield) return "shield_1_1";
        if (moduleType == EModuleType.interceptor) return "interceptor_1_1";
        return "";
    }

    // 로컬 편집 상태(m_pendingModules) 전체를 최종 장착 구성으로 한 번에 서버에 전송 — 낱개로 순서대로 보내면
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

        // 지크프리트 함대(서버 미등록)는 SetModule을 호출해도 서버가 모르는 슬롯이라 항상 실패함 — 로컬 FleetComposition에 바로 반영
        if (ObjectManager.Instance.IsSiegfriedFleetActive() == true)
        {
            FleetComposition tutorialComposition = DataManager.Instance.m_currentFleetComposition;
            if (tutorialComposition != null)
                tutorialComposition.ApplyModuleToggleResult(m_slotIndex, desired);

            if (m_onChanged != null) m_onChanged();
            Close();
            return;
        }

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
                NetworkManager.Instance.ShowRequestFailedPopup(response.errorCode); // 화면은 그대로 유지 — 다시 Confirm하거나 Cancel로 원본 복원
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
