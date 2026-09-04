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
    public List<ModuleHullInfo> hulls;
    // Zone 적 전용 배율 — PvP는 기본값(1.0) 유지, 서버 저장 불필요
    public float healthMultiplier = 1.0f;
    public float attackMultiplier = 1.0f;
    // 함체 기반 함선 배치(탐사 그리드) — hullSubType(모듈 서브타입 이름 문자열)으로 DataTableModule hull 그룹 참조, isFront로 전/후위 배치
    public string hullSubType;
    public bool isFront;
}

[System.Serializable]
public class ModuleHullInfo
{
    public EModuleType moduleType;
    public string moduleSubType;
    public int moduleLevel;
    public int hullIndex;
    public List<ModuleInfo> beams;
    public List<ModuleInfo> missiles;
    public List<ModuleInfo> hangars;
    // 실드/요격체 장착 서브타입 — 빈 문자열이면 미장착. 적함대 존 데이터 배관용으로 서버가 채워 보냄(클라 소비 로직은 후속 작업)
    public string shieldModuleSubType = "";
    public string interceptorModuleSubType = "";
    // 현재 체력 (절대값). 0 이하 = 기본값(만피). 서버 저장/복원용
    public float currentHealth;
}

[System.Serializable]
public class ModuleInfo
{
    public EModuleType moduleType;
    public string moduleSubType;
    public int moduleLevel;
    public int hullIndex;
    public int slotIndex;
    public int attackPoints; // 빔/미사일 공격력, 격납고는 대함 공격력 강화 투자 포인트 — 1포인트 = 지휘력 1
    public int attackToFighterPoints; // 격납고 전용 — 대전투기 공격력 강화 투자 포인트. 빔/미사일은 항상 0
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
    // Hull 프리팹의 ModuleSlot 정보를 저장하는 클래스
}

[System.Serializable]
public class CommanderInfo
{
    public long commanderId;
    public string commanderName;
    public int nameChangeCount;  // 남은 이름 변경 횟수 (초기값 2)
    public int commanderLevel;
    public int exp;    
    public int commandPowerMax;  // 탐험 함대 편성 지휘력 최대치 — IncreaseCommandPowerMaxRequest로 영구 증가
    public int tacticPowerMax;  // 전술 토글(체력회복/미사일/함재기) 공용 소모 게이지 상한 — IncreaseTacticPowerMaxRequest로 영구 증가
    public int tacticPower;  // 진행 중인 탐험 런의 전술력 현재치(ZoneRun.tacticPower와 동일), 진행 중인 런이 없으면 tacticPowerMax와 동일값
    public int explorationSeedBase;  // 서버 월드 시드+커맨더 조합 고정값 — 존별 그리드/적함대 시드는 클라에서 이 값과 zoneNumber를 조합해 결정론적으로 계산
    public List<string> clearedZones;  // 클리어한 존 이름 목록 (순서 무관, 각 독립)
    public int explorationPoint;    // 보유(확정 지급된) 탐험 포인트 — 적립(ZoneRun.explorationPointBanked)과 별개
    public int explorationZoneNumber;  // 진행 중인 탐험 런의 존 번호, 없으면 0
    public string explorationCell;  // 진행 중인 탐험 런의 마지막 클리어 셀 "row-col"(0-indexed, ZoneRun.currentCell과 동일 포맷), 없으면 빈 문자열
    public int highestClearedZoneNumber;  // 존 탈출(ESCAPED)로 확정된 존 번호 중 최댓값, 없으면 0
    public int pvpPoint;
    public int pvpPointMaxGot;
    public string pvpPointExpiry;   // ISO 8601 — PvP 정산 배치 지급, 만료 시 소멸

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
    public string guestSecret;  // 서버 발급 게스트 자격증명 — 최초 로그인 시엔 빈 값
}

[System.Serializable]
public class AuthResponse
{
    public string accessToken;
    public string refreshToken;
    public FleetInfo activeFleetInfo;
    public CommanderInfo commanderInfo;
    public bool bGoogleLinked;             // 구글 계정 연동 여부 (Java boolean is 접두사 제거 방지)
    public VipStatusResponse vipStatus;   // 로그인/캐릭터 선택 시 VIP 상태 포함
    public List<ProgressInfo> progressList; // 튜토리얼 등 진행도 목록 (SpaceScene 진입 전 미리 확보용)
    public string guestSecret;  // 게스트 신규/마이그레이션 발급 시에만 채워짐. 빈 값이면 클라 저장값 유지
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
    public string guestSecret;  // 게스트 복귀용 신규 발급 secret
}

[System.Serializable]
public class CommanderCreateRequest
{
    public string commanderName;
}

[System.Serializable]
public class CommanderResponse
{
    public long commanderId;
    public string commanderName;
}

[System.Serializable]
public class CommanderValidateNameRequest
{
    public string name;
}

[System.Serializable]
public class CommanderRenameRequest
{
    public string newName;
}

[System.Serializable]
public class CommanderRenameResponse
{
    public string commanderName;
    public int nameChangeCount;  // 변경 후 남은 횟수
}

[System.Serializable]
public class RedeemCodeRequest
{
    public string code;
}

[System.Serializable]
public class RedeemCodeResponse
{
    // 보상타입별 필드를 옵셔널로 추가해나가는 방식 (ClaimZoneRewardResponse 패턴 참고)
    public int commanderLevel;
    public int exp;
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
public class PvpClaimSeasonRewardRequest { }

[System.Serializable]
public class PvpClaimSeasonRewardResponse
{
    public int pvpPointGained;
}

#endregion

#region Exploration Grid Data Classes ##########################################################################
[System.Serializable]
public class EnterExplorationCellRequest
{
    public int zoneNumber;
    public int cellRow;
    public int cellCol;
    public FleetInfo fleetInfo; // 전투시작 요청에 함대 배치 동봉 (별도 실시간 동기화 없음)
}

[System.Serializable]
public class EnterExplorationCellResponse
{
    public int zoneNumber;
    public int cellRow;
    public int cellCol;
    public string challengeToken; // 이 셀에 대해 발급된 1회용 클리어 챌린지 토큰 — ClearExplorationCellRequest에 그대로 실어 보내야 함
}

[System.Serializable]
public class ShipHealthRatioInfo
{
    // 슬롯 포지션 인덱스별 함선 체력 비율(0~1) — 존 런 진행 상황(ZoneRun) 스냅샷으로 서버에 저장, 앱 재시작 후 복구용
    public long shipId; // 서버가 실제 함대 편성과 대조(함선 구성 일치 검증)하는 데 사용
    public int positionIndex;
    public float healthRatio;
}

[System.Serializable]
public class ClearExplorationCellRequest
{
    public int zoneNumber;
    public int cellRow;
    public int cellCol;
    public List<ShipHealthRatioInfo> shipHealthRatios; // 셀 클리어 시점의 내 함대 체력 스냅샷 — ZoneRun에 저장돼 재접속 시 복구됨
    public int tacticPower; // 셀 클리어 시점의 전술력 현재치 — ZoneRun.tacticPower에 확정 저장(전투 중엔 서버에 실시간 저장하지 않고 클리어 시에만 확정)
    public string challengeToken; // EnterExplorationCellResponse.challengeToken을 그대로 반환 — enter-cell 없이 clear-cell만 반복 호출하는 것을 막음
}

[System.Serializable]
public class ClearExplorationCellResponse
{
    public int explorationPointGained; // 존 고정값만큼 적립 (미확정 상태, 탈출 시 확정 정산) — 적 함대 성능과 무관
    public int expGained;              // 존 고정값만큼 적립된 지휘관 경험치 (미확정 상태, 탈출 시 확정 정산) — 빈 셀은 0
    public List<string> rewardCardCandidates; // 서버가 추첨한 보상카드 후보 3개(cardId) — 탈출 셀이거나 보상이 없는 셀(빈 셀)은 null
}

[System.Serializable]
public class ConfirmRewardCardRequest
{
    public int zoneNumber;
    public int cellRow;
    public int cellCol;
    public string selectedCardId; // ClearExplorationCellResponse.rewardCardCandidates 중 하나여야 함(서버가 검증)
}

[System.Serializable]
public class ConfirmRewardCardResponse
{
    public string selectedCardId;
    public int explorationPointGained; // 즉시 가산형 카드(Instant_ExplorationPointFlat)일 때만 확정 지급량, 그 외 0
}

[System.Serializable]
public class GetActiveZoneRunProgressRequest { } // 진행 중인 탐험 런의 클리어 셀 목록 조회 — 커맨더당 IN_PROGRESS 런은 항상 1개뿐이라 zoneNumber 불필요

[System.Serializable]
public class GetActiveZoneRunProgressResponse
{
    public int zoneNumber;        // 진행 중인 런이 없으면 0
    public string[] clearedCells; // "row-col"(0-indexed) 목록, 클리어 순서대로
    public int explorationPointBanked; // 진행 중인 런의 적립(미확정) 탐험 포인트, 없으면 0
    public int commanderExpBanked;     // 진행 중인 런의 적립(미확정) 지휘관 경험치, 없으면 0
    public List<ShipHealthRatioInfo> shipHealthRatios; // 마지막 셀 클리어 시점에 저장된 내 함대 체력 스냅샷 — 없으면 null(만피로 스폰된 상태 그대로)
    public List<string> selectedRewardCards; // 이번 런에서 선택 확정한 보상카드(cardId) 전체 목록 — 지속버프 여부는 클라가 DataTableRewardCard로 재판별
    public List<string> pendingRewardCardCandidates; // 마지막 클리어 셀에 후보는 나왔지만 아직 선택 확정 전이면 그 후보 3개 — 카드 선택 팝업이 뜨기 전에 앱이 꺼진 경우 재접속 시 복구용, 없으면 null
}

[System.Serializable]
public class EscapeExplorationZoneRequest
{
    public int zoneNumber;
    public bool isSuccess; // true=탈출 성공(100% 지급), false=실패(50% 지급)
}

[System.Serializable]
public class EscapeExplorationZoneResponse
{
    public int explorationPointGained;   // 확정 지급된 탐험 포인트
    public int explorationPointRemain;   // 확정 지급 후 은행 잔액
    public int expGained;                // 확정 지급된 지휘관 경험치
    public int totalExp;                 // 반영 후 누적 경험치(권위값)
    public int commanderLevel;           // 반영 후 커맨더 레벨(레벨업 없으면 기존과 동일)
    public int highestClearedZoneNumber; // 탈출 성공 시 갱신된 값(권위값) — 클라 GetInitialZoneIndex()가 이 값 기준으로 다음 존을 계산
    public int tacticPower;              // 런 종료로 회복된 전술력 현재치(=tacticPowerMax, 권위값)
}

[System.Serializable]
public class AbandonZoneRunRequest { } // 다른 존 도전 전 진행 중인 런을 명시적으로 포기 — 커맨더당 IN_PROGRESS 런은 항상 1개뿐이라 zoneNumber 불필요

[System.Serializable]
public class AbandonZoneRunResponse
{
    public int explorationPointGained; // 포기로 확정 지급된 탐험 포인트(50%)
    public int explorationPointRemain; // 확정 지급 후 은행 잔액
    public int expGained;              // 포기로 확정 지급된 지휘관 경험치(50%)
    public int totalExp;               // 반영 후 누적 경험치(권위값)
    public int commanderLevel;         // 반영 후 커맨더 레벨(레벨업 없으면 기존과 동일)
    public int tacticPower;            // 런 종료로 회복된 전술력 현재치(=tacticPowerMax, 권위값)
}

[System.Serializable]
public class IncreaseCommandPowerMaxRequest
{
    public int amount; // 소모할 탐험 포인트 — 지휘력 최대치도 동일 수치만큼 증가(교환비 1:1)
}

[System.Serializable]
public class IncreaseCommandPowerMaxResponse
{
    public int commandPowerMax;        // 갱신된 지휘력 최대치
    public int explorationPointRemain; // 소모 후 은행 잔액
}

[System.Serializable]
public class IncreaseTacticPowerMaxRequest
{
    public int amount; // 소모할 탐험 포인트 — 전술력 최대치도 동일 수치만큼 증가(교환비 1:1)
}

[System.Serializable]
public class IncreaseTacticPowerMaxResponse
{
    public int tacticPowerMax;         // 갱신된 전술력 최대치
    public int tacticPower;            // 갱신된 전술력 현재치 — 진행 중인 런이 있으면 그 런의 현재치도 증가분만큼 함께 올라간 값, 없으면 tacticPowerMax와 동일
    public int explorationPointRemain; // 소모 후 은행 잔액
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
    public long commanderId;
    public string commanderName;
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
    public long opponentCommanderId;
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
    public long commanderId;
    public string commanderName;
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
public class FleetPlaceShipRequest
{
    // 함대편성(FleetComposition) 슬롯에 함선 배치/교체 시 저장 — isFront는 그 슬롯의 현재 전/후방 값을 그대로 실어보냄
    public int slotIndex;
    public string hullSubType;
    public bool isFront;
}

[System.Serializable]
public class FleetSetFrontRequest
{
    // 함대편성 슬롯의 전/후방 토글 저장
    public int slotIndex;
    public bool isFront;
}

[System.Serializable]
public class SetModuleRequest
{
    // 함대편성 슬롯(함선) 하나의 최종 장착 모듈 "전체"를 한 번에 교체 — on/off만 지원(서브타입 선택 없음)
    // 낱개 토글을 순서대로 여러 번 보내면 중간 상태에서 "공격모듈 0개"/"예산 초과" 검증에 걸릴 수 있어(예: 빔→미사일 교체 시 순서에 따라 항상 실패),
    // 반드시 최종 상태 하나로 모아 보내고 서버는 그 결과 상태만 검증한다
    public int slotIndex; // CommanderFleetSlot의 slotIndex(함대편성 슬롯)
    public ModuleHullInfo modules; // 이 슬롯에 최종적으로 장착되어 있어야 할 모듈 전체(beams/missiles/hangars, 각 slotIndex만 유효)
}

[System.Serializable]
public class SetModuleResponse
{
    public ModuleHullInfo hull;       // 갱신된 함선의 현재 로드아웃 전체(beams/missiles/hangars)
    public int commandCost;           // 갱신된 함선의 지휘력 코스트
    public int remainingCommandPower; // 커맨더의 남은 지휘력
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
}

[System.Serializable]
public class DailyClaimResponse
{
    public bool available;          // true=지급됨, false=24h 미경과 or 테이블 없음
    public int grantedExplorationPoint; // 이번에 지급된 탐험 포인트 양
    public int explorationPointRemain;  // 지급 후 현재 탐험 포인트
    public string nextAvailableAt;  // 다음 지급 가능 시각 (ISO 8601 UTC)
    public int todayDay;            // 오늘 날짜 (1~28)
    public int claimedDaysMask;     // 이번 달 수령 현황 비트마스크 (bit0=1일, bit27=28일)
    public int vipClaimedDaysMask;  // VIP 보상 수령 현황 비트마스크 (bit0=1일, bit27=28일)
    public int loginRewardMonth;    // 비트마스크 기준 달 (yyyyMM, e.g. 202606)
}

#region Version Data Classes ##################################################################################
[System.Serializable]
public class ServerStatusRequest
{
    public int versionCode;
}

[System.Serializable]
public class ServerStatusResponse
{
    public bool updateRequired;
    public int minVersionCode;
    public string minVersionName;
    public bool working;
    public string endTime;
}
#endregion
#endregion
