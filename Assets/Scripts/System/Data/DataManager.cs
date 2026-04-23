// 게임 전반의 런타임 데이터(캐릭터, 함대, 계정 상태, 데이터 테이블)를 관리하는 싱글톤
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

    // 서버에서 받은 함대 정보 설정 — 로컬 저장 없음
    public void SetFleetData(FleetInfo fleetInfo)
    {
        m_currentFleetInfo = fleetInfo;
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

    public bool GetModuleLevelUpCost(EModuleSubType subType, int moduleLevel, out long mineralCost)
    {
        mineralCost = 0;
        ModuleData moduleData = m_dataTableModule.GetModuleDataFromTable(subType, moduleLevel);
        if (moduleData == null) return false;

        mineralCost = moduleData.mineralCost;
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

}