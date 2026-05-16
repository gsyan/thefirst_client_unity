//------------------------------------------------------------------------------
using TMPro;
using System.Collections.Generic;
using UnityEngine;

public enum EPopupLayer
{
    Normal  = 0,  // 일반 팝업 (Confirm, Levelup, Formation 등)
    Overlay = 1,  // 오버레이 팝업 (Alert, 에러 알림 - Normal 위에 렌더)
    Count   = 2
}

public class UIManager : MonoSingleton<UIManager>
{
    #region MonoSingleton ---------------------------------------------------------------
    protected override bool ShouldDontDestroyOnLoad => false;

    protected override void OnInitialize()
    {

    }
    #endregion MonoSingleton ---------------------------------------------------------------

    [Header("Animation Settings")]
    protected bool useAnimation = true;
    protected float animationDuration = 0.3f;

    // Private fields
    private UIPanelBase currentActivePanel;
    private UIPanelBase mainPanel;
    private Dictionary<string, UIPanelBase> panelDictionary = new Dictionary<string, UIPanelBase>();
    private Stack<UIPanelBase> panelStack = new Stack<UIPanelBase>();

    // Popup Layer System
    private const string POPUP_PREFAB_PATH = "Prefabs/UI/Popup";
    private RectTransform[] m_popupContainers;                               // [EPopupLayer] → container
    private Stack<UIPopupBase>[] m_popupStacks;                              // [EPopupLayer] → active stack
    private readonly Dictionary<string, Queue<UIPopupBase>> m_popupPool = new(); // prefabName → free instances
    private Canvas mainCanvas;

    // UI 컨테이너
    protected RectTransform m_gaugeBarContainer;
    protected RectTransform m_generalContainer;
    protected RectTransform m_tutorialContainer;

    protected override void Awake()
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

    // 컨테이너 초기화 (하이라키 순서 = 렌더 순서)
    protected void InitializeContainers()
    {
        m_gaugeBarContainer = CreateContainer("UIGaugeBarContainer");
        m_generalContainer  = CreateContainer("UIGeneralContainer");
        m_tutorialContainer = CreateContainer("UITutorialContainer");

        int layerCount = (int)EPopupLayer.Count;
        m_popupContainers = new RectTransform[layerCount];
        m_popupStacks     = new Stack<UIPopupBase>[layerCount];

        m_popupContainers[(int)EPopupLayer.Normal]  = CreateContainer("UIPopupNormalContainer");
        m_popupContainers[(int)EPopupLayer.Overlay] = CreateContainer("UIPopupOverlayContainer");

        for (int i = 0; i < layerCount; i++)
            m_popupStacks[i] = new Stack<UIPopupBase>();
    }

    private RectTransform CreateContainer(string name)
    {
        GameObject containerObj = new(name);
        containerObj.transform.SetParent(transform, false);

        RectTransform rect = containerObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return rect;
    }

    public RectTransform GetGaugeBarContainer() => m_gaugeBarContainer;
    public RectTransform GetGeneralContainer()   => m_generalContainer;
    public RectTransform GetTutorialContainer()  => m_tutorialContainer;

    protected void InitializePanels()
    {
        foreach (var panel in panelDictionary.Values)
        {
            if (panel.gameObject != null)
            {
                if (panel.bMainPanel)
                {
                    if (mainPanel != null)
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

        if (targetPanel.bHideCurWhenActive == true)
        {
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

        if (show == true)
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
        if (currentActivePanel == null) return false;
        return currentActivePanel.bCameraMove;
    }

    // ---------------------------------------------------------------------------------
    // Popup Layer System
    // ---------------------------------------------------------------------------------

    // 팝업 인스턴스 취득: 풀에 여유 인스턴스 있으면 재사용, 없으면 새로 생성
    private T GetOrCreatePopup<T>(string prefabName, EPopupLayer layer) where T : UIPopupBase
    {
        if (m_popupPool.TryGetValue(prefabName, out Queue<UIPopupBase> pool) && pool.Count > 0)
        {
            UIPopupBase cached = pool.Dequeue();
            cached.gameObject.SetActive(true);
            return cached as T;
        }

        GameObject prefab = Resources.Load<GameObject>($"{POPUP_PREFAB_PATH}/{prefabName}");
        if (prefab == null)
        {
            Debug.LogError($"Failed to load popup prefab: {POPUP_PREFAB_PATH}/{prefabName}");
            return null;
        }

        RectTransform container = m_popupContainers[(int)layer];
        if (container == null)
        {
            Debug.LogError($"Popup container not found for layer: {layer}");
            return null;
        }

        GameObject obj = Instantiate(prefab, container);
        obj.name = prefab.name;
        if (obj.TryGetComponent(out T popup) == false)
        {
            Debug.LogError($"{typeof(T).Name} component not found on prefab: {prefabName}");
            Destroy(obj);
            return null;
        }

        obj.SetActive(true);
        return popup;
    }

    // 현재 레이어 top을 닫고 새 팝업으로 교체 (Normal 레이어 기본 동작)
    private void ReplacePopup(UIPopupBase popup, EPopupLayer layer)
    {
        CloseTopPopup(layer);
        m_popupStacks[(int)layer].Push(popup);
    }

    // 현재 레이어 top을 유지한 채 위에 새 팝업을 쌓음 (Overlay 기본 동작, 추후 Normal에도 사용 가능)
    private void PushPopup(UIPopupBase popup, EPopupLayer layer)
    {
        m_popupStacks[(int)layer].Push(popup);
    }

    // 레이어 top 팝업을 닫고 풀로 반환
    private void CloseTopPopup(EPopupLayer layer)
    {
        Stack<UIPopupBase> stack = m_popupStacks[(int)layer];
        if (stack.Count == 0) return;

        UIPopupBase popup = stack.Pop();
        ReturnToPool(popup);
    }

    private void ReturnToPool(UIPopupBase popup)
    {
        popup.HidePopup();
        string key = popup.gameObject.name;
        if (m_popupPool.TryGetValue(key, out Queue<UIPopupBase> pool) == false)
        {
            pool = new Queue<UIPopupBase>();
            m_popupPool[key] = pool;
        }
        pool.Enqueue(popup);
    }

    // ---------------------------------------------------------------------------------
    // Show Popup API
    // ---------------------------------------------------------------------------------

    // 확인 팝업
    public void ShowConfirmPopup(ConfirmPopupConfig config)
    {
        UIPopupConfirm popup = GetOrCreatePopup<UIPopupConfirm>("UIPopupConfirm", EPopupLayer.Normal);
        if (popup == null) return;

        ReplacePopup(popup, EPopupLayer.Normal);

        System.Action userConfirm = config.onConfirm;
        System.Action userCancel  = config.onCancel;
        config.onConfirm = () => { userConfirm?.Invoke(); CloseTopPopup(EPopupLayer.Normal); };
        config.onCancel  = () => { userCancel?.Invoke();  CloseTopPopup(EPopupLayer.Normal); };

        popup.ShowPopupConfirm(config);
    }

    // 단순 알림 팝업 (확인 버튼만) - Overlay 레이어: 다른 팝업 위에 쌓임
    // rewardAmounts: [mineral, techPoint, modulePoint, pvpPoint] 순, null이면 보상 섹션 숨김
    public void ShowPopupAlert(string title, string message, System.Action onConfirm, float autoCloseSec = 0f, System.Collections.Generic.List<int> rewardAmounts = null)
    {
        UIPopupAlert popup = GetOrCreatePopup<UIPopupAlert>("UIPopupAlert", EPopupLayer.Overlay);
        if (popup == null) return;

        PushPopup(popup, EPopupLayer.Overlay);

        void WrappedConfirm() { onConfirm?.Invoke(); CloseTopPopup(EPopupLayer.Overlay); }

        popup.ShowPopupAlert(title, message, WrappedConfirm, autoCloseSec, rewardAmounts);
    }

    // PvP 전체 랭킹 팝업
    public void ShowRankingPopup()
    {
        UIPopupRanking popup = GetOrCreatePopup<UIPopupRanking>("UIPopupRanking", EPopupLayer.Normal);
        if (popup == null) return;

        ReplacePopup(popup, EPopupLayer.Normal);
        popup.ShowPopupRanking(() => CloseTopPopup(EPopupLayer.Normal));
    }

    // 진형 선택 팝업
    public void ShowFormationPopup(EFormationType currentFormationType, System.Action<EFormationType> onSelected)
    {
        UIPopupFormation popup = GetOrCreatePopup<UIPopupFormation>("UIPopupFormation", EPopupLayer.Normal);
        if (popup == null) return;

        ReplacePopup(popup, EPopupLayer.Normal);
        popup.ShowPopup(currentFormationType, onSelected);
    }

    // 모듈 서브타입 관리 팝업 (서브타입 교체 선택)
    public void ShowModuleSubTypeManagePopup(ModuleBase sourceModule, System.Action<EModuleSubType> onConfirm)
    {
        UIPopupModuleSubTypeManage popup = GetOrCreatePopup<UIPopupModuleSubTypeManage>("UIPopupModuleSubTypeManage", EPopupLayer.Normal);
        if (popup == null) return;

        ReplacePopup(popup, EPopupLayer.Normal);
        popup.ShowPopup(sourceModule, onConfirm);
    }

    // 캐릭터 이름 변경 팝업
    public void ShowRenameCharacterPopup(System.Action onRenameSuccess = null)
    {
        UIPopupRenameCharacter popup = GetOrCreatePopup<UIPopupRenameCharacter>("UIPopupRenameCharacter", EPopupLayer.Normal);
        if (popup == null) return;

        ReplacePopup(popup, EPopupLayer.Normal);
        popup.ShowPopupRenameCharacter(
            onClose: () => CloseTopPopup(EPopupLayer.Normal),
            onRenameSuccess: onRenameSuccess
        );
    }

    // 외부 라이센스 고지 팝업
    public void ShowLicensePopup()
    {
        UIPopupLicense popup = GetOrCreatePopup<UIPopupLicense>("UIPopupLicense", EPopupLayer.Normal);
        if (popup == null) return;

        ReplacePopup(popup, EPopupLayer.Normal);
        popup.ShowPopupLicense(() => CloseTopPopup(EPopupLayer.Normal));
    }

    // 모듈/기술레벨 공용 레벨업 팝업
    public void ShowTechLevelupPopup(int currentTechLevel, System.Action<int> onConfirm)
    {
        UIPopupLevelup popup = GetOrCreatePopup<UIPopupLevelup>("UIPopupLevelup", EPopupLayer.Normal);
        if (popup == null) return;

        ReplacePopup(popup, EPopupLayer.Normal);
        popup.ShowTechLevel(currentTechLevel,
            onConfirm: targetLevel => { onConfirm?.Invoke(targetLevel); CloseTopPopup(EPopupLayer.Normal); },
            onCancel:  () => CloseTopPopup(EPopupLayer.Normal)
        );
    }

    public void ShowModuleLevelupPopup(EModuleSubType subType, EModuleType moduleType, int currentLevel, System.Action<int> onConfirm)
    {
        UIPopupLevelup popup = GetOrCreatePopup<UIPopupLevelup>("UIPopupLevelup", EPopupLayer.Normal);
        if (popup == null) return;

        ReplacePopup(popup, EPopupLayer.Normal);
        popup.ShowModule(subType, moduleType, currentLevel,
            onConfirm: targetLevel => { onConfirm?.Invoke(targetLevel); CloseTopPopup(EPopupLayer.Normal); },
            onCancel:  () => CloseTopPopup(EPopupLayer.Normal)
        );
    }
}
