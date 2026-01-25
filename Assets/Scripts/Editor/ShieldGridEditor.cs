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
            grid.ClearAll();
            EditorUtility.SetDirty(grid);
        }

        // 메시 에셋 생성
        EditorGUILayout.Space(5);
        if (grid.unitSphereMesh == null)
        {
            EditorGUILayout.HelpBox("단위 구 메시 에셋이 필요합니다.", MessageType.Warning);
            if (GUILayout.Button("Create Unit Sphere Mesh Asset", GUILayout.Height(25)))
            {
                CreateUnitSphereMeshAsset(grid);
            }
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

    void CreateUnitSphereMeshAsset(ShieldGrid grid)
    {
        int resolution = grid.colliderResolution;
        Mesh mesh = new Mesh();
        mesh.name = "UnitSphere";

        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();

        for (int lat = 0; lat <= resolution; lat++)
        {
            float theta = lat * Mathf.PI / resolution;
            float sinTheta = Mathf.Sin(theta);
            float cosTheta = Mathf.Cos(theta);

            for (int lon = 0; lon <= resolution; lon++)
            {
                float phi = lon * 2f * Mathf.PI / resolution;
                float x = Mathf.Cos(phi) * sinTheta * 0.5f;
                float y = cosTheta * 0.5f;
                float z = Mathf.Sin(phi) * sinTheta * 0.5f;
                vertices.Add(new Vector3(x, y, z));
            }
        }

        for (int lat = 0; lat < resolution; lat++)
        {
            for (int lon = 0; lon < resolution; lon++)
            {
                int curr = lat * (resolution + 1) + lon;
                int next = curr + resolution + 1;
                triangles.Add(curr);
                triangles.Add(next);
                triangles.Add(curr + 1);
                triangles.Add(curr + 1);
                triangles.Add(next);
                triangles.Add(next + 1);
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // 에셋 저장
        string path = "Assets/Resources/UnitSphereMesh.asset";
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();

        // 자동 할당
        grid.unitSphereMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        EditorUtility.SetDirty(grid);

        Debug.Log($"단위 구 메시 에셋 생성: {path}");
    }
}
