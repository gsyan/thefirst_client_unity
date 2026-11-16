// 공지 팝업(UIPopupNotice)의 제목 행 — 누르면 자기 인덱스를 팝업에 알려 본문을 엶
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UINoticeRow : MonoBehaviour
{
    [SerializeField] private Button     m_button;
    [SerializeField] private TMP_Text   m_titleText;
    [SerializeField] private GameObject m_unreadDot; // 현재 버전 본문을 아직 안 열었을 때만 표시

    private int m_index;
    private System.Action<int> m_onClicked;

    private void Awake()
    {
        if (m_button != null)
            m_button.onClick.AddListener(OnClick);
    }

    public void Bind(int index, string title, bool isRead, System.Action<int> onClicked)
    {
        m_index = index;
        m_onClicked = onClicked;
        if (m_titleText != null)
            m_titleText.text = title;
        SetRead(isRead);
    }

    public void SetRead(bool isRead)
    {
        if (m_unreadDot != null)
            m_unreadDot.SetActive(isRead == false);
    }

    private void OnClick()
    {
        if (m_onClicked != null)
            m_onClicked.Invoke(m_index);
    }
}
