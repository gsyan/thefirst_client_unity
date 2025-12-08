using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(OutlineScanner))]
public class OutlineScannerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        OutlineScanner scanner = (OutlineScanner)target;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("boundStyle"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pushBackDistance"));
        

        if (scanner.boundStyle == EOutLineBoundStyle.EOutLineBoundStyle_Sphere)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sampleCount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sphereRadius"));
        }
        else
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("spacing"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("jitter"));
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_PointParent"));

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Outline Points"))
        {
            if (scanner.boundStyle == EOutLineBoundStyle.EOutLineBoundStyle_Sphere)
                scanner.GenerateOutline_Sphere();
            else
                scanner.GenerateOutline_ByBox();

            EditorUtility.SetDirty(scanner);
        }

        if (GUILayout.Button("Destroy Outline Points"))
        {
            scanner.DestroyAllPoints();

            EditorUtility.SetDirty(scanner);
        }
    }
}
