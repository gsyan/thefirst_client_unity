using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

// 튜토리얼 시스템 관리자
public class TutorialManager : MonoSingleton<TutorialManager>
{
    private const string TUTORIAL_PROGRESS_KEY = "TutorialProgress";
    private const string TUTORIAL_UI_PATH = "Prefabs/UI/Tutorial/UITutorial";

    private TutorialData m_currentTutorial;
    private int m_currentStepIndex;
    private bool m_isPlaying;
    private TutorialUI m_tutorialUI;
    private HashSet<string> m_completedTutorials = new HashSet<string>();
    private Dictionary<string, int> m_tutorialProgress = new Dictionary<string, int>();
    private System.Action<string> m_onCompleteCallback;

    // Tutorial 조건용 상태
    private Coroutine m_customConditionCoroutine;
    private HashSet<int> m_selectedModuleIds = new HashSet<int>();  // 선택된 모듈 InstanceID
    private float m_cameraRotationAccumulated;
    private Quaternion m_lastCameraRotation;
    private float m_cameraZoomAccumulated;
    private float m_lastCameraZoom;

    // 튜토리얼 완료 이벤트 (tutorialId 전달)
    public event System.Action<string> OnTutorialCompleted;

    protected override bool ShouldDontDestroyOnLoad => true;

    protected override void OnInitialize()
    {
        LoadProgressFromCache();
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

        // 이전 진행 상태 복원
        m_currentStepIndex = GetTutorialProgress(tutorialId);
        if (m_currentStepIndex >= m_currentTutorial.steps.Count)
        {
            CompleteTutorial();
            return;
        }

        m_isPlaying = true;
        EnsureTutorialUI();
        ExecuteCurrentStep();
    }

    // 다음 스텝
    public void NextStep()
    {
        if (!m_isPlaying) return;

        m_currentStepIndex++;
        SaveProgressToCache();

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
            m_completedTutorials.Add(m_currentTutorial.tutorialId);
            m_tutorialProgress.Remove(m_currentTutorial.tutorialId);
        }

        m_isPlaying = false;
        m_tutorialUI?.Hide();
        SaveProgressToCache();

        Debug.Log($"[Tutorial] 완료: {completedId}");
        m_currentTutorial = null;

        // 콜백 호출 (콜백 내에서 새 튜토리얼 시작 시 덮어쓰기 방지)
        var callback = m_onCompleteCallback;
        m_onCompleteCallback = null;
        callback?.Invoke(completedId);

        // 이벤트 발생
        OnTutorialCompleted?.Invoke(completedId);
    }

    // 튜토리얼 UI 생성
    private void EnsureTutorialUI()
    {
        if (m_tutorialUI != null) return;

        GameObject prefab = Resources.Load<GameObject>(TUTORIAL_UI_PATH);
        if (prefab == null)
        {
            Debug.LogError($"[Tutorial] UI 프리팹을 찾을 수 없음: {TUTORIAL_UI_PATH}");
            return;
        }

        // UITutorialContainer에 생성
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
        return Resources.Load<TutorialData>(path);
    }

    // 진행 상태 가져오기
    private int GetTutorialProgress(string tutorialId)
    {
        return m_tutorialProgress.TryGetValue(tutorialId, out int progress) ? progress : 0;
    }

    // 로컬 캐시 저장
    private void SaveProgressToCache()
    {
        if (m_currentTutorial != null && m_isPlaying)
        {
            m_tutorialProgress[m_currentTutorial.tutorialId] = m_currentStepIndex;
        }

        TutorialSaveData saveData = new TutorialSaveData
        {
            completedTutorials = new List<string>(m_completedTutorials),
            progressData = new List<TutorialProgressEntry>()
        };

        foreach (var kvp in m_tutorialProgress)
        {
            saveData.progressData.Add(new TutorialProgressEntry
            {
                tutorialId = kvp.Key,
                stepIndex = kvp.Value
            });
        }

        string json = JsonConvert.SerializeObject(saveData);
        PlayerPrefs.SetString(TUTORIAL_PROGRESS_KEY, json);
        PlayerPrefs.Save();
    }

    // 로컬 캐시 로드
    private void LoadProgressFromCache()
    {
        m_completedTutorials.Clear();
        m_tutorialProgress.Clear();

        if (!PlayerPrefs.HasKey(TUTORIAL_PROGRESS_KEY)) return;

        try
        {
            string json = PlayerPrefs.GetString(TUTORIAL_PROGRESS_KEY);
            TutorialSaveData saveData = JsonConvert.DeserializeObject<TutorialSaveData>(json);

            if (saveData.completedTutorials != null)
            {
                foreach (string id in saveData.completedTutorials)
                    m_completedTutorials.Add(id);
            }

            if (saveData.progressData != null)
            {
                foreach (var entry in saveData.progressData)
                    m_tutorialProgress[entry.tutorialId] = entry.stepIndex;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Tutorial] 캐시 로드 실패: {e.Message}");
            PlayerPrefs.DeleteKey(TUTORIAL_PROGRESS_KEY);
        }
    }

    // 디버그: 튜토리얼 초기화
    public void ResetAllTutorials()
    {
        m_completedTutorials.Clear();
        m_tutorialProgress.Clear();
        PlayerPrefs.DeleteKey(TUTORIAL_PROGRESS_KEY);
        PlayerPrefs.Save();
        Debug.Log("[Tutorial] 모든 튜토리얼 초기화됨");
    }

    #region Tutorial Condition

    // 튜토리얼 조건 시작 (TutorialUI에서 호출)
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
                EventManager.Subscribe_SpaceShipModuleSelected_TabUpgrade(OnModuleSelected);
                break;
        }
    }

    // 튜토리얼 조건 정리
    public void StopTutorialCondition()
    {
        if (m_customConditionCoroutine != null)
        {
            StopCoroutine(m_customConditionCoroutine);
            m_customConditionCoroutine = null;
        }

        EventManager.Unsubscribe_SpaceShipModuleSelected_TabUpgrade(OnModuleSelected);
        m_selectedModuleIds.Clear();
        m_cameraRotationAccumulated = 0f;
    }

    // 카메라 회전량 체크 코루틴
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

    // 카메라 줌 변화량 체크 코루틴
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

    // 모듈 선택 이벤트 콜백
    private void OnModuleSelected(SpaceShip ship, ModuleBase module)
    {
        if (!m_isPlaying || m_currentTutorial == null) return;
        if (m_currentStepIndex >= m_currentTutorial.steps.Count) return;

        TutorialStep step = m_currentTutorial.steps[m_currentStepIndex];
        if (step.triggerType != ETutorialTrigger.Custom) return;

        switch (step.conditionType)
        {
            case ETutorialConditionType.ModuleSelected:
                // 아무 모듈이나 선택하면 완료
                NextStep();
                break;

            case ETutorialConditionType.ModuleSelectedCount:
                // 서로 다른 모듈 N개 선택
                int moduleId = module.GetInstanceID();
                if (m_selectedModuleIds.Add(moduleId))
                {
                    Debug.Log($"[Tutorial] 모듈 선택 {m_selectedModuleIds.Count}/{step.conditionCount}");
                    if (m_selectedModuleIds.Count >= step.conditionCount)
                        NextStep();
                }
                break;

            case ETutorialConditionType.SpecificModuleSelected:
                // 특정 모듈 선택 (classId 비교)
                if (module.GetModuleType() == step.targetModuleType)
                    NextStep();
                break;
        }
    }

    #endregion
}

// 저장 데이터 구조
[System.Serializable]
public class TutorialSaveData
{
    public List<string> completedTutorials;
    public List<TutorialProgressEntry> progressData;
}

[System.Serializable]
public class TutorialProgressEntry
{
    public string tutorialId;
    public int stepIndex;
}
