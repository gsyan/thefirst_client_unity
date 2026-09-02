// ModuleHull 커스텀 인스펙터 - 카메라 줌 범위 프리뷰/기록 버튼 제공
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ModuleHull))]
public class ModuleHullEditor : Editor
{
    SerializedProperty m_cameraMinZoom;
    SerializedProperty m_cameraMaxZoom;

    private void OnEnable()
    {
        m_cameraMinZoom = serializedObject.FindProperty("m_cameraMinZoom");
        m_cameraMaxZoom = serializedObject.FindProperty("m_cameraMaxZoom");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Camera Zoom Preview", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Preview Min Zoom"))
            ApplyZoomToSceneView(m_cameraMinZoom.floatValue);
        if (GUILayout.Button("Capture from Scene View"))
            CaptureZoom((ModuleHull)target, isMin: true);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Preview Max Zoom"))
            ApplyZoomToSceneView(m_cameraMaxZoom.floatValue);
        if (GUILayout.Button("Capture from Scene View"))
            CaptureZoom((ModuleHull)target, isMin: false);
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    // perspective↔orthographic 전환 후에도 일관성 유지를 위해 FOV 고정
    private const float k_sceneFov = 60f;
    private static float SizeToDistance(float size) => size / Mathf.Tan(k_sceneFov * 0.5f * Mathf.Deg2Rad);
    private static float DistanceToSize(float distance) => distance * Mathf.Tan(k_sceneFov * 0.5f * Mathf.Deg2Rad);

    private void ApplyZoomToSceneView(float targetDistance)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            Debug.LogWarning("[ModuleHullEditor] 활성화된 SceneView가 없음");
            return;
        }

        sceneView.LookAt(sceneView.pivot, sceneView.rotation, DistanceToSize(targetDistance));
        sceneView.Repaint();
    }

    private void CaptureZoom(ModuleHull body, bool isMin)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            Debug.LogWarning("[ModuleHullEditor] 활성화된 SceneView가 없음");
            return;
        }

        float zoom = Mathf.Round(SizeToDistance(sceneView.size) * 10f) / 10f;

        Undo.RecordObject(body, isMin ? "Set Min Zoom" : "Set Max Zoom");

        if (isMin == true)
            body.m_cameraMinZoom = zoom;
        else
            body.m_cameraMaxZoom = zoom;

        EditorUtility.SetDirty(body);
        Debug.Log($"[ModuleHullEditor] {(isMin ? "Min" : "Max")} Zoom = {zoom}");
    }
}
