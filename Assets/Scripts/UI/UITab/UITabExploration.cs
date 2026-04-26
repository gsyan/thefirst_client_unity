// 탐사 탭 — 그룹 탭(Z1~Z9) + 존 스테이지 버튼(3D 월드 좌표 → Screen Space), 존 진입/재진입/킬 보상 처리
// waveIndex mismatch(백그라운드 복귀 후 Redis TTL 만료) 시 waveIndex=0 재시도, 그 외 에러 시 안전지역 복귀
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum EEnterZoneState
{
    idle,   // Zone0 또는 현재 스테이지에서 대기
    warp,   // 워프 이동 중
    battle, // 전투 진행 중
}

public class UITabExploration : UITabBase
{
    [SerializeField] private Button m_safeZoneButton;
    [SerializeField] private DataTableZone m_datatableZone;

    [Header("존 스테이지 버튼 (World Space)")]
    [SerializeField] private RectTransform m_zoneButtonRoot;       // Screen Space 오버레이 루트 (stretch 전체)
    [SerializeField] private UIZoneStageButton m_zoneStageButtonPrefab;
    
    [Header("그룹 탭")]
    [SerializeField] private Button[] m_groupTabButtons;           // Z1~Z9 그룹 탭 버튼

    private static readonly Color k_tabActiveColor   = new Color(1f, 0.8f, 0.2f, 1f);
    private static readonly Color k_tabInactiveColor = Color.white;


    private SpaceFleet m_myFleet;
    private Character m_myCharacter;
    private ZoneStageConfig m_currentZoneStage;
    private ZoneStageConfig m_selectedZoneStage;
    private UIZoneStageButton m_currentZoneStageButton;
    private readonly Dictionary<string, UIZoneStageButton> m_zoneStageButtons = new Dictionary<string, UIZoneStageButton>();
    private readonly Dictionary<int, ZoneStageConfig> m_selectedZoneStagePerGroup = new();

    private int m_selectedZoneIndex = 1;
    private EEnterZoneState m_enterZoneState;
    private bool m_isFleetWiped;

    public override void InitializeUITab()
    {
        InitializeUITabExploration();
    }

    private void InitializeUITabExploration()
    {
        m_myCharacter = DataManager.Instance.m_currentCharacter;
        if (m_myCharacter == null || m_myCharacter.GetOwnedFleet() == null) return;
        m_myFleet = m_myCharacter.GetOwnedFleet();

        m_safeZoneButton.onClick.AddListener(RetreatToPreviousStage);

        EventManager.Subscribe_MyFleetDestroyed(OnMyFleetWiped);

        SetupGroupTabs();
        InitializeZoneStageButtons();
        SetEnterZoneState(EEnterZoneState.idle);

        var pp = WarpPostProcessing.Instance;
        if (pp != null)
        {
            var zone0 = m_datatableZone.GetZone(0);
            if (zone0 != null)
                pp.SetSkyboxImmediate(zone0.skyboxMaterial);
        }

        SetInitialFleetPosition();
    }

    private void SetInitialFleetPosition()
    {
        if (ObjectManager.Instance == null || m_myFleet == null) return;

        var clearedZones = m_myCharacter.m_characterInfo != null ? m_myCharacter.m_characterInfo.clearedZones : null;
        ZoneStageConfig targetStage;

        if (clearedZones == null || clearedZones.Count == 0)
        {
            targetStage = m_datatableZone.GetZoneStage(0);
        }
        else
        {
            targetStage = m_datatableZone.GetZoneStageByName(clearedZones[^1]);
            if (targetStage == null)
                targetStage = m_datatableZone.GetZoneStage(0);
        }

        if (targetStage != null)
        {
            ObjectManager.Instance.SetMyFleetPosition(targetStage.fleetPosition, targetStage.fleetRotationY);
            CameraController.Instance.SnapToTarget();
        }
    }

    private void SetupGroupTabs()
    {
        if (m_groupTabButtons == null) return;
        for (int i = 0; i < m_groupTabButtons.Length; i++)
        {
            if (m_groupTabButtons[i] == null) continue;
            int groupIndex = i + 1;
            var label = m_groupTabButtons[i].GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = $"Z{groupIndex}";
            m_groupTabButtons[i].onClick.AddListener(() => OnGroupTabClicked(groupIndex));
        }
    }

    private void OnGroupTabClicked(int groupIndex)
    {
        if (m_selectedZoneStagePerGroup.TryGetValue(m_selectedZoneIndex, out ZoneStageConfig prevZoneStage) &&
            m_zoneStageButtons.TryGetValue(prevZoneStage.zoneName, out UIZoneStageButton prevBtn))
            prevBtn.SetSelected(false);

        m_selectedZoneIndex = groupIndex;
        ShowGroupStageButtons(groupIndex);
        UpdateGroupTabVisual();

        var zoneConfig = m_datatableZone.GetZone(groupIndex);
        if (zoneConfig != null && CameraController.Instance != null)
            CameraController.Instance.FocusOnZoneAnchor(
                zoneConfig.galaxyCameraTarget,
                zoneConfig.galaxyCameraZoom,
                zoneConfig.galaxyCameraRotX,
                zoneConfig.galaxyCameraRotY);
    }

    private void UpdateGroupTabVisual()
    {
        if (m_groupTabButtons == null) return;
        for (int i = 0; i < m_groupTabButtons.Length; i++)
        {
            if (m_groupTabButtons[i] == null) continue;
            bool selected = (i + 1) == m_selectedZoneIndex;
            var colors = m_groupTabButtons[i].colors;
            colors.normalColor   = selected ? k_tabActiveColor : k_tabInactiveColor;
            colors.selectedColor = colors.normalColor;
            m_groupTabButtons[i].colors = colors;
        }
    }

    // 초기 시작 시 Zone 1의 첫 스테이지 선택 그룹 결정 후 전체 버튼 생성
    private void InitializeZoneStageButtons()
    {
        if (m_zoneButtonRoot == null || m_zoneStageButtonPrefab == null || m_datatableZone == null) return;

        var clearedZoneNames = m_myCharacter != null ? m_myCharacter.m_characterInfo.clearedZones : null;
        int myShipCount = m_myFleet != null && m_myFleet.m_ships != null ? m_myFleet.m_ships.Count : 0;

        if (clearedZoneNames != null && clearedZoneNames.Count > 0)
        {
            int group = ParseZoneGroup(clearedZoneNames[^1]);
            if (group > 0) m_selectedZoneIndex = group;
        }

        Camera worldCam = CameraController.Instance != null ? CameraController.Instance.m_targetCamera : Camera.main;

        // Zone-0 제외, index 1부터 모두 생성 후 비활성화
        for (int i = 1; i < m_datatableZone.ZoneStageCount; i++)
        {
            ZoneStageConfig zoneStage = m_datatableZone.GetZoneStage(i);
            if (zoneStage == null) continue;

            UIZoneStageButton btn = Instantiate(m_zoneStageButtonPrefab, m_zoneButtonRoot);
            btn.name = zoneStage.zoneName;

            EZoneState state;
            bool isCleared = clearedZoneNames != null && clearedZoneNames.Contains(zoneStage.zoneName);
            if (isCleared == true)
                state = EZoneState.Cleared;
            else
                state = EZoneState.NotCleared;

            ZoneStageConfig captured = zoneStage;
            btn.Initialize(captured, () => OnZoneStageButtonClicked(captured), () => OnEnterZoneFromButton(captured), state, worldCam);
            btn.gameObject.SetActive(false);
            m_zoneStageButtons[zoneStage.zoneName] = btn;
        }

        UpdateGroupTabVisual();
        ShowGroupStageButtons(m_selectedZoneIndex);
    }

    // 선택 그룹의 버튼만 활성화
    private void ShowGroupStageButtons(int zoneIndex)
    {
        foreach (var kv in m_zoneStageButtons)
        {
            bool visible = ParseZoneGroup(kv.Key) == zoneIndex;
            kv.Value.gameObject.SetActive(visible);
        }

        if (m_currentZoneStage != null && ParseZoneGroup(m_currentZoneStage.zoneName) == zoneIndex)
        {
            m_selectedZoneStagePerGroup[zoneIndex] = m_currentZoneStage;
            ApplyZoneStageSelection(m_currentZoneStage);
            return;
        }

        if (m_currentZoneStageButton != null)
        {
            bool inCurrentGroup = ParseZoneGroup(m_currentZoneStageButton.ZoneStageConfig.zoneName) == zoneIndex;
            m_currentZoneStageButton.SetSelected(inCurrentGroup);
        }

        ZoneStageConfig toSelect = m_selectedZoneStagePerGroup.TryGetValue(zoneIndex, out ZoneStageConfig saved)
            ? saved
            : GetDefaultZoneStageForZone(zoneIndex);

        if (toSelect != null)
            ApplyZoneStageSelection(toSelect);
    }

    public override void OnTabActivated()
    {
        if (m_datatableZone == null) return;

        var zone1 = m_datatableZone.GetZone(1);
        if (zone1 != null && CameraController.Instance != null)
            CameraController.Instance.EnterGalaxyView(
                zone1.galaxyCameraTarget,
                zone1.galaxyCameraZoom,
                zone1.galaxyCameraRotX,
                zone1.galaxyCameraRotY);

        SetOtherTabsVisible(false, includeSelf: true);
    }

    public override void OnTabDeactivated()
    {
        if (CameraController.Instance != null)
            CameraController.Instance.ExitGalaxyView();

        SetOtherTabsVisible(true, includeSelf: true);
        EventManager.TriggerEnterZoneStateChanged(m_enterZoneState);
    }

    private void SetEnterZoneState(EEnterZoneState enterZoneState)
    {
        m_enterZoneState = enterZoneState;
        bool inZone = enterZoneState == EEnterZoneState.battle;
        if (m_safeZoneButton != null) m_safeZoneButton.gameObject.SetActive(inZone);
        EventManager.TriggerEnterZoneStateChanged(enterZoneState);
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
        if (m_selectedZoneStage != null && m_zoneStageButtons.TryGetValue(m_selectedZoneStage.zoneName, out UIZoneStageButton prevBtn))
            prevBtn.SetSelected(false);

        m_selectedZoneStagePerGroup[m_selectedZoneIndex] = zoneStage;
        ApplyZoneStageSelection(zoneStage);
    }

    private void ApplyZoneStageSelection(ZoneStageConfig zoneStage)
    {
        if (m_selectedZoneStage != null &&
            m_zoneStageButtons.TryGetValue(m_selectedZoneStage.zoneName, out UIZoneStageButton prev))
            prev.SetSelected(false);

        m_selectedZoneStage = zoneStage;
        if (m_zoneStageButtons.TryGetValue(zoneStage.zoneName, out UIZoneStageButton btn))
            btn.SetSelected(true);
    }

    private void OnEnterZoneFromButton(ZoneStageConfig zoneStage)
    {
        if (zoneStage == null) return;
        if (m_enterZoneState == EEnterZoneState.warp) return;
        if (m_enterZoneState == EEnterZoneState.battle)
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
            null, null, 0,
            onConfirm: () => ExecuteEnterZone(zoneStage)
        );
    }

    private ZoneStageConfig GetDefaultZoneStageForZone(int zoneIndex)
    {
        var clearedZoneStages = m_myCharacter?.m_characterInfo.clearedZones;
        ZoneStageConfig highest = null;
        ZoneStageConfig lowestUncleared = null;

        for (int i = 1; i < m_datatableZone.ZoneStageCount; i++)
        {
            ZoneStageConfig zoneStage = m_datatableZone.GetZoneStage(i);
            if (zoneStage == null || ParseZoneGroup(zoneStage.zoneName) != zoneIndex) continue;

            int stage = ParseZoneStage(zoneStage.zoneName);
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

    private void OnZoneTryButtonClicked()
    {
        if (m_selectedZoneStage == null) return;
        if (m_enterZoneState == EEnterZoneState.warp) return;
        if (m_enterZoneState == EEnterZoneState.battle)
        {
            if(m_currentZoneStage != null && m_currentZoneStage.zoneName == m_selectedZoneStage.zoneName) return;
            EventManager.Unsubscribe_EnemyFleetKilled(OnEnemyFleetKilled);
            ObjectManager.Instance.StopEnemySpawning();
            ObjectManager.Instance.OrderAllAircraftReturn();
            ObjectManager.Instance.CleanupAllProjectiles();
            ObjectManager.Instance.RemoveAllEnemyFleets();
        }

        ZoneStageConfig zoneStage = m_selectedZoneStage;
        UIManager.Instance.ShowConfirmPopup(
            zoneStage.zoneName,
            LocalizationManager.Instance.Get("exploration_zone_enter_confirm"),
            null, null, 0,
            onConfirm: () => ExecuteEnterZone(zoneStage)
        );
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
        SetEnterZoneState(EEnterZoneState.warp);

        m_currentZoneStage = zoneStage;
        RefreshCurrentZoneStageButton();
        EventManager.Subscribe_EnemyFleetKilled(OnEnemyFleetKilled);

        // 카메라를 먼저 목표 위치로 이동 (갤럭시뷰 종료 포함)
        CameraController.Instance.ExitGalaxyViewMoveTo(zoneStage.fleetPosition);

        if (m_tabSystemParent != null) m_tabSystemParent.CloseAllTabs();
        SetOtherTabsVisible(false, includeSelf: true);

        // 최종 위치·방향을 설정 → StartFleetWarpIn이 transform.forward 기준으로 뒤에서 접근
        ObjectManager.Instance.SetMyFleetPosition(zoneStage.fleetPosition, zoneStage.fleetRotationY);

        var cam = CameraController.Instance;
        m_myFleet.StartFleetWarpIn(onArrived: () =>
        {
            SetOtherTabsVisible(true, includeSelf: true);
            cam.SetTargetOfCameraController(m_myFleet.transform);
            SetEnterZoneState(EEnterZoneState.battle);
            UIManager.Instance.ShowPanel("UIPanelCameraView");
            bool isFirstClear = IsAlreadyCleared(zoneStage) == false;
            EventManager.TriggerZoneEntered(zoneStage.zoneName, isFirstClear);
            StartBattleInZone(zoneStage);
        });
    }

    private void RefreshCurrentZoneStageButton()
    {
        if (m_currentZoneStageButton != null)
        {
            m_currentZoneStageButton.SetSelected(false);
            // if (IsAlreadyCleared(m_currentZoneStageButton.ZoneStageConfig) == false)
            //     m_currentZoneStageButton.SetState(EZoneState.Current);
        }

        m_currentZoneStageButton = null;
        if (m_currentZoneStage == null) return;

        if (m_zoneStageButtons.TryGetValue(m_currentZoneStage.zoneName, out UIZoneStageButton btn))
            m_currentZoneStageButton = btn;

        if (m_currentZoneStageButton != null)
            m_currentZoneStageButton.SetSelected(true);
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

        if (character != null && response.data.rewardInfo != null)
            character.UpdateMineral(response.data.rewardInfo.mineralRemain);

        if (response.data.isZoneCleared == true && character != null)
        {
            character.m_characterInfo.collectDateTime = response.data.collectDateTime;

            if (character.m_characterInfo.clearedZones == null)
                character.m_characterInfo.clearedZones = new List<string>();

            string newlyCleared = response.data.clearedZoneName;
            if (character.m_characterInfo.clearedZones.Contains(newlyCleared) == false)
                character.m_characterInfo.clearedZones.Add(newlyCleared);

            if (m_zoneStageButtons.TryGetValue(newlyCleared, out UIZoneStageButton clearedBtn))
                clearedBtn.SetState(EZoneState.Cleared);

            RefreshCurrentZoneStageButton();
            SelectNextZoneStage(newlyCleared);
        }

        int mineralGained = 0;
        if (character != null && character.m_characterInfo != null)
            mineralGained = character.m_characterInfo.mineral - mineralBefore;

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
                if (zs != null && ParseZoneGroup(zs.zoneName) == nextGroup)
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
            retreatPosition = retreatStage.fleetPosition;
            retreatRotationY = retreatStage.fleetRotationY;
        }
        else
        {
            var zone0Stage = m_datatableZone.GetZoneStage(0);
            retreatPosition = zone0Stage?.fleetPosition ?? Vector3.zero;
            retreatRotationY = zone0Stage?.fleetRotationY ?? 0f;
        }

        UIManager.Instance.HidePanel("UIPanelCameraView");

        // 카메라를 먼저 후퇴 위치로 이동 (갤럭시뷰 종료 포함 — 이후 CloseAllTabs→ExitGalaxyView 중복 호출 방지)
        CameraController.Instance.ExitGalaxyViewMoveTo(retreatPosition);

        if (m_tabSystemParent != null) m_tabSystemParent.CloseAllTabs();
        SetOtherTabsVisible(false, includeSelf: true);

        ObjectManager.Instance.SetMyFleetPosition(retreatPosition, retreatRotationY);

        m_myFleet.StartFleetWarpIn(onArrived: () =>
        {
            SetOtherTabsVisible(true, includeSelf: true);
            CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);

            m_currentZoneStage = retreatStage;
            RefreshCurrentZoneStageButton();

            SetEnterZoneState(EEnterZoneState.idle);
            m_myFleet.SetFleetState(EFleetState.None);
            UpdateGroupTabVisual();
            ShowGroupStageButtons(m_selectedZoneIndex);

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

        UIManager.Instance.HidePanel("UIPanelCameraView");

        SetEnterZoneState(EEnterZoneState.idle);
        m_myFleet.SetFleetState(EFleetState.None);
        UpdateGroupTabVisual();
        ShowGroupStageButtons(m_selectedZoneIndex);
    }

    private ZoneStageConfig FindPreviousClearedStage()
    {
        if (m_currentZoneStage == null) return null;

        int group = ParseZoneGroup(m_currentZoneStage.zoneName);
        int stage = ParseZoneStage(m_currentZoneStage.zoneName);
        var cleared = m_myCharacter?.m_characterInfo.clearedZones;
        if (cleared == null || cleared.Count == 0) return null;

        if (stage > 1)
        {
            string prevName = $"{group}-{stage - 1}";
            if (cleared.Contains(prevName))
                return m_datatableZone.GetZoneStageByName(prevName);
        }

        if (group > 1)
        {
            for (int s = 5; s >= 1; s--)
            {
                string name = $"{group - 1}-{s}";
                if (cleared.Contains(name))
                    return m_datatableZone.GetZoneStageByName(name);
            }
        }

        return null;
    }
}
