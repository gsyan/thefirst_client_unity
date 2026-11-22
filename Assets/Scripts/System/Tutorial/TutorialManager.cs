using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

// 튜토리얼 시스템 관리자
public class TutorialManager : MonoSingleton<TutorialManager>
{
    private const string TUTORIAL_UI_PATH = "Prefabs/UI/Tutorial/UITutorial";
    private const string PROGRESS_CATEGORY = "tutorial";

    // Branch 스텝의 targetUIId에 이 값을 쓰면 점프 대신 튜토리얼을 완료 처리
    public const string BRANCH_JUMP_END = "END";

    // 튜토리얼 스텝의 targetUIId가 '@'로 시작하면 하이라키 경로 대신 이 매니저에 등록된 리졸버가 그때그때 대상을 계산해 돌려줌 —
    // 데이터(잠금 여부 등)에 따라 대상 행이 달라지는 스텝용. 예: 언락 안 된 최저 티어 함체의 언락 버튼
    public const string DYNAMIC_TARGET_LOWEST_LOCKED_HULL_UNLOCK_BUTTON = "@LowestLockedHullUnlockButton";

    // 함선 언락/배치해제/배치 안내 — 함대 패널을 열었을 때 CanStartHullUnlockTutorial() 조건을 만족하면 시작
    public const string HULL_UNLOCK_TUTORIAL_ID = "Tutorial_HullUnlock";
    // 모듈 강화 안내 — 함대 패널을 열었을 때 CanStartModuleReinforceTutorial() 조건(업적포인트로 강화 가능한 항목 존재)을 만족하면 시작
    public const string MODULE_REINFORCE_TUTORIAL_ID = "Tutorial_ModuleReinforce";
    public const string DYNAMIC_TARGET_REINFORCE_SHIP_MANAGE_BUTTON = "@ReinforceShipManageButton";       // UIPanelFleet — 강화 대상 함선 행의 관리 버튼
    public const string DYNAMIC_TARGET_REINFORCE_MODULE_MANAGE_BUTTON = "@ReinforceModuleManageButton";   // UIShipLoadoutEditorView — 강화 대상 모듈 행의 관리 버튼
    public const string DYNAMIC_TARGET_REINFORCE_LEVEL_UP_BUTTON = "@ReinforceLevelUpButton";             // UIPopupModuleReinforce — 강화 버튼
    public const string DYNAMIC_TARGET_REINFORCE_CLOSE_BUTTON = "@ReinforceCloseButton";                  // UIPopupModuleReinforce — 닫기 버튼
    public const string DYNAMIC_TARGET_HULL_UNLOCK_SLOT_SELECT_BUTTON = "@HullUnlockSlotSelectButton"; // UIPanelFleet — 튜토리얼 대상 슬롯의 함체 선택 버튼
    public const string DYNAMIC_TARGET_UNPLACE_CURRENT_HULL_BUTTON = "@UnplaceCurrentHullButton";   // UIHullPickerView — 슬롯에 배치된 함체를 선택해 배치해제 버튼을 가리킴
    public const string DYNAMIC_TARGET_PLACE_UNLOCKED_HULL_BUTTON = "@PlaceUnlockedHullButton";     // UIHullPickerView — 언락된 최저 티어 함체를 선택해 배치 버튼을 가리킴

    public const string DYNAMIC_TARGET_LOADOUT_CONFIRM_BUTTON = "@LoadoutConfirmButton";           // UIShipLoadoutEditorView — 확인 버튼

    private TutorialData m_currentTutorial;
    private int m_currentStepIndex;
    private bool m_isPlaying;
    private bool m_isServerLoaded;
    private int m_hullUnlockSlotIndex;
    private int m_reinforceFleetSlotIndex;
    private EModuleType m_reinforceModuleType;
    private int m_reinforceCategorySlotIndex;
    private TutorialUI m_tutorialUI;
    private HashSet<string> m_completedTutorials = new HashSet<string>();
    private System.Action<string> m_onCompleteCallback;

    // Tutorial 조건용 상태
    private Coroutine m_customConditionCoroutine;
    private HashSet<int> m_selectedModuleIds = new HashSet<int>();
    private float m_cameraRotationAccumulated;
    private Quaternion m_lastCameraRotation;
    private float m_cameraZoomAccumulated;
    private float m_lastCameraZoom;
    private SpaceShip m_pendingNewShip; // ShipArrivedAtFormation 조건이 대기할 함선 (UITabFleet에서 함선 생성 직후 등록)
    private System.Action m_zoneBattleEndHandler; // WaitForZoneBattleEnd 조건용(그리드 복귀 시점 대기) — StopTutorialCondition에서 해제
    // UI 상호작용 잠금 해제 전용 — 스텝 전환(ZoneCellReturnedToGrid, 보상카드 팝업까지 처리된 뒤)보다 먼저, 실제 전투가
    // 끝나는 즉시(ZoneStageBattleEnd) 풀어야 함. 안 그러면 보상카드 팝업 처리하는 동안 UIPanelBattle이 계속 잠긴 채로
    // 남아있다가(그 시점엔 이미 안 보여서 못 느끼지만) 다음 실전투 때도 잠긴 채로 다시 나타나는 문제가 있었음
    private System.Action<bool> m_zoneBattleEndUnlockHandler;
    private System.Action<GridCell3D> m_gridCellClickedHandler; // WaitForGridCellClicked 조건용 — StopTutorialCondition에서 해제
    private System.Action<bool> m_rewardCardDefeatHandler; // WaitForRewardCardPopup 조건용(패배 시 튜토리얼 종료 + UI 잠금 해제) — StopTutorialCondition에서 해제
    private int m_rewardCardPopupWaitStepIndex = -1; // WaitForRewardCardPopup을 기다리는 스텝 인덱스, 대기 중이 아니면 -1

    // Branch 스텝 조건 평가자 — 해당 UI 컴포넌트가 Awake에서 등록, OnDestroy에서 해제(로그아웃 초기화 대상 아님)
    private readonly Dictionary<ETutorialConditionType, System.Func<bool>> m_branchConditionEvaluators = new Dictionary<ETutorialConditionType, System.Func<bool>>();

    // '@' 동적 타겟 리졸버 — 해당 UI 컴포넌트가 Awake에서 등록, OnDestroy에서 해제(로그아웃 초기화 대상 아님)
    private readonly Dictionary<string, System.Func<RectTransform>> m_dynamicTargetResolvers = new Dictionary<string, System.Func<RectTransform>>();

    // 패널 이름 → 그 패널을 처음 열 때 시작할 튜토리얼 id
    private readonly Dictionary<string, string[]> m_panelFirstOpenTutorialIds = new Dictionary<string, string[]>
    {
        { "UIPanelCommander", new string[] { "Tutorial_CommanderPanel" } },
        { "UIPanelRank", new string[] { "Tutorial_RankPanel" } },
        { "UIPanelExplorationGrid", new string[] { "Tutorial_Exploration" } },
        { "UIPanelFleet", new string[] { MODULE_REINFORCE_TUTORIAL_ID, HULL_UNLOCK_TUTORIAL_ID, "Tutorial_Tactics" } }, // 조건 없는 설명형(Tactics)은 항상 마지막 — 조건형이 먼저 검사되도록
    };

    // 튜토리얼 완료 이벤트 (tutorialId 전달)
    public event System.Action<string> OnTutorialCompleted;

    protected override bool ShouldDontDestroyOnLoad => true;

    protected override void OnInitialize()
    {
        EventManager.Subscribe_ConsumeAnyClick(ConsumeAnyClick);
        EventManager.Subscribe_CurrentPanelChanged(OnCurrentPanelChanged);
    }

    // 로그아웃 시 호출 — TutorialManager는 DontDestroyOnLoad라 로그아웃해도 인스턴스가 안 죽어서
    // OnInitialize()가 다시 실행되지 않음. EventManager.UnsubscribeAll()이 지운 구독을 복구하고,
    // 이전 세션(진행 중이던 튜토리얼 스텝/코루틴/조건 상태)이 다음 로그인으로 새어 들어가지 않도록 초기화
    public void ResetForLogout()
    {
        StopTutorialCondition();

        m_currentTutorial = null;
        m_currentStepIndex = 0;
        m_isPlaying = false;
        m_onCompleteCallback = null;
        m_pendingNewShip = null;
        m_lastCameraRotation = default;
        m_lastCameraZoom = 0f;

        if (m_tutorialUI != null)
        {
            m_tutorialUI.HideTutorialUI();
            m_tutorialUI = null;
        }

        m_completedTutorials.Clear();
        m_isServerLoaded = false;

        // EventManager.UnsubscribeAll()로 지워진 이 매니저 자신의 구독 복구 —
        // 먼저 해제 후 재구독해야 호출 시점과 무관하게 항상 구독이 정확히 1개만 남음(중복 구독 방지)
        EventManager.Unsubscribe_ConsumeAnyClick(ConsumeAnyClick);
        EventManager.Subscribe_ConsumeAnyClick(ConsumeAnyClick);
        EventManager.Unsubscribe_CurrentPanelChanged(OnCurrentPanelChanged);
        EventManager.Subscribe_CurrentPanelChanged(OnCurrentPanelChanged);
    }

    // SelectCommander 응답에 이미 포함된 진행도를 그대로 주입 — SpaceScene 진입 전 미리 확보 가능(별도 네트워크 호출 불필요)
    public void ApplyProgressList(List<ProgressInfo> progressList)
    {
        m_completedTutorials.Clear();
        if (progressList != null)
        {
            foreach (var progress in progressList)
                m_completedTutorials.Add(progress.key);
        }
        m_isServerLoaded = true;
    }

    // 서버에서 진행도 로드 (로그인 후 호출) — ApplyProgressList로 이미 로드됐으면 재호출 안 함
    public async Task LoadProgressFromServerAsync()
    {
        if (m_isServerLoaded) return;

        try
        {
            var apiClient = NetworkManager.Instance?.GetApiClient();
            if (apiClient == null) return;

            var response = await apiClient.GetProgressListAsync(PROGRESS_CATEGORY);
            if (response.errorCode != 0) return;

            m_completedTutorials.Clear();

            if (response.data?.progressList != null)
            {
                foreach (var progress in response.data.progressList)
                {
                    m_completedTutorials.Add(progress.key);
                }
            }

            m_isServerLoaded = true;
            Debug.Log($"[Tutorial] 서버에서 {m_completedTutorials.Count}개 완료된 튜토리얼 로드됨");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Tutorial] 서버 로드 실패: {e.Message}");
        }
    }

    // 해당 조건을 소유한 UI가 Awake에서 등록/OnDestroy에서 해제 — 등록된 평가자가 없으면 거짓으로 처리(직선 진행)
    public void RegisterBranchCondition(ETutorialConditionType conditionType, System.Func<bool> evaluator)
    {
        m_branchConditionEvaluators[conditionType] = evaluator;
    }

    public void UnregisterBranchCondition(ETutorialConditionType conditionType, System.Func<bool> evaluator)
    {
        if (m_branchConditionEvaluators.TryGetValue(conditionType, out System.Func<bool> registered) == false) return;
        if (registered != evaluator) return;

        m_branchConditionEvaluators.Remove(conditionType);
    }

    private bool EvaluateBranchCondition(ETutorialConditionType conditionType)
    {
        if (conditionType == ETutorialConditionType.Always) return true;
        if (conditionType == ETutorialConditionType.ExplorationRunActive) return ObjectManager.Instance.IsExplorationRunActive();
        if (m_branchConditionEvaluators.TryGetValue(conditionType, out System.Func<bool> evaluator) == false) return false;

        return evaluator();
    }

    public void RegisterDynamicTarget(string key, System.Func<RectTransform> resolver)
    {
        m_dynamicTargetResolvers[key] = resolver;
    }

    // 등록한 리졸버가 그대로 남아있을 때만 해제 — 새 인스턴스가 이미 같은 키를 덮어썼다면 건드리지 않음
    public void UnregisterDynamicTarget(string key, System.Func<RectTransform> resolver)
    {
        if (m_dynamicTargetResolvers.TryGetValue(key, out System.Func<RectTransform> registered) == false) return;
        if (registered != resolver) return;

        m_dynamicTargetResolvers.Remove(key);
    }

    public RectTransform ResolveDynamicTarget(string key)
    {
        if (m_dynamicTargetResolvers.TryGetValue(key, out System.Func<RectTransform> resolver) == false) return null;
        return resolver();
    }

    // 함선 언락 안내 시작 조건 — 아직 직접 언락한 함체가 없고, 함선이 배치된 슬롯 중 하나를 비웠다고 가정했을 때
    // 지휘력·업적포인트 모두 감당되는 언락 가능 함체(레벨1 행 기준)가 있을 때. 만족하는 첫 슬롯을 튜토리얼 대상 슬롯으로 기록
    private bool CanStartHullUnlockTutorial()
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (commander == null || composition == null) return false;
        if (commander.HasAnyUnlockedHull() == true) return false;
        if (ObjectManager.Instance.IsExplorationRunActive() == true) return false; // 배치해제 단계가 있어 존런 중엔 시작 불가(교체/해제 금지) — 런 밖에서 패널을 다시 열 때 시작

        int achievementPoint = commander.GetAchievementPoint();
        int maxCommandPower = composition.GetMaxCommandPower();
        int usedCommandPower = composition.GetUsedCommandPower();
        List<FleetSlotEntry> placedShips = composition.GetPlacedShips();
        List<ModuleData> hulls = DataManager.Instance.m_dataTableModule.HullModules.modules;

        for (int slotIndex = 0; slotIndex < placedShips.Count; slotIndex++)
        {
            if (string.IsNullOrEmpty(placedShips[slotIndex].hullSubType) == true) continue;

            int usedCommandPowerExcludingSlot = usedCommandPower - composition.GetSlotCommandCost(slotIndex);
            for (int i = 0; i < hulls.Count; i++)
            {
                ModuleData hull = hulls[i];
                if (hull.isEnemyOnly == true) continue;
                if (hull.moduleSubType.EndsWith("_1") == false) continue;
                if (CommonUtility.ParseTier(hull.moduleSubType) <= 1) continue;

                bool isAffordableByAchievementPoint = hull.unlockAchievementPointCost <= achievementPoint;
                bool isAffordableByCommandPower = usedCommandPowerExcludingSlot + hull.statPoint <= maxCommandPower;
                if (isAffordableByAchievementPoint == false || isAffordableByCommandPower == false) continue;

                m_hullUnlockSlotIndex = slotIndex;
                return true;
            }
        }

        return false;
    }

    // 모듈 강화 안내 시작 조건 — 배치된 함선들의 강화 항목(함체/빔/미사일/격납고/해금된 실드·요격체) 중
    // 다음 레벨 비용을 보유 업적포인트로 감당할 수 있는 항목이 있을 때. 그중 가장 싼 항목을 튜토리얼 대상으로 기록
    private bool CanStartModuleReinforceTutorial()
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (commander == null || composition == null) return false;

        int achievementPoint = commander.GetAchievementPoint();
        int cheapestCost = int.MaxValue;
        List<FleetSlotEntry> placedShips = composition.GetPlacedShips();

        for (int slotIndex = 0; slotIndex < placedShips.Count; slotIndex++)
        {
            string hullSubType = placedShips[slotIndex].hullSubType;
            if (string.IsNullOrEmpty(hullSubType) == true) continue;

            ModuleData hullData = DataManager.Instance.m_dataTableModule.GetModuleDataFromTable(hullSubType);
            if (hullData == null || string.IsNullOrEmpty(hullData.hullArchetype) == true) continue;

            int[] maxSlots = FleetComposition.ParseMaxSlotsFromHullSubType(hullSubType); // [beam, missile, hangar, shield, interceptor]
            TryPickCheaperReinforceTarget(slotIndex, hullSubType, hullData.hullArchetype, EModuleType.hull, 1, achievementPoint, ref cheapestCost);
            TryPickCheaperReinforceTarget(slotIndex, hullSubType, hullData.hullArchetype, EModuleType.beam, maxSlots[0], achievementPoint, ref cheapestCost);
            TryPickCheaperReinforceTarget(slotIndex, hullSubType, hullData.hullArchetype, EModuleType.missile, maxSlots[1], achievementPoint, ref cheapestCost);
            TryPickCheaperReinforceTarget(slotIndex, hullSubType, hullData.hullArchetype, EModuleType.hangar, maxSlots[2], achievementPoint, ref cheapestCost);
            TryPickCheaperReinforceTarget(slotIndex, hullSubType, hullData.hullArchetype, EModuleType.shield, maxSlots[3] > 0 ? 1 : 0, achievementPoint, ref cheapestCost);
            TryPickCheaperReinforceTarget(slotIndex, hullSubType, hullData.hullArchetype, EModuleType.interceptor, maxSlots[4] > 0 ? 1 : 0, achievementPoint, ref cheapestCost);
        }

        return cheapestCost != int.MaxValue;
    }

    // moduleType의 slotCount개 슬롯 각각의 다음 레벨 비용을 확인해, 감당 가능하고 지금까지의 최저가보다 싸면 튜토리얼 대상으로 기록
    private void TryPickCheaperReinforceTarget(int fleetSlotIndex, string hullSubType, string hullArchetype, EModuleType moduleType, int slotCount, int achievementPoint, ref int cheapestCost)
    {
        if (HullArchetypePassiveUnlock.IsModuleUnlocked(hullArchetype, moduleType) == false) return;

        DataTableModule moduleTable = DataManager.Instance.m_dataTableModule;
        for (int categorySlotIndex = 0; categorySlotIndex < slotCount; categorySlotIndex++)
        {
            int nextLevel = ArchetypeUpgradeLevelLookup.GetLevel(hullArchetype, moduleType, categorySlotIndex) + 1;
            string nextLevelKey = moduleType == EModuleType.hull
                ? CommonUtility.BuildHullArchetypeLevelKey(hullSubType, nextLevel)
                : $"{moduleType}_{nextLevel}_1";

            ModuleData nextLevelData = moduleTable.GetModuleDataFromTable(nextLevelKey);
            if (nextLevelData == null) continue; // 최대 레벨

            int cost = nextLevelData.unlockAchievementPointCost;
            if (cost > achievementPoint) continue;
            if (cost >= cheapestCost) continue;

            cheapestCost = cost;
            m_reinforceFleetSlotIndex = fleetSlotIndex;
            m_reinforceModuleType = moduleType;
            m_reinforceCategorySlotIndex = categorySlotIndex;
        }
    }

    // 모듈 강화 튜토리얼 대상 — CanStartModuleReinforceTutorial이 시작 직전에 기록
    public int GetReinforceFleetSlotIndex() { return m_reinforceFleetSlotIndex; }
    public EModuleType GetReinforceModuleType() { return m_reinforceModuleType; }
    public int GetReinforceCategorySlotIndex() { return m_reinforceCategorySlotIndex; }

    // 함선 언락 튜토리얼이 배치해제/재배치 대상으로 삼는 슬롯 — CanStartHullUnlockTutorial이 시작 직전에 기록
    public int GetHullUnlockSlotIndex()
    {
        return m_hullUnlockSlotIndex;
    }

    // 튜토리얼별 시작 조건 — 조건이 없는 튜토리얼은 항상 true
    private bool IsPanelFirstOpenConditionMet(string tutorialId)
    {
        if (tutorialId == HULL_UNLOCK_TUTORIAL_ID)
            return CanStartHullUnlockTutorial();
        if (tutorialId == MODULE_REINFORCE_TUTORIAL_ID)
            return CanStartModuleReinforceTutorial();

        return true;
    }

    private void OnCurrentPanelChanged(string panelName)
    {
        TryStartPanelFirstOpenTutorial(panelName);
    }

    // 패널을 처음 열 때 시작하는 설명 튜토리얼 — 서버 진행도를 아직 못 받았거나, 이미 완료했거나, 다른 튜토리얼이 재생 중이거나, 시작 조건이 안 맞으면 시작하지 않음(다음에 열 때 다시 확인)
    // 한 패널에 튜토리얼이 여러 개면 배열 앞쪽부터 검사해 조건을 만족하는 첫 번째만 시작
    private void TryStartPanelFirstOpenTutorial(string panelName)
    {
        if (m_panelFirstOpenTutorialIds.TryGetValue(panelName, out string[] tutorialIds) == false) return;
        if (m_isServerLoaded == false) return;
        if (m_isPlaying == true) return;

        for (int i = 0; i < tutorialIds.Length; i++)
        {
            string tutorialId = tutorialIds[i];
            if (IsTutorialCompleted(tutorialId) == true) continue;
            if (IsPanelFirstOpenConditionMet(tutorialId) == false) continue;

            StartCoroutine(StartPanelFirstOpenTutorialDeferred(panelName, tutorialId));
            return;
        }
    }

    // 패널 열림 직후 레이아웃이 자리 잡도록 2프레임 미룬 뒤, 그 패널이 아직 맨 위일 때만 시작
    private IEnumerator StartPanelFirstOpenTutorialDeferred(string panelName, string tutorialId)
    {
        yield return null;
        yield return null;

        if (m_isPlaying == true) yield break;
        if (IsTutorialCompleted(tutorialId) == true) yield break;
        if (UIManager.Instance.GetTopPanelName() != panelName) yield break;

        StartTutorial(tutorialId);
    }

    // 패널 첫 오픈이 아니라 특정 UI가 나타나는 시점(팝업 표시, 버튼 활성화 등)에 호출 — 서버 진행도 미수신/이미 완료/다른 튜토리얼 재생 중이면 시작하지 않음.
    // requiredTopPanelName이 비어있지 않으면 시작 시점에 그 패널이 맨 위일 때만 시작(팝업은 패널 스택에 없으므로 빈 값)
    public void TryStartTutorialDeferred(string tutorialId, string requiredTopPanelName = "")
    {
        if (m_isServerLoaded == false) return;
        if (m_isPlaying == true) return;
        if (IsTutorialCompleted(tutorialId) == true) return;

        StartCoroutine(StartTutorialDeferred(tutorialId, requiredTopPanelName));
    }

    private IEnumerator StartTutorialDeferred(string tutorialId, string requiredTopPanelName)
    {
        yield return null;
        yield return null;

        // 레벨업 알림 등 열려 있는 팝업이 모두 닫힌 뒤 시작
        while (UIManager.Instance != null && UIManager.Instance.HasOverlayPopupOpenOrWaiting() == true)
            yield return null;

        if (m_isPlaying == true) yield break;
        if (IsTutorialCompleted(tutorialId) == true) yield break;
        if (UIManager.Instance == null) yield break;
        if (string.IsNullOrEmpty(requiredTopPanelName) == false && UIManager.Instance.GetTopPanelName() != requiredTopPanelName) yield break;

        StartTutorial(tutorialId);
    }

    // 현재 진행 중인 튜토리얼이 스킵 버튼을 숨기도록 설정됐는지 — TutorialUI가 스킵 버튼 표시 여부를 결정할 때 사용
    public bool IsSkipButtonHiddenForCurrentTutorial()
    {
        if (m_currentTutorial == null) return false;
        return m_currentTutorial.isHideSkipButton;
    }

    // 튜토리얼 시작 (콜백 버전)
    public void StartTutorial(string tutorialId, System.Action<string> onComplete = null)
    {
        if (m_isPlaying) return;

        // 이미 완료된 튜토리얼이면 즉시 콜백 호출
        if (IsTutorialCompleted(tutorialId))
        {
            onComplete?.Invoke(tutorialId);
            return;
        }

        m_onCompleteCallback = onComplete;

        m_currentTutorial = LoadTutorialData(tutorialId);
        if (m_currentTutorial == null)
        {
            Debug.LogWarning($"[Tutorial] 데이터를 찾을 수 없음: {tutorialId}");
            return;
        }

        m_currentStepIndex = 0;
        m_isPlaying = true;
        EnsureTutorialUI();
        ExecuteCurrentStep();
    }

    // 다음 스텝
    public void NextStep()
    {
        if (!m_isPlaying) return;

        m_currentStepIndex++;

        if (m_currentStepIndex >= m_currentTutorial.steps.Count)
        {
            CompleteTutorial();
            return;
        }

        ExecuteCurrentStep();
    }

    // 튜토리얼 스킵
    public void SkipTutorial()
    {
        if (!m_isPlaying) return;
        StopTutorialCondition();
        CompleteTutorial();
    }

    // 특정 UI 클릭 시 호출
    public void OnTargetClicked(string targetId)
    {
        if (!m_isPlaying) return;
        if (m_currentTutorial == null) return;
        if (m_currentStepIndex >= m_currentTutorial.steps.Count) return;

        TutorialStep currentStep = m_currentTutorial.steps[m_currentStepIndex];
        if (currentStep.targetUIId == targetId && currentStep.triggerType == ETutorialTrigger.TargetClick)
        {
            NextStep();
        }
    }

    // 현재 스텝이 AnyClick(화면 아무 곳이나 클릭) 대기 중인지
    private bool IsWaitingForAnyClick()
    {
        if (!m_isPlaying) return false;
        if (m_currentTutorial == null) return false;
        if (m_currentStepIndex >= m_currentTutorial.steps.Count) return false;

        return m_currentTutorial.steps[m_currentStepIndex].triggerType == ETutorialTrigger.AnyClick;
    }

    // 스텝이 바뀔 때마다 호출 — HandleInputMouse/HandleInputTouch는 이 이벤트로만 상태를 알고, TutorialManager 타입을 직접 참조하지 않음
    private void NotifyWaitingForAnyClickChanged()
    {
        EventManager.Trigger_TutorialWaitingForAnyClickChanged(IsWaitingForAnyClick());
    }

    // EventManager.OnConsumeAnyClick 구독 핸들러 — 화면 클릭 시 HandleInputMouse/HandleInputTouch가 발행
    // (HandleInputMouse/HandleInputTouch가 press 시점 스냅샷으로 판단하므로, 같은 클릭이 방금 바뀐 새 스텝의
    // AnyClick까지 이어서 소비하는 문제는 입력 레이어에서 이미 걸러짐)
    private void ConsumeAnyClick()
    {
        if (IsWaitingForAnyClick() == false) return;

        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        NextStep();
    }

    // 완료 여부 확인
    public bool IsTutorialCompleted(string tutorialId)
    {
        return m_completedTutorials.Contains(tutorialId);
    }

    // 튜토리얼 진행 중 여부
    public bool IsPlaying => m_isPlaying;

    // 현재 진행 중인 튜토리얼 ID (진행 중이 아니면 null)
    public string GetCurrentTutorialId() => m_currentTutorial != null ? m_currentTutorial.tutorialId : null;

    // ShipArrivedAtFormation 조건이 대기할 함선 등록 — 함선 생성 직후(UITabFleet 등) 호출
    public void SetPendingNewShip(SpaceShip ship)
    {
        m_pendingNewShip = ship;
    }

    // 현재 스텝 실행
    private void ExecuteCurrentStep()
    {
        if (m_currentTutorial == null) return;
        if (m_currentStepIndex >= m_currentTutorial.steps.Count) return;

        TutorialStep step = m_currentTutorial.steps[m_currentStepIndex];

        // Branch 스텝은 UI를 띄우지 않고 조건만 평가해 분기
        if (step.triggerType == ETutorialTrigger.Branch)
        {
            NotifyWaitingForAnyClickChanged();
            StartCoroutine(ExecuteBranchStepDeferred(m_currentTutorial, m_currentStepIndex));
            return;
        }

        // 사전 액션 (패널 열기)
        if (!string.IsNullOrEmpty(step.preActionPanelName))
        {
            UIManager.Instance?.ShowPanel(step.preActionPanelName);
        }

        // 사전 액션 (탭 전환) — targetUIId가 TabSystem 하위 탭 안에 있을 때 그 탭을 먼저 활성화
        if (!string.IsNullOrEmpty(step.preActionTabName))
        {
            TabSystem[] tabSystems = FindObjectsByType<TabSystem>(FindObjectsSortMode.None);
            foreach (TabSystem tabSystem in tabSystems)
            {
                if (tabSystem != null && tabSystem.gameObject.activeInHierarchy)
                    tabSystem.SwitchToTabByName(step.preActionTabName);
            }
        }

        // UI 표시
        m_tutorialUI?.ShowStep(step);

        NotifyWaitingForAnyClickChanged();
    }

    // 직전 스텝의 클릭이 UI 상태(지휘력 미리보기 등)를 갱신한 뒤 평가하도록 한 프레임 미룸 — 그 사이 튜토리얼이 끝나거나 바뀌었으면 무시
    private IEnumerator ExecuteBranchStepDeferred(TutorialData tutorial, int stepIndex)
    {
        yield return null;

        if (m_isPlaying == false) yield break;
        if (m_currentTutorial != tutorial || m_currentStepIndex != stepIndex) yield break;

        TutorialStep step = tutorial.steps[stepIndex];
        bool isConditionMet = EvaluateBranchCondition(step.conditionType);
        Debug.Log($"[Tutorial] Branch stepId={step.stepId} condition={step.conditionType} met={isConditionMet} jumpTo={step.targetUIId}");
        if (isConditionMet == false)
        {
            NextStep();
            yield break;
        }

        if (step.targetUIId == BRANCH_JUMP_END)
        {
            CompleteTutorial();
            yield break;
        }

        int jumpIndex = tutorial.steps.FindIndex(s => s.stepId == step.targetUIId);
        if (jumpIndex < 0)
        {
            Debug.LogWarning($"[Tutorial] Branch 점프 대상 stepId를 찾을 수 없음: {step.targetUIId} (튜토리얼: {tutorial.tutorialId})");
            NextStep();
            yield break;
        }

        m_currentStepIndex = jumpIndex;
        ExecuteCurrentStep();
    }

    // 튜토리얼 완료
    private void CompleteTutorial()
    {
        StopTutorialCondition();
        string completedId = m_currentTutorial?.tutorialId;

        if (m_currentTutorial != null)
        {
            _ = SaveTutorialToServerAsync(m_currentTutorial.tutorialId);
            m_completedTutorials.Add(m_currentTutorial.tutorialId);
        }

        m_isPlaying = false;
        m_pendingNewShip = null;
        m_tutorialUI?.HideTutorialUI();
        NotifyWaitingForAnyClickChanged();

        Debug.Log($"[Tutorial] 완료: {completedId}");
        m_currentTutorial = null;

        // 콜백 호출
        var callback = m_onCompleteCallback;
        m_onCompleteCallback = null;
        callback?.Invoke(completedId);

        // 이벤트 발생
        OnTutorialCompleted?.Invoke(completedId);
    }

    // 서버에 튜토리얼 완료 저장 (fire and forget)
    private async Task SaveTutorialToServerAsync(string tutorialId)
    {
        try
        {
            var apiClient = NetworkManager.Instance?.GetApiClient();
            if (apiClient == null) return;

            var request = new ProgressSaveRequest
            {
                category = PROGRESS_CATEGORY,
                key = tutorialId
            };

            await apiClient.SaveProgressAsync(request);
            Debug.Log($"[Tutorial] 서버 저장: {tutorialId}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Tutorial] 서버 저장 실패: {e.Message}");
        }
    }

    // 튜토리얼 UI 생성
    private void EnsureTutorialUI()
    {
        if (m_tutorialUI != null) return;

        GameObject prefab = ResourceManager.Instance.Load<GameObject>(TUTORIAL_UI_PATH);
        if (prefab == null)
        {
            Debug.LogError($"[Tutorial] UI 프리팹을 찾을 수 없음: {TUTORIAL_UI_PATH}");
            return;
        }

        Transform parent = UIManager.Instance != null ? UIManager.Instance.GetTutorialContainer() : null;
        if (parent == null)
        {
            Debug.LogError("[Tutorial] TutorialContainer를 찾을 수 없음");
            return;
        }

        GameObject uiObject = Instantiate(prefab, parent);
        uiObject.name = "UITutorial";
        m_tutorialUI = uiObject.GetComponent<TutorialUI>();
    }

    // 데이터 로드
    private TutorialData LoadTutorialData(string tutorialId)
    {
        string path = $"DataTable/Tutorial/{tutorialId}";
        return ResourceManager.Instance.Load<TutorialData>(path);
    }

    #region Tutorial Condition

    public void StartTutorialCondition(TutorialStep step)
    {
        StopTutorialCondition();

        // 이 스텝을 시작한 시점의 스텝 인덱스를 소유권 토큰으로 캡처 — 조건 코루틴이 나중에 깨어났을 때
        // 이미 다른 스텝으로 넘어가 있다면(m_currentStepIndex가 달라짐) RequestNextStep()이 조용히 무시함.
        // 코루틴을 일일이 추적/취소하는 대신 "advance 시도" 자체를 스텝 소유권으로 검증하는 구조.
        int ownerStepIndex = m_currentStepIndex;

        switch (step.conditionType)
        {
            case ETutorialConditionType.CameraRotationChanged:
                m_cameraRotationAccumulated = 0f;
                m_lastCameraRotation = Camera.main.transform.rotation;
                m_customConditionCoroutine = StartCoroutine(CheckCameraRotation(step.conditionThreshold, ownerStepIndex));
                break;

            case ETutorialConditionType.CameraZoomChanged:
                m_cameraZoomAccumulated = 0f;
                m_lastCameraZoom = CameraController.Instance.CurrentZoom;
                m_customConditionCoroutine = StartCoroutine(CheckCameraZoom(step.conditionThreshold, ownerStepIndex));
                break;

            case ETutorialConditionType.ModuleSelected:
            case ETutorialConditionType.ModuleSelectedCount:
            case ETutorialConditionType.SpecificModuleSelected:
                m_selectedModuleIds.Clear();
                EventManager.Subscribe_SpaceShipModuleSelected(OnModuleSelected);
                break;

            case ETutorialConditionType.ShipArrivedAtFormation:
                m_customConditionCoroutine = StartCoroutine(CheckShipArrivedAtFormation(ownerStepIndex));
                break;

            case ETutorialConditionType.WaitForZoneBattleEnd:
                m_zoneBattleEndHandler = () => RequestNextStep(ownerStepIndex);
                EventManager.Subscribe_ZoneCellReturnedToGrid(m_zoneBattleEndHandler);

                m_zoneBattleEndUnlockHandler = (isVictory) => UIManager.Instance.SetTopPanelInteractable(true);
                EventManager.Subscribe_ZoneStageBattleEnd(m_zoneBattleEndUnlockHandler);
                break;

            case ETutorialConditionType.WaitForRewardCardPopup:
                m_rewardCardPopupWaitStepIndex = ownerStepIndex;
                m_rewardCardDefeatHandler = (isVictory) =>
                {
                    UIManager.Instance.SetTopPanelInteractable(true);
                    if (isVictory == false && ownerStepIndex == m_currentStepIndex)
                        CompleteTutorial();
                };
                EventManager.Subscribe_ZoneStageBattleEnd(m_rewardCardDefeatHandler);
                break;

            case ETutorialConditionType.WaitForGridCellClicked:
                m_gridCellClickedHandler = (cell) => RequestNextStep(ownerStepIndex);
                EventManager.Subscribe_ExplorationGridCellClicked(m_gridCellClickedHandler);
                break;
        }
    }

    // 조건 코루틴이 자기 소유 스텝에서만 다음 스텝으로 넘어가게 함 — ownerStepIndex가 코루틴 시작 시점에 캡처한
    // 스텝 인덱스와 다르면(이미 다른 스텝으로 넘어간 뒤 뒤늦게 깨어난 코루틴) 조용히 무시하고 반환.
    public void RequestNextStep(int ownerStepIndex)
    {
        if (ownerStepIndex != m_currentStepIndex) return;
        NextStep();
    }

    // 조건 코루틴이 매 반복마다 "내 스텝이 아직 유효한지" 확인해서 스폰 등 부작용을 계속 이어갈지 판단하는 용도
    public int GetCurrentStepIndex() => m_currentStepIndex;

    public void StopTutorialCondition()
    {
        if (m_customConditionCoroutine != null)
        {
            StopCoroutine(m_customConditionCoroutine);
            m_customConditionCoroutine = null;
        }

        EventManager.Unsubscribe_SpaceShipModuleSelected(OnModuleSelected);
        m_selectedModuleIds.Clear();
        m_cameraRotationAccumulated = 0f;

        if (m_zoneBattleEndHandler != null)
        {
            EventManager.Unsubscribe_ZoneCellReturnedToGrid(m_zoneBattleEndHandler);
            m_zoneBattleEndHandler = null;
        }

        if (m_zoneBattleEndUnlockHandler != null)
        {
            EventManager.Unsubscribe_ZoneStageBattleEnd(m_zoneBattleEndUnlockHandler);
            m_zoneBattleEndUnlockHandler = null;
        }

        if (m_gridCellClickedHandler != null)
        {
            EventManager.Unsubscribe_ExplorationGridCellClicked(m_gridCellClickedHandler);
            m_gridCellClickedHandler = null;
        }

        if (m_rewardCardDefeatHandler != null)
        {
            EventManager.Unsubscribe_ZoneStageBattleEnd(m_rewardCardDefeatHandler);
            m_rewardCardDefeatHandler = null;
        }
        m_rewardCardPopupWaitStepIndex = -1;
    }

    // 보상 카드 팝업이 뜬 직후 호출 — WaitForRewardCardPopup 대기 중일 때만, 카드 목록 레이아웃이 자리 잡도록 2프레임 뒤 다음 스텝으로
    public void NotifyRewardCardPopupShown()
    {
        if (m_isPlaying == false) return;
        if (m_rewardCardPopupWaitStepIndex < 0) return;

        StartCoroutine(AdvanceAfterRewardCardPopupShown(m_rewardCardPopupWaitStepIndex));
    }

    private IEnumerator AdvanceAfterRewardCardPopupShown(int ownerStepIndex)
    {
        yield return null;
        yield return null;

        RequestNextStep(ownerStepIndex);
    }

    // m_pendingNewShip이 대형 자리에 도착(Moving 상태 해제)할 때까지 대기
    private IEnumerator CheckShipArrivedAtFormation(int ownerStepIndex)
    {
        while (m_isPlaying)
        {
            if (m_pendingNewShip != null && m_pendingNewShip.m_formationMoveState != FormationMoveState.Moving)
            {
                m_pendingNewShip = null;
                RequestNextStep(ownerStepIndex);
                yield break;
            }
            yield return null;
        }
    }

    private IEnumerator CheckCameraRotation(float threshold, int ownerStepIndex)
    {
        while (m_isPlaying)
        {
            if (Camera.main == null)
            {
                yield return null;
                continue;
            }

            Quaternion currentRotation = Camera.main.transform.rotation;
            float angleDelta = Quaternion.Angle(m_lastCameraRotation, currentRotation);
            m_cameraRotationAccumulated += angleDelta;
            m_lastCameraRotation = currentRotation;

            if (m_cameraRotationAccumulated >= threshold)
            {
                RequestNextStep(ownerStepIndex);
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator CheckCameraZoom(float threshold, int ownerStepIndex)
    {
        while (m_isPlaying)
        {
            if (CameraController.Instance == null)
            {
                yield return null;
                continue;
            }

            float currentZoom = CameraController.Instance.CurrentZoom;
            float zoomDelta = Mathf.Abs(currentZoom - m_lastCameraZoom);
            m_cameraZoomAccumulated += zoomDelta;
            m_lastCameraZoom = currentZoom;

            if (m_cameraZoomAccumulated >= threshold)
            {
                RequestNextStep(ownerStepIndex);
                yield break;
            }

            yield return null;
        }
    }

    private void OnModuleSelected(SpaceShip ship, ModuleBase module)
    {
        if (!m_isPlaying || m_currentTutorial == null) return;
        if (m_currentStepIndex >= m_currentTutorial.steps.Count) return;

        TutorialStep step = m_currentTutorial.steps[m_currentStepIndex];
        if (step.triggerType != ETutorialTrigger.Custom) return;

        switch (step.conditionType)
        {
            case ETutorialConditionType.ModuleSelected:
                NextStep();
                break;

            case ETutorialConditionType.ModuleSelectedCount:
                int moduleId = module.GetInstanceID();
                if (m_selectedModuleIds.Add(moduleId))
                {
                    Debug.Log($"[Tutorial] 모듈 선택 {m_selectedModuleIds.Count}/{step.conditionCount}");
                    if (m_selectedModuleIds.Count >= step.conditionCount)
                        NextStep();
                }
                break;

            case ETutorialConditionType.SpecificModuleSelected:
                if (module.GetModuleType() == step.targetModuleType)
                    NextStep();
                break;
        }
    }

    #endregion
}
