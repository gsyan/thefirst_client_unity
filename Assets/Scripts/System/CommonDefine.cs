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
    None = 0,
    Body = 1,
    Engine = 2,
    Weapon = 3,
    Hanger = 4,

    Max = 5
}
[System.Serializable]
public enum EModuleBodySubType
{
    None = 0,
    Battle = 1,
    Aircraft = 2,
    Scout = 3,
    Repair = 4,
}
[System.Serializable]
public enum EModuleEngineSubType
{
    None = 0,
    Standard = 1,
    Booster = 2,
    Warp = 3
}
[System.Serializable]
public enum EModuleWeaponSubType
{
    None = 0,
    Beam = 1,
    Missile = 2,
    Cannon = 3,
    Torpedo = 4,
}
[System.Serializable]
public enum EModuleHangerSubType
{
    None = 0,
    Standard = 1,
    Advanced = 2,
    Military = 3
}
[System.Serializable]
public enum EModuleStyle
{
    None = 0,
    StyleA = 1,
    StyleB = 2,
    StyleC = 3,
    StyleD = 4
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
            case EModuleType.Weapon:
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
    LinearVertical,     // 세로 일렬 배치
    LinearDepth,        // 전후방 일렬 배치
    Grid,               // 격자 배치
    Circle,             // 원형 배치
    Cross,              // 십자 배치
    X                   // X자 배치
}
#endregion