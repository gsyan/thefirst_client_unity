using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


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
    
    public float spacing = 1.0f;
    public float jitter = 0.2f;

    public List<ModuleOutlinePointInfo> m_outlinePointInfos = new List<ModuleOutlinePointInfo>();

    


    public void GenerateOutline_Sphere()
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
        int count = m_outlinePointInfos.Count;

        for (int i = 0; i < count; i++)
        {
            var neighbors = new List<ModuleOutlinePointInfo>();

            // 거리 정렬
            List<(float dist, ModuleOutlinePointInfo point)> candidates = new List<(float, ModuleOutlinePointInfo)>();
            for (int j = 0; j < count; j++)
            {
                if (i == j) continue;
                float distance = Vector3.SqrMagnitude( m_outlinePointInfos[i].transform.position - m_outlinePointInfos[j].transform.position );
                candidates.Add((distance, m_outlinePointInfos[j]));
            }

            // 가까운 순서대로 정렬
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

            // 가까운 후보 순서대로 검사
            foreach (var iterCandiate in candidates)
            {
                if (neighbors.Count >= K)
                    break;

                Vector3 pointI = m_outlinePointInfos[i].transform.position;
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

            m_outlinePointInfos[i].neighbors = neighbors;
        }
    }

    


    private BoundingBox debugBox;
    public void GenerateOutline_ByBox()
    {
        DestroyAllPoints();

        Transform gameObjectTransform = transform;
        BoundingBox box = ComputeBoundingBox(gameObjectTransform);
        debugBox = box;
        List<OutlinePointData> samplePoints = GenerateJitteredGridBoxPoints(box, spacing, jitter);

        for (int i = 0; i < samplePoints.Count; i++)
        {
            OutlinePointData pointData = samplePoints[i];
            CreatePoint(pointData.position);
        }

        BuildAdjacency();
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

        return new BoundingBox(worldCenter, worldSize, margin, ship.rotation);
    }

    public struct OutlinePointData
    {
        public Vector3 position;
        public Vector3 raycastDirection;
    }

    List<OutlinePointData> GenerateJitteredGridBoxPoints(BoundingBox box, float spacing, float jitter)
    {
        List<OutlinePointData> points = new List<OutlinePointData>();

        Vector3 ext = box.extents;
        Quaternion rot = box.rotation;
        Vector3 center = box.center;

        Vector3[] normals = {
            new Vector3(0,0,1),  // 0: Front
            new Vector3(0,0,-1), // 1: Back
            new Vector3(1,0,0),  // 2: Right
            new Vector3(-1,0,0), // 3: Left
            new Vector3(0,1,0),  // 4: Top
            new Vector3(0,-1,0)  // 5: Bottom
        };

        Vector3[] longAxes = {
            new Vector3(1,0,0), // Front
            new Vector3(1,0,0), // Back
            new Vector3(0,0,1), // Right
            new Vector3(0,0,1), // Left
            new Vector3(0,0,1), // Top
            new Vector3(0,0,1)  // Bottom
        };

        Vector3[] verticalAxes = {
            new Vector3(0,1,0), // Front
            new Vector3(0,1,0), // Back
            new Vector3(0,1,0), // Right
            new Vector3(0,1,0), // Left
            new Vector3(1,0,0), // Top
            new Vector3(1,0,0)  // Bottom
        };

        float[] normalOffsets = { ext.z, ext.z, ext.x, ext.x, ext.y, ext.y };
        float[] longExtents = { ext.x, ext.x, ext.z, ext.z, ext.z, ext.z };
        float[] verticalExtents = { ext.y, ext.y, ext.y, ext.y, ext.x, ext.x };

        for (int i = 0; i < 6; i++)
        {
            Vector3 normal = normals[i];
            Vector3 longAxis = longAxes[i];
            Vector3 vAxis = verticalAxes[i];

            float nOffset = normalOffsets[i];
            float longMax = longExtents[i];
            float vMax = verticalExtents[i];

            Vector3 localPlaneCenter = normal * nOffset;

            List<float> lineOffsets = new List<float>();

            if (i < 4)
            {
                lineOffsets.Add(vMax);
                lineOffsets.Add(0f);
                lineOffsets.Add(-vMax);
            }
            else
            {
                lineOffsets.Add(0f);
            }

            foreach (float offset in lineOffsets)
            {
                Vector3 localShortOffset = vAxis * offset;
                Vector3 lineCenter = center + rot * (localPlaneCenter + localShortOffset);

                bool isTopEdge = offset > 0;
                bool isBottomEdge = offset < 0;

                OutlinePointData centerPoint = new OutlinePointData();
                centerPoint.position = lineCenter;
                centerPoint.raycastDirection = CalculateRaycastDirection(rot, normal, isTopEdge, isBottomEdge, ext, i);
                points.Add(centerPoint);

                for (float spacingSum = spacing; spacingSum <= longMax; spacingSum += spacing)
                {
                    Vector3 localOffset = longAxis * spacingSum;

                    Vector3 p1 = center + rot * (localPlaneCenter + localShortOffset + localOffset);
                    Vector3 p2 = center + rot * (localPlaneCenter + localShortOffset - localOffset);

                    OutlinePointData point1 = new OutlinePointData();
                    point1.position = p1;
                    point1.raycastDirection = CalculateRaycastDirection(rot, normal, isTopEdge, isBottomEdge, ext, i);
                    points.Add(point1);

                    OutlinePointData point2 = new OutlinePointData();
                    point2.position = p2;
                    point2.raycastDirection = CalculateRaycastDirection(rot, normal, isTopEdge, isBottomEdge, ext, i);
                    points.Add(point2);
                }
            }
        }

        return points;
    }

    Vector3 CalculateRaycastDirection(Quaternion rot, Vector3 faceNormal, bool isTopEdge, bool isBottomEdge, Vector3 extents, int faceIndex)
    {
        Vector3 direction = -faceNormal;

        float heightRatio = extents.y / extents.x;
        if( faceIndex == 0 || faceIndex == 1) // Front, Back
            heightRatio = 1;
        else if( faceIndex == 2 || faceIndex == 3) // Right, Left
            heightRatio = extents.y / extents.x;

        if (isTopEdge || isBottomEdge)
        {
            if (isTopEdge)
                direction = (-faceNormal + Vector3.down * heightRatio).normalized;
            else
                direction = (-faceNormal + Vector3.up * heightRatio).normalized;
        }

        return rot * direction;
    }
    
    public Transform m_PointParent;
    private void CreatePoint(Vector3 inPosition)
    {
        GameObject newObj = new GameObject($"OutlinePoint_{m_outlinePointInfos.Count}");
        ModuleOutlinePointInfo newOutlinePointInfo = newObj.AddComponent<ModuleOutlinePointInfo>();
        newOutlinePointInfo.transform.position = inPosition;
        newOutlinePointInfo.index = m_outlinePointInfos.Count;
        newOutlinePointInfo.neighbors = new List<ModuleOutlinePointInfo>();
        m_outlinePointInfos.Add(newOutlinePointInfo);

        newObj.transform.SetParent(m_PointParent);
    }
    public void Check()
    {
        if (m_PointParent == null)
            return;

        m_outlinePointInfos.Clear();

        for (int i = 0; i < m_PointParent.childCount; i++)
        {
            Transform child = m_PointParent.GetChild(i);
            ModuleOutlinePointInfo pointInfo = child.GetComponent<ModuleOutlinePointInfo>();
            if (pointInfo != null)
            {
                pointInfo.index = m_outlinePointInfos.Count;
                m_outlinePointInfos.Add(pointInfo);
            }
        }
    }

    public void DestroyAllPoints()
    {
        m_outlinePointInfos.Clear();

        if (m_PointParent == null)
            return;

        while (m_PointParent.childCount > 0)
            DestroyImmediate(m_PointParent.GetChild(0).gameObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (m_outlinePointInfos == null) return;

        // Vector3 viewNormal = Camera.current.transform.forward;
        // Handles.color = Color.white;
        // Handles.DrawWireDisc(transform.position, viewNormal, sphereRadius);

        // bound box
        // Gizmos.color = Color.green;
        // Gizmos.DrawWireCube(debugBox.center, debugBox.size);
        
        // point 
        Gizmos.color = Color.yellow;
        foreach (var iter in m_outlinePointInfos)
            Gizmos.DrawSphere(iter.transform.position, 0.05f);

        // grid
        Gizmos.color = Color.cyan;
        for (int i = 0; i < m_outlinePointInfos.Count; i++)
        {
            foreach (var neighbor in m_outlinePointInfos[i].neighbors)
            {
                Gizmos.DrawLine(m_outlinePointInfos[i].transform.position, neighbor.transform.position);
            }
        }
    }
#endif
    

}