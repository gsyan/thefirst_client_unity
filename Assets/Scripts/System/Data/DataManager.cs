// 게임 전반의 런타임 데이터(캐릭터, 함대, 계정 상태, 데이터 테이블)를 관리하는 싱글톤
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class DataManager : Singleton<DataManager>
{
    #region Initialization #####################################################################
    protected override void OnInitialize()
    {
        LoadDataTableModule();
        LoadDataTableCommander();
        LoadDataTableConfig();
        LoadDataTableZone();
        LoadDataTableShipPreset();
        LoadDataTableRewardCard();
        LoadDataTablePvpSeason();
        LoadDataTableDailyBonus();
        LoadColorPalette();
    }
    #endregion

    #region Account State Management ############################################################
    public bool m_isGoogleLinked;  // 구글 계정 연동 여부
    #endregion

    #region Commander Info Management ###########################################################
    public Commander m_currentCommander;

    // 서버에서 받은 커맨더 정보 설정 — 로컬 저장 없음
    public void SetCommanderInfo(CommanderInfo commanderInfo)
    {
        if (m_currentCommander == null)
            m_currentCommander = new Commander(commanderInfo);

        m_currentCommander.UpdateCommanderInfo(commanderInfo);

        // 탐험 함대 편성 — 지휘력 최대치는 서버 값, 프리셋 카탈로그/모듈 테이블은 로그인 이전(OnInitialize)에 이미 로드되어 있음
        m_currentFleetComposition = new FleetComposition(commanderInfo.commandPowerMax, m_dataTableShipPreset.BuildLookupTable(), m_dataTableModule);
        SeedFleetCompositionFromFleetInfoIfNeeded();
    }

    public void ClearCommanderData()
    {
        m_currentCommander = null;
        m_currentFleetComposition = null;
    }
    #endregion

    #region Fleet Composition Management #########################################################
    public FleetComposition m_currentFleetComposition;

    // SetCommanderInfo(FleetComposition 생성)와 SetFleetData(함대 정보 세팅) 호출 순서가 호출부마다 달라
    // (UIMain은 Fleet→Commander, SpaceSceneDebugBootstrap은 Commander→Fleet 순) 어느 쪽이 먼저 와도 안전하게 시딩되도록
    // 양쪽 호출부에서 이 메서드를 부름. 이미 배치된 함선이 있으면(한 번 시딩됐거나 사용자가 직접 편집한 상태) 건드리지 않음
    private void SeedFleetCompositionFromFleetInfoIfNeeded()
    {
        if (m_currentFleetComposition == null) return;
        if (m_currentFleetInfo == null || m_currentFleetInfo.ships == null) return;
        if (m_currentFleetComposition.GetPlacedShips().Count > 0) return;

        for (int i = 0; i < m_currentFleetInfo.ships.Count; i++)
        {
            ShipInfo shipInfo = m_currentFleetInfo.ships[i];
            ModuleBodyInfo modules = shipInfo.bodies != null && shipInfo.bodies.Count > 0 ? shipInfo.bodies[0] : null;
            m_currentFleetComposition.TryPlaceShip(shipInfo.shipPresetId, shipInfo.isFront, modules);
        }
    }
    #endregion

    #region Fleet Info Management ###############################################################
    public FleetInfo m_currentFleetInfo;

    // 초기화 전용 — 로그인/씬 진입 시 최초 1회만 사용
    public void SetFleetData(FleetInfo fleetInfo)
    {
        m_currentFleetInfo = fleetInfo;
        SeedFleetCompositionFromFleetInfoIfNeeded();
    }

    public void ClearFleetData()
    {
        m_currentFleetInfo = null;
    }

    public int GetShipCount()
    {
        return m_currentFleetInfo?.ships?.Count ?? 0;
    }
    #endregion

    #region Data Table Config ###############################################################
    public DataTableConfig m_dataTableConfig;

    private void LoadDataTableConfig()
    {
        m_dataTableConfig = ResourceManager.Instance.Load<DataTableConfig>("DataTable/DataTableConfig");
        if (m_dataTableConfig == null)
        {
            Debug.LogError("DataTableConfig is not exist");
        }
        else
        {
            Debug.Log("DataTableConfig loaded successfully");
        }
    }

    public void ApplyGameSettings()
    {
        // 게임 설정 적용 로직
    }

    public string GetGameVersion()
    {
        return m_dataTableConfig?.gameSettings?.version ?? "1.0.0";
    }

    #endregion


    #region Data Table Module ###############################################################
    public DataTableModule m_dataTableModule;

    private void LoadDataTableModule()
    {
        m_dataTableModule = ResourceManager.Instance.Load<DataTableModule>("DataTable/DataTableModule");
        if (m_dataTableModule == null)
            Debug.LogError("DataTableModule is not exist");
    }

    #endregion

    #region Data Table Commander ###########################################################
    public DataTableCommander m_dataTableCommander;

    private void LoadDataTableCommander()
    {
        m_dataTableCommander = ResourceManager.Instance.Load<DataTableCommander>("DataTable/DataTableCommander");
        if (m_dataTableCommander == null)
            Debug.LogError("DataTableCommander is not exist");
        else
            Debug.Log("DataTableCommander loaded successfully");
    }
    #endregion

    #region Data Table Zone ###############################################################
    public DataTableZone m_dataTableZone;

    private void LoadDataTableZone()
    {
        m_dataTableZone = ResourceManager.Instance.Load<DataTableZone>("DataTable/DataTableZone");
        if (m_dataTableZone == null)
            Debug.LogError("DataTableZone is not exist");
    }
    #endregion

    #region Data Table Ship Preset ###############################################################
    public DataTableShipPreset m_dataTableShipPreset;

    private void LoadDataTableShipPreset()
    {
        m_dataTableShipPreset = ResourceManager.Instance.Load<DataTableShipPreset>("DataTable/DataTableShipPreset");
        if (m_dataTableShipPreset == null)
            Debug.LogError("DataTableShipPreset is not exist");
    }
    #endregion

    #region Data Table Reward Card ###############################################################
    public DataTableRewardCard m_dataTableRewardCard;

    private void LoadDataTableRewardCard()
    {
        m_dataTableRewardCard = ResourceManager.Instance.Load<DataTableRewardCard>("DataTable/DataTableRewardCard");
        if (m_dataTableRewardCard == null)
            Debug.LogError("DataTableRewardCard is not exist");
    }
    #endregion

    #region Data Table PvpSeason ###############################################################
    public DataTablePvpSeason m_dataTablePvpSeason;

    private void LoadDataTablePvpSeason()
    {
        m_dataTablePvpSeason = ResourceManager.Instance.Load<DataTablePvpSeason>("DataTable/DataTablePvpSeason");
        if (m_dataTablePvpSeason == null)
            Debug.LogError("DataTablePvpSeason is not exist");
    }
    #endregion

    #region Data Table DailyBonus ###############################################################
    public DataTableDailyBonus m_dataTableDailyBonus;

    private void LoadDataTableDailyBonus()
    {
        m_dataTableDailyBonus = ResourceManager.Instance.Load<DataTableDailyBonus>("DataTable/DataTableDailyBonus");
        if (m_dataTableDailyBonus == null)
            Debug.LogError("DataTableDailyBonus is not exist");
    }
    #endregion

    #region ColorPalette ###############################################################
    public ColorPalette m_colorPalette;

    private void LoadColorPalette()
    {
        m_colorPalette = ResourceManager.Instance.Load<ColorPalette>("DataTable/ColorPalette");
        if (m_colorPalette == null)
            Debug.LogError("ColorPalette is not exist");
    }
    #endregion

}