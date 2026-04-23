// 탐사 탭 — 그룹 탭(Z1~Z9) + 존 맵 셀(안개 reveal), 존 진입/재진입/킬 보상 처리
// waveIndex mismatch(백그라운드 복귀 후 Redis TTL 만료) 시 waveIndex=0 재시도, 그 외 에러 시 안전지역 복귀
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum EEnterZoneState
{
    safe,
    warp,
    zone,
}

public class UITabExploration : UITabBase
{
    [SerializeField] private Image m_harvestGaugeFill; // 게이지 바 Image (anchorMax.x 0~1)
    [SerializeField] private TMP_Text m_harvestGaugeText; // "XX%" 텍스트 (게이지 위 오버레이)
    [SerializeField] private TMP_Text m_harvestLimitText;  // 시간당 수확량 합계 텍스트

    [SerializeField] private Button m_safeZoneButton;
    [SerializeField] private DataTableZone m_datatableZone;

    [Header("존 맵")]
    [SerializeField] private RectTransform m_zoneCellContainer;  // ZoneMapCell 부모 RectTransform
    [SerializeField] private TMP_Text m_zoneDetailText;
    [SerializeField] private Button m_zoneTryButton;
    [SerializeField] private GameObject m_zoneMapCellPrefab;     // ZoneMapCell 프리팹
    [SerializeField] private Button[] m_groupTabButtons;         // Z1~Z9 그룹 탭 버튼

    private static readonly Color k_tabActiveColor   = new Color(1f, 0.8f, 0.2f, 1f);
    private static readonly Color k_tabInactiveColor = Color.white;

    private SpaceFleet m_myFleet;
    private Character m_myCharacter;
    private bool m_hasClearedZone;
    private ZoneStageConfig m_currentZoneStage;
    private ZoneStageConfig m_selectedZoneStage;      // 현재 그룹에서 선택된 존
    private ZoneMapCell m_currentZoneCell;
    private readonly Dictionary<string, ZoneMapCell> m_zoneStageCells = new Dictionary<string, ZoneMapCell>();
    private readonly Dictionary<int, ZoneStageConfig> m_selectedZoneStagePerGroup = new(); // 그룹별 선택 존 기억

    private int m_selectedZoneIndex = 1;
    private EEnterZoneState m_enterZoneState;
    private bool m_isFleetWiped;
    private readonly WaitForSeconds m_updateInterval = new WaitForSeconds(1f);

    public override void InitializeUITab()
    {
        InitializeUITabExploration();
    }

    private void InitializeUITabExploration()
    {
        m_myCharacter = DataManager.Instance.m_currentCharacter;
        if (m_myCharacter == null || m_myCharacter.GetOwnedFleet() == null) return;
        m_myFleet = m_myCharacter.GetOwnedFleet();

        
        m_safeZoneButton.onClick.AddListener(ReturnToSafeZone);
        if (m_zoneTryButton != null) m_zoneTryButton.onClick.AddListener(OnZoneTryButtonClicked);

        EventManager.Subscribe_MyFleetDestroyed(OnMyFleetWiped);

        SetupGroupTabs();
        InitializeZoneMap();
        //UpdateZoneInfo();
        SetEnterZoneState(EEnterZoneState.safe);

        var pp = WarpPostProcessing.Instance;
        if (pp != null)
        {
            // 게임 시작 시 항상 Zone-0 스카이박스로 즉시 초기화
            var zone0 = m_datatableZone.GetZone(0);
            if (zone0 != null)
                pp.SetSkyboxImmediate(zone0.skyboxMaterial);
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
        // 이전 그룹 선택 셀 outline 해제
        if (m_selectedZoneStagePerGroup.TryGetValue(m_selectedZoneIndex, out ZoneStageConfig prevZoneStage) &&
            m_zoneStageCells.TryGetValue(prevZoneStage.zoneName, out ZoneMapCell prevCell))
            prevCell.SetSelected(false);

        m_selectedZoneIndex = groupIndex;
        ShowGroupMap(groupIndex);
        UpdateGroupTabVisual();
    }

    private void UpdateGroupTabVisual()
    {
        if (m_groupTabButtons == null) return;
        for (int i = 0; i < m_groupTabButtons.Length; i++)
        {
            if (m_groupTabButtons[i] == null) continue;
            bool selected = (i + 1) == m_selectedZoneIndex;
            var colors = m_groupTabButtons[i].colors;
            colors.normalColor  = selected ? k_tabActiveColor : k_tabInactiveColor;
            colors.selectedColor = colors.normalColor;
            m_groupTabButtons[i].colors = colors;
        }
    }

    // 모든 존의 ZoneMapCell 생성 (Zone-0 안전지역 제외, index 1부터)
    private void InitializeZoneMap()
    {
        if (m_zoneCellContainer == null || m_zoneMapCellPrefab == null || m_datatableZone == null) return;

        Canvas.ForceUpdateCanvases();

        var grid = m_zoneCellContainer.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            int columns = grid.constraintCount > 0 ? grid.constraintCount : 5;

            // 그룹 1 기준 존 수로 행 수 계산 (그룹마다 동일 개수 전제)
            int stagesInZone = 0;
            for (int i = 1; i < m_datatableZone.ZoneStageCount; i++)
            {
                ZoneStageConfig zs = m_datatableZone.GetZoneStage(i);
                if (zs != null && ParseZoneGroup(zs.zoneName) == 1) stagesInZone++;
            }
            int rows = stagesInZone > 0 ? Mathf.CeilToInt((float)stagesInZone / columns) : 5;

            float availW = m_zoneCellContainer.rect.width  - grid.padding.left  - grid.padding.right  - grid.spacing.x * (columns - 1);
            float availH = m_zoneCellContainer.rect.height - grid.padding.top   - grid.padding.bottom - grid.spacing.y * (rows    - 1);
            grid.cellSize = new Vector2(Mathf.Max(1f, availW / columns), Mathf.Max(1f, availH / rows));
        }

        var clearedZoneNames = m_myCharacter != null ? m_myCharacter.m_characterInfo.clearedZones : null;
        int myShipCount = m_myFleet != null && m_myFleet.m_ships != null ? m_myFleet.m_ships.Count : 0;

        // 초기 그룹 탭: 마지막 클리어 존 그룹으로 설정
        if (clearedZoneNames != null && clearedZoneNames.Count > 0)
        {
            int group = ParseZoneGroup(clearedZoneNames[^1]);
            if (group > 0) m_selectedZoneIndex = group;
        }

        for (int i = 1; i < m_datatableZone.ZoneStageCount; i++)
        {
            ZoneStageConfig zoneStage = m_datatableZone.GetZoneStage(i);
            if (zoneStage == null) continue;

            var go = Instantiate(m_zoneMapCellPrefab, m_zoneCellContainer);
            go.name = zoneStage.zoneName;
            var cell = go.GetComponent<ZoneMapCell>();

            EZoneState state;
            bool isCleared = clearedZoneNames != null && clearedZoneNames.Contains(zoneStage.zoneName);
            if (isCleared)
                state = EZoneState.Cleared;
            else if (myShipCount >= ParseZoneGroup(zoneStage.zoneName))
                state = EZoneState.Current;
            else
                state = EZoneState.Locked;

            ZoneStageConfig captured = zoneStage;
            cell.Initialize(captured, () => OnTryZoneStageClicked(captured), state);
            go.SetActive(false); // ShowGroupMap에서 표시
            m_zoneStageCells[zoneStage.zoneName] = cell;
        }

        UpdateGroupTabVisual();
        ShowGroupMap(m_selectedZoneIndex);
    }

    // 선택된 그룹(X)의 셀만 활성화, 이전 선택 존 복원 또는 기본값 선택
    private void ShowGroupMap(int zoneIndex)
    {
        foreach (var kv in m_zoneStageCells)
        {
            bool visible = ParseZoneGroup(kv.Key) == zoneIndex;
            kv.Value.gameObject.SetActive(visible);
        }

        // 탐사 중인 존이 이 그룹에 속하면 → 탐사 중인 존을 선택으로 고정 (저장된 다른 선택 무시)
        if (m_currentZoneStage != null && ParseZoneGroup(m_currentZoneStage.zoneName) == zoneIndex)
        {
            m_selectedZoneStagePerGroup[zoneIndex] = m_currentZoneStage;
            ApplyZoneStageSelection(m_currentZoneStage);
            return;
        }

        if (m_currentZoneCell != null)
        {
            bool inCurrentGroup = ParseZoneGroup(m_currentZoneCell.m_zoneStageConfig.zoneName) == zoneIndex;
            m_currentZoneCell.SetSelected(inCurrentGroup);
        }

        // 저장된 선택 존 복원, 없으면 기본값(미클리어 최고 스테이지 or 전체 클리어 시 최고)
        ZoneStageConfig toSelect = m_selectedZoneStagePerGroup.TryGetValue(zoneIndex, out ZoneStageConfig saved)
            ? saved
            : GetDefaultZoneStageForZone(zoneIndex);

        if (toSelect != null)
            ApplyZoneStageSelection(toSelect);
    }

    public override void OnTabActivated()
    {
        //UpdateZoneInfo();
    }

    public override void OnTabDeactivated()
    {
        
    }

    // private void UpdateZoneInfo()
    // {
    //     if (m_datatableZone == null || m_myCharacter == null) return;
    //     var clearedZoneNames = m_myCharacter.m_characterInfo.clearedZones;
    //     m_hasClearedZone = false;
    //     if (clearedZoneNames != null && clearedZoneNames.Count > 0)
    //     {
    //         var clearedZones = m_datatableZone.GetZoneStagesByNames(clearedZoneNames);
    //         foreach (var z in clearedZones)
    //         {
    //             m_totalMineralPerHour      += z.mineralPerHour;
    //             m_totalMineralRarePerHour  += z.mineralRarePerHour;
    //             m_totalMineralExoticPerHour += z.mineralExoticPerHour;
    //             m_totalMineralDarkPerHour  += z.mineralDarkPerHour;
    //         }
    //         m_hasClearedZone = clearedZones.Count > 0;
    //     }

    //     UpdateHarvestGauge();
    //     UpdateHarvestCapText();
    // }

    private void SetEnterZoneState(EEnterZoneState enterZoneState)
    {
        m_enterZoneState = enterZoneState;
        bool inZone = enterZoneState == EEnterZoneState.zone;
        if (m_safeZoneButton != null) m_safeZoneButton.gameObject.SetActive(inZone);
        if (m_zoneTryButton  != null) m_zoneTryButton.gameObject.SetActive(!inZone);
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

    // "X-Y"에서 그룹 X 파싱
    private int ParseZoneGroup(string zoneName)
    {
        if (string.IsNullOrEmpty(zoneName)) return 0;
        int dashIdx = zoneName.IndexOf('-');
        if (dashIdx <= 0) return 0;
        return int.TryParse(zoneName.Substring(0, dashIdx), out int x) ? x : 0;
    }

    // 셀 클릭 — 선택 및 디테일 표시만 (입장 안함)
    private void OnTryZoneStageClicked(ZoneStageConfig zoneStage)
    {
        // 이전 선택 셀 outline 해제
        if (m_selectedZoneStage != null && m_zoneStageCells.TryGetValue(m_selectedZoneStage.zoneName, out ZoneMapCell prevCell))
            prevCell.SetSelected(false);

        m_selectedZoneStagePerGroup[m_selectedZoneIndex] = zoneStage;
        ApplyZoneStageSelection(zoneStage);
    }

    private void ApplyZoneStageSelection(ZoneStageConfig zoneStage)
    {
        m_selectedZoneStage = zoneStage;
        if (m_zoneStageCells.TryGetValue(zoneStage.zoneName, out ZoneMapCell cell))
            cell.SetSelected(true);
        RefreshZoneStageDetailText(zoneStage);

        // 클리어된 존(isRestored=false)은 입장 버튼 비활성화
        if (m_zoneTryButton != null)
            m_zoneTryButton.interactable = IsAlreadyCleared(zoneStage) == false;
    }

    // 그룹 내 기본 선택: 미클리어 최고 스테이지, 전부 클리어 시 최고 스테이지
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

    // "X-Y"에서 스테이지 Y 파싱
    private int ParseZoneStage(string zoneName)
    {
        if (string.IsNullOrEmpty(zoneName)) return 0;
        int dashIdx = zoneName.IndexOf('-');
        if (dashIdx < 0 || dashIdx >= zoneName.Length - 1) return 0;
        return int.TryParse(zoneName[(dashIdx + 1)..], out int y) ? y : 0;
    }

    // 존 입장 버튼 클릭 — 선택된 존으로 입장 시도
    private void OnZoneTryButtonClicked()
    {
        if (m_selectedZoneStage == null) return;
        if (m_enterZoneState == EEnterZoneState.warp) return;
        if (m_enterZoneState == EEnterZoneState.zone && m_currentZoneStage != null && m_currentZoneStage.zoneName == m_selectedZoneStage.zoneName) return;

        if (m_enterZoneState == EEnterZoneState.zone)
        {
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

    // 선택된 존 정보를 m_zoneDetailText에 표시
    private void RefreshZoneStageDetailText(ZoneStageConfig zoneStage)
    {
        if (m_zoneDetailText == null || zoneStage == null) return;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(zoneStage.zoneName);
        if (string.IsNullOrEmpty(zoneStage.zoneDescription) == false)
            sb.AppendLine(zoneStage.zoneDescription);
        void AppendRate(string icon, float value)
        {
            if (value <= 0f) return;
            sb.AppendLine($"{CommonUtility.Sprite(icon)} {CommonUtility.FormatBigNumber(value)}");
        }
        AppendRate("crystal-growth",  zoneStage.mineralClearReward);
        m_zoneDetailText.text = sb.ToString().TrimEnd();
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
                {
                    EnterZoneStage(zonestage);
                }
                else if (result == EAdResult.Failed)
                {
                    EnterZoneStage(zonestage);
                }
                else if (result == EAdResult.UserClosed)
                {
                    ShowErrorMessage("[광고] 광고를 시청해야 입장할 수 있습니다");
                }
            });
        }
        else
        {
            // 광고 미준비 → 입장 허용 후 즉시 재로드 요청
            if (adInstanceNull == false)
                AdManager.Instance.RequestLoad();
            Debug.Log("[Ad] 광고 미준비 상태로 입장");
            EnterZoneStage(zonestage);
        }
#endif
    }

    private void EnterZoneStage(ZoneStageConfig zoneStage)
    {
        if (m_tabSystemParent != null) m_tabSystemParent.CloseAllTabs();
        SetEnterZoneState(EEnterZoneState.warp);
        var zone = m_datatableZone.GetZone(zoneStage.zoneIndex);
        Material skybox = zone?.skyboxMaterial;
        float rotation = zoneStage.skyboxRotation;

        var pp = WarpPostProcessing.Instance;
        if (pp != null && zoneStage != null)
            pp.SetSkyboxBlendTarget(skybox, rotation);

        m_currentZoneStage = zoneStage;
        CacheCurrentZoneCell();
        EventManager.Subscribe_EnemyFleetKilled(OnEnemyFleetKilled);

        m_myFleet.StartFleetWarp(skybox, () =>
        {
            ObjectManager.Instance.SetMyFleetPosition(zoneStage.fleetPosition);
            CameraController.Instance.SnapToTarget();
            SetEnterZoneState(EEnterZoneState.zone);
            UIManager.Instance.ShowPanel("UIPanelCameraView");
            bool isFirstClear = IsAlreadyCleared(zoneStage) == false;
            EventManager.TriggerZoneEntered(zoneStage.zoneName, isFirstClear);
            StartBattleInZone(zoneStage);
        });
    }

    // 현재 전투 존의 ZoneMapCell 캐싱 및 선택 표시 갱신
    private void CacheCurrentZoneCell()
    {
        if (m_currentZoneCell != null)
        {
            m_currentZoneCell.SetSelected(false);
            if (IsAlreadyCleared(m_currentZoneCell.m_zoneStageConfig) == false)
                m_currentZoneCell.SetState(EZoneState.Current, false);
        }

        m_currentZoneCell = null;
        if (m_currentZoneStage == null) return;

        if (m_zoneStageCells.TryGetValue(m_currentZoneStage.zoneName, out ZoneMapCell cell))
            m_currentZoneCell = cell;

        if (m_currentZoneCell != null)
            m_currentZoneCell.SetSelected(true);
    }

    private void StartBattleInZone(ZoneStageConfig zoneStage)
    {
        // 패배(함대 전멸) 시에만 콜백 호출 — 승리/클리어는 서버 응답으로 판정
        ObjectManager.Instance.StartSpawnEnemies(zoneStage, (_) => ReturnToSafeZone());
    }

    private bool IsAlreadyCleared(ZoneStageConfig zoneStage)
    {
        var clearedZones = m_myCharacter.m_characterInfo.clearedZones;
        return clearedZones != null && clearedZones.Contains(zoneStage.zoneName);
    }

    // 적 함대 전멸 시 호출 — 서버에 클리어 보고 후 자동 안전지역 텔레포트
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

    // 클리어 응답: 보상 처리 + 신규 클리어 판정 + 보상 팝업 → 안전지역 텔레포트
    private void OnClearZoneStageResponse(ApiResponse<ClearZoneStageResponse> response)
    {
        if (response.errorCode != 0)
        {
            Debug.LogWarning($"[Zone] ClearZoneStage 에러: {ErrorCodeMapping.GetMessage(response.errorCode)} ({response.errorCode})");
            ReturnToSafeZone();
            return;
        }

        var character = DataManager.Instance.m_currentCharacter;
        int mineralBefore = (character != null && character.m_characterInfo != null) ? character.m_characterInfo.mineral : 0;

        if (character != null && response.data.rewardInfo != null)
        {
            character.UpdateMineral(response.data.rewardInfo.mineralRemain);
        }

        // 신규 클리어 완료 — clearedZones 갱신 및 수확 시작 시각 기록
        if (response.data.isZoneCleared == true && character != null)
        {
            character.m_characterInfo.collectDateTime = response.data.collectDateTime;

            if (character.m_characterInfo.clearedZones == null)
                character.m_characterInfo.clearedZones = new List<string>();

            string newlyCleared = response.data.clearedZoneName;
            if (character.m_characterInfo.clearedZones.Contains(newlyCleared) == false)
                character.m_characterInfo.clearedZones.Add(newlyCleared);

            if (m_zoneStageCells.TryGetValue(newlyCleared, out ZoneMapCell clearedCell))
                clearedCell.SetState(EZoneState.Cleared, true);

            CacheCurrentZoneCell();
            SelectNextZoneStage(newlyCleared);
        }

        // 광물 획득 팝업 → 확인 또는 5초 후 안전지역 텔레포트
        int mineralGained = 0;
        if (character != null && character.m_characterInfo != null)
            mineralGained = character.m_characterInfo.mineral - mineralBefore;

        if (mineralGained > 0)
        {
            string title = LocalizationManager.Instance.Get("exploration_battle_victory");
            string rewardText = LocalizationManager.Instance.Get("exploration_battle_mineral_reward", mineralGained);
            string msg = $"{CommonUtility.Sprite("crystal-growth")} {rewardText}";
            UIManager.Instance.ShowPopupAlert(title, msg, ReturnToSafeZone, autoCloseSec: 5f);
        }
        else
        {
            ReturnToSafeZone();
        }
    }

    // 클리어한 존의 다음 스테이지를 m_selectedZoneStagePerGroup에 저장
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

        // 같은 그룹 내 다음 스테이지 없으면 다음 그룹 첫 스테이지
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

    private void ReturnToSafeZone()
    {
        EventManager.Unsubscribe_EnemyFleetKilled(OnEnemyFleetKilled);

        Material safeSkybox = m_datatableZone.GetZone(0).skyboxMaterial;
        ZoneStageConfig zoneStageConfig = m_datatableZone.GetZoneStage(0);
        
        var pp = WarpPostProcessing.Instance;
        if (pp != null)
            pp.SetSkyboxBlendTarget(safeSkybox, zoneStageConfig.skyboxRotation);

        UIManager.Instance.HidePanel("UIPanelCameraView");
        CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_my_fleet);

        m_myFleet.StartFleetWarp(safeSkybox, () =>
        {
            ObjectManager.Instance.SetMyFleetPosition(zoneStageConfig.fleetPosition);
            CameraController.Instance.SnapToTarget();

            m_currentZoneStage = null;
            if (m_currentZoneCell != null)
            {
                m_currentZoneCell.SetSelected(false);
                if (IsAlreadyCleared(m_currentZoneCell.m_zoneStageConfig) == false)
                    m_currentZoneCell.SetState(EZoneState.Current, false);
                m_currentZoneCell = null;
            }
            SetEnterZoneState(EEnterZoneState.safe);
            m_myFleet.SetFleetState(EFleetState.None); // 전투 상태 해제 → 미사일 커버 닫힘
            UpdateGroupTabVisual();
            ShowGroupMap(m_selectedZoneIndex);

            if (m_isFleetWiped)
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
}
