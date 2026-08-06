using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 섹션 헤더 구분선 + RowLabelValue(라벨+값) 행들을 묶는 섹션 단위 컴포넌트.
// 행은 개수 제한 없이 필요한 만큼 풀링(Instantiate)해서 씀 — 아이콘 그리드 시절의 컨테이너 개념은 없음(라벨+값은 항상 한 줄에 한 항목).
public class UISection : MonoBehaviour
{
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private RowLabelValue m_rowPrefab;
    [SerializeField] private Transform m_rowsRoot;

    private List<RowLabelValue> m_rowCache = new List<RowLabelValue>();

    private void Awake()
    {
        // 에디터에서 m_rowsRoot 밑에 미리 배치해둔 행이 있으면 그것부터 풀로 사용 — 부족한 만큼만 런타임에 Instantiate
        if (m_rowsRoot != null)
            m_rowCache.AddRange(m_rowsRoot.GetComponentsInChildren<RowLabelValue>(true));
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

    // 풀에서 index번째 행을 꺼냄 — 미리 배치된 행/이전에 만든 행이 있으면 그걸 재사용, 없으면 새로 Instantiate
    private RowLabelValue GetOrCreateRow(int index)
    {
        RowLabelValue row;
        if (index < m_rowCache.Count)
        {
            row = m_rowCache[index];
        }
        else
        {
            row = Instantiate(m_rowPrefab, m_rowsRoot);
            m_rowCache.Add(row);
        }
        return row;
    }

    // 풀 전체를 숨김
    public void HideAllRows()
    {
        for (int i = 0; i < m_rowCache.Count; i++)
            m_rowCache[i].Hide();
    }

    // 사용 개수 이후의 풀만 숨김 — SetRows류가 이미 사용한 행은 그대로 두고 남는 행만 정리할 때 사용
    private void HideRowsFrom(int usedCount)
    {
        for (int i = usedCount; i < m_rowCache.Count; i++)
            m_rowCache[i].Hide();
    }

    public void SetRow(int index, string label, string value, bool rawLabel = false, bool rawValue = true)
    {
        RowLabelValue row = GetOrCreateRow(index);
        row.SetRow(label, value, rawValue, rawLabel);
    }

    public void SetRow(int index, string label, Color valueColor, string value, bool rawLabel = false, bool rawValue = true)
    {
        RowLabelValue row = GetOrCreateRow(index);
        row.SetRow(label, value, rawValue, rawLabel);
        row.SetValueColor(valueColor);
    }

    public void SetRows(List<(string label, string value)> rows)
    {
        int count = rows != null ? rows.Count : 0;
        for (int i = 0; i < count; i++)
            SetRow(i, rows[i].label, rows[i].value);
        HideRowsFrom(count);
    }

    public void SetRows(List<(string label, string value)> rows, Color valueColor)
    {
        int count = rows != null ? rows.Count : 0;
        for (int i = 0; i < count; i++)
            SetRow(i, rows[i].label, valueColor, rows[i].value);
        HideRowsFrom(count);
    }

    public void SetRows(List<(string label, string value, Color? color)> rows)
    {
        int count = rows != null ? rows.Count : 0;
        for (int i = 0; i < count; i++)
        {
            // color가 null이면 RowLabelValue 프리팹 기본색을 그대로 둠
            if (rows[i].color.HasValue)
                SetRow(i, rows[i].label, rows[i].color.Value, rows[i].value);
            else
                SetRow(i, rows[i].label, rows[i].value);
        }
        HideRowsFrom(count);
    }

    // 라벨+값 행은 원래부터 한 줄에 한 항목이라 vertical 여부와 무관하게 동일하게 동작 — 호출부 시그니처 호환용으로만 남김
    public void SetRowsVertical(List<(string label, string value)> rows) => SetRows(rows);
    public void SetRowsVertical(List<(string label, string value, Color? color)> rows) => SetRows(rows);

    // Row → Section 순서로 레이아웃 재빌드 (ContentSizeFitter가 있는 경우 bottom-up 필수)
    public void RebuildLayout()
    {
        for (int i = 0; i < m_rowCache.Count; i++)
        {
            if (m_rowCache[i].gameObject.activeInHierarchy == true)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(m_rowCache[i].transform as RectTransform);
        }
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }
}
