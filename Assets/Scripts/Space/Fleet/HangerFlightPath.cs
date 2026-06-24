// 함체 프리팹 HangerSlot 자식에 배치 — 함재기 사출/귀환 경로 웨이포인트 정의
// LaunchPath/ReturnPath 각 자식 Transform 순서 = 웨이포인트 순서
// m_launchWps / m_returnWps 기반 Catmull-Rom 보간으로 중간 WP 자동 생성 지원 (에디터 ContextMenu)
using UnityEngine;

public class HangerFlightPath : MonoBehaviour
{
    [SerializeField] private Transform m_launchPath;
    [SerializeField] private Transform[] m_launchWps;
    [SerializeField] private int m_launchTargetCount;

    [SerializeField] private Transform m_returnPath;
    [SerializeField] private Transform[] m_returnWps;
    [SerializeField] private int m_returnTargetCount;

    // 사출 경로 컨테이너 (자식 = WP, 매 프레임 현재 월드 좌표로 읽어야 함)
    public Transform LaunchPath => m_launchPath;

    // 귀환 경로 컨테이너 (자식 = WP, 매 프레임 현재 월드 좌표로 읽어야 함)
    public Transform ReturnPath => m_returnPath;

#if UNITY_EDITOR
    [HideInInspector] public bool bShowGizmos = true;
    private static readonly Color k_launchGizmoColor = new(0.3f, 0.85f, 1f, 0.95f);
    private static readonly Color k_returnGizmoColor = new(1f, 0.6f, 0.2f, 0.95f);

    private void OnDrawGizmos()
    {
        if (bShowGizmos == false) return;
        DrawPathGizmo(m_launchPath, k_launchGizmoColor);
        DrawPathGizmo(m_returnPath, k_returnGizmoColor);
    }

    private static void DrawPathGizmo(Transform pathRoot, Color color)
    {
        if (pathRoot == null || pathRoot.childCount < 2) return;

        int childCount = pathRoot.childCount;
        var pts = new Vector3[childCount];
        for (int i = 0; i < childCount; i++)
            pts[i] = pathRoot.GetChild(i).position;

        // WP 위치 점
        Gizmos.color = color;
        foreach (var p in pts)
            Gizmos.DrawSphere(p, 0.05f);

        // Catmull-Rom 곡선
        const int k_steps = 16;
        UnityEditor.Handles.color = color;
        for (int i = 0; i < childCount - 1; i++)
        {
            Vector3 p0 = i > 0 ? pts[i - 1] : pts[i] * 2f - pts[i + 1];
            Vector3 p1 = pts[i];
            Vector3 p2 = pts[i + 1];
            Vector3 p3 = i + 2 < childCount ? pts[i + 2] : pts[i + 1] * 2f - pts[i];

            var seg = new Vector3[k_steps + 1];
            for (int s = 0; s <= k_steps; s++)
                seg[s] = CatmullRom(p0, p1, p2, p3, (float)s / k_steps);

            UnityEditor.Handles.DrawAAPolyLine(2.5f, seg);
        }
    }

    [ContextMenu("Launch 곡선 WP 생성")]
    private void GenerateLaunchCurve()
    {
        if (m_launchWps == null || m_launchWps.Length < 3)
        {
            Debug.LogError("[HangerFlightPath] m_launchWps 최소 3개 필요");
            return;
        }
        if (m_launchTargetCount <= m_launchWps.Length)
        {
            Debug.LogError($"[HangerFlightPath] m_launchTargetCount({m_launchTargetCount}) > m_launchWps.Length({m_launchWps.Length}) 이어야 합니다");
            return;
        }

        GenerateCurveWps(m_launchWps, m_launchTargetCount, m_launchPath, "LaunchWP");
        UnityEditor.EditorUtility.SetDirty(gameObject);
    }

    [ContextMenu("Launch 생성된 WP 제거")]
    private void ClearGeneratedLaunchWps()
    {
        ClearGeneratedWps(m_launchPath);
        UnityEditor.EditorUtility.SetDirty(gameObject);
    }

    [ContextMenu("Return 곡선 WP 생성")]
    private void GenerateReturnCurve()
    {
        if (m_returnWps == null || m_returnWps.Length < 3)
        {
            Debug.LogError("[HangerFlightPath] m_returnWps 최소 3개 필요");
            return;
        }
        if (m_returnTargetCount <= m_returnWps.Length)
        {
            Debug.LogError($"[HangerFlightPath] m_returnTargetCount({m_returnTargetCount}) > m_returnWps.Length({m_returnWps.Length}) 이어야 합니다");
            return;
        }

        GenerateCurveWps(m_returnWps, m_returnTargetCount, m_returnPath, "ReturnWP");
        UnityEditor.EditorUtility.SetDirty(gameObject);
    }

    [ContextMenu("Return 생성된 WP 제거")]
    private void ClearGeneratedReturnWps()
    {
        ClearGeneratedWps(m_returnPath);
        UnityEditor.EditorUtility.SetDirty(gameObject);
    }

    // m_launchWps / m_returnWps 사이에 Catmull-Rom 보간 WP를 삽입하고 계층 순서를 유지
    private static void GenerateCurveWps(Transform[] wps, int targetCount, Transform container, string prefix)
    {
        int n = wps.Length;
        int segCount = n - 1;
        int newCount = targetCount - n;

        // 세그먼트별 삽입 개수 — 앞 세그먼트에 나머지 우선 배분
        int[] insertCounts = new int[segCount];
        int baseCount = newCount / segCount;
        int rem = newCount % segCount;
        for (int i = 0; i < segCount; i++)
            insertCounts[i] = baseCount + (i < rem ? 1 : 0);

        // 뒤 세그먼트부터 처리 → 앞 WP의 sibling index 불변
        for (int seg = segCount - 1; seg >= 0; seg--)
        {
            int count = insertCounts[seg];
            if (count == 0) continue;

            // Catmull-Rom 팬텀 포인트 (양 끝 세그먼트용 외삽)
            Vector3 pos0 = seg > 0
                ? wps[seg - 1].position
                : wps[seg].position * 2f - wps[seg + 1].position;
            Vector3 pos1 = wps[seg].position;
            Vector3 pos2 = wps[seg + 1].position;
            Vector3 pos3 = seg + 2 < n
                ? wps[seg + 2].position
                : wps[seg + 1].position * 2f - wps[seg].position;

            int baseIdx = wps[seg].GetSiblingIndex();

            for (int j = 0; j < count; j++)
            {
                float t = (float)(j + 1) / (count + 1);
                Vector3 pos = CatmullRom(pos0, pos1, pos2, pos3, t);

                var go = new GameObject($"{prefix}_gen");
                UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Generate Curve WP");
                go.transform.SetParent(container, false);
                go.transform.position = pos;
                // j번째 삽입 시 이미 j개가 앞에 들어갔으므로 baseIdx + j + 1
                go.transform.SetSiblingIndex(baseIdx + j + 1);
            }
        }
    }

    private static void ClearGeneratedWps(Transform container)
    {
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
            if (child.name.Contains("_gen"))
                UnityEditor.Undo.DestroyObjectImmediate(child.gameObject);
        }
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }
#endif
}
