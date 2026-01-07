using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


public enum EAirCraftPathGridBoundStyle
{
    Sphere = 0,
    Box = 1
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

public class AirCraftPathGrid : MonoBehaviour
{
    public EAirCraftPathGridBoundStyle boundStyle = EAirCraftPathGridBoundStyle.Box;

    public float pushBackDistance = 1.0f;

    public int sampleCount = 48;
    public float sphereRadius = 1.5f;
    
    public float boundXScale = 1.0f; // 바운드 크기 배율 (1.0 = 100%, 1.5 = 150%)
    public float pointScale = 1.0f;
    public float spacing = 1.0f;
    

    public List<AirCraftPathPoint> m_aircraftPathPoints = new List<AirCraftPathPoint>();

    public Transform m_PointParent;


    public void GenerateAircraftPathGrid_Sphere()
    {
        DestroyAllPoints();
        
        // 1) 구 표면에 균등 분포 포인트 생성 (Fibonacci sphere)
        List<Vector3> spherePoints = GenerateFibonacciPoints(transform.position, sphereRadius, sampleCount);

        for (int i = 0; i < sampleCount; i++)
        {
            Vector3 sample = spherePoints[i];

            // 2) 중심부로 레이캐스트
            Vector3 dir = (transform.position - sample).normalized;
            RaycastHit hit;
            Vector3 finalPoint = sample;

            if (Physics.Raycast(sample, dir, out hit, sphereRadius * 2f))
            {
                finalPoint = hit.point - dir * pushBackDistance;
            }
            
            CreatePoint(finalPoint);
        }

        // 3) adjacency (최근접 이웃 연결)
        BuildAdjacency();
    }
    // ✔️ Fibonacci sphere sampling
    List<Vector3> GenerateFibonacciPoints(Vector3 center, float radius, int N)
    {
        List<Vector3> pts = new List<Vector3>();
        
        float offset = 2f / N;
        float increment = Mathf.PI * (3f - Mathf.Sqrt(5f)); // golden angle

        for (int i = 0; i < N; i++)
        {
            float y = ((i * offset) - 1) + (offset / 2f);
            float r = Mathf.Sqrt(1 - y * y);

            float phi = i * increment;

            float x = Mathf.Cos(phi) * r;
            float z = Mathf.Sin(phi) * r;

            Vector3 p = new Vector3(x, y, z) * radius + center;
            pts.Add(p);
        }
        return pts;
    }

    // ✔️ adjacency = 각 점의 최근접 K개 연결
    public void BuildAdjacency(int K = 4, float margin = 0.01f)
    {
        int count = m_aircraftPathPoints.Count;

        for (int i = 0; i < count; i++)
        {
            var neighbors = new List<AirCraftPathPoint>();

            // 거리 정렬
            List<(float dist, AirCraftPathPoint point)> candidates = new List<(float, AirCraftPathPoint)>();
            for (int j = 0; j < count; j++)
            {
                if (i == j) continue;
                float distance = Vector3.SqrMagnitude( m_aircraftPathPoints[i].transform.position - m_aircraftPathPoints[j].transform.position );
                candidates.Add((distance, m_aircraftPathPoints[j]));
            }

            // 가까운 순서대로 정렬
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

            // 가까운 후보 순서대로 검사
            foreach (var iterCandiate in candidates)
            {
                if (neighbors.Count >= K)
                    break;

                Vector3 pointI = m_aircraftPathPoints[i].transform.position;
                Vector3 pointCandiate = iterCandiate.point.transform.position;

                Vector3 dir = pointCandiate - pointI;
                float dist = dir.magnitude;
                dir /= dist;

                // Raycast 검사
                if (Physics.Raycast(pointI, dir, out RaycastHit hit, dist - margin))
                {
                    continue;
                }

                // 충돌 없음 or 다른 오브젝트와 충돌했지만 상관없음 → 연결 허용
                neighbors.Add(iterCandiate.point);
            }

            m_aircraftPathPoints[i].neighbors = neighbors;
        }
    }

    


    private BoundingBox debugBox;
    public void GenerateAircraftPathGrid_Box()
    {
        DestroyAllPoints();

        if (m_PointParent == null)
            return;

        Transform gameObjectTransform = transform;
        BoundingBox box = ComputeBoundingBox(gameObjectTransform);
        debugBox = box;
        List<AirCraftPathPointData> samplePoints = GenerateBoxGridPoints(box, spacing);

        // 점 생성
        for (int i = 0; i < samplePoints.Count; i++)
        {
            AirCraftPathPointData pointData = samplePoints[i];
            CreatePoint(pointData.position);
        }

        // 이웃 관계 설정 (이미 GenerateBoxGridPoints에서 계산됨)
        for (int i = 0; i < samplePoints.Count; i++)
        {
            AirCraftPathPointData pointData = samplePoints[i];
            foreach (int neighborIdx in pointData.neighborIndices)
            {
                if (neighborIdx < m_aircraftPathPoints.Count)
                {
                    if (!m_aircraftPathPoints[i].neighbors.Contains(m_aircraftPathPoints[neighborIdx]))
                    {
                        m_aircraftPathPoints[i].neighbors.Add(m_aircraftPathPoints[neighborIdx]);
                    }
                }
            }
        }
    }

    private BoundingBox ComputeBoundingBox(Transform ship, float margin = 0.1f)
    {
        Renderer[] renderers = ship.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new BoundingBox(ship.position, Vector3.one, margin, ship.rotation);

        List<Vector3> worldPoints = new List<Vector3>();

        foreach (var r in renderers)
        {
            Bounds lb = r.localBounds;

            // localBounds의 extents
            Vector3 ext = lb.extents;
            Vector3 center = lb.center;

            // 8개 코너
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

            // local → world 변환
            foreach (var p in localCorners)
                worldPoints.Add(r.transform.TransformPoint(p));
        }

        // ship 로컬 좌표계로 역변환 → OBB 계산
        Matrix4x4 worldToShip = ship.worldToLocalMatrix;

        // ship 기준 local OBB 계산
        Bounds localOBB = new Bounds(worldToShip.MultiplyPoint3x4(worldPoints[0]), Vector3.zero);
        for (int i = 1; i < worldPoints.Count; i++)
            localOBB.Encapsulate(worldToShip.MultiplyPoint3x4(worldPoints[i]));

        // 다시 world size로 변환
        Vector3 worldSize = Vector3.Scale(localOBB.size, ship.lossyScale);

        // center는 ship 기준 local center를 다시 world 로 변환
        Vector3 worldCenter = ship.TransformPoint(localOBB.center);

        worldSize += Vector3.one * margin * 2f;

        // boundXScale 적용하여 바운드 크기 조정
        worldSize *= boundXScale;

        return new BoundingBox(worldCenter, worldSize, margin, ship.rotation);
    }

    public struct AirCraftPathPointData
    {
        public Vector3 position;
        public List<int> neighborIndices; // 이웃 점들의 인덱스
    }

    List<AirCraftPathPointData> GenerateBoxGridPoints(BoundingBox box, float spacing)
    {
        List<AirCraftPathPointData> points = new List<AirCraftPathPointData>();
        Dictionary<Vector3, int> positionToIndex = new Dictionary<Vector3, int>();

        Vector3 ext = box.extents;
        Quaternion rot = box.rotation;
        Vector3 center = box.center;

        // 1단계: 8개 꼭지점 생성
        Vector3[] corners = new Vector3[8];
        int cornerIdx = 0;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 localPos = new Vector3(x * ext.x, y * ext.y, z * ext.z);
                    Vector3 worldPos = center + rot * localPos;
                    corners[cornerIdx] = worldPos;

                    AirCraftPathPointData corner = new AirCraftPathPointData();
                    corner.position = worldPos;
                    corner.neighborIndices = new List<int>();

                    positionToIndex[worldPos] = points.Count;
                    points.Add(corner);
                    cornerIdx++;
                }
            }
        }

        // 2단계: 6개 면 중심점 생성
        Vector3[] faceNormals = {
            new Vector3(0, 1, 0),  // Top
            new Vector3(0, -1, 0), // Bottom
            new Vector3(1, 0, 0),  // Right
            new Vector3(-1, 0, 0), // Left
            new Vector3(0, 0, 1),  // Front
            new Vector3(0, 0, -1)  // Back
        };

        int[] faceCenterIndices = new int[6];
        for (int i = 0; i < 6; i++)
        {
            Vector3 localPos = faceNormals[i] * (i < 2 ? ext.y : (i < 4 ? ext.x : ext.z));
            Vector3 worldPos = center + rot * localPos;

            AirCraftPathPointData faceCenter = new AirCraftPathPointData();
            faceCenter.position = worldPos;
            faceCenter.neighborIndices = new List<int>();

            faceCenterIndices[i] = points.Count;
            positionToIndex[worldPos] = points.Count;
            points.Add(faceCenter);
        }

        // 3단계: 초기 이웃 관계 설정
        // 꼭지점들의 이웃 설정 (각 꼭지점은 같은 모서리를 공유하는 3개 꼭지점 + 속한 3개 면의 중심점)
        int[,] cornerNeighbors = {
            {1, 2, 4}, // 0: (-,-,-)
            {0, 3, 5}, // 1: (-,-,+)
            {0, 3, 6}, // 2: (-,+,-)
            {1, 2, 7}, // 3: (-,+,+)
            {0, 5, 6}, // 4: (+,-,-)
            {1, 4, 7}, // 5: (+,-,+)
            {2, 4, 7}, // 6: (+,+,-)
            {3, 5, 6}  // 7: (+,+,+)
        };

        int[,] cornerFaces = {
            {1, 3, 5}, // 0: Bottom, Left, Back
            {1, 3, 4}, // 1: Bottom, Left, Front
            {0, 3, 5}, // 2: Top, Left, Back
            {0, 3, 4}, // 3: Top, Left, Front
            {1, 2, 5}, // 4: Bottom, Right, Back
            {1, 2, 4}, // 5: Bottom, Right, Front
            {0, 2, 5}, // 6: Top, Right, Back
            {0, 2, 4}  // 7: Top, Right, Front
        };

        for (int i = 0; i < 8; i++)
        {
            AirCraftPathPointData pt = points[i];
            // 이웃 꼭지점 3개
            for (int j = 0; j < 3; j++)
            {
                pt.neighborIndices.Add(cornerNeighbors[i, j]);
            }
            // 속한 면 중심점 3개
            for (int j = 0; j < 3; j++)
            {
                pt.neighborIndices.Add(faceCenterIndices[cornerFaces[i, j]]);
            }
            points[i] = pt;
        }

        // 면 중심점들의 이웃 설정 (해당 면의 4개 꼭지점)
        int[,] faceCorners = {
            {2, 3, 6, 7}, // Top
            {0, 1, 4, 5}, // Bottom
            {4, 5, 6, 7}, // Right
            {0, 1, 2, 3}, // Left
            {1, 3, 5, 7}, // Front
            {0, 2, 4, 6}  // Back
        };

        for (int i = 0; i < 6; i++)
        {
            AirCraftPathPointData pt = points[faceCenterIndices[i]];
            for (int j = 0; j < 4; j++)
            {
                pt.neighborIndices.Add(faceCorners[i, j]);
            }
            points[faceCenterIndices[i]] = pt;
        }

        // 4단계: Spacing 기반 세분화
        SubdivideBySpacing(points, spacing);

        return points;
    }

    void SubdivideBySpacing(List<AirCraftPathPointData> points, float spacing)
    {
        bool needsMoreSubdivision = true;
        int maxIterations = 10; // 무한 루프 방지
        int iteration = 0;

        while (needsMoreSubdivision && iteration < maxIterations)
        {
            needsMoreSubdivision = false;
            iteration++;

            List<(int pointIdx, int neighborIdx, int newPointIdx)> subdivisions = new List<(int, int, int)>();

            // 모든 점의 이웃 거리 검사
            for (int i = 0; i < points.Count; i++)
            {
                AirCraftPathPointData pt = points[i];

                foreach (int neighborIdx in pt.neighborIndices)
                {
                    // 중복 방지: 인덱스가 작은 쪽에서만 처리
                    if (neighborIdx <= i) continue;
                    if (neighborIdx >= points.Count) continue;

                    float distance = Vector3.Distance(pt.position, points[neighborIdx].position);

                    if (distance > spacing)
                    {
                        // 중간점 생성
                        Vector3 midPos = (pt.position + points[neighborIdx].position) * 0.5f;

                        AirCraftPathPointData newPoint = new AirCraftPathPointData();
                        newPoint.position = midPos;
                        newPoint.neighborIndices = new List<int> { i, neighborIdx };

                        int newPointIdx = points.Count;
                        subdivisions.Add((i, neighborIdx, newPointIdx));
                        points.Add(newPoint);

                        needsMoreSubdivision = true;
                    }
                }
            }

            // 세분화 결과 적용
            foreach (var (pointIdx, neighborIdx, newPointIdx) in subdivisions)
            {
                AirCraftPathPointData pt1 = points[pointIdx];
                AirCraftPathPointData pt2 = points[neighborIdx];

                // 기존 이웃 관계 제거
                pt1.neighborIndices.Remove(neighborIdx);
                pt2.neighborIndices.Remove(pointIdx);

                // 새 점을 이웃으로 추가
                pt1.neighborIndices.Add(newPointIdx);
                pt2.neighborIndices.Add(newPointIdx);

                points[pointIdx] = pt1;
                points[neighborIdx] = pt2;
            }
        }
    }

    private void CreatePoint(Vector3 inPosition)
    {
        GameObject newObj = new GameObject($"AirCraftPathPoint_{m_aircraftPathPoints.Count}");
        AirCraftPathPoint newAirCraftPathPoint = newObj.AddComponent<AirCraftPathPoint>();
        newAirCraftPathPoint.transform.position = inPosition;
        newAirCraftPathPoint.index = m_aircraftPathPoints.Count;
        newAirCraftPathPoint.neighbors = new List<AirCraftPathPoint>();
        m_aircraftPathPoints.Add(newAirCraftPathPoint);

        // MeshFilter와 MeshRenderer 추가하여 sphere로 표시
        MeshFilter meshFilter = newObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = newObj.AddComponent<MeshRenderer>();

        // Unity 기본 Sphere mesh 사용
        GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        meshFilter.mesh = tempSphere.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(tempSphere);

#if UNITY_EDITOR
        // 에디터에서는 Material Asset 로드
        Material mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/KenneyShipMaterial/dark.mat");
        if (mat != null)
        {
            meshRenderer.sharedMaterial = mat;
        }
#endif

        // 작은 크기로 설정
        newObj.transform.localScale = Vector3.one * pointScale;

#if UNITY_EDITOR
        // bShowPathGrid에 따라 초기 표시 상태 설정
        meshRenderer.enabled = bShowPathGrid;
#else
        // 런타임에는 항상 비활성화
        meshRenderer.enabled = false;
#endif

        newObj.transform.SetParent(m_PointParent);
    }
    

    public void DestroyAllPoints()
    {
        m_aircraftPathPoints.Clear();

        if (m_PointParent == null)
            return;

        while (m_PointParent.childCount > 0)
            DestroyImmediate(m_PointParent.GetChild(0).gameObject);
    }

#if UNITY_EDITOR
    [SerializeField] private bool bShowPathGrid = false;

    private void OnValidate()
    {
        // bShowPathGrid 값이 변경될 때 MeshRenderer 상태 업데이트
        // EditorApplication.delayCall을 사용하여 다음 프레임에 실행
        EditorApplication.delayCall += UpdatePointsVisibility;
    }

    private void UpdatePointsVisibility()
    {
        if (m_aircraftPathPoints == null) return;

        foreach (var point in m_aircraftPathPoints)
        {
            if (point != null && point.TryGetComponent<MeshRenderer>(out var renderer))
            {
                renderer.enabled = bShowPathGrid;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (bShowPathGrid == false) return;
        if (m_aircraftPathPoints == null) return;

        // Vector3 viewNormal = Camera.current.transform.forward;
        // Handles.color = Color.white;
        // Handles.DrawWireDisc(transform.position, viewNormal, sphereRadius);

        // bound box
        // Gizmos.color = Color.green;
        // Gizmos.DrawWireCube(debugBox.center, debugBox.size);

        // point (Gizmo는 유지, MeshRenderer는 OnValidate에서 처리)
        Gizmos.color = Color.yellow;
        foreach (var iter in m_aircraftPathPoints)
            Gizmos.DrawSphere(iter.transform.position, 0.05f);

        // grid
        Gizmos.color = Color.cyan;
        for (int i = 0; i < m_aircraftPathPoints.Count; i++)
        {
            foreach (var neighbor in m_aircraftPathPoints[i].neighbors)
            {
                Gizmos.DrawLine(m_aircraftPathPoints[i].transform.position, neighbor.transform.position);
            }
        }
    }
#endif
    

}