using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

// 튜토리얼 시스템 관리자
public class TutorialManager : MonoSingleton<TutorialManager>
{
    private const string TUTORIAL_UI_PATH = "Prefabs/UI/Tutorial/UITutorial";
    private const string PROGRESS_CATEGORY = "tutorial";

    // 모듈 미네랄 강화 모드 언락 — 이 튜토리얼의 완료 여부 자체를 언락 플래그로 사용 (별도 서버 필드 불필요)
    public const string MINERAL_MODE_UNLOCK_TUTORIAL_ID = "Tutorial_MineralModeUnlock";

    // 지휘력 증가 안내 — 존런 종료로 탐험 포인트가 확정된 직후 TryStartCommandPowerIncreaseTutorial()이 시작시킴
    public const string COMMAND_POWER_INCREASE_TUTORIAL_ID = "Tutorial_CommandPowerIncrease";

    // Branch 스텝의 targetUIId에 이 값을 쓰면 점프 대신 튜토리얼을 완료 처리
    public const string BRANCH_JUMP_END = "END";

    // 튜토리얼 스텝의 targetUIId가 '@'로 시작하면 하이라키 경로 대신 이 매니저에 등록된 리졸버가 그때그때 대상을 계산해 돌려줌 —
    // 데이터(잠금 여부 등)에 따라 대상 행이 달라지는 스텝용. 예: 언락 안 된 최저 티어 함체의 언락 버튼
    public const string DYNAMIC_TARGET_LOWEST_LOCKED_HULL_UNLOCK_BUTTON = "@LowestLockedHullUnlockButton";

    // 함체 언락 안내 — 업적포인트가 티어4 함체 언락 비용에 도달한 뒤 업적 패널이 닫힐 때 TryStartHullUnlockTutorial()이 대기 등록, TryStartPendingTutorial()이 시작시킴
    public const string HULL_UNLOCK_TUTORIAL_ID = "Tutorial_HullUnlock";
    private const int HULL_UNLOCK_TUTORIAL_MIN_ACHIEVEMENT_POINT = 500; // DataTableModule 티어4 함체의 unlockAchievementPointCost

    // 함선 슬롯 증가 안내 — 지휘관 레벨업으로 최대 함선 수가 1에서 2 이상이 되면 RequestShipSlotIncreaseTutorial()로 대기 등록,
    // 레벨업 팝업이 닫히고 메인 UI가 top이며 다른 튜토리얼이 재생 중이 아닐 때 TryStartPendingTutorial()이 시작시킴
    public const string SHIP_SLOT_INCREASE_TUTORIAL_ID = "Tutorial_ShipSlotIncrease";

    // 모듈 티어업 안내 — 티어업이 실제로 가능한 상태(티어 2 모듈이 없고, 모듈 티어 < 함체 티어이며, 여유 지휘력이 티어업 비용 이상)가 되면
    // TryRequestModuleTierUpTutorial()이 대기 등록, TryStartPendingTutorial()이 시작시킴. 조건이 사라지면(IsConditionTutorialObsolete) 폐기
    public const string MODULE_TIER_UP_TUTORIAL_ID = "Tutorial_ModuleTierUp";

    // 모듈 티어업 튜토리얼 스텝의 동적 타겟 — 조건을 만족한 후보 함선/모듈에 따라 대상이 달라지므로 각 UI가 리졸버를 등록
    public const string DYNAMIC_TARGET_TIER_UP_SHIP_MANAGE_BUTTON = "@TierUpShipManageButton";     // UIPanelFleet — 후보 함선 행의 관리 버튼
    public const string DYNAMIC_TARGET_TIER_UP_MODULE_MANAGE_BUTTON = "@TierUpModuleManageButton"; // UIShipLoadoutEditorView — 후보 모듈 행의 관리 버튼
    public const string DYNAMIC_TARGET_LOADOUT_CONFIRM_BUTTON = "@LoadoutConfirmButton";           // UIShipLoadoutEditorView — 확인 버튼
    public const string DYNAMIC_TARGET_MODULE_TIER_UP_BUTTON = "@ModuleTierUpButton";              // UIPopupModuleReinforce — 티어업 버튼

    // 순서대로 진행되는 온보딩 튜토리얼 — ObjectManager.RunTutorialSequence가 이 순서대로 재생하고,
    // 스킵 버튼 클릭 시(SkipTutorial) 이 목록 전체를 한 번에 완료 처리한 뒤 노말 플레이로 전환함
    public static readonly string[] ONBOARDING_TUTORIAL_SEQUENCE =
    {
        "Tutorial_FirstPlay",
        "Tutorial_FirstPlay_ManageShip",
        "Tutorial_FirstPlay_Battle",
        "Tutorial_FirstPlay_Complete"
    };

    private TutorialData m_currentTutorial;
    private int m_currentStepIndex;
    private bool m_isPlaying;
    private bool m_isServerLoaded;
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
    private TutorialBattleCinematic m_battleCinematic; // Tutorial_FirstPlay_Battle/Complete 전용 전투 연출(웨이브/탈출함선/기함폭발) 상태 및 로직 — OnInitialize에서 생성
    private System.Action m_zoneBattleEndHandler; // WaitForZoneBattleEnd 조건용(그리드 복귀 시점 대기) — StopTutorialCondition에서 해제
    // UI 상호작용 잠금 해제 전용 — 스텝 전환(ZoneCellReturnedToGrid, 보상카드 팝업까지 처리된 뒤)보다 먼저, 실제 전투가
    // 끝나는 즉시(ZoneStageBattleEnd) 풀어야 함. 안 그러면 보상카드 팝업 처리하는 동안 UIPanelBattle이 계속 잠긴 채로
    // 남아있다가(그 시점엔 이미 안 보여서 못 느끼지만) 다음 실전투 때도 잠긴 채로 다시 나타나는 문제가 있었음
    private System.Action<bool> m_zoneBattleEndUnlockHandler;
    private System.Action<GridCell3D> m_gridCellClickedHandler; // WaitForGridCellClicked 조건용 — StopTutorialCondition에서 해제

    // Branch 스텝 조건 평가자 — 해당 UI 컴포넌트가 Awake에서 등록, OnDestroy에서 해제(로그아웃 초기화 대상 아님)
    private readonly Dictionary<ETutorialConditionType, System.Func<bool>> m_branchConditionEvaluators = new Dictionary<ETutorialConditionType, System.Func<bool>>();

    // '@' 동적 타겟 리졸버 — 해당 UI 컴포넌트가 Awake에서 등록, OnDestroy에서 해제(로그아웃 초기화 대상 아님)
    private readonly Dictionary<string, System.Func<RectTransform>> m_dynamicTargetResolvers = new Dictionary<string, System.Func<RectTransform>>();

    // 조건형 튜토리얼(함체 언락/함선 슬롯 증가) 대기 상태 — 시작 조건이 갖춰질 때까지 보류
    private readonly List<string> m_pendingTutorialIds = new List<string>();
    private bool m_isLevelupPopupOpen;
    private bool m_isPendingTutorialStartScheduled;

    // 튜토리얼 완료 이벤트 (tutorialId 전달)
    public event System.Action<string> OnTutorialCompleted;

    // 스킵 버튼으로 튜토리얼을 도중에 끝낼 때 발생 — 온보딩 시퀀스를 진행하던 쪽(ObjectManager)이
    // 이 이벤트를 받아 다음 튜토리얼로 이어가지 않고 바로 노말 플레이로 전환하도록 함
    public event System.Action OnTutorialSkipRequested;

    protected override bool ShouldDontDestroyOnLoad => true;

    protected override void OnInitialize()
    {
        EventManager.Subscribe_ConsumeAnyClick(ConsumeAnyClick);
        EventManager.Subscribe_CurrentPanelChanged(OnCurrentPanelChanged);
        m_battleCinematic = new TutorialBattleCinematic(this);
    }

    // 로그아웃 시 호출 — TutorialManager는 DontDestroyOnLoad라 로그아웃해도 인스턴스가 안 죽어서
    // OnInitialize()가 다시 실행되지 않음. EventManager.UnsubscribeAll()이 지운 구독을 복구하고,
    // 이전 세션(진행 중이던 튜토리얼 스텝/코루틴/조건 상태)이 다음 로그인으로 새어 들어가지 않도록 초기화
    public void ResetForLogout()
    {
        StopTutorialCondition();
        CleanupTutorialCombatArtifacts();

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
        m_pendingTutorialIds.Clear();
        m_isLevelupPopupOpen = false;
        m_isPendingTutorialStartScheduled = false;

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

    // 모듈 미네랄 강화 모드 언락 여부 — 이미 언락됐으면 true
    public bool IsMineralModeUnlocked()
    {
        return IsTutorialCompleted(MINERAL_MODE_UNLOCK_TUTORIAL_ID);
    }

    // 특정 스테이지 이후 전멸/후퇴 시 호출 — 아직 언락 전이면 설명 튜토리얼 시작(완료되면 그 자체가 언락 플래그)
    public void TryUnlockMineralMode()
    {
        if (IsMineralModeUnlocked() == true) return;
        StartTutorial(MINERAL_MODE_UNLOCK_TUTORIAL_ID);
    }

    // 존런 종료(탈출/포기)로 탐험 포인트 잔액이 확정된 직후 호출 — 포인트가 있어야 변환을 시연할 수 있으므로 0이면 시작하지 않음(호출부에서 >0 필터링)
    // 호출 시점이 OnConfirmEscape/OnAbandonRunConfirmed가 자기 몫의 다음 존 전환을 아직 안 끝낸 중간이라, 한 프레임 미뤄서 그 전환이 끝난 뒤 시작(안 그러면 ShowMainPanel의 ExitGalaxyView와 겹쳐 그리드 UI 잔상이 남음)
    public void TryStartCommandPowerIncreaseTutorial()
    {
        if (IsTutorialCompleted("Tutorial_Exploration") == false) return;
        StartCoroutine(StartCommandPowerIncreaseTutorialDeferred());
    }

    private IEnumerator StartCommandPowerIncreaseTutorialDeferred()
    {
        yield return null;
        StartTutorial(COMMAND_POWER_INCREASE_TUTORIAL_ID);
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

    // 업적 패널이 닫히는 순간 호출 — 보유 업적포인트가 티어4 함체 언락 비용 이상이고 아직 안 본 경우 대기 등록
    public void TryStartHullUnlockTutorial(int achievementPoint)
    {
        if (achievementPoint < HULL_UNLOCK_TUTORIAL_MIN_ACHIEVEMENT_POINT) return;
        RequestPendingTutorial(HULL_UNLOCK_TUTORIAL_ID);
    }

    // 로그인 후 메인 진입 시 호출 — 조건(업적포인트/함선 슬롯)은 만족했지만 튜토리얼을 끝까지 못 본 채 종료·재접속한 경우 다시 대기 등록
    public void RestoreConditionTutorials()
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        int achievementPoint = commander.GetAchievementPoint();
        if (achievementPoint >= HULL_UNLOCK_TUTORIAL_MIN_ACHIEVEMENT_POINT)
            RequestPendingTutorial(HULL_UNLOCK_TUTORIAL_ID);

        int shipCount = DataManager.Instance.m_dataTableCommander.GetShipCount(commander.GetCommanderLevel());
        if (shipCount >= 2)
            RequestPendingTutorial(SHIP_SLOT_INCREASE_TUTORIAL_ID);

        TryRequestModuleTierUpTutorial();
    }

    // 온보딩을 마친 유저에게만 안내 — 지휘력/함선/로드아웃이 바뀐 뒤 메인 화면으로 돌아올 때(OnCurrentPanelChanged)와 로그인 복구 시 재평가
    // 조건을 만족하지 않으면 RequestPendingTutorial이 IsConditionTutorialObsolete로 걸러 대기 등록하지 않음
    private void TryRequestModuleTierUpTutorial()
    {
        if (m_isServerLoaded == false) return;
        if (IsTutorialCompleted("Tutorial_Exploration") == false) return;
        if (GetCurrentTutorialId() == MODULE_TIER_UP_TUTORIAL_ID) return; // 진행 중 화면 전환마다 중복 대기 등록되지 않도록
        if (IsTutorialCompleted(MODULE_TIER_UP_TUTORIAL_ID) == true) return;
        if (IsModuleTierUpTutorialConditionMet() == false) return; // 화면 전환마다 호출되므로 미충족이면 대기 등록 로직까지 가지 않음

        RequestPendingTutorial(MODULE_TIER_UP_TUTORIAL_ID);
    }

    // 티어2 이상 모듈이 없고(티어업 경험 없음), 지금 티어업 가능한 후보 모듈이 있는지
    private bool IsModuleTierUpTutorialConditionMet()
    {
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition == null) return false;
        if (composition.HasTieredUpModule() == true) return false;

        return composition.TryFindModuleTierUpCandidate(out _, out _, out _);
    }

    // 지금 조건에 맞는 티어업 후보 — 동적 타겟 리졸버(UIPanelFleet/UIShipLoadoutEditorView)가 대상 행을 찾을 때 사용
    public bool TryGetModuleTierUpCandidate(out int shipSlotIndex, out EModuleType moduleType, out int categorySlotIndex)
    {
        shipSlotIndex = -1;
        moduleType = EModuleType.beam;
        categorySlotIndex = 0;

        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (composition == null) return false;

        return composition.TryFindModuleTierUpCandidate(out shipSlotIndex, out moduleType, out categorySlotIndex);
    }

    // 조건형 튜토리얼이 이미 필요 없어진 상태인지 — 함체 언락은 하나라도 언락했으면, 함선 슬롯은 빈 슬롯이 없으면 안내 대상이 아님
    private bool IsConditionTutorialObsolete(string tutorialId)
    {
        if (tutorialId == HULL_UNLOCK_TUTORIAL_ID)
        {
            Commander commander = DataManager.Instance.m_currentCommander;
            return commander != null && commander.HasAnyUnlockedHull() == true;
        }

        if (tutorialId == SHIP_SLOT_INCREASE_TUTORIAL_ID)
            return HasEmptyShipSlot() == false;

        if (tutorialId == MODULE_TIER_UP_TUTORIAL_ID)
            return IsModuleTierUpTutorialConditionMet() == false;

        return false;
    }

    // 현재 레벨에서 열린 함선 슬롯 수보다 배치된 함선이 적은지 — 이미 슬롯을 다 채운 유저에게는 함선 추가 안내가 무의미함
    private bool HasEmptyShipSlot()
    {
        Commander commander = DataManager.Instance.m_currentCommander;
        FleetComposition composition = DataManager.Instance.m_currentFleetComposition;
        if (commander == null || composition == null) return false;

        int openSlotCount = DataManager.Instance.m_dataTableCommander.GetShipCount(commander.GetCommanderLevel());
        int placedShipCount = composition.GetPlacedShips().Count;
        Debug.Log($"[ShipSlotTutorialLOG] HasEmptyShipSlot level={commander.GetCommanderLevel()} openSlotCount={openSlotCount} placedShipCount={placedShipCount}");
        return placedShipCount < openSlotCount;
    }

    // 레벨업으로 최대 함선 수가 늘어난 시점에 호출 — 레벨업 팝업이 닫힐 때까지 시작을 막고 대기 등록
    public void RequestShipSlotIncreaseTutorial()
    {
        bool isCompleted = IsTutorialCompleted(SHIP_SLOT_INCREASE_TUTORIAL_ID);
        Debug.Log($"[ShipSlotTutorialLOG] RequestShipSlotIncreaseTutorial isCompleted={isCompleted}");
        if (isCompleted == true) return;

        m_isLevelupPopupOpen = true;
        RequestPendingTutorial(SHIP_SLOT_INCREASE_TUTORIAL_ID);
    }

    // 레벨업 알림 팝업이 닫힐 때 호출(확인/자동 닫힘 공통)
    public void NotifyLevelupPopupClosed()
    {
        m_isLevelupPopupOpen = false;
        Debug.Log($"[ShipSlotTutorialLOG] NotifyLevelupPopupClosed pendingCount={m_pendingTutorialIds.Count} isPlaying={m_isPlaying}");
        TryStartPendingTutorial();
    }

    private void OnCurrentPanelChanged(string panelName)
    {
        TryRequestModuleTierUpTutorial();
        TryStartPendingTutorial();
    }

    // 시작 조건이 갖춰질 때까지 보류할 튜토리얼 등록 — 이미 완료했거나 이미 등록된 id는 무시
    private void RequestPendingTutorial(string tutorialId)
    {
        bool isCompleted = IsTutorialCompleted(tutorialId);
        bool isObsolete = IsConditionTutorialObsolete(tutorialId);
        if (isCompleted == true) return;
        if (isObsolete == true) return;
        if (m_pendingTutorialIds.Contains(tutorialId) == false)
            m_pendingTutorialIds.Add(tutorialId);

        TryStartPendingTutorial();
    }

    // 대기 중인 튜토리얼의 시작 조건 확인 — 팝업 닫힘 / 패널 변경 / 다른 튜토리얼 종료 시점마다 재확인됨
    private void TryStartPendingTutorial()
    {
        if (CanStartPendingTutorial() == false) return;
        if (m_isPendingTutorialStartScheduled == true) return;

        m_isPendingTutorialStartScheduled = true;
        StartCoroutine(StartPendingTutorialDeferred());
    }

    private bool CanStartPendingTutorial()
    {
        if (m_pendingTutorialIds.Count == 0) return false;

        int panelStackDepth = UIManager.Instance.GetPanelStackDepth();
        bool isMainPanelOnTop = panelStackDepth <= 1;
        Debug.Log($"[ShipSlotTutorialLOG] CanStartPendingTutorial pendingCount={m_pendingTutorialIds.Count} isLevelupPopupOpen={m_isLevelupPopupOpen} isPlaying={m_isPlaying} panelStackDepth={panelStackDepth}");
        if (m_isLevelupPopupOpen == true) return false;
        if (m_isPlaying == true) return false;

        return isMainPanelOnTop;
    }

    // 패널 전환/팝업 정리가 끝난 뒤 시작하도록 한 프레임 미룬 다음 조건을 다시 확인
    private IEnumerator StartPendingTutorialDeferred()
    {
        yield return null;
        m_isPendingTutorialStartScheduled = false;

        if (CanStartPendingTutorial() == false) yield break;

        string tutorialId = m_pendingTutorialIds[0];
        m_pendingTutorialIds.RemoveAt(0);
        Debug.Log($"[ShipSlotTutorialLOG] StartPendingTutorialDeferred id={tutorialId} isObsolete={IsConditionTutorialObsolete(tutorialId)}");

        // 대기하는 사이 안내할 필요가 없어졌으면(이미 언락/이미 슬롯 채움) 시작하지 않고 폐기
        if (IsConditionTutorialObsolete(tutorialId) == false)
            StartTutorial(tutorialId);

        // 완료돼 있던 id라 StartTutorial이 바로 끝났거나 아직 대기열이 남았으면 이어서 확인
        TryStartPendingTutorial();
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

        // 함대편성 패널이 열린 채로 전투 연출이 시작되면 화면을 가리므로, 시작 시점에 미리 닫아둠(기존엔 전투 직전까지 열려있었음)
        if (tutorialId == "Tutorial_FirstPlay_Battle")
            UIManager.Instance.ShowMainPanel();

        // 존런 종료 직후(그리드 패널이 여전히 top)에 발동될 수 있어, 메인 UI로 복귀시킨 뒤 시작 —
        // 안 그러면 1스텝이 가리키는 FleetButton이 비활성 상태(메인 패널이 가려진 채)라 대상을 못 찾음
        if (tutorialId == COMMAND_POWER_INCREASE_TUTORIAL_ID)
            UIManager.Instance.ShowMainPanel();

        // 업적 패널이 닫힌 직후 발동 — 1스텝이 가리키는 FleetButton이 보이도록 메인 UI로 복귀시킨 뒤 시작
        if (tutorialId == HULL_UNLOCK_TUTORIAL_ID)
            UIManager.Instance.ShowMainPanel();

        // 대기 조건에서 이미 메인 UI가 top임을 확인하고 시작하지만, 직접 호출돼도 FleetButton이 보이도록 동일하게 보장
        if (tutorialId == SHIP_SLOT_INCREASE_TUTORIAL_ID)
            UIManager.Instance.ShowMainPanel();

        // 1스텝이 가리키는 FleetButton이 보이도록 메인 UI가 top임을 보장
        if (tutorialId == MODULE_TIER_UP_TUTORIAL_ID)
            UIManager.Instance.ShowMainPanel();

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
        CleanupTutorialCombatArtifacts();

        string tutorialId = m_currentTutorial != null ? m_currentTutorial.tutorialId : null;
        bool isOnboardingTutorial = System.Array.IndexOf(ONBOARDING_TUTORIAL_SEQUENCE, tutorialId) >= 0;
        if (isOnboardingTutorial == true)
        {
            SkipOnboardingSequence();
            return;
        }

        CompleteTutorial();
    }

    // Tutorial_FirstPlay* 온보딩 시퀀스는 어느 단계에서 스킵하든 전부 완료 처리하고 곧바로 노말 플레이로 전환
    private void SkipOnboardingSequence()
    {
        foreach (string tutorialId in ONBOARDING_TUTORIAL_SEQUENCE)
            CompleteTutorialWithoutPlaying(tutorialId);

        m_isPlaying = false;
        m_pendingNewShip = null;
        m_onCompleteCallback = null; // 다음 튜토리얼로 이어가는 콜백 체인을 버림 — 노말 플레이로 바로 전환
        if (m_tutorialUI != null)
            m_tutorialUI.HideTutorialUI();
        NotifyWaitingForAnyClickChanged();

        string completedId = m_currentTutorial != null ? m_currentTutorial.tutorialId : null;
        Debug.Log($"[Tutorial] 온보딩 스킵: {completedId}");
        m_currentTutorial = null;

        OnTutorialSkipRequested?.Invoke();
    }

    // 스킵 등으로 튜토리얼을 도중에 끝낼 때 남아있는 연출용 함대(탈출선/적 웨이브)를 정리
    private void CleanupTutorialCombatArtifacts()
    {
        m_battleCinematic.Cleanup();
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

        // 다른 튜토리얼이 재생 중이라 보류됐던 조건형 튜토리얼 재확인
        TryStartPendingTutorial();
    }

    // 스킵으로 플레이하지 않고 건너뛴 튜토리얼을 완료 처리 — 다음 실행 시 다시 뜨지 않도록 서버에도 저장
    private void CompleteTutorialWithoutPlaying(string tutorialId)
    {
        if (string.IsNullOrEmpty(tutorialId)) return;
        if (m_completedTutorials.Contains(tutorialId) == true) return;

        m_completedTutorials.Add(tutorialId);
        _ = SaveTutorialToServerAsync(tutorialId);

        // 정상 완료가 아니어도 OnTutorialCompleted 구독자(UITabShip 등)는 동일하게 통지받아야 함
        OnTutorialCompleted?.Invoke(tutorialId);
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

            case ETutorialConditionType.WaitForGridCellClicked:
                m_gridCellClickedHandler = (cell) => RequestNextStep(ownerStepIndex);
                EventManager.Subscribe_ExplorationGridCellClicked(m_gridCellClickedHandler);
                break;

            case ETutorialConditionType.EscapeShipDistanceFromFlagship:
            case ETutorialConditionType.EnemyWave1:
            case ETutorialConditionType.EnemyWave2:
            case ETutorialConditionType.FlagshipHealthBelowPercent:
            case ETutorialConditionType.SiegfriedFlagshipExplosion:
            case ETutorialConditionType.CleanupEscapeFleet:
                // Tutorial_FirstPlay_Battle/Complete 전용 연출 조건
                m_battleCinematic.StartCondition(step, ownerStepIndex);
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
