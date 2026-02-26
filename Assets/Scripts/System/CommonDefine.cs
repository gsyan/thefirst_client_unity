// ------------------------------------------------------------

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
// 3001~3009 연관, 3011~3019 연관
[System.Serializable]
public enum EModuleSubType
{
    none                = 0,
    // Body SubType 
    body_t1_std         = 1001,
    body_t1_adv         = 1002,
    // Engine SubType
    engine_t1_std       = 2001,
    engine_t1_adv       = 2002,    
    // Beam SubType
    beam_t1_std         = 3001,
    beam_t1_adv         = 3002,
    // Missile SubType    
    missile_t1_std      = 4001,
    missile_t1_adv      = 4002,
    // HangerSubType
    hanger_t1_std       = 5001,
    hanger_t1_adv       = 5002
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