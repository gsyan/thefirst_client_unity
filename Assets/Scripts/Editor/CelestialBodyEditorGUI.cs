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
            body.rotation = EditorGUILayout.Vector3Field("Rotation", body.rotation);
            body.scale    = EditorGUILayout.Vector3Field("Scale",    body.scale);

            EditorGUILayout.Space(4);

            // Surface
            EditorGUILayout.LabelField("─ Surface (Sea)", EditorStyles.miniLabel);
            body.deepSeaColor    = EditorGUILayout.ColorField("Deep Sea",    body.deepSeaColor);
            body.shallowSeaColor = EditorGUILayout.ColorField("Shallow Sea", body.shallowSeaColor);
            EditorGUILayout.LabelField("─ Surface (Lowland)", EditorStyles.miniLabel);
            body.lowlandSandColor  = EditorGUILayout.ColorField("Sand",  body.lowlandSandColor);
            body.lowlandGreenColor = EditorGUILayout.ColorField("Green", body.lowlandGreenColor);            
            EditorGUILayout.LabelField("─ Surface (Plains)", EditorStyles.miniLabel);
            body.plainsDesertColor = EditorGUILayout.ColorField("Desert", body.plainsDesertColor);
            body.plainsGrassColor  = EditorGUILayout.ColorField("Grass",  body.plainsGrassColor);
            body.plainsForestColor = EditorGUILayout.ColorField("Forest", body.plainsForestColor);
            EditorGUILayout.LabelField("─ Surface (Highland)", EditorStyles.miniLabel);
            body.highlandSnowColor    = EditorGUILayout.ColorField("Snow",    body.highlandSnowColor);
            EditorGUILayout.LabelField("─", EditorStyles.miniLabel);
            body.landCoverage = EditorGUILayout.Slider("Land Coverage",  body.landCoverage, 0f, 1f);
            body.biomeBlend   = EditorGUILayout.Slider("Biome Blend (R)", body.biomeBlend, 0f, 0.2f);
            body.gBlend       = EditorGUILayout.Slider("G Blend (G)",     body.gBlend, 0f, 5f);

            EditorGUILayout.Space(4);

            // Cloud
            EditorGUILayout.LabelField("─ Cloud Layer", EditorStyles.miniLabel);
            body.hasClouds = EditorGUILayout.Toggle("Has Clouds", body.hasClouds);
            if (body.hasClouds == true)
            {
                EditorGUI.indentLevel++;
                body.cloudColor         = EditorGUILayout.ColorField("Cloud Color",       body.cloudColor);
                body.cloudCoverage      = EditorGUILayout.Slider("Cloud Coverage",    body.cloudCoverage,     0f, 1f);
                body.cloudRotation      = EditorGUILayout.Slider("Cloud Rotation°",   body.cloudRotation,     0f, 360f);
                body.cloudScale         = EditorGUILayout.Slider("Cloud Scale",       body.cloudScale,        1.01f, 1.1f);
                body.cloudMidLatOpacity = EditorGUILayout.Slider("MidLat Opacity",    body.cloudMidLatOpacity, 0f, 1f);
                body.cloudMidLatCenter  = EditorGUILayout.Slider("MidLat Center (v)", body.cloudMidLatCenter,  0.1f, 0.45f);
                body.cloudMidLatWidth   = EditorGUILayout.Slider("MidLat Width",      body.cloudMidLatWidth,   0f, 0.5f);
                body.cloudSoftness      = EditorGUILayout.Slider("Cloud Softness",    body.cloudSoftness,      0f, 0.5f);
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
                body.atmosphereScale = EditorGUILayout.Slider("Atmosphere Scale", body.atmosphereScale, 1.01f, 1.2f);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // Polar Ice
            EditorGUILayout.LabelField("─ Polar Ice", EditorStyles.miniLabel);
            body.hasPolarIce = EditorGUILayout.Toggle("Has Polar Ice", body.hasPolarIce);
            if (body.hasPolarIce == true)
            {
                EditorGUI.indentLevel++;
                body.iceColor     = EditorGUILayout.ColorField("Ice Color (Core)", body.iceColor);
                body.iceColorEdge = EditorGUILayout.ColorField("Ice Color (Edge)", body.iceColorEdge);
                body.poleIceWidth = EditorGUILayout.Slider("Pole Ice Width",       body.poleIceWidth, 0f, 0.4f);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 행성 추가"))
        {
            bodies.Add(new CelestialBodyConfig());
            EditorUtility.SetDirty(dirtyTarget);
        }
        EditorGUILayout.EndHorizontal();
    }
}
