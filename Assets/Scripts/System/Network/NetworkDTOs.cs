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
    // bit0=빔, bit1=미사일, bit2=함재기, bit3=수리, bit4=실드, bit5=요격체
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
}

[System.Serializable]
public class ModuleInfo
{
    public EModuleType moduleType;
    public string moduleSubType;
    public int moduleLevel;
    public int hullIndex;
    public int slotIndex;
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
    public int commandPowerMax;  // 지휘력(캐파) 최대치 — 구매 없이 커맨더 레벨에 따라 자동 증가(M13)
    public int resourceMax;  // 전술 토글(체력회복/미사일/함재기) 공용 소모 게이지 상한 — 커맨더 생성 시 고정값, 성장 수단 없음
    public int resource;  // 진행 중인 탐험 런의 자원 현재치(ZoneRun.resource와 동일), 진행 중인 런이 없으면 resourceMax와 동일값
    public int mineralAmount;  // 진행 중인 탐험 런의 광물 현재치(ZoneRun.mineralAmount와 동일) — 존런 전용, 영구 자산 아님, 진행 중인 런이 없으면 0
    public int explorationSeedBase;  // 서버 월드 시드+커맨더 조합 고정값 — 존별 그리드/적함대 시드는 클라에서 이 값과 zoneNumber를 조합해 결정론적으로 계산
    public int mineralEarnedTotal;  // 역대 누적 획득 광물(영구, MineralTotal 업적 조건 전용) — 존런 종료로 사라지는 mineralAmount와 별개
    public int achievementPoint;    // 보유 업적포인트 — 티어4+ 함체 언락(소모형)에 사용, 존 탈출/포기 정산 시 지급
    public List<string> unlockedHulls;  // 업적포인트로 언락 완료한 hullSubType 목록(티어1~3은 언락 불필요라 여기 없어도 사용 가능)
    public int explorationZoneNumber;  // 진행 중인 탐험 런의 존 번호, 없으면 0
    public string explorationCell;  // 진행 중인 탐험 런의 마지막 클리어 셀 "row-col"(0-indexed, ZoneRun.currentCell과 동일 포맷), 없으면 빈 문자열
    public int highestClearedZoneNumber;  // 존 탈출(ESCAPED)로 확정된 존 번호 중 최댓값, 없으면 0
    public int pvpPoint;
    public int pvpPointMaxGot;
    public string pvpPointExpiry;   // ISO 8601 — PvP 정산 배치 지급, 만료 시 소멸
    public bool hasUnclaimedAchievement;  // 완료했지만 아직 안 받은 업적 존재 여부 — 로그인/재접속 시 레드닷 초기값용
    public List<ArchetypeUpgradeInfo> archetypeUpgrades;  // 아키타입단위 영구 모듈강화(M6) 현재 레벨 전체 목록 — 기록이 없는 (아키타입,moduleType) 조합은 레벨 0
    public List<HullArchetypeProgressInfo> hullArchetypePassiveProgress;  // 아키타입 패시브(1~5성, 4단계) 언락 조건 누적 카운터 — 기록이 없는 (아키타입,counterType) 조합은 0

}

[System.Serializable]
public class ArchetypeUpgradeInfo
{
    public string hullArchetype;
    public EModuleType moduleType;
    public int slotIndex;  // beam/missile/hangar만 의미 있음(슬롯별 독립 레벨) — hull/shield/interceptor는 항상 0
    public int level;
}

[System.Serializable]
public class HullArchetypeProgressInfo
{
    public string hullArchetype;
    public int stage;           // 패시브 단계(1~4) — (아키타입, 단계)마다 진행도가 독립이고 앞 단계가 완료돼야 다음 단계가 세기 시작함
    public string counterType;  // 이 단계가 세는 카운터. DataTableHullArchetypePassive.counter_type과 같은 문자열(예: "destroy_enemy_ship", "zone_run_clear")
    public long counterValue;   // 이 단계 안에서의 진행도(0부터, 문턱을 넘는 값은 서버가 문턱까지만 저장)
    public bool completed;      // 이 단계 해금 여부 — 서버가 판정, 한 번 true면 영구
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
public class MoveToExplorationCellRequest
{
    public int zoneNumber;
    public int cellRow;
    public int cellCol;
}

[System.Serializable]
public class MoveToExplorationCellResponse
{
    public int cellRow;
    public int cellCol;
}

[System.Serializable]
public class ShipHealthRatioInfo
{
    // 슬롯 포지션 인덱스별 함선 체력/실드 비율(0~1) — 존 런 진행 상황(ZoneRun) 스냅샷으로 서버에 저장, 앱 재시작 후 복구용
    public long shipId; // 서버가 실제 함대 편성과 대조(함선 구성 일치 검증)하는 데 사용
    public int positionIndex;
    public float healthRatio;
    public float shieldRatio;
}

[System.Serializable]
public class ClearExplorationCellRequest
{
    public int zoneNumber;
    public int cellRow;
    public int cellCol;
    public List<ShipHealthRatioInfo> shipHealthRatios; // 셀 클리어 시점의 내 함대 체력 스냅샷 — ZoneRun에 저장돼 재접속 시 복구됨
    public int resource; // 셀 클리어 시점의 자원 현재치 — ZoneRun.resource에 확정 저장(전투 중엔 서버에 실시간 저장하지 않고 클리어 시에만 확정)
    public int mineralAmount; // 셀 클리어 시점의 광물 현재치 — ZoneRun.mineralAmount에 확정 저장(존런 전용, 서버 검증 없이 클라 보고값을 그대로 신뢰)
    public string challengeToken; // EnterExplorationCellResponse.challengeToken을 그대로 반환 — enter-cell 없이 clear-cell만 반복 호출하는 것을 막음
    public int enemyShipKills; // 이번 셀 전투에서 격침한 적 함선 수 — 일일퀘스트 집계용(클라 보고값, 서버가 상한으로 보정)
    public int aircraftLaunches; // 이번 셀 전투에서 내 함재기가 출격한 횟수 — 일일퀘스트 집계용(클라 보고값, 서버가 상한으로 보정)
    public int scavengedMineral; // 이번 셀 전투에서 함재기 약탈로 얻은 광물 합계 — 서버가 mineralGained에 합산해 지급(상한 없음)하고 corsair scavenge_mineral 카운터에 더함
    public int scoutedMineral; // 이번 셀 전투에서 탐사(argus) 성공으로 얻은 광물 합계 — 서버가 mineralGained에 합산해 지급(상한 없음)
    public List<ArchetypeCounterReport> archetypeCounters; // 이번 셀 전투에서 "그 함선 자신"이 겪은 아키타입 패시브 카운터(받은 데미지/실드 흡수량/요격 횟수) — 서버가 해당 아키타입에 누적
    public bool isMissileOnlyClear;  // 이번 셀 전투를 빔·함재기 없이 미사일만 써서 이겼는지 — 일일퀘스트 집계용(클라 보고값)
    public bool isAircraftOnlyClear; // 이번 셀 전투를 빔·미사일 없이 함재기만 써서 이겼는지 — 일일퀘스트 집계용(클라 보고값)
    public bool isNoShieldClear;     // 이번 셀 전투에서 실드가 피해를 한 번도 흡수하지 않았는지 — 일일퀘스트 집계용(클라 보고값)
    public int shieldAbsorbAmount;   // 이번 셀 전투에서 내 함대 실드가 막은 피해 합계 — 일일퀘스트 집계용(클라 보고값)
    public int interceptCount;       // 이번 셀 전투에서 내 함대 요격체가 요격한 횟수 — 일일퀘스트 집계용(클라 보고값)
    public int fleetRepairAmount;    // 이번 셀 전투에서 내 함대가 수리한 체력 합계(수리 전술 + 아군 수리) — 일일퀘스트 집계용(클라 보고값)
}

[System.Serializable]
public class ArchetypeCounterReport
{
    public string hullArchetype; // 카운터를 겪은 함선의 아키타입
    public string counterType;   // damage_taken_cumulative / shield_absorb_amount / intercept_count / scout_success
    public long value;           // 이번 셀 전투에서의 누적량
}

[System.Serializable]
public class ClearExplorationCellResponse
{
    public int mineralGained;          // 이 클리어로 방금 지급된 광물 합계(존 고정값 + Treasure/VIP/버프 반영분 + 약탈 + 탐사) — mineralAmount는 누적 총량, 이건 델타
    public int scavengedMineral;       // mineralGained 중 함재기 약탈분 — 클리어 보상 화면에서 따로 표시
    public int scoutedMineral;         // mineralGained 중 탐사(argus)분 — 클리어 보상 화면에서 따로 표시
    public int expGained;              // 이 클리어로 방금 지급된 지휘관 경험치(존의 셀당 경험치, VIP 2배 반영) — 재방문/빈 셀은 0
    public int totalExp;               // 지급 반영 후 지휘관 누적 경험치(권위값)
    public int commanderLevel;         // 지급 반영 후 지휘관 레벨(권위값, 레벨업 반영)
    public List<string> rewardCardCandidates; // 서버가 추첨한 보상카드 후보 3개(cardId) — 탈출 셀이거나 보상이 없는 셀(빈 셀)은 null
    public List<TreasureChoiceInfo> treasureChoices; // Treasure 셀 최초 클리어일 때만 채워지는 무료 선택지 3개 — 지급은 select-treasure-reward에서, 평소엔 null
    public int resource;                        // 이 클리어 확정 후 서버가 확정한 자원 현재치(권위값) — 항상 채워짐
    public int mineralAmount;                       // 이 클리어 확정 후 서버에 저장된 광물 현재치(존 고정지급분까지 서버가 반영한 권위값)
    public int rerollRemain;                       // 현재 보상카드 리롤 가능 횟수(충전식, 계정 전체 공유) — rewardCardCandidates가 null이면 무의미
    public List<HullArchetypeProgressInfo> hullArchetypePassiveProgress; // 이 클리어 반영 후 아키타입 패시브 해금 카운터 전체 목록(권위값) — 패시브 탭 진행도 갱신용
    public FleetInfo fleetInfo; // 이 클리어로 실드/요격체 해금 단계가 완료됐을 때만 채워지는 갱신된 내 함대(권위값) — 다음 셀 전투부터 반영, 평소엔 null
}

[System.Serializable]
public class TreasureChoiceInfo
{
    public ETreasureRewardType rewardType;
    public int mineralAmount; // rewardType이 Mineral일 때 지급될 광물(배율/VIP 반영 완료)
    public float ratio;       // rewardType이 ShipHealthHeal/ResourceCharge일 때 비율(0~1)
}

[System.Serializable]
public class SelectTreasureRewardRequest
{
    public int zoneNumber;
    public ETreasureRewardType rewardType; // 서버가 내려준 선택지 중 하나
}

[System.Serializable]
public class SelectTreasureRewardResponse
{
    public ETreasureRewardType rewardType;
    public float ratio;          // ShipHealthHeal/ResourceCharge 비율 — 체력 회복은 클라가 이 비율로 적용
    public int mineralGained;    // Mineral 선택 시 지급된 광물, 그 외 0
    public int mineralAmount;    // 지급 반영 후 런의 광물 현재치(권위값)
    public int resource;         // 지급 반영 후 런의 전투자원 현재치(권위값)
}

[System.Serializable]
public class GetRewardCardShopRequest { } // 셀 클리어와 무관하게 언제든 보상카드 구매 UI를 열기 위한 조회 — 진행 중인 런의 현재 셀 후보를 그대로 돌려주거나(없으면 새로 추첨) 광물/경험치 안내는 없음

[System.Serializable]
public class GetRewardCardShopResponse
{
    public List<string> rewardCardCandidates;
    public int rerollRemain;
}

[System.Serializable]
public class PurchaseRewardCardRequest
{
    public int zoneNumber;
    public int cellRow;
    public int cellCol;
    public string cardId; // ClearExplorationCellResponse.rewardCardCandidates 중 하나여야 함(서버가 검증)
    public int mineralSpent; // 이 카드의 구매 비용(클라가 계산) — 서버는 검증 없이 그대로 차감
}

[System.Serializable]
public class PurchaseRewardCardResponse
{
    public List<string> rewardCardCandidates; // 구매 직후 서버가 새로 추첨한 후보 3개(cardId) — 같은 팝업에서 계속 구매 가능하도록 즉시 갱신
    public int mineralAmount; // 차감 반영된 이번 런의 광물 현재치
}

[System.Serializable]
public class CloseRewardCardPopupRequest
{
    public int zoneNumber;
    public int cellRow;
    public int cellCol;
}

[System.Serializable]
public class CloseRewardCardPopupResponse { } // 팝업을 닫았다는 사실만 기록(재접속 시 재노출 방지) — 반환 데이터 없음

[System.Serializable]
public class RerollRewardCardRequest
{
    public int zoneNumber;
    public int cellRow;
    public int cellCol;
}

[System.Serializable]
public class RerollRewardCardResponse
{
    public List<string> rewardCardCandidates; // 서버가 새로 추첨한 후보 3개(cardId)
    public int rerollRemain; // 이번 리롤 이후 남은 리롤 가능 횟수(충전식, 계정 전체 공유)
}

[System.Serializable]
public class WatchAdForRewardCardRerollChargeRequest { } // 리롤 횟수 0 상태에서 광고 시청(또는 VIP) 충전 — 계정당 횟수라 셀/존 정보 불필요

[System.Serializable]
public class WatchAdForRewardCardRerollChargeResponse
{
    public int rewardCardRerollCharge; // 충전 반영된 현재 리롤 가능 횟수
}

[System.Serializable]
public class GetActiveZoneRunProgressRequest { } // 진행 중인 탐험 런의 클리어 셀 목록 조회 — 커맨더당 IN_PROGRESS 런은 항상 1개뿐이라 zoneNumber 불필요

[System.Serializable]
public class GetActiveZoneRunProgressResponse
{
    public int zoneNumber;        // 진행 중인 런이 없으면 0
    public string[] clearedCells; // "row-col"(0-indexed) 목록, 클리어 순서대로
    public List<ShipHealthRatioInfo> shipHealthRatios; // 마지막 셀 클리어 시점에 저장된 내 함대 체력 스냅샷 — 없으면 null(만피로 스폰된 상태 그대로)
    public List<string> purchasedRewardCards; // 이번 런에서 구매 확정한 보상카드(cardId) 전체 목록 — 지속버프 여부는 클라가 DataTableRewardCard로 재판별
    public List<string> pendingRewardCardCandidates; // 마지막 클리어 셀에 후보는 있는데 아직 팝업을 닫지 않았으면 그 후보 3개 — 팝업이 뜨기 전에 앱이 꺼진 경우 재접속 시 복구용, 없으면 null
    public int rerollRemain;                          // 현재 보상카드 리롤 가능 횟수(충전식) — pendingRewardCardCandidates가 null이면 무의미
    public List<TreasureChoiceInfo> pendingTreasureChoices; // Treasure 셀 무료 보상을 아직 고르지 않았으면 그 선택지 3개 — 재접속 시 팝업 복구용, 없으면 null
    public int mineralAmount;                         // 진행 중인 런의 광물 현재치, 없으면 0
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
    public int expGained;                // 확정 지급된 지휘관 경험치
    public int totalExp;                 // 반영 후 누적 경험치(권위값)
    public int commanderLevel;           // 반영 후 커맨더 레벨(레벨업 없으면 기존과 동일)
    public int highestClearedZoneNumber; // 탈출 성공 시 갱신된 값(권위값) — 클라 GetInitialZoneIndex()가 이 값 기준으로 다음 존을 계산
    public int resource;              // 런 종료로 회복된 자원 현재치(=resourceMax, 권위값)
    public bool hasUnclaimedAchievement; // 이 존런 정산 시점 기준 완료+미수령 업적 존재 여부(업적 버튼 레드닷용)
    public List<HullArchetypeProgressInfo> hullArchetypePassiveProgress; // 이 정산 반영 후 아키타입 패시브 해금 카운터 전체 목록(권위값) — 패시브 탭 진행도 갱신용
    public FleetInfo fleetInfo; // 이 정산으로 실드/요격체 해금 단계가 완료됐을 때만 채워지는 갱신된 내 함대(권위값) — 평소엔 null
}

[System.Serializable]
public class AbandonZoneRunRequest { } // 다른 존 도전 전 진행 중인 런을 명시적으로 포기 — 커맨더당 IN_PROGRESS 런은 항상 1개뿐이라 zoneNumber 불필요. 포기는 보상(경험치) 없음

[System.Serializable]
public class AbandonZoneRunResponse
{
    public int expGained;              // 포기로 지급된 지휘관 경험치 — 포기는 보상 없음이라 항상 0(지휘관 경험치는 존런을 성공적으로 클리어했을 때만 지급)
    public int totalExp;               // 반영 후 누적 경험치(권위값)
    public int commanderLevel;         // 반영 후 커맨더 레벨(레벨업 없으면 기존과 동일)
    public int resource;            // 런 종료로 회복된 자원 현재치(=resourceMax, 권위값)
    public bool hasUnclaimedAchievement; // 이 존런 정산 시점 기준 완료+미수령 업적 존재 여부(업적 버튼 레드닷용)
}

[System.Serializable]
public class ChargeResourceRequest
{
    // 진행 중인 런의 광물을 소모해 자원 게이지를 목표치까지 채움 — IN_PROGRESS 런은 1개뿐이라 zoneNumber 불필요. 환산량은 서버가 권위적으로 계산(ZoneRun.resource/mineralAmount 갱신)
    public int targetResource; // 충전 후 도달할 자원 목표치 — 서버가 [현재치, resourceMax]로 제한하고, 광물이 모자라면 살 수 있는 만큼만 충전
}

[System.Serializable]
public class ChargeResourceResponse
{
    public int resource;      // 충전 반영 후 자원 현재치(권위값)
    public int mineralAmount; // 충전 반영 후 광물 현재치(권위값) — 충전할 게 없었으면(만피/광물0) 변화 없이 그대로
}

[System.Serializable]
public class UnlockHullRequest
{
    public string hullSubType; // 언락할 티어4+ 함체
}

[System.Serializable]
public class UnlockHullResponse
{
    public string hullSubType;          // 언락 완료된 함체
    public int achievementPointRemain;  // 소모 후 보유량
    public List<string> unlockedHulls;  // 갱신된 전체 언락 목록(권위값) — 클라 로컬 캐시 갱신용
}

[System.Serializable]
public class UpgradeArchetypeModuleRequest
{
    public string hullArchetype;
    public EModuleType moduleType;
    public int slotIndex;  // beam/missile/hangar만 의미 있음(슬롯별 독립 레벨) — hull/shield/interceptor는 항상 0
}

[System.Serializable]
public class UpgradeArchetypeModuleResponse
{
    public string hullArchetype;
    public EModuleType moduleType;
    public int slotIndex;
    public int level;                   // 레벨업 완료 후 레벨(권위값)
    public int achievementPointRemain;  // 소모 후 보유량
}

[System.Serializable]
public class AchievementStatus
{
    // 업적 항목 1개의 동적(커맨더별) 상태 — 정적 정의(조건/임계값/보상)는 클라가 DataTableAchievement로 이미 보유
    public string achievementId;
    public int currentValue; // 서버가 조건 타입별로 계산한 현재 진행도
    public bool isClaimed;
    public bool isVipClaimed; // VIP 전용 보상 수령 여부 — 일반 보상과 완전히 별개
}

[System.Serializable]
public class GetAchievementListRequest { }

[System.Serializable]
public class GetAchievementListResponse
{
    public List<AchievementStatus> achievements;
}

[System.Serializable]
public class ClaimAchievementRequest
{
    public string achievementId;
    public bool claimVip; // true면 VIP 전용 보상을 수령 — 일반 보상과 같은 엔드포인트를 이 플래그로 구분
}

[System.Serializable]
public class ClaimAchievementResponse
{
    public string achievementId;
    public int achievementPointReward; // 이번 수령으로 지급된 양
    public int achievementPointRemain; // 지급 후 보유량
}

[System.Serializable]
public class ClaimAllAchievementsRequest { }

[System.Serializable]
public class ClaimAllAchievementsResponse
{
    public List<string> claimedAchievementIds;    // 이번 호출로 수령 처리된 업적 id 목록
    public int totalAchievementPointGranted;       // 이번 호출로 지급된 총 업적포인트
    public int achievementPointRemain;             // 지급 후 보유량
}

[System.Serializable]
public class DailyAchievementStatus
{
    // 일일 업적 항목 1개의 동적(커맨더별, 오늘 기준) 상태 — 정적 정의(조건/임계값/보상)는 클라가 DataTableDailyAchievement로 이미 보유
    public string achievementId;
    public int currentValue; // 서버가 조건 타입별로 "오늘(UTC)" 기준 계산한 현재 진행도
    public bool isClaimed;   // 오늘 이미 수령했는지
    public bool isVipClaimed; // VIP 전용 보상을 오늘 이미 수령했는지 — 일반 보상과 완전히 별개
}

[System.Serializable]
public class GetDailyAchievementListRequest { }

[System.Serializable]
public class GetDailyAchievementListResponse
{
    public List<DailyAchievementStatus> achievements;
    public int streakDays; // 오늘 보상에 적용 중인 연속 달성 일수(최대치로 보정됨) — 가산 %는 클라가 UIDailyStreakBanner 규칙으로 계산
}

[System.Serializable]
public class ClaimDailyAchievementRequest
{
    public string achievementId;
    public bool claimVip; // true면 VIP 전용 보상을 수령 — 일반 보상과 같은 엔드포인트를 이 플래그로 구분
}

[System.Serializable]
public class ClaimDailyAchievementResponse
{
    public string achievementId;
    public int achievementPointReward; // 이번 수령으로 지급된 양
    public int achievementPointRemain; // 지급 후 보유량
}

[System.Serializable]
public class ClaimAllDailyAchievementsRequest { }

[System.Serializable]
public class ClaimAllDailyAchievementsResponse
{
    public List<string> claimedAchievementIds;    // 이번 호출로 수령 처리된 일일 업적 id 목록
    public int totalAchievementPointGranted;       // 이번 호출로 지급된 총 업적포인트
    public int achievementPointRemain;             // 지급 후 보유량
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
    public List<HullArchetypeProgressInfo> passiveProgress;  // 상대 패시브 진행도 — 최종 전투 스탯 계산 입력(화면에 단계 자체는 노출하지 않음)
    public List<ArchetypeUpgradeInfo> archetypeUpgrades;      // 상대 아키타입 강화 레벨 — 최종 전투 스탯 계산 입력(화면에 레벨 자체는 노출하지 않음)
}

[System.Serializable]
public class PvpOpponentDetailRequest
{
    public long targetCommanderId; // 상세 정보를 볼 지휘관(PVP 상대 카드/랭킹 항목)
}

[System.Serializable]
public class PvpOpponentDetailResponse
{
    // 상대 상세 정보 — 항상 실시간 값. 클라가 fleetInfo + 빌드 데이터로 전투와 같은 규칙의 최종 스탯을 계산해 표시
    public long commanderId;
    public string commanderName;
    public int commanderLevel;
    public int pvpScore;      // PVP 기록이 없으면 0
    public int rank;          // PVP 순위, 기록이 없으면 0
    public int wins;
    public int losses;
    public FleetInfo fleetInfo; // 활성 함대 — 없으면 null
    public List<HullArchetypeProgressInfo> passiveProgress;
    public List<ArchetypeUpgradeInfo> archetypeUpgrades;
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
    public List<HullArchetypeProgressInfo> opponentPassiveProgress; // 상대 지휘관의 아키타입 패시브 진행도 전체(권위값) — 상대 함대에도 같은 패시브 능력치 효과를 적용하기 위함
    public List<ArchetypeUpgradeInfo> opponentArchetypeUpgrades; // 상대 지휘관의 아키타입 모듈강화 레벨 전체(구매 횟수, 권위값) — 상대 함선이 상대 자신의 강화 레벨로 만들어지게 함
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
    public bool isPerfectClear;  // Zone 랭킹 전용 - 1존부터 score(=highestClearedZoneNumber)까지 전부 완벽클리어(이탈 없이 전체 셀 클리어)했는지, PVP는 항상 false
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
public class FleetRemoveShipRequest
{
    // 함대편성 슬롯의 함선을 배치 해제 — 해당 슬롯의 함선+장착 모듈을 서버에서 완전히 삭제
    public int slotIndex;
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
public class DailyBonusStatusResponse
{
    // 출석 달력 조회 전용 — 지급 없이 현재 상태만 확인(로그인 시 레드닷 갱신용). 이 호출 자체가 출석 1회로 카운트됨
    public bool available;              // 열린 칸(1~todayDay) 중 미수령이 남아있는지
    public int todayDay;                // 출석일수 — 접속한 서로 다른 날짜 수(1~6), 이 값 이하의 미수령 칸은 전부 클레임 가능
    public int claimedDaysMask;         // 이번 주 수령 현황 비트마스크 (bit0=1일, bit5=6일)
    public int vipClaimedDaysMask;      // VIP 보상 수령 현황 비트마스크 (bit0=1일, bit5=6일)
}

[System.Serializable]
public class DailyClaimRequest
{
    public int day; // 수령할 칸(1~6) — 출석일수(todayDay) 이하이고 아직 미수령인 날짜만 서버에서 허용
    public bool claimVip; // true면 VIP 전용 보상을 수령 — 일반 보상과 완전히 별개(같은 엔드포인트를 이 플래그로 구분)
}

[System.Serializable]
public class DailyClaimResponse
{
    public bool available;              // true=지급됨, false=이미 수령했거나 테이블 없음
    public int grantedAchievementPoint; // 이번에 지급된 업적 포인트 양
    public int achievementPointRemain;  // 지급 후 현재 업적 포인트
    public int todayDay;                // 출석일수 — 접속한 서로 다른 날짜 수(1~6), 이 값 이하의 미수령 칸은 전부 클레임 가능
    public int claimedDaysMask;         // 이번 주 수령 현황 비트마스크 (bit0=1일, bit5=6일)
    public int vipClaimedDaysMask;      // VIP 보상 수령 현황 비트마스크 (bit0=1일, bit5=6일)
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

[System.Serializable]
public class NoticeText
{
    public string locale; // 클라 로케일 코드(en, ko 등)
    public string title;
    public string body;
}

[System.Serializable]
public class NoticeItem
{
    public long noticeId;
    public int version;
    public List<NoticeText> texts; // 언어별 제목/본문 — 클라가 현재 언어로 골라 표시(NoticeManager.PickText)
    public bool isRead; // 이 지휘관이 현재 버전 본문을 열어봤는지 — 목록 행/공지 버튼 레드닷 기준
}

[System.Serializable]
public class NoticeResponse
{
    public List<NoticeItem> notices; // /notice/list: 활성 공지 전체(display_order 순), 항목마다 읽음 여부 포함
}

[System.Serializable]
public class NoticeReadRequest
{
    public long noticeId;
    public int version; // 클라가 열어본 본문의 버전 — 서버 현재 버전과 같을 때만 읽음 처리
}

[System.Serializable]
public class NoticeReadResponse
{
    public bool hasUnread; // 읽음 처리 후에도 안 읽은 활성 공지가 남았는지(공지 버튼 레드닷)
}
#endregion
#endregion
