using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CelestialBodyEditorGUI
{
    // 천체 목록 전체를 그리고 추가/삭제를 처리. 변경 시 dirtyTarget을 SetDirty
    public static void DrawCelestialBodyList(List<CelestialBodyConfig> bodies, Object dirtyTarget)
    {
        if (bodies == null) return;

        for (int i = 0; i < bodies.Count; i++)
        {
            CelestialBodyConfig body = bodies[i];

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"● Planet_{i}", EditorStyles.boldLabel);
            if (GUILayout.Button("X", GUILayout.Width(22)))
            {
                bodies.RemoveAt(i);
                EditorUtility.SetDirty(dirtyTarget);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            body.position = EditorGUILayout.Vector3Field("Position", body.position);
            body.scale    = EditorGUILayout.Vector3Field("Scale",    body.scale);

            DrawMaterialPathField("Material",   ref body.materialPath);
            DrawMaterialPathField("Atmosphere", ref body.atmosphereMaterialPath);
            if (string.IsNullOrEmpty(body.atmosphereMaterialPath) == false)
                body.atmosphereScale = EditorGUILayout.Slider("Atm Scale", body.atmosphereScale, 1.001f, 1.20f);

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 행성 추가"))
        {
            bodies.Add(new CelestialBodyConfig { scale = Vector3.one * 20f });
            EditorUtility.SetDirty(dirtyTarget);
        }
        EditorGUILayout.EndHorizontal();
    }

    // Resources 기준 경로 문자열 필드 — 경로 유효성 인라인 표시
    private static void DrawMaterialPathField(string label, ref string path)
    {
        EditorGUILayout.BeginHorizontal();
        path = EditorGUILayout.TextField(label, path);

        if (string.IsNullOrEmpty(path) == false)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Resources/{path}.mat");
            if (mat == null)
            {
                GUI.color = Color.red;
                GUILayout.Label("✕", GUILayout.Width(20));
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = Color.green;
                if (GUILayout.Button("●", GUILayout.Width(20)))
                    EditorGUIUtility.PingObject(mat);
                GUI.color = Color.white;
            }
        }
        EditorGUILayout.EndHorizontal();
    }
}
