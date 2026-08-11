// 게임 공통 열거형 정의 — EModuleType, EModuleSubType(7자리 인코딩), EFormationType 등
[System.Serializable]
public enum EModuleType
{
    none            = 0,
    body            = 1,
    beam            = 2,
    missile         = 3,
    hanger          = 4,
    shield          = 5,
    interceptor     = 6,
    max             = 7
}
// 6자리 인코딩: T_tt_mmm (type 1자리, 타입(외형) 2자리, m 3자리)
// 파싱: type=val/100000, tier=(val/1000)%100, m=val%1000
// body만 m이 의미를 가짐(예: m111 = 빔1/미사일1/격납고1 슬롯 수) — 그 외 타입은 m=000 고정
[System.Serializable]
public enum EModuleSubType
{
    none                = 0,
    // Body SubType
    body_t1_m111        = 101111,
    body_t1_m211        = 101211,
    body_t1_m221        = 101221,
    body_t1_m222        = 101222,
    body_t1_m322        = 101322,
    body_t1_m332        = 101332,   
    body_t1_m333        = 101333,
    body_t1_m433        = 101433,
    body_t1_m443        = 101443,
    body_t1_m444        = 101444,
    body_t1_m544        = 101544,
    body_t1_m554        = 101554,
    // Beam SubType
    beam_t1             = 201000,
    // Missile SubType
    missile_t1          = 301000,
    // Hanger SubType
    hanger_t1           = 401000,
    // Shield SubType
    shield_t1           = 501000,
    // Interceptor SubType
    interceptor_t1      = 601000,
}

// EModuleSubType 6자리 인코딩 파싱 유틸
public static class EModuleSubTypeExtensions
{
    public static int GetModuleType(this EModuleSubType subType)    => (int)subType / 100000;
    public static int GetTechTier(this EModuleSubType subType) => ((int)subType / 1000) % 100;
    public static int GetModuleModel(this EModuleSubType subType)    => (int)subType % 1000;

    // 인코딩에서 EModuleType 추출
    public static EModuleType GetModuleTypeEnum(this EModuleSubType subType)
        => (EModuleType)((int)subType / 100000);

    // 타입(외형)+1 서브타입 반환 (없으면 EModuleSubType.none) — prerequisites 체인 없이 인코딩 산술로 계산
    public static EModuleSubType GetNextSubType(this EModuleSubType subType)
    {
        int nextVal = (int)subType + 1000;
        return System.Enum.IsDefined(typeof(EModuleSubType), nextVal) ? (EModuleSubType)nextVal : EModuleSubType.none;
    }

    // 타입(외형)-1 서브타입 반환 (없으면 EModuleSubType.none)
    public static EModuleSubType GetPrevSubType(this EModuleSubType subType)
    {
        int prevVal = (int)subType - 1000;
        return System.Enum.IsDefined(typeof(EModuleSubType), prevVal) ? (EModuleSubType)prevVal : EModuleSubType.none;
    }

    // 로컬라이즈된 서브타입 표시명 생성 (예: "함체.T1.M111")
    // CSV에 개별 키 없이, module_type_{type} 키 + tier/model 조합으로 동적 생성
    public static string GetLocalizedName(this EModuleSubType subType)
    {
        string typeName = LocalizationManager.Instance.Get($"module_type_{subType.GetModuleTypeEnum()}");
        int tier = subType.GetTechTier();
        int model = subType.GetModuleModel();
        return $"{typeName}.T{tier}.M{model}";
    }
}

public static class EModuleTypeExtensions
{
    public static UnityEngine.Color GetColorByModuleType(this EModuleType moduleType)
    {
        switch (moduleType)
        {
            case EModuleType.body:
                return new UnityEngine.Color(0.7f, 0.9f, 0.7f);
            case EModuleType.beam:
                return new UnityEngine.Color(0.9f, 0.7f, 0.7f);
            case EModuleType.missile:
                return new UnityEngine.Color(0.9f, 0.7f, 0.7f);
            case EModuleType.hanger:
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
    Buff_HangarShipAttack,        // 지속버프: 함재기 대함 공격력 x(1+value1)
    Buff_HangarFighterAttack,     // 지속버프: 함재기 대함재기 공격력 x(1+value1)
    Buff_ShipHealth,              // 지속버프: 체력 x(1+value1)
    Buff_ExplorationPointRate,    // 지속버프: 탐험 포인트 획득률 x(1+value1)
    Instant_HealthHeal,           // 즉시효과: 체력 value1(0~1) 비율만큼 회복
    Instant_ShieldHeal,           // 즉시효과: 실드 value1 비율만큼 회복
    Instant_InterceptorHeal,      // 즉시효과: 요격체 value1 비율만큼 회복
    Instant_ExplorationPointFlat, // 즉시효과: 탐험 포인트 +value1 가산(비율 버프보다 먼저 적용)
}
#endregion