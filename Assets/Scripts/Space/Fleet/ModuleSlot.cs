using UnityEngine;

public class ModuleSlot : MonoBehaviour
{
    public int m_moduleTypePacked;
    public EModuleType m_moduleType = EModuleType.None;
    public EModuleSubType m_moduleSubType = EModuleSubType.None;
    public EModuleSlotType m_moduleSlotType = EModuleSlotType.All;
    public int m_slotIndex = 0;

#if UNITY_EDITOR
    private void OnValidate()
    {
        m_moduleTypePacked = CommonUtility.CreateModuleTypePacked(m_moduleType, m_moduleSubType, m_moduleSlotType);
    }
#endif
}
