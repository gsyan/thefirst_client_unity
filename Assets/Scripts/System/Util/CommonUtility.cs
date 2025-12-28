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

    public static int CreateModuleTypePacked(EModuleType type, EModuleSubType subType, EModuleStyle style)
    {
        // SubType에서 순수한 서브타입 값만 추출 (1001 -> 1, 2002 -> 2)
        int pureSubType = (int)subType % 1000;
        return ((int)type << TYPE_SHIFT) | (pureSubType << SUBTYPE_SHIFT) | ((int)style << STYLE_SHIFT);
    }

    public static EModuleType GetModuleType(int moduleTypePacked)
    {
        return (EModuleType)((moduleTypePacked >> TYPE_SHIFT) & MASK);
    }

    public static EModuleSubType GetModuleSubType(int moduleTypePacked)
    {
        int pureSubType = (moduleTypePacked >> SUBTYPE_SHIFT) & MASK;
        if (pureSubType == 0)
            return EModuleSubType.None;

        // Type 정보를 가져와서 완전한 SubType 값으로 복원 (Type=1, SubType=1 -> 1001)
        EModuleType type = GetModuleType(moduleTypePacked);
        int fullSubType = (int)type * 1000 + pureSubType;
        return (EModuleSubType)fullSubType;
    }

    public static int GetModuleSubTypeValue(int moduleTypePacked)
    {
        return (moduleTypePacked >> SUBTYPE_SHIFT) & MASK;
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

    public static EModuleType GetModuleTypeFromSubType(EModuleSubType subType)
    {
        if (subType == EModuleSubType.None) return EModuleType.None;        
        int typeValue = (int)subType / 1000;
        return (EModuleType)typeValue;
    }

    #endregion Module Type Packing end -----------------------------------------------------------------------------------




    #region  begin -----------------------------------------------------------------------------------
    
    

    #endregion  end -----------------------------------------------------------------------------------
}