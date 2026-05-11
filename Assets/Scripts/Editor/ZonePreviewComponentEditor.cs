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

        if (GUILayout.Button("Apply to CSV", GUILayout.Height(28)))
        {
            DataTableZoneCSVUtility.ExportZoneAndCelestial(comp.dataTableZone);
            Debug.Log("[ZonePreview] CSV 내보내기 완료");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Convert Absolute → Relative (일회성 마이그레이션)"))
        {
            if (EditorUtility.DisplayDialog("마이그레이션",
                "fleetPosition을 절대좌표에서 galaxyCameraTarget 기준 상대좌표로 변환합니다.\n이미 변환된 경우 재실행하면 값이 틀어집니다.\n\n계속하시겠습니까?", "변환", "취소"))
            {
                ConvertAbsoluteToRelative(comp.dataTableZone);
            }
        }

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
            CelestialBodyEditorGUI.DrawCelestialBodyList(zone.celestialBodies, table);
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

        // 존 중심점 표시 (참고용)
        Vector3 zoneCenter = table.GetZoneCenter(zoneIndex);
        EditorGUILayout.HelpBox($"Zone Center (galaxyCameraTarget): {zoneCenter}  — Fleet Position은 이 점 기준 상대 좌표", MessageType.None);

        EditorGUI.BeginChangeCheck();

        bool hasAny = false;
        foreach (ZoneStageConfig stage in table.zoneStageList)
        {
            if (stage.zoneIndex != zoneIndex) continue;
            hasAny = true;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(stage.zoneName, EditorStyles.boldLabel);
            stage.fleetPosition  = EditorGUILayout.Vector3Field("Fleet Position (상대)", stage.fleetPosition);
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

        // 카메라 타겟 핸들 — 주황
        Handles.color = new Color(1f, 0.5f, 0f, 0.9f);
        Handles.DrawWireDisc(zone.galaxyCameraTarget, Vector3.up,    8f);
        Handles.DrawWireDisc(zone.galaxyCameraTarget, Vector3.right, 8f);
        Handles.Label(zone.galaxyCameraTarget + Vector3.up * 12f,
            $"[CameraTarget] Zone-{zone.zoneIndex}",
            new GUIStyle { normal = { textColor = new Color(1f, 0.5f, 0f) }, fontStyle = FontStyle.Bold, fontSize = 12 });

        EditorGUI.BeginChangeCheck();
        Vector3 newCamTarget = Handles.PositionHandle(zone.galaxyCameraTarget, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(comp.dataTableZone, "Move Camera Target");
            zone.galaxyCameraTarget = newCamTarget;
            comp.SyncPreviewCameraTarget();
            EditorUtility.SetDirty(comp.dataTableZone);
            SceneView.RepaintAll();
        }

        // 천체 위치 핸들 — 흰색 (fleet와 동일 방식)
        if (zone.celestialBodies != null)
        {
            Handles.color = Color.white;
            for (int i = 0; i < zone.celestialBodies.Count; i++)
            {
                CelestialBodyConfig body = zone.celestialBodies[i];
                Handles.Label(body.position + Vector3.up * (body.scale.y * 0.5f + 15f), $"[Planet_{i}]", boldWhite);

                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(body.position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(comp.dataTableZone, "Move Planet");
                    body.position = newPos;
                    comp.SyncPreviewPlanet(i);
                    EditorUtility.SetDirty(comp.dataTableZone);
                    SceneView.RepaintAll();
                }
            }
        }

        // 스테이지별 함대 스폰 위치 핸들 — 초록
        // fleetPosition은 상대좌표이므로 zoneCenter를 더해 절대좌표로 씬에 표시,
        // 드래그 결과는 다시 zoneCenter를 빼서 상대좌표로 저장
        Vector3 zoneCenter = comp.dataTableZone.GetZoneCenter(zone.zoneIndex);
        Handles.color = new Color(0.2f, 1f, 0.3f, 0.9f);
        foreach (ZoneStageConfig stage in comp.dataTableZone.zoneStageList)
        {
            if (stage.zoneIndex != zone.zoneIndex) continue;

            Vector3 worldPos = zoneCenter + stage.fleetPosition;

            Handles.DrawWireDisc(worldPos, Vector3.up,    5f);
            Handles.DrawWireDisc(worldPos, Vector3.right, 5f);

            EditorGUI.BeginChangeCheck();
            Quaternion stageRot = Quaternion.Euler(0f, stage.fleetRotationY, 0f);
            Quaternion newRot   = Handles.RotationHandle(stageRot, worldPos);
            EditorGUI.EndChangeCheck();
            if (stageRot != newRot)
            {
                Undo.RecordObject(comp.dataTableZone, "Rotate Fleet");
                stage.fleetRotationY = newRot.eulerAngles.y;
                EditorUtility.SetDirty(comp.dataTableZone);
                SceneView.RepaintAll();
            }

            EditorGUI.BeginChangeCheck();
            Vector3 newWorldPos = Handles.PositionHandle(worldPos, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(comp.dataTableZone, "Move Fleet Position");
                stage.fleetPosition = newWorldPos - zoneCenter; // 상대좌표로 저장
                EditorUtility.SetDirty(comp.dataTableZone);
            }

            Handles.color = Color.yellow;
            float arrowLen = HandleUtility.GetHandleSize(worldPos) * 1.5f;
            Vector3 fwd   = Quaternion.Euler(0f, stage.fleetRotationY, 0f) * Vector3.forward;
            Vector3 tip   = worldPos + fwd * arrowLen;
            Handles.DrawLine(worldPos, tip, 3f);
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            Handles.DrawLine(tip, tip - fwd * arrowLen * 0.25f + right * arrowLen * 0.15f, 2f);
            Handles.DrawLine(tip, tip - fwd * arrowLen * 0.25f - right * arrowLen * 0.15f, 2f);
            Handles.color = new Color(0.2f, 1f, 0.3f, 0.9f);

            Handles.Label(worldPos + Vector3.up * 8f, $"[Fleet] {stage.zoneName}", boldWhite);
        }
    }

    // 기존 절대좌표 데이터를 galaxyCameraTarget 기준 상대좌표로 일괄 변환 (일회성 마이그레이션)
    private static void ConvertAbsoluteToRelative(DataTableZone table)
    {
        int converted = 0;
        foreach (ZoneStageConfig stage in table.zoneStageList)
        {
            Vector3 center = table.GetZoneCenter(stage.zoneIndex);
            stage.fleetPosition -= center;
            converted++;
        }
        EditorUtility.SetDirty(table);
        EditorUtility.DisplayDialog("완료", $"{converted}개 스테이지 변환 완료.\nApply to CSV로 저장하세요.", "OK");
        SceneView.RepaintAll();
    }
}
#endif
