//------------------------------------------------------------------------------
using System;
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
        LoadCharacterDataFromPlayerPrefs();
        LoadFleetDataFromPlayerPrefs();
    }

    public void Initialize()
    {
        LoadDataTableModule();
        LoadDataTableModuleResearch();
        LoadDataTableConfig();
        LoadCharacterDataFromPlayerPrefs();
        LoadFleetDataFromPlayerPrefs();
    }
    #endregion

    #region Character Data Management ###########################################################
    private const string CHARACTER_DATA_KEY = "CurrentCharacterData";
    public Character m_currentCharacter;

    public void SetCharacterData(CharacterInfo characterInfo)
    {
        if (m_currentCharacter == null)
            m_currentCharacter = new Character(characterInfo);
        
        m_currentCharacter.UpdateCharacterInfo(characterInfo);

        SaveCharacterDataToPlayerPrefs();
    }

    public void RestoreCurrentCharacterData()
    {
        if (m_currentCharacter == null)
        {
            LoadCharacterDataFromPlayerPrefs();
            if (m_currentCharacter == null)
            {
                var defaultCharacterInfo = new CharacterInfo
                {
                    characterName = "DefaultCharacter"
                    , techLevel = 1
                    , mineral = 0
                    , mineralRare = 0
                    , mineralExotic = 0
                    , mineralDark = 0                                        
                };
                m_currentCharacter = new Character(defaultCharacterInfo);
                SaveCharacterDataToPlayerPrefs();
            }
        }
    }

    private void SaveCharacterDataToPlayerPrefs()
    {
        if (m_currentCharacter != null)
        {
            string json = JsonConvert.SerializeObject(m_currentCharacter.GetInfo());
            PlayerPrefs.SetString(CHARACTER_DATA_KEY, json);
            PlayerPrefs.Save();
        }
        else
        {
            PlayerPrefs.DeleteKey(CHARACTER_DATA_KEY);
            PlayerPrefs.Save();
        }
    }

    public void LoadCharacterDataFromPlayerPrefs()
    {
        if (PlayerPrefs.HasKey(CHARACTER_DATA_KEY))
        {
            string json = PlayerPrefs.GetString(CHARACTER_DATA_KEY);
            try
            {
                var characterInfo = JsonConvert.DeserializeObject<CharacterInfo>(json);
                m_currentCharacter = new Character(characterInfo);
            }
            // bk: checked)
            catch (Exception e)
            {
                Debug.LogError($"Failed to load character data from PlayerPrefs: {e.Message}");
                m_currentCharacter = null;
            }
        }
    }

    public void ClearCharacterData()
    {
        m_currentCharacter = null;
        PlayerPrefs.DeleteKey(CHARACTER_DATA_KEY);
        PlayerPrefs.Save();
    }
    #endregion

    #region Fleet Data Management ###############################################################
    private const string FLEET_DATA_KEY = "CurrentFleetData";
    public FleetInfo m_currentFleetInfo;

    public void SetFleetData(FleetInfo fleetInfo)
    {
        m_currentFleetInfo = fleetInfo;
        SaveFleetDataToPlayerPrefs();
    }

    public void RestoreCurrentFleetInfo()
    {
        if (m_currentFleetInfo != null) return;
        LoadFleetDataFromPlayerPrefs();
        if (m_currentFleetInfo != null) return;
        var defaultFleetInfo = new FleetInfo
        {
            id = 0,
            characterId = 0,
            fleetName = "DefaultFleet",
            description = "Default Fleet",
            isActive = true,
            formation = EFormationType.LinearHorizontal,
            ships = new ShipInfo[]
            {
                new ShipInfo
                {
                    id = 0,
                    fleetId = 0,
                    shipName = "DefaultShip",
                    positionIndex = 0,
                    description = "Default Ship",
                    bodies = new ModuleBodyInfo[]
                    {
                        new ModuleBodyInfo
                        {
                            moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Body, EModuleSubType.Body_Battle, EModuleStyle.None),
                            moduleLevel = 1,
                            bodyIndex = 0,
                            engines = new ModuleInfo[]
                            {
                                new ModuleInfo { moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Engine, EModuleSubType.Engine_Standard, EModuleStyle.None), moduleLevel = 1, bodyIndex = 0, slotIndex = 0 }
                            },
                            weapons = new ModuleInfo[]
                            {
                                new ModuleInfo { moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Weapon, EModuleSubType.Weapon_Beam, EModuleStyle.None), moduleLevel = 1, bodyIndex = 0, slotIndex = 0 }
                            }
                        }
                    }
                }
            }
        };
        m_currentFleetInfo = defaultFleetInfo;
        SaveFleetDataToPlayerPrefs();
    }

    private void SaveFleetDataToPlayerPrefs()
    {
        if (m_currentFleetInfo != null)
        {
            string json = JsonConvert.SerializeObject(m_currentFleetInfo);
            PlayerPrefs.SetString(FLEET_DATA_KEY, json);
            PlayerPrefs.Save();
            Debug.Log("Fleet data saved to PlayerPrefs");
        }
        else
        {
            PlayerPrefs.DeleteKey(FLEET_DATA_KEY);
            PlayerPrefs.Save();
            Debug.Log("Fleet data cleared from PlayerPrefs");
        }
    }

    public void LoadFleetDataFromPlayerPrefs()
    {
        if (PlayerPrefs.HasKey(FLEET_DATA_KEY))
        {
            string json = PlayerPrefs.GetString(FLEET_DATA_KEY);
            try
            {
                m_currentFleetInfo = JsonConvert.DeserializeObject<FleetInfo>(json);
            }
            // bk: checked)
            catch (Exception e)
            {
                Debug.LogError($"Failed to load fleet data from PlayerPrefs: {e.Message}");
                Debug.LogWarning("Clearing outdated fleet data from PlayerPrefs");
                PlayerPrefs.DeleteKey(FLEET_DATA_KEY);
                PlayerPrefs.Save();
                m_currentFleetInfo = null;
            }
        }
    }

    public void ClearFleetData()
    {
        m_currentFleetInfo = null;
        PlayerPrefs.DeleteKey(FLEET_DATA_KEY);
        PlayerPrefs.Save();
        Debug.Log("Fleet data cleared");
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
        return m_currentFleetInfo?.ships?.Length ?? 0;
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
        {
            Debug.LogError("DataTableModule is not exist");
        }
        else
        {
            Debug.Log("DataTableModule loaded successfully");
        }
    }

    // 서버 데이터를 기반으로 완전한 모듈 데이터 복원
    public ModuleData RestoreModuleData(EModuleSubType subType, int moduleLevel)
    {
        if (m_dataTableModule == null) return null;
        return m_dataTableModule.GetModuleDataFromTable(subType, moduleLevel);
    }

    public ModuleData RestoreModuleData(int moduleTypePacked, int moduleLevel)
    {
        if (m_dataTableModule == null) return null;

        EModuleSubType subType = CommonUtility.GetModuleSubType(moduleTypePacked);
        if (subType == EModuleSubType.None) return null;

        return RestoreModuleData(subType, moduleLevel);
    }

    public bool GetModuleUpgradeCost(int moduleTypePacked, int moduleLevel, out CostStruct cost)
    {
        cost = new CostStruct();
        ModuleData moduleData = RestoreModuleData(moduleTypePacked, moduleLevel);
        if (moduleData == null) return false;

        cost = moduleData.m_upgradeCost;
        return true;
    }
    #endregion

    #region Data Table Module Research ###############################################################
    public DataTableModuleResearch m_dataTableModuleResearch;

    private void LoadDataTableModuleResearch()
    {
        m_dataTableModuleResearch = Resources.Load<DataTableModuleResearch>("DataTable/DataTableModuleResearch");
        if (m_dataTableModuleResearch == null)
        {
            Debug.LogError("DataTableModuleResearch is not exist");
        }
        else
        {
            Debug.Log("DataTableModuleResearch loaded successfully");
        }
    }

    public CostStruct GetModuleResearchCost(EModuleSubType subType)
    {
        if (m_dataTableModuleResearch == null) return new CostStruct();
        return m_dataTableModuleResearch.GetResearchCost(subType);
    }

    public CostStruct GetModuleResearchCost(int moduleTypePacked)
    {
        EModuleSubType subType = CommonUtility.GetModuleSubType(moduleTypePacked);
        if (subType == EModuleSubType.None) return new CostStruct();

        return GetModuleResearchCost(subType);
    }
    #endregion

}