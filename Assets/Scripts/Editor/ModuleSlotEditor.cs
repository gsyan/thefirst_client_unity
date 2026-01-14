using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ModuleSlot))]
public class ModuleSlotEditor : Editor
{
    SerializedProperty m_moduleTypePacked;
    SerializedProperty m_slotIndex;
    SerializedProperty m_moduleType;
    SerializedProperty m_moduleSubType;
    SerializedProperty m_moduleSlotType;

    private void OnEnable()
    {
        m_moduleTypePacked = serializedObject.FindProperty("m_moduleTypePacked");
        m_slotIndex = serializedObject.FindProperty("m_slotIndex");
        m_moduleType = serializedObject.FindProperty("m_moduleType");
        m_moduleSubType = serializedObject.FindProperty("m_moduleSubType");
        m_moduleSlotType = serializedObject.FindProperty("m_moduleSlotType");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Read-only packed value
        GUI.enabled = false;
        EditorGUILayout.PropertyField(m_moduleTypePacked);
        GUI.enabled = true;

        EditorGUILayout.Space();
        
        // Module Type
        EditorGUILayout.PropertyField(m_moduleType);

        // Module SubType - 타입에 따라 필터링된 SubType 표시
        EModuleType typeValue = (EModuleType)m_moduleType.enumValueIndex;

        EditorGUI.BeginChangeCheck();
        // enumValueIndex uses ordinal position, but we need actual enum value
        int currentEnumValue = m_moduleSubType.intValue;
        EModuleSubType currentSubType = (EModuleSubType)currentEnumValue;
        EModuleSubType newSubType = DrawFilteredSubTypePopup(typeValue, currentSubType);
        if (EditorGUI.EndChangeCheck())
            m_moduleSubType.intValue = (int)newSubType;

        // Slot Type - 비트 플래그로 다중 선택 가능
        EModuleSlotType currentSlotType = (EModuleSlotType)m_moduleSlotType.intValue;
        EModuleSlotType newSlotType = (EModuleSlotType)EditorGUILayout.EnumFlagsField("Module Slot Type", currentSlotType);
        m_moduleSlotType.intValue = (int)newSlotType;

        // Module Slot Index
        EditorGUILayout.PropertyField(m_slotIndex);

        serializedObject.ApplyModifiedProperties();
    }

    private EModuleSubType DrawFilteredSubTypePopup(EModuleType moduleType, EModuleSubType currentSubType)
    {
        // 타입에 맞는 SubType만 필터링
        var filteredSubTypes = new System.Collections.Generic.List<EModuleSubType>();
        filteredSubTypes.Add(EModuleSubType.None);

        int typeValue = (int)moduleType;
        foreach (EModuleSubType subType in System.Enum.GetValues(typeof(EModuleSubType)))
        {
            if (subType == EModuleSubType.None) continue;

            int subTypeValue = (int)subType;
            if (subTypeValue / 1000 == typeValue)
            {
                filteredSubTypes.Add(subType);
            }
        }

        // 현재 선택된 SubType이 필터링된 목록에 없으면 None으로 변경
        if (filteredSubTypes.Contains(currentSubType) == false)
        {
            currentSubType = EModuleSubType.None;
        }

        int currentIndex = filteredSubTypes.IndexOf(currentSubType);
        string[] displayNames = new string[filteredSubTypes.Count];
        for (int i = 0; i < filteredSubTypes.Count; i++)
        {
            displayNames[i] = filteredSubTypes[i].ToString();
        }

        int newIndex = EditorGUILayout.Popup("Module Sub Type", currentIndex, displayNames);
        return filteredSubTypes[newIndex];
    }
}
