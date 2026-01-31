using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 튜토리얼 UI 전체 관리
public class TutorialUI : UIPopupBase
{
    [Header("UI 요소")]
    [SerializeField] private TutorialTextBox m_textBox;
    //[SerializeField] private TutorialArrow m_arrow;
    [SerializeField] private TutorialMask m_mask;
    [SerializeField] private UIBorderFrame m_borderFrame;
    [SerializeField] private Button m_skipButton;
    [SerializeField] private CanvasGroup m_canvasGroup;

    [Header("테두리 설정")]
    [SerializeField] private float m_borderPadding = 8f;

    private TutorialStep m_currentStep;
    private RectTransform m_targetRect;
    private Coroutine m_autoNextCoroutine;

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
        m_currentStep = step;

        // 진행 중인 자동 진행 취소
        if (m_autoNextCoroutine != null)
        {
            StopCoroutine(m_autoNextCoroutine);
            m_autoNextCoroutine = null;
        }

        // 먼저 팝업 활성화 (자식 코루틴 사용 가능하도록)
        ShowPopup();

        // 대상 UI 찾기
        m_targetRect = FindTargetUI(step.targetUIId, step.targetPanelName);

        // 텍스트 표시
        if (m_textBox != null)
            m_textBox.ShowMessage(step.message, step.textBoxOffset, m_targetRect, step.textBoxSize);

        // // 화살표 표시
        // if (m_arrow != null)
        // {
        //     if (step.showArrow && m_targetRect != null)
        //         m_arrow.Show(m_targetRect, step.arrowDirection);
        //     else
        //         m_arrow.Hide();
        // }

        // 마스크(강조) 표시
        if (m_mask != null)
        {
            if (step.highlightTarget && m_targetRect != null)
                m_mask.HighlightTarget(m_targetRect);
            else if (m_targetRect == null)
                m_mask.ShowDimOnly(); // 스토리 텍스트용 (구멍 없이 전체 어둡게)
            else
                m_mask.HideHighlight();

            // 클릭 핸들러 설정
            SetupClickHandler(step);
        }

        // 테두리 표시
        if (m_borderFrame != null)
        {
            if (step.highlightTarget && m_targetRect != null)
            {
                RectTransform borderRect = m_borderFrame.rectTransform;
                borderRect.position = m_targetRect.TransformPoint(m_targetRect.rect.center);
                borderRect.sizeDelta = m_targetRect.sizeDelta + Vector2.one * m_borderPadding * 2;
                m_borderFrame.gameObject.SetActive(true);
            }
            else
            {
                m_borderFrame.gameObject.SetActive(false);
            }
        }

        

    }

    // 숨기기
    public void Hide()
    {
        if (m_autoNextCoroutine != null)
        {
            StopCoroutine(m_autoNextCoroutine);
            m_autoNextCoroutine = null;
        }

        //if (m_arrow != null) m_arrow.Hide();
        if (m_mask != null) m_mask.HideHighlight();
        if (m_borderFrame != null) m_borderFrame.gameObject.SetActive(false);
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

    // 재귀 자식 검색
    private Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name) return child;

            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // 클릭 핸들러 설정
    private void SetupClickHandler(TutorialStep step)
    {
        switch (step.triggerType)
        {
            case ETutorialTrigger.AnyClick:
                m_mask.SetClickable(true, () => TutorialManager.Instance.NextStep());
                break;

            case ETutorialTrigger.AutoNext:
                m_mask.SetClickable(false, null);
                m_autoNextCoroutine = StartCoroutine(AutoNextCoroutine(step.autoNextDelay));
                break;

            case ETutorialTrigger.UIClick:
                // 대상 UI 클릭을 TutorialClickHandler로 감지
                m_mask.SetClickable(false, null);
                break;

            case ETutorialTrigger.Custom:
                m_mask.SetClickable(false, null);
                break;
        }
    }

    private IEnumerator AutoNextCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay > 0 ? delay : 2f);
        TutorialManager.Instance.NextStep();
    }

    private void OnSkipClicked()
    {
        TutorialManager.Instance.SkipTutorial();
    }

    // 캐시 클리어
    public void ClearCache()
    {
        m_uiCache.Clear();
    }
}
