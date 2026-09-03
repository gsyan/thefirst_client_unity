using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum EShieldGridMode
{
    Triangle,  // 삼각형 그리드 (꼭지점 이웃 5~6개)
    Hexagon    // 벌집 그리드 (꼭지점 이웃 3개)
}

[System.Serializable]
public struct BoundingBox
{
    public Vector3 center;
    public Vector3 size;
    public Vector3 extents;
    public float margin;
    public Quaternion rotation; // ***추가*** 박스의 회전

    public BoundingBox(Vector3 c, Vector3 s, float m, Quaternion r)
    {
        center = c;
        size = s;
        extents = s * 0.5f;
        margin = m;
        rotation = r;
    }
}

/// <summary>
// Geodesic Dome 기반 보호층 그리드. 삼각형/헥사곤 모드 지원.

public class ShieldGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    public EShieldGridMode gridMode = EShieldGridMode.Triangle;

    [Tooltip("세분화 횟수 (0=12, 1=42, 2=162, 3=642 기본 꼭지점)")]
    [Range(0, 4)]
    public int subdivisions = 1;

    [Tooltip("바운드 마진")]
    public float boundMargin = 0.3f;

    [Tooltip("바운드 스케일 배율 (전체)")]
    public float boundScale = 1.2f;

    [Tooltip("축별 추가 스케일 (X=좌우, Y=상하, Z=전후)")]
    public Vector3 axisScale = Vector3.one;

    [Header("Collider Settings")]
    [Tooltip("표면 메시(렌더링+콜라이더) 생성 여부")]
    public bool generateCollider = true;

    [Tooltip("Shield 레이어 이름 (미리 생성 필요)")]
    public string shieldLayerName = "Shield";

    [Tooltip("실드 표면 파동 셰이더 머티리얼")]
    public Material surfaceMaterial;

    [Tooltip("육각형 판 경계를 각지게 표시 (끄면 인접 셀과 매끄럽게 이어짐)")]
    public bool flatShading = false;

    [Header("References")]
    public Transform m_PointParent;

    public List<ShieldVertex> m_vertices = new List<ShieldVertex>();
    public List<ShieldCell> m_cells = new List<ShieldCell>();

    private BoundingBox m_boundBox;
    [SerializeField, HideInInspector] private Vector3 m_extents;

    // 충돌체 관련
    private GameObject m_colliderObject;
    private MeshCollider m_meshCollider;

    // 표면 렌더링 관련 — 콜라이더와 같은 메시(BuildSurfaceMesh 결과)를 공유
    private GameObject m_surfaceObject;
    private MeshRenderer m_surfaceMeshRenderer;
    private MaterialPropertyBlock m_surfaceMpb;
    private static readonly int k_hitDataId = Shader.PropertyToID("_HitData");

    // 파동 웨이브 슬롯 — ShieldSurfaceWave.shader의 SHIELD_WAVE_SLOT_COUNT와 반드시 동일한 개수 유지
    // 링버퍼: 새 피격이 들어오면 가장 오래된(가장 먼저 채워진) 슬롯을 덮어써 동시/연속 피격을 함께 표시(GC 없이 배열 재사용)
    private const int k_hitWaveSlotCount = 4;
    private readonly Vector4[] m_hitDataSlots = new Vector4[k_hitWaveSlotCount];
    private int m_nextHitWaveSlot;

    private static readonly float PHI = (1f + Mathf.Sqrt(5f)) / 2f;

    // 내부 데이터 (듀얼 변환용)
    private List<Vector3> m_icoVertices = new List<Vector3>();
    private List<TriangleIndices> m_icoTriangles = new List<TriangleIndices>();

    // 런타임 전용 — PlayHitWave가 매번 Find/GetComponent를 하지 않도록 표면 렌더러를 캐싱 (에디터 타임 필드는 런타임에 비어있을 수 있음, InitFormationRelay와 동일 관례)
    void Awake()
    {
        Transform surfaceChild = transform.Find("ShieldSurface");
        if (surfaceChild != null)
            m_surfaceMeshRenderer = surfaceChild.GetComponent<MeshRenderer>();

        // 슬롯의 hitTime(w)을 셰이더 기본값과 동일한 과거값으로 초기화 — 게임 시작 직후 원점에서 파동이 보이는 것을 방지
        for (int i = 0; i < k_hitWaveSlotCount; i++)
            m_hitDataSlots[i] = new Vector4(0f, 0f, 0f, -1000f);

        // 초기화한 값을 실제 렌더러에 반영 — _HitData는 Properties 블록에 없는 CBUFFER 배열이라 머티리얼 기본값이 없어서,
        // SetPropertyBlock으로 직접 밀어넣지 않으면 GPU에서 미정의 상태로 그려짐(PlayHitWave와 동일 패턴)
        if (m_surfaceMeshRenderer != null)
        {
            if (m_surfaceMpb == null)
                m_surfaceMpb = new MaterialPropertyBlock();

            m_surfaceMeshRenderer.GetPropertyBlock(m_surfaceMpb);
            m_surfaceMpb.SetVectorArray(k_hitDataId, m_hitDataSlots);
            m_surfaceMeshRenderer.SetPropertyBlock(m_surfaceMpb);
        }
    }

    public void GenerateShield()
    {
        ClearAll();

        if (m_PointParent == null)
        {
            Debug.LogError("PointParent가 설정되지 않았습니다.");
            return;
        }

        m_boundBox = ComputeBoundingBox(transform, boundMargin);
        m_extents = Vector3.Scale(m_boundBox.extents * boundScale, axisScale);

        // 정이십면체 생성 및 세분화
        GenerateIcosahedron();
        for (int i = 0; i < subdivisions; i++)
            Subdivide();

        // 모드에 따라 생성
        if (gridMode == EShieldGridMode.Triangle)
            GenerateTriangleMode();
        else
            GenerateHexagonMode();

        // 표면 렌더링 메시 + 콜라이더 생성 (같은 메시 공유)
        if (generateCollider)
            GenerateSurfaceAndCollider();

        Debug.Log($"ShieldGrid 생성 [{gridMode}]: {m_vertices.Count}개 꼭지점, {m_cells.Count}개 셀");
    }

    /// <summary>
    /// 히트 지점에서 가장 가까운 셀 반환
    /// </summary>
    public ShieldCell GetHitCell(Vector3 hitPoint)
    {
        if (m_cells.Count == 0) return null;

        ShieldCell closest = null;
        float minDistSqr = float.MaxValue;

        foreach (var cell in m_cells)
        {
            float distSqr = (cell.center - hitPoint).sqrMagnitude;
            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                closest = cell;
            }
        }
        return closest;
    }

    /// <summary>
    /// 히트 지점에서 가장 가까운 꼭지점 반환
    /// </summary>
    public ShieldVertex GetHitVertex(Vector3 hitPoint)
    {
        if (m_vertices.Count == 0) return null;

        ShieldVertex closest = null;
        float minDistSqr = float.MaxValue;

        foreach (var v in m_vertices)
        {
            if (v == null) continue;
            float distSqr = (v.transform.position - hitPoint).sqrMagnitude;
            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                closest = v;
            }
        }
        return closest;
    }

    // 실드 표면 파동 셰이더 트리거 — 게이지가 데미지를 흡수한 시점(SpaceShip.SpawnShieldHitEffect)마다 호출
    // 링버퍼 슬롯 하나(가장 오래된 것)를 이번 피격으로 덮어써 동시/연속 피격이 각자의 파동으로 함께 표시되게 함
    // ProjectileBeam.cs의 MaterialPropertyBlock 패턴과 동일: GetPropertyBlock → Set → SetPropertyBlock, GC Alloc 없음(배열은 필드로 재사용)
    public void PlayHitWave(Vector3 hitPositionWS)
    {
        if (m_surfaceMeshRenderer == null) return;

        if (m_surfaceMpb == null)
            m_surfaceMpb = new MaterialPropertyBlock();

        Vector3 hitLocal = transform.InverseTransformPoint(hitPositionWS);
        m_hitDataSlots[m_nextHitWaveSlot] = new Vector4(hitLocal.x, hitLocal.y, hitLocal.z, Time.time);
        m_nextHitWaveSlot = (m_nextHitWaveSlot + 1) % k_hitWaveSlotCount;

        m_surfaceMeshRenderer.GetPropertyBlock(m_surfaceMpb);
        m_surfaceMpb.SetVectorArray(k_hitDataId, m_hitDataSlots);
        m_surfaceMeshRenderer.SetPropertyBlock(m_surfaceMpb);
    }

    // m_cells(육각형/오각형 셀, 이미 각도 정렬된 vertexIndices)를 셀마다 삼각형 팬으로 분해해 로컬 좌표 메시로 굽는다.
    // 렌더링(ShieldSurface)과 콜라이더(ShieldCollider)가 이 메시를 그대로 공유 — 형상을 일치시켜 raycast/trigger가 실제 표면과 어긋나지 않게 함
    // Mesh.RecalculateNormals()는 위치가 같은 정점을 자동으로 병합해 노멀을 평균 내므로(smooth), 정점을 공유하기만 해도 매끄러운 구면이 된다.
    // flatShading이 true면 셀마다 정점을 새로 추가(공유 안 함)하고 각 삼각형 면 노멀을 직접 계산해 넣어 판 경계가 각지게 드러나도록 함
    Mesh BuildSurfaceMesh()
    {
        if (m_cells.Count == 0)
        {
            Debug.LogWarning("[ShieldGrid] m_cells가 비어있어 표면 메시를 생성할 수 없습니다.");
            return null;
        }

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> flatNormals = new List<Vector3>();
        List<int> triangles = new List<int>();

        // smooth 모드에서만 사용 — m_vertices 인덱스별로 로컬 좌표를 한 번만 만들어 여러 셀이 같은 인덱스를 공유
        Dictionary<int, int> sharedVertexCache = new Dictionary<int, int>();

        foreach (ShieldCell cell in m_cells)
        {
            if (cell.vertexIndices == null || cell.vertexIndices.Count < 3) continue;

            Vector3 centerLocal = transform.InverseTransformPoint(cell.center);
            Vector3 outwardLocal = centerLocal.normalized;

            int centerIdx = vertices.Count;
            vertices.Add(centerLocal);
            if (flatShading == true)
                flatNormals.Add(outwardLocal);

            int fanStart = vertices.Count;
            int fanCount = cell.vertexIndices.Count;
            for (int i = 0; i < fanCount; i++)
            {
                int sourceIdx = cell.vertexIndices[i];
                Vector3 pointLocal = transform.InverseTransformPoint(m_vertices[sourceIdx].transform.position);

                if (flatShading == true)
                {
                    vertices.Add(pointLocal);
                    flatNormals.Add(outwardLocal);
                    continue;
                }

                if (sharedVertexCache.TryGetValue(sourceIdx, out int cachedIdx) == false)
                {
                    cachedIdx = vertices.Count;
                    vertices.Add(pointLocal);
                    sharedVertexCache[sourceIdx] = cachedIdx;
                }
                else
                {
                    vertices.Add(vertices[cachedIdx]);
                }
            }

            // winding이 바깥(중심에서 멀어지는 방향)을 향하도록, 첫 삼각형 노멀과 바깥 방향의 내적으로 팬 순서 확정
            Vector3 edgeA = vertices[fanStart] - centerLocal;
            Vector3 edgeB = vertices[fanStart + (1 % fanCount)] - centerLocal;
            bool flipWinding = Vector3.Dot(Vector3.Cross(edgeA, edgeB), outwardLocal) < 0f;

            for (int i = 0; i < fanCount; i++)
            {
                int a = fanStart + i;
                int b = fanStart + (i + 1) % fanCount;
                if (flipWinding == true)
                {
                    triangles.Add(centerIdx);
                    triangles.Add(b);
                    triangles.Add(a);
                }
                else
                {
                    triangles.Add(centerIdx);
                    triangles.Add(a);
                    triangles.Add(b);
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "ShieldSurfaceMesh";
        if (vertices.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        if (flatShading == true)
            mesh.SetNormals(flatNormals);
        else
            mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 표면 렌더링(ShieldSurface)과 콜라이더(ShieldCollider)를 같은 메시로 함께 생성 — 기존 GenerateCollider() 대체
    void GenerateSurfaceAndCollider()
    {
        Mesh surfaceMesh = BuildSurfaceMesh();
        if (surfaceMesh == null) return;

        // 레이어 설정
        int layer = LayerMask.NameToLayer(shieldLayerName);
        if (layer < 0)
            Debug.LogWarning($"Shield 레이어 '{shieldLayerName}'가 없습니다. Project Settings > Tags and Layers에서 생성하세요.");

        // 렌더링 오브젝트 — 로컬 좌표 정점이므로 부모(this.transform) 자체가 오프셋, local TRS는 identity
        if (m_surfaceObject != null)
            DestroyImmediate(m_surfaceObject);
        m_surfaceObject = new GameObject("ShieldSurface");
        m_surfaceObject.transform.SetParent(transform, false);

        MeshFilter meshFilter = m_surfaceObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = surfaceMesh;
        m_surfaceMeshRenderer = m_surfaceObject.AddComponent<MeshRenderer>();
        m_surfaceMeshRenderer.sharedMaterial = surfaceMaterial;
        m_surfaceMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        m_surfaceMeshRenderer.receiveShadows = false;

        // 콜라이더 오브젝트 — 정점 자체가 이미 최종 타원체 형상으로 구워져 있어 별도 스케일/오프셋 불필요(identity)
        if (m_colliderObject != null)
            DestroyImmediate(m_colliderObject);
        m_colliderObject = new GameObject("ShieldCollider");
        m_colliderObject.transform.SetParent(transform, false);
        if (layer >= 0)
            m_colliderObject.layer = layer;

        m_meshCollider = m_colliderObject.AddComponent<MeshCollider>();
        m_meshCollider.sharedMesh = surfaceMesh;
        m_meshCollider.convex = true;
        m_meshCollider.isTrigger = true;

        // OnTriggerStay 발생 조건: Rigidbody 필요 → kinematic으로 물리 영향 차단 (프리팹에 저장됨)
        Rigidbody rb = m_colliderObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    // 진형 배치 계산용 — 실드 콜라이더의 실제 반크기 반환 (boundScale/axisScale 적용됨)
    public Vector3 GetFormationExtents() => m_extents;

    // 에디터 전용 — GenerateShield() 직후 생성된 표면 메시를 프리팹 서브에셋으로 저장하기 위한 조회(ShieldGridEditor.cs)
    public Mesh GetSurfaceMesh()
    {
        if (m_surfaceObject == null) return null;
        MeshFilter meshFilter = m_surfaceObject.GetComponent<MeshFilter>();
        if (meshFilter == null) return null;
        return meshFilter.sharedMesh;
    }

    // 런타임 전용 — SpaceShip 로딩 시점에 호출하여 진형 충돌 릴레이 owner 설정
    public void InitFormationRelay(SpaceShip owner)
    {
        Transform colliderChild = transform.Find("ShieldCollider");
        if (colliderChild == null)
        {
            Debug.LogWarning($"[ShieldGrid] ShieldCollider 자식을 찾을 수 없음: {gameObject.name}");
            return;
        }

        if (colliderChild.TryGetComponent<ShieldTriggerRelay>(out var relay) == false)
            relay = colliderChild.gameObject.AddComponent<ShieldTriggerRelay>();
        relay.owner = owner;
    }

    /// <summary>
    /// 삼각형 모드: icosahedron 꼭지점 = ShieldVertex, 삼각형 = ShieldCell
    /// </summary>
    void GenerateTriangleMode()
    {
        Dictionary<int, int> icoToVertex = new Dictionary<int, int>();

        // 꼭지점 생성
        for (int i = 0; i < m_icoVertices.Count; i++)
        {
            Vector3 worldPos = ToWorldPosition(m_icoVertices[i]);
            ShieldVertex v = CreateVertex(worldPos, m_vertices.Count);
            icoToVertex[i] = m_vertices.Count;
            m_vertices.Add(v);
        }

        // 이웃 관계 (삼각형 엣지)
        HashSet<long> processedEdges = new HashSet<long>();
        foreach (var tri in m_icoTriangles)
        {
            ConnectVertices(tri.v0, tri.v1, icoToVertex, processedEdges);
            ConnectVertices(tri.v1, tri.v2, icoToVertex, processedEdges);
            ConnectVertices(tri.v2, tri.v0, icoToVertex, processedEdges);
        }

        // 셀 생성 (삼각형)
        for (int i = 0; i < m_icoTriangles.Count; i++)
        {
            var tri = m_icoTriangles[i];
            Vector3 center = (m_icoVertices[tri.v0] + m_icoVertices[tri.v1] + m_icoVertices[tri.v2]) / 3f;
            Vector3 worldCenter = ToWorldPosition(center.normalized);

            ShieldCell cell = new ShieldCell
            {
                index = i,
                center = worldCenter,
                vertexIndices = new List<int> { icoToVertex[tri.v0], icoToVertex[tri.v1], icoToVertex[tri.v2] }
            };
            m_cells.Add(cell);
        }
    }

    /// <summary>
    /// 헥사곤 모드: 삼각형 중심 = ShieldVertex (이웃 3개), icosahedron 꼭지점 = ShieldCell 중심
    /// </summary>
    void GenerateHexagonMode()
    {
        // 각 삼각형 중심이 헥사곤 꼭지점
        Dictionary<int, int> triToVertex = new Dictionary<int, int>();

        for (int i = 0; i < m_icoTriangles.Count; i++)
        {
            var tri = m_icoTriangles[i];
            Vector3 center = (m_icoVertices[tri.v0] + m_icoVertices[tri.v1] + m_icoVertices[tri.v2]) / 3f;
            Vector3 worldPos = ToWorldPosition(center.normalized);

            ShieldVertex v = CreateVertex(worldPos, m_vertices.Count);
            triToVertex[i] = m_vertices.Count;
            m_vertices.Add(v);
        }

        // 이웃 관계: 엣지를 공유하는 삼각형들이 이웃
        Dictionary<long, List<int>> edgeToTriangles = new Dictionary<long, List<int>>();

        for (int i = 0; i < m_icoTriangles.Count; i++)
        {
            var tri = m_icoTriangles[i];
            RegisterTriangleEdge(edgeToTriangles, tri.v0, tri.v1, i);
            RegisterTriangleEdge(edgeToTriangles, tri.v1, tri.v2, i);
            RegisterTriangleEdge(edgeToTriangles, tri.v2, tri.v0, i);
        }

        // 같은 엣지를 공유하는 두 삼각형의 중심점들을 연결
        foreach (var pair in edgeToTriangles)
        {
            if (pair.Value.Count == 2)
            {
                int t0 = pair.Value[0];
                int t1 = pair.Value[1];
                int v0 = triToVertex[t0];
                int v1 = triToVertex[t1];

                m_vertices[v0].neighborIndices.Add(v1);
                m_vertices[v1].neighborIndices.Add(v0);
            }
        }

        // 셀 생성: 각 원본 icosahedron 꼭지점 주변의 삼각형들이 하나의 셀 (헥사곤 또는 펜타곤)
        Dictionary<int, List<int>> vertexToTriangles = new Dictionary<int, List<int>>();

        for (int i = 0; i < m_icoTriangles.Count; i++)
        {
            var tri = m_icoTriangles[i];
            AddToList(vertexToTriangles, tri.v0, i);
            AddToList(vertexToTriangles, tri.v1, i);
            AddToList(vertexToTriangles, tri.v2, i);
        }

        int cellIdx = 0;
        foreach (var pair in vertexToTriangles)
        {
            int icoVertexIdx = pair.Key;
            List<int> triangleIndices = pair.Value;

            Vector3 worldCenter = ToWorldPosition(m_icoVertices[icoVertexIdx]);

            // 셀의 꼭지점들 (삼각형 중심들)
            List<int> cellVertices = new List<int>();
            foreach (int triIdx in triangleIndices)
                cellVertices.Add(triToVertex[triIdx]);

            // 꼭지점들을 중심 기준으로 정렬 (시계/반시계 방향)
            SortVerticesAroundCenter(cellVertices, worldCenter);

            ShieldCell cell = new ShieldCell
            {
                index = cellIdx++,
                center = worldCenter,
                vertexIndices = cellVertices
            };
            m_cells.Add(cell);
        }
    }

    void RegisterTriangleEdge(Dictionary<long, List<int>> edgeToTriangles, int v0, int v1, int triIdx)
    {
        long key = v0 < v1 ? ((long)v0 << 32) | (uint)v1 : ((long)v1 << 32) | (uint)v0;
        if (!edgeToTriangles.ContainsKey(key))
            edgeToTriangles[key] = new List<int>();
        edgeToTriangles[key].Add(triIdx);
    }

    void AddToList(Dictionary<int, List<int>> dict, int key, int value)
    {
        if (!dict.ContainsKey(key))
            dict[key] = new List<int>();
        dict[key].Add(value);
    }

    void SortVerticesAroundCenter(List<int> vertexIndices, Vector3 center)
    {
        if (vertexIndices.Count < 3) return;

        Vector3 normal = center.normalized;
        Vector3 refDir = (m_vertices[vertexIndices[0]].transform.position - center).normalized;

        vertexIndices.Sort((a, b) =>
        {
            Vector3 dirA = (m_vertices[a].transform.position - center).normalized;
            Vector3 dirB = (m_vertices[b].transform.position - center).normalized;

            float angleA = Vector3.SignedAngle(refDir, dirA, normal);
            float angleB = Vector3.SignedAngle(refDir, dirB, normal);

            return angleA.CompareTo(angleB);
        });
    }

    void GenerateIcosahedron()
    {
        m_icoVertices.Clear();
        m_icoTriangles.Clear();

        float a = 1f;
        float b = 1f / PHI;

        Vector3[] v = new Vector3[]
        {
            new Vector3(-b, a, 0), new Vector3(b, a, 0), new Vector3(-b, -a, 0), new Vector3(b, -a, 0),
            new Vector3(0, -b, a), new Vector3(0, b, a), new Vector3(0, -b, -a), new Vector3(0, b, -a),
            new Vector3(a, 0, -b), new Vector3(a, 0, b), new Vector3(-a, 0, -b), new Vector3(-a, 0, b)
        };

        for (int i = 0; i < v.Length; i++)
            m_icoVertices.Add(v[i].normalized);

        int[] idx = new int[]
        {
            0,11,5,  0,5,1,  0,1,7,  0,7,10, 0,10,11,
            1,5,9,   5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4,   3,4,2,  3,2,6,  3,6,8,  3,8,9,
            4,9,5,   2,4,11, 6,2,10, 8,6,7,  9,8,1
        };

        for (int i = 0; i < idx.Length; i += 3)
            m_icoTriangles.Add(new TriangleIndices { v0 = idx[i], v1 = idx[i + 1], v2 = idx[i + 2] });
    }

    void Subdivide()
    {
        Dictionary<long, int> midpointCache = new Dictionary<long, int>();
        List<TriangleIndices> newTriangles = new List<TriangleIndices>();

        foreach (var tri in m_icoTriangles)
        {
            int a = GetMidpoint(tri.v0, tri.v1, midpointCache);
            int b = GetMidpoint(tri.v1, tri.v2, midpointCache);
            int c = GetMidpoint(tri.v2, tri.v0, midpointCache);

            newTriangles.Add(new TriangleIndices { v0 = tri.v0, v1 = a, v2 = c });
            newTriangles.Add(new TriangleIndices { v0 = tri.v1, v1 = b, v2 = a });
            newTriangles.Add(new TriangleIndices { v0 = tri.v2, v1 = c, v2 = b });
            newTriangles.Add(new TriangleIndices { v0 = a, v1 = b, v2 = c });
        }

        m_icoTriangles = newTriangles;
    }

    int GetMidpoint(int i0, int i1, Dictionary<long, int> cache)
    {
        long key = i0 < i1 ? ((long)i0 << 32) | (uint)i1 : ((long)i1 << 32) | (uint)i0;

        if (cache.TryGetValue(key, out int midIdx))
            return midIdx;

        Vector3 mid = ((m_icoVertices[i0] + m_icoVertices[i1]) * 0.5f).normalized;
        midIdx = m_icoVertices.Count;
        m_icoVertices.Add(mid);
        cache[key] = midIdx;

        return midIdx;
    }

    Vector3 ToWorldPosition(Vector3 unitSpherePoint)
    {
        Vector3 ellipsoidPos = new Vector3(
            unitSpherePoint.x * m_extents.x,
            unitSpherePoint.y * m_extents.y,
            unitSpherePoint.z * m_extents.z
        );
        return m_boundBox.center + m_boundBox.rotation * ellipsoidPos;
    }

    void ConnectVertices(int i0, int i1, Dictionary<int, int> icoToVertex, HashSet<long> processed)
    {
        long key = i0 < i1 ? ((long)i0 << 32) | (uint)i1 : ((long)i1 << 32) | (uint)i0;
        if (processed.Contains(key)) return;
        processed.Add(key);

        int h0 = icoToVertex[i0];
        int h1 = icoToVertex[i1];

        m_vertices[h0].neighborIndices.Add(h1);
        m_vertices[h1].neighborIndices.Add(h0);
    }

    ShieldVertex CreateVertex(Vector3 position, int index)
    {
        GameObject vertexObj = new GameObject($"ShieldVertex_{index}");
        vertexObj.transform.position = position;
        vertexObj.transform.SetParent(m_PointParent);

        ShieldVertex vertex = vertexObj.AddComponent<ShieldVertex>();
        vertex.index = index;
        vertex.neighborIndices = new List<int>();

        return vertex;
    }

    public void ClearAll()
    {
        foreach (var v in m_vertices)
        {
            if (v != null && v.gameObject != null)
                DestroyImmediate(v.gameObject);
        }
        m_vertices.Clear();
        m_cells.Clear();
        m_icoVertices.Clear();
        m_icoTriangles.Clear();

        if (m_PointParent != null)
        {
            while (m_PointParent.childCount > 0)
                DestroyImmediate(m_PointParent.GetChild(0).gameObject);
        }

        // 충돌체 제거 (참조가 끊어져도 이름으로 찾아서 삭제)
        if (m_colliderObject != null)
        {
            DestroyImmediate(m_colliderObject);
        }
        else
        {
            var existing = transform.Find("ShieldCollider");
            if (existing != null)
                DestroyImmediate(existing.gameObject);
        }
        m_colliderObject = null;
        m_meshCollider = null;

        // 표면 렌더링 오브젝트 제거 (참조가 끊어져도 이름으로 찾아서 삭제)
        if (m_surfaceObject != null)
        {
            DestroyImmediate(m_surfaceObject);
        }
        else
        {
            var existingSurface = transform.Find("ShieldSurface");
            if (existingSurface != null)
                DestroyImmediate(existingSurface.gameObject);
        }
        m_surfaceObject = null;
        m_surfaceMeshRenderer = null;
    }

    BoundingBox ComputeBoundingBox(Transform ship, float margin)
    {
        Renderer[] renderers = ship.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new BoundingBox(ship.position, Vector3.one, margin, ship.rotation);

        List<Vector3> worldPoints = new List<Vector3>();

        foreach (var r in renderers)
        {
            Bounds lb = r.localBounds;
            Vector3 ext = lb.extents;
            Vector3 center = lb.center;

            Vector3[] localCorners =
            {
                center + new Vector3(+ext.x, +ext.y, +ext.z),
                center + new Vector3(+ext.x, +ext.y, -ext.z),
                center + new Vector3(+ext.x, -ext.y, +ext.z),
                center + new Vector3(+ext.x, -ext.y, -ext.z),
                center + new Vector3(-ext.x, +ext.y, +ext.z),
                center + new Vector3(-ext.x, +ext.y, -ext.z),
                center + new Vector3(-ext.x, -ext.y, +ext.z),
                center + new Vector3(-ext.x, -ext.y, -ext.z),
            };

            foreach (var p in localCorners)
                worldPoints.Add(r.transform.TransformPoint(p));
        }

        Matrix4x4 worldToShip = ship.worldToLocalMatrix;
        Bounds localOBB = new Bounds(worldToShip.MultiplyPoint3x4(worldPoints[0]), Vector3.zero);
        for (int i = 1; i < worldPoints.Count; i++)
            localOBB.Encapsulate(worldToShip.MultiplyPoint3x4(worldPoints[i]));

        Vector3 worldSize = Vector3.Scale(localOBB.size, ship.lossyScale);
        Vector3 worldCenter = ship.TransformPoint(localOBB.center);
        worldSize += Vector3.one * margin * 2f;

        return new BoundingBox(worldCenter, worldSize, margin, ship.rotation);
    }

    struct TriangleIndices
    {
        public int v0, v1, v2;
    }

#if UNITY_EDITOR
    [Header("Debug")]
    [HideInInspector] public bool bShowGrid = true;
    [SerializeField] private Color vertexColor = Color.cyan;
    [SerializeField] private Color edgeColor = Color.yellow;
    [SerializeField] private Color cellColor = new Color(1f, 0.5f, 0f, 0.5f);
    [SerializeField] private bool bShowCellOutline = true;

    // m_PointParent의 자식에서 ShieldVertex를 다시 수집
    void RefreshVertexReferences()
    {
        if (m_PointParent == null)
        {
            Debug.LogWarning("[ShieldGrid] m_PointParent가 null입니다");
            return;
        }

        var vertices = m_PointParent.GetComponentsInChildren<ShieldVertex>();
        Debug.Log($"[ShieldGrid] RefreshVertexReferences: {vertices.Length}개 발견 (m_vertices.Count={m_vertices.Count})");

        if (vertices.Length == 0) return;

        m_vertices.Clear();
        m_vertices.AddRange(vertices);
        m_vertices.Sort((a, b) => a.index.CompareTo(b.index));
    }

    private void OnDrawGizmos()
    {
        if (!bShowGrid) return;
        
        // destroyed 객체 감지 시 자동 복구
        if (m_vertices.Count > 0 && m_vertices[0] == null)
            RefreshVertexReferences();

        // 꼭지점
        Gizmos.color = vertexColor;
        foreach (var v in m_vertices)
        {
            if (v != null)
                Gizmos.DrawSphere(v.transform.position, 0.05f);
        }

        // 엣지
        Gizmos.color = edgeColor;
        HashSet<long> drawnEdges = new HashSet<long>();
        foreach (var v in m_vertices)
        {
            if (v == null) continue;
            foreach (int neighborIdx in v.neighborIndices)
            {
                if (neighborIdx >= m_vertices.Count) continue;

                long key = v.index < neighborIdx ? ((long)v.index << 32) | (uint)neighborIdx : ((long)neighborIdx << 32) | (uint)v.index;
                if (drawnEdges.Contains(key)) continue;
                drawnEdges.Add(key);

                Gizmos.DrawLine(v.transform.position, m_vertices[neighborIdx].transform.position);
            }
        }

        // 셀 중심 및 외곽선
        if (bShowCellOutline && gridMode == EShieldGridMode.Hexagon)
        {
            Gizmos.color = cellColor;
            foreach (var cell in m_cells)
            {
                Gizmos.DrawSphere(cell.center, 0.03f);

                // 셀 외곽선 그리기
                if (cell.vertexIndices.Count >= 3)
                {
                    for (int i = 0; i < cell.vertexIndices.Count; i++)
                    {
                        int curr = cell.vertexIndices[i];
                        int next = cell.vertexIndices[(i + 1) % cell.vertexIndices.Count];

                        if (curr < m_vertices.Count && next < m_vertices.Count)
                        {
                            Gizmos.DrawLine(m_vertices[curr].transform.position, m_vertices[next].transform.position);
                        }
                    }
                }
            }
        }
        else if (gridMode == EShieldGridMode.Triangle)
        {
            Gizmos.color = cellColor;
            foreach (var cell in m_cells)
                Gizmos.DrawSphere(cell.center, 0.03f);
        }

        // // 타원체 바운드 (m_boundBox는 비직렬화 — rotation이 zero면 identity로 대체)
        // if (m_extents != Vector3.zero)
        // {
        //     Quaternion rot = m_boundBox.rotation;
        //     if (rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f)
        //         rot = Quaternion.identity;
        //     Gizmos.color = Color.green;
        //     Gizmos.matrix = Matrix4x4.TRS(m_boundBox.center, rot, m_extents * 2f);
        //     Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
        //     Gizmos.matrix = Matrix4x4.identity;
        // }
    }
#endif
}

[System.Serializable]
public class ShieldCell
{
    public int index;
    public Vector3 center;
    public List<int> vertexIndices;
    public float hp = 100f;
    public bool isDestroyed = false;
}

// 실드 콜라이더(자식 오브젝트)의 OnTriggerStay를 부모 SpaceShip으로 전달
[RequireComponent(typeof(Collider))]
public class ShieldTriggerRelay : MonoBehaviour
{
    public SpaceShip owner;
    private Collider m_collider;
    // 현재 겹치는 콜라이더 목록 (OnTriggerStay/Exit에서만 관리)
    private readonly HashSet<Collider> m_overlapping = new();
    // GetComponentInParent 반복 호출 방지용 캐시
    private readonly Dictionary<Collider, SpaceShip> m_shipCache = new();

    private void Awake()
    {
        m_collider = GetComponent<Collider>();
    }

    // 물리 콜백: 겹침 목록만 기록, 무거운 작업 없음
    private void OnTriggerStay(Collider other)
    {
        m_overlapping.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        m_overlapping.Remove(other);
        m_shipCache.Remove(other);
    }

    // Update: 진형 이동 중일 때만 ComputePenetration 수행
    private void Update()
    {
        if (owner == null || owner.m_formationMoveState != FormationMoveState.Moving) return;
        if (m_collider == null || m_overlapping.Count == 0) return;

        foreach (Collider other in m_overlapping)
        {
            if (other == null) continue;

            if (m_shipCache.TryGetValue(other, out SpaceShip otherShip) == false)
            {
                otherShip = other.GetComponentInParent<SpaceShip>();
                m_shipCache[other] = otherShip;
            }
            if (otherShip == null || otherShip == owner) continue;

            Physics.ComputePenetration(m_collider, transform.position, transform.rotation,
                other, other.transform.position, other.transform.rotation,
                out _, out float depth);

            owner.OnShieldTriggerStay(otherShip, depth);
        }
    }
}
