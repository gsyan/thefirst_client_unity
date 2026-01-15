//------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using UnityEngine;

#region Core Data Classes #####################################################################################
[System.Serializable]
public class ApiResponse<T>
{
    public int errorCode;
    public T data;

    public static ApiResponse<T> success(T data)
    {
        return new ApiResponse<T> { errorCode = 0, data = data };
    }

    public static ApiResponse<T> error(int code)
    {
        return new ApiResponse<T> { errorCode = code, data = default };
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
    public EFormationType formation;    
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
    public ModuleBodyInfo[] bodies;
}

[System.Serializable]
public class ModuleBodyInfo
{
    public EModuleType moduleType;
    public EModuleSubType moduleSubType;
    public int moduleLevel;
    public int bodyIndex;
    public ModuleInfo[] engines;
    public ModuleInfo[] weapons;
    public ModuleInfo[] hangers;
}

[System.Serializable]
public class ModuleInfo
{
    public EModuleType moduleType;
    public EModuleSubType moduleSubType;
    public int moduleLevel;
    public int bodyIndex;
    public int slotIndex;
}

// Body 프리팹의 ModuleSlot 정보를 저장하는 클래스
[System.Serializable]
public class ModuleSlotInfo
{
    public EModuleType moduleType;
    public EModuleSubType moduleSubType;
    public EModuleSlotType moduleSlotType;
    public int slotIndex;

    public ModuleSlotInfo() { }
    public ModuleSlotInfo(EModuleType moduleType, EModuleSubType moduleSubType, EModuleSlotType moduleSlotType, int slotIndex)
    {
        this.moduleType = moduleType;
        this.moduleSubType = moduleSubType;
        this.moduleSlotType = moduleSlotType;
        this.slotIndex = slotIndex;
    }
}

[System.Serializable]
public class CostStruct
{
    public int techLevel;
    public long mineral;
    public long mineralRare;
    public long mineralExotic;
    public long mineralDark;

    public CostStruct()
    {
        this.techLevel = 0;
        this.mineral = 0;
        this.mineralRare = 0;
        this.mineralExotic = 0;
        this.mineralDark = 0;
    }
    public CostStruct(int techLevel, long mineral, long mineralRare, long mineralExotic, long mineralDark)
    {
        this.techLevel = techLevel;
        this.mineral = mineral;
        this.mineralRare = mineralRare;
        this.mineralExotic = mineralExotic;
        this.mineralDark = mineralDark;
    }
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
    public int[][] researchedModuleTypes;  // [moduleType, moduleSubType] 쌍의 배열
}

[System.Serializable]
public class CharacterCreateRequest
{
    public string characterName;
}

[System.Serializable]
public class CharacterResponse
{
    public long characterId;
    public string characterName;
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
    public ShipInfo newShipInfo;
    public CostRemainInfo costRemainInfo;
    public FleetInfo updatedFleetInfo;
}

[System.Serializable]
public class ChangeFormationRequest
{
    public long fleetId;
    public EFormationType formationType;
}

[System.Serializable]
public class ChangeFormationResponse
{
    public FleetInfo updatedFleetInfo;
}

[System.Serializable]
public class ModuleUpgradeRequest
{
    public long shipId;
    public int bodyIndex;
    public EModuleType moduleType;
    public EModuleSubType moduleSubType;
    public int slotIndex;
    public int currentLevel;
    public int targetLevel;
}

[System.Serializable]
public class ModuleUpgradeResponse
{
    public long shipId;
    public int bodyIndex;
    public EModuleType moduleType;
    public EModuleSubType moduleSubType;
    public int slotIndex;
    public int newLevel;
    public CostRemainInfo costRemainInfo;
}

[System.Serializable]
public class ModuleChangeRequest
{
    public long shipId;
    public int bodyIndex;
    public EModuleType moduleTypeCurrent;
    public EModuleSubType moduleSubTypeCurrent;
    public EModuleType moduleTypeNew;
    public EModuleSubType moduleSubTypeNew;
    public int slotIndex;
}

[System.Serializable]
public class ModuleChangeResponse
{
    public long shipId;
    public int bodyIndex;
    public EModuleType moduleTypeCurrent;
    public EModuleSubType moduleSubTypeCurrent;
    public EModuleType moduleTypeNew;
    public EModuleSubType moduleSubTypeNew;
    public int slotIndex;
}

[System.Serializable]
public class ModuleUnlockRequest
{
    public long shipId;
    public int bodyIndex;
    public EModuleType moduleType;
    public EModuleSubType moduleSubType;
    public int slotIndex;
}

[System.Serializable]
public class ModuleUnlockResponse
{
    public long shipId;
    public int bodyIndex;
    public EModuleType moduleType;
    public EModuleSubType moduleSubType;
    public int slotIndex;
    public CostRemainInfo costRemainInfo;
}

[System.Serializable]
public class ModuleResearchRequest
{
    public EModuleType moduleType;
    public EModuleSubType moduleSubType;
}

[System.Serializable]
public class ModuleResearchResponse
{
    public EModuleType moduleType;
    public EModuleSubType moduleSubType;
    public CostRemainInfo costRemainInfo;
    public int[][] researchedModuleTypes;  // [moduleType, moduleSubType] 쌍의 배열
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

// [System.Serializable]
// public class ModuleBodyAddRequest
// {
//     public long shipId;
//     public string bodyType;
//     public int bodyLevel;
//     public Vector3 position;
// }

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
    public EModuleType moduleType;
    public int moduleLevel;
    public int slotIndex;
}

// [System.Serializable]
// public class FleetStatsRequest
// {
//     public long fleetId;
// }

// [System.Serializable]
// public class FleetStatsResponse
// {
//     public long fleetId;
//     public string fleetName;
//     public ShipStatsInfo[] ships;
//     public FleetTotalStats totalStats;
// }

// [System.Serializable]
// public class ShipStatsInfo
// {
//     public long shipId;
//     public string shipName;
//     public int positionIndex;
//     public ModuleStats totalStats;
//     //public PartsBodyInfo[] partsBodies;
// }

// [System.Serializable]
// public class PartsBodyInfo
// {
//     public int bodyIndex;
//     public int level;
//     public ModuleStats stats;
//     public ModuleInfo[] weapons;
//     public ModuleInfo[] engines;
// }

// [System.Serializable]
// public class ModuleInfo
// {
//     public EModuleType moduleType;
//     public int level;
//     public int slotIndex;
//     public ModuleStats stats;
// }

// [System.Serializable]
// public class FleetTotalStats
// {
//     public float totalHealth;
//     public float totalAttackPower;
//     public float averageMovementSpeed;
//     public float totalCargoCapacity;
//     public int totalShips;
//     public int totalWeapons;
//     public int totalEngines;
// }
#endregion
