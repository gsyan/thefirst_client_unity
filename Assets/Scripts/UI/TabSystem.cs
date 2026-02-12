using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 탭 시스템: ButtonGroupSystem(버튼 선택 상태) + 패널 전환
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
    public int defaultActiveTab = 0;

    [Header("Animation Settings")]
    public bool useAnimation = true;
    public float animationDuration = 0.3f;
    public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private ButtonGroupSystem buttonGroup;
    private int currentActiveTab = -1;

    private void Start()
    {
        InitializeTabs();
    }

    private void InitializeTabs()
    {
        // ButtonGroupSystem 생성 및 설정
        buttonGroup = gameObject.AddComponent<ButtonGroupSystem>();
        buttonGroup.defaultIndex = defaultActiveTab;

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
                StartCoroutine(AnimatePanel(tab.tabPanel, true));
                tab.tabPanel.SetActive(true);
            }
            else
            {
                tab.tabPanel.SetActive(true);
            }
        }

        tab.onActivate?.Invoke();
    }

    private void DeactivatePanel(int tabIndex)
    {
        var tab = tabs[tabIndex];

        tab.onDeactivate?.Invoke();

        if (tab.tabPanel != null)
        {
            if (useAnimation)
                StartCoroutine(AnimatePanel(tab.tabPanel, false));
            else
                tab.tabPanel.SetActive(false);
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
