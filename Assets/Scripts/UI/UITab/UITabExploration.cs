// 탐사 탭 — 그룹 탭(Z1~Z9) + 존 스테이지 버튼(3D 월드 좌표 → Screen Space), 존 진입/재진입/킬 보상 처리
// 패배 시: 현재 존에서 클리어 스테이지 있으면 최고 클리어 위치로, 없으면 해당 존 x-0 스폰 마커 위치로 복귀
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class UITabExploration : UITabBase
{
    [SerializeField] private DataTableZone m_datatableZone;

    [Header("존 스테이지 버튼 (World Space)")]
    [SerializeField] private RectTransform m_zoneButtonRoot;       // Screen Space 오버레이 루트 (stretch 전체)
    [SerializeField] private UIZoneStageButton m_zoneStageButtonPrefab;
    
    [Header("그룹 탭")]
    [SerializeField] private Transform m_zoneTabButtonContainer;
    private UISelectableButton[] m_zoneTabButtons;
    private readonly List<UIZoneStageButton> m_buttonPool = new();

    private SpaceFleet m_myFleet;
    private Character m_myCharacter;
    private ZoneStageConfig m_currentZoneStage;  // 클리어 진행 위치
    private ZoneStageConfig m_battleZoneStage;   // 현재 전투 중인 존 (입장 시 set, 후퇴/완료 후 null)
    private ZoneStageConfig m_selectedZoneStage;
    private UIZoneStageButton m_currentZoneStageButton;
    private readonly Dictionary<string, UIZoneStageButton> m_zoneStageButtons = new Dictionary<string, UIZoneStageButton>();
    private readonly Dictionary<int, ZoneStageConfig> m_selectedZoneStagePerGroup = new();

    [Header("뷰 전환 타이밍")]
    [SerializeField] private float m_fleetHideDelay = 0.5f; // 함대뷰→갤럭시뷰 전환 시 함대 숨기기 딜레이(초)

    private Coroutine m_hideFleetCoroutine;
    private Vector3 m_pendingFleetPos;
    private float m_pendingFleetRotY;

    private bool m_isFleetWiped;
    private readonly WaitForSeconds m_wipePopupDelay = new WaitForSeconds(2f);

    public override void InitializeUITab()
    {
        InitializeUITabExploration();
    }

    private void InitializeUITabExploration()
    {
        m_myCharacter = DataManager.Instance.m_currentCharacter;
        if (m_myCharacter == null || m_myCharacter.GetOwnedFleet() == null) return;
        m_myFleet = m_myCharacter.GetOwnedFleet();

        EventManager.Subscribe_RetreatRequested(RetreatToPreviousStage);
        EventManager.Subscribe_MyFleetDestroyed(OnMyFleetWiped);

        SetupZoneTabButtons();
        InitializeZoneStageButtons();
        SetFleetState(EUnitState.Idle);

        SetInitialFleetPosition();
    }

    private void SetInitialFleetPosition()
    {
        if (ObjectManager.Instance == null || m_myFleet == null) return;

        var clearedZones = m_myCharacter.m_characterInfo != null ? m_myCharacter.m_characterInfo.clearedZones : null;

        if (clearedZones == null || clearedZones.Count == 0)
        {
            // 신규 유저 — zone1 스폰 마커(1-0) 위치
            ZoneStageConfig spawnStage = m_datatableZone.GetZoneSpawnStage(1);
            if (spawnStage != null)
            {
                ObjectManager.Instance.SetMyFleetPosition(m_datatableZone.ResolveFleetWorldPosition(spawnStage), spawnStage.fleetRotationY);
                CameraController.Instance.SnapToTarget();
            }
            return;
        }

        ZoneStageConfig targetStage = m_datatableZone.GetZoneStageByName(clearedZones[^1]);
        if (targetStage == null)
        {
            // 유효하지 않은 클리어 데이터 — zone1 스폰 마커로 fallback
            ZoneStageConfig spawnStage = m_datatableZone.GetZoneSpawnStage(1);
            if (spawnStage != null)
            {
                ObjectManager.Instance.SetMyFleetPosition(m_datatableZone.ResolveFleetWorldPosition(spawnStage), spawnStage.fleetRotationY);
                CameraController.Instance.SnapToTarget();
            }
            return;
        }

        ObjectManager.Instance.SetMyFleetPosition(m_datatableZone.ResolveFleetWorldPosition(targetStage), targetStage.fleetRotationY);
        CameraController.Instance.SnapToTarget();
    }

    private void SetupZoneTabButtons()
    {
        if (m_zoneTabButtonContainer == null) return;
        m_zoneTabButtons = m_zoneTabButtonContainer.GetComponentsInChildren<UISelectableButton>();
        for (int i = 0; i < m_zoneTabButtons.Length; i++)
        {
            int groupIndex = i + 1;
            m_zoneTabButtons[i].Setup($"Z{groupIndex}", () => OnGroupTabClicked(groupIndex));
        }
    }

    private void OnGroupTabClicked(int groupIndex)
    {
        ObjectManager.Instance.ChangeZone(groupIndex);
        SetupButtonsForGroup(groupIndex);
        UpdateGroupTabVisual(groupIndex);
    }

    private void UpdateGroupTabVisual(int groupIndex)
    {
        if (m_zoneTabButtons == null) return;
        for (int i = 0; i < m_zoneTabButtons.Length; i++)
            m_zoneTabButtons[i].SetSelected((i + 1) == groupIndex);
    }

    // 초기 그룹 인덱스 및 현재 함대 스테이지 결정 — 버튼 생성은 OnTabActivated의 SetupButtonsForGroup에서
    private void InitializeZoneStageButtons()
    {
        if (m_datatableZone == null) return;

        var clearedZoneNames = m_myCharacter != null ? m_myCharacter.m_characterInfo.clearedZones : null;
        if (clearedZoneNames != null && clearedZoneNames.Count > 0)
        {
            string lastCleared = clearedZoneNames[^1];
            m_currentZoneStage = m_datatableZone.GetZoneStageByName(lastCleared);
        }
    }

    // 선택 그룹 버튼을 풀에서 꺼내 배치 — 이전 그룹 버튼은 먼저 풀로 반납
    private void SetupButtonsForGroup(int groupIndex)
    {
        ReturnAllButtonsToPool();
        m_currentZoneStageButton = null;

        if (m_zoneButtonRoot == null || m_zoneStageButtonPrefab == null || m_datatableZone == null) return;

        m_zoneButtonRoot.gameObject.SetActive(true); // RebuildLayout이 Canvas에 반영되려면 root가 활성 상태여야 함

        var clearedZoneNames = m_myCharacter != null ? m_myCharacter.m_characterInfo.clearedZones : null;
        Camera worldCam = CameraController.Instance != null ? CameraController.Instance.m_targetCamera : Camera.main;

        for (int i = 0; i < m_datatableZone.ZoneStageCount; i++)
        {
            ZoneStageConfig zoneStage = m_datatableZone.GetZoneStage(i);
            if (zoneStage == null || ParseZoneGroup(zoneStage.zoneName) != groupIndex) continue;
            if (ParseZoneStage(zoneStage.zoneName) == 0) continue;

            UIZoneStageButton btn = GetButtonFromPool();
            btn.name = zoneStage.zoneName;

            bool isCleared = clearedZoneNames != null && clearedZoneNames.Contains(zoneStage.zoneName);
            EZoneState state = isCleared == true ? EZoneState.Cleared : EZoneState.NotCleared;

            btn.gameObject.SetActive(true);  // Initialize 전에 활성화해야 RebuildLayout이 Canvas에 반영됨

            ZoneStageConfig captured = zoneStage;
            Vector3 capturedWorldPos = m_datatableZone.ResolveFleetWorldPosition(captured);
            btn.InitializeUIZoneStageButton(captured, capturedWorldPos, () => OnZoneStageButtonClicked(captured), () => OnEnterZoneFromButton(captured), state, worldCam);
            m_zoneStageButtons[zoneStage.zoneName] = btn;
        }

        SortButtonsByName();

        // 풀에서 꺼낸 버튼의 이전 위치 잔류 방지 — OnCameraGalaxyViewSettled가 재발생하지 않는 경우(탭 이미 열림)에도 올바른 위치 보장
        foreach (var kv in m_zoneStageButtons)
            kv.Value.UpdateScreenPosition();

        // 함대 마커 버튼 갱신 (선택과 분리)
        if (m_currentZoneStage != null && ParseZoneGroup(m_currentZoneStage.zoneName) == groupIndex)
        {
            if (m_zoneStageButtons.TryGetValue(m_currentZoneStage.zoneName, out UIZoneStageButton curBtn))
                m_currentZoneStageButton = curBtn;
        }

        // 선택 스테이지 결정: 명시적으로 저장된 값 우선, 없으면 클리어+1(최저 미클리어) 기본값
        ZoneStageConfig toSelect = m_selectedZoneStagePerGroup.TryGetValue(groupIndex, out ZoneStageConfig saved)
            ? saved
            : GetDefaultZoneStageForZone(groupIndex);

        if (toSelect != null)
            ApplyZoneStageSelection(toSelect);

        RefreshMyFleetMarker();
    }

    private void RefreshMyFleetMarker()
    {
        foreach (var kv in m_zoneStageButtons)
            kv.Value.SetMyFleetMarker(false);

        if (m_currentZoneStage == null) return;

        if (m_zoneStageButtons.TryGetValue(m_currentZoneStage.zoneName, out UIZoneStageButton markerBtn))
            markerBtn.SetMyFleetMarker(true);
    }

    private void SortButtonsByName()
    {
        var sorted = new List<UIZoneStageButton>(m_zoneStageButtons.Count);
        foreach (var kv in m_zoneStageButtons)
            sorted.Add(kv.Value);
        sorted.Sort((a, b) => ParseZoneStage(a.name).CompareTo(ParseZoneStage(b.name)));
        for (int i = 0; i < sorted.Count; i++)
            sorted[i].transform.SetSiblingIndex(i);
    }

    private UIZoneStageButton GetButtonFromPool()
    {
        if (m_buttonPool.Count > 0)
        {
            UIZoneStageButton btn = m_buttonPool[^1];
            m_buttonPool.RemoveAt(m_buttonPool.Count - 1);
            return btn;
        }
        return Instantiate(m_zoneStageButtonPrefab, m_zoneButtonRoot);
    }

    private void ReturnAllButtonsToPool()
    {
        foreach (var kv in m_zoneStageButtons)
        {
            kv.Value.gameObject.SetActive(false);
            m_buttonPool.Add(kv.Value);
        }
        m_zoneStageButtons.Clear();
    }

    public override void OnTabActivated()
    {
        if (m_datatableZone == null) return;

        // WarpIn 진행 중 재진입이면 취소 후 Idle로 복귀
        if (m_myFleet != null && m_myFleet.m_fleetState == EUnitState.Warp)
        {
            m_myFleet.CancelFleetWarpIn();
            SetFleetState(EUnitState.Idle);
        }

        // 전투 중이 아닐 때만 함대 오프스크린으로 이동 (전투 중에는 갤럭시뷰에서도 전투 지속)
        if (m_myFleet == null || m_myFleet.m_fleetState != EUnitState.Battle)
        {
            if (m_hideFleetCoroutine != null) StopCoroutine(m_hideFleetCoroutine);
            m_hideFleetCoroutine = StartCoroutine(HideFleetDelayed());
        }

        // 최고 클리어 스테이지가 속한 존 그룹으로 열기
        int groupIndex = m_currentZoneStage != null ? ParseZoneGroup(m_currentZoneStage.zoneName) : 1;
        if (groupIndex <= 0) groupIndex = 1;

        ObjectManager.Instance.ChangeZone(groupIndex);

        if (CameraController.Instance != null)
        {
            CameraController.Instance.OnGalaxyViewSettled += OnCameraGalaxyViewSettled;
            var zoneConfig = m_datatableZone.GetZoneByZoneIndex(groupIndex);
            if (zoneConfig != null)
                CameraController.Instance.EnterGalaxyView(
                    zoneConfig.galaxyCameraTarget,
                    zoneConfig.galaxyCameraZoom,
                    zoneConfig.galaxyCameraRotX,
                    zoneConfig.galaxyCameraRotY);
        }

        SetupButtonsForGroup(groupIndex);
        UpdateGroupTabVisual(groupIndex);

        if (m_zoneButtonRoot != null) m_zoneButtonRoot.gameObject.SetActive(false);

        SetOtherTabsVisible(false, includeSelf: true);
        EventManager.TriggerExplorationTabOpened();
    }

    public override void OnTabDeactivated()
    {
        if (CameraController.Instance != null)
            CameraController.Instance.OnGalaxyViewSettled -= OnCameraGalaxyViewSettled;

        // 함대 숨기기 코루틴 진행 중이면 취소하고 즉시 숨기기
        if (m_hideFleetCoroutine != null)
        {
            StopCoroutine(m_hideFleetCoroutine);
            m_hideFleetCoroutine = null;
            if (m_myFleet != null)
                m_myFleet.transform.position = new Vector3(0f, -9999f, 0f);
        }

        ReturnAllButtonsToPool();
        SetOtherTabsVisible(true, includeSelf: true);
        EventManager.TriggerExplorationTabClosed();

        // EnterZoneStage/ExecuteRetreat가 이미 Warp 상태로 갤럭시뷰 탈출 처리 중이면 스킵
        if (m_myFleet == null || m_myFleet.m_fleetState == EUnitState.Warp) return;

        // 전투 중이면 카메라만 전투 위치로 복귀 (함대는 그대로 전투 지속)
        if (m_myFleet.m_fleetState == EUnitState.Battle)
        {
            if (m_battleZoneStage != null)
            {
                Vector3 battlePos = m_datatableZone.ResolveFleetWorldPosition(m_battleZoneStage);
                EventManager.Subscribe_FleetViewRestored(OnFleetViewRestoredAfterBattleReturn);
                CameraController.Instance.ExitGalaxyViewMoveTo(battlePos);
            }
            return;
        }

        ReturnFleetToCurrentZone();
    }

    private void ReturnFleetToCurrentZone()
    {
        int fleetZoneIndex = m_currentZoneStage != null ? m_currentZoneStage.zoneIndex : 1;
        ObjectManager.Instance.ChangeZone(fleetZoneIndex);

        if (m_currentZoneStage != null)
        {
            m_pendingFleetPos = m_datatableZone.ResolveFleetWorldPosition(m_currentZoneStage);
            m_pendingFleetRotY = m_currentZoneStage.fleetRotationY;
        }
        else
        {
            ZoneStageConfig spawnStage = m_datatableZone.GetZoneSpawnStage(fleetZoneIndex);
            m_pendingFleetPos = spawnStage != null ? m_datatableZone.ResolveFleetWorldPosition(spawnStage) : Vector3.zero;
            m_pendingFleetRotY = spawnStage != null ? spawnStage.fleetRotationY : 0f;
        }

        // 카메라 복귀 완료 후 함대 배치 + 워프인 (함대뷰 전환 완료 전에 함대가 나타나지 않도록)
        EventManager.Subscribe_FleetViewRestored(OnFleetViewRestoredAfterReturn);
        CameraController.Instance.ExitGalaxyViewMoveTo(m_pendingFleetPos);
    }

    private void OnFleetViewRestoredAfterReturn()
    {
        EventManager.Unsubscribe_FleetViewRestored(OnFleetViewRestoredAfterReturn);
        ObjectManager.Instance.SetMyFleetPosition(m_pendingFleetPos, m_pendingFleetRotY);
        SetFleetState(EUnitState.Warp);
        m_myFleet.StartFleetWarpIn(onArrived: () =>
        {
            CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
            SetFleetState(EUnitState.Idle);
        });
    }

    private void OnCameraGalaxyViewSettled()
    {
        if (m_zoneButtonRoot != null) m_zoneButtonRoot.gameObject.SetActive(true);

        foreach (var kv in m_zoneStageButtons)
            kv.Value.UpdateScreenPosition();
    }

    private void SetFleetState(EUnitState unitState)
    {
        m_myFleet.SetFleetState(unitState);
    }

    private void OnMyFleetWiped()
    {
        m_isFleetWiped = true;
    }

    private void OnFleetViewRestoredAfterBattleReturn()
    {
        EventManager.Unsubscribe_FleetViewRestored(OnFleetViewRestoredAfterBattleReturn);
        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
    }

    private void OnDestroy()
    {        
        EventManager.Unsubscribe_RetreatRequested(RetreatToPreviousStage);
        EventManager.Unsubscribe_MyFleetDestroyed(OnMyFleetWiped);
        EventManager.Unsubscribe_FleetViewRestored(OnFleetViewRestoredAfterReturn);
        EventManager.Unsubscribe_FleetViewRestored(OnFleetViewRestoredAfterEnterZone);
        EventManager.Unsubscribe_FleetViewRestored(OnFleetViewRestoredAfterBattleReturn);
    }

    private IEnumerator HideFleetDelayed()
    {
        yield return new WaitForSeconds(m_fleetHideDelay);
        if (m_myFleet != null)
            m_myFleet.transform.position = new Vector3(0f, -9999f, 0f);
        m_hideFleetCoroutine = null;
    }

    private int ParseZoneGroup(string zoneName)
    {
        if (string.IsNullOrEmpty(zoneName)) return 0;
        int dashIdx = zoneName.IndexOf('-');
        if (dashIdx <= 0) return 0;
        return int.TryParse(zoneName.Substring(0, dashIdx), out int x) ? x : 0;
    }

    private void OnZoneStageButtonClicked(ZoneStageConfig zoneStage)
    {
        m_selectedZoneStagePerGroup[ParseZoneGroup(zoneStage.zoneName)] = zoneStage;
        ApplyZoneStageSelection(zoneStage);
    }

    private void ApplyZoneStageSelection(ZoneStageConfig zoneStage)
    {
        if (m_selectedZoneStage != null &&
            m_zoneStageButtons.TryGetValue(m_selectedZoneStage.zoneName, out UIZoneStageButton prev))
            prev.SetSelectedUIZoneStageButton(false);

        m_selectedZoneStage = zoneStage;
        if (m_zoneStageButtons.TryGetValue(zoneStage.zoneName, out UIZoneStageButton btn))
            btn.SetSelectedUIZoneStageButton(true);
    }

    private void OnEnterZoneFromButton(ZoneStageConfig zoneStage)
    {
        if (zoneStage == null) return;
        if (m_myFleet.m_fleetState == EUnitState.Warp) return;
        if (m_myFleet.m_fleetState == EUnitState.Battle)
        {
            if (m_battleZoneStage != null && m_battleZoneStage.zoneName == zoneStage.zoneName) return;
            EventManager.Unsubscribe_EnemyFleetKilled(OnEnemyFleetKilled);
            ObjectManager.Instance.StopEnemySpawning();
            ObjectManager.Instance.OrderAllAircraftReturn();
            ObjectManager.Instance.CleanupAllProjectiles();
            ObjectManager.Instance.RemoveAllEnemyFleets();
        }

        if (IsPreviousStageCleared(zoneStage) == false)
        {
            ShowErrorMessage(LocalizationManager.Instance.Get("UITabExploration_PreviousStageRequired"));
            return;
        }

        bool isFirstClear = IsAlreadyCleared(zoneStage) == false;
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title   = zoneStage.zoneName,
            message = LocalizationManager.Instance.Get("exploration_zone_enter_confirm"),
            rewardAmounts = new System.Collections.Generic.List<int>
            {
                zoneStage.mineralClearReward,
                isFirstClear ? zoneStage.techPointClearReward    : 0,
                isFirstClear ? zoneStage.modulePointClearReward  : 0,
                0
            },
            onConfirm = () => ExecuteEnterZone(zoneStage)
        });
    }

    private ZoneStageConfig GetDefaultZoneStageForZone(int zoneIndex)
    {
        var clearedZoneStages = m_myCharacter?.m_characterInfo.clearedZones;
        ZoneStageConfig highest = null;
        ZoneStageConfig lowestUncleared = null;

        for (int i = 0; i < m_datatableZone.ZoneStageCount; i++)
        {
            ZoneStageConfig zoneStage = m_datatableZone.GetZoneStage(i);
            if (zoneStage == null || ParseZoneGroup(zoneStage.zoneName) != zoneIndex) continue;

            int stage = ParseZoneStage(zoneStage.zoneName);
            if (stage == 0) continue; // x-0 스폰 마커는 선택 대상 제외

            if (highest == null || stage > ParseZoneStage(highest.zoneName))
                highest = zoneStage;

            bool isCleared = clearedZoneStages != null && clearedZoneStages.Contains(zoneStage.zoneName);
            if (isCleared == false && (lowestUncleared == null || stage < ParseZoneStage(lowestUncleared.zoneName)))
                lowestUncleared = zoneStage;
        }

        return lowestUncleared ?? highest;
    }

    private int ParseZoneStage(string zoneName)
    {
        if (string.IsNullOrEmpty(zoneName)) return 0;
        int dashIdx = zoneName.IndexOf('-');
        if (dashIdx < 0 || dashIdx >= zoneName.Length - 1) return 0;
        return int.TryParse(zoneName[(dashIdx + 1)..], out int y) ? y : 0;
    }




    private void ExecuteEnterZone(ZoneStageConfig zoneStage)
    {
        TryEnterZoneStageWithAd(zoneStage);
    }

    private void TryEnterZoneStageWithAd(ZoneStageConfig zonestage)
    {
#if UNITY_EDITOR
        EnterZoneStage(zonestage);
#else
        if (AdManager.s_devSkipAd == true)
        {
            EnterZoneStage(zonestage);
            return;
        }

        bool adInstanceNull = AdManager.Instance == null;
        bool adReady = adInstanceNull == false && AdManager.Instance.IsRewardedAdReady;

        if (adInstanceNull == false && adReady == true)
        {
            AdManager.Instance.ShowRewardedAd(result =>
            {
                if (result == EAdResult.Rewarded)
                    EnterZoneStage(zonestage);
                else if (result == EAdResult.Failed)
                    EnterZoneStage(zonestage);
                else if (result == EAdResult.UserClosed)
                    ShowErrorMessage("[광고] 광고를 시청해야 입장할 수 있습니다");
            });
        }
        else
        {
            if (adInstanceNull == false)
                AdManager.Instance.RequestLoad();
            Debug.Log("[Ad] 광고 미준비 상태로 입장");
            EnterZoneStage(zonestage);
        }
#endif
    }

    private void EnterZoneStage(ZoneStageConfig zoneStage)
    {
        SetFleetState(EUnitState.Warp); // CloseAllTabs→OnTabDeactivated에서 ReturnFleetToCurrentZone 스킵용

        m_battleZoneStage = zoneStage;
        RefreshCurrentZoneStageButton();
        EventManager.Subscribe_EnemyFleetKilled(OnEnemyFleetKilled);

        m_pendingFleetPos  = m_datatableZone.ResolveFleetWorldPosition(zoneStage);
        m_pendingFleetRotY = zoneStage.fleetRotationY;
        ObjectManager.Instance.ChangeZone(zoneStage.zoneIndex);

        if (m_tabSystemParent != null) m_tabSystemParent.CloseAllTabs();
        SetOtherTabsVisible(false, includeSelf: true);

        // 카메라 복귀 완료 후 함대 배치 + 워프인
        EventManager.Subscribe_FleetViewRestored(OnFleetViewRestoredAfterEnterZone);
        CameraController.Instance.ExitGalaxyViewMoveTo(m_pendingFleetPos);
    }

    private void OnFleetViewRestoredAfterEnterZone()
    {
        EventManager.Unsubscribe_FleetViewRestored(OnFleetViewRestoredAfterEnterZone);
        ObjectManager.Instance.SetMyFleetPosition(m_pendingFleetPos, m_pendingFleetRotY);
        var cam = CameraController.Instance;
        m_myFleet.StartFleetWarpIn(onArrived: () =>
        {
            SetOtherTabsVisible(true, includeSelf: true);
            cam.SetTargetOfCameraController(m_myFleet.transform);
            SetFleetState(EUnitState.Battle);
            if (m_battleZoneStage != null)
            {
                bool isFirstClear = IsAlreadyCleared(m_battleZoneStage) == false;
                EventManager.TriggerZoneEntered(m_battleZoneStage.zoneName, isFirstClear);
                StartBattleInZone(m_battleZoneStage);
            }
        });
    }

    private void RefreshCurrentZoneStageButton()
    {
        if (m_currentZoneStageButton != null)
        {
            m_currentZoneStageButton.SetSelectedUIZoneStageButton(false);
            // if (IsAlreadyCleared(m_currentZoneStageButton.ZoneStageConfig) == false)
            //     m_currentZoneStageButton.SetState(EZoneState.Current);
        }

        m_currentZoneStageButton = null;
        if (m_currentZoneStage == null) return;

        if (m_zoneStageButtons.TryGetValue(m_currentZoneStage.zoneName, out UIZoneStageButton btn))
            m_currentZoneStageButton = btn;

        if (m_currentZoneStageButton != null)
            m_currentZoneStageButton.SetSelectedUIZoneStageButton(true);
    }

    private void StartBattleInZone(ZoneStageConfig zoneStage)
    {
        ObjectManager.Instance.StartSpawnEnemies(zoneStage, (_) => RetreatToPreviousStage());
    }

    private bool IsAlreadyCleared(ZoneStageConfig zoneStage)
    {
        var clearedZones = m_myCharacter.m_characterInfo.clearedZones;
        return clearedZones != null && clearedZones.Contains(zoneStage.zoneName);
    }

    private bool IsPreviousStageCleared(ZoneStageConfig zoneStage)
    {
        int group = ParseZoneGroup(zoneStage.zoneName);
        int stage = ParseZoneStage(zoneStage.zoneName);

        string prevStageName;
        if (stage > 1)
        {
            prevStageName = $"{group}-{stage - 1}";
        }
        else
        {
            if (group <= 1) return true; // 1-1은 조건 없음

            int maxStage = 0;
            for (int i = 0; i < m_datatableZone.ZoneStageCount; i++)
            {
                ZoneStageConfig zs = m_datatableZone.GetZoneStage(i);
                if (zs == null || ParseZoneGroup(zs.zoneName) != group - 1) continue;
                int s = ParseZoneStage(zs.zoneName);
                if (s > maxStage) maxStage = s;
            }
            if (maxStage == 0) return true;
            prevStageName = $"{group - 1}-{maxStage}";
        }

        var cleared = m_myCharacter?.m_characterInfo.clearedZones;
        return cleared != null && cleared.Contains(prevStageName);
    }

    private void OnEnemyFleetKilled()
    {
        if (m_battleZoneStage == null) return;

        var request = new ClearZoneStageRequest
        {
            zoneName = m_battleZoneStage.zoneName,
            mineralRemain = m_myCharacter != null ? m_myCharacter.GetMineral() : 0,
        };
        NetworkManager.Instance.ClearZoneStage(request, OnClearZoneStageResponse);
    }

    private bool m_pendingRewardIsFirstClear;

    private void OnClearZoneStageResponse(ApiResponse<ClearZoneStageResponse> response)
    {
        if (response.errorCode != 0)
        {
            Debug.LogWarning($"[Zone] ClearZoneStage 에러: {ErrorCodeMapping.GetMessage(response.errorCode)} ({response.errorCode})");
            StayInCurrentStage();
            return;
        }

        var character = DataManager.Instance.m_currentCharacter;
        if (response.data.isFirstClear == true && character != null)
        {
            if (character.m_characterInfo.clearedZones == null)
                character.m_characterInfo.clearedZones = new List<string>();

            string newlyCleared = response.data.clearedZoneName;
            if (character.m_characterInfo.clearedZones.Contains(newlyCleared) == false)
                character.m_characterInfo.clearedZones.Add(newlyCleared);

            if (m_zoneStageButtons.TryGetValue(newlyCleared, out UIZoneStageButton clearedBtn))
            {
                clearedBtn.SetStateUIZoneStageButton(EZoneState.Cleared);
                clearedBtn.RefreshRewardRowsForState(EZoneState.Cleared);
            }

            RefreshCurrentZoneStageButton();
            SelectNextZoneStage(newlyCleared);
            UpdateCurrentZoneStageOnClear(newlyCleared);
        }
        else if (m_battleZoneStage != null)
        {
            // 재도전 클리어: m_currentZoneStage를 방금 클리어한 스테이지로 갱신
            RefreshCurrentZoneStageButton();
            UpdateCurrentZoneStageOnClear(m_battleZoneStage.zoneName);
        }

        m_pendingRewardIsFirstClear = response.data.isFirstClear;
        StayInCurrentStage();
        string title = LocalizationManager.Instance.Get("exploration_battle_victory");
        UIManager.Instance.StartCoroutine(ShowRewardPopupDelayed(title, 2f));
    }

    private IEnumerator ShowRewardPopupDelayed(string title, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        if (m_battleZoneStage == null) { yield break; }

        bool isFirstClear = m_pendingRewardIsFirstClear;
        var rewards = new List<int>
        {
            m_battleZoneStage.mineralClearReward,
            isFirstClear ? m_battleZoneStage.techPointClearReward   : 0,
            isFirstClear ? m_battleZoneStage.modulePointClearReward : 0,
            0
        };

        var loc = LocalizationManager.Instance;
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title         = title,
            rewardAmounts = rewards,
            cancelText1   = loc.Get("Simple_NoThanks"),
            cancelText2   = loc.Get("Simple_MineralX1"),
            confirmText1  = loc.Get("Simple_WatchAD"),
            confirmText2  = loc.Get("Simple_MineralX", 2),
            onCancel      = OnClaimRewardX1,
            onConfirm     = OnWatchAdForDoubleReward,
        });
    }

    private void OnClaimRewardX1()
    {
        if (m_battleZoneStage == null) { return; }
        var request = new ClaimZoneRewardRequest { zoneName = m_battleZoneStage.zoneName, watchedAd = false };
        NetworkManager.Instance.ClaimZoneReward(request, OnClaimZoneRewardResponse);
    }

    private void OnWatchAdForDoubleReward()
    {
#if UNITY_EDITOR
        if (AdManager.s_devSkipAd == true)
        {
            SendClaimZoneReward(true);
            return;
        }
#endif
        bool adReady = AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady;
        if (adReady == false)
        {
            SendClaimZoneReward(false);
            return;
        }

        AdManager.Instance.ShowRewardedAd(result =>
        {
            SendClaimZoneReward(result == EAdResult.Rewarded);
        });
    }

    private void SendClaimZoneReward(bool watchedAd)
    {
        if (m_battleZoneStage == null) { return; }
        var request = new ClaimZoneRewardRequest { zoneName = m_battleZoneStage.zoneName, watchedAd = watchedAd };
        NetworkManager.Instance.ClaimZoneReward(request, OnClaimZoneRewardResponse);
    }

    private void OnClaimZoneRewardResponse(ApiResponse<ClaimZoneRewardResponse> response)
    {
        if (response.errorCode != 0)
        {
            Debug.LogWarning($"[Zone] ClaimZoneReward 에러: {ErrorCodeMapping.GetMessage(response.errorCode)} ({response.errorCode})");
            return;
        }

        var character = DataManager.Instance.m_currentCharacter;
        if (character != null && character.m_characterInfo != null)
        {
            character.UpdateMineral(response.data.mineralRemain);
            character.UpdateTechPoint(response.data.techPointRemain);
            character.UpdateModulePointMaxGot(response.data.modulePointMaxGot); // 이벤트 발생 전에 먼저 갱신
            character.UpdateModulePoint(response.data.modulePointRemain);
        }
        m_battleZoneStage = null;
    }

    private void SelectNextZoneStage(string clearedZoneName)
    {
        int group = ParseZoneGroup(clearedZoneName);
        int stage = ParseZoneStage(clearedZoneName);

        ZoneStageConfig nextStage = null;
        for (int i = 1; i < m_datatableZone.ZoneStageCount; i++)
        {
            ZoneStageConfig zs = m_datatableZone.GetZoneStage(i);
            if (zs == null) continue;
            if (ParseZoneGroup(zs.zoneName) == group && ParseZoneStage(zs.zoneName) == stage + 1)
            {
                nextStage = zs;
                break;
            }
        }

        if (nextStage == null)
        {
            int nextGroup = group + 1;
            for (int i = 1; i < m_datatableZone.ZoneStageCount; i++)
            {
                ZoneStageConfig zs = m_datatableZone.GetZoneStage(i);
                if (zs != null && ParseZoneGroup(zs.zoneName) == nextGroup && ParseZoneStage(zs.zoneName) > 0)
                {
                    nextStage = zs;
                    break;
                }
            }
        }

        if (nextStage == null) return;
        m_selectedZoneStagePerGroup[ParseZoneGroup(nextStage.zoneName)] = nextStage;
    }

    private void UpdateCurrentZoneStageOnClear(string clearedZoneName)
    {
        ZoneStageConfig clearedStage = m_datatableZone.GetZoneStageByName(clearedZoneName);
        if (clearedStage == null) return;
        m_currentZoneStage = clearedStage;
    }

    private void RetreatToPreviousStage()
    {
        if (m_isFleetWiped == false)
        {
            UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
            {
                title   = LocalizationManager.Instance.Get("UITabExploration_RetreatTitle"),
                message = LocalizationManager.Instance.Get("UITabExploration_RetreatConfirm"),
                onConfirm = DoRetreatSequence
            });
            return;
        }
        DoRetreatSequence();
    }

    private void DoRetreatSequence()
    {
        EventManager.Unsubscribe_EnemyFleetKilled(OnEnemyFleetKilled);
        ObjectManager.Instance.StopEnemySpawning();
        ObjectManager.Instance.OrderAllAircraftReturn();
        ObjectManager.Instance.CleanupAllProjectiles();
        ObjectManager.Instance.RemoveAllEnemyFleets();

        ZoneStageConfig retreatStage = FindPreviousClearedStage();

        Vector3 retreatPosition;
        float retreatRotationY;

        if (retreatStage != null)
        {
            retreatPosition = m_datatableZone.ResolveFleetWorldPosition(retreatStage);
            retreatRotationY = retreatStage.fleetRotationY;
        }
        else
        {
            // 한 스테이지도 클리어 못했으므로 해당 존 x-0 스폰 마커로 복귀
            int currentGroup = m_battleZoneStage != null ? ParseZoneGroup(m_battleZoneStage.zoneName) : 1;
            ZoneStageConfig spawnStage = m_datatableZone.GetZoneSpawnStage(currentGroup);
            retreatPosition  = spawnStage != null ? m_datatableZone.ResolveFleetWorldPosition(spawnStage) : Vector3.zero;
            retreatRotationY = spawnStage != null ? spawnStage.fleetRotationY : 0f;
        }

        if (m_isFleetWiped == true)
            ObjectManager.Instance.StartCoroutine(ShowWipePopupAfterDelay(retreatPosition, retreatRotationY));
        else
            ExecuteRetreat(retreatPosition, retreatRotationY);
    }

    private IEnumerator ShowWipePopupAfterDelay(Vector3 retreatPosition, float retreatRotationY)
    {
        yield return m_wipePopupDelay;
        string title = LocalizationManager.Instance.Get("exploration_fleet_wiped");
        string message = LocalizationManager.Instance.Get("exploration_wipe_retreat");
        UIManager.Instance.ShowPopupAlert(new AlertPopupConfig
        {
            title = title,
            message = message,
            onConfirm = () => ExecuteRetreat(retreatPosition, retreatRotationY),
            autoCloseSec = 5f,
        });
    }

    private void ExecuteRetreat(Vector3 retreatPosition, float retreatRotationY)
    {
        // 카메라를 먼저 후퇴 위치로 이동 (갤럭시뷰 종료 포함 — 이후 CloseAllTabs→ExitGalaxyView 중복 호출 방지)
        CameraController.Instance.ExitGalaxyViewMoveTo(retreatPosition);
        // CloseAllTabs 전에 함대 상태를 battle -> warp 로 변경
        SetFleetState(EUnitState.Warp);
        if (m_tabSystemParent != null) m_tabSystemParent.CloseAllTabs();
        SetOtherTabsVisible(false, includeSelf: true);

        ObjectManager.Instance.SetMyFleetPosition(retreatPosition, retreatRotationY);

        int retreatGroup = m_battleZoneStage != null ? ParseZoneGroup(m_battleZoneStage.zoneName) : (m_currentZoneStage != null ? ParseZoneGroup(m_currentZoneStage.zoneName) : 1);
        if (retreatGroup <= 0) retreatGroup = 1;

        m_myFleet.StartFleetWarpIn(onArrived: () =>
        {
            SetOtherTabsVisible(true, includeSelf: true);
            CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);

            m_battleZoneStage = null;
            RefreshCurrentZoneStageButton();

            SetFleetState(EUnitState.Idle);
            UpdateGroupTabVisual(retreatGroup);
            SetupButtonsForGroup(retreatGroup);

            if (m_isFleetWiped == true)
            {
                m_myFleet.RebuildFleet(0.1f);
                m_isFleetWiped = false;
            }
            else
            {
                m_myFleet.RestoreDestroyedShips(0.1f);
            }
        });
    }

    private void StayInCurrentStage()
    {
        EventManager.Unsubscribe_EnemyFleetKilled(OnEnemyFleetKilled);
        ObjectManager.Instance.StopEnemySpawning();
        ObjectManager.Instance.OrderAllAircraftReturn();
        ObjectManager.Instance.CleanupAllProjectiles();
        ObjectManager.Instance.RemoveAllEnemyFleets();

        SetFleetState(EUnitState.Idle);

        if (m_myFleet != null)
        {
            if (m_isFleetWiped == true)
            {
                m_myFleet.RebuildFleet(0.1f);
                m_isFleetWiped = false;
            }
            else
            {
                m_myFleet.RestoreDestroyedShips(0.1f);
            }
        }

        // 갤럭시뷰 중에 전투가 완료된 경우 함대를 즉시 오프스크린으로 이동
        if (CameraController.Instance != null && CameraController.Instance.IsGalaxyView == true)
        {
            if (m_myFleet != null)
                m_myFleet.transform.position = new Vector3(0f, -9999f, 0f);
        }

        int battleGroup = m_battleZoneStage != null ? ParseZoneGroup(m_battleZoneStage.zoneName) : (m_currentZoneStage != null ? ParseZoneGroup(m_currentZoneStage.zoneName) : 1);
        if (battleGroup <= 0) battleGroup = 1;
        UpdateGroupTabVisual(battleGroup);
        SetupButtonsForGroup(battleGroup);
    }

    // 현재 존에서 클리어한 스테이지 중 가장 높은 것 반환 — 없으면 null(→ 해당 존 최초지점으로 복귀)
    private ZoneStageConfig FindPreviousClearedStage()
    {
        if (m_battleZoneStage == null) return null;

        int group = ParseZoneGroup(m_battleZoneStage.zoneName);
        var cleared = m_myCharacter?.m_characterInfo.clearedZones;
        if (cleared == null || cleared.Count == 0) return null;

        ZoneStageConfig highestCleared = null;
        int highestStageNum = -1;

        for (int i = 0; i < m_datatableZone.ZoneStageCount; i++)
        {
            ZoneStageConfig zs = m_datatableZone.GetZoneStage(i);
            if (zs == null || ParseZoneGroup(zs.zoneName) != group) continue;
            if (cleared.Contains(zs.zoneName) == false) continue;

            int stageNum = ParseZoneStage(zs.zoneName);
            if (stageNum > highestStageNum)
            {
                highestStageNum = stageNum;
                highestCleared = zs;
            }
        }

        return highestCleared;
    }
}
