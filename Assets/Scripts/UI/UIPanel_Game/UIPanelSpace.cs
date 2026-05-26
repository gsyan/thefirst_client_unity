// 우주 공간 UI 패널 — 탭 시스템 초기화, UITabShip/UITabStation 탭 시 카메라 viewport 애니메이션, 모듈 선택 자동 전환
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelSpace : UIPanelBase
{
    [Header("Tab System")]
    public TabSystem m_tabSystem;

    [Header("Layout Animation (UITabShip / UITabStation)")]
    public float m_animDuration = 0.3f;

    [Header("Hidden Close Button (backdrop)")]
    [SerializeField] private Button m_hiddenCloseButton;

    [HideInInspector] public SpaceFleet m_myFleet;

    // UITabShip / UITabStation 탭 인덱스 및 RectTransform (카메라 뷰포트용)
    private int m_moduleTabIndex = -1;
    private RectTransform m_shipTabRect;
    private bool m_isUIOpen = false;
    private Coroutine m_layoutCoroutine;

    // 각 탭 anchorMin.x 기준 카메라 뷰포트 너비
    private float m_openCameraWidth;

    public override void InitializeUIPanel()
    {
        InitializeUIPanelSpace();
    }

    private void InitializeUIPanelSpace()
    {
        if (m_myFleet == null)
            m_myFleet = DataManager.Instance.m_currentCharacter.GetOwnedFleet();

        m_tabSystem.InitializeTabBases();

        for (int i = 0; i < m_tabSystem.tabs.Count; i++)
        {
            var tabData = m_tabSystem.tabs[i];
            if (tabData.tabPanel == null) continue;
            if (tabData.tabPanel.TryGetComponent<UITabShip>(out _) == false) continue;
            m_moduleTabIndex = i;
            m_shipTabRect = tabData.tabPanel.GetComponent<RectTransform>();
        }

        m_tabSystem.onTabSelectionChanged += OnTabSelectionChanged;

        // 760px 고정 UI 너비 → 캔버스 너비 기준으로 카메라 viewport 비율 계산
        const float uiPanelWidth = 760f;
        RectTransform canvasRect = m_shipTabRect != null ? m_shipTabRect.root as RectTransform : null;
        float canvasWidth = canvasRect != null ? canvasRect.rect.width : 1920f;
        m_openCameraWidth = (canvasWidth - uiPanelWidth) / canvasWidth;
        SetLayoutImmediate(false);

        if (m_hiddenCloseButton != null)
        {
            m_hiddenCloseButton.onClick.AddListener(OnHiddenCloseButtonClicked);
            m_hiddenCloseButton.gameObject.SetActive(false);
        }
    }

    public override void OnShowUIPanel()
    {
        CameraController.Instance.SetShipSelectionEnabled(true);
        EventManager.Subscribe_SpaceShipModuleSelected(OnModuleSelectedAutoTabSwitch);
        m_tabSystem.ForceActivateTab();
    }

    public override void OnHideUIPanel()
    {
        CameraController.Instance.SetShipSelectionEnabled(false);
        EventManager.Unsubscribe_SpaceShipModuleSelected(OnModuleSelectedAutoTabSwitch);
        m_tabSystem.ForceDeactivateTab();

        SetTabNavVisible(true);
        SetLayoutImmediate(false);

        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    private void OnDestroy()
    {
        if (m_tabSystem != null)
            m_tabSystem.onTabSelectionChanged -= OnTabSelectionChanged;
    }

    private void OnTabSelectionChanged(int tabIndex)
    {
        if (m_hiddenCloseButton != null)
            m_hiddenCloseButton.gameObject.SetActive(tabIndex >= 0);

        bool shouldShrinkCamera = tabIndex == m_moduleTabIndex;
        if (shouldShrinkCamera == m_isUIOpen) return;

        float targetWidth;
        if (tabIndex == m_moduleTabIndex)        targetWidth = m_openCameraWidth;
        else                                     targetWidth = 1f;

        m_isUIOpen = shouldShrinkCamera;
        if (m_layoutCoroutine != null)
            StopCoroutine(m_layoutCoroutine);
        m_layoutCoroutine = StartCoroutine(Co_AnimateLayout(shouldShrinkCamera, targetWidth));
    }

    // 카메라 viewport width 애니메이션
    private IEnumerator Co_AnimateLayout(bool open, float openCameraWidth)
    {
        float startCamWidth = CameraController.Instance.GetViewportWidth();
        float targetCamWidth = open ? openCameraWidth : 1f;

        float elapsed = 0f;
        while (elapsed < m_animDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / m_animDuration));

            float camWidth = Mathf.Lerp(startCamWidth, targetCamWidth, t);
            CameraController.Instance.SetViewportWidth(camWidth);
            EventManager.TriggerCameraViewportChanged(Mathf.InverseLerp(1f, openCameraWidth, camWidth));

            yield return null;
        }

        SetLayoutImmediate(open, openCameraWidth);
        m_layoutCoroutine = null;
    }

    private void SetLayoutImmediate(bool open, float openCameraWidth = 0f)
    {
        if (openCameraWidth <= 0f) openCameraWidth = m_openCameraWidth;
        CameraController.Instance.SetViewportWidth(open ? openCameraWidth : 1f);
        EventManager.TriggerCameraViewportChanged(open ? 1f : 0f);
    }

    private void SetTabNavVisible(bool visible)
    {
        for (int i = 0; i < m_tabSystem.tabs.Count; i++)
        {
            var btn = m_tabSystem.tabs[i].tabButton;
            if (btn != null)
                btn.gameObject.SetActive(visible);
        }
    }

    private void OnHiddenCloseButtonClicked()
    {
        if (m_tabSystem.GetCurrentActiveTab() != m_moduleTabIndex)
        {
            m_tabSystem.SwitchToTab(-1);
            return;
        }

        // UITabShip 상태: 모듈 피킹 먼저 시도, 내 함대 모듈이 맞으면 닫지 않음
        LayerMask pickMask = ~(1 << 13); // Shield 레이어 제외
        if (CameraController.Instance.GetCameraRaycast(out RaycastHit hit, pickMask, 3000f))
        {
            SpaceShip ship = hit.collider.GetComponentInParent<SpaceShip>();
            if (ship != null && ship.m_myFleet != null && ship.m_myFleet.IsEnemy == false)
            {
                ModuleBase module = hit.collider.GetComponentInParent<ModuleBase>();
                if (module != null)
                {
                    EventManager.Trigger_SpaceShipSelected(ship);
                    EventManager.TriggerSpaceShipModuleSelected(ship, module);
                    return;
                }
            }
        }

        m_tabSystem.SwitchToTab(-1);
    }

    // 모듈이 선택될 때만 UITabShip 로 자동 전환 (함선 클릭만으로는 전환 안 함)
    private void OnModuleSelectedAutoTabSwitch(SpaceShip ship, ModuleBase module)
    {
        if (m_moduleTabIndex < 0) return;
        if (m_tabSystem.GetCurrentActiveTab() == m_moduleTabIndex) return;
        m_tabSystem.SwitchToTab(m_moduleTabIndex);
    }

}
