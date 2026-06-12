using TMPro;
using UnityEngine;
using UnityEngine.UI;

// VIP 버튼 독립 컴포넌트 — Top 버튼 클릭으로 DetailContainer 확장/접기
[RequireComponent(typeof(RectTransform))]
public class UICalendarButton : MonoBehaviour
{
    [SerializeField] private Button m_calendarButton;   // 출석 달력 보기 버튼
    
    private void Awake()
    {
        if (m_calendarButton != null)
            m_calendarButton.onClick.AddListener(OnCalendarButtonClicked);
    }

    
    private void OnCalendarButtonClicked()
    {
        UIManager.Instance.ShowDailyBonusCalendar();
    }
}
