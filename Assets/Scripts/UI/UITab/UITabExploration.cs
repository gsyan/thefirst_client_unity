// 탐사 탭 — 그룹 탭(Z1~Z9) + 존 맵 셀(안개 reveal), 존 진입/재진입/킬 보상 처리
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

enum EEnterZoneState
{
    safe,
    warp,
    zone,
}

public class UITabExploration : UITabBase
{
    [SerializeField] private RowLabelValue m_rowLabelValueMineral;
    [SerializeField] private RowLabelValue m_rowLabelValueMineralRare;
    [SerializeField] private RowLabelValue m_rowLabelValueMineralExotic;
    [SerializeField] private RowLabelValue m_rowLabelValueMineralDark;

    [SerializeField] private Button m_safeZoneButton;
    [SerializeField] private Button m_collectMineralButton;
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
    private float m_totalMineralPerHour;
    private float m_totalMineralRarePerHour;
    private float m_totalMineralExoticPerHour;
    private float m_totalMineralDarkPerHour;
    private ZoneConfig m_currentZone;
    private ZoneConfig m_selectedZone;      // 현재 그룹에서 선택된 존
    private ZoneMapCell m_currentZoneCell;
    private readonly Dictionary<string, ZoneMapCell> m_zoneCells = new Dictionary<string, ZoneMapCell>();
    private readonly Dictionary<int, ZoneConfig> m_selectedZonePerGroup = new(); // 그룹별 선택 존 기억

    private int m_selectedGroupIndex = 1;
    private EEnterZoneState m_enterZoneState;
    private bool m_isFleetWiped;
    private int m_currentWave;
    private int m_zoneClearCount;
    private Coroutine m_mineralUpdateCoroutine;
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

        m_collectMineralButton.onClick.AddListener(OnCollectZoneClicked);
        m_safeZoneButton.onClick.AddListener(ReturnToSafeZone);
        if (m_zoneTryButton != null) m_zoneTryButton.onClick.AddListener(OnZoneTryButtonClicked);

        EventManager.Subscribe_MyFleetDestroyed(OnMyFleetWiped);

        m_rowLabelValueMineral.SetLabel("mineral_amount");
        m_rowLabelValueMineralRare.SetLabel("mineral_rare_amount");
        m_rowLabelValueMineralExotic.SetLabel("mineral_exotic_amount");
        m_rowLabelValueMineralDark.SetLabel("mineral_dark_amount");

        SetupGroupTabs();
        InitializeZoneMap();
        UpdateZoneInfo();
        SetEnterZoneState(EEnterZoneState.safe);

        var pp = WarpPostProcessing.Instance;
        if (pp != null)
        {
            var cleared = m_myCharacter.m_characterInfo.clearedZones;
            if (cleared == null || cleared.Count == 0) return;
            var lastZone = m_datatableZone.GetZoneByName(cleared[cleared.Count - 1]);
            if (lastZone != null) pp.SetSkyboxBlendTarget(lastZone.skyboxMaterial);
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
        if (m_selectedZonePerGroup.TryGetValue(m_selectedGroupIndex, out ZoneConfig prevZone) &&
            m_zoneCells.TryGetValue(prevZone.zoneName, out ZoneMapCell prevCell))
            prevCell.SetSelected(false);

        m_selectedGroupIndex = groupIndex;
        ShowGroupMap(groupIndex);
        UpdateGroupTabVisual();
    }

    private void UpdateGroupTabVisual()
    {
        if (m_groupTabButtons == null) return;
        for (int i = 0; i < m_groupTabButtons.Length; i++)
        {
            if (m_groupTabButtons[i] == null) continue;
            bool selected = (i + 1) == m_selectedGroupIndex;
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
            int zonesInGroup = 0;
            for (int i = 1; i < m_datatableZone.ZoneCount; i++)
            {
                ZoneConfig z = m_datatableZone.GetZone(i);
                if (z != null && ParseZoneGroup(z.zoneName) == 1) zonesInGroup++;
            }
            int rows = zonesInGroup > 0 ? Mathf.CeilToInt((float)zonesInGroup / columns) : 5;

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
            if (group > 0) m_selectedGroupIndex = group;
        }

        for (int i = 1; i < m_datatableZone.ZoneCount; i++)
        {
            ZoneConfig zone = m_datatableZone.GetZone(i);
            if (zone == null) continue;

            var go = Instantiate(m_zoneMapCellPrefab, m_zoneCellContainer);
            go.name = zone.zoneName;
            var cell = go.GetComponent<ZoneMapCell>();

            EZoneState state;
            bool isCleared = clearedZoneNames != null && clearedZoneNames.Contains(zone.zoneName);
            if (isCleared)
                state = EZoneState.Cleared;
            else if (myShipCount >= ParseZoneGroup(zone.zoneName))
                state = EZoneState.Current;
            else
                state = EZoneState.Locked;

            ZoneConfig captured = zone;
            cell.Initialize(captured, () => OnTryZoneClicked(captured), state);
            go.SetActive(false); // ShowGroupMap에서 표시
            m_zoneCells[zone.zoneName] = cell;
        }

        UpdateGroupTabVisual();
        ShowGroupMap(m_selectedGroupIndex);
    }

    // 선택된 그룹(X)의 셀만 활성화, 이전 선택 존 복원 또는 기본값 선택
    private void ShowGroupMap(int groupIndex)
    {
        foreach (var kv in m_zoneCells)
        {
            bool visible = ParseZoneGroup(kv.Key) == groupIndex;
            kv.Value.gameObject.SetActive(visible);
        }

        if (m_currentZoneCell != null)
        {
            bool inCurrentGroup = ParseZoneGroup(m_currentZoneCell.m_zoneConfig.zoneName) == groupIndex;
            m_currentZoneCell.SetSelected(inCurrentGroup);
        }

        // 저장된 선택 존 복원, 없으면 기본값(미클리어 최고 스테이지 or 전체 클리어 시 최고)
        ZoneConfig toSelect = m_selectedZonePerGroup.TryGetValue(groupIndex, out ZoneConfig saved)
            ? saved
            : GetDefaultZoneForGroup(groupIndex);

        if (toSelect != null)
            ApplyZoneSelection(toSelect);
    }

    public override void OnTabActivated()
    {
        UpdateZoneInfo();
        StartMineralUpdateCoroutine();
    }

    public override void OnTabDeactivated()
    {
        StopMineralUpdateCoroutine();
    }

    private void StartMineralUpdateCoroutine()
    {
        StopMineralUpdateCoroutine();
        m_mineralUpdateCoroutine = StartCoroutine(MineralUpdateRoutine());
    }

    private void StopMineralUpdateCoroutine()
    {
        if (m_mineralUpdateCoroutine != null)
        {
            StopCoroutine(m_mineralUpdateCoroutine);
            m_mineralUpdateCoroutine = null;
        }
    }

    // 1초마다 자원 누적량 UI 갱신
    private IEnumerator MineralUpdateRoutine()
    {
        while (true)
        {
            yield return m_updateInterval;
            UpdateMineralTextsOnly();
        }
    }

    private void UpdateMineralTextsOnly()
    {
        if (m_hasClearedZone == false) return;
        float elapsedSeconds = GetElapsedSecondsFromCollect();
        SetMineralTexts(
            m_totalMineralPerHour / 3600f * elapsedSeconds, m_totalMineralPerHour,
            m_totalMineralRarePerHour / 3600f * elapsedSeconds, m_totalMineralRarePerHour,
            m_totalMineralExoticPerHour / 3600f * elapsedSeconds, m_totalMineralExoticPerHour,
            m_totalMineralDarkPerHour / 3600f * elapsedSeconds, m_totalMineralDarkPerHour
        );
    }

    private void UpdateZoneInfo()
    {
        if (m_datatableZone == null || m_myCharacter == null) return;
        var clearedZoneNames = m_myCharacter.m_characterInfo.clearedZones;

        m_totalMineralPerHour = 0f;
        m_totalMineralRarePerHour = 0f;
        m_totalMineralExoticPerHour = 0f;
        m_totalMineralDarkPerHour = 0f;
        m_hasClearedZone = false;

        if (clearedZoneNames != null && clearedZoneNames.Count > 0)
        {
            var clearedZones = m_datatableZone.GetZonesByNames(clearedZoneNames);
            for (int i = 0; i < clearedZones.Count; i++)
            {
                m_totalMineralPerHour      += clearedZones[i].mineralPerHour;
                m_totalMineralRarePerHour  += clearedZones[i].mineralRarePerHour;
                m_totalMineralExoticPerHour += clearedZones[i].mineralExoticPerHour;
                m_totalMineralDarkPerHour  += clearedZones[i].mineralDarkPerHour;
            }
            m_hasClearedZone = clearedZones.Count > 0;
        }

        if (m_hasClearedZone == false)
        {
            SetMineralTexts(0, 0, 0, 0, 0, 0, 0, 0);
        }
        else
        {
            float elapsed = GetElapsedSecondsFromCollect();
            SetMineralTexts(
                m_totalMineralPerHour / 3600f * elapsed, m_totalMineralPerHour,
                m_totalMineralRarePerHour / 3600f * elapsed, m_totalMineralRarePerHour,
                m_totalMineralExoticPerHour / 3600f * elapsed, m_totalMineralExoticPerHour,
                m_totalMineralDarkPerHour / 3600f * elapsed, m_totalMineralDarkPerHour
            );
        }
    }

    private float GetElapsedSecondsFromCollect()
    {
        string collectDateTimeStr = m_myCharacter.m_characterInfo.collectDateTime;
        if (string.IsNullOrEmpty(collectDateTimeStr)) return 0f;
        if (DateTime.TryParse(collectDateTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime collectDateTime))
        {
            TimeSpan elapsed = DateTime.UtcNow - collectDateTime;
            return (float)elapsed.TotalSeconds;
        }
        return 0f;
    }

    private void SetMineralTexts(float mineral, float mineralPerH, float rare, float rarePerH,
                                  float exotic, float exoticPerH, float dark, float darkPerH)
    {
        m_rowLabelValueMineral.SetValues(FormatMineralText(mineral, mineralPerH));
        m_rowLabelValueMineralRare.SetValues(FormatMineralText(rare, rarePerH));
        m_rowLabelValueMineralExotic.SetValues(FormatMineralText(exotic, exoticPerH));
        m_rowLabelValueMineralDark.SetValues(FormatMineralText(dark, darkPerH));
    }

    private string FormatMineralText(float accumulated, float perHour)
    {
        return $"{CommonUtility.FormatBigNumber(accumulated)}({CommonUtility.FormatBigNumber(perHour)}/h)";
    }

    private void SetEnterZoneState(EEnterZoneState enterZoneState)
    {
        m_enterZoneState = enterZoneState;
        m_safeZoneButton.gameObject.SetActive(enterZoneState == EEnterZoneState.zone);
    }

    private void OnWaveStarted(int currentWave, int zoneClearCount)
    {
        m_currentWave = currentWave;
        m_zoneClearCount = zoneClearCount;
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
    private void OnTryZoneClicked(ZoneConfig zone)
    {
        // 이전 선택 셀 outline 해제
        if (m_selectedZone != null && m_zoneCells.TryGetValue(m_selectedZone.zoneName, out ZoneMapCell prevCell))
            prevCell.SetSelected(false);

        m_selectedZonePerGroup[m_selectedGroupIndex] = zone;
        ApplyZoneSelection(zone);
    }

    private void ApplyZoneSelection(ZoneConfig zone)
    {
        m_selectedZone = zone;
        if (m_zoneCells.TryGetValue(zone.zoneName, out ZoneMapCell cell))
            cell.SetSelected(true);
        RefreshZoneDetailText(zone);
    }

    // 그룹 내 기본 선택: 미클리어 최고 스테이지, 전부 클리어 시 최고 스테이지
    private ZoneConfig GetDefaultZoneForGroup(int groupIndex)
    {
        var clearedZones = m_myCharacter?.m_characterInfo.clearedZones;
        ZoneConfig highest = null;
        ZoneConfig lowestUncleared = null;

        for (int i = 1; i < m_datatableZone.ZoneCount; i++)
        {
            ZoneConfig zone = m_datatableZone.GetZone(i);
            if (zone == null || ParseZoneGroup(zone.zoneName) != groupIndex) continue;

            int stage = ParseZoneStage(zone.zoneName);
            if (highest == null || stage > ParseZoneStage(highest.zoneName))
                highest = zone;

            bool isCleared = clearedZones != null && clearedZones.Contains(zone.zoneName);
            if (isCleared == false && (lowestUncleared == null || stage < ParseZoneStage(lowestUncleared.zoneName)))
                lowestUncleared = zone;
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
        if (m_selectedZone == null) return;
        if (m_enterZoneState == EEnterZoneState.warp) return;
        if (m_enterZoneState == EEnterZoneState.zone && m_currentZone != null && m_currentZone.zoneName == m_selectedZone.zoneName) return;

        int requiredShips = ParseZoneGroup(m_selectedZone.zoneName);
        if (requiredShips > 0 && m_myFleet.m_ships.Count < requiredShips)
        {
            ShowResultMessage(LocalizationManager.Instance.Get("zone_insufficient_ships"), 3f);
            return;
        }

        if (m_enterZoneState == EEnterZoneState.zone)
        {
            EventManager.Unsubscribe_WaveStarted(OnWaveStarted);
            EventManager.Unsubscribe_EnemyFleetKilled(OnEnemyFleetKilledForReward);
            ObjectManager.Instance.StopEnemySpawning();
            ObjectManager.Instance.OrderAllAircraftReturn();
            ObjectManager.Instance.CleanupAllProjectiles();
            ObjectManager.Instance.RemoveAllEnemyFleets();
        }

        ZoneConfig zone = m_selectedZone;
        UIManager.Instance.ShowConfirmPopup(
            zone.zoneName,
            LocalizationManager.Instance.Get("zone_enter_confirm"),
            null, null, null,
            onConfirm: () => TryEnterZoneWithAd(zone)
        );
    }

    // 선택된 존 정보를 m_zoneDetailText에 표시
    private void RefreshZoneDetailText(ZoneConfig zone)
    {
        if (m_zoneDetailText == null || zone == null) return;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(zone.zoneName);
        if (string.IsNullOrEmpty(zone.zoneDescription) == false)
            sb.AppendLine(zone.zoneDescription);
        if (zone.killRewardMineral > 0)
            sb.AppendLine($"Kill M: +{CommonUtility.FormatBigNumber(zone.killRewardMineral)}");
        if (zone.killRewardMineralRare > 0)
            sb.AppendLine($"Kill R: +{CommonUtility.FormatBigNumber(zone.killRewardMineralRare)}");
        if (zone.mineralPerHour > 0)
            sb.AppendLine($"M: {CommonUtility.FormatBigNumber(zone.mineralPerHour)}/h");
        if (zone.mineralRarePerHour > 0)
            sb.AppendLine($"R: {CommonUtility.FormatBigNumber(zone.mineralRarePerHour)}/h");
        if (zone.mineralExoticPerHour > 0)
            sb.AppendLine($"E: {CommonUtility.FormatBigNumber(zone.mineralExoticPerHour)}/h");
        if (zone.mineralDarkPerHour > 0)
            sb.AppendLine($"D: {CommonUtility.FormatBigNumber(zone.mineralDarkPerHour)}/h");
        m_zoneDetailText.text = sb.ToString().TrimEnd();
    }

    private void TryEnterZoneWithAd(ZoneConfig zone)
    {
        bool adInstanceNull = AdManager.Instance == null;
        bool adReady = adInstanceNull == false && AdManager.Instance.IsRewardedAdReady;

        if (adInstanceNull == false && adReady == true)
        {
            AdManager.Instance.ShowRewardedAd(result =>
            {
                if (result == EAdResult.Rewarded)
                {
                    EnterZone(zone);
                }
                else if (result == EAdResult.Failed)
                {
                    ShowResultMessage("[광고] 광고 오류로 입장합니다");
                    EnterZone(zone);
                }
                else if (result == EAdResult.UserClosed)
                {
                    ShowResultMessage("[광고] 광고를 시청해야 입장할 수 있습니다");
                }
            });
        }
        else
        {
            // 광고 미준비 → 입장 허용 후 즉시 재로드 요청
            if (adInstanceNull == false)
                AdManager.Instance.RequestLoad();
            ShowResultMessage("[광고] 광고 미준비 상태로 입장합니다");
            EnterZone(zone);
        }
    }

    private void EnterZone(ZoneConfig zone)
    {
        SetEnterZoneState(EEnterZoneState.warp);
        var pp = WarpPostProcessing.Instance;
        if (pp != null && zone != null)
            pp.SetSkyboxBlendTarget(zone.skyboxMaterial);

        m_currentZone = zone;
        CacheCurrentZoneCell();
        EventManager.Subscribe_WaveStarted(OnWaveStarted);
        EventManager.Subscribe_EnemyFleetKilled(OnEnemyFleetKilledForReward);

        m_myFleet.StartFleetWarp(zone.skyboxMaterial, () =>
        {
            SetEnterZoneState(EEnterZoneState.zone);
            UIManager.Instance.ShowPanel("UIPanelCameraView");
            StartBattleInZone(zone);
        });
    }

    // 현재 전투 존의 ZoneMapCell 캐싱 및 선택 표시 갱신
    private void CacheCurrentZoneCell()
    {
        if (m_currentZoneCell != null)
        {
            m_currentZoneCell.SetSelected(false);
            if (IsAlreadyCleared(m_currentZoneCell.m_zoneConfig) == false)
                m_currentZoneCell.SetState(EZoneState.Current, false);
        }

        m_currentZoneCell = null;
        if (m_currentZone == null) return;

        if (m_zoneCells.TryGetValue(m_currentZone.zoneName, out ZoneMapCell cell))
            m_currentZoneCell = cell;

        if (m_currentZoneCell != null)
            m_currentZoneCell.SetSelected(true);
    }

    private void StartBattleInZone(ZoneConfig zone)
    {
        ObjectManager.Instance.StartSpawnEnemies(zone, (isVictory) =>
        {
            if (isVictory)
            {
                if (IsAlreadyCleared(zone))
                    StartBattleInZone(zone);
                else
                {
                    var request = new ZoneClearRequest { zoneName = zone.zoneName };
                    NetworkManager.Instance.ClearZone(request, OnZoneClearResponse);
                }
            }
            else
            {
                ReturnToSafeZone();
            }
        });
    }

    private bool IsAlreadyCleared(ZoneConfig zone)
    {
        var clearedZones = m_myCharacter.m_characterInfo.clearedZones;
        return clearedZones != null && clearedZones.Contains(zone.zoneName);
    }

    private void OnEnemyFleetKilledForReward()
    {
        if (m_currentZone == null) return;

        if (IsAlreadyCleared(m_currentZone) == false && m_currentZoneCell != null)
            m_currentZoneCell.SetClearProgress(m_currentWave, m_zoneClearCount);

        var request = new ZoneKillRequest { zoneName = m_currentZone.zoneName };
        NetworkManager.Instance.KillZoneEnemy(request, OnZoneKillResponse);
    }

    private void OnZoneKillResponse(ApiResponse<ZoneKillResponse> response)
    {
        if (response.errorCode != 0) return;
        var character = DataManager.Instance.m_currentCharacter;
        if (character != null && response.data.rewardInfo != null)
        {
            character.UpdateMineral(response.data.rewardInfo.remainMineral);
            character.UpdateMineralRare(response.data.rewardInfo.remainMineralRare);
            character.UpdateMineralExotic(response.data.rewardInfo.remainMineralExotic);
            character.UpdateMineralDark(response.data.rewardInfo.remainMineralDark);
        }
    }

    private void ReturnToSafeZone()
    {
        EventManager.Unsubscribe_WaveStarted(OnWaveStarted);
        EventManager.Unsubscribe_EnemyFleetKilled(OnEnemyFleetKilledForReward);

        ZoneConfig zoneConfig = m_datatableZone.GetZone(0);
        if (zoneConfig == null) return;

        var pp = WarpPostProcessing.Instance;
        if (pp != null)
            pp.SetSkyboxBlendTarget(zoneConfig.skyboxMaterial);

        UIManager.Instance.HidePanel("UIPanelCameraView");
        CameraController.Instance.SetCameraFocusTarget(ECameraFocusTarget.camera_focus_my_fleet);

        m_myFleet.StartFleetWarp(zoneConfig.skyboxMaterial, () =>
        {
            m_currentZone = null;
            if (m_currentZoneCell != null)
            {
                m_currentZoneCell.SetSelected(false);
                if (IsAlreadyCleared(m_currentZoneCell.m_zoneConfig) == false)
                    m_currentZoneCell.SetState(EZoneState.Current, false);
                m_currentZoneCell = null;
            }
            SetEnterZoneState(EEnterZoneState.safe);

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

    private void OnZoneClearResponse(ApiResponse<ZoneClearResponse> response)
    {
        if (response.errorCode != 0) return;

        var character = DataManager.Instance.m_currentCharacter;
        if (character != null)
        {
            character.m_characterInfo.collectDateTime = response.data.collectDateTime;

            if (response.data.rewardInfo != null)
            {
                character.UpdateMineral(response.data.rewardInfo.remainMineral);
                character.UpdateMineralRare(response.data.rewardInfo.remainMineralRare);
                character.UpdateMineralExotic(response.data.rewardInfo.remainMineralExotic);
                character.UpdateMineralDark(response.data.rewardInfo.remainMineralDark);
            }

            // clearedZones 목록에 추가
            if (character.m_characterInfo.clearedZones == null)
                character.m_characterInfo.clearedZones = new List<string>();
            string newlyCleared = response.data.clearedZoneName;
            if (character.m_characterInfo.clearedZones.Contains(newlyCleared) == false)
                character.m_characterInfo.clearedZones.Add(newlyCleared);

            // 방금 클리어된 셀 reveal
            if (m_zoneCells.TryGetValue(newlyCleared, out ZoneMapCell clearedCell))
                clearedCell.SetState(EZoneState.Cleared, true);
        }

        UpdateZoneInfo();
        CacheCurrentZoneCell();

        if (m_currentZone != null)
            StartBattleInZone(m_currentZone);
    }

    private void OnCollectZoneClicked()
    {
        // 하트비트 먼저 발송하여 lastOnlineAt 갱신 후 수확 — 네트워크 불안정으로 hb 누락된 경우 보정
        NetworkManager.Instance.Heartbeat(() =>
        {
            NetworkManager.Instance.CollectZone(new ZoneCollectRequest(), OnZoneCollectResponse);
        });
    }

    private void OnZoneCollectResponse(ApiResponse<ZoneCollectResponse> response)
    {
        if (response.errorCode != 0) return;
        var character = DataManager.Instance.m_currentCharacter;
        if (character != null)
        {
            character.m_characterInfo.collectDateTime = response.data.collectDateTime;
            if (response.data.rewardInfo != null)
            {
                character.UpdateMineral(response.data.rewardInfo.remainMineral);
                character.UpdateMineralRare(response.data.rewardInfo.remainMineralRare);
                character.UpdateMineralExotic(response.data.rewardInfo.remainMineralExotic);
                character.UpdateMineralDark(response.data.rewardInfo.remainMineralDark);
            }
        }
    }
}
