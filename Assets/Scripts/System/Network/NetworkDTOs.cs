// 의도적 생략이 아니라면 클래스위 [System.Serializable]위에 주석 금지
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
    public string fleetName;
    public string description;
    public bool isActive;
    public EFormationType formation;
    public List<ShipInfo> ships;
}

[System.Serializable]
public class ShipInfo
{
    public long id;
    public long fleetId;
    public string shipName;
    public int positionIndex;
    public string description;
    public List<ModuleBodyInfo> bodies;
}

[System.Serializable]
public class ModuleBodyInfo
{
    public EModuleType moduleType;
    public EModuleSubType moduleSubType;
    public int moduleLevel;
    public int bodyIndex;
    public List<ModuleInfo> beams;
    public List<ModuleInfo> missiles;
    public List<ModuleInfo> hangers;
    // 이 body 슬롯에서 subTypeAddCost를 납부해 비용 없이 교체 가능한 서브타입 목록
    public List<EModuleSubType> unlockedSubTypes;
}

[System.Serializable]
public class ModuleInfo
{
    public EModuleType moduleType;
    public EModuleSubType moduleSubType;
    public int moduleLevel;
    public int bodyIndex;
    public int slotIndex;
    // 이 슬롯에서 subTypeAddCost를 납부해 비용 없이 교체 가능한 서브타입 목록
    public List<EModuleSubType> unlockedSubTypes;
}

[System.Serializable]
public class ModuleSlotInfo
{
    public EModuleType moduleType;
    public int slotIndex;

    public ModuleSlotInfo() { }
    public ModuleSlotInfo(EModuleType moduleType, int slotIndex)
    {
        this.moduleType = moduleType;
        this.slotIndex = slotIndex;
    }
    // Body 프리팹의 ModuleSlot 정보를 저장하는 클래스
}

[System.Serializable]
public class ModuleChangeCostEntry
{
    public EModuleSubType moduleSubType; // 적용 대상 새 모듈 subType
    public CostStruct cost;
    // 모듈 교체(적용) 비용 항목 — subType별 MR/ME/MD 비용 정의
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
    public long characterId;
    public string characterName;
    public long mineral;
    public long mineralRare;
    public long mineralExotic;
    public long mineralDark;
    public List<string> clearedZones;  // 클리어한 존 이름 목록 (순서 무관, 각 독립)
    public string collectDateTime;  // 마지막 자원 수확 시간 (ISO 8601 형식)
    public int nameChangeCount;  // 남은 이름 변경 횟수 (초기값 2)
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
public class GuestLoginRequest
{
    public string guestId;
}

[System.Serializable]
public class AuthResponse
{
    public string accessToken;
    public string refreshToken;
    public FleetInfo activeFleetInfo;
    public CharacterInfo characterInfo;
    public int[][] researchedModuleTypes;  // [moduleType, moduleSubType] 쌍의 배열
    public string[] researchedIds;         // 문자열 기반 완료 연구 ID 목록 (tech_level_N 등)
    public bool bGoogleLinked;             // 구글 계정 연동 여부 (Java boolean is 접두사 제거 방지)
}

[System.Serializable]
public class LinkGoogleRequest
{
    public string idToken;
}

[System.Serializable]
public class UnlinkGoogleResponse
{
    public string guestId;  // 해제 후 게스트 복귀용 ID
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

[System.Serializable]
public class CharacterValidateNameRequest
{
    public string name;
}

[System.Serializable]
public class CharacterRenameRequest
{
    public string newName;
}

[System.Serializable]
public class CharacterRenameResponse
{
    public string characterName;
    public int nameChangeCount;  // 변경 후 남은 횟수
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
    public EModuleType moduleType;
    public EModuleSubType moduleSubTypeCurrent;
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
    public int moduleNewLevel;
    public CostRemainInfo costRemainInfo;
    // 교체 후 이 슬롯의 갱신된 unlock 목록 (최초 교체 시 새 subType 포함)
    public List<EModuleSubType> newUnlockedSubTypes;
}

[System.Serializable]
public class ModuleUnlockRequest
{
    public long shipId;
    public int bodyIndex;
    public EModuleType moduleType;
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
public class TechLevelResearchRequest
{
    public string researchId;  // tech_level_N 형식
}

[System.Serializable]
public class TechLevelResearchResponse
{
    public CostRemainInfo costRemainInfo;
    public string[] researchedIds;  // 완료된 tech_level_N 목록
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

#endregion

#region Progress Data Classes #################################################################################
[System.Serializable]
public class ProgressSaveRequest
{
    public string category;  // "tutorial", "achievement", "quest" 등
    public string key;       // "tutorialId_stepId" 형식
}

[System.Serializable]
public class ProgressInfo
{
    public string category;
    public string key;
    public string completedDateTime;  // ISO 8601 UTC 형식
}

[System.Serializable]
public class ProgressListResponse
{
    public List<ProgressInfo> progressList;
}
#endregion

#region Zone Battle Data Classes ##############################################################################
[System.Serializable]
public class ZoneCollectRequest
{

}

[System.Serializable]
public class ZoneCollectResponse
{
    public string collectDateTime;  // 수확 시간 (ISO 8601 형식)
    public CostRemainInfo rewardInfo;  // 보상 (remainMineral = 최종 잔액)
}

[System.Serializable]
public class DestroyZoneStageWaveRequest
{
    public string zoneName;   // 존 이름 (예: "2-5")
    public int waveIndex;     // 방금 처치한 웨이브 인덱스 (0-based, 서버 검증용)
}

[System.Serializable]
public class DestroyZoneStageWaveResponse
{
    public CostRemainInfo rewardInfo;   // 킬 즉시 보상
    public bool isZoneCleared;          // true = 이번 웨이브로 신규 클리어 완료
    public string clearedZoneName;      // isZoneCleared == true 일 때만 유효
    public string collectDateTime;      // isZoneCleared == true 일 때만 유효
}

[System.Serializable]
public class ExitZoneRequest
{
    public string zoneName;
}

[System.Serializable]
public class ExitZoneResponse { }
#endregion

#region Heartbeat Data Classes ################################################################################
[System.Serializable]
public class HeartbeatRequest { }

[System.Serializable]
public class HeartbeatResponse
{
}
#endregion

#region PvP Data Classes #################################################################################
[System.Serializable]
public class PvpRankInfo
{
    public int pvpScore;
    public int pvpWins;
    public int pvpLosses;
    public int pvpRank;
    public int pvpListRefreshRemain;
}

[System.Serializable]
public class PvpOpponentInfo
{
    public long characterId;
    public string characterName;
    public int pvpScore;
    public int rank;
    public FleetInfo fleetInfo;
}

[System.Serializable]
public class PvpListRequest { }

[System.Serializable]
public class PvpListResponse
{
    public List<PvpOpponentInfo> opponents;
}

[System.Serializable]
public class PvpMyRankRequest { }

[System.Serializable]
public class PvpMyRankResponse
{
    public PvpRankInfo myRankInfo;
}

[System.Serializable]
public class PvpRefreshRequest { }

[System.Serializable]
public class PvpRefreshResponse
{
    public List<PvpOpponentInfo> opponents;
    public int refreshRemain;
}

[System.Serializable]
public class PvpBattleStartRequest
{
    public long opponentCharacterId;
}

[System.Serializable]
public class PvpBattleStartResponse
{
    public FleetInfo opponentFleetInfo;
    public string battleToken;
}

[System.Serializable]
public class PvpBattleResultRequest
{
    public string battleToken;
    public bool isVictory;
}

[System.Serializable]
public class PvpBattleResultResponse
{
    public int scoreChange;
    public int newScore;
    public int newRank;
}

[System.Serializable]
public class RankingEntry
{
    public int rank;
    public long characterId;
    public string characterName;
    public string score;
    // PVP/Zone/Attack 랭킹 공용 엔트리. score는 항상 string
}

[System.Serializable]
public class PvpRankingRequest
{
    public int offset; // 0-based
    public int limit;
}

[System.Serializable]
public class PvpRankingResponse
{
    public int totalCount;
    public List<RankingEntry> items;
    public RankingEntry myInfo;         // 내 랭킹 정보 (rank/score, 1시간 주기 랭킹 기준)
    public string lastUpdatedAt;        // 랭킹 마지막 업데이트 시각 ISO 8601
    public string seasonName;           // 시즌 이름 (미설정 시 null)
    public string seasonStartTime;      // 시즌 시작 시각 ISO 8601 (미설정 시 null)
    public string seasonEndTime;        // 시즌 종료 시각 ISO 8601 (미설정 시 null)
}

[System.Serializable]
public class ZoneRankingRequest
{
    public int offset;
    public int limit;
}

[System.Serializable]
public class ZoneRankingResponse
{
    public int totalCount;
    public List<RankingEntry> items;
    public RankingEntry myInfo;         // 내 랭킹 정보 (rank/score, 1시간 주기 기준)
    public string lastUpdatedAt;        // 랭킹 마지막 업데이트 시각 ISO 8601
}

#endregion
