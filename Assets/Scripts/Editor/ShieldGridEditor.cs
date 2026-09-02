using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

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
            SaveSurfaceMeshAsSubAsset(grid);
            EditorUtility.SetDirty(grid);
        }

        if (GUILayout.Button("Clear All", GUILayout.Height(25)))
        {
            Undo.RecordObject(grid, "Clear Shield");
            grid.ClearAll();
            RemoveSurfaceMeshSubAsset();
            EditorUtility.SetDirty(grid);
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Hit Points", EditorStyles.boldLabel);

        ModuleHull moduleHull = grid.GetComponent<ModuleHull>();
        if (moduleHull != null)
        {
            bool newShow = EditorGUILayout.Toggle("Show Hit Point Gizmos", moduleHull.bShowHitPointGizmos);
            if (newShow != moduleHull.bShowHitPointGizmos)
            {
                moduleHull.bShowHitPointGizmos = newShow;
                EditorUtility.SetDirty(moduleHull);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Bake Hit Points", GUILayout.Height(30)))
            {
                var method = typeof(ModuleHull).GetMethod("BakeHitPoints",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method != null)
                {
                    method.Invoke(moduleHull, null);
                    EditorUtility.SetDirty(moduleHull);
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("ModuleHull 컴포넌트가 없습니다.", MessageType.Warning);
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Gizmo 표시", EditorStyles.boldLabel);

        // ShieldGrid 자신
        bool newShowGrid = EditorGUILayout.Toggle("Shield Grid", grid.bShowGrid);
        if (newShowGrid != grid.bShowGrid)
        {
            grid.bShowGrid = newShowGrid;
            EditorUtility.SetDirty(grid);
            SceneView.RepaintAll();
        }

        // HangarFlightPath
        HangarFlightPath flightPath = grid.GetComponentInChildren<HangarFlightPath>(true);
        if (flightPath != null)
        {
            bool newShowPath = EditorGUILayout.Toggle("Hangar Flight Path", flightPath.bShowGizmos);
            if (newShowPath != flightPath.bShowGizmos)
            {
                flightPath.bShowGizmos = newShowPath;
                EditorUtility.SetDirty(flightPath);
                SceneView.RepaintAll();
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

    // new Mesh()로 런타임 생성된 표면 메시는 프리팹 저장 시 자동으로 서브에셋화되지 않아 참조가 끊김(m_Mesh: {fileID: 0}) —
    // 프리팹 편집 모드의 에셋 경로에 명시적으로 추가해 프리팹 저장 시 함께 직렬화되도록 함
    void SaveSurfaceMeshAsSubAsset(ShieldGrid grid)
    {
        Mesh surfaceMesh = grid.GetSurfaceMesh();
        if (surfaceMesh == null) return;

        string assetPath = GetPrefabAssetPath();
        if (string.IsNullOrEmpty(assetPath) == true) return;

        RemoveSurfaceMeshSubAssetsAt(assetPath, surfaceMesh);

        AssetDatabase.AddObjectToAsset(surfaceMesh, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath);
    }

    // Clear All로 표면 메시 자체가 사라졌을 때, 프리팹에 고아로 남는 이전 서브에셋 정리
    void RemoveSurfaceMeshSubAsset()
    {
        string assetPath = GetPrefabAssetPath();
        if (string.IsNullOrEmpty(assetPath) == true) return;

        RemoveSurfaceMeshSubAssetsAt(assetPath, null);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath);
    }

    void RemoveSurfaceMeshSubAssetsAt(string assetPath, Mesh keepMesh)
    {
        Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        foreach (Object subAsset in subAssets)
        {
            if (subAsset is Mesh mesh && mesh.name == "ShieldSurfaceMesh" && subAsset != keepMesh)
                AssetDatabase.RemoveObjectFromAsset(subAsset);
        }
    }

    string GetPrefabAssetPath()
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage == null)
        {
            Debug.LogWarning("[ShieldGridEditor] 프리팹 편집 모드가 아니어서 표면 메시 서브에셋을 정리할 수 없습니다.");
            return null;
        }
        return prefabStage.assetPath;
    }
}
