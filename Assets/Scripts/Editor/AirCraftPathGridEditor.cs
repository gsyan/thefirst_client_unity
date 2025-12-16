using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AirCraftPathGrid))]
public class AirCraftPathGridEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        AirCraftPathGrid pathGrid = (AirCraftPathGrid)target;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("boundStyle"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pushBackDistance"));
        

        if (pathGrid.boundStyle == EAirCraftPathGridBoundStyle.Sphere)
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
            if (pathGrid.boundStyle == EAirCraftPathGridBoundStyle.Sphere)
                pathGrid.GenerateOutline_Sphere();
            else
                pathGrid.GenerateOutline_ByBox();

            EditorUtility.SetDirty(pathGrid);
        }

        if (GUILayout.Button("Destroy Outline Points"))
        {
            pathGrid.DestroyAllPoints();

            EditorUtility.SetDirty(pathGrid);
        }
    }
}
