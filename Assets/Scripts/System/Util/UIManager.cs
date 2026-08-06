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

    // 메인 패널이 아닌 UIPanelBase가 열려있는 개수 — 0↔1 전이 시 EventManager로 알려 진입 버튼 등의 "기본 상태" 판단에 사용
    private int m_activeOverlayPanelCount = 0;

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

        // 빈 공간 터치 시 현재 열린 오버레이 패널을 닫는 로직 — 특정 패널(UIPanelSpace 등)이 아니라 항상 살아있는
        // UIManager 자신이 구독해야 함. 그 패널 자체가 하이드될 수 있는 상태라 리스너가 같이 끊기면 안 되기 때문
        EventManager.Subscribe_EmptySpaceTapped(OnEmptySpaceTapped);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        EventManager.Unsubscribe_EmptySpaceTapped(OnEmptySpaceTapped);
    }

    private void OnEmptySpaceTapped()
    {
        if (string.IsNullOrEmpty(GetCurrentActivePanelName()) == false)
            HideCurrentPanel();
    }

    public virtual void InitializeUIManager()
    {
        InitializeContainers();
    }

    // 곡면(엣지) 디스플레이 대비 좌우 마진 비율 — SafeAreaRoot·레터박스 커버 바·GaugeBar 컬링이 공유
    public const float CURVED_EDGE_MARGIN = 0.02f;

    // 컨테이너 초기화 (하이라키 순서 = 렌더 순서) — 컨테이너들은 씬/캔버스에 미리 만들어둔 것을 찾아서 씀(동적 생성 아님)
    protected void InitializeContainers()
    {
        m_gaugeBarContainer = FindContainer("UIGaugeBarContainer");
        m_generalContainer  = FindContainer("UIGeneralContainer");

        int layerCount = (int)EPopupLayer.Count;
        m_popupContainers = new RectTransform[layerCount];
        m_popupStacks     = new Stack<UIPopupBase>[layerCount];

        m_popupContainers[(int)EPopupLayer.Normal]  = FindContainer("UIPopupNormalContainer");
        m_popupContainers[(int)EPopupLayer.Overlay] = FindContainer("UIPopupOverlayContainer");

        for (int i = 0; i < layerCount; i++)
            m_popupStacks[i] = new Stack<UIPopupBase>();

        m_tutorialContainer = FindContainer("UITutorialContainer");

        
    }

    // 씬에 미리 만들어둔 컨테이너를 이름으로 찾음 — Canvas/SafeAreaRoot/{name} 경로 고정 (SafeAreaRoot는 컴포넌트만 비활성화된 채 오브젝트는 유지됨)
    // 없으면 에러 로그(씬 세팅 누락)
    private RectTransform FindContainer(string name)
    {
        Transform safeAreaRoot = mainCanvas != null ? mainCanvas.transform.Find("SafeAreaRoot") : null;
        if (safeAreaRoot == null)
        {
            Debug.LogError("SafeAreaRoot not found under canvas");
            return null;
        }

        Transform found = safeAreaRoot.Find(name);
        if (found == null)
        {
            Debug.LogError($"Container not found under SafeAreaRoot: {name}");
            return null;
        }
        return found as RectTransform;
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
            EventManager.TriggerCurrentPanelChanged(currentActivePanel.panelName);
        }

        ShowPanel(targetPanel);
    }

    public void ShowMainPanel()
    {
        if (mainPanel != null)
            ShowPanel(mainPanel.panelName);
    }

    // showMainPanelIfEmpty=false로 호출하면, 스택이 비어있어도 메인 패널로 안 돌아가고 화면을 빈 상태로 둠 —
    // 다음에 보여줄 패널이 아직 준비 안 됐지만(비동기 대기 등) 곧 ShowPanel로 이어붙일 걸 아는 경우,
    // 그 사이에 메인 패널이 잠깐 비쳤다 사라지는 깜빡임을 막기 위해 사용
    public void HideCurrentPanel(bool showMainPanelIfEmpty = true)
    {
        if (currentActivePanel != null && !currentActivePanel.bMainPanel)
        {
            HidePanel(currentActivePanel);

            if (panelStack.Count > 0)
            {
                UIPanelBase previousPanel = panelStack.Pop();
                currentActivePanel = previousPanel;
                EventManager.TriggerCurrentPanelChanged(currentActivePanel.panelName);
                ShowPanel(previousPanel);
            }
            else
            {
                currentActivePanel = null;
                EventManager.TriggerCurrentPanelChanged(string.Empty);
                if (showMainPanelIfEmpty == true)
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

        panel.transform.SetAsLastSibling(); // 최근에 연 패널이 하이라키 최상단(=렌더 최상위)에 오도록 — 이전에 열려있던 패널을 가리며 나타남

        if (useAnimation == true)
            StartPanelAnimation(panel.gameObject, true);
        else
            panel.gameObject.SetActive(true);

        panel.OnShowUIPanel();

        if (panel.bMainPanel == false && panel.bAffectsOverlayCount == true)
        {
            m_activeOverlayPanelCount++;
            if (m_activeOverlayPanelCount == 1)
                EventManager.TriggerOverlayPanelActiveChanged(true);
        }
    }

    private void HidePanel(UIPanelBase panel)
    {
        if (panel == null || panel.gameObject == null) return;

        if (useAnimation == true)
            StartPanelAnimation(panel.gameObject, false);
        else
            panel.gameObject.SetActive(false);

        panel.OnHideUIPanel();

        if (panel.bMainPanel == false && panel.bAffectsOverlayCount == true)
        {
            m_activeOverlayPanelCount--;
            if (m_activeOverlayPanelCount == 0)
                EventManager.TriggerOverlayPanelActiveChanged(false);
        }
    }

    // 패널(GameObject)별로 현재 실행 중인 전환 코루틴 추적 — 더블클릭 등으로 같은 패널에 Show/Hide가 연속 호출되면
    // 이전 코루틴을 먼저 취소해야 함. 안 그러면 두 코루틴이 같은 CanvasGroup.alpha를 서로 다른 방향으로 덮어쓰다가
    // 나중에 끝난 쪽이 최종 상태(SetActive(false) 포함)를 그대로 확정시켜버려 패널이 뜨지도 닫히지도 않는 상태로 멈춤
    private readonly Dictionary<GameObject, Coroutine> m_panelAnimCoroutines = new();

    private void StartPanelAnimation(GameObject panel, bool show)
    {
        if (m_panelAnimCoroutines.TryGetValue(panel, out Coroutine running) == true && running != null)
            StopCoroutine(running);

        m_panelAnimCoroutines[panel] = StartCoroutine(AnimatePanel(panel, show));
    }

    private System.Collections.IEnumerator AnimatePanel(GameObject panel, bool show)
    {
        CanvasGroup canvasGroup = GetOrAddCanvasGroup(panel);

        // 하드코딩된 0f/1f가 아니라 현재 알파에서 이어감 — 취소된 이전 전환 도중의 알파값에서 자연스럽게 이어받아
        // 재시작 시 화면이 뚝 끊기며 튀는 현상을 방지
        float startAlpha = canvasGroup.alpha;

        if (show == true)
        {
            panel.SetActive(true);

            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / animationDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / animationDuration);
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
        Color defaultColor = CommonUtility.PaletteColor("General.Bright1");
        var rows = new List<(string label, string value, Color? color)>();
        if (shipCount != prevShipCount)
            rows.Add(("UITabCommander_ShipCountMaxTitle", shipCount.ToString(), defaultColor));
        ShowConfirmPopup(new ConfirmPopupConfig
        {
            message            = LocalizationManager.Instance.Get("UIPopupMessage_CommanderLevelupMessage"),
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

    // 함선 프리셋 상세 스탯 팝업 (함대편성 UI — 배치가능 프리셋 클릭 시) — 전용 팝업 대신 UIPopupConfirm의 stat gauge 섹션 재사용
    public void ShowShipStatsPopup(ShipPresetData preset)
    {
        // 함선 이름 로컬라이즈는 아직 미정 — 프리셋 코드(presetId)를 그대로 표시
        ShowConfirmPopup(new ConfirmPopupConfig
        {
            message = preset.presetId,
            statGaugeRows = ShipStatGaugeBuilder.Build(preset),
            onConfirm = null,
        });
    }

}
