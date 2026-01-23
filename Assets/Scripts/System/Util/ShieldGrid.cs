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

/// <summary>
/// Geodesic Dome 기반 보호층 그리드. 삼각형/헥사곤 모드 지원.
/// </summary>
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

    [Header("References")]
    public Transform m_PointParent;

    public List<ShieldVertex> m_vertices = new List<ShieldVertex>();
    public List<ShieldCell> m_cells = new List<ShieldCell>();

    private BoundingBox m_boundBox;
    private Vector3 m_extents;

    private static readonly float PHI = (1f + Mathf.Sqrt(5f)) / 2f;

    // 내부 데이터 (듀얼 변환용)
    private List<Vector3> m_icoVertices = new List<Vector3>();
    private List<TriangleIndices> m_icoTriangles = new List<TriangleIndices>();

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

        Debug.Log($"ShieldGrid 생성 [{gridMode}]: {m_vertices.Count}개 꼭지점, {m_cells.Count}개 셀");
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

    void ClearAll()
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
    [SerializeField] private bool bShowGrid = true;
    [SerializeField] private Color vertexColor = Color.cyan;
    [SerializeField] private Color edgeColor = Color.yellow;
    [SerializeField] private Color cellColor = new Color(1f, 0.5f, 0f, 0.5f);
    [SerializeField] private bool bShowCellOutline = true;

    private void OnDrawGizmos()
    {
        if (!bShowGrid) return;

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

        // 타원체 바운드
        if (m_extents != Vector3.zero)
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = Matrix4x4.TRS(m_boundBox.center, m_boundBox.rotation, m_extents * 2f);
            Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
#endif
}

public class ShieldVertex : MonoBehaviour
{
    public int index;
    public List<int> neighborIndices = new List<int>();
    public Vector3 GetPosition() => transform.position;
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
