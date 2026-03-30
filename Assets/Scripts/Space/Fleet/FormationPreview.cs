// 에디터 전용 — FormationPreset 시각화. 씬뷰에 슬롯 구체 + positionIndex 라벨 표시
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class FormationPreview : MonoBehaviour
{
    [Header("프리셋")]
    public FormationPreset m_preset;

    [Header("미리보기 설정")]
    [Tooltip("격자 1칸 거리 (CubeGrid)")]
    public float m_gridUnitSize = 3f;
    [Tooltip("원 반지름 (Circle)")]
    public float m_circleRadius = 5f;
    [Tooltip("슬롯 구체 반지름")]
    public float m_slotRadius = 0.6f;
    [Tooltip("Circle 미리보기 시 함선 수 (2~9)")]
    [Range(2, 9)]
    public int m_previewShipCount = 5;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (m_preset == null) return;

        FormationSlot[] slots = GetPreviewSlots();
        if (slots == null || slots.Length == 0) return;

        foreach (var slot in slots)
        {
            Vector3 worldPos = transform.position + ComputeSlotWorldPos(slot);

            // 기함(0) = 노란색, 홀수(우측) = 청록, 짝수(좌측) = 주황
            if (slot.positionIndex == 0)
                Gizmos.color = Color.yellow;
            else if (slot.positionIndex % 2 == 1)
                Gizmos.color = Color.cyan;
            else
                Gizmos.color = new Color(1f, 0.6f, 0.1f);

            Gizmos.DrawSphere(worldPos, m_slotRadius);
            Gizmos.DrawWireSphere(worldPos, m_slotRadius + 0.05f);

            // positionIndex 라벨
            GUIStyle style = new GUIStyle
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = Color.white;
            Handles.Label(worldPos + Vector3.up * (m_slotRadius + 0.35f),
                          $"{slot.positionIndex}", style);
        }

        // Circle 가이드 링
        if (m_preset.parseType == EFormationParseType.Circle)
        {
            Handles.color = new Color(0.4f, 1f, 0.4f, 0.25f);
            Handles.DrawWireDisc(transform.position, Vector3.up, m_circleRadius);
        }

        // CubeGrid 격자 가이드선
        if (m_preset.parseType == EFormationParseType.CubeGrid)
            DrawGridGuide();
    }

    private FormationSlot[] GetPreviewSlots()
    {
        if (m_preset.parseType == EFormationParseType.CubeGrid)
            return m_preset.slots;

        var layout = m_preset.GetCircleLayout(m_previewShipCount);
        return layout?.slots;
    }

    private Vector3 ComputeSlotWorldPos(FormationSlot slot)
    {
        if (m_preset.parseType == EFormationParseType.CubeGrid)
        {
            return new Vector3(slot.gridCoord.x * m_gridUnitSize, 0f,
                               slot.gridCoord.y * m_gridUnitSize);
        }
        else
        {
            float rad = slot.circleAngle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad) * m_circleRadius, 0f,
                               Mathf.Cos(rad) * m_circleRadius);
        }
    }

    private void DrawGridGuide()
    {
        int maxCoord = 0;
        if (m_preset.slots != null)
        {
            foreach (var s in m_preset.slots)
            {
                maxCoord = Mathf.Max(maxCoord, Mathf.Abs(s.gridCoord.x), Mathf.Abs(s.gridCoord.y));
            }
        }

        float extent = (maxCoord + 0.5f) * m_gridUnitSize;
        Gizmos.color = new Color(1f, 1f, 1f, 0.1f);
        for (int i = -maxCoord; i <= maxCoord; i++)
        {
            float p = i * m_gridUnitSize;
            Gizmos.DrawLine(transform.position + new Vector3(p, 0, -extent),
                            transform.position + new Vector3(p, 0,  extent));
            Gizmos.DrawLine(transform.position + new Vector3(-extent, 0, p),
                            transform.position + new Vector3( extent, 0, p));
        }
    }
#endif
}
