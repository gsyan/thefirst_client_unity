//------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

#region Core Data Classes #####################################################################################
[System.Serializable]
public class ApiResponse<T>
{
    public int errorCode;
    public string errorMessage;
    public T data;

    public static ApiResponse<T> success(T data)
    {
        return new ApiResponse<T> { errorCode = 0, errorMessage = "Success", data = data };
    }

    public static ApiResponse<T> error(int code, string message)
    {
        return new ApiResponse<T> { errorCode = code, errorMessage = message, data = default };
    }
}

[System.Serializable]
public class FleetInfo
{
    public long id;
    public long characterId;
    public string fleetName;
    public string description;
    public bool isActive;
    public string formation;
    public string dateTime;
    public string lastModified;
    public ShipInfo[] ships;
}

[System.Serializable]
public class ShipInfo
{
    public long id;
    public long fleetId;
    public string shipName;
    public int positionIndex;
    public string description;
    public string dateTime;
    public string lastModified;
    public ModuleBodyInfo[] bodies;
}

[System.Serializable]
public class ModuleBodyInfo
{
    public int moduleTypePacked;
    public int moduleLevel;
    public int bodyIndex;
    public string dateTime;
    public ModuleEngineInfo[] engines;
    public ModuleWeaponInfo[] weapons;
    public ModuleHangerInfo[] hangers;

    public EModuleType ModuleType => CommonUtility.GetModuleType(moduleTypePacked);
    public EModuleBodySubType ModuleSubType => CommonUtility.GetModuleSubType<EModuleBodySubType>(moduleTypePacked);
    public EModuleStyle ModuleStyle => CommonUtility.GetModuleStyle(moduleTypePacked);
}

// [System.Serializable]
// public class ModuleInfo
// {
//     public int moduleTypePacked;
//     public int moduleLevel;
//     public int bodyIndex;
//     public int slotIndex;
//     public string dateTime;

//     public EModuleType ModuleType => CommonUtility.GetModuleType(moduleTypePacked);
//     public EModuleEngineSubType ModuleSubType => CommonUtility.GetModuleSubType<EModuleEngineSubType>(moduleTypePacked);
//     public EModuleStyle ModuleStyle => CommonUtility.GetModuleStyle(moduleTypePacked);
// }

[System.Serializable]
public class ModuleEngineInfo
{
    public int moduleTypePacked;
    public int moduleLevel;
    public int bodyIndex;
    public int slotIndex;
    public string dateTime;

    public EModuleType ModuleType => CommonUtility.GetModuleType(moduleTypePacked);
    public EModuleEngineSubType ModuleSubType => CommonUtility.GetModuleSubType<EModuleEngineSubType>(moduleTypePacked);
    public EModuleStyle ModuleStyle => CommonUtility.GetModuleStyle(moduleTypePacked);
}

[System.Serializable]
public class ModuleWeaponInfo
{
    public int moduleTypePacked;
    public int moduleLevel;
    public int bodyIndex;
    public int slotIndex;
    public string dateTime;

    public EModuleType ModuleType => CommonUtility.GetModuleType(moduleTypePacked);
    public EModuleWeaponSubType ModuleSubType => CommonUtility.GetModuleSubType<EModuleWeaponSubType>(moduleTypePacked);
    public EModuleStyle ModuleStyle => CommonUtility.GetModuleStyle(moduleTypePacked);
}

[System.Serializable]
public class ModuleHangerInfo
{
    public int moduleTypePacked;
    public int moduleLevel;
    public int bodyIndex;
    public int slotIndex;
    public string dateTime;

    public EModuleType ModuleType => CommonUtility.GetModuleType(moduleTypePacked);
    public EModuleHangerSubType ModuleSubType => CommonUtility.GetModuleSubType<EModuleHangerSubType>(moduleTypePacked);
    public EModuleStyle ModuleStyle => CommonUtility.GetModuleStyle(moduleTypePacked);
}

[System.Serializable]
public class CharacterInfo
{
    public string characterName;
    public int techLevel;
    public long mineral;
    public long mineralRare;
    public long mineralExotic;
    public long mineralDark;    
}
#endregion

#region Authentication Data Classes ###########################################################################
[System.Serializable]
public class SignUpRequest
{
    public string email;
    public string password;
}

[System.Serializable]
public class LoginRequest
{
    public string email;
    public string password;
}

[System.Serializable]
public class RefreshTokenRequest
{
    public string refreshToken;
}

[System.Serializable]
public class GoogleLoginRequest
{
    public string idToken;
}

[System.Serializable]
public class AuthResponse
{
    public string accessToken;
    public string refreshToken;
    public FleetInfo activeFleetInfo;
    public CharacterInfo characterInfo;
    public int[] researchedModuleTypePacked;
}

[System.Serializable]
public class CharacterCreateRequest
{
    public string characterName;
}

[System.Serializable]
public class CharacterResponse
{
    public long id;
    public long characterId;
    public string characterName;
    public string dateTime;
}
#endregion

#region Development Data Classes ##############################################################################
[System.Serializable]
public class DevCommandRequest
{
    public string command;
    public string[] @params;
}
#endregion

#region Fleet Upgrade Data Classes ############################################################################
[System.Serializable]
public class AddShipRequest
{
    public long? fleetId;
}

[System.Serializable]
public class AddShipResponse
{
    public bool success;
    public string message;
    public ShipInfo newShipInfo;
    public CostRemainInfo costRemainInfo;
    public FleetInfo updatedFleetInfo;
}

[System.Serializable]
public class ChangeFormationRequest
{
    public long fleetId;
    public string formationType;
}

[System.Serializable]
public class ChangeFormationResponse
{
    public bool success;
    public string message;
    public FleetInfo updatedFleetInfo;
}

[System.Serializable]
public class ModuleUpgradeRequest
{
    public long shipId;
    public int bodyIndex;
    public string moduleType;
    public int currentLevel;
    public int targetLevel;
}

[System.Serializable]
public class ModuleUpgradeResponse
{
    public bool success;
    public int newLevel;
    public ModuleStats newStats;
    public CostRemainInfo costRemainInfo;
    public string message;
}

[System.Serializable]
public class ModuleStats
{
    public float health;
    public float attackPower;
    public float movementSpeed;
    public float rotationSpeed;
    public float cargoCapacity;
}

[System.Serializable]
public class ModuleChangeRequest
{
    public long shipId;
    public int bodyIndex;
    public int slotIndex;
    public string currentModuleType;
    public string newModuleType;
    public int newModuleLevel;
}

[System.Serializable]
public class ModuleChangeResponse
{
    public bool success;
    public ShipInfo updatedShipInfo;
    public CostRemainInfo costRemainInfo;
    public string message;
}

[System.Serializable]
public class ModuleResearchRequest
{
    public int moduleTypePacked;
}

[System.Serializable]
public class ModuleResearchResponse
{
    public bool success;
    public int moduleTypePacked;
    public CostRemainInfo costRemainInfo;
    public int[] researchedModuleTypePacked;
    public string message;
}

[System.Serializable]
public class CostRemainInfo
{
    public long mineralCost;
    public long mineralRareCost;
    public long mineralExoticCost;
    public long mineralDarkCost;

    public long remainMineral;
    public long remainMineralRare;
    public long remainMineralExotic;
    public long remainMineralDark;
}

[System.Serializable]
public class ModuleBodyAddRequest
{
    public long shipId;
    public string bodyType;
    public int bodyLevel;
    public Vector3 position;
}

[System.Serializable]
public class ModuleBodyRemoveRequest
{
    public long shipId;
    public int bodyIndex;
}

[System.Serializable]
public class ModuleInstallRequest
{
    public long shipId;
    public int bodyIndex;
    public string moduleType;
    public int moduleLevel;
    public int slotIndex;
}

[System.Serializable]
public class FleetStatsRequest
{
    public long fleetId;
}

[System.Serializable]
public class FleetStatsResponse
{
    public long fleetId;
    public string fleetName;
    public ShipStatsInfo[] ships;
    public FleetTotalStats totalStats;
}

[System.Serializable]
public class ShipStatsInfo
{
    public long shipId;
    public string shipName;
    public int positionIndex;
    public ModuleStats totalStats;
    public PartsBodyInfo[] partsBodies;
}

[System.Serializable]
public class PartsBodyInfo
{
    public int bodyIndex;
    public int level;
    public ModuleStats stats;
    public ModuleInfo[] weapons;
    public ModuleInfo[] engines;
}

[System.Serializable]
public class ModuleInfo
{
    public string moduleType;
    public int level;
    public int slotIndex;
    public ModuleStats stats;
}

[System.Serializable]
public class FleetTotalStats
{
    public float totalHealth;
    public float totalAttackPower;
    public float averageMovementSpeed;
    public float totalCargoCapacity;
    public int totalShips;
    public int totalWeapons;
    public int totalEngines;
}
#endregion
