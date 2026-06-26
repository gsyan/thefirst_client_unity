using System.Collections.Generic;
using UnityEngine;

public class DogfightSphere
{
    public Vector3 center;
    public float   radius;

    private List<AircraftBase> m_participants = new List<AircraftBase>();

    // Static 풀
    private static readonly Stack<DogfightSphere> s_pool = new Stack<DogfightSphere>();

    public static DogfightSphere Get(Vector3 center, float radius)
    {
        DogfightSphere sphere = s_pool.Count > 0 ? s_pool.Pop() : new DogfightSphere();
        sphere.center = center;
        sphere.radius = radius;
        return sphere;
    }

    public static void ReturnToPool(DogfightSphere sphere)
    {
        sphere.m_participants.Clear();
        s_pool.Push(sphere);
    }

    private DogfightSphere() { }

    public void Join(AircraftBase aircraft)
    {
        if (m_participants.Contains(aircraft) == false)
            m_participants.Add(aircraft);
    }

    public void LeaveDogFightSphere(AircraftBase aircraft)
    {
        m_participants.Remove(aircraft);

        if (m_participants.Count == 0)
        {
            ReturnToPool(this);
            return;
        }

        
    }

    public bool IsParticipating(AircraftBase aircraft)
    {
        return m_participants.Contains(aircraft);
    }

    // 구체 경계 제한 적용한 이동 방향 반환
    public Vector3 ClampMoveDir(Vector3 currentPos, Vector3 desiredDir, float overWeight = 2f)
    {
        float distFromCenter = Vector3.Distance(currentPos, center);
        if (distFromCenter <= radius)
            return desiredDir;

        Vector3 inwardDir   = (center - currentPos).normalized;
        float   overRatio   = (distFromCenter - radius) / radius;
        float   inwardWeight = Mathf.Clamp01(overRatio * overWeight);
        return Vector3.Lerp(desiredDir, inwardDir, inwardWeight).normalized;
    }
}
