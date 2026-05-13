using UnityEngine;
using UnityEngine.UI;

// RowImageText 직접 부모에 붙이는 컴포넌트. UISection이 컨테이너 단위로 show/hide에 활용.
public class RowImageTextContainer : MonoBehaviour
{
    private RowImageText[] m_rows;

    private void Awake()
    {
        m_rows = GetComponentsInChildren<RowImageText>(true);
    }

    public int GetRowCount()
    {
        return m_rows != null ? m_rows.Length : 0;
    }

    public void HideAll()
    {
        if (m_rows == null) return;
        for (int i = 0; i < m_rows.Length; i++)
            m_rows[i].Hide();
    }

    public void SetRow(int index, string icon, string text)
    {
        if (m_rows == null || index < 0 || index >= m_rows.Length) return;
        m_rows[index].SetRow(icon, text);
    }

    public void SetRowText(int index, string text)
    {
        if (m_rows == null || index < 0 || index >= m_rows.Length) return;
        m_rows[index].SetTextWithString(text);
    }

    public void HideRow(int index)
    {
        if (m_rows == null || index < 0 || index >= m_rows.Length) return;
        m_rows[index].Hide();
    }

    public void RebuildLayout()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }
}
