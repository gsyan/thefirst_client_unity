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

    // UITabShip / UITabStation 탭 인덱스 및 RectTransform (카메라 뷰포트용)
    private int m_moduleTabIndex = -1;
    private RectTransform m_shipTabRect;
    private bool m_isUIOpen = false;
    private Coroutine m_viewportCoroutine;

    // 각 탭 anchorMin.x 기준 카메라 뷰포트 너비
    private float m_openCameraWidth;

    // 카메라 rect(viewport)가 축소되는 동안 그 바깥 영역(경계면 3D 잔상)을 가리는 배경
    // 탭 alpha 페이드(TabSystem.AnimatePanel)와 별도 오브젝트라 페이드 타이밍에 영향받지 않음
    private RectTransform m_cameraViewportBgRect;
    private Image m_cameraViewportBgImage;

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
            tabData.deferReveal = true; // 카메라 viewport 애니메이션이 끝난 뒤에 보이도록 — Co_AnimateViewport에서 RevealDeferredPanel 호출
        }

        EventManager.Subscribe_TabSelectionChanged(OnTabSelectionChanged);

        // 760px 고정 UI 너비 + 우측 여백 → 캔버스 너비 기준으로 카메라 viewport 비율 계산
        const float uiPanelWidth = 760f;
        const float uiPanelRightMargin = 100f;
        RectTransform canvasRect = m_shipTabRect != null ? m_shipTabRect.root as RectTransform : null;
        float canvasWidth = canvasRect != null ? canvasRect.rect.width : 1920f;
        float occupiedWidth = uiPanelWidth + uiPanelRightMargin;
        m_openCameraWidth = (canvasWidth - occupiedWidth) / canvasWidth;

        CreateCameraViewportBackground();
        SetViewport(open:false);
    }

    // 탭 콘텐츠보다 먼저(가장 아래) 그려지도록 UIPanelSpace 최상위의 첫 자식으로 생성
    private void CreateCameraViewportBackground()
    {
        GameObject bgObj = new GameObject("CameraViewportBg");
        m_cameraViewportBgRect = bgObj.AddComponent<RectTransform>();
        bgObj.AddComponent<CanvasRenderer>();
        m_cameraViewportBgImage = bgObj.AddComponent<Image>();
        m_cameraViewportBgImage.color = Color.black;
        m_cameraViewportBgImage.raycastTarget = false;

        m_cameraViewportBgRect.SetParent(transform, false);
        m_cameraViewportBgRect.SetAsFirstSibling();
        m_cameraViewportBgRect.anchorMin = new Vector2(1f, 0f);
        m_cameraViewportBgRect.anchorMax = Vector2.one;
        m_cameraViewportBgRect.offsetMin = Vector2.zero;
        m_cameraViewportBgRect.offsetMax = Vector2.zero;

        bgObj.SetActive(false);
    }

    // 카메라 rect width에 맞춰 배경이 덮는 영역(camWidth ~ 1)을 갱신
    private void UpdateCameraViewportBackground(float camWidth)
    {
        if (m_cameraViewportBgRect == null) return;

        if (camWidth >= 1f)
        {
            m_cameraViewportBgRect.gameObject.SetActive(false);
            return;
        }

        m_cameraViewportBgRect.gameObject.SetActive(true);
        Vector2 anchorMin = m_cameraViewportBgRect.anchorMin;
        anchorMin.x = camWidth;
        m_cameraViewportBgRect.anchorMin = anchorMin;
    }

    public override void OnShowUIPanel()
    {
        CameraController.Instance.SetShipSelectionEnabled(true);
        EventManager.Subscribe_SpaceShipModuleSelected(OnModuleSelectedAutoTabSwitch);
        EventManager.Subscribe_EmptySpaceTapped(OnEmptySpaceTapped);
        EventManager.Subscribe_VipStatusChanged(OnVipStatusChangedForDailyReward);
        EventManager.Subscribe_TutorialGeneralUIBlockedChanged(OnTutorialGeneralUIBlockedChanged);
        CheckAndClaimPendingStageRewards();
        CheckAndClaimPvpSeasonReward();
        m_tabSystem.ForceActivateTab();
    }

    public override void OnHideUIPanel()
    {
        CameraController.Instance.SetShipSelectionEnabled(false);
        EventManager.Unsubscribe_SpaceShipModuleSelected(OnModuleSelectedAutoTabSwitch);
        EventManager.Unsubscribe_EmptySpaceTapped(OnEmptySpaceTapped);
        EventManager.Unsubscribe_VipStatusChanged(OnVipStatusChangedForDailyReward);
        EventManager.Unsubscribe_TutorialGeneralUIBlockedChanged(OnTutorialGeneralUIBlockedChanged);
        m_tabSystem.ForceDeactivateTab();

        //SetTabNavVisible(true);
        SetViewport(open:false);

        CameraController.Instance.SetTargetOfCameraController(ObjectManager.Instance.GetMyFleet().transform);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_TabSelectionChanged(OnTabSelectionChanged);
    }

    // ── 미수령 존 보상 복구 ───────────────────────────────────────────────────

    private void CheckAndClaimPvpSeasonReward()
    {
        NetworkManager.Instance.PvpClaimSeasonReward(response =>
        {
            if (response == null || response.errorCode != 0) return;
            if (response.data.pvpPointGained <= 0) return;

            var loc = LocalizationManager.Instance;
            UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
            {
                title   = loc.Get("UIPopupMessage_PvpSeasonRewardTitle"),
                message = loc.Get("UIPopupMessage_PvpSeasonRewardMessage"),
                rewardAmounts = new System.Collections.Generic.List<int> { 0, 0, 0, response.data.pvpPointGained },
                onConfirm = () => { }
            });
        });
    }

    private void CheckAndClaimPendingStageRewards()
    {
        NetworkManager.Instance.ClaimPendingStageRewards(response =>
        {
            if (response == null || response.errorCode != 0) return;
            if (response.data.mineralGained == 0) return;

            var commander = DataManager.Instance.m_currentCommander;
            int newLevel  = 0;
            int prevLevel = 0;
            if (commander != null)
            {
                prevLevel = commander.GetCommanderLevel();
                commander.UpdateMineral(response.data.mineralRemain);
                commander.UpdateExp(response.data.totalExp);
                commander.UpdateModulePointMaxGot(response.data.modulePointMaxGot);
                commander.UpdateModulePoint(response.data.modulePointRemain);
                newLevel = response.data.commanderLevel;
                commander.UpdateCommanderLevel(newLevel);
            }

            // 보상 획득 후 기술 레벨업 순서로 표시
            UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
            {
                title   = LocalizationManager.Instance.Get("pending_reward_title"),
                message = LocalizationManager.Instance.Get("pending_reward_message"),
                rewardAmounts = new System.Collections.Generic.List<int>
                {
                    response.data.mineralGained,
                    response.data.expGained,
                    response.data.modulePointGained,
                    0
                },
                onConfirm = () => { }
            });

            if (newLevel > prevLevel)
                UIManager.Instance.ShowCommanderLevelupNotify(newLevel);

            if (response.data.mineralSettingReset == true && response.data.updatedFleetInfo != null)
            {
                var loc = LocalizationManager.Instance;
                var fleet = ObjectManager.Instance.GetMyFleet();
                FleetInfo fleetInfoToApply = response.data.updatedFleetInfo;
                UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
                {
                    title        = loc.Get("UIPopupMessage_MineralResetTitle"),
                    message      = loc.Get("UIPopupMessage_MineralResetMessage"),
                    confirmText1 = loc.Get("Simple_Confirm"),
                    autoCloseSec = 5f,
                    onConfirm    = () =>
                    {
                        if (fleet != null)
                            fleet.ApplyMineralReset(fleetInfoToApply);
                    },
                });
            }
        });
    }

    // ── VIP 일일 미네랄 팝업 ──────────────────────────────────────────────────
    // 최초 진입 시점 체크는 ObjectManager.StartNormalPlay()에서 담당 — 튜토리얼 진행 중에는 호출되지 않도록 보장

    private void OnVipStatusChangedForDailyReward()
    {
        if (DailyBonusManager.Instance == null) return;
        DailyBonusManager.Instance.CheckAndShowDailyRewardPopup();
    }

    private void OnTabSelectionChanged(string systemName, int tabIndex)
    {
        if (systemName != m_tabSystem.GetSystemName()) return;
        bool shouldShrinkCamera = tabIndex == m_moduleTabIndex;
        if (shouldShrinkCamera == m_isUIOpen) return;

        float targetWidth = tabIndex == m_moduleTabIndex ? m_openCameraWidth : 1f;

        m_isUIOpen = shouldShrinkCamera;
        if (m_viewportCoroutine != null)
            StopCoroutine(m_viewportCoroutine);
        m_viewportCoroutine = StartCoroutine(Co_AnimateViewport(shouldShrinkCamera, targetWidth));
    }

    private void OnEmptySpaceTapped()
    {
        if (m_tabSystem.GetCurrentActiveTab() >= 0)
            m_tabSystem.SwitchToTab(-1);
    }

    // 카메라 viewport width 애니메이션
    private IEnumerator Co_AnimateViewport(bool open, float openCameraWidth)
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
            UpdateCameraViewportBackground(camWidth);
            EventManager.TriggerCameraViewportChanged(Mathf.InverseLerp(1f, openCameraWidth, camWidth));

            yield return null;
        }

        SetViewport(open, openCameraWidth);
        if (open == true)
            m_tabSystem.RevealDeferredPanel(m_moduleTabIndex);
        m_viewportCoroutine = null;
    }

    private void SetViewport(bool open, float openCameraWidth = 0f)
    {
        if (openCameraWidth <= 0f) openCameraWidth = m_openCameraWidth;
        float camWidth = open ? openCameraWidth : 1f;
        CameraController.Instance.SetViewportWidth(camWidth);
        UpdateCameraViewportBackground(camWidth);
        EventManager.TriggerCameraViewportChanged(open ? 1f : 0f);
    }

    // private void SetTabNavVisible(bool visible)
    // {
    //     for (int i = 0; i < m_tabSystem.tabs.Count; i++)
    //     {
    //         var btn = m_tabSystem.tabs[i].tabButton;
    //         if (btn != null)
    //             btn.gameObject.SetActive(visible);
    //     }
    // }

    // 모듈이 선택될 때만 UITabShip 로 자동 전환 (함선 클릭만으로는 전환 안 함)
    // 튜토리얼 진행 중에는 자동 전환 안 함 — 튜토리얼 스텝이 preActionTabName 등으로 탭 전환을 직접 제어함
    private void OnModuleSelectedAutoTabSwitch(SpaceShip ship, ModuleBase module)
    {
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsPlaying) return;
        if (m_moduleTabIndex < 0) return;
        if (m_tabSystem.GetCurrentActiveTab() == m_moduleTabIndex) return;
        m_tabSystem.SwitchToTab(m_moduleTabIndex);
    }

    // 튜토리얼 dim 없는 스텝에서 상단 탭 버튼(Fleet/Ship/Settings/Pvp/Exploration) 클릭 차단 — 3D 카메라 조작은 별개라 영향 없음
    private void OnTutorialGeneralUIBlockedChanged(bool isBlocked)
    {
        m_tabSystem.SetTabButtonsInteractable(!isBlocked);
    }

}
