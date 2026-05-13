using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 섹션 헤더 구분선 + RowImageTextContainer(들)을 묶는 섹션 단위 컴포넌트.
// RowImageTextContainer가 여러 개인 경우(2열 등) 리스트를 순서대로 컨테이너에 분배.
public class UISection : MonoBehaviour
{
    private RowImageTextContainer[] m_containers;
    private RowImageText[] m_allRows;

    private void Awake()
    {
        m_containers = GetComponentsInChildren<RowImageTextContainer>(true);
        m_allRows    = GetComponentsInChildren<RowImageText>(true);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public int GetRowCount()
    {
        return m_allRows != null ? m_allRows.Length : 0;
    }

    public void HideAllRows()
    {
        if (m_allRows == null) return;
        for (int i = 0; i < m_allRows.Length; i++)
            m_allRows[i].Hide();
    }

    // 전체 통합 인덱스로 특정 행 설정
    public void SetRow(int index, string icon, string text)
    {
        if (m_allRows == null || index < 0 || index >= m_allRows.Length) return;
        m_allRows[index].SetRow(icon, text);
    }

    public void SetRowText(int index, string text)
    {
        if (m_allRows == null || index < 0 || index >= m_allRows.Length) return;
        m_allRows[index].SetTextWithString(text);
    }

    public void HideRow(int index)
    {
        if (m_allRows == null || index < 0 || index >= m_allRows.Length) return;
        m_allRows[index].Hide();
    }

    // 리스트를 컨테이너 단위로 순서대로 분배. 내용이 없는 컨테이너는 비활성화.
    public void SetRows(List<(string icon, string value)> rows)
    {
        if (m_containers == null) return;
        int globalIdx = 0;
        for (int c = 0; c < m_containers.Length; c++)
        {
            m_containers[c].HideAll();
            bool hasContent = rows != null && globalIdx < rows.Count;
            m_containers[c].gameObject.SetActive(hasContent);
            if (hasContent == false) continue;
            int count = m_containers[c].GetRowCount();
            for (int r = 0; r < count && globalIdx < rows.Count; r++, globalIdx++)
                m_containers[c].SetRow(r, rows[globalIdx].icon, rows[globalIdx].value);
        }
    }

    // 컨테이너 안쪽 → 섹션 루트 순서로 레이아웃 재빌드
    public void RebuildLayout()
    {
        if (m_containers != null)
        {
            for (int i = 0; i < m_containers.Length; i++)
                m_containers[i].RebuildLayout();
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }
}
