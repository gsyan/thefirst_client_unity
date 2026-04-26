// 꺾인 연결선 — start→bend→end 두 세그먼트를 quad로 그림
// SetBentPoints(screenStart, screenBend, screenEnd) 호출 시 내부에서 로컬 좌표 변환 후 메시 갱신
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class UIConnectLine : Graphic
{
    [SerializeField] private float m_thickness = 2f;

    private Vector2 m_localStart;
    private Vector2 m_localBend;
    private Vector2 m_localEnd;

    // Screen Space Overlay 기준 — camera 인자 null
    public void SetBentPoints(Vector2 screenStart, Vector2 screenBend, Vector2 screenEnd)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenStart, null, out m_localStart);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenBend,  null, out m_localBend);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenEnd,   null, out m_localEnd);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        DrawSegment(vh, m_localStart, m_localBend);
        DrawSegment(vh, m_localBend,  m_localEnd);
    }

    private void DrawSegment(VertexHelper vh, Vector2 from, Vector2 to)
    {
        Vector2 dir = to - from;
        if (dir.sqrMagnitude < 0.01f) return;

        Vector2 perp    = new Vector2(-dir.y, dir.x).normalized * (m_thickness * 0.5f);
        Color32 c       = color;
        int     startIdx = vh.currentVertCount;

        vh.AddVert(new UIVertex { color = c, position = from + perp });
        vh.AddVert(new UIVertex { color = c, position = from - perp });
        vh.AddVert(new UIVertex { color = c, position = to   - perp });
        vh.AddVert(new UIVertex { color = c, position = to   + perp });

        vh.AddTriangle(startIdx,     startIdx + 1, startIdx + 2);
        vh.AddTriangle(startIdx + 2, startIdx + 3, startIdx);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        m_thickness = Mathf.Max(1f, m_thickness);
        base.OnValidate();
    }
#endif
}
