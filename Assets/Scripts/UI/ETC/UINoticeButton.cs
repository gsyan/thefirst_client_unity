// 메인 화면 공지사항 버튼 — 패널이 아니라 팝업(UIPopupNotice)을 열어서 UIPanelEntryButton 대신 별도 컴포넌트로 둠
using UnityEngine;
using UnityEngine.UI;

public class UINoticeButton : MonoBehaviour
{
    [SerializeField] private GameObject m_redDot; // 안 읽은 공지가 하나라도 있으면 표시(NoticeManager → EventManager.OnNoticeUnreadChanged)

    private Button m_button;

    private void Awake()
    {
        m_button = GetComponent<Button>();
        if (m_button != null)
            m_button.onClick.AddListener(OnClick);
        EventManager.Subscribe_NoticeUnreadChanged(OnNoticeUnreadChanged);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_NoticeUnreadChanged(OnNoticeUnreadChanged);
    }

    // 비활성 중 놓친 이벤트를 보정하기 위해 켜질 때마다 현재 값으로 갱신
    private void OnEnable()
    {
        bool hasUnread = NoticeManager.Instance != null && NoticeManager.Instance.HasUnread() == true;
        OnNoticeUnreadChanged(hasUnread);
    }

    private void OnNoticeUnreadChanged(bool hasUnread)
    {
        if (m_redDot != null)
            m_redDot.SetActive(hasUnread);
    }

    // 튜토리얼 dim 없는 스텝에서 메인 화면 버튼 클릭을 일괄 차단할 때 사용 (UIPanelSpace.OnTutorialGeneralUIBlockedChanged)
    public void SetInteractable(bool interactable)
    {
        if (m_button != null)
            m_button.interactable = interactable;
    }

    private void OnClick()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        UIManager.Instance.ShowNoticePopup();
    }
}
