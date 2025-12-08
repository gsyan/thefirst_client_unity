using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ModuleSlot))]
public class ModuleSlotEditor : Editor
{
    SerializedProperty m_moduleType;
    SerializedProperty m_slotIndex;
    SerializedProperty m_type;
    SerializedProperty m_bodySubType;
    SerializedProperty m_weaponSubType;
    SerializedProperty m_engineSubType;
    SerializedProperty m_hangerSubType;

    private void OnEnable()
    {
        m_moduleType = serializedObject.FindProperty("m_moduleType");
        m_slotIndex = serializedObject.FindProperty("m_slotIndex");
        m_type = serializedObject.FindProperty("m_type");
        m_bodySubType = serializedObject.FindProperty("m_bodySubType");
        m_weaponSubType = serializedObject.FindProperty("m_weaponSubType");
        m_engineSubType = serializedObject.FindProperty("m_engineSubType");
        m_hangerSubType = serializedObject.FindProperty("m_hangerSubType");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        GUI.enabled = false;
        EditorGUILayout.PropertyField(m_moduleType);
        GUI.enabled = true;

        EditorGUILayout.PropertyField(m_slotIndex);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Module Type Settings", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(m_type);

        EModuleType typeValue = (EModuleType)m_type.enumValueIndex;

        if (typeValue == EModuleType.Body)
            EditorGUILayout.PropertyField(m_bodySubType);
        else if (typeValue == EModuleType.Weapon)
            EditorGUILayout.PropertyField(m_weaponSubType);
        else if (typeValue == EModuleType.Engine)
            EditorGUILayout.PropertyField(m_engineSubType);
        else if (typeValue == EModuleType.Hanger)
            EditorGUILayout.PropertyField(m_hangerSubType);

        serializedObject.ApplyModifiedProperties();
    }
}
