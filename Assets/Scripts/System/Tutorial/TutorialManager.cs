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
    private SpaceFleet m_cinematicFleetA;
    private SpaceShip m_pendingNewShip; // ShipArrivedAtFormation 조건이 대기할 함선 (UITabFleet에서 함선 생성 직후 등록)
    private SpaceFleet m_escapeFleet;   // EscapeShipDistanceFromFlagship 조건이 스폰/추적하는 탈출 함대 — 이후 실제 유저 함대의 기함이 됨
    private Coroutine m_escapeFleetMoveCoroutine; // m_customConditionCoroutine과 별개로 관리 — step 전환(StopTutorialCondition)에 끊기지 않고 계속 이동
    private float m_escapeFleetSpeedMultiplier = 1f; // 워프 연출 시 MoveEscapeFleetForward 재시작 없이 속도만 배가
    private WarpEffectShip m_escapeWarpEffect; // Tutorial_FirstPlay_Complete까지 유지했다가 CleanupEscapeFleet()에서 정지
    private const float ESCAPE_SHIP_SPEED = 10f;
    private readonly List<SpaceFleet> m_enemyWaveFleets = new List<SpaceFleet>(); // EnemyWave1/2 조건이 스폰한 적 함대 목록
    private readonly Dictionary<int, SpaceFleet> m_waveIndexOccupancy = new Dictionary<int, SpaceFleet>(); // positionIndex → 그 자리를 차지한 함대 (전멸하면 자리 반납)
    private static readonly WaitForSeconds k_cameraTransitionWait = new WaitForSeconds(3f); // 카메라가 탈출 함선으로 넘어갈 시간

    // 튜토리얼 완료 이벤트 (tutorialId 전달)
    public event System.Action<string> OnTutorialCompleted;

    protected override bool ShouldDontDestroyOnLoad => true;

    protected override void OnInitialize()
    {
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
        if (currentStep.targetUIId == targetId && currentStep.triggerType == ETutorialTrigger.UIClick)
        {
            NextStep();
        }
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
    }

    // 튜토리얼 완료
    private void CompleteTutorial()
    {
        StopTutorialCondition();
        string completedId = m_currentTutorial?.tutorialId;

        if (m_currentTutorial != null)
        {
            SaveTutorialToServer(m_currentTutorial.tutorialId);
            m_completedTutorials.Add(m_currentTutorial.tutorialId);
        }

        m_isPlaying = false;
        m_pendingNewShip = null;
        m_tutorialUI?.Hide();

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
    private async void SaveTutorialToServer(string tutorialId)
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

        switch (step.conditionType)
        {
            case ETutorialConditionType.CameraRotationChanged:
                m_cameraRotationAccumulated = 0f;
                m_lastCameraRotation = Camera.main.transform.rotation;
                m_customConditionCoroutine = StartCoroutine(CheckCameraRotation(step.conditionThreshold));
                break;

            case ETutorialConditionType.CameraZoomChanged:
                m_cameraZoomAccumulated = 0f;
                m_lastCameraZoom = CameraController.Instance.CurrentZoom;
                m_customConditionCoroutine = StartCoroutine(CheckCameraZoom(step.conditionThreshold));
                break;

            case ETutorialConditionType.ModuleSelected:
            case ETutorialConditionType.ModuleSelectedCount:
            case ETutorialConditionType.SpecificModuleSelected:
                m_selectedModuleIds.Clear();
                EventManager.Subscribe_SpaceShipModuleSelected(OnModuleSelected);
                break;

            case ETutorialConditionType.CinematicOpeningBattle:
                m_cinematicFleetA = TutorialCinematicController.SpawnOpeningBattle();
                if (m_cinematicFleetA != null && CameraController.Instance != null)
                    CameraController.Instance.SetTargetOfCameraController(m_cinematicFleetA.transform);
                // 완료 조건(A함대 거의 전멸 감지)은 다음 단계에서 구현 — 지금은 스킵 버튼으로만 다음 스텝 진행
                break;

            case ETutorialConditionType.ShipArrivedAtFormation:
                m_customConditionCoroutine = StartCoroutine(CheckShipArrivedAtFormation());
                break;

            case ETutorialConditionType.EscapeShipDistanceFromFlagship:
            {
                SpaceFleet siegfriedFleet = ObjectManager.Instance.GetMyFleet();
                m_escapeFleet = TutorialCinematicController.SpawnEscapeFleet(siegfriedFleet);
                // 이동은 별도 코루틴으로 분리 — step 전환 시 StopTutorialCondition()에 끊기지 않고 계속 이동
                m_escapeFleetMoveCoroutine = StartCoroutine(MoveEscapeFleetForward());
                m_customConditionCoroutine = StartCoroutine(CheckEscapeShipDistance(siegfriedFleet, step.conditionThreshold));
                break;
            }

            case ETutorialConditionType.EnemyWave1:
            {
                // 지크프리트 기함은 전투 데미지로는 실제 파괴되지 않고 10%에서 버티다가 이후 연출(폭발)로만 파괴됨
                SpaceFleet siegfriedFleet = ObjectManager.Instance.GetMyFleet();
                SpaceShip flagship = siegfriedFleet != null ? siegfriedFleet.GetFlagship() : null;
                if (flagship != null)
                    flagship.m_minHealthRatio = 0.1f;

                m_customConditionCoroutine = StartCoroutine(SpawnEnemyWaveRoutine(new int[] { 7, 3, 3 }, fleetCount: 5, spawnInterval: 5f));
                break;
            }

            case ETutorialConditionType.EnemyWave2:
                // 애초에 전멸이 불가능한 물량 — 스폰 코루틴은 전멸 대기 없이 스폰만 담당하고,
                // 다음 스텝 전환은 별도로 띄운 FlagshipHealthBelowPercent 코루틴이 담당
                m_customConditionCoroutine = StartCoroutine(SpawnEnemyWaveRoutine(new int[] { 7, 4, 4, 3, 3 }, fleetCount: 10, spawnInterval: 5f, waitForFullClear: false));
                StartCoroutine(CheckFlagshipHealthBelowPercent(0.1f));
                break;

            case ETutorialConditionType.FlagshipHealthBelowPercent:
                // conditionThreshold는 CSV에 비율(0~1)로 직접 기재 — 런타임 변환 없이 그대로 사용
                m_customConditionCoroutine = StartCoroutine(CheckFlagshipHealthBelowPercent(step.conditionThreshold));
                break;

            case ETutorialConditionType.SiegfriedFlagshipExplosion:
                m_customConditionCoroutine = StartCoroutine(PlaySiegfriedFlagshipExplosion(step.conditionThreshold));
                break;

            case ETutorialConditionType.CleanupEscapeFleet:
                CleanupEscapeFleet();
                NextStep();
                break;
        }
    }

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
    }

    // m_pendingNewShip이 대형 자리에 도착(Moving 상태 해제)할 때까지 대기
    private IEnumerator CheckShipArrivedAtFormation()
    {
        while (m_isPlaying)
        {
            if (m_pendingNewShip != null && m_pendingNewShip.m_formationMoveState != FormationMoveState.Moving)
            {
                m_pendingNewShip = null;
                NextStep();
                yield break;
            }
            yield return null;
        }
    }

    // maxCount 범위 안에서 비어있는(한 번도 안 쓰였거나, 배정된 함대가 전멸한) 가장 낮은 인덱스를 반환 — 자리가 없으면 -1
    private int GetLowestFreeWaveIndex(int maxCount)
    {
        for (int i = 0; i < maxCount; i++)
        {
            if (m_waveIndexOccupancy.TryGetValue(i, out SpaceFleet fleet) == false)
                return i;
            if (fleet == null || fleet.IsFleetAlive() == false)
                return i;
        }
        return -1;
    }

    // fleetCount개의 적 함대를 spawnInterval 간격으로 순차 스폰한 뒤, waitForFullClear가 true면 전부 전멸할 때까지 대기 후 NextStep
    // waitForFullClear가 false면 스폰만 담당하고 끝남 — 애초에 전멸 불가능한 물량용, 다음 스텝 전환은 별도 조건이 담당
    // positionIndex 자리가 꽉 차 있으면(그레이드 그룹의 프리셋 수보다 fleetCount가 많은 경우) 빈 자리가 날 때까지 대기 후 스폰
    private IEnumerator SpawnEnemyWaveRoutine(int[] shipGradeLevels, int fleetCount, float spawnInterval, bool waitForFullClear = true)
    {
        m_enemyWaveFleets.Clear();
        m_waveIndexOccupancy.Clear();

        int flagshipGrade = shipGradeLevels.Length > 0 ? shipGradeLevels[0] : 1;
        int maxPositions = DataManager.Instance.m_dataTableZone.GetFleetPositionCount(flagshipGrade);

        for (int i = 0; i < fleetCount; i++)
        {
            int positionIndex = GetLowestFreeWaveIndex(maxPositions);
            while (m_isPlaying && positionIndex < 0)
            {
                yield return null;
                positionIndex = GetLowestFreeWaveIndex(maxPositions);
            }
            if (!m_isPlaying) yield break;

            SpaceFleet fleet = TutorialCinematicController.SpawnEnemyWaveFleet(shipGradeLevels, positionIndex);
            if (fleet != null)
            {
                m_enemyWaveFleets.Add(fleet);
                m_waveIndexOccupancy[positionIndex] = fleet;
            }

            if (i < fleetCount - 1)
                yield return new WaitForSeconds(spawnInterval);
        }

        if (waitForFullClear == false) yield break;

        while (m_isPlaying)
        {
            bool anyAlive = false;
            foreach (SpaceFleet fleet in m_enemyWaveFleets)
            {
                if (fleet != null && fleet.IsFleetAlive())
                {
                    anyAlive = true;
                    break;
                }
            }

            if (anyAlive == false)
            {
                NextStep();
                yield break;
            }

            yield return null;
        }
    }

    // 내 함대(지크프리트) 기함 체력 비율이 ratioThreshold(0~1) 이하로 떨어질 때까지 대기
    private IEnumerator CheckFlagshipHealthBelowPercent(float ratioThreshold)
    {
        while (m_isPlaying)
        {
            SpaceFleet siegfriedFleet = ObjectManager.Instance.GetMyFleet();
            SpaceShip flagship = siegfriedFleet != null ? siegfriedFleet.GetFlagship() : null;
            if (flagship != null && flagship.IsAlive() == true)
            {
                float healthRatio = flagship.GetHealthRatio();
                if (healthRatio <= ratioThreshold)
                {
                    NextStep();
                    yield break;
                }
            }

            yield return null;
        }
    }

    // 카메라를 탈출 함선으로 전환하고, 지크프리트 기함을 연출로 파괴한 뒤 waitSeconds초 대기 후 다음 스텝
    private IEnumerator PlaySiegfriedFlagshipExplosion(float waitSeconds)
    {
        SpaceFleet siegfriedFleet = ObjectManager.Instance.GetMyFleet();
        SpaceShip flagship = siegfriedFleet != null ? siegfriedFleet.GetFlagship() : null;

        if (m_escapeFleet != null && CameraController.Instance != null)
        {
            CameraController.Instance.SetTargetOfCameraController(m_escapeFleet.transform);

            // 탈출선 정면 기준 좌로 10도, 위로 10도 꺾인 각도 + 그 함선의 최대 줌
            float escapeYaw = m_escapeFleet.transform.eulerAngles.y;
            CameraController.Instance.SetTargetRotation(escapeYaw + 180f + 30f, +30f);

            SpaceShip escapeShip = m_escapeFleet.GetFlagship();
            if (escapeShip != null && escapeShip.m_moduleBodys.Count > 0 && escapeShip.m_moduleBodys[0] != null)
            {
                CameraController.Instance.ApplyZoomRangeFromShip(escapeShip);
                CameraController.Instance.SetTargetZoom(escapeShip.m_moduleBodys[0].m_cameraMaxZoom);
            }
        }

        // 카메라가 탈출 함선으로 넘어갈 시간을 벌어준 뒤 기함 폭발
        yield return k_cameraTransitionWait;

        if (flagship != null)
            flagship.DestroyForCinematic();

        // 워프 배속(SpaceFleet.SpawnApproachSpeedMult, 기존 워프 진입과 동일한 배율)으로 가속 + 워프 이펙트
        // 탈출선은 화면 밖으로 사라지는 연출용일 뿐 실제 함대로 승격하지 않음 — Tutorial_FirstPlay_Complete까지 그대로 날아가다가
        // CleanupEscapeFleet()(StartNormalPlay 직전)에서 한 번에 정리됨
        SpaceShip escapeFlagship = m_escapeFleet != null ? m_escapeFleet.GetFlagship() : null;
        if (escapeFlagship != null)
        {
            m_escapeFleetSpeedMultiplier = m_escapeFleet.SpawnApproachSpeedMult;

            if (escapeFlagship.TryGetComponent(out m_escapeWarpEffect) == false)
            {
                m_escapeWarpEffect = escapeFlagship.gameObject.AddComponent<WarpEffectShip>();
                m_escapeWarpEffect.InitializeWarpEffect();
            }
            m_escapeWarpEffect.StartFleetWarpIn();

            // 카메라 lerp 추적 속도로는 워프 가속을 못 따라가 화면 밖으로 벗어나므로, 이 구간만 위치를 매 프레임 그대로 스냅
            if (CameraController.Instance != null)
                CameraController.Instance.SetInstantFollow(true);
        }

        yield return new WaitForSeconds(waitSeconds);

        // 튜토리얼 전투를 위해 존재했던 적 함대/발사체/함재기 전부 정리 (탈출선/워프 이펙트/이동은 유지)
        ObjectManager.Instance.RemoveAllEnemyFleets();
        ObjectManager.Instance.CleanupAllProjectiles();
        ObjectManager.Instance.DestroyAllAircraft();

        // 지크프리트 잔존 함대만 여기서 제거 — 탈출선은 Tutorial_FirstPlay_Complete가 끝날 때까지 유지
        ObjectManager.Instance.DestroyTutorialFleet(siegfriedFleet);

        NextStep();
    }

    // Tutorial_FirstPlay_Complete까지 끝난 뒤(StartNormalPlay 직전) 탈출선/워프이펙트/이동 코루틴을 한 번에 정리
    public void CleanupEscapeFleet()
    {
        if (m_escapeWarpEffect != null)
        {
            m_escapeWarpEffect.StopWarp();
            m_escapeWarpEffect = null;
        }

        if (CameraController.Instance != null)
            CameraController.Instance.SetInstantFollow(false);

        if (m_escapeFleetMoveCoroutine != null)
        {
            StopCoroutine(m_escapeFleetMoveCoroutine);
            m_escapeFleetMoveCoroutine = null;
        }

        if (m_escapeFleet != null)
        {
            TutorialCinematicController.DespawnCinematicFleet(m_escapeFleet);
            m_escapeFleet = null;
        }
    }

    // 탈출 함선을 계속 기존 방향/속도로 전진시킴 — step 전환과 무관하게 별도 코루틴으로 유지
    private IEnumerator MoveEscapeFleetForward()
    {
        while (m_isPlaying && m_escapeFleet != null)
        {
            m_escapeFleet.transform.position += m_escapeFleet.transform.forward * (ESCAPE_SHIP_SPEED * m_escapeFleetSpeedMultiplier * Time.deltaTime);
            yield return null;
        }
    }

    // 기함과의 거리가 threshold 이상 벌어질 때까지 대기 (이동 자체는 MoveEscapeFleetForward가 담당)
    private IEnumerator CheckEscapeShipDistance(SpaceFleet siegfriedFleet, float threshold)
    {
        while (m_isPlaying)
        {
            if (m_escapeFleet == null || siegfriedFleet == null)
            {
                yield return null;
                continue;
            }

            SpaceShip flagship = siegfriedFleet.GetFlagship();
            if (flagship != null)
            {
                float sqrDistance = (m_escapeFleet.transform.position - flagship.transform.position).sqrMagnitude;
                if (sqrDistance >= threshold * threshold)
                {
                    NextStep();
                    yield break;
                }
            }

            yield return null;
        }
    }

    private IEnumerator CheckCameraRotation(float threshold)
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
                NextStep();
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator CheckCameraZoom(float threshold)
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
                NextStep();
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
