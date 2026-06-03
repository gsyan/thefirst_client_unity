using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 섹션 헤더 구분선 + RowImageTextContainer(들)을 묶는 섹션 단위 컴포넌트.
// RowImageTextContainer가 여러 개인 경우(2열 등) 리스트를 순서대로 컨테이너에 분배.
public class UISection : MonoBehaviour
{
    [SerializeField] private TMP_Text m_titleText;

    private RowImageTextContainer[] m_containers;
    private RowImageText[] m_allRows;

    private void Awake()
    {
        m_containers = GetComponentsInChildren<RowImageTextContainer>(true);
        m_allRows    = GetComponentsInChildren<RowImageText>(true);
    }

    public void SetTitle(string title)
    {
        if (m_titleText != null)
            m_titleText.text = title;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public int GetRowCount()
    {
        return m_allRows != null ? m_allRows.Length : 0;
    }

    // 모든 Row와 Container를 숨김
    public void HideAllRows()
    {
        if (m_containers == null) return;
        for (int i = 0; i < m_containers.Length; i++)
        {
            m_containers[i].HideAll();
            m_containers[i].gameObject.SetActive(false);
        }
    }

    // 전체 통합 인덱스로 특정 행 설정 (해당 Container 자동 활성화)
    public void SetRow(int index, string icon, string text)
    {
        if (m_allRows == null || index < 0 || index >= m_allRows.Length) return;
        ActivateContainerForRow(index);
        m_allRows[index].SetRow(icon, text);
    }

    public void SetRow(int index, string icon, Color iconColor, string text)
    {
        if (m_allRows == null || index < 0 || index >= m_allRows.Length) return;
        ActivateContainerForRow(index);
        m_allRows[index].SetRow(icon, text);
        m_allRows[index].SetImageColor(iconColor);
    }

    public void SetRowText(int index, string text)
    {
        if (m_allRows == null || index < 0 || index >= m_allRows.Length) return;
        ActivateContainerForRow(index);
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

    public void SetRows(List<(string icon, string value)> rows, Color iconColor)
    {
        SetRows(rows);
        if (m_allRows == null) return;
        int count = rows != null ? rows.Count : 0;
        for (int i = 0; i < count && i < m_allRows.Length; i++)
            m_allRows[i].SetImageColor(iconColor);
    }

    // Row → Container → Section 순서로 레이아웃 재빌드 (ContentSizeFitter가 있는 경우 bottom-up 필수)
    public void RebuildLayout()
    {
        if (m_allRows != null)
        {
            for (int i = 0; i < m_allRows.Length; i++)
            {
                if (m_allRows[i].gameObject.activeInHierarchy == true)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(m_allRows[i].GetComponent<RectTransform>());
            }
        }
        if (m_containers != null)
        {
            for (int i = 0; i < m_containers.Length; i++)
            {
                if (m_containers[i].gameObject.activeSelf == true)
                    m_containers[i].RebuildLayout();
            }
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }

    // rowIndex가 속한 Container를 활성화
    private void ActivateContainerForRow(int rowIndex)
    {
        if (m_containers == null) return;
        int offset = 0;
        for (int c = 0; c < m_containers.Length; c++)
        {
            int count = m_containers[c].GetRowCount();
            if (rowIndex >= offset && rowIndex < offset + count)
            {
                m_containers[c].gameObject.SetActive(true);
                return;
            }
            offset += count;
        }
    }
}
