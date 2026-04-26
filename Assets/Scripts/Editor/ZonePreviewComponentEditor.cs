#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ZonePreviewComponent))]
public class ZonePreviewComponentEditor : Editor
{
    private bool m_zoneFoldout      = true;
    private bool m_celestialFoldout = true;
    private bool m_stagesFoldout    = true;

    public override void OnInspectorGUI()
    {
        ZonePreviewComponent comp = (ZonePreviewComponent)target;

        EditorGUI.BeginChangeCheck();
        comp.dataTableZone = (DataTableZone)EditorGUILayout.ObjectField(
            "DataTableZone", comp.dataTableZone, typeof(DataTableZone), false);
        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(comp);

        if (comp.dataTableZone == null)
        {
            EditorGUILayout.HelpBox("DataTableZone을 먼저 할당하세요.", MessageType.Info);
            return;
        }

        int zoneCount = comp.dataTableZone.zoneList.Count;
        if (zoneCount == 0)
        {
            EditorGUILayout.HelpBox("ZoneList가 비어있습니다.", MessageType.Warning);
            return;
        }

        // Zone 선택 드롭다운
        string[] zoneNames = new string[zoneCount];
        for (int i = 0; i < zoneCount; i++)
            zoneNames[i] = $"Zone-{comp.dataTableZone.zoneList[i].zoneIndex}";

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUILayout.Popup("Zone", comp.selectedZoneIndex, zoneNames);
        if (EditorGUI.EndChangeCheck())
        {
            comp.selectedZoneIndex = newIndex;
            comp.RefreshPreview();
            EditorUtility.SetDirty(comp);
        }

        EditorGUILayout.Space(4);

        ZoneConfig zone = comp.dataTableZone.GetZone(comp.selectedZoneIndex);
        if (zone != null)
        {
            DrawZoneInspector(comp.dataTableZone, zone);
            EditorGUILayout.Space(4);
            DrawStagesInspector(comp.dataTableZone, zone.zoneIndex);
        }

        EditorGUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Preview", GUILayout.Height(28)))
            comp.RefreshPreview();
        if (GUILayout.Button("Apply to DataTable", GUILayout.Height(28)))
            comp.ApplyFromScene();
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Clear Preview"))
            comp.ClearPreview();
    }

    private void DrawZoneInspector(DataTableZone table, ZoneConfig zone)
    {
        EditorGUI.BeginChangeCheck();

        m_zoneFoldout = EditorGUILayout.Foldout(m_zoneFoldout, "갤럭시 뷰 카메라 앵커", true, EditorStyles.foldoutHeader);
        if (m_zoneFoldout)
        {
            EditorGUI.indentLevel++;
            zone.skyboxMaterial    = (Material)EditorGUILayout.ObjectField("Skybox Material", zone.skyboxMaterial, typeof(Material), false);
            zone.galaxyCameraTarget = EditorGUILayout.Vector3Field("Camera Target", zone.galaxyCameraTarget);
            zone.galaxyCameraZoom   = EditorGUILayout.FloatField("Camera Zoom", zone.galaxyCameraZoom);
            zone.galaxyCameraRotX   = EditorGUILayout.Slider("Rot X (앙각)", zone.galaxyCameraRotX, -80f, 80f);
            zone.galaxyCameraRotY   = EditorGUILayout.FloatField("Rot Y (수평)", zone.galaxyCameraRotY);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        m_celestialFoldout = EditorGUILayout.Foldout(m_celestialFoldout, "천체 배치", true, EditorStyles.foldoutHeader);
        if (m_celestialFoldout)
        {
            EditorGUI.indentLevel++;

            if (zone.celestialBodies == null)
                zone.celestialBodies = new List<CelestialBodyConfig>();

            for (int i = 0; i < zone.celestialBodies.Count; i++)
            {
                CelestialBodyConfig body = zone.celestialBodies[i];
                string label = body.isStar ? $"★ Star_{i}" : $"● Planet_{i}";

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
                body.isStar = EditorGUILayout.ToggleLeft("항성", body.isStar, GUILayout.Width(55));
                if (GUILayout.Button("X", GUILayout.Width(22)))
                {
                    zone.celestialBodies.RemoveAt(i);
                    EditorUtility.SetDirty(table);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                body.position = EditorGUILayout.Vector3Field("Position", body.position);
                body.scale    = EditorGUILayout.Vector3Field("Scale",    body.scale);
                body.material = (Material)EditorGUILayout.ObjectField("Material", body.material, typeof(Material), false);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 행성 추가"))
                zone.celestialBodies.Add(new CelestialBodyConfig { isStar = false, scale = Vector3.one * 20f });
            if (GUILayout.Button("+ 항성 추가"))
                zone.celestialBodies.Add(new CelestialBodyConfig { isStar = true,  scale = Vector3.one * 50f });
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(table);
    }

    private void DrawStagesInspector(DataTableZone table, int zoneIndex)
    {
        m_stagesFoldout = EditorGUILayout.Foldout(m_stagesFoldout, "Stage 함대 스폰 위치", true, EditorStyles.foldoutHeader);
        if (m_stagesFoldout == false) return;

        EditorGUI.indentLevel++;
        EditorGUI.BeginChangeCheck();

        bool hasAny = false;
        foreach (ZoneStageConfig stage in table.zoneStageList)
        {
            if (stage.zoneIndex != zoneIndex) continue;
            hasAny = true;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(stage.zoneName, EditorStyles.boldLabel);
            stage.fleetPosition = EditorGUILayout.Vector3Field("Fleet Position", stage.fleetPosition);
            stage.fleetRotationY = EditorGUILayout.Slider("Fleet Rotation Y", stage.fleetRotationY, 0f, 360f);
            EditorGUILayout.EndVertical();
        }

        if (hasAny == false)
            EditorGUILayout.HelpBox("이 Zone에 속한 Stage가 없습니다.", MessageType.None);

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(table);
            SceneView.RepaintAll();
        }

        EditorGUI.indentLevel--;
    }

    private void OnSceneGUI()
    {
        ZonePreviewComponent comp = (ZonePreviewComponent)target;
        if (comp.dataTableZone == null) return;
        ZoneConfig zone = comp.dataTableZone.GetZone(comp.selectedZoneIndex);
        if (zone == null) return;

        GUIStyle boldWhite = new GUIStyle
        {
            normal    = { textColor = Color.white },
            fontStyle = FontStyle.Bold,
            fontSize  = 11,
        };

        // 카메라 타겟 — 주황
        Handles.color = new Color(1f, 0.5f, 0f, 0.9f);
        Handles.DrawWireDisc(zone.galaxyCameraTarget, Vector3.up,    8f);
        Handles.DrawWireDisc(zone.galaxyCameraTarget, Vector3.right, 8f);
        Handles.Label(zone.galaxyCameraTarget + Vector3.up * 12f,
            $"[CameraTarget] Zone-{zone.zoneIndex}",
            new GUIStyle { normal = { textColor = new Color(1f, 0.5f, 0f) }, fontStyle = FontStyle.Bold, fontSize = 12 });

        // 스테이지별 함대 스폰 위치 — 초록
        Handles.color = new Color(0.2f, 1f, 0.3f, 0.9f);
        foreach (ZoneStageConfig stage in comp.dataTableZone.zoneStageList)
        {
            if (stage.zoneIndex != zone.zoneIndex) continue;

            Handles.DrawWireDisc(stage.fleetPosition, Vector3.up,   5f);
            Handles.DrawWireDisc(stage.fleetPosition, Vector3.right, 5f);

            // Y축 회전 편집
            EditorGUI.BeginChangeCheck();
            Quaternion stageRot = Quaternion.Euler(0f, stage.fleetRotationY, 0f);
            Quaternion newRot = Handles.RotationHandle(stageRot, stage.fleetPosition);
            EditorGUI.EndChangeCheck();
            if (stageRot != newRot)
            {
                Undo.RecordObject(comp.dataTableZone, "Rotate Fleet");
                stage.fleetRotationY = newRot.eulerAngles.y;
                EditorUtility.SetDirty(comp.dataTableZone);
                SceneView.RepaintAll();
            }

            // 드래그로 위치 편집
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = Handles.PositionHandle(stage.fleetPosition, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(comp.dataTableZone, "Move Fleet Position");
                stage.fleetPosition = newPos;
                EditorUtility.SetDirty(comp.dataTableZone);
            }

            // 방향 화살표 — 핸들 처리 후 최신값으로 그림
            Handles.color = Color.yellow;
            float arrowLen = HandleUtility.GetHandleSize(stage.fleetPosition) * 1.5f;
            Vector3 fwd = Quaternion.Euler(0f, stage.fleetRotationY, 0f) * Vector3.forward;
            Vector3 tip = stage.fleetPosition + fwd * arrowLen;
            Handles.DrawLine(stage.fleetPosition, tip, 3f);
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            Handles.DrawLine(tip, tip - fwd * arrowLen * 0.25f + right * arrowLen * 0.15f, 2f);
            Handles.DrawLine(tip, tip - fwd * arrowLen * 0.25f - right * arrowLen * 0.15f, 2f);
            Handles.color = new Color(0.2f, 1f, 0.3f, 0.9f);

            Handles.Label(stage.fleetPosition + Vector3.up * 8f, $"[Fleet] {stage.zoneName}", boldWhite);
        }
    }
}
#endif
