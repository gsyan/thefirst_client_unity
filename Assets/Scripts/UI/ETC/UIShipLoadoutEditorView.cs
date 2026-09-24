// 함선 로드아웃 편집 화면 — UIHullPickerView가 "바디(함체) 선택"을 담당한다면, 이 컴포넌트는 그 다음 단계인
// "슬롯별 모듈 on/off 편집"을 담당한다. 토글은 전부 로컬에서만 미리보기 상태로 바뀌고, Confirm을 눌러야 서버에 실제로 반영됨
// (UIHullPickerView와 동일 패턴 — 예산 초과 상태에선 Confirm 버튼 비활성화)
// 카테고리별 최대 슬롯 수는 FleetComposition.ParseMaxSlotsFromHullSubType으로 hullSubType에서 파싱(빔/미사일/격납고/실드/요격체 —
// 실드는 2세대(gen=2) 함체부터 1개 지원, 요격체는 현재 어떤 함체 데이터에도 슬롯이 없어 항상 0)
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
    // m_moduleSlotEntries와 1:1 대응 — 슬롯의 현재 편집 상태(서브타입 + 모든 강화 포인트). 미장착 슬롯도 기본 서브타입 + 포인트 0으로 유지
    // 실드/요격체는 ModuleHullInfo 필드가 아니라 이 형태로 편집: 실드 attackPoints=게이지/attackToFighterPoints=회복속도, 요격체 attackPoints=회복속도 (ApplyEditInfoToHull/CreateEditInfoFromHull로 변환)
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

        // 모듈 티어업 튜토리얼이 "후보 모듈 행의 관리 버튼"과 확인 버튼을 가리킬 수 있도록 리졸버 등록
        m_tierUpModuleManageButtonResolver = ResolveTierUpModuleManageButton;
        m_confirmButtonResolver = ResolveConfirmButton;
        TutorialManager.Instance.RegisterDynamicTarget(TutorialManager.DYNAMIC_TARGET_TIER_UP_MODULE_MANAGE_BUTTON, m_tierUpModuleManageButtonResolver);
        TutorialManager.Instance.RegisterDynamicTarget(TutorialManager.DYNAMIC_TARGET_LOADOUT_CONFIRM_BUTTON, m_confirmButtonResolver);

        gameObject.SetActive(false);
    }

    private System.Func<RectTransform> m_tierUpModuleManageButtonResolver;
    private System.Func<RectTransform> m_confirmButtonResolver;

    private void OnDestroy()
    {
        TutorialManager tutorialManager = TutorialManager.Instance; // 종료 중이면 null
        if (tutorialManager == null) return;

        tutorialManager.UnregisterDynamicTarget(TutorialManager.DYNAMIC_TARGET_TIER_UP_MODULE_MANAGE_BUTTON, m_tierUpModuleManageButtonResolver);
        tutorialManager.UnregisterDynamicTarget(TutorialManager.DYNAMIC_TARGET_LOADOUT_CONFIRM_BUTTON, m_confirmButtonResolver);
    }

    // 지금 열려 있는 함선이 티어업 후보 함선일 때, 후보 모듈 행의 관리 버튼 — 그 행이 보이도록 먼저 스크롤한 뒤 찾음
    private RectTransform ResolveTierUpModuleManageButton()
    {
        if (m_moduleScrollView == null) return null;
        if (TutorialManager.Instance.TryGetModuleTierUpCandidate(out int shipSlotIndex, out EModuleType moduleType, out int categorySlotIndex) == false) return null;
        if (shipSlotIndex != m_slotIndex) return null;

        int candidateDataIndex = FindDataIndex(moduleType, categorySlotIndex);
        if (candidateDataIndex < 0) return null;

        m_moduleScrollView.EnsureVisible(candidateDataIndex);

        RectTransform manageButtonRect = null;
        m_moduleScrollView.ForEachVisibleItem((dataIndex, rowObject) =>
        {
            if (dataIndex != candidateDataIndex) return;

            UIModuleSlotToggleRow row = rowObject.GetComponent<UIModuleSlotToggleRow>();
            if (row != null) manageButtonRect = row.GetManageButtonRect();
        });
        return manageButtonRect;
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

    // 장착된 슬롯의 모듈 아이콘 클릭 — 슬롯을 선택한 뒤 강화 팝업까지 바로 띄움(수동으로 행 선택 후 관리 버튼을 누른 것과 동일 흐름)
    public void OpenAndManageSlot(int slotIndex, EModuleType moduleType, int categorySlotIndex, System.Action onChanged)
    {
        int dataIndex = OpenAndSelectSlot(slotIndex, moduleType, categorySlotIndex, onChanged);
        if (dataIndex < 0) return;

        OnManageButtonClicked(dataIndex);
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
        m_pendingModules.Clear();
        // 슬롯 잠금 없음 — 모든 슬롯이 자유롭게 토글 가능. 공격 모듈 0개 방지는 Confirm 버튼 비활성화(RefreshCommandPowerPreview)로 처리
        AppendCategorySlots(EModuleType.beam, maxSlots[0]);
        AppendCategorySlots(EModuleType.missile, maxSlots[1]);
        AppendCategorySlots(EModuleType.hangar, maxSlots[2]);
        AppendShieldSlot(maxSlots[3]);
        AppendInterceptorSlot(maxSlots[4]);

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
            m_moduleSlotEntries.Add(new ModuleSlotEntry(moduleType, i, isLocked: false));
            m_pendingInstalled.Add(IsSlotInstalled(installedList, i));

            ModuleInfo installedModule = FindInstalledModule(installedList, i);
            ModuleInfo editInfo = installedModule != null ? FleetComposition.CloneModuleInfo(installedModule) : new ModuleInfo { moduleType = moduleType, slotIndex = i };
            if (string.IsNullOrEmpty(editInfo.moduleSubType) == true) editInfo.moduleSubType = GetDefaultSubType(moduleType);
            m_pendingModules.Add(editInfo);
        }
    }

    // 실드는 슬롯이 없어(문자열 장착 여부만 존재) 리스트 카테고리와 별도 처리 — 함체에 실드 슬롯이 있을 때만(maxSlotCount>0) 행 1개 추가
    private void AppendShieldSlot(int maxSlotCount)
    {
        if (maxSlotCount <= 0) return;

        bool isInstalled = m_originalModules != null && string.IsNullOrEmpty(m_originalModules.shieldModuleSubType) == false;
        m_moduleSlotEntries.Add(new ModuleSlotEntry(EModuleType.shield, 0, isLocked: false));
        m_pendingInstalled.Add(isInstalled);
        // 이미 장착돼 있으면 실제 티어(예: shield_5_1)로 시드해야 강화 팝업이 그 티어부터 시작함 — 미설치면 기본 티어1
        ModuleInfo editInfo = CreateEditInfoFromHull(EModuleType.shield, m_originalModules);
        if (editInfo == null) editInfo = new ModuleInfo { moduleType = EModuleType.shield, slotIndex = 0, moduleSubType = GetDefaultSubType(EModuleType.shield) };
        m_pendingModules.Add(editInfo);
    }

    // 요격체는 슬롯이 없어(문자열 장착 여부만 존재) 리스트 카테고리와 별도 처리 — 함체에 요격체 슬롯이 있을 때만(maxSlotCount>0) 행 1개 추가
    private void AppendInterceptorSlot(int maxSlotCount)
    {
        if (maxSlotCount <= 0) return;

        bool isInstalled = m_originalModules != null && string.IsNullOrEmpty(m_originalModules.interceptorModuleSubType) == false;
        m_moduleSlotEntries.Add(new ModuleSlotEntry(EModuleType.interceptor, 0, isLocked: false));
        m_pendingInstalled.Add(isInstalled);
        // 이미 장착돼 있으면 실제 티어로 시드해야 강화 팝업이 그 티어부터 시작함 — 미설치면 기본 티어1
        ModuleInfo editInfo = CreateEditInfoFromHull(EModuleType.interceptor, m_originalModules);
        if (editInfo == null) editInfo = new ModuleInfo { moduleType = EModuleType.interceptor, slotIndex = 0, moduleSubType = GetDefaultSubType(EModuleType.interceptor) };
        m_pendingModules.Add(editInfo);
    }

    // 실드/요격체 ModuleHullInfo 필드 → 편집용 ModuleInfo (미장착이면 null). 실드 attackPoints=게이지/attackToFighterPoints=회복속도, 요격체 attackPoints=회복속도
    private ModuleInfo CreateEditInfoFromHull(EModuleType moduleType, ModuleHullInfo hull)
    {
        if (hull == null) return null;

        if (moduleType == EModuleType.shield && string.IsNullOrEmpty(hull.shieldModuleSubType) == false)
        {
            return new ModuleInfo { moduleType = moduleType, slotIndex = 0, moduleSubType = hull.shieldModuleSubType,
                attackPoints = hull.shieldGaugePoints, attackToFighterPoints = hull.shieldRegenRatePoints };
        }

        if (moduleType == EModuleType.interceptor && string.IsNullOrEmpty(hull.interceptorModuleSubType) == false)
        {
            return new ModuleInfo { moduleType = moduleType, slotIndex = 0, moduleSubType = hull.interceptorModuleSubType,
                attackPoints = hull.interceptorRegenRatePoints };
        }

        return null;
    }

    // 편집용 ModuleInfo → ModuleHullInfo에 반영 (CreateEditInfoFromHull의 역변환) — 빔/미사일/격납고는 해당 리스트에 추가
    private void ApplyEditInfoToHull(ModuleHullInfo hull, ModuleInfo info)
    {
        if (info.moduleType == EModuleType.shield)
        {
            hull.shieldModuleSubType = info.moduleSubType;
            hull.shieldGaugePoints = info.attackPoints;
            hull.shieldRegenRatePoints = info.attackToFighterPoints;
            return;
        }

        if (info.moduleType == EModuleType.interceptor)
        {
            hull.interceptorModuleSubType = info.moduleSubType;
            hull.interceptorRegenRatePoints = info.attackPoints;
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

    // InfiniteScrollView가 dataIndex번 슬롯 행을 화면에 배치할 때마다 호출 — 토글하면 서버 호출 없이 m_pendingInstalled만 갱신
    private void OnModuleSlotItemBind(int dataIndex, GameObject rowObject)
    {
        if (dataIndex < 0 || dataIndex >= m_moduleSlotEntries.Count) return;

        UIModuleSlotToggleRow row = rowObject.GetComponent<UIModuleSlotToggleRow>();
        if (row == null) return;

        ModuleSlotEntry entry = m_moduleSlotEntries[dataIndex];
        // 강화 포인트뿐 아니라 현재 티어의 설치비(statPoint)도 이 슬롯이 쓰는 지휘력이므로 합산해서 표시
        ModuleInfo pending = m_pendingModules[dataIndex];
        int tierCost = GetModuleInstallCostBySubType(DataManager.Instance.m_dataTableModule, pending.moduleSubType);
        int reinforceCost = FleetComposition.GetReinforceCpCostPerPoint() * FleetComposition.SumReinforcePoints(pending);
        int investedCommandPower = tierCost + reinforceCost;
        bool isSelected = dataIndex == m_selectedDataIndex;

        row.Setup(entry.moduleType, entry.slotIndex, m_pendingInstalled[dataIndex], entry.isLocked,
            investedCommandPower, isSelected, pending.moduleSubType,
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

    private void OnLocalToggleChanged(int dataIndex, bool install)
    {
        if (dataIndex < 0 || dataIndex >= m_pendingInstalled.Count) return;
        m_pendingInstalled[dataIndex] = install;
        if (install == false)
            ClearReinforcePoints(m_pendingModules[dataIndex]);

        ApplyModulePreviewToShip(dataIndex, m_pendingModules[dataIndex], install);

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
    private void ClearReinforcePoints(ModuleInfo info)
    {
        info.attackPoints = 0;
        info.attackToFighterPoints = 0;
        info.fireRatePoints = 0;
        info.silencePoints = 0;
        info.ammoPoints = 0;
        info.healthPoints = 0;
        info.disruptPoints = 0;
    }

    private void ApplyModulePreviewToShip(int dataIndex, ModuleInfo info, bool installed)
    {
        if (dataIndex < 0 || dataIndex >= m_moduleSlotEntries.Count) return;

        SpaceShip ship = FindEditingShip();
        if (ship == null || ship.m_moduleHulls.Count == 0) return;

        ModuleHull body = ship.m_moduleHulls[0];
        ModuleSlotEntry entry = m_moduleSlotEntries[dataIndex];

        if (entry.moduleType == EModuleType.shield)
        {
            body.InitializeShield(installed == true ? info.moduleSubType : "", info.attackPoints, info.attackToFighterPoints);
            return;
        }

        if (entry.moduleType == EModuleType.interceptor)
        {
            body.InitializeInterceptor(installed == true ? info.moduleSubType : "", info.attackPoints);
            return;
        }

        body.SetModuleSlotState(entry.moduleType, entry.slotIndex, installed, info);
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
            int originalAttackPoints = original != null ? original.attackPoints : 0;
            int originalSecondaryPoints = original != null ? original.attackToFighterPoints : 0;
            if (entry.moduleType == EModuleType.shield)
                body.InitializeShield(restoreSubType, originalAttackPoints, originalSecondaryPoints);
            else
                body.InitializeInterceptor(restoreSubType, originalAttackPoints);
        }
    }

    // ManageButton 클릭 — 강화 포인트 편집 팝업 오픈. 현재 pending 값을 팝업의 초기 로컬 버퍼로 전달
    private void OnManageButtonClicked(int dataIndex)
    {
        if (dataIndex < 0 || dataIndex >= m_moduleSlotEntries.Count) return;

        OnRowSelected(dataIndex); // 관리 버튼을 누른 슬롯을 자동으로 선택 상태로 만듦

        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition == null) return;

        ModuleSlotEntry entry = m_moduleSlotEntries[dataIndex];
        DataTableModule moduleTable = DataManager.Instance.m_dataTableModule;
        ModuleInfo pending = m_pendingModules[dataIndex];
        int installCost = GetModuleInstallCostBySubType(moduleTable, pending.moduleSubType);

        int thisSlotReinforceCost = FleetComposition.GetReinforceCpCostPerPoint() * FleetComposition.SumReinforcePoints(pending);
        int usedByOtherSlots = composition.GetUsedCommandPower() - composition.GetSlotCommandCost(m_slotIndex)
            + (ComputePendingSlotCost() - installCost - thisSlotReinforceCost);
        int maxCommandPower = composition.GetMaxCommandPower();

        int hullTier = CommonUtility.ParseTier(m_hullSubType);
        UIManager.Instance.ShowModuleReinforcePopup(entry.moduleType, pending,
            maxCommandPower, usedByOtherSlots, installCost, hullTier,
            confirmedInfo => OnReinforceConfirmed(dataIndex, confirmedInfo));
    }

    private void OnReinforceConfirmed(int dataIndex, ModuleInfo confirmedInfo)
    {
        if (dataIndex < 0 || dataIndex >= m_pendingModules.Count) return;
        m_pendingModules[dataIndex] = confirmedInfo;

        ApplyModulePreviewToShip(dataIndex, confirmedInfo, installed: true);

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

    // dataIndex번 슬롯의 pending 상태(장착 여부/티어/강화 포인트)가 편집 시작 시점(원본)과 다른지
    private bool IsSlotChangedFromOriginal(int dataIndex)
    {
        ModuleSlotEntry entry = m_moduleSlotEntries[dataIndex];
        ModuleInfo original = TryGetOriginalModuleInfo(entry);
        bool wasInstalled = original != null;

        if (m_pendingInstalled[dataIndex] != wasInstalled) return true;
        if (wasInstalled == false) return false; // 둘 다 미설치면 이 슬롯은 비교할 게 없음

        ModuleInfo pending = m_pendingModules[dataIndex];
        if (pending.moduleSubType != original.moduleSubType) return true;
        if (FleetComposition.AreSameReinforcePoints(pending, original) == false) return true;
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

    // 현재 로컬 토글 상태(m_pendingInstalled)를 ModuleHullInfo로 구성 — OnConfirmClicked의 desired 구성과 동일 로직
    private ModuleHullInfo BuildPendingModuleHullInfo()
    {
        ModuleHullInfo pending = new ModuleHullInfo { beams = new List<ModuleInfo>(), missiles = new List<ModuleInfo>(), hangars = new List<ModuleInfo>(), shieldModuleSubType = "" };
        for (int i = 0; i < m_moduleSlotEntries.Count; i++)
        {
            if (m_pendingInstalled[i] == false) continue;

            // 강화 팝업에서 티어업/다운된 실제 서브타입과 포인트를 그대로 커밋 — 기본 서브타입 고정값을 쓰면 항상 티어1로 되돌아감
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

        m_commandPowerRow.SetRow("UI_CommandPower", $"{projectedUsed} / {max}", rawValue: true);
        m_commandPowerRow.SetValueColor(CommonUtility.PaletteColor(isOverCommandPower == true ? "Text.Warning" : "Text.Dark1"));
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_commandPowerRow.transform as RectTransform);

        if (m_confirmButton != null)
            m_confirmButton.interactable = isOverCommandPower == false;
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
            int installCost = GetModuleInstallCostBySubType(moduleTable, m_pendingModules[i].moduleSubType);
            int reinforceCost = FleetComposition.GetReinforceCpCostPerPoint() * FleetComposition.SumReinforcePoints(m_pendingModules[i]);
            modulesCost += installCost + reinforceCost;
        }

        return bodyCost + modulesCost;
    }

    // 새로 장착하는 빈 슬롯(기존 장착 이력 없음)의 초기 서브타입 — 무기 티어는 함체와 독립적인 별도 축이라 기본값은 항상 1티어.
    // 장착 후에는 UIPopupModuleReinforce의 티어업/다운으로 m_pendingModules의 서브타입이 바뀌므로 이 값은 시작점일 뿐임(실드는 티어 없이 항상 이 값 고정)
    private string GetDefaultSubType(EModuleType moduleType)
    {
        if (moduleType == EModuleType.beam) return "beam_1_1";
        if (moduleType == EModuleType.missile) return "missile_1_1";
        if (moduleType == EModuleType.hangar) return "hangar_1_1";
        if (moduleType == EModuleType.shield) return "shield_1_1";
        if (moduleType == EModuleType.interceptor) return "interceptor_1_1";
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
