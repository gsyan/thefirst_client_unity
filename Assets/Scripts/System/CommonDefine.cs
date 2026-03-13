// 게임 공통 열거형 정의 — EModuleType, EModuleSubType(7자리 인코딩), EFormationType 등
// EModuleSubType 파싱: type=val/1000000, tech=(val/10000)%100, grade=(val/100)%100, ver=val%100

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
    engine          = 2,
    beam            = 3,
    missile         = 4,
    hanger          = 5,
    max             = 6
}
// 7자리 인코딩: T_tt_gg_vv (type 1자리, tech 2자리, grade 2자리, ver 2자리)
// 파싱: type=val/1000000, tech=(val/10000)%100, grade=(val/100)%100, ver=val%100
// grade: 01=std, 02=adv / ver: 01~99
[System.Serializable]
public enum EModuleSubType
{
    none                    = 0,
    // Body SubType
    body_t1_std_ver1        = 1010101,
    body_t1_adv_ver1        = 1020201,
    // Engine SubType
    engine_t1_std_ver1      = 2010101,
    engine_t1_adv_ver1      = 2020201,
    // Beam SubType
    beam_t1_std_ver1        = 3010101,
    beam_t1_adv_ver1        = 3020201,
    // Missile SubType
    missile_t1_std_ver1     = 4010101,
    missile_t1_adv_ver1     = 4020201,
    // Hanger SubType
    hanger_t1_std_ver1      = 5010101,
    hanger_t1_adv_ver1      = 5020201,
}

// EModuleSubType 7자리 인코딩 파싱 유틸
public static class EModuleSubTypeExtensions
{
    public static int GetTechTier(this EModuleSubType subType) => ((int)subType / 10000) % 100;
    public static int GetGrade(this EModuleSubType subType)    => ((int)subType / 100)   % 100;
}

public static class EModuleTypeExtensions
{
    public static UnityEngine.Color GetColorByModuleType(this EModuleType moduleType)
    {
        switch (moduleType)
        {
            case EModuleType.body:
                return new UnityEngine.Color(0.7f, 0.9f, 0.7f);
            case EModuleType.engine:
                return new UnityEngine.Color(0.7f, 0.7f, 0.9f);
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
    formation_type_linear_horizontal,   // 가로 일렬 배치
    formation_type_circle,              // 원형 배치
    formation_type_cross,               // 십자 배치
    formation_type_x                    // X자 배치
}
#endregion