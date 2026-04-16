using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 탭 시스템: ButtonGroupSystem(버튼 선택 상태) + 패널 전환 / defaultActiveTab=-1이면 시작 시 탭 없음
// 빠른 탭 전환 시 코루틴 중복 실행 방지: 패널별 코루틴 추적 후 덮어쓰기
[System.Serializable]
public class TabData
{
    public Button tabButton;
    public GameObject tabPanel;
    public string tabName;
    [Header("Visual States")]
    public Color activeColor = Color.white;
    public Color inactiveColor = Color.gray;
    [Header("Callbacks")]
    public System.Action onActivate;
    public System.Action onDeactivate;
}

public class TabSystem : MonoBehaviour
{
    private bool m_bInitialized = false;

    [Header("Tab Configuration")]
    public List<TabData> tabs = new List<TabData>();
    // -1: 시작 시 탭 없음(전체 화면 3D), 0이상: 해당 탭 기본 활성
    public int defaultActiveTab = -1;
    // true면 현재 탭 재클릭 시 탭 닫힘(3D 복귀)
    public bool allowDeselect = true;

    [Header("Animation Settings")]
    public bool useAnimation = true;
    public float animationDuration = 0.3f;
    public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // 탭 인덱스 변경 시 호출 (-1이면 모든 탭 닫힘)
    public System.Action<int> onTabSelectionChanged;

    private ButtonGroupSystem buttonGroup;
    private int currentActiveTab = -1;
    private readonly Dictionary<GameObject, Coroutine> m_animCoroutines = new Dictionary<GameObject, Coroutine>();

    // 인스펙터에서 + 버튼으로 추가 시 색상 기본값 자동 적용
    private void OnValidate()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            var item = tabs[i];
            if (item.activeColor.a == 0f && item.activeColor.r == 0f && item.activeColor.g == 0f && item.activeColor.b == 0f)
                item.activeColor = new Color(1f, 0.8f, 0.2f, 1f);
            if (item.inactiveColor.a == 0f && item.inactiveColor.r == 0f && item.inactiveColor.g == 0f && item.inactiveColor.b == 0f)
                item.inactiveColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        }
    }

    private void Start()
    {
        InitializeTabs();
    }

    private void InitializeTabs()
    {
        // ButtonGroupSystem 생성 및 설정
        buttonGroup = gameObject.AddComponent<ButtonGroupSystem>();
        buttonGroup.defaultIndex = defaultActiveTab;
        buttonGroup.allowDeselect = allowDeselect;

        for (int i = 0; i < tabs.Count; i++)
        {
            int tabIndex = i;
            var tab = tabs[i];

            var item = new ButtonGroupItem
            {
                button = tab.tabButton,
                activeColor = tab.activeColor,
                inactiveColor = tab.inactiveColor,
                onSelected = () => ActivatePanel(tabIndex),
                onDeselected = () => DeactivatePanel(tabIndex)
            };
            buttonGroup.items.Add(item);

            // 초기에는 모든 패널 비활성화
            if (tab.tabPanel != null)
                tab.tabPanel.SetActive(false);
        }

        m_bInitialized = true;
        buttonGroup.Initialize();
    }

    public void SwitchToTab(int tabIndex)
    {
        if (!m_bInitialized) return;
        if (tabIndex < 0)
            buttonGroup.Deselect();
        else
            buttonGroup.Select(tabIndex);
    }

    private void ActivatePanel(int tabIndex)
    {
        var tab = tabs[tabIndex];
        currentActiveTab = tabIndex;

        if (tab.tabPanel != null)
        {
            if (useAnimation)
            {
                StopPanelCoroutine(tab.tabPanel);
                m_animCoroutines[tab.tabPanel] = StartCoroutine(AnimatePanel(tab.tabPanel, true));
            }
            else
            {
                tab.tabPanel.SetActive(true);
            }
        }

        tab.onActivate?.Invoke();
        onTabSelectionChanged?.Invoke(tabIndex);
    }

    private void DeactivatePanel(int tabIndex)
    {
        var tab = tabs[tabIndex];

        tab.onDeactivate?.Invoke();

        if (tab.tabPanel != null)
        {
            if (useAnimation)
            {
                StopPanelCoroutine(tab.tabPanel);
                m_animCoroutines[tab.tabPanel] = StartCoroutine(AnimatePanel(tab.tabPanel, false));
            }
            else
                tab.tabPanel.SetActive(false);
        }

        // allowDeselect로 전체 탭이 닫힌 경우
        if (buttonGroup.GetCurrentIndex() < 0)
        {
            currentActiveTab = -1;
            onTabSelectionChanged?.Invoke(-1);
        }
    }

    private System.Collections.IEnumerator AnimatePanel(GameObject panel, bool show)
    {
        if (show)
        {
            panel.SetActive(true);

            CanvasGroup canvasGroup = GetOrAddCanvasGroup(panel);
            canvasGroup.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / animationDuration;
                canvasGroup.alpha = animationCurve.Evaluate(progress);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }
        else
        {
            CanvasGroup canvasGroup = GetOrAddCanvasGroup(panel);
            canvasGroup.alpha = 1f;

            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / animationDuration;
                canvasGroup.alpha = 1f - animationCurve.Evaluate(progress);
                yield return null;
            }
            canvasGroup.alpha = 0f;
            panel.SetActive(false);
        }
    }

    private void StopPanelCoroutine(GameObject panel)
    {
        if (m_animCoroutines.TryGetValue(panel, out var existing) && existing != null)
            StopCoroutine(existing);
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject obj)
    {
        CanvasGroup canvasGroup = obj.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = obj.AddComponent<CanvasGroup>();
        return canvasGroup;
    }

    // 외부 API
    public void SwitchToTabByName(string tabName)
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i].tabName == tabName)
            {
                SwitchToTab(i);
                return;
            }
        }
        Debug.LogWarning($"Tab with name '{tabName}' not found");
    }

    // 열린 탭 전체 닫기 (3D 뷰 복귀)
    public void CloseAllTabs()
    {
        if (!m_bInitialized || currentActiveTab < 0) return;
        buttonGroup.Deselect();
    }

    public int GetCurrentActiveTab() => currentActiveTab;

    public string GetCurrentActiveTabName()
    {
        if (currentActiveTab >= 0 && currentActiveTab < tabs.Count)
            return tabs[currentActiveTab].tabName;
        return "";
    }

    public void AddTab(TabData newTab)
    {
        tabs.Add(newTab);

        int tabIndex = tabs.Count - 1;
        var item = new ButtonGroupItem
        {
            button = newTab.tabButton,
            activeColor = newTab.activeColor,
            inactiveColor = newTab.inactiveColor,
            onSelected = () => ActivatePanel(tabIndex),
            onDeselected = () => DeactivatePanel(tabIndex)
        };
        buttonGroup.items.Add(item);

        if (newTab.tabButton != null)
            newTab.tabButton.onClick.AddListener(() => buttonGroup.Select(tabIndex));

        if (newTab.tabPanel != null)
            newTab.tabPanel.SetActive(false);
    }

    public void RemoveTab(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= tabs.Count) return;

        if (tabIndex == currentActiveTab)
        {
            int newActiveTab = tabIndex > 0 ? tabIndex - 1 : (tabs.Count > 1 ? 1 : -1);
            if (newActiveTab >= 0)
                SwitchToTab(newActiveTab);
        }

        tabs.RemoveAt(tabIndex);
        buttonGroup.items.RemoveAt(tabIndex);
    }

    public void ForceActivateTab()
    {
        if (currentActiveTab == -1) return;
        tabs[currentActiveTab].onActivate?.Invoke();
    }

    public void ForceDeactivateTab()
    {
        if (currentActiveTab == -1) return;
        tabs[currentActiveTab].onDeactivate?.Invoke();
    }
}

[System.Serializable]
public class SimpleTab
{
    public string name;
    public Button button;
    public GameObject panel;
}
