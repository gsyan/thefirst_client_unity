using System.Collections.Generic;
using UnityEngine;

// 진형 변경 시 함선들의 경로를 사전 계획하는 시스템
// Hungarian Algorithm으로 최적 매칭 + 경로 교차 해결
public class FormationPathPlanner
{
    // 함선별 계획된 경로 정보
    public class PlannedPath
    {
        public SpaceShip ship;
        public Vector3 startPos;
        public Vector3 endPos;
        public List<Vector3> waypoints;  // 중간 경유점 (교차 회피용)
        public float startDelay;         // 출발 지연 시간
        public float totalDistance;

        public PlannedPath(SpaceShip ship, Vector3 start, Vector3 end)
        {
            this.ship = ship;
            this.startPos = start;
            this.endPos = end;
            this.waypoints = new List<Vector3> { start, end };
            this.startDelay = 0f;
            this.totalDistance = Vector3.Distance(start, end);
        }
    }

    // 진형 변경 경로 계획 (메인 진입점)
    public static List<PlannedPath> PlanFormationChange(List<SpaceShip> ships, EFormationType targetFormation)
    {
        if (ships == null || ships.Count == 0)
            return new List<PlannedPath>();

        var paths = new List<PlannedPath>();

        // 기함(positionIndex == 0) 분리
        SpaceShip flagship = null;
        var otherShips = new List<SpaceShip>();
        foreach (var ship in ships)
        {
            if (ship.m_shipInfo.positionIndex == 0)
                flagship = ship;
            else
                otherShips.Add(ship);
        }

        // 기함은 항상 중심 위치로 고정 (Hungarian에서 제외)
        if (flagship != null)
        {
            Vector3 flagshipTarget = flagship.CalculateShipPosition(targetFormation);
            var flagshipPath = new PlannedPath(flagship, flagship.transform.localPosition, flagshipTarget);
            paths.Add(flagshipPath);
        }

        // 나머지 함선이 없으면 기함만 반환
        if (otherShips.Count == 0)
        {
            ResolvePathCrossings(paths);
            return paths;
        }

        // 1. 나머지 함선들의 현재 위치와 목표 위치 수집
        var currentPositions = new List<Vector3>();
        var targetPositions = new List<Vector3>();

        foreach (var ship in otherShips)
        {
            currentPositions.Add(ship.transform.localPosition);
            targetPositions.Add(ship.CalculateShipPosition(targetFormation));
        }

        // 2. Hungarian Algorithm으로 최적 매칭 (기함 제외)
        int[] assignment = SolveHungarianAssignment(currentPositions, targetPositions);

        // 3. 계획된 경로 생성
        for (int i = 0; i < otherShips.Count; i++)
        {
            int targetIdx = assignment[i];
            var path = new PlannedPath(otherShips[i], currentPositions[i], targetPositions[targetIdx]);
            paths.Add(path);
        }

        // 4. 경로 교차 해결
        ResolvePathCrossings(paths);

        return paths;
    }

    #region Hungarian Algorithm
    // Hungarian Algorithm (Kuhn-Munkres) - O(n³)
    // 총 이동거리가 최소가 되는 매칭 찾기
    public static int[] SolveHungarianAssignment(List<Vector3> sources, List<Vector3> targets)
    {
        int n = sources.Count;
        if (n == 0) return new int[0];
        if (n != targets.Count)
        {
            Debug.LogError("Hungarian: source와 target 개수가 다름");
            return CreateSequentialAssignment(n);
        }

        // 비용 행렬 생성 (거리)
        float[,] cost = new float[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                cost[i, j] = Vector3.Distance(sources[i], targets[j]);
            }
        }

        // Hungarian Algorithm 실행
        return HungarianSolve(cost, n);
    }

    private static int[] HungarianSolve(float[,] cost, int n)
    {
        // u[i]: i번째 행의 포텐셜, v[j]: j번째 열의 포텐셜
        float[] u = new float[n + 1];
        float[] v = new float[n + 1];
        // p[j]: j번째 열에 매칭된 행 번호
        int[] p = new int[n + 1];
        // way[j]: 최단 경로에서 j번째 열의 이전 열
        int[] way = new int[n + 1];

        for (int i = 1; i <= n; i++)
        {
            p[0] = i;
            int j0 = 0; // 증강 경로 담색에서 지금 서 있는 열
            float[] minv = new float[n + 1]; // 열j로 갈 수 있는 최소 reduced cost
            bool[] used = new bool[n + 1]; // 이미 탐색된 트리에 포함된 열인가?

            for (int j = 0; j <= n; j++)
            {
                minv[j] = float.MaxValue;
                used[j] = false;
            }

            do
            {
                used[j0] = true;
                int i0 = p[j0];
                float delta = float.MaxValue;
                int j1 = 0;

                for (int j = 1; j <= n; j++)
                {
                    if (!used[j])
                    {
                        float cur = cost[i0 - 1, j - 1] - u[i0] - v[j];
                        if (cur < minv[j])
                        {
                            minv[j] = cur;
                            way[j] = j0;
                        }
                        if (minv[j] < delta)
                        {
                            delta = minv[j];
                            j1 = j;
                        }
                    }
                }

                for (int j = 0; j <= n; j++)
                {
                    if (used[j])
                    {
                        u[p[j]] += delta;
                        v[j] -= delta;
                    }
                    else
                    {
                        minv[j] -= delta;
                    }
                }

                j0 = j1;
            } while (p[j0] != 0);

            do
            {
                int j1 = way[j0];
                p[j0] = p[j1];
                j0 = j1;
            } while (j0 != 0);
        }

        // 결과 변환: assignment[i] = i번째 source가 가야 할 target 인덱스
        int[] assignment = new int[n];
        for (int j = 1; j <= n; j++)
        {
            if (p[j] > 0)
                assignment[p[j] - 1] = j - 1;
        }

        return assignment;
    }

    private static int[] CreateSequentialAssignment(int n)
    {
        int[] result = new int[n];
        for (int i = 0; i < n; i++)
            result[i] = i;
        return result;
    }
    #endregion

    #region Path Crossing Resolution
    private const float DETOUR_OFFSET = 10f;  // 우회 거리

    // 경로 교차 해결
    private static void ResolvePathCrossings(List<PlannedPath> paths)
    {
        if (paths.Count < 2) return;

        // 모든 경로 쌍에 대해 교차 검사 및 우회 경유점 추가
        for (int i = 0; i < paths.Count; i++)
        {
            for (int j = i + 1; j < paths.Count; j++)
            {
                Vector3 intersection;
                if (TryGetIntersectionPoint(paths[i], paths[j], out intersection))
                {
                    AddDetourWaypoints(paths[i], paths[j], intersection);
                }
            }
        }

        // 모든 경로의 총 거리 재계산
        foreach (var path in paths)
            RecalculateTotalDistance(path);
    }

    // 두 경로의 근접점 계산 (3D 선분 간 최소 거리 기반)
    private const float COLLISION_THRESHOLD = 5f;  // 충돌 판정 거리

    private static bool TryGetIntersectionPoint(PlannedPath a, PlannedPath b, out Vector3 intersection)
    {
        intersection = Vector3.zero;

        Vector3 p1 = a.startPos, p2 = a.endPos;
        Vector3 p3 = b.startPos, p4 = b.endPos;

        // 두 선분 사이 최소 거리와 최근접점 계산
        Vector3 closestOnA, closestOnB;
        float minDist = ClosestPointsBetweenSegments(p1, p2, p3, p4, out closestOnA, out closestOnB);

        if (minDist > COLLISION_THRESHOLD)
            return false;

        // 교차점은 두 최근접점의 중간
        intersection = (closestOnA + closestOnB) * 0.5f;
        return true;
    }

    // 두 3D 선분 사이 최소 거리 및 최근접점 계산
    private static float ClosestPointsBetweenSegments(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4,
        out Vector3 closestOnA, out Vector3 closestOnB)
    {
        Vector3 d1 = p2 - p1;  // 선분 A 방향
        Vector3 d2 = p4 - p3;  // 선분 B 방향
        Vector3 r = p1 - p3;

        float a = Vector3.Dot(d1, d1);
        float b = Vector3.Dot(d1, d2);
        float c = Vector3.Dot(d2, d2);
        float d = Vector3.Dot(d1, r);
        float e = Vector3.Dot(d2, r);

        float denom = a * c - b * b;
        float s, t;

        if (denom < 0.0001f)
        {
            // 평행한 경우
            s = 0f;
            t = (b > c ? d / b : e / c);
        }
        else
        {
            s = (b * e - c * d) / denom;
            t = (a * e - b * d) / denom;
        }

        // 선분 범위로 클램프
        s = Mathf.Clamp01(s);
        t = Mathf.Clamp01(t);

        // 클램프 후 재계산
        if (s < 0f || s > 1f)
        {
            s = Mathf.Clamp01(s);
            t = Mathf.Clamp01((b * s + e) / c);
        }
        if (t < 0f || t > 1f)
        {
            t = Mathf.Clamp01(t);
            s = Mathf.Clamp01((b * t - d) / a);
        }

        closestOnA = p1 + d1 * s;
        closestOnB = p3 + d2 * t;

        return Vector3.Distance(closestOnA, closestOnB);
    }

    // 우회 경유점 추가 (서로 반대 방향으로 우회, 3D 기준)
    private static void AddDetourWaypoints(PlannedPath pathA, PlannedPath pathB, Vector3 intersection)
    {
        Vector3 dirA = (pathA.endPos - pathA.startPos).normalized;
        Vector3 dirB = (pathB.endPos - pathB.startPos).normalized;

        // 두 경로의 외적으로 공통 수직 방향 계산
        Vector3 crossDir = Vector3.Cross(dirA, dirB);
        if (crossDir.sqrMagnitude < 0.0001f)
        {
            // 평행한 경우 x,y 평면 기준 수직 방향 사용
            crossDir = new Vector3(-dirA.y, dirA.x, 0);
        }
        crossDir.Normalize();

        // A는 +방향, B는 -방향으로 우회
        Vector3 detourA = intersection + crossDir * DETOUR_OFFSET;
        Vector3 detourB = intersection - crossDir * DETOUR_OFFSET;

        pathA.waypoints.Clear();
        pathA.waypoints.Add(pathA.startPos);
        pathA.waypoints.Add(detourA);
        pathA.waypoints.Add(pathA.endPos);

        pathB.waypoints.Clear();
        pathB.waypoints.Add(pathB.startPos);
        pathB.waypoints.Add(detourB);
        pathB.waypoints.Add(pathB.endPos);
    }

    // 총 거리 재계산
    private static void RecalculateTotalDistance(PlannedPath path)
    {
        float total = 0f;
        for (int i = 0; i < path.waypoints.Count - 1; i++)
            total += Vector3.Distance(path.waypoints[i], path.waypoints[i + 1]);
        path.totalDistance = total;
    }

    // 두 경로가 교차하는지 검사 (2D 투영, XZ 평면)
    private static bool DoPathsCross(PlannedPath a, PlannedPath b)
    {
        Vector2 a1 = new Vector2(a.startPos.x, a.startPos.z);
        Vector2 a2 = new Vector2(a.endPos.x, a.endPos.z);
        Vector2 b1 = new Vector2(b.startPos.x, b.startPos.z);
        Vector2 b2 = new Vector2(b.endPos.x, b.endPos.z);

        return LineSegmentsIntersect(a1, a2, b1, b2);
    }

    // 선분 교차 검사
    private static bool LineSegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        float d1 = CrossProduct2D(p3, p4, p1);
        float d2 = CrossProduct2D(p3, p4, p2);
        float d3 = CrossProduct2D(p1, p2, p3);
        float d4 = CrossProduct2D(p1, p2, p4);

        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
            ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
            return true;

        const float epsilon = 0.0001f;
        if (Mathf.Abs(d1) < epsilon && OnSegment(p3, p4, p1)) return true;
        if (Mathf.Abs(d2) < epsilon && OnSegment(p3, p4, p2)) return true;
        if (Mathf.Abs(d3) < epsilon && OnSegment(p1, p2, p3)) return true;
        if (Mathf.Abs(d4) < epsilon && OnSegment(p1, p2, p4)) return true;

        return false;
    }

    private static float CrossProduct2D(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    private static bool OnSegment(Vector2 a, Vector2 b, Vector2 c)
    {
        return Mathf.Min(a.x, b.x) <= c.x && c.x <= Mathf.Max(a.x, b.x) &&
               Mathf.Min(a.y, b.y) <= c.y && c.y <= Mathf.Max(a.y, b.y);
    }

    // 시간차 출발로 교차 해결
    private static void ResolveByTiming(PlannedPath a, PlannedPath b)
    {
        // 더 짧은 경로가 먼저 출발, 긴 경로는 지연
        if (a.totalDistance <= b.totalDistance)
        {
            // a가 더 짧음 → b가 대기
            float delay = EstimateCrossingTime(a);
            b.startDelay = Mathf.Max(b.startDelay, delay);
        }
        else
        {
            // b가 더 짧음 → a가 대기
            float delay = EstimateCrossingTime(b);
            a.startDelay = Mathf.Max(a.startDelay, delay);
        }
    }

    // 경로 중간점 통과 예상 시간 (속도 1 기준)
    private static float EstimateCrossingTime(PlannedPath path)
    {
        // 경로 절반 지점 통과 시간 + 여유
        return (path.totalDistance * 0.6f);
    }
    #endregion

    #region Debug
    public static void DebugDrawPaths(List<PlannedPath> paths, float duration = 5f)
    {
        if (paths == null) return;

        Color[] colors = { Color.red, Color.green, Color.blue, Color.yellow, Color.cyan, Color.magenta };

        for (int i = 0; i < paths.Count; i++)
        {
            var path = paths[i];
            Color color = colors[i % colors.Length];

            // 경로 선 그리기
            for (int w = 0; w < path.waypoints.Count - 1; w++)
            {
                Debug.DrawLine(path.waypoints[w], path.waypoints[w + 1], color, duration);
            }

            // 시작/끝 표시
            Debug.DrawRay(path.startPos, Vector3.up * 2f, Color.white, duration);
            Debug.DrawRay(path.endPos, Vector3.up * 2f, color, duration);
        }
    }
    #endregion
}
