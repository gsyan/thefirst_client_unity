using System.Collections.Generic;

 public enum ServerErrorCode
{
    SUCCESS = 0,
    ACCOUNT_REGISTER_FAIL_REASON1 = 1001,
    ACCOUNT_NOT_FOUND = 1002,
    LOGIN_FAIL_REASON1 = 2001,
    INVALID_TOKEN = 2002,
    CHARACTER_CREATE_FAIL_REASON1 = 3001,
    CHARACTER_NAME_DUPLICATE = 3002,
    CHARACTER_SELECTION_REQUIRED = 3003,
    CHARACTER_NOT_FOUND = 3004,
    FLEET_NOT_FOUND = 4001,
    FLEET_DUPLICATE_NAME = 4002,
    FLEET_ACCESS_DENIED = 4003,
    SHIP_NOT_FOUND = 4004,
    MODULE_NOT_FOUND = 4005,
    MODULE_LEVEL_MISMATCH = 4006,
    MODULE_DATA_NOT_FOUND = 4007,
    INSUFFICIENT_MONEY = 4008,
    INSUFFICIENT_MINERAL = 4009,
    ACTIVE_FLEET_NOT_FOUND = 4010,
    FLEET_MAX_SHIPS_REACHED = 4011,
    INVALID_DATA_TABLE = 5001,
    UNKNOWN_ERROR = int.MaxValue,
}

public static class ErrorCodeMapping
{
    public static readonly Dictionary<ServerErrorCode, string> Messages = new Dictionary<ServerErrorCode, string>
    {
        { ServerErrorCode.SUCCESS, "Success" },
        { ServerErrorCode.ACCOUNT_REGISTER_FAIL_REASON1, "Account registration failed due to duplicate email" },
        { ServerErrorCode.ACCOUNT_NOT_FOUND, "Account not found" },
        { ServerErrorCode.LOGIN_FAIL_REASON1, "Invalid email or password" },
        { ServerErrorCode.INVALID_TOKEN, "Invalid token" },
        { ServerErrorCode.CHARACTER_CREATE_FAIL_REASON1, "Character creation failed" },
        { ServerErrorCode.CHARACTER_NAME_DUPLICATE, "Character name already exists" },
        { ServerErrorCode.CHARACTER_SELECTION_REQUIRED, "Character selection required" },
        { ServerErrorCode.CHARACTER_NOT_FOUND, "Character not found" },
        { ServerErrorCode.FLEET_NOT_FOUND, "Fleet not found" },
        { ServerErrorCode.FLEET_DUPLICATE_NAME, "Fleet name already exists" },
        { ServerErrorCode.FLEET_ACCESS_DENIED, "Fleet access denied" },
        { ServerErrorCode.SHIP_NOT_FOUND, "Ship not found" },
        { ServerErrorCode.MODULE_NOT_FOUND, "Module not found" },
        { ServerErrorCode.MODULE_LEVEL_MISMATCH, "Module level mismatch" },
        { ServerErrorCode.MODULE_DATA_NOT_FOUND, "Module data not found" },
        { ServerErrorCode.INSUFFICIENT_MONEY, "Insufficient money for upgrade" },
        { ServerErrorCode.INSUFFICIENT_MINERAL, "Insufficient mineral for upgrade" },
        { ServerErrorCode.ACTIVE_FLEET_NOT_FOUND, "Active fleet not found" },
        { ServerErrorCode.FLEET_MAX_SHIPS_REACHED, "Maximum ships per fleet reached" },
        { ServerErrorCode.INVALID_DATA_TABLE, "Invalid data table provided" },
        { ServerErrorCode.UNKNOWN_ERROR, "Unknown error" },
    };
}