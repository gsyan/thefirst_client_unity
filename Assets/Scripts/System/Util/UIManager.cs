//------------------------------------------------------------------------------
using TMPro;
using System.Collections.Generic;
using UnityEngine;

// public enum EUIMode
// {
//     Main
//     , Fleet_Upgrade
//     , Fleet_Formation
//     , Fleet_Admiral
// }

public class UIManager : MonoSingleton<UIManager>
{
    #region MonoSingleton ---------------------------------------------------------------
    protected override bool ShouldDontDestroyOnLoad => false;

    protected override void OnInitialize()
    {

    }
    #endregion MonoSingleton ---------------------------------------------------------------

    //public TMP_Text m_resultText;
    //public EUIMode m_uiMode = EUIMode.Main;

    [Header("Animation Settings")]
    protected bool useAnimation = true;
    protected float animationDuration = 0.3f;

    // Private fields
    private UIPanelBase currentActivePanel;
    private UIPanelBase mainPanel;
    private Dictionary<string, UIPanelBase> panelDictionary = new Dictionary<string, UIPanelBase>();
    private Stack<UIPanelBase> panelStack = new Stack<UIPanelBase>();

    // Popup
    private const string POPUP_PREFAB_PATH = "Prefabs/UI/Popup";
    private UIPopupBase currentPopup;
    private Canvas mainCanvas;
    private Dictionary<string, UIPopupBase> popupCache = new Dictionary<string, UIPopupBase>();

    // UI 컨테이너
    protected RectTransform m_gaugeBarContainer;
    protected RectTransform m_generalContainer;
    protected RectTransform m_tutorialContainer;

    protected override void  Awake()
    {
        base.Awake();
        mainCanvas = GetComponentInParent<Canvas>();
        if (mainCanvas == null)
            mainCanvas = FindFirstObjectByType<Canvas>();
    }

    public virtual void InitializeUIManager()
    {
        InitializeContainers();
    }

    // 컨테이너 초기화 (하이라키 순서로 정렬)
    protected void InitializeContainers()
    {
        m_gaugeBarContainer = CreateContainer("UIGaugeBarContainer");
        m_generalContainer = CreateContainer("UIGeneralContainer");
        m_tutorialContainer = CreateContainer("UITutorialContainer");
    }

    private RectTransform CreateContainer(string name)
    {
        GameObject containerObj = new GameObject(name);
        containerObj.transform.SetParent(transform, false);

        RectTransform rect = containerObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return rect;
    }

    public RectTransform GetGaugeBarContainer() => m_gaugeBarContainer;
    public RectTransform GetGeneralContainer() => m_generalContainer;
    public RectTransform GetTutorialContainer() => m_tutorialContainer;

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
    
    // 이름으로 특정 패널을 숨김 (스택 관리 없이 단순 hide)
    public void HidePanel(string panelName)
    {
        if (!panelDictionary.ContainsKey(panelName)) return;
        var targetPanel = panelDictionary[panelName];
        if (!targetPanel.gameObject.activeInHierarchy) return;
        HidePanel(targetPanel);
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

    // 확인 팝업: detailText(null 허용), cost(null 허용)
    public void ShowConfirmPopup(string title, string message, string detailText, CostStruct cost, System.Action onConfirm, System.Action onCancel = null)
    {
        if (currentPopup != null)
            CloseCurrentPopup();

        UIPopupConfirm confirmPopup = GetOrCreatePopup<UIPopupConfirm>("UIPopupConfirm");
        if (confirmPopup == null) return;

        currentPopup = confirmPopup;

        System.Action wrappedConfirm = () =>
        {
            onConfirm?.Invoke();
            CloseCurrentPopup();
        };

        System.Action wrappedCancel = () =>
        {
            onCancel?.Invoke();
            CloseCurrentPopup();
        };

        confirmPopup.ShowPopupConfirm(title, message, detailText, cost, wrappedConfirm, wrappedCancel);
    }

    // 첫 호출 시 Instantiate 후 캐싱, 이후엔 SetActive(true)로 재사용
    private T GetOrCreatePopup<T>(string prefabName) where T : UIPopupBase
    {
        if (popupCache.TryGetValue(prefabName, out UIPopupBase cached))
        {
            cached.gameObject.SetActive(true);
            return cached as T;
        }

        GameObject prefab = Resources.Load<GameObject>($"{POPUP_PREFAB_PATH}/{prefabName}");
        if (prefab == null)
        {
            Debug.LogError($"Failed to load popup prefab: {POPUP_PREFAB_PATH}/{prefabName}");
            return null;
        }

        if (m_generalContainer == null)
        {
            Debug.LogError("GeneralContainer not found!");
            return null;
        }

        GameObject obj = Instantiate(prefab, m_generalContainer);
        obj.name = prefab.name;
        if (obj.TryGetComponent(out T popup) == false)
        {
            Debug.LogError($"{typeof(T).Name} component not found on prefab: {prefabName}");
            Destroy(obj);
            return null;
        }

        popupCache[prefabName] = popup;
        return popup;
    }

    private void CloseCurrentPopup()
    {
        if (currentPopup != null)
        {
            currentPopup.gameObject.SetActive(false);
            currentPopup = null;
        }
    }

    // PvP 전체 랭킹 팝업
    public void ShowRankingPopup()
    {
        if (currentPopup != null)
            CloseCurrentPopup();

        UIPopupRanking popup = GetOrCreatePopup<UIPopupRanking>("UIPopupRanking");
        if (popup == null) return;

        currentPopup = popup;
        popup.ShowPopupRanking(() => CloseCurrentPopup());
    }

    // 진형 선택 팝업
    public void ShowFormationPopup(EFormationType currentFormationType, System.Action<EFormationType> onSelected)
    {
        if (currentPopup != null)
            CloseCurrentPopup();

        UIPopupFormation popup = GetOrCreatePopup<UIPopupFormation>("UIPopupFormation");
        if (popup == null) return;

        currentPopup = popup;
        popup.ShowPopup(currentFormationType, onSelected);
    }

    // 모듈 서브타입 관리 팝업 (서브타입 교체 선택)
    public void ShowModuleSubTypeManagePopup(ModuleBase sourceModule, System.Action<EModuleSubType> onConfirm)
    {
        if (currentPopup != null)
            CloseCurrentPopup();

        UIPopupModuleSubTypeManage popup = GetOrCreatePopup<UIPopupModuleSubTypeManage>("UIPopupModuleSubTypeManage");
        if (popup == null) return;

        currentPopup = popup;
        popup.ShowPopup(sourceModule, onConfirm);
    }

    // 캐릭터 이름 변경 팝업
    public void ShowRenameCharacterPopup(System.Action onRenameSuccess = null)
    {
        if (currentPopup != null)
            CloseCurrentPopup();

        UIPopupRenameCharacter popup = GetOrCreatePopup<UIPopupRenameCharacter>("UIPopupRenameCharacter");
        if (popup == null) return;

        currentPopup = popup;
        popup.ShowPopupRenameCharacter(
            onClose: () => CloseCurrentPopup(),
            onRenameSuccess: onRenameSuccess
        );
    }

    // 외부 라이센스 고지 팝업
    public void ShowLicensePopup()
    {
        if (currentPopup != null)
            CloseCurrentPopup();

        UIPopupLicense popup = GetOrCreatePopup<UIPopupLicense>("UIPopupLicense");
        if (popup == null) return;

        currentPopup = popup;
        popup.ShowPopupLicense(() => CloseCurrentPopup());
    }

    // 단순 알림 팝업 (확인 버튼만)
    public void ShowAlertPopup(string title, string message, System.Action onConfirm, string buttonText = null)
    {
        if (currentPopup != null)
            CloseCurrentPopup();

        UIPopupAlert alertPopup = GetOrCreatePopup<UIPopupAlert>("UIPopupAlert");
        if (alertPopup == null) return;

        currentPopup = alertPopup;

        System.Action wrappedConfirm = () =>
        {
            onConfirm?.Invoke();
            CloseCurrentPopup();
        };

        alertPopup.ShowPopupAlert(title, message, wrappedConfirm, buttonText);
    }

}
