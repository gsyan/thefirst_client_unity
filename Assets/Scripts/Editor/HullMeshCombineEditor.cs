// 함체 프리팹의 지정된 MeshRenderer들을 에디터에서 미리 합쳐 드로우콜을 줄이는 툴
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(CombineMeshTarget))]
public class CombineMeshTargetEditor : Editor
{
    private const string MeshSaveFolder   = "Assets/GeneratedMeshes/HullCombined";
    private const string CombinedHullName = "Combined";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);

        if (GUILayout.Button("Combine Meshes", GUILayout.Height(30)))
            CombineThis();
    }

    // ── 단일 처리 ──────────────────────────────────────────

    void CombineThis()
    {
        EnsureFolderExists("Assets", "GeneratedMeshes");
        EnsureFolderExists("Assets/GeneratedMeshes", "Combined");

        // Prefab Editor 전용 — 디스크 재로드 없이 라이브 Stage 오브젝트를 직접 사용
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null)
        {
            Debug.LogWarning("[CombineMeshTarget] Prefab Editor 에서만 사용 가능합니다.");
            return;
        }

        GameObject stageRoot = stage.prefabContentsRoot;
        if (stageRoot.TryGetComponent(out CombineMeshTarget stageTarget) == false)
        {
            Debug.LogWarning("[CombineMeshTarget] PrefabStage 루트에서 CombineMeshTarget을 찾을 수 없습니다.");
            return;
        }
        if (CombinePrefab(stageRoot, stageTarget, stage.assetPath))
            Debug.Log($"[CombineMeshTarget] {stageRoot.name} 완료");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // ── 공용 로직 ──────────────────────────────────────────

    static bool CombinePrefab(GameObject prefab, CombineMeshTarget target, string prefabPath)
    {
        // 지정된 루트 오브젝트들에서 재귀적으로 MeshFilter 수집
        var filters = target.m_combineTargets
            .Where(go => go != null)
            .SelectMany(go => go.GetComponentsInChildren<MeshFilter>(includeInactive: true))
            .Where(f => f.sharedMesh != null)
            .ToList();

        if (filters.Count == 0)
        {
            Debug.LogWarning($"[CombineMeshTarget] {prefab.name}: 유효한 메시 없음, 스킵");
            return false;
        }

        Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;
        CombineInstance[] combines = filters.Select(f => new CombineInstance
        {
            mesh      = f.sharedMesh,
            transform = rootInverse * f.transform.localToWorldMatrix
        }).ToArray();

        Mesh combinedMesh = new Mesh();
        combinedMesh.name = prefab.name + "_Combined";
        combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combinedMesh.CombineMeshes(combines, mergeSubMeshes: true, useMatrices: true);

        // 기존 파일 삭제 후 재생성 — CopySerialized+SaveAssetIfDirty는 dirty 마킹 누락 시 간헐적으로 저장 안 됨
        string meshPath = $"{MeshSaveFolder}/{prefab.name}_Combined.mesh";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null)
            AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(combinedMesh, meshPath);

        // 기존 오브젝트 삭제 후 재생성 (첫 번째 자식 위치 보장)
        Transform existing2 = prefab.transform.Find(CombinedHullName);
        if (existing2 != null)
            DestroyImmediate(existing2.gameObject);

        GameObject combinedGo = new(CombinedHullName);
        combinedGo.transform.SetParent(prefab.transform, worldPositionStays: false);
        combinedGo.transform.SetSiblingIndex(0);
        Transform combinedHullTf = combinedGo.transform;

        MeshFilter mf = combinedHullTf.GetComponent<MeshFilter>();
        if (mf == null) mf = combinedHullTf.gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);

        MeshRenderer mr = combinedHullTf.GetComponent<MeshRenderer>();
        if (mr == null) mr = combinedHullTf.gameObject.AddComponent<MeshRenderer>();
        MeshRenderer firstMr = filters[0].GetComponent<MeshRenderer>();
        mr.sharedMaterial    = firstMr != null ? firstMr.sharedMaterial : null;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        if (combinedHullTf.TryGetComponent(out MeshCollider mc) == false)
            mc = combinedHullTf.gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = mf.sharedMesh;

        PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        Debug.Log($"[HullMeshCombine] {prefab.name}: {filters.Count}개 → 1 드로우콜");
        return true;
    }

    static void EnsureFolderExists(string parent, string folderName)
    {
        string full = $"{parent}/{folderName}";
        if (AssetDatabase.IsValidFolder(full) == false)
            AssetDatabase.CreateFolder(parent, folderName);
    }
}
