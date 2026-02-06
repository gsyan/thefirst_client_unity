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
    [SerializeField] private RectTransform m_scrollViewZoneContent;
    [SerializeField] private GameObject m_scrollViewZoneItem;       // 프리팹
    [SerializeField] private Button m_safeZoneButton;
    [SerializeField] private Button m_collectMineralButton;
    [SerializeField] private DataTableZone m_datatableZone;         // Zone 설정 ScriptableObject

    private SpaceFleet m_myFleet;
    private Character m_myCharacter;
    private ZoneConfig m_clearedZone;
    
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
        m_safeZoneButton.onClick.AddListener(OnEnterZoneZeroClicked);
        // m_tryZoneButton.onClick.AddListener(OnTryZoneClicked);

        m_rowLabelValueMineral.SetLabel("Mineral:");
        m_rowLabelValueMineralRare.SetLabel("Mineral.R:");
        m_rowLabelValueMineralExotic.SetLabel("Mineral.E:");
        m_rowLabelValueMineralDark.SetLabel("Mineral.D:");

        PopulateZoneScrollView();
        UpdateZoneInfo();
    }

    // 현재 그룹의 zone 목록으로 스크롤뷰 채우기
    private void PopulateZoneScrollView()
    {
        if (m_scrollViewZoneContent == null || m_scrollViewZoneItem == null) return;
        if (m_datatableZone == null || m_myCharacter == null) return;

        // 기존 아이템 제거
        for (int i = m_scrollViewZoneContent.childCount - 1; i >= 0; i--)
            Destroy(m_scrollViewZoneContent.GetChild(i).gameObject);

        int targetGroup = GetCurrentZoneGroup();
        string prefix = targetGroup + "-";

        string clearedZoneName = m_myCharacter.m_characterInfo.clearedZone;
        int clearedIndex = string.IsNullOrEmpty(clearedZoneName)
            ? -1
            : m_datatableZone.GetZoneIndex(clearedZoneName);

        for (int i = 0; i < m_datatableZone.ZoneCount; i++)
        {
            ZoneConfig zone = m_datatableZone.GetZone(i);
            if (zone == null || !zone.zoneName.StartsWith(prefix)) continue;

            int zoneIndex = m_datatableZone.GetZoneIndex(zone.zoneName);
            bool isCleared = zoneIndex <= clearedIndex;

            GameObject item = Instantiate(m_scrollViewZoneItem, m_scrollViewZoneContent);
            if (item == null) continue;

            item.name = m_scrollViewZoneItem.name;
            ZoneConfig capturedZone = zone;
            ScrollViewZoneItem scrollViewItem = item.GetComponent<ScrollViewZoneItem>();
            scrollViewItem.InitializeScrollViewZoneItem(
                capturedZone,
                () => OnTryZoneClicked(capturedZone),
                isCleared
            );
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

    private void OnEnterZoneZeroClicked()
    {
        // Zone-0: 안전지역 (0번 인덱스)
        ZoneConfig zoneConfig = m_datatableZone.GetZone(0);
        if (zoneConfig == null) return;

        m_myFleet.StartFleetWarp(zoneConfig.skyboxMaterial, () =>
        {
            
        });
    }

    private void OnTryZoneClicked(ZoneConfig zone)
    {
        m_myFleet.StartFleetWarp(zone.skyboxMaterial, () =>
        {
            // ZoneConfig 기반으로 적 함대 생성, 전투 완료 시 콜백
            ObjectManager.Instance.StartSpawnEnemies(zone, (isVictory) =>
            {
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

        // zone zero 로 이동
        OnEnterZoneZeroClicked();
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

