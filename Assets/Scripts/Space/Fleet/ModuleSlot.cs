using UnityEngine;

public class ModuleSlot : MonoBehaviour
{
    public int m_moduleTypePacked;
    public ModuleSlotInfo m_moduleSlotInfo = new ModuleSlotInfo();

#if UNITY_EDITOR
    private void OnValidate()
    {
        m_moduleTypePacked = CommonUtility.CreateModuleTypePacked(m_moduleSlotInfo.moduleType, m_moduleSlotInfo.moduleSubType, m_moduleSlotInfo.moduleSlotType);
    }
#endif
}
