// 우주 공간 UI 패널 — 탭 시스템 초기화, 레이아웃 애니메이션(카메라 viewport + UI 슬라이드), 모듈 선택 자동 전환
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelSpace : UIPanelBase
{
    [Header("Tab System")]
    public TabSystem m_tabSystem;

    [Header("Layout Animation")]
    // 탭 패널들을 감싸는 컨테이너 — 에디터에서 보이는 상태가 '열린 상태' 기준
    public RectTransform m_uiContentRoot;
    public float m_animDuration = 0.3f;

    [Header("Manual Tab Setup (Alternative)")]
    public Button closeButton;

    [HideInInspector] public SpaceFleet m_myFleet;

    // UITabShip 탭 인덱스 (자동 전환용)
    private int m_moduleTabIndex = -1;

    private bool m_isUIOpen = false;
    private Coroutine m_layoutCoroutine;

    // 에디터에서 설정한 초기 상태 저장 (= UI 열린 상태)
    private Vector2 m_openAnchoredPos;
    private float m_openCameraWidth; // = uiContentRoot.anchorMin.x
    private RectTransform m_canvasRect;

    public override void InitializeUIPanel()
    {
        InitializeUIPanelSpace();
    }

    private void InitializeUIPanelSpace()
    {
        if (m_myFleet == null)
            m_myFleet = DataManager.Instance.m_currentCharacter.GetOwnedFleet();

        // TabSystem의 각 탭
        for (int i = 0; i < m_tabSystem.tabs.Count; i++)
        {
            var tabData = m_tabSystem.tabs[i];
            if (tabData.tabPanel != null)
            {
                UITabBase tabBase = tabData.tabPanel.GetComponent<UITabBase>();
                if (tabBase == null) continue;
                tabBase.m_tabSystemParent = m_tabSystem;
                tabBase.InitializeUITab();
                tabData.onActivate = tabBase.OnTabActivated;
                tabData.onDeactivate = tabBase.OnTabDeactivated;

                // UITabShip 탭 인덱스 저장
                if (tabBase is UITabShip)
                    m_moduleTabIndex = i;
            }
        }

        m_tabSystem.onTabSelectionChanged += OnTabSelectionChanged;

        // 에디터 설정값(= 열린 상태)을 저장한 뒤 닫힌 상태로 초기화
        m_canvasRect = GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();
        m_openAnchoredPos = m_uiContentRoot.anchoredPosition;
        m_openCameraWidth = m_uiContentRoot.anchorMin.x;
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

        // UI 패널 닫힘 상태로 즉시 복구
        SetLayoutImmediate(false);

        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    private void OnDestroy()
    {
        if (m_tabSystem != null)
            m_tabSystem.onTabSelectionChanged -= OnTabSelectionChanged;
    }

    // 탭 선택 변경 시 카메라 viewport + UI 슬라이드 애니메이션
    private void OnTabSelectionChanged(int tabIndex)
    {
        bool shouldOpen = tabIndex >= 0;
        if (shouldOpen == m_isUIOpen) return;

        m_isUIOpen = shouldOpen;
        if (m_layoutCoroutine != null)
            StopCoroutine(m_layoutCoroutine);
        m_layoutCoroutine = StartCoroutine(Co_AnimateLayout(shouldOpen));
    }

    // 앵커 비율 × 캔버스 너비 → 해상도 무관한 슬라이드 오프셋
    private float GetHideOffsetX()
    {
        float canvasWidth = m_canvasRect.rect.width;
        return canvasWidth * (m_uiContentRoot.anchorMax.x - m_uiContentRoot.anchorMin.x);
    }

    // 카메라 viewport width와 UI 패널 위치를 동시에 애니메이션
    private IEnumerator Co_AnimateLayout(bool open)
    {
        float startCamWidth = CameraController.Instance.GetViewportWidth();
        float targetCamWidth = open ? m_openCameraWidth : 1f;

        Vector2 startPos = m_uiContentRoot.anchoredPosition;
        Vector2 targetPos = open ? m_openAnchoredPos
                                 : m_openAnchoredPos + new Vector2(GetHideOffsetX(), 0f);

        float elapsed = 0f;
        while (elapsed < m_animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / m_animDuration));

            float camWidth = Mathf.Lerp(startCamWidth, targetCamWidth, t);
            CameraController.Instance.SetViewportWidth(camWidth);
            m_uiContentRoot.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            EventManager.TriggerCameraViewportChanged(Mathf.InverseLerp(1f, m_openCameraWidth, camWidth));

            yield return null;
        }

        SetLayoutImmediate(open);
        m_layoutCoroutine = null;
    }

    // 애니메이션 없이 즉시 레이아웃 설정
    private void SetLayoutImmediate(bool open)
    {
        CameraController.Instance.SetViewportWidth(open ? m_openCameraWidth : 1f);
        EventManager.TriggerCameraViewportChanged(open ? 1f : 0f);

        if (m_uiContentRoot != null)
        {
            m_uiContentRoot.anchoredPosition = open
                ? m_openAnchoredPos
                : m_openAnchoredPos + new Vector2(GetHideOffsetX(), 0f);
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

