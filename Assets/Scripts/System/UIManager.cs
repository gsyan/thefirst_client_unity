//------------------------------------------------------------------------------
using TMPro;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class UIPanel
{
    public string panelName;
    public GameObject panelObject;
    public bool isMainPanel = false;
    public bool hideMainWhenActive = true; // 이 패널이 활성화될 때 메인 패널을 숨길지 여부
}

public class UIManager : MonoSingleton<UIManager>
{
    #region MonoSingleton ---------------------------------------------------------------
    protected override bool ShouldDontDestroyOnLoad => false;

    protected override void OnInitialize()
    {

    }
    #endregion

    public TMP_Text m_resultText;
    

    [Header("Panel Configuration")]
    public List<UIPanel> panels = new List<UIPanel>();

    [Header("Animation Settings")]
    public bool useAnimation = true;
    public float animationDuration = 0.3f;

    public UIPanelMoney m_uiPanelMoney;
    
    
    // Private fields
    private UIPanel currentActivePanel;
    private UIPanel mainPanel;
    private Dictionary<string, UIPanel> panelDictionary = new Dictionary<string, UIPanel>();

    protected override void  Awake()
    {
        base.Awake();
    }

    public void InitializePanels()
    {
        // 패널 딕셔너리 구성
        panelDictionary.Clear();
        
        foreach (var panel in panels)
        {
            if (panel.panelObject != null)
            {
                if (string.IsNullOrEmpty(panel.panelName))
                    panel.panelName = panel.panelObject.name;
                panelDictionary[panel.panelName] = panel;
                
                if (panel.isMainPanel)
                    mainPanel = panel;

                panel.panelObject.SetActive(false);
            }
        }
        
        if (mainPanel == null && panels.Count > 0)
        {
            mainPanel = panels[0];
            mainPanel.isMainPanel = true;
        }
    }
    
    protected virtual void Start()
    {
    }

    public void ShowDefaultPanel()
    {
        if (mainPanel != null)
            ShowPanel(mainPanel.panelName);
    }    
    public void ShowPanel(string panelName)
    {
        if (!panelDictionary.ContainsKey(panelName)) return;
        var targetPanel = panelDictionary[panelName];        
        if (currentActivePanel == targetPanel && targetPanel.panelObject.activeInHierarchy) return;

        if (currentActivePanel != null)
            HidePanel(currentActivePanel);
        
        // 메인 패널이 아닌 패널을 표시할 때 메인 패널 숨김
        if (!targetPanel.isMainPanel && targetPanel.hideMainWhenActive && mainPanel != null)
            HidePanel(mainPanel);
        
        ShowPanel(targetPanel);
        currentActivePanel = targetPanel;
    }
    
    public void ShowMainPanel()
    {
        if (mainPanel != null)
            ShowPanel(mainPanel.panelName);
    }
    
    public void HideCurrentPanel()
    {
        if (currentActivePanel != null && !currentActivePanel.isMainPanel)
        {
            HidePanel(currentActivePanel);
            currentActivePanel = null;
            ShowMainPanel();
        }
    }
    
    public void TogglePanel(string panelName)
    {
        if (!panelDictionary.ContainsKey(panelName)) return;
        var targetPanel = panelDictionary[panelName];
        
        if (currentActivePanel == targetPanel && targetPanel.panelObject.activeInHierarchy)
            ShowMainPanel();
        else
            ShowPanel(panelName);
    }
    
    private void ShowPanel(UIPanel panel)
    {
        if (panel.panelObject == null) return;
        
        if (useAnimation == true)
            StartCoroutine(AnimatePanel(panel.panelObject, true));
        else
            panel.panelObject.SetActive(true);
    }
    
    private void HidePanel(UIPanel panel)
    {
        if (panel.panelObject == null) return;
        
        if (useAnimation == true)
            StartCoroutine(AnimatePanel(panel.panelObject, false));
        else
            panel.panelObject.SetActive(false);
    }
    
    private System.Collections.IEnumerator AnimatePanel(GameObject panel, bool show)
    {
        CanvasGroup canvasGroup = GetOrAddCanvasGroup(panel);
        
        if (show)
        {
            panel.SetActive(true);
            canvasGroup.alpha = 0f;
            
            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / animationDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }
        else
        {
            canvasGroup.alpha = 1f;
            
            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / animationDuration);
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
    
    // 새로운 패널을 동적으로 추가하는 메서드, 외부에서 사용할 수 있는 유틸리티 메서드들
    public void AddPanel(string panelName, GameObject panelObject, bool hideMainWhenActive = true)
    {
        var newPanel = new UIPanel
        {
            panelName = panelName,
            panelObject = panelObject,
            isMainPanel = false,
            hideMainWhenActive = hideMainWhenActive
        };
        
        panels.Add(newPanel);
        panelDictionary[panelName] = newPanel;
        panelObject.SetActive(false);
        
        Debug.Log($"Added new panel: {panelName}");
    }
    
    public void RemovePanel(string panelName)
    {
        if (panelDictionary.ContainsKey(panelName))
        {
            var panel = panelDictionary[panelName];
            
            // 현재 활성 패널이면 메인으로 전환
            if (currentActivePanel == panel)
                ShowMainPanel();
            
            panels.Remove(panel);
            panelDictionary.Remove(panelName);
            
            Debug.Log($"Removed panel: {panelName}");
        }
    }
    
    public bool IsPanelActive(string panelName)
    {
        if (panelDictionary.ContainsKey(panelName))
            return panelDictionary[panelName].panelObject.activeInHierarchy;
        return false;
    }
    
    public string GetCurrentActivePanelName()
    {
        return currentActivePanel?.panelName ?? "";
    }
    
    public bool IsMainPanelActive()
    {
        return mainPanel != null && mainPanel.panelObject.activeInHierarchy;
    }
    
}
