using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
//using UnityEngine.UIElements;

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
    
    private int currentActiveTab = -1;
    
    private void Start()
    {
        InitializeTabs();
        SwitchToTab(defaultActiveTab);
    }
    
    private void InitializeTabs()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            if (tabs[i].tabButton != null)
            {
                int tabIndex = i; // 클로저 문제 해결을 위한 로컬 변수
                tabs[i].tabButton.onClick.AddListener(() => SwitchToTab(tabIndex));
            }
            
            // 초기에는 모든 패널 비활성화
            if (tabs[i].tabPanel != null)
            {
                tabs[i].tabPanel.SetActive(false);
            }
        }

        m_bInitialized = true;
    }
    
    public void SwitchToTab(int tabIndex)
    {
        if (m_bInitialized == false) return;

        if (tabIndex < 0 || tabIndex >= tabs.Count)
        {
            Debug.LogWarning($"Invalid tab index: {tabIndex}");
            return;
        }
        
        if (tabIndex == currentActiveTab)
        {
            return; // 이미 활성화된 탭
        }
        
        // 이전 탭 비활성화
        if (currentActiveTab >= 0)
        {
            DeactivateTab(currentActiveTab);
        }
        
        // 새 탭 활성화
        ActivateTab(tabIndex);
        currentActiveTab = tabIndex;
        
        //Debug.Log($"Switched to tab: {tabs[tabIndex].tabName}");
    }
    
    private void ActivateTab(int tabIndex)
    {
        var tab = tabs[tabIndex];

        // 패널 활성화
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

        // 버튼 비주얼 업데이트
        UpdateButtonVisual(tab.tabButton, true, tab.activeColor);

        // 콜백 호출
        tab.onActivate?.Invoke();
    }
    
    private void DeactivateTab(int tabIndex)
    {
        var tab = tabs[tabIndex];

        // 콜백 호출 (패널 비활성화 전에)
        tab.onDeactivate?.Invoke();

        // 패널 비활성화
        if (tab.tabPanel != null)
        {
            if (useAnimation)
            {
                StartCoroutine(AnimatePanel(tab.tabPanel, false));
            }
            else
            {
                tab.tabPanel.SetActive(false);
            }
        }

        // 버튼 비주얼 업데이트
        UpdateButtonVisual(tab.tabButton, false, tab.inactiveColor);
    }
    
    private void UpdateButtonVisual(Button button, bool isActive, Color color)
    {
        if (button == null) return;
        
        // 버튼 색상 변경
        var colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = color * 1.1f;
        colors.pressedColor = color * 0.8f;
        button.colors = colors;
        
        // 텍스트 색상 변경 (있다면)
        var text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.color = isActive ? Color.black : Color.white;
        }
        
        // 스케일 효과 (선택사항)
        if (isActive)
        {
            button.transform.localScale = Vector3.one * 1.05f;
        }
        else
        {
            button.transform.localScale = Vector3.one;
        }
    }
    
    private System.Collections.IEnumerator AnimatePanel(GameObject panel, bool show)
    {
        if (show)
        {
            panel.SetActive(true);
            
            // 페이드 인 애니메이션
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
            // 페이드 아웃 애니메이션
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
        {
            canvasGroup = obj.AddComponent<CanvasGroup>();
        }
        return canvasGroup;
    }
    
    // 외부에서 호출할 수 있는 메서드들
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
    
    public int GetCurrentActiveTab()
    {
        return currentActiveTab;
    }
    
    public string GetCurrentActiveTabName()
    {
        if (currentActiveTab >= 0 && currentActiveTab < tabs.Count)
        {
            return tabs[currentActiveTab].tabName;
        }
        return "";
    }
    
    public void AddTab(TabData newTab)
    {
        tabs.Add(newTab);
        
        if (newTab.tabButton != null)
        {
            int tabIndex = tabs.Count - 1;
            newTab.tabButton.onClick.AddListener(() => SwitchToTab(tabIndex));
        }
        
        if (newTab.tabPanel != null)
        {
            newTab.tabPanel.SetActive(false);
        }
    }
    
    public void RemoveTab(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= tabs.Count) return;
        
        if (tabIndex == currentActiveTab)
        {
            // 현재 활성 탭을 제거하는 경우, 다른 탭으로 전환
            int newActiveTab = tabIndex > 0 ? tabIndex - 1 : (tabs.Count > 1 ? 1 : -1);
            if (newActiveTab >= 0)
            {
                SwitchToTab(newActiveTab);
            }
        }
        
        tabs.RemoveAt(tabIndex);
    }

    public void ForceActivateTab()
    {
        if( currentActiveTab == -1) return;
        // 강제로 onActivate 호출
        var tab = tabs[currentActiveTab];
        tab.onActivate?.Invoke();
    }

    public void ForceDeactivateTab()
    {
        if( currentActiveTab == -1) return;
        // 강제로 onDeactivate 호출
        var tab = tabs[currentActiveTab];
        tab.onDeactivate?.Invoke();
    }
}

// 에디터에서 쉽게 설정할 수 있도록 도우미 컴포넌트
[System.Serializable]
public class SimpleTab
{
    public string name;
    public Button button;
    public GameObject panel;
}