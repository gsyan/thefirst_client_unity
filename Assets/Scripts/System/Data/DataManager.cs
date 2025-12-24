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
        LoadDataTableConfig();
        LoadCharacterDataFromPlayerPrefs();
        LoadFleetDataFromPlayerPrefs();
    }

    public void Initialize()
    {
        LoadDataTableModule();
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
            formation = "Linear",
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
                            moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Body, (int)EModuleBodySubType.Battle, EModuleStyle.None),
                            moduleLevel = 1,
                            bodyIndex = 0,
                            weapons = new ModuleWeaponInfo[]
                            {
                                new ModuleWeaponInfo { moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Weapon, (int)EModuleWeaponSubType.Beam, EModuleStyle.None), moduleLevel = 1, bodyIndex = 0, slotIndex = 0 }
                            },
                            engines = new ModuleEngineInfo[]
                            {
                                new ModuleEngineInfo { moduleTypePacked = CommonUtility.CreateModuleTypePacked(EModuleType.Engine, (int)EModuleEngineSubType.Standard, EModuleStyle.None), moduleLevel = 1, bodyIndex = 0, slotIndex = 0 }
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
                Debug.Log($"Fleet data loaded from PlayerPrefs: {m_currentFleetInfo?.fleetName}");
            }
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
    public ModuleBodyData RestoreBodyModuleData(EModuleBodySubType subType, int moduleLevel)
    {
        if (m_dataTableModule == null) return null;
        return m_dataTableModule.GetBodyModule(subType, moduleLevel);
    }

    public ModuleWeaponData RestoreWeaponModuleData(EModuleWeaponSubType subType, int moduleLevel)
    {
        if (m_dataTableModule == null) return null;
        return m_dataTableModule.GetWeaponModule(subType, moduleLevel);
    }

    public ModuleEngineData RestoreEngineModuleData(EModuleEngineSubType subType, int moduleLevel)
    {
        if (m_dataTableModule == null) return null;
        return m_dataTableModule.GetEngineModule(subType, moduleLevel);
    }

    public ModuleHangerData RestoreHangerModuleData(EModuleHangerSubType subType, int moduleLevel)
    {
        if (m_dataTableModule == null) return null;
        return m_dataTableModule.GetHangerModule(subType, moduleLevel);
    }

    public object RestoreModuleDataByType(int moduleType, int moduleLevel)
    {
        EModuleType type = CommonUtility.GetModuleType(moduleType);
        switch (type)
        {
            case EModuleType.Body:
                EModuleBodySubType bodySubType = CommonUtility.GetModuleSubType<EModuleBodySubType>(moduleType);
                return RestoreBodyModuleData(bodySubType, moduleLevel);
            case EModuleType.Weapon:
                EModuleWeaponSubType weaponSubType = CommonUtility.GetModuleSubType<EModuleWeaponSubType>(moduleType);
                return RestoreWeaponModuleData(weaponSubType, moduleLevel);
            case EModuleType.Engine:
                EModuleEngineSubType engineSubType = CommonUtility.GetModuleSubType<EModuleEngineSubType>(moduleType);
                return RestoreEngineModuleData(engineSubType, moduleLevel);
            default:
                return null;
        }
    }

    public bool GetModuleUpgradeCost(int moduleType, int moduleLevel, out CostStruct cost)
    {
        object moduleData = RestoreModuleDataByType(moduleType, moduleLevel);
        if (moduleData == null)
        {
            cost = new CostStruct();
            return false;
        }

        EModuleType type = CommonUtility.GetModuleType(moduleType);
        switch (type)
        {
            case EModuleType.Body:
                var bodyData = (ModuleBodyData)moduleData;
                cost = bodyData.m_upgradeCost;
                return true;
            case EModuleType.Weapon:
                var weaponData = (ModuleWeaponData)moduleData;
                cost = weaponData.m_upgradeCost;
                return true;
            case EModuleType.Engine:
                var engineData = (ModuleEngineData)moduleData;
                cost = engineData.m_upgradeCost;
                return true;
            default:
                cost = new CostStruct();
                return false;
        }
    }
    #endregion

}