// 우주 공간 UI 패널 — 탭 시스템 초기화, UITabShip/UITabStation 탭 시 카메라 viewport 애니메이션, 모듈 선택 자동 전환
using System.Collections;
using UnityEngine;

public class UIPanelSpace : UIPanelBase
{
    [Header("Tab System")]
    public TabSystem m_tabSystem;

    [Header("Layout Animation (UITabShip / UITabStation)")]
    public float m_animDuration = 0.3f;

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
        m_tabSystem.InitializeTabBases();

        for (int i = 0; i < m_tabSystem.tabs.Count; i++)
        {
            var tabData = m_tabSystem.tabs[i];
            if (tabData.tabPanel == null) continue;
            if (tabData.tabPanel.TryGetComponent<UITabShip>(out _) == false) continue;
            m_moduleTabIndex = i;
            m_shipTabRect = tabData.tabPanel.GetComponent<RectTransform>();
        }

        EventManager.Subscribe_TabSelectionChanged(OnTabSelectionChanged);

        // 760px 고정 UI 너비 → 캔버스 너비 기준으로 카메라 viewport 비율 계산
        const float uiPanelWidth = 760f;
        RectTransform canvasRect = m_shipTabRect != null ? m_shipTabRect.root as RectTransform : null;
        float canvasWidth = canvasRect != null ? canvasRect.rect.width : 1920f;
        m_openCameraWidth = (canvasWidth - uiPanelWidth) / canvasWidth;
        SetLayoutImmediate(false);
    }

    public override void OnShowUIPanel()
    {
        CameraController.Instance.SetShipSelectionEnabled(true);
        EventManager.Subscribe_SpaceShipModuleSelected(OnModuleSelectedAutoTabSwitch);
        EventManager.Subscribe_EmptySpaceTapped(OnEmptySpaceTapped);
        EventManager.Subscribe_VipButtonOpened(OnVipButtonOpened);
        EventManager.Subscribe_VipStatusChanged(OnVipStatusChangedForDailyReward);
        CheckAndShowDailyRewardPopup();
        m_tabSystem.ForceActivateTab();
    }

    public override void OnHideUIPanel()
    {
        CameraController.Instance.SetShipSelectionEnabled(false);
        EventManager.Unsubscribe_SpaceShipModuleSelected(OnModuleSelectedAutoTabSwitch);
        EventManager.Unsubscribe_EmptySpaceTapped(OnEmptySpaceTapped);
        EventManager.Unsubscribe_VipButtonOpened(OnVipButtonOpened);
        EventManager.Unsubscribe_VipStatusChanged(OnVipStatusChangedForDailyReward);
        m_tabSystem.ForceDeactivateTab();

        SetTabNavVisible(true);
        SetLayoutImmediate(false);

        CameraController.Instance.SetTargetOfCameraController(ObjectManager.Instance.m_myFleet.transform);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_TabSelectionChanged(OnTabSelectionChanged);
    }

    // ── VIP 일일 미네랄 팝업 ──────────────────────────────────────────────────

    private void OnVipStatusChangedForDailyReward()
    {
        CheckAndShowDailyRewardPopup();
    }

    private void CheckAndShowDailyRewardPopup()
    {
        if (IAPManager.Instance == null) return;

        IAPManager.Instance.TryClaimDailyReward(result =>
        {
            if (result == null) return;

            var character = DataManager.Instance.m_currentCharacter;
            if (character != null)
            {
                character.SetClaimedDaysMask(result.claimedDaysMask);
                character.SetVipClaimedDaysMask(result.vipClaimedDaysMask);
                character.SetTodayDay(result.todayDay);
            }

            if (result.available == false) return;

            if (character != null)
                character.UpdateMineral(result.mineralRemain);

            UIManager.Instance.ShowDailyBonusPopup(result.claimedDaysMask, result.vipClaimedDaysMask, result.todayDay, result.grantedMineral);
        });
    }

    private void OnTabSelectionChanged(string systemName, int tabIndex)
    {
        if (systemName != m_tabSystem.GetSystemName()) return;
        bool shouldShrinkCamera = tabIndex == m_moduleTabIndex;
        if (shouldShrinkCamera == m_isUIOpen) return;

        float targetWidth = tabIndex == m_moduleTabIndex ? m_openCameraWidth : 1f;

        m_isUIOpen = shouldShrinkCamera;
        if (m_layoutCoroutine != null)
            StopCoroutine(m_layoutCoroutine);
        m_layoutCoroutine = StartCoroutine(Co_AnimateLayout(shouldShrinkCamera, targetWidth));
    }

    private void OnEmptySpaceTapped()
    {
        if (m_tabSystem.GetCurrentActiveTab() >= 0)
            m_tabSystem.SwitchToTab(-1);
    }

    private void OnVipButtonOpened()
    {
        m_tabSystem.CloseAllTabs();
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
