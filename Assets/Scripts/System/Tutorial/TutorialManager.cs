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
#if false // TutorialBattleCinematic 주석처리로 임시 비활성화
    private TutorialBattleCinematic m_battleCinematic; // Tutorial_FirstPlay_Battle/Complete 전용 전투 연출(웨이브/탈출함선/기함폭발) 상태 및 로직 — OnInitialize에서 생성
#endif
    private System.Action<bool> m_zoneBattleEndHandler; // WaitForZoneBattleEnd 조건용 — StopTutorialCondition에서 해제

    // 튜토리얼 완료 이벤트 (tutorialId 전달)
    public event System.Action<string> OnTutorialCompleted;

    // 스킵 버튼으로 튜토리얼을 도중에 끝낼 때 발생 — 온보딩 시퀀스를 진행하던 쪽(ObjectManager)이
    // 이 이벤트를 받아 다음 튜토리얼로 이어가지 않고 바로 노말 플레이로 전환하도록 함
    public event System.Action OnTutorialSkipRequested;

    protected override bool ShouldDontDestroyOnLoad => true;

    protected override void OnInitialize()
    {
        EventManager.Subscribe_ConsumeAnyClick(ConsumeAnyClick);
#if false // TutorialBattleCinematic 주석처리로 임시 비활성화
        m_battleCinematic = new TutorialBattleCinematic(this);
#endif
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

        // EventManager.UnsubscribeAll()로 지워진 이 매니저 자신의 구독 복구 —
        // 먼저 해제 후 재구독해야 호출 시점과 무관하게 항상 구독이 정확히 1개만 남음(중복 구독 방지)
        EventManager.Unsubscribe_ConsumeAnyClick(ConsumeAnyClick);
        EventManager.Subscribe_ConsumeAnyClick(ConsumeAnyClick);
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
#if false // TutorialBattleCinematic 주석처리로 임시 비활성화
        m_battleCinematic.Cleanup();
#endif
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
                m_zoneBattleEndHandler = (isVictory) => RequestNextStep(ownerStepIndex);
                EventManager.Subscribe_ZoneStageBattleEnd(m_zoneBattleEndHandler);
                break;

            case ETutorialConditionType.EscapeShipDistanceFromFlagship:
            case ETutorialConditionType.EnemyWave1:
            case ETutorialConditionType.EnemyWave2:
            case ETutorialConditionType.FlagshipHealthBelowPercent:
            case ETutorialConditionType.SiegfriedFlagshipExplosion:
            case ETutorialConditionType.CleanupEscapeFleet:
                // Tutorial_FirstPlay_Battle/Complete 전용 연출 조건 — TutorialBattleCinematic 주석처리로 임시 비활성화
#if false
                m_battleCinematic.StartCondition(step, ownerStepIndex);
#endif
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
            EventManager.Unsubscribe_ZoneStageBattleEnd(m_zoneBattleEndHandler);
            m_zoneBattleEndHandler = null;
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
