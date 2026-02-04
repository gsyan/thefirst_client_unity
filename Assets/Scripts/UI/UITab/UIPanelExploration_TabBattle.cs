using TMPro;
using UnityEngine;
using UnityEngine.UI;




public class UIPanelExploration_TabBattle : UITabBase
{
    [SerializeField] private TextMeshProUGUI m_textTop;
    [SerializeField] private Button m_safeZoneButton;
    [SerializeField] private RectTransform m_scrollViewZoneContent;
    [SerializeField] private GameObject m_scrollViewZoneItem;       // 프리팹
    [SerializeField] private ZoneEnemyFleetConfig m_zoneConfig;     // Zone 설정 ScriptableObject

    [HideInInspector] public SpaceFleet m_myFleet;
    private ScrollViewZoneItem m_selectedScrollViewZoneItem;    // 현재 선택된 스크롤 뷰 아이템

    public override void InitializeUITab()
    {
        var character = DataManager.Instance.m_currentCharacter;
        if (character == null || character.GetOwnedFleet() == null) return;
        m_myFleet = character.GetOwnedFleet();
        if (m_scrollViewZoneContent == null || m_scrollViewZoneItem == null || m_zoneConfig == null) return;

        string clearedZoneName = character.m_characterInfo.clearedZone;

        // 클리어한 zone 표시 (있으면)
        if (!string.IsNullOrEmpty(clearedZoneName))
        {
            ZoneConfig clearedConfig = m_zoneConfig.GetZoneByName(clearedZoneName);
            if (clearedConfig != null)
                CreateZoneItem(clearedConfig, isCleared: true, isNextChallenge: false);
        }

        // 다음 zone 표시
        ZoneConfig nextConfig = string.IsNullOrEmpty(clearedZoneName)
            ? m_zoneConfig.GetZone(0)  // 클리어한 게 없으면 첫 번째 zone
            : m_zoneConfig.GetNextZone(clearedZoneName);

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
             m_textTop.text = "Exploration Battle";

        // UpdateFleetStatsDisplay();
        // UpdateShipStatsDisplay();
    }


    private void OnZoneItemSelected(ScrollViewZoneItem selectedItem, ZoneConfig zoneConfig)
    {
        if (selectedItem == null || zoneConfig == null) return;
        if (selectedItem == m_selectedScrollViewZoneItem) return;

        // 이전에 선택된 아이템의 관리 버튼 숨김
        if (m_selectedScrollViewZoneItem != null && m_selectedScrollViewZoneItem != selectedItem)
            m_selectedScrollViewZoneItem.SetSelected_ScrollViewZoneItem(false);

        // 선택 스크롤 뷰 아이템 업데이트
        m_selectedScrollViewZoneItem = selectedItem;
        m_selectedScrollViewZoneItem.SetSelected_ScrollViewZoneItem(true);

        // TODO: Zone 상세 정보 표시 (Wave 정보, 적 함대 구성 등)
    }

    private void OnEnterZoneClicked(ZoneConfig zoneConfig)
    {
        // TODO: 해당 Zone으로 진입, zoneConfig를 기반으로 적 함대 생성
        // 전투 완료 후 OnZoneBattleComplete 호출

        //ObjectManager.Instance.StartSpawnEnemies();
        m_myFleet.StartFleetWarp();
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

            // 보상 처리
            if (response.data.rewardInfo != null)
            {
                character.m_characterInfo.mineral = response.data.rewardInfo.remainMineral;
                character.m_characterInfo.mineralRare = response.data.rewardInfo.remainMineralRare;
                character.m_characterInfo.mineralExotic = response.data.rewardInfo.remainMineralExotic;
                character.m_characterInfo.mineralDark = response.data.rewardInfo.remainMineralDark;
            }
        }

        // Zone 목록 갱신
        RefreshZoneList();

        Debug.Log($"Zone cleared! New clearedZone: {response.data.clearedZone}");
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
