//------------------------------------------------------------------------------
using TMPro;
using System.Collections.Generic;
using UnityEngine;

public enum EUIMode
{
    Main
    , Fleet_Upgrade
    , Fleet_Formation
    , Fleet_Admiral
}

public class UIManager : MonoSingleton<UIManager>
{
    #region MonoSingleton ---------------------------------------------------------------
    protected override bool ShouldDontDestroyOnLoad => false;

    protected override void OnInitialize()
    {

    }
    #endregion MonoSingleton ---------------------------------------------------------------

    public TMP_Text m_resultText;
    public EUIMode m_uiMode = EUIMode.Main;

    [Header("Animation Settings")]
    protected bool useAnimation = true;
    protected float animationDuration = 0.3f;

    protected UIPanelMoney m_uiPanelMoney;


    // Private fields
    private UIPanelBase currentActivePanel;
    private UIPanelBase mainPanel;
    private Dictionary<string, UIPanelBase> panelDictionary = new Dictionary<string, UIPanelBase>();
    private Stack<UIPanelBase> panelStack = new Stack<UIPanelBase>();

    protected override void  Awake()
    {
        base.Awake();
    }

    public virtual void InitializeUIManager()
    {
    }

    protected void InitializePanels()
    {
        // 모든 패널을 비활성화하고 메인 패널 찾기
        foreach (var panel in panelDictionary.Values)
        {
            if (panel.gameObject != null)
            {
                if (panel.bMainPanel)
                {
                    if( mainPanel != null)
                        Debug.LogError("main UIPanel is Two more!!");
                    else
                        mainPanel = panel;
                }
                panel.gameObject.SetActive(false);
            }
        }
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
        if (currentActivePanel == targetPanel && targetPanel.gameObject.activeInHierarchy) return;

        if(targetPanel.bHideCurWhenActive == true)
        {
            // 현재 활성 패널이 있으면 숨기고 스택에 추가
            if (currentActivePanel != null)
            {
                HidePanel(currentActivePanel);
                panelStack.Push(currentActivePanel);
            }

            currentActivePanel = targetPanel;
        }

        ShowPanel(targetPanel);
    }
    
    public void ShowMainPanel()
    {
        if (mainPanel != null)
            ShowPanel(mainPanel.panelName);
    }
    
    public void HideCurrentPanel()
    {
        if (currentActivePanel != null && !currentActivePanel.bMainPanel)
        {
            HidePanel(currentActivePanel);

            // 스택에서 이전 패널을 꺼내서 보여줌
            if (panelStack.Count > 0)
            {
                UIPanelBase previousPanel = panelStack.Pop();
                currentActivePanel = previousPanel;
                ShowPanel(previousPanel);
            }
            else
            {
                currentActivePanel = null;
                ShowMainPanel();
            }
        }
    }
    
    public void TogglePanel(string panelName)
    {
        if (!panelDictionary.ContainsKey(panelName)) return;
        var targetPanel = panelDictionary[panelName];
        
        if (currentActivePanel == targetPanel && targetPanel.gameObject.activeInHierarchy)
            ShowMainPanel();
        else
            ShowPanel(panelName);
    }
    
    private void ShowPanel(UIPanelBase panel)
    {
        if (panel.gameObject == null) return;
        
        if (useAnimation == true)
            StartCoroutine(AnimatePanel(panel.gameObject, true));
        else
            panel.gameObject.SetActive(true);
        
        panel.OnShowUIPanel();
    }
    
    private void HidePanel(UIPanelBase panel)
    {
        if (panel == null || panel.gameObject == null) return;
        
        if (useAnimation == true)
            StartCoroutine(AnimatePanel(panel.gameObject, false));
        else
            panel.gameObject.SetActive(false);
        
        panel.OnHideUIPanel();
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
    public void AddPanel(UIPanelBase panelBase)
    {
        panelDictionary[panelBase.panelName] = panelBase;
        panelBase.gameObject.SetActive(false);
    }
    
    public void RemovePanel(string panelName)
    {
        if (panelDictionary.ContainsKey(panelName))
        {
            var panel = panelDictionary[panelName];

            // 현재 활성 패널이면 메인으로 전환
            if (currentActivePanel == panel)
                ShowMainPanel();

            panelDictionary.Remove(panelName);

            Debug.Log($"Removed panel: {panelName}");
        }
    }
    
    public bool IsPanelActive(string panelName)
    {
        if (panelDictionary.ContainsKey(panelName))
            return panelDictionary[panelName].gameObject.activeInHierarchy;
        return false;
    }
    
    public string GetCurrentActivePanelName()
    {
        return currentActivePanel?.panelName ?? "";
    }
    
    public bool IsMainPanelActive()
    {
        return mainPanel != null && mainPanel.gameObject.activeInHierarchy;
    }

    public bool CanCameraMove()
    {
        if(currentActivePanel == null) return false;
        return currentActivePanel.bCameraMove;
    }


}
