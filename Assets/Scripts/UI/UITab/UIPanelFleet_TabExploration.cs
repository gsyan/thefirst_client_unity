using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelFleet_TabExploration : UITabBase
{
    [SerializeField] private TextMeshProUGUI m_textTop;
    [SerializeField] private TextMeshProUGUI m_textCollectMineral;
    [SerializeField] private TextMeshProUGUI m_textCollectMineralRare;
    [SerializeField] private TextMeshProUGUI m_textCollectMineralExotic;
    [SerializeField] private TextMeshProUGUI m_textCollectMineralDark;
    [SerializeField] private Button m_collectMineralButton;
    [SerializeField] private Button m_safeZoneButton;
    [SerializeField] private Button m_tryZoneButton;
    [SerializeField] private TextMeshProUGUI m_textTryZoneButton;
    [SerializeField] private DataTableZone m_datatableZone;     // Zone 설정 ScriptableObject

    private SpaceFleet m_myFleet;
    private Character m_myCharacter;
    private ZoneConfig m_clearedZone;
    private ZoneConfig m_nextZone;

    private Coroutine m_mineralUpdateCoroutine;
    private readonly WaitForSeconds m_updateInterval = new WaitForSeconds(1f);

    public override void InitializeUITab()
    {
        if (m_textTop != null)
             m_textTop.text = "Exploration Battle";

        m_myCharacter = DataManager.Instance.m_currentCharacter;
        if (m_myCharacter == null || m_myCharacter.GetOwnedFleet() == null) return;
        m_myFleet = m_myCharacter.GetOwnedFleet();

        m_collectMineralButton.onClick.AddListener(OnCollectZoneClicked);
        m_safeZoneButton.onClick.AddListener(OnEnterZoneZeroClicked);
        m_tryZoneButton.onClick.AddListener(OnTryZoneClicked);

        // 초기 상태: 평화 상태로 시작 (tryZone 버튼 표시)
        SetZoneButtonState(false);

        UpdateZoneInfo();
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
            m_nextZone = m_datatableZone.GetZone(1);

            SetMineralTexts(0, 0, 0, 0, 0, 0, 0, 0);
        }
        else
        {
            m_nextZone = m_datatableZone.GetNextZone(clearedZoneName);            
            
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
        // try zone button text
        m_textTryZoneButton.text = $"Try {m_nextZone.zoneName}";
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
        m_textCollectMineral.text = FormatMineralText(mineral, mineralPerH);
        m_textCollectMineralRare.text = FormatMineralText(rare, rarePerH);
        m_textCollectMineralExotic.text = FormatMineralText(exotic, exoticPerH);
        m_textCollectMineralDark.text = FormatMineralText(dark, darkPerH);
    }

    // "{누적량}({시간당}/h)" 형식 문자열 생성
    private string FormatMineralText(float accumulated, float perHour)
    {
        return $"{(int)accumulated}({(int)perHour}/h)";
    }

    // 버튼 표시 상태 변경 (isBattleMode: true=전투, false=평화)
    private void SetZoneButtonState(bool isBattleMode)
    {
        m_safeZoneButton.gameObject.SetActive(isBattleMode);
        m_tryZoneButton.gameObject.SetActive(!isBattleMode);
    }

    private void OnEnterZoneZeroClicked()
    {
        // Zone-0: 안전지역 (0번 인덱스)
        ZoneConfig zoneConfig = m_datatableZone.GetZone(0);
        if (zoneConfig == null) return;

        m_myFleet.StartFleetWarp(zoneConfig.skyboxMaterial, () =>
        {
            SetZoneButtonState(false); // 평화 상태로 전환
        });
    }

    private void OnTryZoneClicked()
    {
        SetZoneButtonState(true); // 전투 상태로 전환

        m_myFleet.StartFleetWarp(m_nextZone.skyboxMaterial, () =>
        {
            // ZoneConfig 기반으로 적 함대 생성, 전투 완료 시 콜백
            ObjectManager.Instance.StartSpawnEnemies(m_nextZone, (isVictory) =>
            {
                OnZoneBattleComplete(m_nextZone.zoneName, isVictory);
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
