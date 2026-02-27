// 탐사 탭 — 존 목록/웨이브 UI, 존 진입/재진입/다음존 이동, 킬 보상 처리
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


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
    private ScrollViewZoneItem m_currentZoneItem;                       // 현재 전투 중인 존 UI 아이템
    private readonly List<ScrollViewZoneItem> m_zoneItemPool = new List<ScrollViewZoneItem>();
    private readonly List<ScrollViewZoneItem> m_zoneItemActive = new List<ScrollViewZoneItem>();
    
    private EEnterZoneState m_enterZoneState;
    private bool m_isFleetWiped;
    private int m_currentWave;
    private int m_zoneClearCount;
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
        SetEnterZoneState(EEnterZoneState.safe);
     
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
            else
                zoneState = EZoneState.Current;

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
        //CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
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
    private void SetEnterZoneState(EEnterZoneState enterZoneState)
    {   
        m_enterZoneState =  enterZoneState;
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

    // UI 버튼 콜백 → 현재 전투 중인 존 재진입 차단
    private void OnTryZoneClicked(ZoneConfig zone)
    {
        // 워프중이면 리턴
        if(m_enterZoneState == EEnterZoneState.warp) return;
        // 현재 존과 같은 존이면 리턴       
        if (m_enterZoneState == EEnterZoneState.zone && m_currentZone != null && m_currentZone.zoneName == zone.zoneName) return;
        
        // 현재 존이라면 초기화 해야할 것들 초기화
        if (m_enterZoneState == EEnterZoneState.zone)
        {
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
        SetEnterZoneState(EEnterZoneState.warp);
        var pp = WarpPostProcessing.Instance;
        if (pp != null && zone != null)
            pp.SetSkyboxBlendTarget(zone.skyboxMaterial);

        m_currentZone = zone;
        CacheCurrentZoneItem();
        EventManager.Subscribe_WaveStarted(OnWaveStarted);
        EventManager.Subscribe_EnemyFleetKilled(OnEnemyFleetKilledForReward);

        //Debug.Log($"step:{m_currentZone.zoneName} warp");
        m_myFleet.StartFleetWarp(zone.skyboxMaterial, () =>
        {
            //Debug.Log($"step:{m_currentZone.zoneName} complete");
            SetEnterZoneState(EEnterZoneState.zone);
            UIManager.Instance.ShowPanel("UIPanelCameraView");
            StartBattleInZone(zone);
        });
    }

    private void CacheCurrentZoneItem()
    {
        if (m_currentZoneItem != null)
        {
            m_currentZoneItem.SetSelected(false);
            if (IsAlreadyCleared(m_currentZone) == false)
                m_currentZoneItem.SetZoneItemState(EZoneState.Current);
        }

        m_currentZoneItem = null;
        if (m_currentZone == null) return;
        for (int i = 0; i < m_zoneItemActive.Count; i++)
        {
            if (m_zoneItemActive[i].m_zoneConfig.zoneName == m_currentZone.zoneName)
            {
                m_currentZoneItem = m_zoneItemActive[i];
                break;
            }
        }

        if (m_currentZoneItem != null)
            m_currentZoneItem.SetSelected(true);
    }

    // 적 스폰 및 전투 결과 처리
    private void StartBattleInZone(ZoneConfig zone)
    {
        ObjectManager.Instance.StartSpawnEnemies(zone, (isVictory) =>
        {
            if (isVictory)
            {
                // 이미 클리어된 존이면 API 호출 없이 바로 재진입
                if (IsAlreadyCleared(zone))
                {
                    StartBattleInZone(zone);
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

    // 적 함대 격멸 시 진행률 갱신 + 킬 보상 API 호출
    private void OnEnemyFleetKilledForReward()
    {
        if (m_currentZone == null) return;

        if (IsAlreadyCleared(m_currentZone) == false && m_currentZoneItem != null)
            m_currentZoneItem.SetClearProgress(m_currentWave, m_zoneClearCount);

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
            if (m_currentZoneItem != null)
            {
                m_currentZoneItem.SetSelected(false);
                m_currentZoneItem.SetZoneItemState(EZoneState.Current);
                m_currentZoneItem = null;
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
            StartBattleInZone(m_currentZone);
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
