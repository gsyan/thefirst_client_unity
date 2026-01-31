using UnityEngine;
using UnityEngine.UI;

// 코드로 사각형 테두리를 그리는 UI 컴포넌트
[RequireComponent(typeof(CanvasRenderer))]
public class UIBorderFrame : Graphic
{
    [SerializeField] private float m_thickness = 4f;

    [Header("깜박임 효과")]
    [SerializeField] private bool m_useBlink = true;
    [SerializeField] private float m_blinkSpeed = 2f;
    [SerializeField] private float m_minAlpha = 0.3f;
    [SerializeField] private float m_maxAlpha = 1f;

    private float m_blinkTime;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        float w = rect.width;
        float h = rect.height;
        float t = m_thickness;

        // 좌하단 기준 좌표
        Vector2 bl = new Vector2(rect.xMin, rect.yMin);

        // 상단 바
        AddQuad(vh, bl + new Vector2(0, h - t), w, t);
        // 하단 바
        AddQuad(vh, bl, w, t);
        // 좌측 바
        AddQuad(vh, bl + new Vector2(0, t), t, h - t * 2);
        // 우측 바
        AddQuad(vh, bl + new Vector2(w - t, t), t, h - t * 2);
    }

    private void AddQuad(VertexHelper vh, Vector2 pos, float width, float height)
    {
        int idx = vh.currentVertCount;

        vh.AddVert(new Vector3(pos.x, pos.y), color, Vector2.zero);
        vh.AddVert(new Vector3(pos.x, pos.y + height), color, Vector2.zero);
        vh.AddVert(new Vector3(pos.x + width, pos.y + height), color, Vector2.zero);
        vh.AddVert(new Vector3(pos.x + width, pos.y), color, Vector2.zero);

        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }

    public void SetThickness(float thickness)
    {
        m_thickness = thickness;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (!m_useBlink) return;

        m_blinkTime += Time.deltaTime * m_blinkSpeed;
        float alpha = Mathf.Lerp(m_minAlpha, m_maxAlpha, (Mathf.Sin(m_blinkTime * Mathf.PI) + 1f) * 0.5f);

        Color c = color;
        c.a = alpha;
        color = c;
    }
}
