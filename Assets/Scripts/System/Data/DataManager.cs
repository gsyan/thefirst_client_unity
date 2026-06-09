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
        LoadDataTableModuleResearch();
        LoadDataTableConfig();
        LoadDataTableZone();
        LoadDataTablePvpSeason();
        LoadColorPalette();
    }
    #endregion

    #region Account State Management ############################################################
    public bool m_isGoogleLinked;  // 구글 계정 연동 여부
    #endregion

    #region Character Info Management ###########################################################
    private const string CHARACTER_DATA_KEY = "CurrentCharacterData";
    public Character m_currentCharacter;

    // 서버에서 받은 캐릭터 정보 설정 — 로컬 저장 없음
    public void SetCharacterInfo(CharacterInfo characterInfo)
    {
        if (m_currentCharacter == null)
            m_currentCharacter = new Character(characterInfo);

        m_currentCharacter.UpdateCharacterInfo(characterInfo);
    }

    public void ClearCharacterData()
    {
        m_currentCharacter = null;
        PlayerPrefs.DeleteKey(CHARACTER_DATA_KEY);
        PlayerPrefs.Save();
    }
    #endregion

    #region Fleet Info Management ###############################################################
    private const string FLEET_DATA_KEY = "CurrentFleetData";
    public FleetInfo m_currentFleetInfo;

    // 초기화 전용 — 로그인/씬 진입 시 최초 1회만 사용
    public void SetFleetData(FleetInfo fleetInfo)
    {
        m_currentFleetInfo = fleetInfo;
    }

    // 서버 응답으로 함선 목록이 바뀌었을 때 (추가/제거)
    public void ApplyFleetShips(List<ShipInfo> ships)
    {
        if (m_currentFleetInfo == null) return;
        m_currentFleetInfo.ships = ships;
    }

    // 진형 변경 시
    public void ApplyFleetFormation(EFormationType formation)
    {
        if (m_currentFleetInfo == null) return;
        m_currentFleetInfo.formation = formation;
    }

    // 전술 옵션 변경 시
    public void ApplyFleetTacticOptions(int tacticOptions)
    {
        if (m_currentFleetInfo == null) return;
        m_currentFleetInfo.tacticOptions = tacticOptions;
    }

    public void ClearFleetData()
    {
        m_currentFleetInfo = null;
        PlayerPrefs.DeleteKey(FLEET_DATA_KEY);
        PlayerPrefs.Save();
    }

    public ShipInfo GetShipAtPosition(int positionIndex)
    {
        if (m_currentFleetInfo?.ships == null) return null;

        foreach (var shipInfo in m_currentFleetInfo.ships)
        {
            if (shipInfo.positionIndex == positionIndex)
                return shipInfo;
        }
        return null;
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
        m_dataTableConfig = Resources.Load<DataTableConfig>("DataTable/DataTableConfig");
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
        m_dataTableModule = Resources.Load<DataTableModule>("DataTable/DataTableModule");
        if (m_dataTableModule == null)
            Debug.LogError("DataTableModule is not exist");
    }

    public bool GetModuleLevelUpCost(EModuleSubType subType, int moduleLevel, out long modulePointCost)
    {
        modulePointCost = 0;
        ModuleData moduleData = m_dataTableModule.GetModuleDataFromTable(subType, moduleLevel + 1);
        if (moduleData == null) return false;

        modulePointCost = moduleData.modulePointCost;
        return true;
    }

    // level+1 데이터가 없으면 현재 레벨이 최대
    public int GetMaxModuleLevel(EModuleSubType subType)
    {
        int level = 1;
        while (m_dataTableModule.GetModuleDataFromTable(subType, level + 1) != null)
            level++;
        return level;
    }
    #endregion

    #region Data Table Module Research ###############################################################
    public DataTableResearch m_dataTableResearch;

    private void LoadDataTableModuleResearch()
    {
        m_dataTableResearch = Resources.Load<DataTableResearch>("DataTable/DataTableResearch");
        if (m_dataTableResearch == null)
        {
            Debug.LogError("DataTableResearch is not exist");
        }
        else
        {
            Debug.Log("DataTableResearch loaded successfully");
        }
    }

    public long GetModuleResearchCost(EModuleSubType subType)
    {
        if (m_dataTableResearch == null) return 0;
        return m_dataTableResearch.GetResearchCost(subType);
    }
    #endregion

    #region Data Table Zone ###############################################################
    public DataTableZone m_dataTableZone;

    private void LoadDataTableZone()
    {
        m_dataTableZone = Resources.Load<DataTableZone>("DataTable/DataTableZone");
        if (m_dataTableZone == null)
            Debug.LogError("DataTableZone is not exist");
    }
    #endregion

    #region Data Table PvpSeason ###############################################################
    public DataTablePvpSeason m_dataTablePvpSeason;

    private void LoadDataTablePvpSeason()
    {
        m_dataTablePvpSeason = Resources.Load<DataTablePvpSeason>("DataTable/DataTablePvpSeason");
        if (m_dataTablePvpSeason == null)
            Debug.LogError("DataTablePvpSeason is not exist");
    }
    #endregion

    #region ColorPalette ###############################################################
    public ColorPalette m_colorPalette;

    private void LoadColorPalette()
    {
        m_colorPalette = Resources.Load<ColorPalette>("DataTable/ColorPalette");
        if (m_colorPalette == null)
            Debug.LogError("ColorPalette is not exist");
    }
    #endregion

}