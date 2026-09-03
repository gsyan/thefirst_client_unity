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
public enum EDailyBonusRewardType { ExplorationPoint }

// 탐사 그리드 셀 타입 — Normal은 목록에 없는 좌표의 기본값(희소 저장이라 별도 항목을 만들지 않음)
[System.Serializable]
public enum EGridCellType
{
    Blocked, // 통행 불가
    Start,   // 시작점
    Escape,  // 탈출점
    Event,   // 이벤트 셀 — 세부 종류는 EGridEventType 참고
}

// Event 셀의 세부 종류 — 지금은 NoEnemy만 실제로 동작, 나머지는 향후 확장용으로 미리 늘려둠(파라미터화는 필요 시 추가)
[System.Serializable]
public enum EGridEventType
{
    NoEnemy,  // 적 없음, 소액 보상만 (구 Empty와 동일 의도)
    Treasure, // TODO: 미구현 — 보물/자원 획득
    Trap,     // TODO: 미구현 — 함선 체력 등 페널티
    Merchant, // TODO: 미구현 — 상인 조우
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