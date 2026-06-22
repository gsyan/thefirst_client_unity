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
    private readonly Queue<ConfirmPopupConfig> m_confirmPopupQueue = new();  // 팝업 큐: 겹침 방지
    private bool m_isConfirmPopupShowing;
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

        GameObject prefab = ResourceManager.Instance.Load<GameObject>($"{POPUP_PREFAB_PATH}/{prefabName}");
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

    // 확인 팝업 — 이미 표시 중이면 큐에 적재, 닫힐 때 자동으로 다음 팝업 표시
    public void ShowConfirmPopup(ConfirmPopupConfig config)
    {
        m_confirmPopupQueue.Enqueue(config);
        if (m_isConfirmPopupShowing == false)
            ShowNextConfirmPopup();
    }

    private void ShowNextConfirmPopup()
    {
        if (m_confirmPopupQueue.Count == 0)
        {
            m_isConfirmPopupShowing = false;
            return;
        }

        m_isConfirmPopupShowing = true;
        ConfirmPopupConfig config = m_confirmPopupQueue.Dequeue();

        UIPopupConfirm popup = GetOrCreatePopup<UIPopupConfirm>("UIPopupConfirm", EPopupLayer.Overlay);
        if (popup == null) { ShowNextConfirmPopup(); return; }

        PushPopup(popup, EPopupLayer.Overlay);

        System.Action userConfirm = config.onConfirm;
        System.Action userCancel  = config.onCancel;
        config.onConfirm = () => { userConfirm?.Invoke(); CloseTopPopup(EPopupLayer.Overlay); ShowNextConfirmPopup(); };
        config.onCancel  = userCancel != null
            ? () => { userCancel.Invoke(); CloseTopPopup(EPopupLayer.Overlay); ShowNextConfirmPopup(); }
            : (System.Action)null;

        popup.ShowPopupConfirm(config);
    }

    // 기술 레벨업 알림 팝업 (서버 자동 레벨업 감지 시 호출)
    public void ShowTechLevelupNotify(int newLevel)
    {
        int shipCount = DataManager.Instance.m_dataTableTechLevel.GetShipCount(newLevel);
        var loc = LocalizationManager.Instance;
        string shipLabel  = loc.Get("UITabTech_ShipCountMaxTitle");
        string gradeLabel = loc.Get("UITabTech_ModuleGradeTitle");
        var rows = new List<(string icon, string value)>
        {
            ("icon_ship",   $"{shipLabel}  {shipCount}"),
            ("cargo-crane", $"{gradeLabel}  T.{newLevel}"),
        };
        ShowConfirmPopup(new ConfirmPopupConfig
        {
            title              = LocalizationManager.Instance.Get("UIPopupMessage_TechLevelupTitle"),
            resultRows         = rows,
            resultRowsVertical = true,
            onConfirm          = () => { },
            autoCloseSec       = 5f,
        });
    }

    // 일일 출석 보너스 달력 팝업 (수령 직후 호출)
    public void ShowDailyBonusPopup(int grantedMineral, System.Action onConfirm = null)
    {
        UIPopupDailyBonus popup = GetOrCreatePopup<UIPopupDailyBonus>("UIPopupDailyBonus", EPopupLayer.Overlay);
        if (popup == null) return;

        PushPopup(popup, EPopupLayer.Overlay);

        System.Action userConfirm = onConfirm;
        popup.ShowPopupDailyBonus(grantedMineral, () =>
        {
            userConfirm?.Invoke();
            CloseTopPopup(EPopupLayer.Overlay);
        });
    }

    // 일일 출석 보너스 달력 팝업 (수령 없이 열람용)
    public void ShowDailyBonusCalendar(System.Action onConfirm = null)
    {
        UIPopupDailyBonus popup = GetOrCreatePopup<UIPopupDailyBonus>("UIPopupDailyBonus", EPopupLayer.Overlay);
        if (popup == null) return;

        PushPopup(popup, EPopupLayer.Overlay);

        System.Action userConfirm = onConfirm;
        popup.ShowCalendarOnly(() =>
        {
            userConfirm?.Invoke();
            CloseTopPopup(EPopupLayer.Overlay);
        });
    }

    // 커맨더 이름 변경 팝업
    public void ShowRenameCommanderPopup(System.Action onRenameSuccess = null)
    {
        UIPopupRenameCommander popup = GetOrCreatePopup<UIPopupRenameCommander>("UIPopupRenameCommander", EPopupLayer.Normal);
        if (popup == null) return;

        ReplacePopup(popup, EPopupLayer.Normal);
        popup.ShowPopupRenameCommander(
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
