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
    [SerializeField] private Toggle m_autoExplorationToggle;            // 자동 탐사 토글
    
    [SerializeField] private GameObject m_waveContainer;
    [SerializeField] private GameObject m_scrollViewWave;               // 웨이브 스크롤뷰 루트
    [SerializeField] private RectTransform m_scrollViewWaveContent;
    [SerializeField] private GameObject m_scrollViewWaveItem;           // 웨이브 아이템 프리팹
    [SerializeField] private Button m_safeZoneButton;

    [SerializeField] private Button m_collectMineralButton;
    [SerializeField] private DataTableZone m_datatableZone;             // Zone 설정 ScriptableObject

    private SpaceFleet m_myFleet;
    private Character m_myCharacter;
    private ZoneConfig m_clearedZone;
    private readonly List<ScrollViewZoneItem> m_zoneItemPool = new List<ScrollViewZoneItem>();
    private readonly List<ScrollViewZoneItem> m_zoneItemActive = new List<ScrollViewZoneItem>();
    private readonly List<ScrollViewWaveItem> m_waveItemPool = new List<ScrollViewWaveItem>();
    private readonly List<ScrollViewWaveItem> m_waveItemActive = new List<ScrollViewWaveItem>();
    
    private bool m_isAutoExploration;
    private bool m_isInZone;
    private Coroutine m_mineralUpdateCoroutine;
    private readonly WaitForSeconds m_updateInterval = new WaitForSeconds(1f);
    private ScrollViewAutoCenter m_zoneAutoCenter;
    private ScrollViewAutoCenter m_waveAutoCenter;

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
        m_safeZoneButton.onClick.AddListener(OnEnterZoneZeroClicked);

        if (m_autoExplorationToggle != null)
        {
            m_autoExplorationToggle.isOn = m_isAutoExploration;
            m_autoExplorationToggle.onValueChanged.AddListener(v => m_isAutoExploration = v);
        }

        // 기존 스크롤뷰 오브젝트에 자동 센터링 컴포넌트 부착
        if (m_scrollViewZone != null)
        {
            if (!m_scrollViewZone.TryGetComponent(out m_zoneAutoCenter))
                m_zoneAutoCenter = m_scrollViewZone.AddComponent<ScrollViewAutoCenter>();
        }
        if (m_scrollViewWave != null)
        {
            if (!m_scrollViewWave.TryGetComponent(out m_waveAutoCenter))
                m_waveAutoCenter = m_scrollViewWave.AddComponent<ScrollViewAutoCenter>();
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

    // 현재 그룹의 zone 목록으로 스크롤뷰 채우기 (풀 재사용)
    private void PopulateZoneScrollView()
    {
        if (m_scrollViewZoneContent == null || m_scrollViewZoneItem == null) return;
        if (m_datatableZone == null || m_myCharacter == null) return;

        // 활성 아이템 비활성화
        for (int i = 0; i < m_zoneItemActive.Count; i++)
            m_zoneItemActive[i].gameObject.SetActive(false);
        m_zoneItemActive.Clear();

        int targetGroup = GetCurrentZoneGroup();
        string prefix = targetGroup + "-";

        string clearedZoneName = m_myCharacter.m_characterInfo.clearedZone;
        int clearedIndex = string.IsNullOrEmpty(clearedZoneName)
            ? 0
            : m_datatableZone.GetZoneIndex(clearedZoneName);

        int poolIndex = 0;
        for (int i = 0; i < m_datatableZone.ZoneCount; i++)
        {
            ZoneConfig zone = m_datatableZone.GetZone(i);
            if (zone == null || !zone.zoneName.StartsWith(prefix)) continue;

            int zoneIndex = m_datatableZone.GetZoneIndex(zone.zoneName);
            EZoneState zoneState;
            if (zoneIndex <= clearedIndex)
                zoneState = EZoneState.Cleared;
            else if (zoneIndex == clearedIndex + 1)
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
            poolIndex++;
        }

        // Current 상태인 존 아이템을 스크롤뷰 중앙에 배치
        if (m_zoneAutoCenter != null)
        {
            ScrollViewZoneItem centerTarget = null;
            for (int i = 0; i < m_zoneItemActive.Count; i++)
            {
                if (m_zoneItemActive[i].gameObject.activeSelf)
                {
                    centerTarget = m_zoneItemActive[i]; // 마지막 활성 아이템을 fallback
                }
            }
            // Current 상태 우선
            for (int i = 0; i < m_zoneItemActive.Count; i++)
            {
                // Current 상태 판별: clearedIndex+1 위치
                int zi = m_datatableZone.GetZoneIndex(m_zoneItemActive[i].m_zoneConfig.zoneName);
                if (zi == clearedIndex + 1)
                {
                    centerTarget = m_zoneItemActive[i];
                    break;
                }
            }
            if (centerTarget != null)
                m_zoneAutoCenter.CenterOnChild((RectTransform)centerTarget.transform);
        }
    }

    // 캐릭터의 클리어 진행도에 따라 표시할 zone 그룹 번호 반환
    private int GetCurrentZoneGroup()
    {
        string clearedZone = m_myCharacter.m_characterInfo.clearedZone;
        if (string.IsNullOrEmpty(clearedZone)) return 1;

        // "x-y" 파싱
        string[] parts = clearedZone.Split('-');
        if (parts.Length < 2 || !int.TryParse(parts[0], out int groupNum)) return 1;

        // 다음 zone 확인하여 그룹이 바뀌었는지 체크
        ZoneConfig nextZone = m_datatableZone.GetNextZone(clearedZone);
        if (nextZone == null) return groupNum; // 모든 zone 클리어 완료

        string[] nextParts = nextZone.zoneName.Split('-');
        if (nextParts.Length >= 2 && int.TryParse(nextParts[0], out int nextGroupNum))
        {
            // 다음 zone이 다른 그룹이면 현재 그룹 완료 → 다음 그룹 표시
            if (nextGroupNum != groupNum) return nextGroupNum;
        }

        return groupNum;
    }

    public override void OnTabActivated()
    {
        UpdateZoneInfo();
        StartMineralUpdateCoroutine();
    }

    public override void OnTabDeactivated()
    {
        StopMineralUpdateCoroutine();

        CameraController.Instance.SetTargetOfCameraController(m_myFleet.transform);
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

    // 자원 텍스트만 갱신 (zone 정보 변경 없이)
    private void UpdateMineralTextsOnly()
    {
        if (m_clearedZone == null) return;

        float elapsedSeconds = GetElapsedSecondsFromCollect();
        float accumulatedMineral = m_clearedZone.MineralPerSecond * elapsedSeconds;
        float accumulatedRare = m_clearedZone.MineralRarePerSecond * elapsedSeconds;
        float accumulatedExotic = m_clearedZone.MineralExoticPerSecond * elapsedSeconds;
        float accumulatedDark = m_clearedZone.MineralDarkPerSecond * elapsedSeconds;

        SetMineralTexts(
            accumulatedMineral, m_clearedZone.mineralPerHour,
            accumulatedRare, m_clearedZone.mineralRarePerHour,
            accumulatedExotic, m_clearedZone.mineralExoticPerHour,
            accumulatedDark, m_clearedZone.mineralDarkPerHour
        );
    }

    private void UpdateZoneInfo()
    {
        if (m_datatableZone == null) return;
        if (m_myCharacter == null) return;
        string clearedZoneName = m_myCharacter.m_characterInfo.clearedZone;

        // 클리어한 zone 표시 (있으면)
        if (!string.IsNullOrEmpty(clearedZoneName))
        {
            ZoneConfig clearedConfig = m_datatableZone.GetZoneByName(clearedZoneName);
            if (clearedConfig != null)
                m_clearedZone = clearedConfig;
        }

        if (string.IsNullOrEmpty(clearedZoneName))
        {
            // 클리어한 zone이 없으면 수확량 0
            SetMineralTexts(0, 0, 0, 0, 0, 0, 0, 0);
        }
        else
        {
            // 누적 자원량 계산
            float elapsedSeconds = GetElapsedSecondsFromCollect();
            float accumulatedMineral = m_clearedZone.MineralPerSecond * elapsedSeconds;
            float accumulatedRare = m_clearedZone.MineralRarePerSecond * elapsedSeconds;
            float accumulatedExotic = m_clearedZone.MineralExoticPerSecond * elapsedSeconds;
            float accumulatedDark = m_clearedZone.MineralDarkPerSecond * elapsedSeconds;

            SetMineralTexts(
                accumulatedMineral, m_clearedZone.mineralPerHour,
                accumulatedRare, m_clearedZone.mineralRarePerHour,
                accumulatedExotic, m_clearedZone.mineralExoticPerHour,
                accumulatedDark, m_clearedZone.mineralDarkPerHour
            );
        }
    }

    // 마지막 수확 시간으로부터 경과한 초 계산
    private float GetElapsedSecondsFromCollect()
    {
        string collectDateTimeStr = m_myCharacter.m_characterInfo.collectDateTime;
        if (string.IsNullOrEmpty(collectDateTimeStr)) return 0f;

        // RoundtripKind: "Z" 접미사를 UTC로 올바르게 처리
        if (DateTime.TryParse(collectDateTimeStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime collectDateTime))
        {
            TimeSpan elapsed = DateTime.UtcNow - collectDateTime;
            return (float)elapsed.TotalSeconds;
        }
        return 0f;
    }

    // 자원 텍스트 업데이트 (누적량, 시간당 수확량)
    private void SetMineralTexts(float mineral, float mineralPerH, float rare, float rarePerH,
                                  float exotic, float exoticPerH, float dark, float darkPerH)
    {
        m_rowLabelValueMineral.SetValue(FormatMineralText(mineral, mineralPerH));
        m_rowLabelValueMineralRare.SetValue(FormatMineralText(rare, rarePerH));
        m_rowLabelValueMineralExotic.SetValue(FormatMineralText(exotic, exoticPerH));
        m_rowLabelValueMineralDark.SetValue(FormatMineralText(dark, darkPerH));
    }

    // "{누적량}({시간당}/h)" 형식 문자열 생성
    private string FormatMineralText(float accumulated, float perHour)
    {
        return $"{CommonUtility.FormatBigNumber(accumulated)}({CommonUtility.FormatBigNumber(perHour)}/h)";
    }

    // 존 진입 시 웨이브UI+안전지역버튼 활성화, zone스크롤뷰는 상시 표시
    private void SetExplorationUI(bool isSafeZone)
    {
        m_isInZone = !isSafeZone;
        if (m_waveContainer != null) m_waveContainer.SetActive(!isSafeZone);
    }

    // 웨이브 스크롤뷰에 아이템 배치 (풀에서 꺼내 재사용)
    private void PopulateWaveScrollView(int totalWaves)
    {
        ClearWaveScrollView();
        if (m_scrollViewWaveContent == null || m_scrollViewWaveItem == null) return;

        for (int i = 0; i < totalWaves; i++)
        {
            ScrollViewWaveItem waveItem;
            if (i < m_waveItemPool.Count)
            {
                waveItem = m_waveItemPool[i];
                waveItem.gameObject.SetActive(true);
            }
            else
            {
                var item = Instantiate(m_scrollViewWaveItem, m_scrollViewWaveContent);
                item.name = m_scrollViewWaveItem.name;
                waveItem = item.GetComponent<ScrollViewWaveItem>();
                m_waveItemPool.Add(waveItem);
            }
            waveItem.InitializeScrollViewWaveItem(i);
            m_waveItemActive.Add(waveItem);
        }

        // 첫 웨이브 아이템을 중앙에 배치
        if (m_waveAutoCenter != null && m_waveItemActive.Count > 0)
            m_waveAutoCenter.CenterOnChild((RectTransform)m_waveItemActive[0].transform);
    }

    // 활성 아이템 비활성화, 풀에 유지
    private void ClearWaveScrollView()
    {
        for (int i = 0; i < m_waveItemActive.Count; i++)
            m_waveItemActive[i].gameObject.SetActive(false);
        m_waveItemActive.Clear();
    }

    // ObjectManager 웨이브 이벤트 핸들러 (1-based currentWave)
    private void OnWaveStarted(int currentWave, int totalWaves)
    {
        for (int i = 0; i < m_waveItemActive.Count; i++)
        {
            if (i < currentWave - 1)
                m_waveItemActive[i].SetState(EWaveState.Cleared);
            else if (i == currentWave - 1)
                m_waveItemActive[i].SetState(EWaveState.InProgress);
        }

        // 현재 진행 중인 웨이브를 중앙에 배치
        int inProgressIndex = currentWave - 1;
        if (m_waveAutoCenter != null && inProgressIndex >= 0 && inProgressIndex < m_waveItemActive.Count)
            m_waveAutoCenter.CenterOnChild((RectTransform)m_waveItemActive[inProgressIndex].transform);
    }

    private void OnEnterZoneZeroClicked()
    {
        // Zone-0: 안전지역 (0번 인덱스)
        ZoneConfig zoneConfig = m_datatableZone.GetZone(0);
        if (zoneConfig == null) return;

        m_myFleet.StartFleetWarp(zoneConfig.skyboxMaterial, () =>
        {
            ClearWaveScrollView();
            SetExplorationUI(true);
        });
    }

    // UI 버튼 콜백 → 존 안에서는 차단
    private void OnTryZoneClicked(ZoneConfig zone)
    {
        if (m_isInZone) return;
        EnterZone(zone);
    }

    // 실제 존 진입 로직 (자동 탐사에서도 직접 호출)
    private void EnterZone(ZoneConfig zone)
    {
        SetExplorationUI(false);

        PopulateWaveScrollView(zone.TotalWaveCount);

        EventManager.Subscribe_WaveStarted(OnWaveStarted);

        m_myFleet.StartFleetWarp(zone.skyboxMaterial, () =>
        {
            ObjectManager.Instance.StartSpawnEnemies(zone, (isVictory) =>
            {
                // 전투 완료 시 마지막 웨이브도 클리어 표시
                for (int i = 0; i < m_waveItemActive.Count; i++)
                    m_waveItemActive[i].SetState(EWaveState.Cleared);

                EventManager.Unsubscribe_WaveStarted(OnWaveStarted);
                OnZoneBattleComplete(zone.zoneName, isVictory);
            });
        });
    }

    // 전투 클리어 시 호출 (전투 시스템에서 호출)
    public void OnZoneBattleComplete(string zoneName, bool isVictory)
    {
        if (!isVictory) return;

        var request = new ZoneClearRequest { zoneName = zoneName };
        NetworkManager.Instance.ClearZone(request, OnZoneClearResponse);
    }

    private void OnZoneClearResponse(ApiResponse<ZoneClearResponse> response)
    {
        if (response.errorCode != 0) return;
        
        // CharacterInfo 업데이트
        var character = DataManager.Instance.m_currentCharacter;
        if (character != null)
        {
            character.m_characterInfo.clearedZone = response.data.clearedZone;
            character.m_characterInfo.collectDateTime = response.data.collectDateTime;

            // 보상 처리
            if (response.data.rewardInfo != null)
            {
                character.UpdateMineral(response.data.rewardInfo.remainMineral);
                character.UpdateMineralRare(response.data.rewardInfo.remainMineralRare);
                character.UpdateMineralExotic(response.data.rewardInfo.remainMineralExotic);
                character.UpdateMineralDark(response.data.rewardInfo.remainMineralDark);
            }
        }

        // 스크롤뷰 갱신 (그룹 완료 시 다음 그룹으로 전환)
        PopulateZoneScrollView();

        UpdateZoneInfo();

        string clearedZoneName = character.m_characterInfo.clearedZone;
        ZoneConfig nextZone = string.IsNullOrEmpty(clearedZoneName) ? null : m_datatableZone.GetNextZone(clearedZoneName);

        // 자동 탐사: 다음 존이 있으면 바로 진입 (가드 우회)
        if (m_isAutoExploration && nextZone != null)
        {
            EnterZone(nextZone);
        }
        else
        {
            OnEnterZoneZeroClicked();
        }

        var pp = WarpPostProcessing.Instance;
        if (pp != null && nextZone != null)
        {
            pp.SetSkyboxBlendTarget(nextZone.skyboxMaterial);
        }
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

