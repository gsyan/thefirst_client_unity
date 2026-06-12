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
    // bit0=전투수리, bit1=미사일, bit2=항공기. 기본값 7(0b111=전체 ON)
    public int tacticOptions;
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
    // Zone 적 전용 배율 — PvP는 기본값(1.0) 유지, 서버 저장 불필요
    public float bodyMultiplier    = 1.0f;
    public float beamMultiplier    = 1.0f;
    public float missileMultiplier = 1.0f;
    public float hangerMultiplier  = 1.0f;
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
    // 이 슬롯에 투자한 modulePoint 이력 (리셋 시 100% 환급)
    public int investedModulePoint;
    // 현재 체력 (절대값). 0 이하 = 기본값(만피). 서버 저장/복원용
    public float currentHealth;
}

[System.Serializable]
public class ModuleInfo
{
    public EModuleType moduleType;
    public EModuleSubType moduleSubType;
    public int moduleLevel;
    public int bodyIndex;
    public int slotIndex;
    // 이 슬롯에 투자한 modulePoint 이력 (리셋 시 100% 환급)
    public int investedModulePoint;
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
    public EModuleSubType moduleSubType;
    public int modulePointCost;
}

[System.Serializable]
public class CharacterInfo
{
    public long characterId;
    public string characterName;
    public int mineral;
    public int techPoint;
    public int modulePoint;
    public int modulePointMaxGot;    // 누적 획득량 (리셋 환급 반영)
    public int pvpPoint;
    public int pvpPointMaxGot;
    public string pvpPointExpiry;   // ISO 8601 — PvP 정산 배치 지급, 만료 시 소멸
    public List<string> clearedZones;  // 클리어한 존 이름 목록 (순서 무관, 각 독립)
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
    public string[] researchedIds;         // 문자열 기반 완료 연구 ID 목록 (tech_level_N 등)
    public bool bGoogleLinked;             // 구글 계정 연동 여부 (Java boolean is 접두사 제거 방지)
    public VipStatusResponse vipStatus;   // 로그인/캐릭터 선택 시 VIP 상태 포함
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
    public int modulePointRemain;
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
    public EFormationType formation;
}

[System.Serializable]
public class ChangeTacticOptionsRequest
{
    public long fleetId;
    public int tacticOptions;
}

[System.Serializable]
public class ChangeTacticOptionsResponse
{
    public int tacticOptions;
}

[System.Serializable]
public class ModuleLevelChangeRequest
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
public class ModuleLevelChangeResponse
{
    public long shipId;
    public int bodyIndex;
    public EModuleType moduleType;
    public EModuleSubType moduleSubType;
    public int slotIndex;
    public int newLevel;
    public int modulePointRemain;
    public int investedModulePoint;
}

[System.Serializable]
public class ModuleGradeChangeRequest
{
    public long shipId;
    public int bodyIndex;
    public EModuleType moduleType;
    public EModuleSubType moduleSubTypeCurrent;
    public EModuleSubType moduleSubTypeNew;
    public int slotIndex;
}

[System.Serializable]
public class ModuleGradeChangeResponse
{
    public long shipId;
    public int bodyIndex;
    public EModuleType moduleTypeCurrent;
    public EModuleSubType moduleSubTypeCurrent;
    public EModuleType moduleTypeNew;
    public EModuleSubType moduleSubTypeNew;
    public int slotIndex;
    public int moduleNewLevel;
    public int modulePointRemain;
    public int investedModulePoint;
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
    public int modulePointRemain;
    public int investedModulePoint;
}

[System.Serializable]
public class TechLevelResearchRequest
{
    public string researchId;  // tech_level_N 형식
}

[System.Serializable]
public class TechLevelResearchResponse
{
    public int techPointRemain;
    public string[] researchedIds;  // 완료된 tech_level_N 목록
}

[System.Serializable]
public class ModuleResetRequest
{
    public long shipId;
    public int bodyIndex;
    public EModuleType moduleType;
    public int slotIndex;
}

[System.Serializable]
public class ModuleResetResponse
{
    public long shipId;
    public int bodyIndex;
    public EModuleType moduleType;
    public int slotIndex;
    public int modulePointRemain;
}

[System.Serializable]
public class ShipResetRemoveRequest
{
    public long shipId;
}

[System.Serializable]
public class ShipResetRemoveResponse
{
    public long removedShipId;
    public int modulePointRemain;
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
public class ClearZoneStageRequest
{
    public string zoneName;   // 존 이름 (예: "2-5")
    public int mineralRemain; // 전투 종료 시점 클라 Mineral 잔액
}

[System.Serializable]
public class ClearZoneStageResponse
{
    public bool isFirstClear;           // true = 최초 클리어
    public string clearedZoneName;      // isFirstClear == true 일 때만 유효
}

[System.Serializable]
public class ClaimZoneRewardRequest
{
    public string zoneName;
    public bool watchedAd;
}

[System.Serializable]
public class ClaimZoneRewardResponse
{
    public string zoneName;
    public bool watchedAd;
    public int mineralRemain;
    public int techPointRemain;
    public int modulePointRemain;
    public int modulePointMaxGot;
}


[System.Serializable]
public class GetStageEnemiesRequest
{
    public string zoneName;
}

[System.Serializable]
public class GetStageEnemiesResponse
{
    public string zoneName;
    public FleetInfo enemyFleet;
}

[System.Serializable]
public class PendingStageRewardRequest { }

[System.Serializable]
public class PendingStageRewardResponse
{
    public int mineralGained;       // 합산 획득량 (*1 고정), 0이면 미수령 없음
    public int techPointGained;
    public int modulePointGained;
    public int mineralRemain;       // 처리 후 잔액
    public int techPointRemain;
    public int modulePointRemain;
    public int modulePointMaxGot;
}

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
    public int seasonNumber;
    public string seasonEndTime; // ISO 8601
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
    public int shipCount;      // 함선 수
    public float statHealth;   // 총 체력
    public float statAttack;   // 총 공격력
    public int statAirCount;   // 함재기 수
    public int statAirAttack;  // 함재기 공격력
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
    public int seasonNumber;            // 시즌 번호 (미설정 시 0)
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

[System.Serializable]
public class FleetHealthSaveRequest
{
    public List<ShipHealthInfo> ships;
}

[System.Serializable]
public class ShipHealthInfo
{
    public long shipId;
    public List<BodyHealthEntry> bodies;
}

[System.Serializable]
public class BodyHealthEntry
{
    public int bodyIndex;
    public float currentHealth;
}

[System.Serializable]
public class FleetInstantRepairRequest { }

[System.Serializable]
public class FleetInstantRepairResponse
{
    public int mineralRemain;
}

#endregion

#region IAP Data Classes ##################################################################################
[System.Serializable]
public class VipPurchaseRequest
{
    public string receipt;    // Unity IAP receipt JSON (플랫폼별 원본)
    public string platform;   // "GooglePlay" | "AppleAppStore"
}

[System.Serializable]
public class VipStatusResponse
{
    public bool isVip;
    public string vipExpiry;            // ISO 8601 UTC, null이면 VIP 아님
    public int mineralRewardMultiplier; // 스테이지 미네랄 보상 배율 (서버 설정값)
}

[System.Serializable]
public class DailyClaimResponse
{
    public bool available;          // true=지급됨, false=24h 미경과 or 테이블 없음
    public int grantedMineral;      // 이번에 지급된 미네랄 양
    public int mineralRemain;       // 지급 후 현재 미네랄
    public string nextAvailableAt;  // 다음 지급 가능 시각 (ISO 8601 UTC)
    public int todayDay;            // 오늘 날짜 (1~28)
    public int claimedDaysMask;     // 이번 달 수령 현황 비트마스크 (bit0=1일, bit27=28일)
    public int vipClaimedDaysMask;  // VIP 보상 수령 현황 비트마스크 (bit0=1일, bit27=28일)
    public int loginRewardMonth;    // 비트마스크 기준 달 (yyyyMM, e.g. 202606)
}
#endregion
