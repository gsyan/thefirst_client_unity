// 탐사 탭 — 그룹 탭(Z1~Z9) + 존 스테이지 버튼(3D 월드 좌표 → Screen Space), 존 진입/재진입/킬 보상 처리
// 패배 시: 현재 존에서 클리어 스테이지 있으면 최고 클리어 위치로, 없으면 해당 존 x-0 스폰 마커 위치로 복귀
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UITabExploration : UITabBase
{
    [SerializeField] private Button m_retreatButton;
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
    private ZoneStageConfig m_currentZoneStage;
    private ZoneStageConfig m_selectedZoneStage;
    private UIZoneStageButton m_currentZoneStageButton;
    private readonly Dictionary<string, UIZoneStageButton> m_zoneStageButtons = new Dictionary<string, UIZoneStageButton>();
    private readonly Dictionary<int, ZoneStageConfig> m_selectedZoneStagePerGroup = new();

    private int m_selectedZoneIndex = 1;

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

        m_retreatButton.onClick.AddListener(RetreatToPreviousStage);

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
        m_selectedZoneIndex = groupIndex;
        SetupButtonsForGroup(groupIndex);
        UpdateGroupTabVisual();

        var zoneConfig = m_datatableZone.GetZoneByZoneIndex(groupIndex);
        if (zoneConfig != null && CameraController.Instance != null)
            CameraController.Instance.FocusOnZoneAnchor(
                zoneConfig.galaxyCameraTarget,
                zoneConfig.galaxyCameraZoom,
                zoneConfig.galaxyCameraRotX,
                zoneConfig.galaxyCameraRotY);
    }

    private void UpdateGroupTabVisual()
    {
        if (m_zoneTabButtons == null) return;
        for (int i = 0; i < m_zoneTabButtons.Length; i++)
            m_zoneTabButtons[i].SetSelected((i + 1) == m_selectedZoneIndex);
    }

    // 초기 그룹 인덱스만 결정 — 버튼 생성은 OnTabActivated의 SetupButtonsForGroup에서
    private void InitializeZoneStageButtons()
    {
        if (m_datatableZone == null) return;

        var clearedZoneNames = m_myCharacter != null ? m_myCharacter.m_characterInfo.clearedZones : null;
        if (clearedZoneNames != null && clearedZoneNames.Count > 0)
        {
            int group = ParseZoneGroup(clearedZoneNames[^1]);
            if (group > 0) m_selectedZoneIndex = group;
        }
    }

    // 선택 그룹 버튼을 풀에서 꺼내 배치 — 이전 그룹 버튼은 먼저 풀로 반납
    private void SetupButtonsForGroup(int groupIndex)
    {
        ReturnAllButtonsToPool();
        m_currentZoneStageButton = null;

        if (m_zoneButtonRoot == null || m_zoneStageButtonPrefab == null || m_datatableZone == null) return;

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

        if (m_currentZoneStage != null && ParseZoneGroup(m_currentZoneStage.zoneName) == groupIndex)
        {
            if (m_zoneStageButtons.TryGetValue(m_currentZoneStage.zoneName, out UIZoneStageButton curBtn))
                m_currentZoneStageButton = curBtn;
            m_selectedZoneStagePerGroup[groupIndex] = m_currentZoneStage;
            ApplyZoneStageSelection(m_currentZoneStage);
            return;
        }

        ZoneStageConfig toSelect = m_selectedZoneStagePerGroup.TryGetValue(groupIndex, out ZoneStageConfig saved)
            ? saved
            : GetDefaultZoneStageForZone(groupIndex);

        if (toSelect != null)
            ApplyZoneStageSelection(toSelect);
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

        var zone1 = m_datatableZone.GetZoneByZoneIndex(1);
        if (zone1 != null && CameraController.Instance != null)
            CameraController.Instance.EnterGalaxyView(
                zone1.galaxyCameraTarget,
                zone1.galaxyCameraZoom,
                zone1.galaxyCameraRotX,
                zone1.galaxyCameraRotY);

        SetupButtonsForGroup(m_selectedZoneIndex);
        UpdateGroupTabVisual();

        SetOtherTabsVisible(false, includeSelf: true);
        EventManager.TriggerExplorationTabOpened();
    }

    public override void OnTabDeactivated()
    {
        ReturnAllButtonsToPool();

        if (CameraController.Instance != null)
            CameraController.Instance.ExitGalaxyView();

        SetOtherTabsVisible(true, includeSelf: true);
        EventManager.TriggerExplorationTabClosed();
    }

    private void SetFleetState(EUnitState unitState)
    {
        m_myFleet.SetFleetState(unitState);
        if (m_retreatButton != null) m_retreatButton.gameObject.SetActive(unitState == EUnitState.Battle);
    }

    private void OnMyFleetWiped()
    {
        m_isFleetWiped = true;
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_MyFleetDestroyed(OnMyFleetWiped);
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
        m_selectedZoneStagePerGroup[m_selectedZoneIndex] = zoneStage;
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
            if (m_currentZoneStage != null && m_currentZoneStage.zoneName == zoneStage.zoneName) return;
            EventManager.Unsubscribe_EnemyFleetKilled(OnEnemyFleetKilled);
            ObjectManager.Instance.StopEnemySpawning();
            ObjectManager.Instance.OrderAllAircraftReturn();
            ObjectManager.Instance.CleanupAllProjectiles();
            ObjectManager.Instance.RemoveAllEnemyFleets();
        }

        UIManager.Instance.ShowConfirmPopup(
            zoneStage.zoneName,
            LocalizationManager.Instance.Get("exploration_zone_enter_confirm"),
            null, null, null,
            onConfirm: () => ExecuteEnterZone(zoneStage)
        );
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
        SetFleetState(EUnitState.Warp);

        m_currentZoneStage = zoneStage;
        RefreshCurrentZoneStageButton();
        EventManager.Subscribe_EnemyFleetKilled(OnEnemyFleetKilled);

        // 카메라를 먼저 목표 위치로 이동 (갤럭시뷰 종료 포함)
        Vector3 fleetWorldPos = m_datatableZone.ResolveFleetWorldPosition(zoneStage);
        CameraController.Instance.ExitGalaxyViewMoveTo(fleetWorldPos);

        if (m_tabSystemParent != null) m_tabSystemParent.CloseAllTabs();
        SetOtherTabsVisible(false, includeSelf: true);

        // 최종 위치·방향을 설정 → StartFleetWarpIn이 transform.forward 기준으로 뒤에서 접근
        ObjectManager.Instance.SetMyFleetPosition(fleetWorldPos, zoneStage.fleetRotationY);
        ObjectManager.Instance.ChangeZone(zoneStage.zoneIndex);

        var cam = CameraController.Instance;
        m_myFleet.StartFleetWarpIn(onArrived: () =>
        {
            SetOtherTabsVisible(true, includeSelf: true);
            cam.SetTargetOfCameraController(m_myFleet.transform);
            SetFleetState(EUnitState.Battle);
            bool isFirstClear = IsAlreadyCleared(zoneStage) == false;
            EventManager.TriggerZoneEntered(zoneStage.zoneName, isFirstClear);
            StartBattleInZone(zoneStage);
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

    private void OnEnemyFleetKilled()
    {
        Debug.Log("OnEnemyFleetKilled");
        if (m_currentZoneStage == null) return;

        var request = new ClearZoneStageRequest
        {
            zoneName = m_currentZoneStage.zoneName,
        };
        NetworkManager.Instance.ClearZoneStage(request, OnClearZoneStageResponse);
    }

    private void OnClearZoneStageResponse(ApiResponse<ClearZoneStageResponse> response)
    {
        if (response.errorCode != 0)
        {
            Debug.LogWarning($"[Zone] ClearZoneStage 에러: {ErrorCodeMapping.GetMessage(response.errorCode)} ({response.errorCode})");
            StayInCurrentStage();
            return;
        }

        var character = DataManager.Instance.m_currentCharacter;
        int mineralBefore = (character != null && character.m_characterInfo != null) ? character.m_characterInfo.mineral : 0;

        if (character != null && character.m_characterInfo != null)
        {
            character.m_characterInfo.mineral = response.data.mineralRemain;
            character.m_characterInfo.techPoint = response.data.techPointRemain;
            character.m_characterInfo.modulePoint = response.data.modulePointRemain;
            EventManager.TriggerMineralChange(response.data.mineralRemain);
        }

        if (response.data.isZoneCleared == true && character != null)
        {
            if (character.m_characterInfo.clearedZones == null)
                character.m_characterInfo.clearedZones = new List<string>();

            string newlyCleared = response.data.clearedZoneName;
            if (character.m_characterInfo.clearedZones.Contains(newlyCleared) == false)
                character.m_characterInfo.clearedZones.Add(newlyCleared);

            if (m_zoneStageButtons.TryGetValue(newlyCleared, out UIZoneStageButton clearedBtn))
                clearedBtn.SetStateUIZoneStageButton(EZoneState.Cleared);

            RefreshCurrentZoneStageButton();
            SelectNextZoneStage(newlyCleared);
        }

        int mineralGained = response.data.mineralRemain - mineralBefore;

        if (mineralGained > 0)
        {
            string title = LocalizationManager.Instance.Get("exploration_battle_victory");
            string rewardText = LocalizationManager.Instance.Get("exploration_battle_mineral_reward", mineralGained);
            string msg = $"{CommonUtility.Sprite("crystal-growth")} {rewardText}";
            UIManager.Instance.StartCoroutine(ShowRewardPopupDelayed(title, msg, 2f));
        }
        else
        {
            StayInCurrentStage();
        }
    }

    private IEnumerator ShowRewardPopupDelayed(string title, string msg, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        UIManager.Instance.ShowPopupAlert(title, msg, StayInCurrentStage, autoCloseSec: 5f);
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
            if (nextStage != null)
                m_selectedZoneIndex = nextGroup;
        }

        if (nextStage == null) return;
        m_selectedZoneStagePerGroup[ParseZoneGroup(nextStage.zoneName)] = nextStage;
    }

    private void RetreatToPreviousStage()
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
            int currentGroup = m_currentZoneStage != null ? ParseZoneGroup(m_currentZoneStage.zoneName) : 1;
            ZoneStageConfig spawnStage = m_datatableZone.GetZoneSpawnStage(currentGroup);
            retreatPosition  = spawnStage != null ? m_datatableZone.ResolveFleetWorldPosition(spawnStage) : Vector3.zero;
            retreatRotationY = spawnStage != null ? spawnStage.fleetRotationY : 0f;
        }

        if (m_isFleetWiped == true)
            ObjectManager.Instance.StartCoroutine(ShowWipePopupAfterDelay(retreatPosition, retreatRotationY, retreatStage));
        else
            ExecuteRetreat(retreatPosition, retreatRotationY, retreatStage);
    }

    private IEnumerator ShowWipePopupAfterDelay(Vector3 retreatPosition, float retreatRotationY, ZoneStageConfig retreatStage)
    {
        yield return m_wipePopupDelay;
        string title = LocalizationManager.Instance.Get("exploration_fleet_wiped");
        string message = LocalizationManager.Instance.Get("exploration_wipe_retreat");
        UIManager.Instance.ShowPopupAlert(title, message,
            () => ExecuteRetreat(retreatPosition, retreatRotationY, retreatStage),
            autoCloseSec: 5f);
    }

    private void ExecuteRetreat(Vector3 retreatPosition, float retreatRotationY, ZoneStageConfig retreatStage)
    {
        // 카메라를 먼저 후퇴 위치로 이동 (갤럭시뷰 종료 포함 — 이후 CloseAllTabs→ExitGalaxyView 중복 호출 방지)
        CameraController.Instance.ExitGalaxyViewMoveTo(retreatPosition);
        // CloseAllTabs 전에 함대 상태를 battle -> warp 로 변경
        SetFleetState(EUnitState.Warp);
        if (m_tabSystemParent != null) m_tabSystemParent.CloseAllTabs();
        SetOtherTabsVisible(false, includeSelf: true);

        ObjectManager.Instance.SetMyFleetPosition(retreatPosition, retreatRotationY);

        m_myFleet.StartFleetWarpIn(onArrived: () =>
        {
            SetOtherTabsVisible(true, includeSelf: true);
            CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);

            m_currentZoneStage = retreatStage;
            RefreshCurrentZoneStageButton();

            SetFleetState(EUnitState.Idle);
            UpdateGroupTabVisual();
            SetupButtonsForGroup(m_selectedZoneIndex);

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
        UpdateGroupTabVisual();
        SetupButtonsForGroup(m_selectedZoneIndex);
    }

    // 현재 존에서 클리어한 스테이지 중 가장 높은 것 반환 — 없으면 null(→ 해당 존 최초지점으로 복귀)
    private ZoneStageConfig FindPreviousClearedStage()
    {
        if (m_currentZoneStage == null) return null;

        int group = ParseZoneGroup(m_currentZoneStage.zoneName);
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
