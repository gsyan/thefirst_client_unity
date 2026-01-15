using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ModuleSlot))]
public class ModuleSlotEditor : Editor
{
    SerializedProperty m_moduleTypePacked;
    SerializedProperty m_moduleSlotInfo;

    private void OnEnable()
    {
        m_moduleTypePacked = serializedObject.FindProperty("m_moduleTypePacked");
        m_moduleSlotInfo = serializedObject.FindProperty("m_moduleSlotInfo");
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
        var moduleTypeProp = m_moduleSlotInfo.FindPropertyRelative("moduleType");
        EditorGUILayout.PropertyField(moduleTypeProp);

        // Module SubType - 타입에 따라 필터링된 SubType 표시
        EModuleType typeValue = (EModuleType)moduleTypeProp.enumValueIndex;

        var moduleSubTypeProp = m_moduleSlotInfo.FindPropertyRelative("moduleSubType");
        EditorGUI.BeginChangeCheck();
        int currentEnumValue = moduleSubTypeProp.intValue;
        EModuleSubType currentSubType = (EModuleSubType)currentEnumValue;
        EModuleSubType newSubType = DrawFilteredSubTypePopup(typeValue, currentSubType);
        if (EditorGUI.EndChangeCheck())
            moduleSubTypeProp.intValue = (int)newSubType;

        // Slot Type - 비트 플래그로 다중 선택 가능
        var moduleSlotTypeProp = m_moduleSlotInfo.FindPropertyRelative("moduleSlotType");
        EModuleSlotType currentSlotType = (EModuleSlotType)moduleSlotTypeProp.intValue;
        EModuleSlotType newSlotType = (EModuleSlotType)EditorGUILayout.EnumFlagsField("Module Slot Type", currentSlotType);
        moduleSlotTypeProp.intValue = (int)newSlotType;

        // Module Slot Index
        var slotIndexProp = m_moduleSlotInfo.FindPropertyRelative("slotIndex");
        EditorGUILayout.PropertyField(slotIndexProp);

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
