// 우주 공간 UI 패널 — 탭 시스템 초기화, UITabShip/UITabStation 탭 시 카메라 viewport 애니메이션, 모듈 선택 자동 전환
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelSpace : UIPanelBase
{
    [Header("Tab System")]
    public TabSystem m_tabSystem;

    [Header("Layout Animation (UITabShip / UITabStation)")]
    public float m_animDuration = 0.3f;

    [Header("Manual Tab Setup (Alternative)")]
    public Button closeButton;

    [HideInInspector] public SpaceFleet m_myFleet;

    // UITabShip / UITabStation 탭 인덱스 및 RectTransform (카메라 뷰포트용)
    private int m_moduleTabIndex = -1;
    private RectTransform m_shipTabRect;
    private int m_stationTabIndex = -1;
    private RectTransform m_stationTabRect;

    private bool m_isUIOpen = false;
    private Coroutine m_layoutCoroutine;

    // 각 탭 anchorMin.x 기준 카메라 뷰포트 너비
    private float m_openCameraWidth;
    private float m_stationOpenCameraWidth;

    public override void InitializeUIPanel()
    {
        InitializeUIPanelSpace();
    }

    private void InitializeUIPanelSpace()
    {
        if (m_myFleet == null)
            m_myFleet = DataManager.Instance.m_currentCharacter.GetOwnedFleet();

        for (int i = 0; i < m_tabSystem.tabs.Count; i++)
        {
            var tabData = m_tabSystem.tabs[i];
            if (tabData.tabPanel != null)
            {
                UITabBase tabBase = tabData.tabPanel.GetComponent<UITabBase>();
                if (tabBase == null) continue;
                tabBase.m_tabSystemParent = m_tabSystem;
                tabBase.InitializeUITab();
                tabBase.InitializeCloseButton();
                tabData.onActivate = tabBase.OnTabActivated;
                tabData.onDeactivate = tabBase.OnTabDeactivated;

                if (tabBase is UITabShip)
                {
                    m_moduleTabIndex = i;
                    m_shipTabRect = tabData.tabPanel.GetComponent<RectTransform>();
                }
                else if (tabBase is UITabStation)
                {
                    m_stationTabIndex = i;
                    m_stationTabRect = tabData.tabPanel.GetComponent<RectTransform>();
                }
            }
        }

        m_tabSystem.onTabSelectionChanged += OnTabSelectionChanged;

        // 각 탭 anchorMin.x → 열린 상태 카메라 너비
        m_openCameraWidth        = m_shipTabRect    != null ? m_shipTabRect.anchorMin.x    : 0.68f;
        m_stationOpenCameraWidth = m_stationTabRect != null ? m_stationTabRect.anchorMin.x : 0.68f;
        SetLayoutImmediate(false);

        if (closeButton != null)
            closeButton.onClick.AddListener(() => UIManager.Instance.ShowMainPanel());
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
        bool shouldShrinkCamera = tabIndex == m_moduleTabIndex || tabIndex == m_stationTabIndex;
        if (shouldShrinkCamera == m_isUIOpen) return;

        float targetWidth;
        if (tabIndex == m_moduleTabIndex)        targetWidth = m_openCameraWidth;
        else if (tabIndex == m_stationTabIndex)  targetWidth = m_stationOpenCameraWidth;
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

    // 모듈이 선택될 때만 UITabShip 로 자동 전환 (함선 클릭만으로는 전환 안 함)
    private void OnModuleSelectedAutoTabSwitch(SpaceShip ship, ModuleBase module)
    {
        if (m_moduleTabIndex < 0) return;
        if (m_tabSystem.GetCurrentActiveTab() == m_moduleTabIndex) return;
        m_tabSystem.SwitchToTab(m_moduleTabIndex);
    }

}
