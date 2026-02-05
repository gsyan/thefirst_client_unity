using TMPro;
using UnityEngine;
using UnityEngine.UI;




public class UIPanelExploration_TabBattle : UITabBase
{
    [SerializeField] private TextMeshProUGUI m_textTop;
    [SerializeField] private Button m_safeZoneButton;
    [SerializeField] private RectTransform m_scrollViewZoneContent;
    [SerializeField] private GameObject m_scrollViewZoneItem;       // 프리팹
    [SerializeField] private DataTableZone m_datatableZone;     // Zone 설정 ScriptableObject

    [HideInInspector] public SpaceFleet m_myFleet;
    private ScrollViewZoneItem m_selectedScrollViewZoneItem;    // 현재 선택된 스크롤 뷰 아이템

    public override void InitializeUITab()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null || character.GetOwnedFleet() == null) return;
        m_myFleet = character.GetOwnedFleet();

        m_safeZoneButton.onClick.AddListener(OnEnterZoneZeroClicked);

        if (m_scrollViewZoneContent == null || m_scrollViewZoneItem == null || m_datatableZone == null) return;

        string clearedZoneName = character.m_characterInfo.clearedZone;

        // 클리어한 zone 표시 (있으면)
        if (!string.IsNullOrEmpty(clearedZoneName))
        {
            ZoneConfig clearedConfig = m_datatableZone.GetZoneByName(clearedZoneName);
            if (clearedConfig != null)
                CreateZoneItem(clearedConfig, isCleared: true, isNextChallenge: false);
        }

        // 다음 zone 표시 (0번은 안전지역이므로 1번부터 시작)
        ZoneConfig nextConfig = string.IsNullOrEmpty(clearedZoneName)
            ? m_datatableZone.GetZone(1)  // 클리어한 게 없으면 1번 zone (0번은 안전지역)
            : m_datatableZone.GetNextZone(clearedZoneName);

        if (nextConfig != null)
            CreateZoneItem(nextConfig, isCleared: false, isNextChallenge: true);
    }

    private void CreateZoneItem(ZoneConfig zoneConfig, bool isCleared, bool isNextChallenge)
    {
        GameObject item = Instantiate(m_scrollViewZoneItem, m_scrollViewZoneContent);
        if (item == null) return;

        item.name = m_scrollViewZoneItem.name;
        ScrollViewZoneItem scrollViewItem = item.GetComponent<ScrollViewZoneItem>();
        scrollViewItem.InitializeScrollViewZoneItem(
            zoneConfig,
            () => OnZoneItemSelected(scrollViewItem, zoneConfig),
            () => OnEnterZoneClicked(zoneConfig),
            () => OnCollectZoneClicked(zoneConfig),
            isCleared,
            isNextChallenge
        );
    }

    public override void OnTabActivated()
    {
        InitializeUI();

        //EventManager.Subscribe_FleetChange(OnFleetChanged);

        // if (m_selectedShip == null)
        //     SelectShip(m_myFleet.GetFirstAliveShip());

        // if (m_selectedShip != null)
        // {
        //     m_selectedShip.m_shipOutline.enabled = true;
        //     CameraController.Instance.SetTargetOfCameraController(m_selectedShip.transform);
        // }
    }

    public override void OnTabDeactivated()
    {
        InitializeUI();

        //EventManager.Unsubscribe_FleetChange(OnFleetChanged);

        // if (m_selectedShip != null)
        //     m_selectedShip.m_shipOutline.enabled = false;
    }

    private void InitializeUI()
    {
        if (m_myFleet != null)
            m_myFleet.ClearAllSelectedModule();
        

        if (m_textTop != null)
             m_textTop.text = "Exploration";

        // UpdateFleetStatsDisplay();
        // UpdateShipStatsDisplay();
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

    private void OnZoneItemSelected(ScrollViewZoneItem selectedItem, ZoneConfig zoneConfig)
    {
        if (selectedItem == null || zoneConfig == null) return;
        if (selectedItem == m_selectedScrollViewZoneItem) return;

        // 이전에 선택된 아이템의 관리 버튼 숨김
        // if (m_selectedScrollViewZoneItem != null && m_selectedScrollViewZoneItem != selectedItem)
        //     m_selectedScrollViewZoneItem.SetSelected_ScrollViewZoneItem(false);

        // 선택 스크롤 뷰 아이템 업데이트
        m_selectedScrollViewZoneItem = selectedItem;
        //m_selectedScrollViewZoneItem.SetSelected_ScrollViewZoneItem(true);

        // TODO: Zone 상세 정보 표시 (Wave 정보, 적 함대 구성 등)
    }

    private void OnEnterZoneClicked(ZoneConfig zoneConfig)
    {
        m_myFleet.StartFleetWarp(zoneConfig.skyboxMaterial, () =>
        {
            // ZoneConfig 기반으로 적 함대 생성, 전투 완료 시 콜백
            ObjectManager.Instance.StartSpawnEnemies(zoneConfig, (isVictory) =>
            {
                OnZoneBattleComplete(zoneConfig.zoneName, isVictory);
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
        if (response.errorCode != 0)
        {
            Debug.LogError($"Zone clear failed: {response.errorCode}");
            return;
        }

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

        // Zone 목록 갱신
        RefreshZoneList();

        // zone zero 로 이동
        OnEnterZoneZeroClicked();

        Debug.Log($"Zone cleared! New clearedZone: {response.data.clearedZone}");
    }

    private void OnCollectZoneClicked(ZoneConfig zoneConfig)
    {
        var request = new ZoneCollectRequest {};
        NetworkManager.Instance.CollectZone(request, OnZoneCollectResponse);
    }

    private void OnZoneCollectResponse(ApiResponse<ZoneCollectResponse> response)
    {
        if (response.errorCode != 0)
        {
            Debug.LogError($"Zone collect failed: {response.errorCode}");
            return;
        }

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

        Debug.Log($"Zone collected! New collectDateTime: {response.data.collectDateTime}");
    }

    // Zone 목록 갱신
    private void RefreshZoneList()
    {
        // 기존 아이템 삭제
        foreach (Transform child in m_scrollViewZoneContent)
            Destroy(child.gameObject);

        m_selectedScrollViewZoneItem = null;

        // 다시 초기화
        InitializeUITab();
    }
}
