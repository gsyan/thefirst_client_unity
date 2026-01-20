using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 간단한 레이더 차트
[RequireComponent(typeof(CanvasRenderer))]
public class SimpleRadarChart : Graphic
{
    [Header("Chart Data")]
    public List<float> m_stats = new List<float>();

    [Tooltip("최대 값 (정규화 기준)")]
    public float maxValue = 100f;

    [Header("Appearance")]
    [Tooltip("차트 채우기 색상")]
    public Color fillColor = new Color(0.3f, 0.8f, 1f, 0.4f);

    [Tooltip("차트 외곽선 색상")]
    public Color outlineColor = new Color(0.3f, 0.8f, 1f, 1f);

    [Tooltip("그리드 색상")]
    public Color gridColor = new Color(1f, 1f, 1f, 0.15f);

    [Header("Size")]
    [Tooltip("차트 반지름")]
    public float radius = 80f;

    public float lineThickness = 3f;

    [Tooltip("그리드 레벨 수")]
    [Range(2, 10)]
    public int gridLevels = 5;

    public void SetStats(CapabilityProfile stats)
    {
        m_stats.Clear();
        m_stats.Add(stats.firepower);
        m_stats.Add(stats.survivability);
        m_stats.Add(stats.mobility);
        m_stats.Add(stats.logistics);
        
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (m_stats == null || m_stats.Count < 1) return;
        
        DrawGrid(vh);
        DrawDataFill(vh);
        DrawDataOutline(vh);
    }

    private void DrawGrid(VertexHelper vh)
    {
        if (m_stats.Count == 0) return;

        for (int level = 1; level <= gridLevels; level++)
        {
            float levelRadius = radius * (level / (float)gridLevels);
            DrawHexagonOutline(vh, levelRadius, gridColor);
        }

        // 중심에서 각 축으로 선 그리기
        for (int i = 0; i < m_stats.Count; i++)
        {
            Vector2 point = GetHexagonPoint(i, radius);
            DrawLineToCenter(vh, point, gridColor);
        }
    }

    // 데이터 영역 채우기
    private void DrawDataFill(VertexHelper vh)
    {
        int centerVertIndex = vh.currentVertCount;
        AddVertex(vh, Vector2.zero, fillColor);

        int count = m_stats.Count;
        // 데이터 포인트 추가
        for (int i = 0; i < count; i++)
        {
            float normalizedValue = Mathf.Clamp01(m_stats[i] / maxValue);
            Vector2 point = GetHexagonPoint(i, radius * normalizedValue);
            AddVertex(vh, point, fillColor);
        }

        // 삼각형으로 채우기
        for (int i = 0; i < count; i++)
        {
            int nextIndex = (i + 1) % count;
            vh.AddTriangle(centerVertIndex, centerVertIndex + 1 + i, centerVertIndex + 1 + nextIndex);
        }
    }

    // 데이터 외곽선 그리기
    private void DrawDataOutline(VertexHelper vh)
    {
        int count = m_stats.Count;

        for (int i = 0; i < count; i++)
        {
            int nextIndex = (i + 1) % count;

            float normalizedValue1 = Mathf.Clamp01(m_stats[i] / maxValue);
            float normalizedValue2 = Mathf.Clamp01(m_stats[nextIndex] / maxValue);

            Vector2 point1 = GetHexagonPoint(i, radius * normalizedValue1);
            Vector2 point2 = GetHexagonPoint(nextIndex, radius * normalizedValue2);

            DrawThickLine(vh, point1, point2, lineThickness, outlineColor);
        }
    }

    // 육각형 외곽선 그리기
    private void DrawHexagonOutline(VertexHelper vh, float hexRadius, Color color)
    {
        int count = m_stats.Count;

        for (int i = 0; i < count; i++)
        {
            int nextIndex = (i + 1) % count;
            Vector2 point1 = GetHexagonPoint(i, hexRadius);
            Vector2 point2 = GetHexagonPoint(nextIndex, hexRadius);
            DrawThickLine(vh, point1, point2, lineThickness, color);
        }
    }

    // 중심에서 포인트로 선 그리기
    private void DrawLineToCenter(VertexHelper vh, Vector2 point, Color color)
    {
        DrawThickLine(vh, Vector2.zero, point, 1f, color);
    }

    // 두께 있는 선 그리기
    private void DrawThickLine(VertexHelper vh, Vector2 start, Vector2 end, float thickness, Color color)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x) * thickness * 0.5f;

        int startIndex = vh.currentVertCount;

        AddVertex(vh, start + perpendicular, color);
        AddVertex(vh, start - perpendicular, color);
        AddVertex(vh, end + perpendicular, color);
        AddVertex(vh, end - perpendicular, color);

        vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vh.AddTriangle(startIndex + 1, startIndex + 3, startIndex + 2);
    }

    // 다각형 포인트 계산
    private Vector2 GetHexagonPoint(int index, float hexRadius)
    {
        if (m_stats.Count == 0) return Vector2.zero;

        // 12시 방향 시작 (90도), 시계 방향 회전 (각도를 빼기)
        float angleStep = 360f / m_stats.Count;
        float angle = (90f - index * angleStep) * Mathf.Deg2Rad;

        return new Vector2(
            Mathf.Cos(angle) * hexRadius,
            Mathf.Sin(angle) * hexRadius
        );
    }

    // 버텍스 추가
    private void AddVertex(VertexHelper vh, Vector2 position, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}
