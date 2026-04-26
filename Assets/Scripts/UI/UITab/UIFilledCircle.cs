// 채워진 원 — Graphic 상속, 스프라이트 없이 순수 메시로 그림
// RectTransform.position을 스크린 좌표로 이동시켜 사용
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class UIFilledCircle : Graphic
{
    [SerializeField] private float m_radius   = 14f;
    [SerializeField] private int   m_segments = 32;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        vh.AddVert(new UIVertex { color = color, position = Vector3.zero });

        for (int i = 0; i <= m_segments; i++)
        {
            float angle = 2f * Mathf.PI * i / m_segments;
            vh.AddVert(new UIVertex
            {
                color    = color,
                position = new Vector3(Mathf.Cos(angle) * m_radius, Mathf.Sin(angle) * m_radius, 0f)
            });
        }

        for (int i = 1; i <= m_segments; i++)
            vh.AddTriangle(0, i, i + 1);
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        m_segments = Mathf.Max(8, m_segments);
        base.OnValidate();
        SetVerticesDirty();
    }
#endif
}
