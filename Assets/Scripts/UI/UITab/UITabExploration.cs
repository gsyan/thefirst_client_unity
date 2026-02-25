// 탐사 탭 — 존 목록/웨이브 UI, 존 진입/재진입/다음존 이동, 킬 보상 처리
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UITabExploration : UITabBase
{
    [SerializeField] private RowLabelValue m_rowLabelValueMineral;
    [SerializeField] private RowLabelValue m_rowLabelValueMineralRare;
    [SerializeField] private RowLabelValue m_rowLabelValueMineralExotic;
    [SerializeField] private RowLabelValue m_rowLabelValueMineralDark;

    [SerializeField] private GameObject m_scrollViewZone;               // 스크롤뷰 루트 (Content의 상위)
    [SerializeField] private RectTransform m_scrollViewZoneContent;
    [SerializeField] private GameObject m_scrollViewZoneItem;           // 프리팹
    
    [SerializeField] private Button m_safeZoneButton;
    [SerializeField] private Button m_collectMineralButton;
    [SerializeField] private DataTableZone m_datatableZone;             // Zone 설정 ScriptableObject

    // 클리어 기준 앞뒤로 표시할 존 개수 (Inspector에서 조절 가능)
    [SerializeField] private int m_zonePadding = 4;

    private SpaceFleet m_myFleet;
    private Character m_myCharacter;
    private ZoneConfig m_clearedZone;
    private ZoneConfig m_currentZone;                                   // 현재 전투 중인 존
    private readonly List<ScrollViewZoneItem> m_zoneItemPool = new List<ScrollViewZoneItem>();
    private readonly List<ScrollViewZoneItem> m_zoneItemActive = new List<ScrollViewZoneItem>();
    
    private bool m_isInZone;
    private bool m_isFleetWiped;
    private Coroutine m_mineralUpdateCoroutine;
    private readonly WaitForSeconds m_updateInterval = new WaitForSeconds(1f);
    private ScrollViewAutoCenter m_zoneAutoCenter;

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

        EventManager.Subscribe_MyFleetDestroyed(OnMyFleetWiped);

        if (m_scrollViewZone != null)
        {
            if (!m_scrollViewZone.TryGetComponent(out m_zoneAutoCenter))
                m_zoneAutoCenter = m_scrollViewZone.AddComponent<ScrollViewAutoCenter>();
        }
        
        m_rowLabelValueMineral.SetLabel("mineral_amount");
        m_rowLabelValueMineralRare.SetLabel("mineral_rare_amount");
        m_rowLabelValueMineralExotic.SetLabel("mineral_exotic_amount");
        m_rowLabelValueMineralDark.SetLabel("mineral_dark_amount");

        PopulateZoneScrollView();
        UpdateZoneInfo();
        SetExplorationUI(true);

        var pp = WarpPostProcessing.Instance;
        if (pp != null)
        {
            string clearedZone = m_myCharacter.m_characterInfo.clearedZone;
            if (string.IsNullOrEmpty(clearedZone)) return;

            ZoneConfig zone = m_datatableZone.GetNextZone(clearedZone);
            if (zone == null) return;
            pp.SetSkyboxBlendTarget(zone.skyboxMaterial);
        }
    }

    // clearedZone 기준 앞뒤 m_zonePadding개씩 존 표시 (그룹 경계 무관, 전체 존 목록 기준)
    private void PopulateZoneScrollView()
    {
        if (m_scrollViewZoneContent == null || m_scrollViewZoneItem == null) return;
        if (m_datatableZone == null || m_myCharacter == null) return;

        for (int i = 0; i < m_zoneItemActive.Count; i++)
            m_zoneItemActive[i].gameObject.SetActive(false);
        m_zoneItemActive.Clear();

        string clearedZoneName = m_myCharacter.m_characterInfo.clearedZone;
        int clearedIndex = string.IsNullOrEmpty(clearedZoneName)
            ? 0
            : m_datatableZone.GetZoneIndex(clearedZoneName);

        int windowStart = Mathf.Max(1, clearedIndex - m_zonePadding);
        int windowEnd = Mathf.Min(m_datatableZone.ZoneCount - 1, clearedIndex + m_zonePadding);

        int poolIndex = 0;
        ScrollViewZoneItem currentZoneItem = null;

        for (int i = windowStart; i <= windowEnd; i++)
        {
            ZoneConfig zone = m_datatableZone.GetZone(i);
            if (zone == null) continue;

            EZoneState zoneState;
            if (i <= clearedIndex)
                zoneState = EZoneState.Cleared;
            else if (i == clearedIndex + 1)
                zoneState = EZoneState.Current;
            else
                zoneState = EZoneState.Locked;

            ScrollViewZoneItem scrollViewItem;
            if (poolIndex < m_zoneItemPool.Count)
            {
                scrollViewItem = m_zoneItemPool[poolIndex];
                scrollViewItem.gameObject.SetActive(true);
            }
            else
            {
                var item = Instantiate(m_scrollViewZoneItem, m_scrollViewZoneContent);
                item.name = m_scrollViewZoneItem.name;
                scrollViewItem = item.GetComponent<ScrollViewZoneItem>();
                m_zoneItemPool.Add(scrollViewItem);
            }

            ZoneConfig capturedZone = zone;
            scrollViewItem.InitializeScrollViewZoneItem(
                capturedZone,
                () => OnTryZoneClicked(capturedZone),
                zoneState
            );
            m_zoneItemActive.Add(scrollViewItem);

            if (zoneState == EZoneState.Current)
                currentZoneItem = scrollViewItem;

            poolIndex++;
        }

        // Current 존 아이템을 스크롤뷰 중앙에 배치
        if (m_zoneAutoCenter != null)
        {
            ScrollViewZoneItem target = currentZoneItem;
            if (target == null && m_zoneItemActive.Count > 0)
                target = m_zoneItemActive[^1];
            if (target != null)
                m_zoneAutoCenter.CenterOnChild((RectTransform)target.transform);
        }
    }

    public override void OnTabActivated()
    {
        UpdateZoneInfo();
        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
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
        if (m_clearedZone == null) return;

        float elapsedSeconds = GetElapsedSecondsFromCollect();
        SetMineralTexts(
            m_clearedZone.MineralPerSecond * elapsedSeconds, m_clearedZone.mineralPerHour,
            m_clearedZone.MineralRarePerSecond * elapsedSeconds, m_clearedZone.mineralRarePerHour,
            m_clearedZone.MineralExoticPerSecond * elapsedSeconds, m_clearedZone.mineralExoticPerHour,
            m_clearedZone.MineralDarkPerSecond * elapsedSeconds, m_clearedZone.mineralDarkPerHour
        );
    }

    private void UpdateZoneInfo()
    {
        if (m_datatableZone == null || m_myCharacter == null) return;
        string clearedZoneName = m_myCharacter.m_characterInfo.clearedZone;

        if (!string.IsNullOrEmpty(clearedZoneName))
        {
            ZoneConfig clearedConfig = m_datatableZone.GetZoneByName(clearedZoneName);
            if (clearedConfig != null)
                m_clearedZone = clearedConfig;
        }

        if (string.IsNullOrEmpty(clearedZoneName))
        {
            SetMineralTexts(0, 0, 0, 0, 0, 0, 0, 0);
        }
        else
        {
            float elapsed = GetElapsedSecondsFromCollect();
            SetMineralTexts(
                m_clearedZone.MineralPerSecond * elapsed, m_clearedZone.mineralPerHour,
                m_clearedZone.MineralRarePerSecond * elapsed, m_clearedZone.mineralRarePerHour,
                m_clearedZone.MineralExoticPerSecond * elapsed, m_clearedZone.mineralExoticPerHour,
                m_clearedZone.MineralDarkPerSecond * elapsed, m_clearedZone.mineralDarkPerHour
            );
        }
    }

    // 마지막 수확 시간으로부터 경과한 초 계산
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

    // 존 진입 시 웨이브UI+안전지역버튼 활성화, zone스크롤뷰는 상시 표시
    private void SetExplorationUI(bool isSafeZone)
    {
        m_isInZone = !isSafeZone;
    }

    private void OnWaveStarted(int currentWave, int totalWaves)
    {
        
    }

    private void OnMyFleetWiped()
    {
        m_isFleetWiped = true;
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_MyFleetDestroyed(OnMyFleetWiped);
    }

    // UI 버튼 콜백 → 전투 중이면 다음 존(Current)만 허용, 나머지는 차단
    private void OnTryZoneClicked(ZoneConfig zone)
    {
        if (m_isInZone)
        {
            string clearedZone = m_myCharacter.m_characterInfo.clearedZone;
            ZoneConfig nextZone = string.IsNullOrEmpty(clearedZone) ? null : m_datatableZone.GetNextZone(clearedZone);
            if (nextZone == null || nextZone.zoneName != zone.zoneName) return;

            EventManager.Unsubscribe_WaveStarted(OnWaveStarted);
            EventManager.Unsubscribe_EnemyFleetKilled(OnEnemyFleetKilledForReward);
            ObjectManager.Instance.StopEnemySpawning();
            ObjectManager.Instance.OrderAllAircraftReturn();
            ObjectManager.Instance.CleanupAllProjectiles();
            ObjectManager.Instance.RemoveAllEnemyFleets();
        }

        EnterZone(zone);
    }

    // 워프 후 전투 시작
    private void EnterZone(ZoneConfig zone)
    {
        var pp = WarpPostProcessing.Instance;
        if (pp != null && zone != null)
            pp.SetSkyboxBlendTarget(zone.skyboxMaterial);

        m_currentZone = zone;
        SetExplorationUI(false);
        EventManager.Subscribe_WaveStarted(OnWaveStarted);
        EventManager.Subscribe_EnemyFleetKilled(OnEnemyFleetKilledForReward);

        m_myFleet.StartFleetWarp(zone.skyboxMaterial, () =>
        {
            UIManager.Instance.ShowPanel("UIPanelCameraView");
            StartBattleInZone(zone);
        });
    }

    // 워프 없이 같은 존에서 전투 재시작 (클리어 후 계속 전투)
    private void ContinueBattleInZone(ZoneConfig zone)
    {
        EventManager.Subscribe_WaveStarted(OnWaveStarted);
        EventManager.Subscribe_EnemyFleetKilled(OnEnemyFleetKilledForReward);
        StartBattleInZone(zone);
    }

    // 적 스폰 및 전투 결과 처리
    private void StartBattleInZone(ZoneConfig zone)
    {
        ObjectManager.Instance.StartSpawnEnemies(zone, (isVictory) =>
        {
            EventManager.Unsubscribe_WaveStarted(OnWaveStarted);
            EventManager.Unsubscribe_EnemyFleetKilled(OnEnemyFleetKilledForReward);

            if (isVictory)
            {
                // 이미 클리어된 존이면 API 호출 없이 바로 재진입
                if (IsAlreadyCleared(zone))
                {
                    ContinueBattleInZone(zone);
                }
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

    // 해당 존이 이미 클리어된 존인지 판별
    private bool IsAlreadyCleared(ZoneConfig zone)
    {
        string clearedZone = m_myCharacter.m_characterInfo.clearedZone;
        if (string.IsNullOrEmpty(clearedZone)) return false;
        int zoneIndex = m_datatableZone.GetZoneIndex(zone.zoneName);
        int clearedIndex = m_datatableZone.GetZoneIndex(clearedZone);
        return zoneIndex <= clearedIndex;
    }

    // 적 함대 격멸 시 킬 보상 API 호출
    private void OnEnemyFleetKilledForReward()
    {
        if (m_currentZone == null) return;
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

    // 안전지역 복귀 (패배/퇴각)
    private void ReturnToSafeZone()
    {
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
            SetExplorationUI(true);

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
            character.m_characterInfo.clearedZone = response.data.clearedZone;
            character.m_characterInfo.collectDateTime = response.data.collectDateTime;

            if (response.data.rewardInfo != null)
            {
                character.UpdateMineral(response.data.rewardInfo.remainMineral);
                character.UpdateMineralRare(response.data.rewardInfo.remainMineralRare);
                character.UpdateMineralExotic(response.data.rewardInfo.remainMineralExotic);
                character.UpdateMineralDark(response.data.rewardInfo.remainMineralDark);
            }
        }

        PopulateZoneScrollView();
        UpdateZoneInfo();

        // 클리어 후 안전지역으로 이동하지 않음 → 현재 존에서 계속 전투
        if (m_currentZone != null)
            ContinueBattleInZone(m_currentZone);
    }

    private void OnCollectZoneClicked()
    {
        var request = new ZoneCollectRequest {};
        NetworkManager.Instance.CollectZone(request, OnZoneCollectResponse);
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
