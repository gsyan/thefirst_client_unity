//------------------------------------------------------------------------------
using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    private RectTransform m_safeAreaRoot;                                    // 동적 컨테이너들의 공통 부모, SafeAreaAdapter 부착

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

    // 곡면(엣지) 디스플레이 대비 좌우 마진 비율 — SafeAreaRoot·레터박스 커버 바·GaugeBar 컬링이 공유
    public const float CURVED_EDGE_MARGIN = 0.02f;

    // 컨테이너 초기화 (하이라키 순서 = 렌더 순서)
    protected void InitializeContainers()
    {
        m_safeAreaRoot = CreateSafeAreaRoot();

        m_gaugeBarContainer = CreateContainer("UIGaugeBarContainer");
        m_generalContainer  = CreateContainer("UIGeneralContainer");

        int layerCount = (int)EPopupLayer.Count;
        m_popupContainers = new RectTransform[layerCount];
        m_popupStacks     = new Stack<UIPopupBase>[layerCount];

        m_popupContainers[(int)EPopupLayer.Normal]  = CreateContainer("UIPopupNormalContainer");
        m_popupContainers[(int)EPopupLayer.Overlay] = CreateContainer("UIPopupOverlayContainer");

        for (int i = 0; i < layerCount; i++)
            m_popupStacks[i] = new Stack<UIPopupBase>();

        m_tutorialContainer = CreateContainer("UITutorialContainer");

        // 커브드 엣지 마진으로 새는 UI(게이지바, 튜토리얼 화살표 등)를 가리기 위해 항상 최상단에 위치해야 함
        CreateLetterboxCover();
    }

    // 곡면(엣지) 화면 대비 안전영역 부모 — SafeAreaAdapter가 여기서 anchor를 깎음
    private RectTransform CreateSafeAreaRoot()
    {
        GameObject rootObj = new("SafeAreaRoot");
        rootObj.transform.SetParent(transform, false);

        RectTransform rect = rootObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // 곡면(엣지) 디스플레이는 Screen.safeArea로 안 잡히는 기종이 있어 좌우 고정 마진 추가
        rootObj.AddComponent<SafeAreaAdapter>().SetExtraMargins(CURVED_EDGE_MARGIN, CURVED_EDGE_MARGIN, 0f, 0f);

        return rect;
    }

    // 곡면(엣지) 영역을 카메라 rect를 좁혀서 가리면, 카메라가 그리지 않는 그 스트립을 아무도 매 프레임 지우지 않아
    // 그 위를 지나간 UI(체력바 등)가 잔상으로 남는 문제가 있었음 — 카메라는 항상 풀스크린으로 그리게 두고,
    // 대신 화면 절대 좌표 기준 불투명 바로 가려서 커브드 엣지를 숨김 (매 프레임 Canvas가 정상적으로 다시 그려주므로 잔상 없음)
    private void CreateLetterboxCover()
    {
        CreateLetterboxBar("LetterboxBarLeft",  Vector2.zero,                              new Vector2(CURVED_EDGE_MARGIN, 1f));
        CreateLetterboxBar("LetterboxBarRight", new Vector2(1f - CURVED_EDGE_MARGIN, 0f),   Vector2.one);
    }

    private void CreateLetterboxBar(string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject barObj = new(name);
        barObj.transform.SetParent(transform, false);

        RectTransform rect = barObj.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = barObj.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
    }

    private RectTransform CreateContainer(string name)
    {
        GameObject containerObj = new(name);
        containerObj.transform.SetParent(m_safeAreaRoot, false);

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

    // panelDictionary는 UIPanelBase로만 저장되어 있어, 개별 패널 고유 API(버튼 콜백 주입 등)를 호출하려면 구체 타입으로 캐스팅해서 꺼내야 함
    public T GetPanel<T>(string panelName) where T : UIPanelBase
    {
        if (panelDictionary.TryGetValue(panelName, out UIPanelBase panel) == false) return null;
        return panel as T;
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

    // 커맨더 레벨업 알림 팝업 (서버 자동 레벨업 감지 시 호출)
    public void ShowCommanderLevelupNotify(int newLevel)
    {
        SoundManager.Instance.PlayFX(EFx.Commander_Level_Up);
        int prevLevel        = newLevel - 1;
        int shipCount        = DataManager.Instance.m_dataTableCommander.GetShipCount(newLevel);
        int prevShipCount    = DataManager.Instance.m_dataTableCommander.GetShipCount(prevLevel);
        var loc = LocalizationManager.Instance;
        string shipLabel   = loc.Get("UITabCommander_ShipCountMaxTitle");
        Color defaultColor = CommonUtility.PaletteColor("GeneralBright1");
        var rows = new List<(string icon, string value, Color? color)>();
        if (shipCount != prevShipCount)
            rows.Add(("icon_ship", $"{shipLabel}  {shipCount}", defaultColor));
        ShowConfirmPopup(new ConfirmPopupConfig
        {
            title              = LocalizationManager.Instance.Get("UIPopupMessage_CommanderLevelupTitle"),
            resultRows         = rows,
            resultRowsVertical = true,
            onConfirm          = () => { },
            autoCloseSec       = 10f,
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

    // 보상코드 입력 팝업
    public void ShowRedeemCodePopup()
    {
        UIPopupRedeemCode popup = GetOrCreatePopup<UIPopupRedeemCode>("UIPopupRedeemCode", EPopupLayer.Normal);
        if (popup == null) return;

        ReplacePopup(popup, EPopupLayer.Normal);
        popup.ShowPopupRedeemCode(onClose: () => CloseTopPopup(EPopupLayer.Normal));
    }

    // 외부 라이센스 고지 팝업
    public void ShowLicensePopup()
    {
        UIPopupLicense popup = GetOrCreatePopup<UIPopupLicense>("UIPopupLicense", EPopupLayer.Normal);
        if (popup == null) return;

        ReplacePopup(popup, EPopupLayer.Normal);
        popup.ShowPopupLicense(() => CloseTopPopup(EPopupLayer.Normal));
    }

    // 함선 프리셋 상세 스탯 팝업 (함대편성 UI — 배치가능 프리셋 클릭 시)
    public void ShowShipStatsPopup(ShipPresetData preset)
    {
        UIPopupShipStats popup = GetOrCreatePopup<UIPopupShipStats>("UIPopupShipStats", EPopupLayer.Normal);
        if (popup == null) return;

        ReplacePopup(popup, EPopupLayer.Normal);
        popup.ShowPopupShipStats(preset, onClose: () => CloseTopPopup(EPopupLayer.Normal));
    }

}
