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
    private Coroutine m_autoNextCoroutine;
    private Coroutine m_waitTargetCoroutine;

    // UI 캐시 (동일 UI 반복 검색 방지)
    private System.Collections.Generic.Dictionary<string, RectTransform> m_uiCache =
        new System.Collections.Generic.Dictionary<string, RectTransform>();

    protected override void Awake()
    {
        base.Awake();
        if (m_skipButton != null)
            m_skipButton.onClick.AddListener(OnSkipClicked);
    }

    // 스텝 표시
    public void ShowStep(TutorialStep step)
    {
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

        // dim 없는 스텝(m_targetRect == null)에서는 3D 조작은 열어두되 상단 탭 버튼 등 일반 UI는 차단
        EventManager.Trigger_TutorialGeneralUIBlockedChanged(m_targetRect == null);

        // 화살표 표시
        if (m_arrow != null)
        {
            if (step.showArrow && m_targetRect != null)
                m_arrow.Show(m_targetRect, step.arrowDirection);
            else
                m_arrow.Hide();
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

    // 숨기기
    public void HideTutorialUI()
    {
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
        EventManager.Trigger_TutorialGeneralUIBlockedChanged(false); // 튜토리얼 종료 — 일반 UI 차단 해제
        HidePopup();
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

        // 이름으로 검색
        Transform target = FindChildRecursive(searchRoot, targetId);
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
