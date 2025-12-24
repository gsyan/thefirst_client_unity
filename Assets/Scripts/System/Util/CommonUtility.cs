using UnityEngine;

public static class CommonUtility
{
    #region Fleet Utility begin -----------------------------------------------------------------------------------
    public static Vector3 CalculateFleetCenter(Vector3[] shipPositions)
    {
        if (shipPositions == null || shipPositions.Length == 0)
            return Vector3.zero;
            
        Vector3 center = Vector3.zero;
        foreach (var position in shipPositions)
        {
            center += position;
        }
        
        return center / shipPositions.Length;
    }
    
    // Calculate fleet bounds
    public static Bounds CalculateFleetBounds(Vector3[] shipPositions, float shipSize = 2f)
    {
        if (shipPositions == null || shipPositions.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one);
            
        Bounds bounds = new Bounds(shipPositions[0], Vector3.one * shipSize);
        
        foreach (var position in shipPositions)
        {
            bounds.Encapsulate(new Bounds(position, Vector3.one * shipSize));
        }
        
        return bounds;
    }
    #endregion Fleet Utility end -----------------------------------------------------------------------------------

    #region Module Type Packing begin -----------------------------------------------------------------------------------
    private const int TYPE_SHIFT = 24;
    private const int SUBTYPE_SHIFT = 16;
    private const int STYLE_SHIFT = 8;
    private const int MASK = 0xFF;

    public static int CreateModuleTypePacked(EModuleType type, int subType, EModuleStyle style)
    {
        return ((int)type << TYPE_SHIFT) | (subType << SUBTYPE_SHIFT) | ((int)style << STYLE_SHIFT);
    }

    public static EModuleType GetModuleType(int moduleType)
    {
        return (EModuleType)((moduleType >> TYPE_SHIFT) & MASK);
    }

    public static T GetModuleSubType<T>(int moduleTypePacked) where T : System.Enum
    {
        return (T)(object)((moduleTypePacked >> SUBTYPE_SHIFT) & MASK);
    }
    public static int GetModuleSubType(int moduleTypePacked)
    {
        return moduleTypePacked >> SUBTYPE_SHIFT & MASK;
    }

    public static EModuleStyle GetModuleStyle(int moduleType)
    {
        return (EModuleStyle)((moduleType >> STYLE_SHIFT) & MASK);
    }

    public static int GetModuleTypeWithoutStyle(int moduleType)
    {
        return moduleType & unchecked((int)0xFFFF0000);
    }

    public static int GetModuleTypeOnly(int moduleType)
    {
        return moduleType & unchecked((int)0xFF000000);
    }

    public static bool CompareModuleTypeForSlot(int moduleType1, int moduleType2)
    {
        EModuleType type = GetModuleType(moduleType1);

        if (type == EModuleType.Weapon)
            return GetModuleTypeWithoutStyle(moduleType1) == GetModuleTypeWithoutStyle(moduleType2);

        return GetModuleTypeOnly(moduleType1) == GetModuleTypeOnly(moduleType2);
    }
    #endregion Module Type Packing end -----------------------------------------------------------------------------------




    #region  begin -----------------------------------------------------------------------------------
    
    #endregion  end -----------------------------------------------------------------------------------
}