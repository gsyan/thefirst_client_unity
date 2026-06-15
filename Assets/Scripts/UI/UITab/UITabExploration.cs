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
    [SerializeField] private UnityEngine.UI.Button m_backgroundCloseButton; // 빈 곳 클릭 시 탭 닫기용 투명 풀스크린 버튼
    
    [Header("그룹 탭")]
    [SerializeField] private Transform m_zoneTabButtonContainer;
    private UISelectableButton[] m_zoneTabButtons;
    private readonly List<UIZoneStageButton> m_buttonPool = new();

    private SpaceFleet m_playerFleet;
    private Character m_myCharacter;
    private ZoneStageConfig m_currentZoneStage;  // 클리어 진행 위치
    private ZoneStageConfig m_battleZoneStage;   // 현재 전투 중인 존 (입장 시 set, 후퇴/완료 후 null)
    private ZoneStageConfig m_selectedZoneStage;
    private UIZoneStageButton m_currentZoneStageButton;
    private readonly Dictionary<string, UIZoneStageButton> m_zoneStageButtons = new Dictionary<string, UIZoneStageButton>();
    private readonly Dictionary<int, ZoneStageConfig> m_selectedZoneStagePerGroup = new();

    [Header("개발 설정")]
    [SerializeField] private bool m_requirePreviousStageCleared = true;

    private Vector3 m_pendingFleetPos;
    private float m_pendingFleetRotY;
    private FleetInfo m_pendingEnemyFleetInfo;

    private bool m_isFleetWiped;
    private bool m_isBattleEnded;
    private readonly WaitForSeconds m_wipePopupDelay = new WaitForSeconds(2f);

    public override void InitializeUITab()
    {
        InitializeUITabExploration();
    }

    private void InitializeUITabExploration()
    {
        m_myCharacter = DataManager.Instance.m_currentCharacter;
        if (m_myCharacter == null || ObjectManager.Instance.m_myFleet == null) return;
        m_playerFleet = ObjectManager.Instance.m_myFleet;
        

        EventManager.Subscribe_RetreatExploration(OnRetreatZoneStage);
        EventManager.Subscribe_MyFleetDestroyed(OnMyFleetWiped);
        EventManager.Subscribe_ZoneStageBattleEnd(OnZoneStageBattleEnd);
        EventManager.Subscribe_PvpBattleStart(OnPvpBattleStarted);

        if (m_backgroundCloseButton != null)
            m_backgroundCloseButton.onClick.AddListener(() => m_tabSystemParent.SwitchToTab(-1));

        SetupZoneTabButtons();
        InitializeZoneStageButtons();
        SetFleetState(EUnitState.Idle);

        SetInitialFleetPosition();
    }

    private void SetInitialFleetPosition()
    {
        var clearedZones = m_myCharacter.m_characterInfo != null ? m_myCharacter.m_characterInfo.clearedZones : null;

        ZoneStageConfig targetStage = null;
        if (clearedZones != null && clearedZones.Count > 0)
            targetStage = m_datatableZone.GetZoneStageByName(clearedZones[^1]);

        // 신규 유저 또는 유효하지 않은 클리어 데이터 — zone1 스폰 마커로 fallback
        if (targetStage == null)
            targetStage = m_datatableZone.GetZoneFirstStage(1);

        if (targetStage == null) return;

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
            btn.InitializeUIZoneStageButton(captured, capturedWorldPos, () => OnZoneStageButtonClicked(captured), () => OnEnterZoneStageFromButton(captured), state, worldCam);
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
        if (m_playerFleet != null && m_playerFleet.m_fleetState == EUnitState.Warp)
        {
            m_playerFleet.CancelFleetWarpIn();
            SetFleetState(EUnitState.Idle);
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

        HideTabButtons();
        EventManager.TriggerExplorationTabOpened();
    }

    public override void OnTabDeactivated()
    {
        if (CameraController.Instance != null)
            CameraController.Instance.OnGalaxyViewSettled -= OnCameraGalaxyViewSettled;

        ReturnAllButtonsToPool();
        EventManager.TriggerExplorationTabClosed();

        ReturnFleetView();
    }

    private void ReturnFleetView()
    {
        int fleetZoneIndex;
        if (m_battleZoneStage != null)
            fleetZoneIndex = m_battleZoneStage.zoneIndex;
        else
            fleetZoneIndex = m_currentZoneStage != null ? m_currentZoneStage.zoneIndex : 1;
        ObjectManager.Instance.ChangeZone(fleetZoneIndex);

        Vector3 targetCameraPosition;

        // 전투 중이면 카메라만 포커스 타겟 위치로 복귀 (함대는 그대로 전투 지속)
        if (m_battleZoneStage != null && m_playerFleet.m_fleetState.IsBattleState() == true)
        {
            // m_pendingFleetPos 을 설정하지 않는다.
            targetCameraPosition = CameraController.Instance.GetFocusTargetPosition();
            RefreshTabButtons();
        }
        // 전투 목표 stage 정해진 상태에서 워프 상태라면, 새로운 스테이지로 이동한다는 것
        else if (m_battleZoneStage != null && m_playerFleet.m_fleetState == EUnitState.Warp)
        {
            m_pendingFleetPos = m_datatableZone.ResolveFleetWorldPosition(m_battleZoneStage);
            m_pendingFleetRotY = m_battleZoneStage.fleetRotationY;
            targetCameraPosition = m_pendingFleetPos;
            // 카메라 복귀 완료 후 함대 배치 + 워프인, RefreshTabButtons는 워프인 완료 후 OnFleetViewRestoredAfterEnterZone에서 처리
            EventManager.Subscribe_FleetViewRestored(OnFleetViewRestoredAfterEnterZone);
        }
        else if (m_currentZoneStage != null)
        {
            m_pendingFleetPos = m_datatableZone.ResolveFleetWorldPosition(m_currentZoneStage);
            m_pendingFleetRotY = m_currentZoneStage.fleetRotationY;
            targetCameraPosition = m_pendingFleetPos;
            RefreshTabButtons();
        }
        else
        {
            ZoneStageConfig spawnStage = m_datatableZone.GetZoneFirstStage(fleetZoneIndex);
            m_pendingFleetPos = spawnStage != null ? m_datatableZone.ResolveFleetWorldPosition(spawnStage) : Vector3.zero;
            m_pendingFleetRotY = spawnStage != null ? spawnStage.fleetRotationY : 0f;
            targetCameraPosition = m_pendingFleetPos;
            RefreshTabButtons();
        }

        CameraController.Instance.ExitGalaxyView(targetCameraPosition);
    }

    private void OnFleetViewRestoredAfterEnterZone()
    {
        EventManager.Unsubscribe_FleetViewRestored(OnFleetViewRestoredAfterEnterZone);
        ObjectManager.Instance.SetMyFleetPosition(m_pendingFleetPos, m_pendingFleetRotY);
        var cam = CameraController.Instance;
        m_playerFleet.StartFleetWarpIn(onArrived: () =>
        {
            RefreshTabButtons();
            cam.SetTargetOfCameraController(m_playerFleet.transform);
            SetFleetState(EUnitState.BattleExploration);
            if (m_battleZoneStage != null)
            {
                bool isFirstClear = IsAlreadyCleared(m_battleZoneStage) == false;
                EventManager.TriggerZoneEntered(m_battleZoneStage.zoneName, isFirstClear);
                StartBattleInZone(m_battleZoneStage);
            }
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
        m_playerFleet.SetFleetState(unitState);
    }

    private void OnMyFleetWiped()
    {
        m_isFleetWiped = true;
    }

    private void OnFleetViewRestoredAfterBattleReturn()
    {
        EventManager.Unsubscribe_FleetViewRestored(OnFleetViewRestoredAfterBattleReturn);
        CameraController.Instance.SetTargetOfCameraController(m_playerFleet.transform);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_RetreatExploration(OnRetreatZoneStage);
        EventManager.Unsubscribe_MyFleetDestroyed(OnMyFleetWiped);
        EventManager.Unsubscribe_ZoneStageBattleEnd(OnZoneStageBattleEnd);
        EventManager.Unsubscribe_PvpBattleStart(OnPvpBattleStarted);
        EventManager.Unsubscribe_FleetViewRestored(OnFleetViewRestoredAfterEnterZone);
        EventManager.Unsubscribe_FleetViewRestored(OnFleetViewRestoredAfterBattleReturn);
    }

    private void OnPvpBattleStarted()
    {
        EventManager.Unsubscribe_EnemyFleetKilled(OnEnemyFleetKilled);
        m_battleZoneStage = null;
        m_pendingClaimZoneName = null;
    }

    private void SetMyFleetToHiddenPosition()
    {
        if (m_playerFleet != null)
            m_playerFleet.transform.position = new Vector3(0f, -9999f, 0f);
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

    private void OnEnterZoneStageFromButton(ZoneStageConfig zoneStage)
    {
        if (zoneStage == null) return;
        if (m_playerFleet.m_fleetState == EUnitState.Warp) return;
        if (m_playerFleet.m_fleetState.IsBattleState() == true)
        {
            if (m_battleZoneStage != null && m_battleZoneStage.zoneName == zoneStage.zoneName) return;
        }

        if (m_requirePreviousStageCleared == true && IsPreviousStageCleared(zoneStage) == false)
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
            onConfirm = () => EnterZoneStageWithServerData(zoneStage),
            onCancel  = () => { }
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

    private void EnterZoneStage(ZoneStageConfig zoneStage)
    {
        if (m_playerFleet.m_fleetState.IsBattleState() == true)
        {
            EventManager.Unsubscribe_EnemyFleetKilled(OnEnemyFleetKilled);
            ObjectManager.Instance.StopEnemySpawning();
            ObjectManager.Instance.OrderAllAircraftReturn();
            ObjectManager.Instance.CleanupAllProjectiles();
            ObjectManager.Instance.RemoveAllEnemyFleets();
        }

        // 실제 제3의 지역으로 위치 이동
        SetMyFleetToHiddenPosition();
        // 전투중 다른 스테이지 위치로 이동 위해
        // 함대가 전투중이면 OnTabDeactivated 에서는 단순히 uitabexploration 을 열었다 닫은것으로 인식
        SetFleetState(EUnitState.Warp);

        m_battleZoneStage = zoneStage;
        RefreshCurrentZoneStageButton();
        EventManager.Subscribe_EnemyFleetKilled(OnEnemyFleetKilled);

        m_pendingFleetPos  = m_datatableZone.ResolveFleetWorldPosition(zoneStage);
        m_pendingFleetRotY = zoneStage.fleetRotationY;

        if (m_tabSystemParent != null) m_tabSystemParent.CloseAllTabs();
        HideTabButtons();
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

    private void OnZoneStageBattleEnd(bool isVictory)
    {
        if (m_isBattleEnded == true) return;
        m_isBattleEnded = true;
        EventManager.Unsubscribe_EnemyFleetKilled(OnEnemyFleetKilled);

        ZoneStageConfig retreatStage = m_currentZoneStage;

        Vector3 retreatPosition;
        float retreatRotationY;

        if (retreatStage != null)
        {
            retreatPosition = m_datatableZone.ResolveFleetWorldPosition(retreatStage);
            retreatRotationY = retreatStage.fleetRotationY;
        }
        else
        {
            // 한 스테이지도 클리어 못한 신규 유저 → 존1-0 스폰 마커로 복귀
            ZoneStageConfig spawnStage = m_datatableZone.GetZoneFirstStage(1);
            retreatPosition  = spawnStage != null ? m_datatableZone.ResolveFleetWorldPosition(spawnStage) : Vector3.zero;
            retreatRotationY = spawnStage != null ? spawnStage.fleetRotationY : 0f;
        }

        if (m_isFleetWiped == true)
            ObjectManager.Instance.StartCoroutine(ShowWipePopupAfterDelay(retreatPosition, retreatRotationY));
        else
            ExecuteRetreat(retreatPosition, retreatRotationY);
    }

    private void EnterZoneStageWithServerData(ZoneStageConfig zoneStage)
    {
        var request = new GetStageEnemiesRequest { zoneName = zoneStage.zoneName };
        NetworkManager.Instance.GetStageEnemies(request, (response) => OnGetStageEnemiesResponse(zoneStage, response));
    }

    private void OnGetStageEnemiesResponse(ZoneStageConfig zoneStage, ApiResponse<GetStageEnemiesResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            int code = -1;
            if (response != null) code = response.errorCode;
            Debug.LogWarning($"[Zone] GetStageEnemies 실패: errorCode={code}");
            UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
            {
                title     = zoneStage.zoneName,
                message   = LocalizationManager.Instance.Get("exploration_zone_enter_failed"),
                onConfirm = () => { },
            });
            return;
        }
        m_pendingEnemyFleetInfo = response.data.enemyFleet;
        EnterZoneStage(zoneStage);
    }

    private void StartBattleInZone(ZoneStageConfig zoneStage)
    {
        m_isBattleEnded = false;
        ObjectManager.Instance.StartSpawnEnemiesFromFleetInfo(m_pendingEnemyFleetInfo);
        m_pendingEnemyFleetInfo = null;
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

        m_pendingClaimZoneName = m_battleZoneStage.zoneName; // 후퇴로 m_battleZoneStage가 null이 되기 전에 캡처

        var request = new ClearZoneStageRequest
        {
            zoneName = m_battleZoneStage.zoneName,
            mineralRemain = m_myCharacter != null ? m_myCharacter.GetMineral() : 0,
        };
        NetworkManager.Instance.ClearZoneStage(request, OnClearZoneStageResponse);
    }

    private bool m_pendingRewardIsFirstClear;
    private string m_pendingClaimZoneName; // ExecuteRetreat로 m_battleZoneStage가 null이 돼도 보상 청구 가능하도록
    private static readonly WaitForSecondsRealtime s_victorySequenceWait = new WaitForSecondsRealtime(1.5f);

    private void OnClearZoneStageResponse(ApiResponse<ClearZoneStageResponse> response)
    {
        if (response.errorCode != 0)
        {
            Debug.LogWarning($"[Zone] ClearZoneStage 에러: {ErrorCodeMapping.GetMessage(response.errorCode)} ({response.errorCode})");
            m_pendingClaimZoneName = null;
            ObjectManager.Instance.CleanupAllProjectiles();
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

        // 전투 소모 미네랄 + 미네랄 강화 초기화 반영
        var characterForMineral = DataManager.Instance.m_currentCharacter;
        if (characterForMineral != null)
            characterForMineral.UpdateMineral(response.data.mineralRemain);

        if (response.data.updatedFleetInfo != null)
            ApplyUpdatedFleetInfo(response.data.updatedFleetInfo);

        StayInCurrentStage();
        string title = LocalizationManager.Instance.Get("exploration_battle_victory");
        UIManager.Instance.StartCoroutine(StartVictorySequence(title));
    }

    private void ApplyUpdatedFleetInfo(FleetInfo updatedFleetInfo)
    {
        SpaceFleet fleet = ObjectManager.Instance.m_myFleet;
        if (fleet == null || updatedFleetInfo.ships == null) return;

        foreach (ShipInfo updatedShip in updatedFleetInfo.ships)
        {
            SpaceShip ship = fleet.FindShip(updatedShip.id);
            if (ship == null || updatedShip.bodies == null) continue;

            foreach (ModuleBodyInfo updatedBody in updatedShip.bodies)
            {
                ApplyModuleBodyUpdate(ship, updatedBody);
            }
            EventManager.Trigger_ShipStatsChanged(ship);
        }
    }

    private void ApplyModuleBodyUpdate(SpaceShip ship, ModuleBodyInfo updatedBody)
    {
        // Body 업데이트
        ModuleBase bodyModule = ship.FindModule(updatedBody.bodyIndex, EModuleType.body, 0);
        if (bodyModule != null)
        {
            bool bodySubTypeChanged = bodyModule.GetModuleSubType() != updatedBody.moduleSubType;
            bool bodyLevelChanged   = bodyModule.GetModuleLevel()   != updatedBody.moduleLevel;
            if (bodySubTypeChanged == true || bodyLevelChanged == true)
                ship.ApplyModuleChange(updatedBody.bodyIndex, EModuleType.body, updatedBody.moduleSubType, 0, updatedBody.moduleLevel);

            // ApplyModuleChange 후 오브젝트가 교체됐을 수 있으므로 재탐색
            bodyModule = ship.FindModule(updatedBody.bodyIndex, EModuleType.body, 0);
            if (bodyModule != null)
            {
                ship.SetModuleInvestedMineral(updatedBody.bodyIndex, EModuleType.body, 0, updatedBody.investedMineral);
                bodyModule.SetModulePointInfo(updatedBody.modulePointSubType, updatedBody.modulePointLevel);
            }
        }

        ApplyModuleInfoListUpdate(ship, updatedBody.beams,    EModuleType.beam);
        ApplyModuleInfoListUpdate(ship, updatedBody.missiles, EModuleType.missile);
        ApplyModuleInfoListUpdate(ship, updatedBody.hangers,  EModuleType.hanger);
    }

    private void ApplyModuleInfoListUpdate(SpaceShip ship, List<ModuleInfo> moduleInfos, EModuleType moduleType)
    {
        if (moduleInfos == null) return;
        foreach (ModuleInfo updatedModule in moduleInfos)
        {
            ModuleBase existing = ship.FindModule(updatedModule.bodyIndex, moduleType, updatedModule.slotIndex);
            if (existing == null) continue;

            bool subTypeChanged = existing.GetModuleSubType() != updatedModule.moduleSubType;
            bool levelChanged   = existing.GetModuleLevel()   != updatedModule.moduleLevel;
            if (subTypeChanged == true || levelChanged == true)
                ship.ApplyModuleChange(updatedModule.bodyIndex, moduleType, updatedModule.moduleSubType, updatedModule.slotIndex, updatedModule.moduleLevel);

            ship.SetModuleInvestedMineral(updatedModule.bodyIndex, moduleType, updatedModule.slotIndex, updatedModule.investedMineral);

            // ApplyModuleChange 후 재탐색
            ModuleBase refreshed = ship.FindModule(updatedModule.bodyIndex, moduleType, updatedModule.slotIndex);
            if (refreshed != null)
                refreshed.SetModulePointInfo(updatedModule.modulePointSubType, updatedModule.modulePointLevel);
        }
    }

    private IEnumerator StartVictorySequence(string title)
    {
        yield return s_victorySequenceWait;
        ObjectManager.Instance.CleanupAllProjectiles();

        ZoneStageConfig pendingStage = m_datatableZone.GetZoneStageByName(m_pendingClaimZoneName);
        if (pendingStage == null) { m_pendingClaimZoneName = null; yield break; }

        bool isFirstClear = m_pendingRewardIsFirstClear;
        var rewards = new List<int>
        {
            pendingStage.mineralClearReward,
            isFirstClear ? pendingStage.techPointClearReward   : 0,
            isFirstClear ? pendingStage.modulePointClearReward : 0,
            0
        };

        var loc = LocalizationManager.Instance;
        bool isVip = IAPManager.Instance != null && IAPManager.Instance.IsVipActive();
        ConfirmPopupConfig popupConfig;
        if (isVip == true)
        {
            popupConfig = new ConfirmPopupConfig
            {
                title                = title,
                rewardAmounts        = rewards,
                mineralVipMultiplier = 4,
                confirmText1         = loc.Get("Simple_VipReward"),
                onConfirm            = OnClaimRewardVip,
            };
        }
        else
        {
            popupConfig = new ConfirmPopupConfig
            {
                title         = title,
                rewardAmounts = rewards,
                cancelText1   = loc.Get("Simple_NoThanks"),
                cancelText2   = loc.Get("Simple_MineralX1"),
                confirmText1  = loc.Get("Simple_WatchAD"),
                confirmText2  = loc.Get("Simple_MineralX", 2) + "\n" + loc.Get("Simple_FleetFullRepair"),
                onCancel      = OnClaimRewardX1,
                onConfirm     = OnWatchAdForDoubleReward,
            };
        }
        UIManager.Instance.ShowConfirmPopup(popupConfig);
    }

    private void OnClaimRewardVip()
    {
        // VIP는 광고 없이 자동으로 watchedAd=true 처리 (서버에서 *4 보상 + 전체 수리)
        if (m_playerFleet != null) m_playerFleet.FullRepair();
        SendClaimZoneReward(true);
    }

    private void OnClaimRewardX1()
    {
        if (string.IsNullOrEmpty(m_pendingClaimZoneName)) { return; }
        var request = new ClaimZoneRewardRequest { zoneName = m_pendingClaimZoneName, watchedAd = false };
        NetworkManager.Instance.ClaimZoneReward(request, OnClaimZoneRewardResponse);
    }

    private void OnWatchAdForDoubleReward()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (AdManager.s_devSkipAd == true)
        {
            if (m_playerFleet != null) m_playerFleet.FullRepair();
            SendClaimZoneReward(true);
            return;
        }
#endif
        bool adReady = AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady;
        if (adReady == false)
        {
            if (AdManager.Instance != null)
                AdManager.Instance.LogAdReadyStatus("[DoubleReward]");
            else
                Debug.LogWarning("[DoubleReward] AdManager.Instance == null → 일반 보상으로 대체");
            SendClaimZoneReward(false);
            return;
        }

        AdManager.Instance.ShowRewardedAd(result =>
        {
            bool rewarded = result == EAdResult.Rewarded;
            if (rewarded == true && m_playerFleet != null)
                m_playerFleet.FullRepair();
            SendClaimZoneReward(rewarded);
        });
    }

    private void SendClaimZoneReward(bool watchedAd)
    {
        if (string.IsNullOrEmpty(m_pendingClaimZoneName)) { return; }
        var request = new ClaimZoneRewardRequest { zoneName = m_pendingClaimZoneName, watchedAd = watchedAd };
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
            int prevLevel = character.GetTechLevel();
            character.UpdateMineral(response.data.mineralRemain);
            character.UpdateTechPoint(response.data.techPointRemain);
            character.UpdateModulePointMaxGot(response.data.modulePointMaxGot); // 이벤트 발생 전에 먼저 갱신
            character.UpdateModulePoint(response.data.modulePointRemain);
            int newLevel = response.data.techLevel;
            character.UpdateTechLevel(newLevel);
            if (newLevel > prevLevel)
                UIManager.Instance.ShowTechLevelupNotify(newLevel);
        }
        m_battleZoneStage = null;
        m_pendingClaimZoneName = null;
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

    private void OnRetreatZoneStage()
    {
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title     = LocalizationManager.Instance.Get("UITabExploration_RetreatTitle"),
            message   = LocalizationManager.Instance.Get("UITabExploration_RetreatConfirm"),
            onConfirm = () => ObjectManager.Instance.ForceEndBattle(false),
            onCancel  = () => { }
        });
    }

    private IEnumerator ShowWipePopupAfterDelay(Vector3 retreatPosition, float retreatRotationY)
    {
        yield return m_wipePopupDelay;
        string title = LocalizationManager.Instance.Get("exploration_fleet_wiped");
        string message = LocalizationManager.Instance.Get("exploration_wipe_retreat");
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title        = title,
            message      = message,
            onConfirm    = () => ExecuteRetreat(retreatPosition, retreatRotationY),
            autoCloseSec = 5f,
        });
    }

    private void ExecuteRetreat(Vector3 retreatPosition, float retreatRotationY)
    {
        // 카메라를 먼저 후퇴 위치로 이동
        CameraController.Instance.ExitGalaxyView(retreatPosition);
        // CloseAllTabs 전에 함대 상태를 battle -> warp 로 변경
        SetFleetState(EUnitState.Warp);
        // CloseAllTabs → OnTabDeactivated → ReturnFleetView 호출 시 battleZoneStage 기반 워프인 구독을 막기 위해 먼저 null 처리
        m_battleZoneStage = null;
        if (m_tabSystemParent != null) m_tabSystemParent.CloseAllTabs();
        HideTabButtons();

        int retreatGroup = m_currentZoneStage != null ? ParseZoneGroup(m_currentZoneStage.zoneName) : 1;
        if (retreatGroup <= 0) retreatGroup = 1;

        ObjectManager.Instance.ChangeZone(retreatGroup);
        ObjectManager.Instance.SetMyFleetPosition(retreatPosition, retreatRotationY);

        m_playerFleet.StartFleetWarpIn(onArrived: () =>
        {
            RefreshTabButtons();
            CameraController.Instance.SetTargetOfCameraController(m_playerFleet.transform);

            RefreshCurrentZoneStageButton();

            SetFleetState(EUnitState.Idle);
            
            UpdateGroupTabVisual(retreatGroup);
            SetupButtonsForGroup(retreatGroup);

            if (m_isFleetWiped == true)
            {
                m_playerFleet.RebuildFleet(0.1f);
                m_isFleetWiped = false;
            }
            else
            {
                m_playerFleet.RestoreDestroyedShips(0.1f);
            }
        });
    }

    private void StayInCurrentStage()
    {
        EventManager.Unsubscribe_EnemyFleetKilled(OnEnemyFleetKilled);
        ObjectManager.Instance.StopEnemySpawning();
        ObjectManager.Instance.OrderAllAircraftReturn();
        ObjectManager.Instance.RemoveAllEnemyFleets();

        SetFleetState(EUnitState.Idle);

        if (m_playerFleet != null)
        {
            if (m_isFleetWiped == true)
            {
                m_playerFleet.RebuildFleet(0.1f);
                m_isFleetWiped = false;
            }
            else
            {
                m_playerFleet.RestoreDestroyedShips(0.1f);
            }
        }

        // 갤럭시뷰 중에 전투가 완료된 경우 함대를 즉시 오프스크린으로 이동
        if (CameraController.Instance != null && CameraController.Instance.IsGalaxyView == true)
        {
            if (m_playerFleet != null)
                m_playerFleet.transform.position = new Vector3(0f, -9999f, 0f);
        }

        int battleGroup = m_battleZoneStage != null ? ParseZoneGroup(m_battleZoneStage.zoneName) : (m_currentZoneStage != null ? ParseZoneGroup(m_currentZoneStage.zoneName) : 1);
        if (battleGroup <= 0) battleGroup = 1;
        UpdateGroupTabVisual(battleGroup);
        SetupButtonsForGroup(battleGroup);
    }

}

