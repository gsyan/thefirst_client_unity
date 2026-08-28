using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 섹션 헤더 구분선 + RowLabelValue(라벨+값) 행들을 묶는 섹션 단위 컴포넌트.
// 행은 런타임 동적 생성(Instantiate) 없이 프리팹에 필요한 최대 개수만큼 미리 배치해두고 재사용 —
// 모자라면 GetOrCreateRow가 범위를 벗어나 명확한 예외를 던지므로 배치 누락이 바로 드러남
public class UISection : MonoBehaviour
{
    [SerializeField] private TMP_Text m_titleText;
    [SerializeField] private Transform m_rowsRoot;

    private List<RowLabelValue> m_rowCache = new List<RowLabelValue>();

    private void Awake()
    {
        Debug.Log($"[버그수정] UISection.Awake 호출됨 name={gameObject.name} activeInHierarchy={gameObject.activeInHierarchy} m_rowsRoot={(m_rowsRoot != null ? m_rowsRoot.name : "null")} rowsRootActiveInHierarchy={(m_rowsRoot != null ? m_rowsRoot.gameObject.activeInHierarchy.ToString() : "N/A")}");
        // m_rowsRoot 밑에 미리 배치해둔 행을 그대로 풀로 사용
        if (m_rowsRoot != null)
            m_rowCache.AddRange(m_rowsRoot.GetComponentsInChildren<RowLabelValue>(true));
        Debug.Log($"[버그수정] UISection.Awake 완료 name={gameObject.name} m_rowCache.Count={m_rowCache.Count}");
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

    // 풀에서 index번째 행을 꺼냄 — 프리팹에 미리 배치해둔 행만 사용, 개수가 모자라면 여기서 바로 예외가 발생해 배치 누락을 드러냄
    private RowLabelValue GetOrCreateRow(int index)
    {
        Debug.Log($"[버그수정] GetOrCreateRow name={gameObject.name} index={index} m_rowCache.Count={m_rowCache.Count}");
        return m_rowCache[index];
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
