using UnityEngine;

public class HangerPort : MonoBehaviour
{
    public int m_moduleType;
    public int m_slotIndex = 0;

    [Header("Module Type Settings")]
    public EModuleType m_type = EModuleType.None;
    public EModuleBodySubType m_bodySubType = EModuleBodySubType.None;
    public EModuleWeaponSubType m_weaponSubType = EModuleWeaponSubType.None;
    public EModuleEngineSubType m_engineSubType = EModuleEngineSubType.None;
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        int subType = 0;

        if (m_type == EModuleType.Body)
            subType = (int)m_bodySubType;
        else if (m_type == EModuleType.Weapon)
            subType = (int)m_weaponSubType;
        else if (m_type == EModuleType.Engine)
            subType = (int)m_engineSubType;

        m_moduleType = CommonUtility.CreateModuleType(m_type, subType, EModuleStyle.None);
    }
#endif
}
