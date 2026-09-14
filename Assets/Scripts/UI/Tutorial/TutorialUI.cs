using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 튜토리얼 UI 전체 관리
public class TutorialUI : UIPopupBase
{
    [Header("UI 요소")]
    [SerializeField] private TutorialTextBox m_textBox;
    [SerializeField] private TutorialArrow m_arrow;
    [SerializeField] private TutorialMask m_mask;
    [SerializeField] private UIBorderFrame m_borderFrame;
    [SerializeField] private Button m_skipButton;

    [Header("테두리 설정")]
    private float m_borderPadding = 10f;

    private RectTransform m_targetRect;
    private TutorialStep m_currentStep; // TargetClick 스텝 진행 중 m_targetRect가 파괴되면(풀링된 스크롤뷰 아이템 등) Update()에서 재조회하는 데 사용
    private Coroutine m_autoNextCoroutine;
    private Coroutine m_waitTargetCoroutine;

    // UI 캐시 (동일 UI 반복 검색 방지)
    private System.Collections.Generic.Dictionary<string, RectTransform> m_uiCache =
        new System.Collections.Generic.Dictionary<string, RectTransform>();

    // targetPanelName에 실제 GameObject 이름 대신 이 값을 쓰면 "RectTransform 없는 3D 그리드 셀을 화살표로 가리킴" 특수 케이스로 처리됨
    private const string GRID_CELL_HINT_PANEL = "ExplorationGridCell";

    protected override void Awake()
    {
        base.Awake();
        if (m_skipButton != null)
            m_skipButton.onClick.AddListener(OnSkipClicked);
    }

    // 스텝 표시
    public void ShowStep(TutorialStep step)
    {
        m_currentStep = step;

        // 진행 중인 코루틴 취소
        if (m_autoNextCoroutine != null)
        {
            StopCoroutine(m_autoNextCoroutine);
            m_autoNextCoroutine = null;
        }
        if (m_waitTargetCoroutine != null)
        {
            StopCoroutine(m_waitTargetCoroutine);
            m_waitTargetCoroutine = null;
        }

        // 이전 스텝의 마스크/화살표/테두리를 즉시 초기화 — 새 스텝 타겟을 아직 못 찾아 대기(WaitForTargetCoroutine)로
        // 빠지는 경우, 이걸 안 하면 이전 스텝의 강조 박스가 새 스텝 화면 위에 잔상처럼 계속 남아있게 됨
        if (m_mask != null) m_mask.HideDim();
        if (m_arrow != null) m_arrow.Hide();
        if (m_borderFrame != null) m_borderFrame.gameObject.SetActive(false);

        // 먼저 팝업 활성화 (자식 코루틴 사용 가능하도록)
        ShowPopup();

        // 대상 UI 찾기
        m_targetRect = FindTargetUI(step.targetUIId, step.targetPanelName);

        // 타겟이 지정되어 있는데 못 찾으면 대기
        if (!string.IsNullOrEmpty(step.targetUIId) && m_targetRect == null)
        {
            m_waitTargetCoroutine = StartCoroutine(WaitForTargetCoroutine(step));
            return;
        }

        DisplayStep(step);
    }

    // 타겟 UI 활성화 대기
    private IEnumerator WaitForTargetCoroutine(TutorialStep step)
    {
        float elapsed = 0f;
        const float maxWaitTime = 2f;
        const float checkInterval = 0.1f;

        while (elapsed < maxWaitTime)
        {
            yield return new WaitForSeconds(checkInterval);
            elapsed += checkInterval;

            m_targetRect = FindTargetUI(step.targetUIId, step.targetPanelName);
            if (m_targetRect != null)
                break;
        }

        m_waitTargetCoroutine = null;
        DisplayStep(step);
    }

    // 실제 스텝 UI 표시
    private void DisplayStep(TutorialStep step)
    {
        // 스킵 버튼 표시 여부 — 튜토리얼(TutorialData) 단위 설정
        if (m_skipButton != null)
            m_skipButton.gameObject.SetActive(TutorialManager.Instance.IsSkipButtonHiddenForCurrentTutorial() == false);

        // 레이아웃 강제 업데이트 (ContentSizeFitter/LayoutGroup 계산 완료 보장)
        if (m_targetRect != null)
            Canvas.ForceUpdateCanvases();

        // 텍스트 표시 (message를 Tutorial 테이블 키로 직접 사용 — 번역 누락 시 키가 그대로 노출되어 실수를 바로 알 수 있음)
        if (m_textBox != null)
        {
            string message = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.Get(step.message, "Tutorial")
                : step.message;
            m_textBox.ShowMessage(message, step.textBoxOffset, m_targetRect, step.textBoxSize, step.textBoxPosition);
        }

        // 마스크(강조) 표시
        if (m_mask != null)
        {
            if (m_targetRect != null)
                m_mask.ShowDimWithHole(m_targetRect); // targetUIId가 있으면 dim+hole 표시
            else
                m_mask.HideDim(); // targetUIId가 없으면 dim 없이 완전히 열림
        }

        // Custom 트리거(자동 진행 대기)는 클릭으로 진행되면 안 되므로 현재 열린 패널의 버튼 입력만 잠금 —
        // 화면 전체를 덮는 방식이 아니라 패널의 CanvasGroup만 건드리므로 3D 뷰 카메라 드래그는 그대로 동작함
        UIManager.Instance.SetTopPanelInteractable(step.triggerType != ETutorialTrigger.Custom);

        // 배치된 함선 "행 전체"를 타겟팅하는 스텝(targetUIId가 "[N]" 단독형 — 예: 함선 선택)에서는 그 안의
        // 함체교체 버튼/전후방 토글이 행 클릭보다 먼저 클릭을 가로채면 안 되므로 같이 잠금 — 그 외 스텝은 항상 해제
        UIPanelFleet fleetPanel = UIManager.Instance.GetPanel<UIPanelFleet>("UIPanelFleet");
        if (fleetPanel != null)
        {
            bool isBareRowTarget = step.targetPanelName == "PlacedShipsContent" && IsBareIndexTargetId(step.targetUIId);
            fleetPanel.SetPlacedShipRowActionsLocked(isBareRowTarget);
        }

        // dim 없는 스텝(m_targetRect == null)에서는 3D 조작은 열어두되 상단 탭 버튼 등 일반 UI는 차단
        EventManager.Trigger_TutorialGeneralUIBlockedChanged(m_targetRect == null);

        // 화살표 표시 — targetPanelName이 GRID_CELL_HINT_PANEL이면 RectTransform이 없는 3D 그리드 셀을 가리키는 특수 케이스
        // (dim/텍스트박스는 m_targetRect==null 그대로 유지되어 관여하지 않음, 화살표만 월드 좌표를 스크린 좌표로 재투영해 표시)
        if (m_arrow != null)
        {
            if (step.showArrow && step.targetPanelName == GRID_CELL_HINT_PANEL)
            {
                UIPanelExplorationGrid gridPanel = UIManager.Instance.GetPanel<UIPanelExplorationGrid>("UIPanelExplorationGrid");
                Vector3 hintWorldPos;
                if (gridPanel != null && gridPanel.TryGetAdjacentReachableCellWorldPos(out hintWorldPos))
                {
                    // Auto 기본값은 Up(화살표가 셀 위쪽에 떠서 아래로 셀을 가리킴) — 3D 대상은 half-extent를 몰라 자동판정 불가
                    EArrowDirection direction = step.arrowDirection == EArrowDirection.Auto ? EArrowDirection.Up : step.arrowDirection;
                    Camera worldCamera = CameraController.Instance != null ? CameraController.Instance.m_targetCamera : Camera.main;
                    m_arrow.ShowAtWorldPosition(hintWorldPos, worldCamera, direction);
                }
                else
                {
                    m_arrow.Hide();
                }
            }
            else if (step.showArrow && m_targetRect != null)
            {
                m_arrow.Show(m_targetRect, step.arrowDirection);
            }
            else
            {
                m_arrow.Hide();
            }
        }

        // 테두리 표시
        if (m_borderFrame != null)
        {
            if (m_targetRect != null)
            {
                RectTransform borderRect = m_borderFrame.rectTransform;

                // GetWorldCorners로 실제 렌더링된 크기/위치 계산 (LayoutGroup/ContentSizeFitter 대응)
                Vector3[] corners = new Vector3[4];
                m_targetRect.GetWorldCorners(corners);
                Vector3 center = (corners[0] + corners[2]) * 0.5f;

                // Canvas 스케일 보정 (월드 좌표 → 로컬 좌표)
                Vector2 size = new Vector2(
                    Mathf.Abs(corners[3].x - corners[0].x) / borderRect.lossyScale.x,
                    Mathf.Abs(corners[1].y - corners[0].y) / borderRect.lossyScale.y
                );

                borderRect.position = center;
                borderRect.sizeDelta = size + Vector2.one * m_borderPadding * 2;
                m_borderFrame.gameObject.SetActive(true);
            }
            else
            {
                m_borderFrame.gameObject.SetActive(false);
            }
        }

        // 트리거 설정 — 현재 스텝 UI 반영이 모두 끝난 뒤에 호출 (Custom 트리거가 NextStep()을 동기 재귀 호출해도
        // 다음 스텝 UI가 이 스텝의 나머지 코드에 의해 덮어써지지 않도록 항상 마지막에 실행)
        SetupStepTrigger(step);
    }

    // TargetClick 스텝 진행 중 m_targetRect가 파괴되면(예: InfiniteScrollView 풀 리빌드로 대상 오브젝트가 교체됨) 매 프레임 재조회해서
    // 마스크 훌/화살표/클릭핸들러를 새 인스턴스에 재연결 — Custom 트리거는 조건 코루틴 중복 시작을 피하기 위해 대상에서 제외
    private void Update()
    {
        if (m_currentStep == null) return;
        if (m_currentStep.triggerType != ETutorialTrigger.TargetClick) return;
        if (m_targetRect != null) return; // 파괴된 오브젝트 참조는 Unity가 자동으로 null 취급함

        RectTransform reResolved = FindTargetUI(m_currentStep.targetUIId, m_currentStep.targetPanelName);
        if (reResolved == null) return; // 아직 못 찾음 — 다음 프레임에 다시 시도

        m_targetRect = reResolved;
        if (m_mask != null) m_mask.ShowDimWithHole(m_targetRect);
        if (m_arrow != null && m_currentStep.showArrow) m_arrow.Show(m_targetRect, m_currentStep.arrowDirection);
        EnsureClickHandler(m_currentStep.targetUIId);
    }

    // 숨기기
    public void HideTutorialUI()
    {
        m_currentStep = null;

        if (m_autoNextCoroutine != null)
        {
            StopCoroutine(m_autoNextCoroutine);
            m_autoNextCoroutine = null;
        }
        if (m_waitTargetCoroutine != null)
        {
            StopCoroutine(m_waitTargetCoroutine);
            m_waitTargetCoroutine = null;
        }

        if (m_mask != null) m_mask.HideDim();
        if (m_arrow != null) m_arrow.Hide();
        if (m_borderFrame != null) m_borderFrame.gameObject.SetActive(false);
        UIManager.Instance.SetTopPanelInteractable(true); // 튜토리얼 종료/스킵 시 패널이 잠긴 채로 남지 않게 원복
        UIPanelFleet fleetPanel = UIManager.Instance.GetPanel<UIPanelFleet>("UIPanelFleet");
        if (fleetPanel != null) fleetPanel.SetPlacedShipRowActionsLocked(false);
        EventManager.Trigger_TutorialGeneralUIBlockedChanged(false); // 튜토리얼 종료 — 일반 UI 차단 해제
        HidePopup();
    }

    // targetUIId가 "[N]" 단독형(뒤에 "/추가경로" 없이 인덱스만)인지 확인 — 배치된 함선 "행 전체"를 타겟팅하는 스텝 판별용
    private bool IsBareIndexTargetId(string targetUIId)
    {
        if (string.IsNullOrEmpty(targetUIId)) return false;
        if (targetUIId[0] != '[') return false;

        int closeIdx = targetUIId.IndexOf(']');
        return closeIdx == targetUIId.Length - 1;
    }

    // 대상 UI 찾기
    private RectTransform FindTargetUI(string targetId, string panelName)
    {
        if (string.IsNullOrEmpty(targetId)) return null;

        // 캐시 확인
        string cacheKey = $"{panelName}/{targetId}";
        if (m_uiCache.TryGetValue(cacheKey, out RectTransform cached))
        {
            if (cached != null) return cached;
            m_uiCache.Remove(cacheKey);
        }

        // 패널에서 찾기
        GameObject panel = null;
        if (!string.IsNullOrEmpty(panelName))
            panel = GameObject.Find(panelName);

        Transform searchRoot = panel != null ? panel.transform : null;

        // 전체 Canvas에서 검색
        if (searchRoot == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
                searchRoot = canvas.transform;
        }

        if (searchRoot == null) return null;

        // "[N]/나머지경로" 형식이면 이름이 아니라 searchRoot의 N번째 자식으로 먼저 진입 — 유니티가 복제 시 자동으로
        // 붙이는 "Xxx (N)" 이름에 의존하지 않기 위함(그 이름은 하이러키 재배치/재복제 시 언제든 바뀔 수 있음).
        // 예: targetPanelName=ShipSelectorContainer, targetUIId=[4]/ShipSelectButton
        //     → ShipSelectorContainer의 5번째(0-based 4) 자식으로 들어간 뒤 그 안에서 "ShipSelectButton" 검색
        string remainingPath = targetId;
        if (remainingPath.Length > 0 && remainingPath[0] == '[')
        {
            int closeIdx = remainingPath.IndexOf(']');
            if (closeIdx > 0 && int.TryParse(remainingPath.Substring(1, closeIdx - 1), out int childIndex))
            {
                if (childIndex < 0 || childIndex >= searchRoot.childCount) return null;

                searchRoot = searchRoot.GetChild(childIndex);
                remainingPath = closeIdx + 1 < remainingPath.Length ? remainingPath.Substring(closeIdx + 2) : "";
            }
        }

        // 이름으로 검색 ("[N]"만 있고 나머지 경로가 없으면 그 자식 자체가 대상)
        Transform target = string.IsNullOrEmpty(remainingPath) ? searchRoot : FindChildRecursive(searchRoot, remainingPath);
        if (target == null) return null;

        RectTransform result = target.GetComponent<RectTransform>();
        if (result != null)
            m_uiCache[cacheKey] = result;

        return result;
    }

    // 재귀 자식 검색 (활성화된 오브젝트만)
    private Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name && parent.gameObject.activeInHierarchy)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (!child.gameObject.activeInHierarchy) continue;

            if (child.name == name) return child;

            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // 스텝 진행 트리거 설정
    private void SetupStepTrigger(TutorialStep step)
    {
        switch (step.triggerType)
        {
            case ETutorialTrigger.TargetClick:
                // 대상 UI 클릭을 TutorialClickHandler로 감지 — 없으면 자동 부착(에디터에서 수동으로 붙일 필요 없음)
                EnsureClickHandler(step.targetUIId);
                break;

            case ETutorialTrigger.AnyClick:
                // HandleInputMouse/HandleInputTouch가 release 시점에 TutorialManager.IsWaitingForAnyClick()을 직접 확인해서 소비함 —
                // 여기서는 별도로 할 일 없음(마스크 활성 상태와 무관하게 항상 작동)
                break;

            case ETutorialTrigger.AutoNext:
                m_autoNextCoroutine = StartCoroutine(AutoNextCoroutine(step.autoNextDelay));
                break;

            case ETutorialTrigger.Custom:
                // 마스크 dim/hole 상태는 위쪽 공통 로직(targetUIId 존재 여부)에서 이미 결정됨 — 여기선 건드리지 않음
                TutorialManager.Instance.StartTutorialCondition(step);
                break;
        }
    }

    // targetUIId로 찾은 UI에 TutorialClickHandler가 없으면 부착 — 매 스텝마다 에디터에서 수동으로 붙일 필요 없게 함
    private void EnsureClickHandler(string targetId)
    {
        if (m_targetRect == null) return;

        TutorialClickHandler handler = m_targetRect.GetComponent<TutorialClickHandler>();
        if (handler == null)
            handler = m_targetRect.gameObject.AddComponent<TutorialClickHandler>();
        handler.SetTargetId(targetId);
    }

    private IEnumerator AutoNextCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay > 0 ? delay : 2f);
        TutorialManager.Instance.NextStep();
    }

    private void OnSkipClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        TutorialManager.Instance.SkipTutorial();
    }

    // 캐시 클리어
    public void ClearCache()
    {
        m_uiCache.Clear();
    }
}
