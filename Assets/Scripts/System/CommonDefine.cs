// ------------------------------------------------------------
public class CommonDefine
{ 
    public const int MAX_MODULE_LEVEL = 10;
    public const int DEFAULT_CARGO_CAPACITY = 50;
    public const float DEFAULT_HEALTH = 100f;

}

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
    None            = 0,
    Body            = 1,
    Engine          = 2,
    Beam            = 3,
    Missile         = 4,
    Hanger          = 5,
    Max             = 6
}
[System.Serializable]
public enum EModuleSubType
{
    None                = 0,
    // Body SubType 
    Body_Battle         = 1001,
    Body_Aircraft       = 1002,
    Body_Repair         = 1003,
    // Engine SubType
    Engine_Standard     = 2001,
    Engine_Booster      = 2002,
    Engine_Warp         = 2003,
    // Beam SubType
    Beam_Standard       = 3001,
    Beam_Advanced       = 3002,
    // Missile SubType    
    Missile_Standard    = 4001,
    Missile_Advanced    = 4002,
    // HangerSubType
    Hanger_Standard     = 5001,
    Hanger_Advanced     = 5002
}

public static class EModuleTypeExtensions
{
    public static UnityEngine.Color GetColor(this EModuleType moduleType)
    {
        switch (moduleType)
        {
            case EModuleType.Body:
                return new UnityEngine.Color(0.7f, 0.9f, 0.7f);
            case EModuleType.Engine:
                return new UnityEngine.Color(0.7f, 0.7f, 0.9f);
            case EModuleType.Beam:
                return new UnityEngine.Color(0.9f, 0.7f, 0.7f);
            case EModuleType.Missile:
                return new UnityEngine.Color(0.9f, 0.7f, 0.7f);
            case EModuleType.Hanger:
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
    LinearHorizontal,   // 가로 일렬 배치
    Circle,             // 원형 배치
    Cross,              // 십자 배치
    X                   // X자 배치
}
#endregion