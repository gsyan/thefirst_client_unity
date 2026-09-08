// 게임 공통 열거형 정의 — EModuleType, EFormationType 등
// 모듈 서브타입(구 EModuleSubType)은 enum이 아니라 문자열로 관리한다.
// 이름 규칙: hull은 "hull_{tier}_{gen}_{5자리구성(빔,미사일,격납고,실드,요격체)}", 그 외는 "{type}_{tier}_{gen}"
// tier = 5자리 구성 중 빔+미사일+격납고 3자리 합만 사용(실드/요격체 제외), gen = 세대(외형) 구분자
// 파싱 유틸은 CommonUtility.cs의 "Module Type" 영역 참고
[System.Serializable]
public enum EModuleType
{
    none            = 0,
    hull            = 1,
    beam            = 2,
    missile         = 3,
    hangar          = 4,
    shield          = 5,
    interceptor     = 6,
    max             = 7
}

public static class EModuleTypeExtensions
{
    public static UnityEngine.Color GetColorByModuleType(this EModuleType moduleType)
    {
        switch (moduleType)
        {
            case EModuleType.hull:
                return new UnityEngine.Color(0.7f, 0.9f, 0.7f);
            case EModuleType.beam:
                return new UnityEngine.Color(0.9f, 0.7f, 0.7f);
            case EModuleType.missile:
                return new UnityEngine.Color(0.9f, 0.7f, 0.7f);
            case EModuleType.hangar:
                return new UnityEngine.Color(0.9f, 0.9f, 0.7f);
            case EModuleType.shield:
                return new UnityEngine.Color(0.7f, 0.85f, 0.95f);
            case EModuleType.interceptor:
                return new UnityEngine.Color(0.85f, 0.75f, 0.95f);
            default:
                return UnityEngine.Color.white;
        }
    }


}

#region Fleet Formation
[System.Serializable]
public enum EFormationType
{
    linear_horizontal,   // 가로 일렬 배치, 균형 (보너스 없음)
    x,                   // x 배치, 한점 집중 사격
    cross,               // 십자 배치, 회복력 증가
    circle,              // 원형 배치, 기함방어우선
}

public enum FormationMoveState
{
    Idle,       // 정지 (진형 이동 없음)
    Moving,     // 목표 위치로 이동 중 (실드 트리거 회피 포함)
    Arrived     // 목표 위치 도착 완료
}
#endregion

#region Daily Bonus
[System.Serializable]
public enum EDailyBonusTier { Normal, VIP }

[System.Serializable]
public enum EDailyBonusRewardType { ExplorationPoint, AchievementPoint }

// 탐사 그리드 셀 타입 — Normal은 목록에 없는 좌표의 기본값(희소 저장이라 별도 항목을 만들지 않음)
[System.Serializable]
public enum EGridCellType
{
    Blocked, // 통행 불가
    Start,   // 시작점
    Escape,  // 탈출점
    Event,   // 이벤트 셀 — 세부 종류는 EGridEventType 참고
}

// Event 셀의 세부 종류 — 지금은 Treasure만 실제로 동작, 나머지는 향후 확장용으로 미리 늘려둠(파라미터화는 필요 시 추가)
[System.Serializable]
public enum EGridEventType
{
    Treasure, // 탐사포인트/함선체력회복/전술력회복 중 랜덤 1개 지급 — 세부 보상은 ETreasureRewardType 참고
    Trap,     // TODO: 미구현 — 함선 체력 등 페널티
    Merchant, // TODO: 미구현 — 상인 조우
}

// Treasure(Event) 셀 클리어 시 랜덤 지급되는 보상 종류 — 매번 진짜 랜덤(결정론적 시드 아님)으로 서버가 1개 선택
[System.Serializable]
public enum ETreasureRewardType
{
    None,               // 이 클리어가 Treasure 보상이 아님(일반 전투/재방문 등)
    ExplorationPoint,   // 그 존의 일반 전투 셀 보상의 배율 지급
    ShipHealthHeal,     // 함대 전체 체력 비율 회복
    TacticPowerRestore, // 전술력 전액 회복
}

// 업적 조건 타입 — AchievementData.conditionType. conditionParam 해석은 타입별로 다름(예: ZoneClearSpecific="존번호", EventCell="ETreasureRewardType 이름",
// HullTierCount="티어 숫자", ModuleTierCount="{EModuleType 이름}_{티어}"), 나머지 타입은 conditionParam 미사용
[System.Serializable]
public enum EAchievementConditionType
{
    CellClear,             // 일반 셀(비이벤트) 누적 클리어수
    EventCell,              // 이벤트 셀(ETreasureRewardType 종류별) 누적 클리어수
    ZoneClearTotal,        // 누적 존 클리어 개수(Commander.highestClearedZoneNumber 기준)
    ZoneClearSpecific,     // 특정 존 번호 클리어 여부
    CommanderLevel,        // 지휘관 레벨 스냅샷
    CommandPower,          // 지휘력(commandPowerMax) 스냅샷
    TacticPower,           // 전술력(tacticPowerMax) 스냅샷
    ExplorationPointTotal, // 역대 누적 획득 탐사포인트(Commander.explorationPointEarnedTotal)
    HullTierCount,         // 활성 함대 내 특정 티어 함체 동시보유 개수
    ModuleTierCount,       // 활성 함대 내 특정 카테고리+티어 모듈 동시보유 개수
}

// 존 진행(ZoneRun) 상태 — 서버 엔티티 필드용. 클라는 직접 이 값을 받지 않고 EscapeExplorationZoneRequest.isSuccess(bool)로만 결과를 통지하지만,
// 모든 enum은 이 파일에서 생성해 서버와 동기화하는 프로젝트 관례를 따름
[System.Serializable]
public enum EZoneRunStatus
{
    IN_PROGRESS,
    ESCAPED,
    ABANDONED,
}

// 셀 클리어 보상카드 효과 종류 — 수치(1~5% 등)는 enum이 아니라 RewardCardData.value1/value2에 카드마다 다르게 저장됨(효과 종류와 수치 분리)
// 접두사로 지속버프(Buff_)/즉시효과(Instant_)를 구분 — 신규 효과는 이 접두사 규칙을 따라 값만 추가하면 됨(범용 확장 설계)
[System.Serializable]
public enum ECardEffectType
{
    Buff_BeamAttack,              // 지속버프: 빔 공격력 x(1+value1)
    Buff_BeamFireRate,            // 지속버프: 빔 연사속도(쿨다운 감소) x(1+value1)
    Buff_MissileAttack,           // 지속버프: 미사일 공격력 x(1+value1)
    Buff_MissileFireRate,         // 지속버프: 미사일 연사속도(쿨다운 감소) x(1+value1)
    Buff_MissileSilence,          // 지속버프: 미사일 침묵효과(silenceTime) x(1+value1)
    Buff_HangarAttackToShip,      // 지속버프: 함재기 대함 공격력(Phase_AttackShip) x(1+value1)
    Buff_HangarAttackToFighter,   // 지속버프: 함재기 대함재기 공격력(Dogfight) x(1+value1)
    Buff_ShipHealth,              // 지속버프: 체력 x(1+value1)
    Buff_ExplorationPointRate,    // 지속버프: 탐험 포인트 획득률 x(1+value1)
    Instant_HealthHeal,           // 즉시효과: 체력 value1(0~1) 비율만큼 회복
    Instant_ShieldHeal,           // 즉시효과: 실드 value1 비율만큼 회복
    Instant_InterceptorHeal,      // 즉시효과: 요격체 value1 비율만큼 회복
    Instant_ExplorationPointFlat, // 즉시효과: 탐험 포인트 +value1 가산(비율 버프보다 먼저 적용)
}
#endregion