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

            EditorGUILayout.Space(4);

            // Surface
            EditorGUILayout.LabelField("─ Surface (Land + Sea)", EditorStyles.miniLabel);
            body.deepSeaColor    = EditorGUILayout.ColorField("Deep Sea",    body.deepSeaColor);
            body.shallowSeaColor = EditorGUILayout.ColorField("Shallow Sea", body.shallowSeaColor);
            body.coastColor      = EditorGUILayout.ColorField("Coast",      body.coastColor);
            body.grasslandColor  = EditorGUILayout.ColorField("Grassland",  body.grasslandColor);
            body.forestColor     = EditorGUILayout.ColorField("Forest",     body.forestColor);
            body.desertColor     = EditorGUILayout.ColorField("Desert",     body.desertColor);
            body.highlandColor   = EditorGUILayout.ColorField("Highland",   body.highlandColor);
            body.landCoverage    = EditorGUILayout.Slider("Land Coverage",   body.landCoverage, 0f, 1f);
            body.landRotation    = EditorGUILayout.Slider("Land Rotation°",  body.landRotation, 0f, 360f);

            EditorGUILayout.Space(4);

            // Cloud
            EditorGUILayout.LabelField("─ Cloud Layer", EditorStyles.miniLabel);
            body.hasClouds = EditorGUILayout.Toggle("Has Clouds", body.hasClouds);
            if (body.hasClouds == true)
            {
                EditorGUI.indentLevel++;
                body.cloudColor    = EditorGUILayout.ColorField("Cloud Color",     body.cloudColor);
                body.cloudCoverage = EditorGUILayout.Slider("Cloud Coverage",  body.cloudCoverage, 0f, 1f);
                body.cloudRotation = EditorGUILayout.Slider("Cloud Rotation°", body.cloudRotation, 0f, 360f);
                body.cloudScale    = EditorGUILayout.Slider("Cloud Scale",     body.cloudScale, 1.001f, 1.10f);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // Atmosphere
            EditorGUILayout.LabelField("─ Atmosphere Layer", EditorStyles.miniLabel);
            body.hasAtmosphere = EditorGUILayout.Toggle("Has Atmosphere", body.hasAtmosphere);
            if (body.hasAtmosphere == true)
            {
                EditorGUI.indentLevel++;
                body.atmosphereColor = EditorGUILayout.ColorField("Atmosphere Color", body.atmosphereColor);
                body.atmosphereScale = EditorGUILayout.Slider("Atmosphere Scale", body.atmosphereScale, 1.01f, 1.30f);
                EditorGUI.indentLevel--;
            }

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
