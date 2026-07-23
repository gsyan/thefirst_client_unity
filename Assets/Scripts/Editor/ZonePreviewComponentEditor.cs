#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ZonePreviewComponent))]
public class ZonePreviewComponentEditor : Editor
{
    private bool m_zoneFoldout      = true;
    private bool m_celestialFoldout = true;

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
        
        // 버튼 배치
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Preview", GUILayout.Height(28)))
            comp.RefreshPreview();

        if (GUILayout.Button("Apply to CSV", GUILayout.Height(28)))
        {
            DataTableZoneCSVUtility.ExportZoneAndCelestial(comp.dataTableZone);
            Debug.Log("[ZonePreview] CSV 내보내기 완료");
        }

        if (GUILayout.Button("Clear Preview", GUILayout.Height(28)))
            comp.ClearPreview();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(6);

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
            DrawZoneInspector(comp, zone);
        }
    }

    private void DrawZoneInspector(ZonePreviewComponent comp, ZoneConfig zone)
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
            CelestialBodyEditorGUI.DrawCelestialBodyList(zone.zoneIndex, zone.celestialBodies, comp.dataTableZone);
            EditorGUI.indentLevel--;
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(comp.dataTableZone);
            comp.RefreshPreview();
        }
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

    }

}
#endif
