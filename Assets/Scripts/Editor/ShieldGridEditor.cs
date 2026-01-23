using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ShieldGrid))]
public class ShieldGridEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ShieldGrid grid = (ShieldGrid)target;

        EditorGUILayout.Space(10);

        // 예상 수치 계산
        int baseVertices = 12;
        int baseTriangles = 20;
        for (int i = 0; i < grid.subdivisions; i++)
        {
            baseVertices = baseVertices * 4 - 6;
            baseTriangles *= 4;
        }

        string modeInfo = grid.gridMode == EShieldGridMode.Triangle
            ? $"삼각형 모드: {baseVertices}개 꼭지점, {baseTriangles}개 셀"
            : $"헥사곤 모드: {baseTriangles}개 꼭지점, {baseVertices}개 셀 (5각형 12개 + 6각형 {baseVertices - 12}개)";

        EditorGUILayout.HelpBox($"Subdivision {grid.subdivisions}\n{modeInfo}", MessageType.Info);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);

        if (GUILayout.Button("Generate Shield", GUILayout.Height(30)))
        {
            grid.GenerateShield();
            EditorUtility.SetDirty(grid);
        }

        if (GUILayout.Button("Clear All", GUILayout.Height(25)))
        {
            Undo.RecordObject(grid, "Clear Shield");
            if (grid.m_PointParent != null)
            {
                while (grid.m_PointParent.childCount > 0)
                    DestroyImmediate(grid.m_PointParent.GetChild(0).gameObject);
            }
            grid.m_vertices.Clear();
            grid.m_cells.Clear();
            EditorUtility.SetDirty(grid);
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Info", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Vertices: {grid.m_vertices.Count}");
        EditorGUILayout.LabelField($"Cells: {grid.m_cells.Count}");

        // 이웃 통계
        if (grid.m_vertices.Count > 0)
        {
            int[] neighborCounts = new int[10];
            foreach (var v in grid.m_vertices)
            {
                if (v != null)
                {
                    int count = Mathf.Clamp(v.neighborIndices.Count, 0, 9);
                    neighborCounts[count]++;
                }
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("이웃 분포:", EditorStyles.miniLabel);

            for (int i = 1; i < 10; i++)
            {
                if (neighborCounts[i] > 0)
                    EditorGUILayout.LabelField($"  {i}-이웃: {neighborCounts[i]}개");
            }
        }
    }
}
