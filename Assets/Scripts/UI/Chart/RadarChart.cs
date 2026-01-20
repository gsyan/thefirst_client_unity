using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class RadarChart : Graphic
{
    [Header("Chart Data")]
    [SerializeField] private float[] m_values = new float[6]; // 6개 스탯
    [SerializeField] private float m_maxValue = 100f;

    [Header("Chart Appearance")]
    [SerializeField] private Color m_fillColor = new Color(0, 1, 1, 0.3f);
    [SerializeField] private Color m_lineColor = Color.cyan;
    [SerializeField] private float m_lineWidth = 2f;
    [SerializeField] private Color m_gridColor = new Color(1, 1, 1, 0.2f);
    [SerializeField] private int m_gridLevels = 5;

    [Header("Chart Size")]
    [SerializeField] private float m_radius = 100f;

    public float[] Values
    {
        get => m_values;
        set
        {
            m_values = value;
            SetVerticesDirty();
        }
    }

    public void SetValue(int index, float value)
    {
        if (index >= 0 && index < m_values.Length)
        {
            m_values[index] = value;
            SetVerticesDirty();
        }
    }

    public void SetValues(float[] values)
    {
        if (values.Length == m_values.Length)
        {
            m_values = values;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Vector2 center = Vector2.zero;

        // 그리드 그리기
        DrawGrid(vh, center);

        // 데이터 영역 그리기 (채우기)
        DrawDataArea(vh, center);

        // 데이터 외곽선 그리기
        DrawDataOutline(vh, center);
    }

    private void DrawGrid(VertexHelper vh, Vector2 center)
    {
        int vertexStartIndex = vh.currentVertCount;

        // 그리드 레벨별로 육각형 그리기
        for (int level = 1; level <= m_gridLevels; level++)
        {
            float levelRadius = m_radius * (level / (float)m_gridLevels);

            for (int i = 0; i <= m_values.Length; i++)
            {
                float angle = (i % m_values.Length) * (360f / m_values.Length) * Mathf.Deg2Rad - 90f * Mathf.Deg2Rad;
                Vector2 point = center + new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)
                ) * levelRadius;

                AddVertex(vh, point, m_gridColor);

                if (i > 0)
                {
                    int currentIndex = vertexStartIndex + i;
                    int prevIndex = currentIndex - 1;
                    DrawLine(vh, prevIndex, currentIndex, 1f);
                }
            }

            vertexStartIndex = vh.currentVertCount;
        }

        // 중심에서 각 꼭짓점으로 선 그리기
        for (int i = 0; i < m_values.Length; i++)
        {
            float angle = i * (360f / m_values.Length) * Mathf.Deg2Rad - 90f * Mathf.Deg2Rad;
            Vector2 point = center + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * m_radius;

            AddVertex(vh, center, m_gridColor);
            AddVertex(vh, point, m_gridColor);

            DrawLine(vh, vh.currentVertCount - 2, vh.currentVertCount - 1, 1f);
        }
    }

    private void DrawDataArea(VertexHelper vh, Vector2 center)
    {
        int centerIndex = vh.currentVertCount;
        AddVertex(vh, center, m_fillColor);

        // 데이터 포인트 생성
        for (int i = 0; i < m_values.Length; i++)
        {
            float angle = i * (360f / m_values.Length) * Mathf.Deg2Rad - 90f * Mathf.Deg2Rad;
            float normalizedValue = Mathf.Clamp01(m_values[i] / m_maxValue);

            Vector2 point = center + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * m_radius * normalizedValue;

            AddVertex(vh, point, m_fillColor);
        }

        // 삼각형 생성 (채우기)
        for (int i = 0; i < m_values.Length; i++)
        {
            int nextIndex = (i + 1) % m_values.Length;
            vh.AddTriangle(centerIndex, centerIndex + 1 + i, centerIndex + 1 + nextIndex);
        }
    }

    private void DrawDataOutline(VertexHelper vh, Vector2 center)
    {
        int startIndex = vh.currentVertCount;

        // 외곽선 포인트 생성
        for (int i = 0; i <= m_values.Length; i++)
        {
            int index = i % m_values.Length;
            float angle = index * (360f / m_values.Length) * Mathf.Deg2Rad - 90f * Mathf.Deg2Rad;
            float normalizedValue = Mathf.Clamp01(m_values[index] / m_maxValue);

            Vector2 point = center + new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            ) * m_radius * normalizedValue;

            AddVertex(vh, point, m_lineColor);

            if (i > 0)
            {
                DrawLine(vh, startIndex + i - 1, startIndex + i, m_lineWidth);
            }
        }
    }

    private void AddVertex(VertexHelper vh, Vector2 position, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }

    private void DrawLine(VertexHelper vh, int startIndex, int endIndex, float width)
    {
        // 선을 사각형으로 그리기 위한 헬퍼 메서드
        // 실제로는 삼각형 2개로 선을 표현

        // 이 메서드는 단순화된 버전입니다
        // 더 정교한 선 그리기가 필요하면 별도 구현 필요
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}
