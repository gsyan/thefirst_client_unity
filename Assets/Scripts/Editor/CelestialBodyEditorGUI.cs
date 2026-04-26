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

            body.position           = EditorGUILayout.Vector3Field("Position",  body.position);
            body.scale              = EditorGUILayout.Vector3Field("Scale",     body.scale);
            body.material           = (Material)EditorGUILayout.ObjectField("Material",   body.material,           typeof(Material), false);
            body.atmosphereMaterial = (Material)EditorGUILayout.ObjectField("Atmosphere", body.atmosphereMaterial, typeof(Material), false);
            if (body.atmosphereMaterial != null)
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
}
