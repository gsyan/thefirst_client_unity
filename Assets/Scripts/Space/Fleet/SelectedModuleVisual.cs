// 선택된 모듈 위에 GridOverlay 메시를 동적 생성하여 그리드 선택 효과를 표시.
// 원본 머티리얼/쉐이더와 무관하므로 신규 모델 도입 시 별도 작업 불필요.
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class SelectedModuleVisual : MonoBehaviour
{
    private SpaceShip m_myShip;
    private ModuleBase m_partsBase;
    [SerializeField] private bool m_isSelected = false;

    private readonly List<GameObject> m_overlayObjects = new();
    private Material m_overlayMaterial;

    public ModuleBase ModuleBase => m_partsBase;

    public void InitializeSelectedModuleVisual(SpaceShip ship, ModuleBase partsBase)
    {
        m_myShip = ship;
        m_partsBase = partsBase;

        if (transform.childCount <= 0) return;

        if (TryCreateOverlayMaterial() == true)
            BuildOverlayRenderers();
    }

    private bool TryCreateOverlayMaterial()
    {
        // Resources 폴더 경유로 로드해야 Android 빌드 스트리핑 방지
        Shader shader = ResourceManager.Instance.Load<Shader>("Shaders/GridOverlay");
        if (shader == null)
        {
            Debug.LogError("[SelectedModuleVisual] Resources/Shaders/GridOverlay 쉐이더를 찾을 수 없습니다.");
            return false;
        }
        m_overlayMaterial = new Material(shader);
        m_overlayMaterial.SetFloat("_GridSpacing", CalculateGridSpacing());
        return true;
    }

    // 월드 바운드 기준으로 4칸 그리드 간격 계산
    private float CalculateGridSpacing()
    {
        if (transform.childCount <= 0) return 0.4f;

        var renderers = new List<Renderer>();
        CollectOwnRenderers(transform, renderers);
        if (renderers.Count == 0) return 0.4f;

        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers)
            bounds.Encapsulate(r.bounds);

        float minSize = Mathf.Min(bounds.size.x, bounds.size.y, bounds.size.z);
        return Mathf.Max(minSize / 4.0f, 0.05f);
    }

    // ModuleBase가 있는 자식 = 별도 모듈 경계 → 탐색 중단
    private void CollectOwnRenderers(Transform t, List<Renderer> result)
    {
        foreach (Transform child in t)
        {
            if (child.GetComponent<ModuleBase>() != null) continue;
            Renderer r = child.GetComponent<Renderer>();
            if (r != null) result.Add(r);
            CollectOwnRenderers(child, result);
        }
    }

    private void CollectOwnMeshFilters(Transform t, List<MeshFilter> result)
    {
        foreach (Transform child in t)
        {
            if (child.GetComponent<ModuleBase>() != null) continue;
            if (child.name == "_GridOverlay") continue;
            MeshFilter mf = child.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) result.Add(mf);
            CollectOwnMeshFilters(child, result);
        }
    }

    private void BuildOverlayRenderers()
    {
        DestroyOverlayObjects();

        var meshFilters = new List<MeshFilter>();
        CollectOwnMeshFilters(transform, meshFilters);
        foreach (var mf in meshFilters)
        {
            GameObject overlayObj = new GameObject("_GridOverlay");
            overlayObj.transform.SetParent(mf.transform, false);
            overlayObj.SetActive(m_isSelected);

            overlayObj.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;

            var mr = overlayObj.AddComponent<MeshRenderer>();
            // 서브메시 수만큼 같은 머티리얼로 채워야 모든 파트가 커버됨
            int subMeshCount = mf.sharedMesh.subMeshCount;
            Material[] mats = new Material[subMeshCount];
            for (int i = 0; i < subMeshCount; i++)
                mats[i] = m_overlayMaterial;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            m_overlayObjects.Add(overlayObj);
        }
    }

    private void DestroyOverlayObjects()
    {
        foreach (var obj in m_overlayObjects)
        {
            if (obj == null) continue;
            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }
        m_overlayObjects.Clear();
    }

    public void SetSelected(bool selected)
    {
        if (m_isSelected == selected) return;
        m_isSelected = selected;
        ApplySelection();
    }

    private void ApplySelection()
    {
        foreach (var obj in m_overlayObjects)
        {
            if (obj != null)
                obj.SetActive(m_isSelected);
        }
    }

    private void OnDestroy()
    {
        DestroyOverlayObjects();
        if (m_overlayMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(m_overlayMaterial);
            else
                DestroyImmediate(m_overlayMaterial);
        }
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (Application.isPlaying == true) return;

        // 에디터에서 m_isSelected 인스펙터 토글 시 오버레이 재생성하여 미리보기
        if (m_overlayObjects.Count == 0 && transform.childCount > 0 && TryCreateOverlayMaterial() == true)
            BuildOverlayRenderers();
    }

    private void OnValidate()
    {
        ApplySelection();
    }
#endif
}
