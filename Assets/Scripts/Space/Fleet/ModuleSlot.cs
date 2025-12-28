using UnityEngine;

public class ModuleSlot : MonoBehaviour
{
    public int m_moduleTypePacked;
    public int m_slotIndex = 0;

    [Header("Module Type Settings")]
    public EModuleType m_moduleType = EModuleType.None;
    public EModuleSubType m_moduleSubType = EModuleSubType.None;

#if UNITY_EDITOR
    private void OnValidate()
    {
        m_moduleTypePacked = CommonUtility.CreateModuleTypePacked(m_moduleType, m_moduleSubType, EModuleStyle.None);
    }
#endif
}
