// HangerFlightPath 커스텀 인스팩터 — Launch/Return 그룹별 곡선 WP 생성/제거 버튼 배치
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HangerFlightPath))]
public class HangerFlightPathEditor : Editor
{
    // Launch 그룹 버튼 → m_launchPath 위
    // Return 그룹 버튼 → m_returnPath 아래
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var path = (HangerFlightPath)target;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_launchPath"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_launchWps"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_launchTargetCount"));
        DrawCurveToolbar(path, "Launch 곡선 도구", "GenerateLaunchCurve", "ClearGeneratedLaunchWps");
        
        EditorGUILayout.Space(6f);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_returnPath"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_returnWps"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_returnTargetCount"));
        DrawCurveToolbar(path, "Return 곡선 도구", "GenerateReturnCurve", "ClearGeneratedReturnWps");

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawCurveToolbar(HangerFlightPath path, string label, string generateMethod, string clearMethod)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        if (GUILayout.Button($"{label.Replace(" 도구", "")} WP 생성"))
            Invoke(path, generateMethod);

        GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
        if (GUILayout.Button("생성된 WP 제거"))
            Invoke(path, clearMethod);
        GUI.backgroundColor = Color.white;
    }

    private static void Invoke(HangerFlightPath target, string methodName)
    {
        typeof(HangerFlightPath)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.Invoke(target, null);
    }
}
