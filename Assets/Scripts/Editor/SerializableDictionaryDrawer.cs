using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomPropertyDrawer(typeof(SerializableDictionary<,>))]
public class SerializableDictionaryDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var keysProp = property.FindPropertyRelative("keys");
        var valuesProp = property.FindPropertyRelative("values");

        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        var keysRect = new Rect(position.x, position.y, position.width, position.height / 2);
        var valuesRect = new Rect(position.x, position.y + position.height / 2, position.width, position.height / 2);

        EditorGUI.PropertyField(keysRect, keysProp, GUIContent.none);
        EditorGUI.PropertyField(valuesRect, valuesProp, GUIContent.none);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("keys")) + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("values")) + 20; // 여유 공간 추가
    }
}