using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AirCraftPathPoint))]
public class AirCraftPathPointEditor : Editor
{
    private AirCraftPathPoint targetPoint;
    private AirCraftPathPoint pointToAdd;

    private void OnEnable()
    {
        targetPoint = (AirCraftPathPoint)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("이웃 포인트 관리", EditorStyles.boldLabel);

        // 이웃 추가 UI
        EditorGUILayout.BeginHorizontal();
        pointToAdd = (AirCraftPathPoint)EditorGUILayout.ObjectField(
            "추가할 이웃",
            pointToAdd,
            typeof(AirCraftPathPoint),
            true
        );

        GUI.enabled = pointToAdd != null && pointToAdd != targetPoint;
        if (GUILayout.Button("이웃 추가", GUILayout.Width(100)))
        {
            AddNeighbor(targetPoint, pointToAdd);
            pointToAdd = null;
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        // 현재 이웃 목록 표시
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"현재 이웃 수: {targetPoint.neighbors.Count}");

        if (targetPoint.neighbors.Count > 0)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            for (int i = targetPoint.neighbors.Count - 1; i >= 0; i--)
            {
                if (i >= targetPoint.neighbors.Count) continue;

                EditorGUILayout.BeginHorizontal();

                AirCraftPathPoint neighbor = targetPoint.neighbors[i];

                if (neighbor == null)
                {
                    EditorGUILayout.LabelField($"[{i}] (Null)");
                    if (GUILayout.Button("제거", GUILayout.Width(60)))
                    {
                        targetPoint.neighbors.RemoveAt(i);
                        EditorUtility.SetDirty(targetPoint);
                    }
                }
                else
                {
                    EditorGUILayout.ObjectField(
                        $"[{i}] ({neighbor.index})",
                        neighbor,
                        typeof(AirCraftPathPoint),
                        true
                    );

                    if (GUILayout.Button("제거", GUILayout.Width(60)))
                    {
                        RemoveNeighbor(targetPoint, neighbor);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }
    }

    private void AddNeighbor(AirCraftPathPoint pointA, AirCraftPathPoint pointB)
    {
        if (pointA == null || pointB == null || pointA == pointB)
            return;

        // A에 B 추가
        if (!pointA.neighbors.Contains(pointB))
        {
            pointA.neighbors.Add(pointB);
            EditorUtility.SetDirty(pointA);
        }

        // B에 A 추가 (양방향)
        if (!pointB.neighbors.Contains(pointA))
        {
            pointB.neighbors.Add(pointA);
            EditorUtility.SetDirty(pointB);
        }

        Debug.Log($"이웃 추가 완료: {pointA.name} ↔ {pointB.name}");
    }

    private void RemoveNeighbor(AirCraftPathPoint pointA, AirCraftPathPoint pointB)
    {
        if (pointA == null || pointB == null)
            return;

        // A에서 B 제거
        if (pointA.neighbors.Contains(pointB))
        {
            pointA.neighbors.Remove(pointB);
            EditorUtility.SetDirty(pointA);
        }

        // B에서 A 제거 (양방향)
        if (pointB != null && pointB.neighbors.Contains(pointA))
        {
            pointB.neighbors.Remove(pointA);
            EditorUtility.SetDirty(pointB);
        }

        Debug.Log($"이웃 제거 완료: {pointA.name} ↔ {pointB.name}");
    }

    // Scene View에서 이웃 연결선 그리기
    private void OnSceneGUI()
    {
        if (targetPoint == null || targetPoint.neighbors == null)
            return;

        Handles.color = Color.cyan;
        Vector3 startPos = targetPoint.transform.position;

        foreach (var neighbor in targetPoint.neighbors)
        {
            if (neighbor != null)
            {
                Vector3 endPos = neighbor.transform.position;
                Handles.DrawLine(startPos, endPos);

                // 방향 표시 (화살표)
                Vector3 direction = (endPos - startPos).normalized;
                Vector3 midPoint = (startPos + endPos) * 0.5f;
                float arrowSize = 0.3f;

                Handles.color = Color.yellow;
                Handles.ArrowHandleCap(0, midPoint, Quaternion.LookRotation(direction), arrowSize, EventType.Repaint);
                Handles.color = Color.cyan;
            }
        }
    }
}
