// 게임 공통 열거형 정의 — EModuleType, EModuleSubType(7자리 인코딩), EFormationType 등
[System.Serializable]
public enum ESpaceMineralState
{
    None = 0,
    Occupied = 1,
    End = 2,
    Max,
}

[System.Serializable]
public enum EModuleType
{
    none            = 0,
    body            = 1,
    beam            = 2,
    missile         = 3,
    hanger          = 4,
    max             = 5
}
// 7자리 인코딩: T_tt_gg_vv (type 1자리, tech 2자리, model 2자리)
// 파싱: type=val/10000, tech=(val/100)%100, model=val%100
[System.Serializable]
public enum EModuleSubType
{
    none                = 0,
    // Body SubType
    body_t1_m1          = 10101,
    body_t2_m1          = 10201,
    body_t3_m1          = 10301,
    body_t4_m1          = 10401,
    body_t5_m1          = 10501,
    body_t6_m1          = 10601,    
    body_t7_m1          = 10701,
    body_t8_m1          = 10801,
    body_t9_m1          = 10901,
    body_t10_m1         = 11001,
    body_t11_m1         = 11101,
    body_t12_m1         = 11201,
    body_t13_m1         = 11301,
    body_t14_m1         = 11401,
    // Beam SubType
    beam_t1_m1          = 20101,
    beam_t2_m1          = 20201,
    beam_t3_m1          = 20301,
    beam_t4_m1          = 20401,
    beam_t5_m1          = 20501,
    beam_t6_m1          = 20601,
    beam_t7_m1          = 20701,
    beam_t8_m1          = 20801,
    beam_t9_m1          = 20901,
    beam_t10_m1         = 21001,
    beam_t11_m1         = 21101,
    beam_t12_m1         = 21201,
    beam_t13_m1         = 21301,
    beam_t14_m1         = 21401,
    // Missile SubType
    missile_t1_m1       = 30101,
    missile_t2_m1       = 30201,
    missile_t3_m1       = 30301,
    missile_t4_m1       = 30401,
    missile_t5_m1       = 30501,
    missile_t6_m1       = 30601,
    missile_t7_m1       = 30701,
    missile_t8_m1       = 30801,
    missile_t9_m1       = 30901,
    missile_t10_m1      = 31001,
    missile_t11_m1      = 31101,
    missile_t12_m1      = 31201,
    missile_t13_m1      = 31301,
    missile_t14_m1      = 31401,
    // Hanger SubType
    hanger_t1_m1        = 40101,
    hanger_t2_m1        = 40201,
    hanger_t3_m1        = 40301,
    hanger_t4_m1        = 40401,
    hanger_t5_m1        = 40501,
    hanger_t6_m1        = 40601,
    hanger_t7_m1        = 40701,
    hanger_t8_m1        = 40801,
    hanger_t9_m1        = 40901,
    hanger_t10_m1       = 41001,
    hanger_t11_m1       = 41101,
    hanger_t12_m1       = 41201,
    hanger_t13_m1       = 41301,
    hanger_t14_m1       = 41401,
}

// EModuleSubType 7자리 인코딩 파싱 유틸
public static class EModuleSubTypeExtensions
{
    public static int GetModuleType(this EModuleSubType subType)    => (int)subType / 10000;
    public static int GetTechTier(this EModuleSubType subType) => ((int)subType / 100) % 100;
    public static int GetModuleModel(this EModuleSubType subType)    => (int)subType % 100;

    // 인코딩에서 EModuleType 추출
    public static EModuleType GetModuleTypeEnum(this EModuleSubType subType)
        => (EModuleType)((int)subType / 10000);

    // tier+1 서브타입 반환 (없으면 EModuleSubType.none) — prerequisites 체인 없이 인코딩 산술로 계산
    public static EModuleSubType GetNextSubType(this EModuleSubType subType)
    {
        int nextVal = (int)subType + 100;
        return System.Enum.IsDefined(typeof(EModuleSubType), nextVal) ? (EModuleSubType)nextVal : EModuleSubType.none;
    }

    // tier-1 서브타입 반환 (없으면 EModuleSubType.none)
    public static EModuleSubType GetPrevSubType(this EModuleSubType subType)
    {
        int prevVal = (int)subType - 100;
        return System.Enum.IsDefined(typeof(EModuleSubType), prevVal) ? (EModuleSubType)prevVal : EModuleSubType.none;
    }

    // 로컬라이즈된 서브타입 표시명 생성 (예: "함체.T1.M1")
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
    x_offensive,         // 전진 x 배치, 공격력 증가
    x_defensive,         // 후진 x 배치, 데미지 차감
    circle,              // 원형 배치, 기함방어우선
    cross_defensive,     // 십자 배치, 회복력 증가
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
public enum EDailyBonusRewardType { Mineral }
#endregion