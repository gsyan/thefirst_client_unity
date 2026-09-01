// UIPopupModuleReinforce 안의 스탯 항목 1행 — 라벨 + 현재값 + Up/Down 버튼
// isEditable == false면 Up/Down이 항상 비활성화(강화 UI 배치는 하되 실제 작동은 공격력 계열만 지원하는 현재 스코프 반영)
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIReinforceStatRow : MonoBehaviour
{
    [SerializeField] private TMP_Text m_labelText;
    [SerializeField] private TMP_Text m_valueText;
    [SerializeField] private Button m_upButton;
    [SerializeField] private Button m_downButton;

    private int m_dataIndex;
    private System.Action<int, int> m_onPointsChanged; // (dataIndex, delta) delta=+1 또는 -1

    public void Setup(int dataIndex, string label, int currentValue, bool isEditable, bool canIncrease, bool canDecrease, System.Action<int, int> onPointsChanged)
    {
        m_dataIndex = dataIndex;
        m_onPointsChanged = onPointsChanged;

        if (m_labelText != null) m_labelText.text = label;
        if (m_valueText != null) m_valueText.text = currentValue.ToString();

        if (m_upButton != null)
        {
            m_upButton.onClick.RemoveAllListeners();
            m_upButton.onClick.AddListener(OnUpClicked);
            m_upButton.interactable = isEditable == true && canIncrease == true;
        }

        if (m_downButton != null)
        {
            m_downButton.onClick.RemoveAllListeners();
            m_downButton.onClick.AddListener(OnDownClicked);
            m_downButton.interactable = isEditable == true && canDecrease == true;
        }
    }

    private void OnUpClicked()
    {
        if (m_onPointsChanged != null) m_onPointsChanged(m_dataIndex, 1);
    }

    private void OnDownClicked()
    {
        if (m_onPointsChanged != null) m_onPointsChanged(m_dataIndex, -1);
    }
}
