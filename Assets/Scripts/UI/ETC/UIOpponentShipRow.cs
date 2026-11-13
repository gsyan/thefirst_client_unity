// 상대 상세 팝업(UIPopupOpponentDetail)의 함선 1척 행 — 함체 이름 + 고정 위치 스탯 칸(4열 × 2줄). InfiniteScrollView가 행 프리팹으로 생성/재활용
// 칸 순서는 PvpOpponentStatRows.BuildShipCells와 1:1 — 행 높이가 고정이라 항상 모든 칸을 표시(값 없음은 "-")
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIOpponentShipRow : MonoBehaviour
{
    [SerializeField] private TMP_Text m_nameText;     // 함체 표시 이름(예: 켄투리온함)
    [SerializeField] private TMP_Text[] m_cellLabels; // 칸 라벨(체력/빔 공격/...) — m_cellValues와 같은 개수·순서
    [SerializeField] private TMP_Text[] m_cellValues; // 칸 값

    public void Setup(string shipName, List<(string label, string valueText)> cells)
    {
        gameObject.SetActive(true);
        if (m_nameText != null) m_nameText.text = shipName;

        for (int i = 0; i < m_cellLabels.Length; i++)
        {
            bool hasCell = i < cells.Count;
            string label = hasCell == true ? cells[i].label : "";
            string valueText = hasCell == true ? cells[i].valueText : "";
            if (m_cellLabels[i] != null) m_cellLabels[i].text = label;
            if (i < m_cellValues.Length && m_cellValues[i] != null) m_cellValues[i].text = valueText;
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
